using System.Text;
using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Cli;

/// <summary>
/// The <c>play</c> command (issue 11): a battle on one map, commands from a script file or
/// standard input, every event printed as a line and the board after every command. The
/// session renders only from events and asks the core for every number through
/// <see cref="Queries"/>, <see cref="Resolver"/>, and <see cref="EnemyAi"/>; it computes
/// nothing about the rules itself (DESIGN.md section 2). Output is plain ASCII.
/// Slots count from one at this boundary (issue 101): <c>show</c> lists them from 1 and
/// <c>attack</c>, <c>item</c>, and <c>forecast</c> read them that way; the core counts
/// from zero and is not told. A scripted run is a claim about a game, so it ends with a
/// summary of every rejected line, and <c>--strict</c> stops at the first one.
/// </summary>
public sealed class PlaySession
{
    public const string Usage = "usage: ironwake play <map-file|map-name> [--seed N] [--script file] [--strict] [--content dir]";

    /// <summary>The exit code of a <c>--strict</c> run stopped by a rejection: not a loss (1) and not a usage error (2).</summary>
    public const int StrictStop = 3;

    /// <summary>The refusal when the content directory has no cast file: the roster is content (issue 13), so nothing stands in for it.</summary>
    public const string NoCast = "content has no cast: " + ContentFiles.CastName + " is missing or empty";

    private const string Help = """
        commands:
          move <unit> <x,y>        move a unit to a tile in its reach
          attack <unit> <target> [slot]  attack an enemy in range, with the weapon in a slot (the forecast prints first)
          item <unit> <slot> [ally] use the item in a slot; a healing spell names the ally
          wait <unit>              end the unit's action
          end                      end the player phase; the enemy phase plays out
          recall <n>               rewind to history state n (spends a charge)
          forecast <unit> <target> [slot]  show the forecast without attacking
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
        var contentDir = "content";
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
                case "--content" when value is not null:
                    contentDir = value;
                    i++;
                    break;
                default:
                    Console.WriteLine($"ERROR: unexpected argument '{args[i]}'");
                    Console.WriteLine(Usage);
                    return 2;
            }
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

        var session = new PlaySession(content, BattleState.From(map, content, content.Cast, seed), Console.Out, scripted: script is not null);
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
        return stopped ? StrictStop : outcome.Result == BattleResult.Won ? 0 : 1;
    }

    private void Execute(string[] words)
    {
        switch (words[0])
        {
            case "move" when words.Length == 3 && TryCoord(words[2], out var to):
                Apply(new Move(words[1], to));
                break;
            case "move":
                Error("usage: move <unit> <x,y>");
                break;
            case "attack" when words.Length == 3 || (words.Length == 4 && int.TryParse(words[3], out _)):
                if (TrySlot(words[1], words.Length == 4 ? words[3] : null, out var attackSlot) && PrintForecast(words[1], words[2], attackSlot))
                {
                    Apply(new Attack(words[1], words[2], attackSlot));
                }

                break;
            case "attack":
                Error("usage: attack <unit> <target> [slot]");
                break;
            case "wait" when words.Length == 2:
                Apply(new Wait(words[1]));
                break;
            case "wait":
                Error("usage: wait <unit>");
                break;
            case "end" when words.Length == 1:
                if (Apply(new EndPhase()))
                {
                    EnemyPhase();
                }

                break;
            case "end":
                Error("usage: end");
                break;
            case "recall" when words.Length == 2 && int.TryParse(words[1], out var index):
                Apply(new Recall(index));
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
            case "forecast" when words.Length == 3 || (words.Length == 4 && int.TryParse(words[3], out _)):
                if (TrySlot(words[1], words.Length == 4 ? words[3] : null, out var forecastSlot))
                {
                    PrintForecast(words[1], words[2], forecastSlot);
                }

                break;
            case "forecast":
                Error("usage: forecast <unit> <target> [slot]");
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

    /// <summary>Plays the enemy phase from <see cref="EnemyAi.Plan"/>, one command at a time through the same resolver, and prints the board once it is over.</summary>
    private void EnemyPhase()
    {
        foreach (var command in EnemyAi.Plan(_state, _content))
        {
            _out.WriteLine("enemy: " + Describe(command));
            var result = Resolver.Apply(_state, _content, command);
            if (!result.Accepted)
            {
                throw new InvalidOperationException($"the enemy AI's {command} was rejected: {result.Rejection!.Message}");
            }

            _state = result.Next;
            foreach (var e in result.Events)
            {
                _out.WriteLine(Describe(e));
            }
        }

        _out.Write(MapRenderer.Render(_state, _content));
        AnnounceOutcome();
    }

    private void AnnounceOutcome()
    {
        var outcome = _state.Outcome;
        if (outcome.IsOver)
        {
            _out.WriteLine($"battle {(outcome.Result == BattleResult.Won ? "won" : "lost")}: {outcome.Reason}; only recall is left");
        }
    }

    private bool PrintForecast(string unitId, string targetId, int? slot)
    {
        if (Find(unitId) is not { } unit || Find(targetId) is not { } target)
        {
            return false;
        }

        var forecast = Queries.Forecast(_state, _content, unit, target, slot);
        if (forecast is null)
        {
            var (_, _, rejection) = Resolver.ChooseWeapon(unit, _content, slot);
            Error(rejection?.Message ?? $"{unit.Id} cannot attack {target.Id} from {unit.At}");
            return false;
        }

        _out.WriteLine($"forecast {unit.Id} -> {target.Id}: {Side(forecast.Attacker)}; counter: {(forecast.Defender.Strikes ? Side(forecast.Defender) : "none")}");
        return true;

        static string Side(SideForecast side) =>
            $"dmg {side.Damage}{(side.Doubles ? " x2" : "")} hit {side.DisplayedHit}% crit {side.CritChance}%";
    }

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
        var stats = unit.Unit.EffectiveStats(_content.Class(unit.Unit.ClassId));
        _out.WriteLine($"{unit.Id}: {unit.Unit.Name}, {_content.Class(unit.Unit.ClassId).Name} L{unit.Unit.Level}, at {unit.At} on {_state.Map.TerrainAt(unit.At, _content).Name}");
        _out.WriteLine($"  hp {unit.Hp}/{stats.Hp}  str {stats.Str} mag {stats.Mag} dex {stats.Dex} spd {stats.Spd} lck {stats.Lck} def {stats.Def} res {stats.Res} cha {stats.Cha}");
        _out.WriteLine($"  weapon: {WeaponLine(unit, _content)}");
        var slots = unit.Unit.Inventory.Items.Select((item, slot) => $"{slot + 1}: {Named(item.ItemId)} x{item.Uses}");
        _out.WriteLine($"  items: {(unit.Unit.Inventory.Count == 0 ? "none" : string.Join(", ", slots))}");
        var targets = string.Join(", ", Queries.Targets(_state, _content, unit).Select(t => t.Id));
        _out.WriteLine($"  targets from here: {(targets.Length == 0 ? "none" : targets)}");
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
            case UnitWaited w:
                return $"{w.UnitId} waits";
            case UnitHealed h:
                return $"{h.UnitId} heals {h.Amount} (hp {h.HpAfter})";
            case PhaseEnded p:
                return $"-- {p.Side.ToString().ToLowerInvariant()} phase ends, turn {p.Turn} --";
            case PhaseBegan p:
                return $"-- {p.Side.ToString().ToLowerInvariant()} phase, turn {p.Turn} --";
            case GroupWoke g:
                return $"group {g.Group} wakes: {g.Cause.ToString().ToLowerInvariant()}";
            case Recalled r:
                return $"recalled to state {r.ToIndex}; {r.ChargesLeft} charges left";
            case ItemUsed i:
                return $"{i.UnitId} uses {i.ItemId}" + (i.TargetId == i.UnitId ? "" : " on " + i.TargetId) + $" ({i.UsesLeft} left)";
            case WeaponEquipped w:
                return $"{w.UnitId} equips {w.ItemId}";
            case WeaponBroke b:
                return $"{b.UnitId}'s {b.ItemId} breaks";
            case SpellSpent s:
                return $"{s.UnitId}'s {s.ItemId} is spent for this battle";
            default:
                return e.ToString() ?? "?";
        }
    }
}
