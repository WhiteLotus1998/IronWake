using System.Text;
using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Cli;

/// <summary>
/// The <c>play</c> command (issue 11): a battle on one map, commands from a script file or
/// standard input, every event printed as a line and the board after every command. The
/// session renders only from events and asks the core for every number through
/// <see cref="Queries"/>, <see cref="Resolver"/>, and <see cref="EnemyAi"/>; it computes
/// nothing about the rules itself (DESIGN.md section 2). Output is plain ASCII. Every
/// attack the session resolves prints its forecast line first, the enemy phase's included
/// (issue 152): the odds behind a hit are on the transcript whichever side threw it, so a
/// reader can tell a misplay from bad luck. Slots count from one at this boundary (issue 101): <c>show</c> lists them from 1 and
/// <c>attack</c>, <c>item</c>, and <c>forecast</c> read them that way; the core counts
/// from zero and is not told. A scripted run is a claim about a game, so it ends with a
/// summary of every rejected line, and <c>--strict</c> stops at the first one.
/// <c>--scheme</c> picks the roll scheme through <see cref="RollSchemes"/>, the same word the
/// Sim's trace takes, and the header line names it, so a transcript says which game it is.
/// <c>--protocol</c> hands the battle to <see cref="ProtocolSession"/> instead: JSON lines in and out (issue 25).
/// </summary>
public sealed class PlaySession
{
    public const string Usage = "usage: ironwake play <map-file|map-name> [--seed N] [--script file] [--strict] [--content dir] [--scheme one|two] [--protocol]";

    /// <summary>The exit code of a <c>--strict</c> run stopped by a rejection: not a loss (1) and not a usage error (2).</summary>
    public const int StrictStop = 3;

    /// <summary>The refusal when the content directory has no cast file: the roster is content (issue 13), so nothing stands in for it.</summary>
    public const string NoCast = "content has no cast: " + ContentFiles.CastName + " is missing or empty";

    private const string Help = """
        commands:
          move <unit> <x,y>        move a unit to a tile in its reach
          attack <unit> <target> [slot] [art <id>]  attack an enemy in range, with the weapon in a slot, declaring a combat art (the forecast prints first)
          item <unit> <slot> [ally] use the item in a slot; a healing spell names the ally
          wait <unit>              end the unit's action
          end                      end the player phase; the enemy phase plays out, each enemy attack printing its forecast first
          recall <n>               rewind to history state n, a player-phase state (spends a charge)
          recall                   list the state each player turn started at, and the charges left
          forecast <unit> <target> [slot] [art <id>] [from <x,y>]  show the forecast without attacking, from any tile the unit can reach
          threat <unit> [from <x,y>]  what each enemy would strike it with next enemy phase, from where it stands or a tile it can reach
          reach <unit>             show the board with the unit's reachable tiles marked
          show <unit>              show a unit's numbers
          map                      show the board
          help                     this list
        slots count from 1, as show lists them
        a scripted run ends with a summary of every rejected line; --strict stops at the first
        """;

    private readonly GameContent _content;
    private readonly TextWriter _out;
    private readonly bool _scripted;
    private readonly List<(int Line, string Command, string Reason)> _rejections = new();

    /// <summary>
    /// Rivalry's exposure log (issue 16): every recruit that ended a player phase beside a
    /// rival on a tile an awake enemy could strike (<see cref="Rivalry.Exposed"/>, issue 209), with the history length when that phase ended, and whether an enemy attacked
    /// it in the enemy phase that followed. A Recall drops the entries it undoes.
    /// </summary>
    private readonly List<ExposureEntry> _exposure = new();
    private BattleState _state;
    private int _line;
    private string _command = "";

    private PlaySession(GameContent content, BattleState state, TextWriter output, bool scripted)
    {
        _content = content;
        _state = state;
        _out = output;
        _scripted = scripted;
    }

    /// <summary>Parses the arguments after <c>play</c>, runs the session, and returns the exit code: 0 on a win, 1 otherwise, 2 for a usage error.</summary>
    public static int Run(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine(Usage);
            return 2;
        }

        ulong seed = 1;
        string? script = null;
        var strict = false;
        var protocol = false;
        var contentDir = "content";
        var scheme = RollScheme.TwoRollAverage;
        for (var i = 1; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "--seed" when value is not null && ulong.TryParse(value, out seed):
                    i++;
                    break;
                case "--script" when value is not null:
                    script = value;
                    i++;
                    break;
                case "--strict":
                    strict = true;
                    break;
                case "--protocol":
                    protocol = true;
                    break;
                case "--content" when value is not null:
                    contentDir = value;
                    i++;
                    break;
                case "--scheme" when value is not null && RollSchemes.Parse(value) is { } parsedScheme:
                    scheme = parsedScheme;
                    i++;
                    break;
                default:
                    Console.WriteLine($"ERROR: unexpected argument '{args[i]}'");
                    Console.WriteLine(Usage);
                    return 2;
            }
        }

        if (strict && protocol)
        {
            Console.WriteLine("ERROR: --strict applies to a text script; a protocol run answers every line with ok true or false");
            Console.WriteLine(Usage);
            return 2;
        }

        if (strict && script is null)
        {
            Console.WriteLine("ERROR: --strict applies to a scripted run; give --script");
            Console.WriteLine(Usage);
            return 2;
        }

        GameContent content;
        MapDefinition map;
        try
        {
            content = ContentLoader.Load(contentDir);
            map = MapFiles.Load(ResolveMap(args[0], contentDir), content);
        }
        catch (Exception e) when (e is ContentException or MapException)
        {
            Console.WriteLine("ERROR: " + e.Message);
            return 1;
        }

        TextReader input;
        if (script is not null)
        {
            if (!File.Exists(script))
            {
                Console.WriteLine($"ERROR: script file '{script}' not found");
                return 2;
            }

            input = new StringReader(File.ReadAllText(script));
        }
        else
        {
            input = Console.In;
        }

        if (content.Cast.Count == 0)
        {
            Console.Error.WriteLine(NoCast);
            return 2;
        }

        if (protocol)
        {
            return new ProtocolSession(content, BattleState.From(map, content, content.Cast, seed, scheme), Console.Out).Run(input);
        }

        var session = new PlaySession(content, BattleState.From(map, content, content.Cast, seed, scheme), Console.Out, scripted: script is not null);
        return session.Play(input, strict, seed);
    }

    /// <summary>
    /// A bare map name resolves to <c>&lt;content&gt;/maps/&lt;name&gt;.map</c> when no file
    /// exists at the path given (issue 101), so <c>play the_tollgate</c> works the way the
    /// journals name maps. A path that exists is used as given.
    /// </summary>
    public static string ResolveMap(string mapArg, string contentDir)
    {
        if (File.Exists(mapArg))
        {
            return mapArg;
        }

        var named = Path.Combine(contentDir, "maps", mapArg + ".map");
        return File.Exists(named) ? named : mapArg;
    }

    private int Play(TextReader input, bool strict, ulong seed)
    {
        _out.WriteLine($"{_state.Map.Name}, seed {seed}, scheme {_state.Scheme}");
        _out.Write(MapRenderer.Render(_state, _content));
        var commands = 0;
        var stopped = false;
        while (input.ReadLine() is { } line)
        {
            _line++;
            var text = line.Trim();
            if (text.Length == 0 || text.StartsWith('#'))
            {
                continue;
            }

            if (_scripted)
            {
                _out.WriteLine("> " + text);
            }

            _command = text;
            commands++;
            Execute(text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (strict && _rejections.Count > 0)
            {
                _out.WriteLine($"strict: stopped at line {_line} ({text}); no later command applied");
                stopped = true;
                break;
            }
        }

        if (_scripted && _rejections.Count > 0)
        {
            _out.WriteLine($"rejected {_rejections.Count} of {commands} commands:");
            foreach (var (at, command, reason) in _rejections)
            {
                _out.WriteLine($"  line {at}: {command}: {reason}");
            }
        }

        var outcome = _state.Outcome;
        _out.WriteLine(outcome.IsOver
            ? $"battle {(outcome.Result == BattleResult.Won ? "won" : "lost")}: {outcome.Reason}"
            : $"battle ongoing at turn {_state.Turn}, {_state.Phase.ToString().ToLowerInvariant()} phase");
        if (_state.Map.RivalryArm is { } arm)
        {
            var attacked = _exposure.Count(entry => entry.Attacked);
            _out.WriteLine($"rivalry ({arm}): {_exposure.Count} threatened player phases ended beside a rival, {attacked} of them attacked in the enemy phase after");
        }

        return stopped ? StrictStop : outcome.Result == BattleResult.Won ? 0 : 1;
    }

    private void Execute(string[] words)
    {
        var art = words[0] is "attack" or "forecast" ? TakeArt(ref words) : null;
        switch (words[0])
        {
            case "move" when words.Length == 3 && TryCoord(words[2], out var to):
                Apply(new Move(words[1], to));
                break;
            case "move":
                Error("usage: move <unit> <x,y>");
                break;
            case "attack" when words.Length == 3 || (words.Length == 4 && int.TryParse(words[3], out _)):
                if (TrySlot(words[1], words.Length == 4 ? words[3] : null, out var attackSlot) && PrintForecast(words[1], words[2], attackSlot, art))
                {
                    Apply(new Attack(words[1], words[2], attackSlot, art));
                }

                break;
            case "attack":
                Error("usage: attack <unit> <target> [slot] [art <id>]");
                break;
            case "wait" when words.Length == 2:
                Apply(new Wait(words[1]));
                break;
            case "wait":
                Error("usage: wait <unit>");
                break;
            case "end" when words.Length == 1:
                var exposed = _state.Map.RivalryArm is not null && _state.Phase == Side.Player && !_state.Outcome.IsOver
                    ? Rivalry.Exposed(_state, _content).Select(u => new ExposureEntry(_state.History.Count, _state.Turn, u.Id)).ToList()
                    : new List<ExposureEntry>();
                if (Apply(new EndPhase()))
                {
                    _exposure.AddRange(exposed);
                    EnemyPhase();
                }

                break;
            case "end":
                Error("usage: end");
                break;
            case "recall" when words.Length == 2 && int.TryParse(words[1], out var index):
                if (Apply(new Recall(index)))
                {
                    _exposure.RemoveAll(entry => entry.HistoryAt >= index);
                }

                break;
            case "recall" when words.Length == 1:
                ListRecallTargets();
                break;
            case "recall":
                Error("usage: recall <n>  (history holds " + _state.History.Count + " states)");
                break;
            case "item" when words.Length is 3 or 4 && int.TryParse(words[2], out _):
                if (TrySlot(words[1], words[2], out var itemSlot))
                {
                    Apply(new UseItem(words[1], itemSlot!.Value, words.Length == 4 ? words[3] : null));
                }

                break;
            case "item":
                Error("usage: item <unit> <slot> [ally]");
                break;
            case "forecast" when TryForecastWords(words, out var slotText, out var from):
                if (TrySlot(words[1], slotText, out var forecastSlot))
                {
                    PrintForecast(words[1], words[2], forecastSlot, art, from);
                }

                break;
            case "forecast":
                Error("usage: forecast <unit> <target> [slot] [art <id>] [from <x,y>]");
                break;
            case "threat" when words.Length == 2:
                PrintThreat(words[1], null);
                break;
            case "threat" when words.Length == 4 && words[2] == "from" && TryCoord(words[3], out var threatFrom):
                PrintThreat(words[1], threatFrom);
                break;
            case "threat":
                Error("usage: threat <unit> [from <x,y>]");
                break;
            case "reach" when words.Length == 2:
                if (Find(words[1]) is { } mover)
                {
                    _out.Write(MapRenderer.Render(_state, _content, Queries.Reachable(_state, _content, mover)));
                }

                break;
            case "reach":
                Error("usage: reach <unit>");
                break;
            case "show" when words.Length == 2:
                if (Find(words[1]) is { } unit)
                {
                    Show(unit);
                }

                break;
            case "show":
                Error("usage: show <unit>");
                break;
            case "map":
                _out.Write(MapRenderer.Render(_state, _content));
                break;
            case "help":
                _out.WriteLine(Help);
                break;
            default:
                Error($"unknown command '{words[0]}'; type help");
                break;
        }
    }

    private bool Apply(Command command)
    {
        var result = Resolver.Apply(_state, _content, command);
        if (!result.Accepted)
        {
            Error(result.Rejection!.Message);
            return false;
        }

        _state = result.Next;
        foreach (var e in result.Events)
        {
            _out.WriteLine(Describe(e));
        }

        if (command is not EndPhase)
        {
            _out.Write(MapRenderer.Render(_state, _content));
            AnnounceOutcome();
        }

        return true;
    }

    /// <summary>
    /// Plays the enemy phase from <see cref="EnemyAi.Plan"/>, one command at a time through
    /// the same resolver, and prints the board once it is over. An enemy attack prints the
    /// same forecast line a player's attack does, asked of the core on the board the
    /// resolver is about to read, so the transcript carries the odds of every combat and
    /// not only the player's half (issue 152). The planner only emits attacks the core
    /// forecasts, so a null forecast here is a harness fault, like a rejected command.
    /// </summary>
    private void EnemyPhase()
    {
        foreach (var command in EnemyAi.Plan(_state, _content))
        {
            _out.WriteLine("enemy: " + Describe(command));
            if (command is Attack attack)
            {
                var attacker = _state.Find(attack.UnitId);
                var target = _state.Find(attack.TargetId);
                var forecast = attacker is null || target is null ? null : Queries.Forecast(_state, _content, attacker, target, attacker.At, attack.Slot);
                if (forecast is null)
                {
                    throw new InvalidOperationException($"the enemy AI's {command} has no forecast");
                }

                _out.WriteLine(ForecastLine(attacker!, target!, forecast));
                PrintRivalry(target!, countering: true);
            }

            var result = Resolver.Apply(_state, _content, command);
            if (!result.Accepted)
            {
                throw new InvalidOperationException($"the enemy AI's {command} was rejected: {result.Rejection!.Message}");
            }

            _state = result.Next;
            foreach (var e in result.Events)
            {
                _out.WriteLine(Describe(e));
                if (e is CombatFought fought)
                {
                    foreach (var entry in _exposure.Where(x => x.UnitId == fought.TargetId && x.Turn == fought.Turn))
                    {
                        entry.Attacked = true;
                    }
                }
            }
        }

        _out.Write(MapRenderer.Render(_state, _content));
        AnnounceOutcome();
    }

    /// <summary>
    /// Prints the history index at which each player phase in the history began, so a
    /// player can find the n for <c>recall n</c> (issue 190). Spends nothing.
    /// </summary>
    private void ListRecallTargets()
    {
        var starts = _state.RecallTargets()
            .Where(i => i == 0 || _state.History[i - 1].Phase != Side.Player)
            .Select(i => $"turn {_state.History[i].Turn} state {i}")
            .ToList();
        var list = starts.Count == 0 ? "none yet" : string.Join(", ", starts);
        _out.WriteLine($"player turns start at: {list}; history holds {_state.History.Count} states; {_state.RecallCharges} charges left");
    }

    private void AnnounceOutcome()
    {
        var outcome = _state.Outcome;
        if (outcome.IsOver)
        {
            _out.WriteLine($"battle {(outcome.Result == BattleResult.Won ? "won" : "lost")}: {outcome.Reason}; only recall is left");
        }
    }

    /// <summary>
    /// Takes <c>art &lt;id&gt;</c> out of an <c>attack</c> or <c>forecast</c> line (issue 68),
    /// after the unit and the target, and returns the id; the remaining words parse as
    /// before. A trailing <c>art</c> with no id is left for the usage error.
    /// </summary>
    private static string? TakeArt(ref string[] words)
    {
        for (var i = 3; i + 1 < words.Length; i++)
        {
            if (words[i] == "art")
            {
                var id = words[i + 1];
                words = words.Take(i).Concat(words.Skip(i + 2)).ToArray();
                return id;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the words after <c>forecast &lt;unit&gt; &lt;target&gt;</c>: an optional slot, then
    /// an optional <c>from &lt;x,y&gt;</c> (issue 151). False when they are anything else.
    /// </summary>
    private static bool TryForecastWords(string[] words, out string? slotText, out Coord? from)
    {
        slotText = null;
        from = null;
        if (words.Length < 3)
        {
            return false;
        }

        var next = 3;
        if (next < words.Length && int.TryParse(words[next], out _))
        {
            slotText = words[next];
            next++;
        }

        if (next < words.Length)
        {
            if (words[next] != "from" || next + 1 != words.Length - 1 || !TryCoord(words[next + 1], out var at))
            {
                return false;
            }

            from = at;
            next += 2;
        }

        return next == words.Length;
    }

    /// <summary>
    /// Prints the forecast line, from the unit's own tile or from <paramref name="from"/>,
    /// a tile it could still move to (issue 151). The from-tile line names the tile and
    /// its terrain, since the terrain is what the player is choosing between.
    /// </summary>
    private bool PrintForecast(string unitId, string targetId, int? slot, string? art, Coord? from = null)
    {
        if (Find(unitId) is not { } unit || Find(targetId) is not { } target)
        {
            return false;
        }

        var tile = from ?? unit.At;
        if (tile != unit.At && !Queries.CanStandOn(_state, _content, unit, tile))
        {
            Error(unit.Moved ? $"{unit.Id} has already moved this phase; forecast from {unit.At}" : $"{unit.Id} cannot move to {tile}");
            return false;
        }

        var forecast = Queries.Forecast(_state, _content, unit, target, tile, slot, art);
        if (forecast is null)
        {
            Error(Queries.WeaponRefusal(_content, unit, slot, art)?.Message ?? $"{unit.Id} cannot attack {target.Id} from {tile}");
            return false;
        }

        _out.WriteLine(ForecastText(_state, _content, unit, target, forecast, tile, from is not null, slot, art));
        return true;
    }

    /// <summary>
    /// Everything the console prints for a forecast, one line per row: the forecast line,
    /// then the art line, the rivalry line and the pending-retreat lines when they apply. Shared with the
    /// protocol's forecast query (issue 25), whose <c>text</c> is exactly this.
    /// <paramref name="fromTile"/> is true for a forecast asked from a tile the unit has not
    /// moved to, whose line names the tile and its terrain.
    /// </summary>
    public static string ForecastText(BattleState state, GameContent content, BattleUnit unit, BattleUnit target, CombatForecast forecast, Coord tile, bool fromTile, int? slot = null, string? art = null)
    {
        var where = fromTile ? $" from {tile} ({state.Map.TerrainAt(tile, content).Name})" : "";
        var lines = new List<string> { ForecastLine(unit, target, forecast, where) };
        if (art is not null)
        {
            lines.Add(ArtLine(content, unit, forecast, slot, art));
        }

        if (RivalryLine(state, content, unit with { At = tile }, countering: false) is { } rivalry)
        {
            lines.Add(rivalry);
        }

        lines.AddRange(PendingRetreatLines(state, content, unit, tile, target, forecast));
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Under a forecast of a combat art (issue 68): the art, the weapon as the art makes it,
    /// and the most uses the attack spends against the uses the weapon has, the art's cost
    /// included, since that is paid whether the strike lands or not.
    /// </summary>
    private static string ArtLine(GameContent content, BattleUnit unit, CombatForecast forecast, int? slot, string art)
    {
        var (armed, weapon, _) = Resolver.ChooseWeapon(unit, content, slot);
        var ability = content.Ability(art);
        var struck = ((CombatArtEffect)ability.Effect).Apply(weapon!);
        var uses = armed.Unit.Inventory.Items[armed.EquippedSlot(content)].Uses;
        return $"  art {ability.Name}: {struck.Name} at mt {struck.Mt} hit {struck.Hit} crit {struck.Crit} wt {struck.Wt} range {struck.MinRange}-{struck.MaxRange}; spends up to {forecast.AttackerSpendsAtMost} of {uses} uses, {forecast.ArtCost} of them hit or miss";
    }

    /// <summary>
    /// On a retreat map, the line under a forecast that says where the target would fall
    /// back to if the strike leaves it alive and below the threshold (issue 215), through
    /// <see cref="RetreatRule.Pending"/>: once per distinct HP the attacker's landed strikes
    /// can leave, one hit and, when it doubles, two, crits aside. Silent when no outcome
    /// sends it anywhere.
    /// </summary>
    private static IEnumerable<string> PendingRetreatLines(BattleState state, GameContent content, BattleUnit unit, Coord tile, BattleUnit target, CombatForecast forecast)
    {
        if (!state.Map.RetreatEnabled || !forecast.Attacker.Strikes)
        {
            yield break;
        }

        var hits = forecast.Attacker.Doubles ? new[] { 1, 2 } : new[] { 1 };
        foreach (var hpAfter in hits.Select(n => target.Hp - n * forecast.Attacker.Damage).Distinct())
        {
            if (RetreatRule.Pending(state, content, unit, tile, target, hpAfter) is { } refuge)
            {
                yield return $"  {target.Id} would fall back to {refuge} at {hpAfter} hp";
            }
        }
    }

    /// <summary>
    /// Prints what the coming enemy phase could do to a player unit standing where it is
    /// or on <paramref name="from"/> (issue 217), through <see cref="Queries.Threats"/>:
    /// one line per enemy with the weapon the planner would swing, its slot counted from
    /// one, the tile it strikes from, and the forecast line the enemy phase would print,
    /// then the total if every strike lands against the unit's HP. Spends nothing.
    /// </summary>
    private void PrintThreat(string unitId, Coord? from)
    {
        if (Find(unitId) is not { } unit)
        {
            return;
        }

        if (unit.Side != Ironwake.Core.Side.Player)
        {
            Error($"{unit.Id} is an enemy; threat answers for a player unit");
            return;
        }

        var tile = from ?? unit.At;
        if (Queries.Threats(_state, _content, unit, tile) is not { } lines)
        {
            Error(unit.Moved ? $"{unit.Id} has already moved this phase; threat from {unit.At}" : $"{unit.Id} cannot move to {tile}");
            return;
        }

        _out.WriteLine(ThreatText(_state, _content, unit, tile, lines));
    }

    /// <summary>What <c>threat</c> prints for <see cref="Queries.Threats"/>' lines, one row per line; the protocol's threat query carries it as its <c>text</c> (issue 25).</summary>
    public static string ThreatText(BattleState state, GameContent content, BattleUnit unit, Coord tile, IReadOnlyList<ThreatLine> lines)
    {
        var where = $"{tile} ({state.Map.TerrainAt(tile, content).Name})";
        if (lines.Count == 0)
        {
            return $"threat on {unit.Id} at {where}: no enemy can strike it next phase";
        }

        var rows = new List<string> { $"threat on {unit.Id} at {where}:" };
        foreach (var line in lines)
        {
            rows.Add($"  {line.Enemy.Id} from {line.From} with {line.Weapon.Name} (slot {line.Slot + 1}): {StrikeText(line.Forecast.Attacker)}; counter: {(line.Forecast.Defender.Strikes ? StrikeText(line.Forecast.Defender) : "none")}");
        }

        rows.Add($"  if all land: {lines.Sum(l => l.IfAllLand)} against {unit.Hp} hp");
        return string.Join("\n", rows);
    }

    /// <summary>
    /// The one forecast line, printed before an attack from either side and by the
    /// <c>forecast</c> command: the attacker's strike, then the counter or <c>none</c>.
    /// <paramref name="where"/> is the tile suffix of a forecast asked from a tile the
    /// unit has not moved to (issue 151), empty for a forecast on the standing board.
    /// </summary>
    public static string ForecastLine(BattleUnit unit, BattleUnit target, CombatForecast forecast, string where = "")
    {
        return $"forecast {unit.Id} -> {target.Id}{where}: {StrikeText(forecast.Attacker)}; counter: {(forecast.Defender.Strikes ? StrikeText(forecast.Defender) : "none")}";
    }

    /// <summary>One side of a forecast as the console prints it: damage, doubles, displayed hit, and crit.</summary>
    private static string StrikeText(SideForecast side) =>
        $"dmg {side.Damage}{(side.Doubles ? " x2" : "")} hit {side.DisplayedHit}% crit {side.CritChance}%";

    /// <summary>
    /// Reads a one-based slot typed by the player into the core's zero-based one. Null text
    /// is no slot. A slot outside 1..count is refused here, naming the range the player
    /// sees, so the core's zero-based message never reaches the screen.
    /// </summary>
    private bool TrySlot(string unitId, string? text, out int? slot)
    {
        slot = null;
        if (text is null)
        {
            return true;
        }

        if (Find(unitId) is not { } unit)
        {
            return false;
        }

        var typed = int.Parse(text);
        var count = unit.Unit.Inventory.Count;
        if (typed < 1 || typed > count)
        {
            Error(count == 0 ? $"{unit.Id} carries nothing" : $"{unit.Id} has nothing in slot {typed}; slots run 1-{count}");
            return false;
        }

        slot = typed - 1;
        return true;
    }

    /// <summary>
    /// The <c>show</c> weapon line: the equipped weapon's numbers, or <c>unarmed</c> for a
    /// unit with nothing usable, with <c>(spell spent)</c> when a spell its class could
    /// cast sits at zero uses (issue 101; DECISIONS/0018 item 3).
    /// </summary>
    public static string WeaponLine(BattleUnit unit, GameContent content)
    {
        var weapon = unit.EquippedWeapon(content);
        if (weapon is not null)
        {
            return $"{weapon.Name} (mt {weapon.Mt} hit {weapon.Hit} crit {weapon.Crit} wt {weapon.Wt} range {weapon.MinRange}-{weapon.MaxRange}){(unit.WeaponBroken(content) ? " broken: -5 mt -10 hit" : "")}";
        }

        var unitClass = content.Class(unit.Unit.ClassId);
        var spent = unit.Unit.Inventory.Items.Any(item =>
            item.Uses == 0 && content.Weapons.TryGetValue(item.ItemId, out var w) && w.IsMagic && !w.Heals && unitClass.CanUse(w.Type));
        return spent ? "unarmed (spell spent)" : "unarmed";
    }

    private void Show(BattleUnit unit)
    {
        var stats = _content.StatsOf(unit.Unit);
        _out.WriteLine($"{unit.Id}: {unit.Unit.Name}, {_content.Class(unit.Unit.ClassId).Name} L{unit.Unit.Level}, at {unit.At} on {_state.Map.TerrainAt(unit.At, _content).Label(stats.Hp)}");
        _out.WriteLine($"  hp {unit.Hp}/{stats.Hp}  str {stats.Str} mag {stats.Mag} dex {stats.Dex} spd {stats.Spd} lck {stats.Lck} def {stats.Def} res {stats.Res} cha {stats.Cha}");
        _out.WriteLine($"  weapon: {WeaponLine(unit, _content)}");
        var slots = unit.Unit.Inventory.Items.Select((item, slot) => $"{slot + 1}: {Named(item.ItemId)} x{item.Uses}");
        _out.WriteLine($"  items: {(unit.Unit.Inventory.Count == 0 ? "none" : string.Join(", ", slots))}");
        var ranks = _content.Class(unit.Unit.ClassId).Weapons
            .Select(type => $"{type.ToString().ToLowerInvariant()} {unit.Unit.Skill.Rank(type)} ({unit.Unit.Skill.Points(type)})");
        _out.WriteLine($"  ranks: {string.Join(", ", ranks)}");
        var arts = _content.ArtsOf(unit.Unit).Select(a =>
            $"{a.Ability.Id} ({a.Art.Weapon.ToString().ToLowerInvariant()} {a.Art.Rank}, cost {a.Art.Cost}): {a.Ability.Text}").ToList();
        if (arts.Count > 0)
        {
            _out.WriteLine($"  arts: {string.Join(", ", arts)}");
        }

        var held = _content.AbilitiesOf(unit.Unit).Where(a => a.Effect is not CombatArtEffect).Select(a => $"{a.Name} ({a.Text.TrimEnd('.')})").ToList();
        if (held.Count > 0)
        {
            _out.WriteLine($"  abilities: {string.Join(", ", held)}");
        }

        if (MasteryLine(unit, _content) is { } mastery)
        {
            _out.WriteLine(mastery);
        }

        var targets = string.Join(", ", Queries.Targets(_state, _content, unit).Select(t => t.Id));
        _out.WriteLine($"  targets from here: {(targets.Length == 0 ? "none" : targets)}");
        if (_state.Map.RivalryArm is not null && Rivalry.IsRecruit(unit))
        {
            var rivals = _state.UnitsOf(Side.Player)
                .Where(other => Rivalry.AreRivals(_state, _content, unit, other))
                .Select(other => $"{other.Id} {Rivalry.PointsOf(_state, unit.Id, other.Id)}/{_content.Rivalry.OverwriteAt}");
            var list = string.Join(", ", rivals);
            _out.WriteLine($"  {unit.Unit.Region}; rapport {Rivalry.RateOf(unit, _content)} per phase beside a recruit; rivals: {(list.Length == 0 ? "none" : list)}");
        }
    }

    /// <summary>
    /// The mastery line of <c>show</c> (issue 69): the class's mastery ability and the unit's
    /// points against the requirement, or that it is mastered. Silent for a class with none.
    /// </summary>
    public static string? MasteryLine(BattleUnit unit, GameContent content)
    {
        var unitClass = content.Class(unit.Unit.ClassId);
        if (unitClass.Mastery is not { } id)
        {
            return null;
        }

        var name = content.Ability(id).Name;
        return unit.Unit.Abilities.Contains(id)
            ? $"  mastery: {name}, mastered"
            : $"  mastery: {name} ({unit.Unit.Mastery.Points(unitClass.Id)} of {unitClass.MasteryPoints} combats)";
    }

    /// <summary>
    /// On a rivalry map, the line under a forecast that names what rivalry changed on the
    /// player's side: the rivals beside the unit and the modifiers the forecast included.
    /// Silent when no rival is adjacent.
    /// </summary>
    private void PrintRivalry(BattleUnit unit, bool countering)
    {
        if (RivalryLine(_state, _content, unit, countering) is { } line)
        {
            _out.WriteLine(line);
        }
    }

    private static string? RivalryLine(BattleState state, GameContent content, BattleUnit unit, bool countering)
    {
        if (Rivalry.ArmOf(state, content) is null || Rivalry.AdjacentRivals(state, content, unit) is not { Count: > 0 } rivals)
        {
            return null;
        }

        var (hit, crit, critAvoid) = Rivalry.Modifiers(state, content, unit, countering);
        return $"  rivalry: {unit.Id} beside {string.Join(", ", rivals.Select(r => r.Id))}: hit {hit:+0;-0;0} crit {crit:+0;-0;0} crit avoid {critAvoid:+0;-0;0}";
    }

    private string Named(string itemId) =>
        _content.Items.TryGetValue(itemId, out var item) ? item.Name : _content.Weapon(itemId).Name;

    private BattleUnit? Find(string id)
    {
        var unit = _state.Find(id);
        if (unit is null)
        {
            Error($"no living unit '{id}'");
        }

        return unit;
    }

    private void Error(string message)
    {
        _out.WriteLine("ERROR: " + message);
        if (_scripted)
        {
            _rejections.Add((_line, _command, message));
        }
    }

    private static bool TryCoord(string text, out Coord at)
    {
        at = default;
        var parts = text.Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
        {
            return false;
        }

        at = new Coord(x, y);
        return true;
    }

    private static string Describe(Command command) => command switch
    {
        Move m => $"move {m.UnitId} {m.To}",
        Attack a => $"attack {a.UnitId} {a.TargetId}" + (a.Slot is null ? "" : " " + (a.Slot + 1)),
        Wait w => $"wait {w.UnitId}",
        Retreat r => $"retreat {r.UnitId} {r.To}",
        EndPhase => "end",
        Recall r => $"recall {r.ToIndex}",
        UseItem i => $"item {i.UnitId} {i.Slot + 1}" + (i.TargetId is null ? "" : " " + i.TargetId),
        _ => command.ToString() ?? "?",
    };

    /// <summary>One line per event, the words a transcript reader sees.</summary>
    public static string Describe(GameEvent e)
    {
        switch (e)
        {
            case UnitMoved m:
                return $"{m.UnitId} moves {m.From} -> {m.To}" + (m.Path.Count > 1 ? " via " + string.Join(" ", m.Path.Take(m.Path.Count - 1)) : "");
            case CombatFought f:
                var sb = new StringBuilder();
                sb.Append($"{f.AttackerId} attacks {f.TargetId}");
                foreach (var strike in f.Strikes)
                {
                    sb.Append('\n').Append("  ").Append(strike.AttackerId).Append(' ')
                        .Append(!strike.Hit ? "misses " + strike.TargetId : $"{(strike.Crit ? "crits" : "hits")} {strike.TargetId} for {strike.Damage} (hp {strike.TargetHpAfter})");
                }

                sb.Append('\n').Append($"  {f.AttackerId} hp {f.AttackerHpAfter}, {f.TargetId} hp {f.TargetHpAfter}");
                return sb.ToString();
            case UnitDied d:
                return $"{d.UnitId} falls at {d.At}";
            case ExpGained x:
                return $"{x.UnitId} gains {x.Amount} exp ({x.ExpAfter})";
            case LeveledUp l:
                var rose = string.Join(" ", Stats.All.Where(stat => l.Gains.Get(stat) > 0).Select(stat => stat.ToString().ToLowerInvariant() + " +1"));
                return $"{l.UnitId} reaches level {l.NewLevel}: {(rose.Length == 0 ? "nothing rose" : rose)}";
            case RankRaised k:
                return $"{k.UnitId} reaches rank {k.Rank} in {k.Type.ToString().ToLowerInvariant()}";
            case MasteryEarned m:
                return $"{m.UnitId} masters the {m.ClassId} class and keeps {m.AbilityId}";
            case UnitWaited w:
                return $"{w.UnitId} waits";
            case UnitRetreated r:
                return $"{r.UnitId} falls back to {r.To} and will not fight this phase";
            case UnitHealed h:
                return $"{h.UnitId} heals {h.Amount} (hp {h.HpAfter})";
            case PhaseEnded p:
                return $"-- {p.Side.ToString().ToLowerInvariant()} phase ends, turn {p.Turn} --";
            case PhaseBegan p:
                return $"-- {p.Side.ToString().ToLowerInvariant()} phase, turn {p.Turn} --";
            case GroupWoke g:
                return $"group {g.Group} wakes: {g.Cause.ToString().ToLowerInvariant()}";
            case MapEventFired m:
                return $"event {m.Name}" + (m.Blocked ? " is blocked: its tile is held" : "");
            case TerrainChanged t:
                return $"  {t.At} becomes {t.TerrainId}";
            case UnitSpawned u:
                return $"  {u.UnitId} arrives at {u.At}, group {u.Group}, {u.Behavior.ToString().ToLowerInvariant()}";
            case FlagSet f:
                return $"  flag {f.Flag} is set";
            case RapportGained g:
                return $"rapport {g.A} and {g.B} +{g.Amount} ({g.Total}{(g.OutOf is { } outOf ? $" of {outOf}" : "")})";
            case RivalryEnded r:
                return $"{r.A} and {r.B} are rivals no longer";
            case Recalled r:
                return $"recalled to state {r.ToIndex}; {r.ChargesLeft} charges left";
            case ItemUsed i:
                return $"{i.UnitId} uses {i.ItemId}" + (i.TargetId == i.UnitId ? "" : " on " + i.TargetId) + $" ({i.UsesLeft} left)";
            case WeaponEquipped w:
                return $"{w.UnitId} equips {w.ItemId}";
            case ArtDeclared a:
                return $"{a.UnitId} declares {a.ArtId} with {a.ItemId}, spending {a.Cost} extra uses";
            case WeaponBroke b:
                return $"{b.UnitId}'s {b.ItemId} breaks";
            case SpellSpent s:
                return $"{s.UnitId}'s {s.ItemId} is spent for this battle";
            default:
                return e.ToString() ?? "?";
        }
    }
}

/// <summary>One recruit that ended a threatened player phase beside a rival (issues 16 and 209), and whether the enemy phase after struck it.</summary>
internal sealed class ExposureEntry
{
    public ExposureEntry(int historyAt, int turn, string unitId)
    {
        HistoryAt = historyAt;
        Turn = turn;
        UnitId = unitId;
    }

    public int HistoryAt { get; }

    public int Turn { get; }

    public string UnitId { get; }

    public bool Attacked { get; set; }
}
