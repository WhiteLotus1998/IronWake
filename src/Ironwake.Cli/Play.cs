using System.Text;
using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Cli;

/// <summary>One row of <c>recall list</c> (issue 75): the history state a click on it recalls, or null for a heading, and the console's text.</summary>
public sealed record RecallRow(int? State, string Text);

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
    public const string Usage = "usage: ironwake play <map-file|map-name> [--seed N] [--script file] [--strict] [--content dir] [--scheme one|two] [--protocol [--omniscient]] [--candidate id] [--log file]";

    /// <summary>The exit code of a <c>--strict</c> run stopped by a rejection: not a loss (1) and not a usage error (2).</summary>
    public const int StrictStop = 3;

    /// <summary>The refusal when the content directory has no cast file: the roster is content (issue 13), so nothing stands in for it.</summary>
    public const string NoCast = "content has no cast: " + ContentFiles.CastName + " is missing or empty";

    private const string Help = """
        commands:
          move <unit> <x,y>        move a unit to a tile in its reach
          attack <unit> <target> [slot|weapon] [art <id>]  attack an enemy in range, with the weapon in a slot or named, declaring a combat art (the forecast prints first)
          item <unit> <slot> [ally] use the item in a slot; a healing spell names the ally
          wait <unit>              end the unit's action
          canto <unit> <x,y|stay>  after acting, a unit with Canto moves on what its move left, or stays
          exit <unit>              on an Escape map, leave the board from an exit as the unit's action; the captain's exit ends the battle
          recover <unit>           on a keepsakes map, take the weapon a fallen ally left on the unit's tile, as its action
          shove <unit> <target>    on a shove map, push an adjacent ally one tile away, as the action
          cover <unit> <ally>      on a cover map, take the first strike aimed at the ally beside it, as the action
          watch <unit>             on an overwatch map, strike the first foe to end a move in the unit's ring, as the action
          end                      end the player phase; the enemy phase plays out, each enemy attack printing its forecast first
          recall <n>               rewind to history state n, a player-phase state (spends a charge), printing what it undoes
          recall list              every state recall can return to, the command that made it, and what a rewind there gives back
          recall                   list the state each player turn started at, and the charges left
          forecast <unit> <target> [slot|weapon] [art <id>] [from <x,y>]  show the forecast without attacking, from any tile the unit can reach
          threat <unit> [from <x,y>]  what each enemy would strike it with next enemy phase, from where it stands or a tile it can reach, on the board as it stands now (a foe freed by a kill mid-phase is not counted)
          reach <unit>             show the board with the unit's reachable tiles marked
          show <unit>              show a unit's numbers
          map                      show the board
          help                     this list
        slots count from 1, as show lists them; a weapon may be named instead, by id or name (gust, iron bow)
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

    /// <summary>
    /// The command applied from each history state, by index (issue 75): entry i is what
    /// turned history state i into the next one, so <c>recall list</c> can name the command
    /// that made each state. A Recall drops the entries it undoes.
    /// </summary>
    private readonly List<string> _made = new();
    private BattleState _state;
    private int _line;

    /// <summary>
    /// The event log (issue 347): every event line the console prints, in order and nothing
    /// else, the same lines a renderer's event log prints. <c>--log</c> writes it to a file,
    /// which is what the thin renderer's parity gate compares against byte for byte.
    /// </summary>
    private readonly StringWriter _log = new() { NewLine = "\n" };

    /// <summary>The event lines printed so far, each ending in <c>\n</c>.</summary>
    internal string EventLog => _log.ToString();

    /// <summary>Prints one event line to the console and to the event log.</summary>
    private void WriteEvent(string line)
    {
        _out.WriteLine(line);
        _log.WriteLine(line);
    }
    private string _command = "";

    private PlaySession(GameContent content, BattleState state, TextWriter output, bool scripted)
    {
        _content = content;
        _state = state;
        _out = output;
        _scripted = scripted;
    }

    /// <summary>
    /// A battle inside a campaign (issue 74): the same commands, plus <c>leave</c>, which ends a
    /// decided battle and hands the board back to <see cref="CampaignSession"/>. Script lines count
    /// on from <paramref name="line"/>, so a rejection names its line in the whole campaign script.
    /// </summary>
    internal PlaySession(GameContent content, BattleState state, TextWriter output, bool scripted, int line)
        : this(content, state, output, scripted)
    {
        _campaign = true;
        _line = line;
    }

    private readonly bool _campaign;

    /// <summary>The board as it stands.</summary>
    internal BattleState State => _state;

    /// <summary>The last script line read.</summary>
    internal int Line => _line;

    /// <summary>Every rejected line, with its line number and reason.</summary>
    internal IReadOnlyList<(int Line, string Command, string Reason)> Rejections => _rejections;

    /// <summary>Whether a campaign battle has been left with <c>leave</c>.</summary>
    internal bool Left { get; private set; }

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
        var omniscient = false;
        var contentDir = "content";
        var scheme = RollScheme.TwoRollAverage;
        string? candidate = null;
        string? log = null;
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
                case "--omniscient":
                    omniscient = true;
                    break;
                case "--content" when value is not null:
                    contentDir = value;
                    i++;
                    break;
                case "--candidate" when value is not null:
                    candidate = value;
                    i++;
                    break;
                case "--log" when value is not null:
                    log = value;
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

        if (omniscient && !protocol)
        {
            Console.WriteLine("ERROR: --omniscient applies to a protocol run; the console always shows the player's view");
            Console.WriteLine(Usage);
            return 2;
        }

        if (strict && protocol)
        {
            Console.WriteLine("ERROR: --strict applies to a text script; a protocol run answers every line with ok true or false");
            Console.WriteLine(Usage);
            return 2;
        }

        if (log is not null && protocol)
        {
            Console.WriteLine("ERROR: --log applies to a text run; a protocol run carries each event's text in its answer");
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

        var roster = content.Cast;
        if (candidate is not null)
        {
            if (map.Certification is null)
            {
                Console.WriteLine($"ERROR: --candidate is for a certification map, and '{map.Name}' has no certification header");
                return 2;
            }

            if (content.Cast.FirstOrDefault(u => u.Id == candidate) is not { } unit)
            {
                Console.WriteLine($"ERROR: --candidate names '{candidate}', who is not in the cast");
                return 2;
            }

            roster = ValueList<Unit>.From(new[] { unit });
        }

        if (protocol)
        {
            return new ProtocolSession(content, BattleState.From(map, content, roster, seed, scheme), Console.Out, omniscient).Run(input);
        }

        var session = new PlaySession(content, BattleState.From(map, content, roster, seed, scheme), Console.Out, scripted: script is not null);
        var code = session.Play(input, strict, seed);
        if (log is not null)
        {
            File.WriteAllText(log, session.EventLog);
        }

        return code;
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

    /// <summary>
    /// Lists the map's events that have not fired, one line each in player words, on a
    /// certification trial and on a map with <c>announce: on</c> (issues 78, 256): when it
    /// fires, what it does, and for a spawn the held-tile rule that stops it.
    /// </summary>
    internal void WritePendingEvents()
    {
        foreach (var mapEvent in _state.Map.Events)
        {
            if (!_state.HasFired(mapEvent.Name))
            {
                _out.WriteLine("  " + DescribeEvent(mapEvent, _content));
            }
        }
    }

    /// <summary>
    /// A map event in player words, for example <c>turn 3, enemy phase: a rider arrives at
    /// 7,0 (aggressive). A unit standing on 7,0 stops it.</c> The held-tile rule is the one
    /// <see cref="MapEvents"/> applies: a spawn tile with any unit on it blocks the spawn.
    /// </summary>
    internal static string DescribeEvent(MapEvent mapEvent, GameContent content)
    {
        var when = mapEvent.Trigger switch
        {
            TurnTrigger turn => $"turn {turn.Turn}, {(turn.Phase == Side.Enemy ? "enemy" : "player")} phase",
            EnterTrigger enter => $"when one of yours stops on {string.Join(" or ", enter.Tiles)}",
            _ => throw new InvalidOperationException("unknown trigger " + mapEvent.Trigger.GetType().Name),
        };
        var what = mapEvent.Action switch
        {
            SpawnEnemy spawn => SpawnWords(spawn.Placement, content),
            ChangeTerrain change => TerrainWords(change, content),
            SetFlag flag => $"{flag.Flag} is set.",
            _ => throw new InvalidOperationException("unknown action " + mapEvent.Action.GetType().Name),
        };
        return when + ": " + what;
    }

    /// <summary>
    /// A terrain change in player words, with the held-tile rule <see cref="MapEvents"/>
    /// applies when the new terrain is impassable to some movement type: the change does not
    /// happen under a unit that could not stand on it (the tide sample, DESIGN.md 13.21).
    /// </summary>
    private static string TerrainWords(ChangeTerrain change, GameContent content)
    {
        var terrain = content.TerrainById(change.TerrainId);
        var name = terrain.Name.ToLowerInvariant();
        var impassable = Enum.GetValues<MovementType>().Any(movement => !terrain.IsPassable(movement));
        return impassable
            ? $"{change.At} becomes {name}, unless one who cannot enter {name} stands on it."
            : $"{change.At} becomes {name}.";
    }

    private static string SpawnWords(EnemyPlacement placement, GameContent content)
    {
        var name = content.Unit(placement.TemplateId).Name.ToLowerInvariant();
        var article = "aeiou".Contains(name[0]) ? "an" : "a";
        var behavior = placement.Behavior.ToString().ToLowerInvariant();
        return $"{article} {name} arrives at {placement.At} ({behavior}). A unit standing on {placement.At} stops it.";
    }

    private int Play(TextReader input, bool strict, ulong seed)
    {
        _out.WriteLine($"{_state.Map.Name}, seed {seed}, scheme {_state.Scheme}");
        if (_state.Map.Certification is { } trialHeader)
        {
            var candidate = _state.UnitsOf(Side.Player).Single();
            _out.WriteLine($"certification trial: {candidate.Unit.Id} plays as {_content.Class(trialHeader.ClassId).Name} with {string.Join(", ", trialHeader.Loadout)}");
        }

        _out.WriteLine(Objective.Line(_state, _content));
        if (_state.Map.Certification is not null || _state.Map.Announced)
        {
            WritePendingEvents();
        }
        _out.Write(MapRenderer.Render(_state, _content));
        var commands = 0;
        var stopped = RunCommands(input, strict, ref commands);

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
        if (EscapeSummary(_state) is { } escape && outcome.IsOver)
        {
            _out.WriteLine(escape);
        }

        if (_state.Map.Certification is { } trial && outcome.IsOver)
        {
            var className = _content.Class(trial.ClassId).Name;
            _out.WriteLine(outcome.Result == BattleResult.Won
                ? $"certification: {_state.UnitsOf(Side.Player).Single().Id} earned {className}"
                : $"certification: {className} not earned");
        }
        if (_state.Map.RivalryArm is { } arm)
        {
            var attacked = _exposure.Count(entry => entry.Attacked);
            _out.WriteLine($"rivalry ({arm}): {_exposure.Count} threatened player phases ended beside a rival, {attacked} of them attacked in the enemy phase after");
        }

        return stopped ? StrictStop : outcome.Result == BattleResult.Won ? 0 : 1;
    }

    /// <summary>
    /// Reads and applies commands until the input ends, a campaign battle is left, or, under
    /// <paramref name="strict"/>, a line is rejected; true for the strict stop. Blank lines and
    /// <c>#</c> comments are skipped and not counted.
    /// </summary>
    internal bool RunCommands(TextReader input, bool strict, ref int commands)
    {
        while (!Left && input.ReadLine() is { } line)
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
                return true;
            }
        }

        return false;
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
            case "attack" when words.Length >= 3 && IsSlotText(words[3..]):
                if (TrySlot(words[1], SlotText(words[3..]), out var attackSlot) && PrintForecast(words[1], words[2], attackSlot, art))
                {
                    Apply(new Attack(words[1], words[2], attackSlot, art));
                }

                break;
            case "attack":
                Error("usage: attack <unit> <target> [slot|weapon] [art <id>]");
                break;
            case "wait" when words.Length == 2:
                Apply(new Wait(words[1]));
                break;
            case "wait":
                Error("usage: wait <unit>");
                break;
            case "watch" when words.Length == 2:
                Apply(new Watch(words[1]));
                break;
            case "watch":
                Error("usage: watch <unit>");
                break;
            case "cover" when words.Length == 3:
                Apply(new Cover(words[1], words[2]));
                break;
            case "cover":
                Error("usage: cover <unit> <ally>");
                break;
            case "canto" when words.Length == 3 && TryCoord(words[2], out var cantoTo):
                Apply(new Canto(words[1], cantoTo));
                break;
            case "canto" when words.Length == 3 && words[2] == "stay":
                if (Find(words[1]) is { } stayer)
                {
                    Apply(new Canto(stayer.Id, stayer.At));
                }

                break;
            case "canto":
                Error("usage: canto <unit> <x,y|stay>");
                break;
            case "exit" when words.Length == 2:
                Apply(new Exit(words[1]));
                break;
            case "exit":
                Error("usage: exit <unit>");
                break;
            case "recover" when words.Length == 2:
                Apply(new Recover(words[1]));
                break;
            case "recover":
                Error("usage: recover <unit>");
                break;
            case "shove" when words.Length == 3:
                Apply(new Shove(words[1], words[2]));
                break;
            case "shove":
                Error("usage: shove <unit> <target>");
                break;
            case "end" when words.Length == 1:
                var exposed = _state.Map.RivalryArm is not null && _state.Phase == Side.Player && !_state.Outcome.IsOver
                    ? Rivalry.Exposed(_state, _content).Select(u => new ExposureEntry(_state.History.Count, _state.Turn, u.Id)).ToList()
                    : new List<ExposureEntry>();
                var lethal = Queries.Lethal(_state, _content);
                if (Apply(new EndPhase(), first: lethal.Count == 0 ? null : string.Join("\n", lethal.Select(LethalLine))))
                {
                    _exposure.AddRange(exposed);
                    EnemyPhase();
                }

                break;
            case "end":
                Error("usage: end");
                break;
            case "recall" when words.Length == 2 && int.TryParse(words[1], out var index):
                var undone = index >= 0 && index < _state.History.Count ? RecallCost.Of(_state, index) : null;
                if (Apply(new Recall(index), undone is null ? null : "undone: " + UndoText(undone) + "\n" + SameRolls))
                {
                    _exposure.RemoveAll(entry => entry.HistoryAt >= index);
                }

                break;
            case "recall" when words.Length == 2 && words[1] == "list":
                ListRecallHistory();
                break;
            case "recall" when words.Length == 1:
                ListRecallTargets();
                break;
            case "recall":
                Error("usage: recall <n> | recall list  (history holds " + _state.History.Count + " states)");
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
                Error("usage: forecast <unit> <target> [slot|weapon] [art <id>] [from <x,y>]");
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
                if (_state.Map.Announced)
                {
                    WritePendingEvents();
                }

                break;
            case "leave" when _campaign && words.Length == 1:
                if (_state.Outcome.IsOver)
                {
                    Left = true;
                }
                else
                {
                    Error("the battle is not decided; leave comes after it is won or lost");
                }

                break;
            case "help":
                _out.WriteLine(Help);
                break;
            default:
                Error($"unknown command '{words[0]}'; type help");
                break;
        }
    }

    /// <summary>
    /// Applies a player command and, if it was accepted, prints <paramref name="first"/>, its
    /// events, then <paramref name="after"/>, then the board. False, with the rejection printed, if it was refused.
    /// </summary>
    private bool Apply(Command command, string? after = null, string? first = null)
    {
        var result = Resolver.Apply(_state, _content, command);
        if (!result.Accepted)
        {
            Error(result.Rejection!.Message);
            return false;
        }

        if (first is not null)
        {
            _out.WriteLine(first);
        }

        Record(command);
        var before = _state;
        _state = result.Next;
        foreach (var e in result.Events)
        {
            WriteEvent(Describe(e, _content));
        }

        foreach (var notice in Objective.Notices(before, _state, _content, command))
        {
            WriteEvent(notice);
        }

        if (after is not null)
        {
            _out.WriteLine(after);
        }

        if (CantoOwed(command) is { } owed)
        {
            _out.WriteLine($"{owed.Id} may canto up to {owed.Canto} movement: canto {owed.Id} <x,y|stay>");
        }

        if (command is not EndPhase)
        {
            _out.Write(MapRenderer.Render(_state, _content));
            AnnounceOutcome();
        }

        return true;
    }

    /// <summary>
    /// Who came out of an Escape map and who did not (issue 269): the units that left
    /// through an exit in the order they left, then the ones left behind and the ones that
    /// fell, the deployed player units missing from both. Null on any other map.
    /// </summary>
    internal static string? EscapeSummary(BattleState state)
    {
        if (state.Map.Win != WinCondition.Escape)
        {
            return null;
        }

        var opening = state.History.Count > 0 ? state.History[0] : state;
        var escaped = state.Escaped.Select(u => u.Id).ToList();
        var behind = state.LeftBehind().Select(u => u.Id).ToList();
        var fell = opening.UnitsOf(Side.Player).Select(u => u.Id)
            .Where(id => !state.HasEscaped(id) && state.Find(id) is null).ToList();
        static string List(List<string> ids) => ids.Count == 0 ? "none" : string.Join(", ", ids);
        return $"escaped: {List(escaped)}; left behind: {List(behind)}; fell: {List(fell)}";
    }

    /// <summary>The player unit an Attack, Item or Wait just left owed a Canto (issue 71), or null.</summary>
    private BattleUnit? CantoOwed(Command command)
    {
        var id = command switch
        {
            Attack a => a.UnitId,
            UseItem i => i.UnitId,
            Wait w => w.UnitId,
            _ => null,
        };
        return id is not null && _state.Find(id) is { Side: Side.Player } unit && _state.CantoReachOf(unit, _content) is not null ? unit : null;
    }

    /// <summary>
    /// Plays the enemy phase from <see cref="EnemyAi.Plan"/>, one command at a time through
    /// the same resolver, and prints the board once it is over. An enemy attack prints the
    /// same forecast line a player's attack does, asked of the core on the board the
    /// resolver is about to read, so the transcript carries the odds of every combat and
    /// not only the player's half (issue 152). The planner only emits attacks the core
    /// forecasts, so a null forecast here is a harness fault, like a rejected command.
    /// A grudge strike prints the planner's best alternative under its forecast (issue 331).
    /// A run of consecutive dark commands prints as one counted line (issue 415), while the
    /// event log keeps one line per command, the protocol's and the client's granularity.
    /// </summary>
    private void EnemyPhase()
    {
        var grudges = new Dictionary<string, EnemyAi.GrudgeStrike?>();
        var darkRun = 0;
        foreach (var command in EnemyAi.Plan(_state, _content))
        {
            var grudge = EnemyAi.GrudgeLog(_state, _content, command, grudges);
            var passed = CoverRule.PassedLine(_state, _content, command);
            if (command is Move or Wait && InTheDark(command))
            {
                var hidden = Resolve(command);
                _state = hidden.Next;
                _log.WriteLine(ProtocolSession.DarkLine);
                darkRun++;
                foreach (var e in hidden.Events.Where(e => e is not UnitMoved and not UnitWaited))
                {
                    darkRun = FlushDarkRun(darkRun);
                    WriteEvent(Describe(e, _content));
                }

                continue;
            }

            darkRun = FlushDarkRun(darkRun);
            if (passed is not null)
            {
                _out.WriteLine(passed);
            }

            _out.WriteLine("enemy: " + CommandText(command));
            if (command is Attack attack)
            {
                var attacker = _state.Find(attack.UnitId);
                var aimed = _state.Find(attack.TargetId);
                var covered = aimed is null ? null : CoverRule.Swapped(_state, aimed);
                var target = covered?.Struck ?? aimed;
                var board = covered?.Board ?? _state;
                if (covered is { } swap)
                {
                    _out.WriteLine($"  cover: {swap.Struck.Id} takes the strike aimed at {aimed!.Id}");
                }

                var forecast = attacker is null || target is null ? null : Queries.Forecast(board, _content, attacker, target, attacker.At, attack.Slot);
                if (forecast is null)
                {
                    throw new InvalidOperationException($"the enemy AI's {command} has no forecast");
                }

                var (with, counterWith) = Arms(_content, attacker!, target!, attack.Slot, attacker!.At);
                _out.WriteLine(ForecastLine(attacker!, target!, forecast, "", with, counterWith, RaisesWith(_state, _content, attacker!, attack.Slot)));
                PrintRivalry(target!, countering: true);
                if (SwornLine(attacker!, target!) is { } sworn)
                {
                    _out.WriteLine(sworn);
                }

                foreach (var pincer in PincerLines(_state, attacker!, target!))
                {
                    _out.WriteLine(pincer);
                }

                foreach (var brace in BraceLines(attacker!, target!))
                {
                    _out.WriteLine(brace);
                }

                foreach (var ignite in IgniteLines(_state, _content, attacker!, target!, attacker!.At, attack.Slot, forecast.Defender.Strikes))
                {
                    _out.WriteLine(ignite);
                }

                foreach (var windup in WindupLines(_state, _content, attacker!, target!, attack.Slot))
                {
                    _out.WriteLine(windup);
                }

                if (grudge is not null)
                {
                    _out.WriteLine("  " + grudge);
                }
            }

            var before = _state;
            var result = Resolve(command);
            _state = result.Next;
            foreach (var e in result.Events)
            {
                WriteEvent(Describe(e, _content));
                if (e is CombatFought fought)
                {
                    foreach (var entry in _exposure.Where(x => x.UnitId == fought.TargetId && x.Turn == fought.Turn))
                    {
                        entry.Attacked = true;
                    }
                }
            }

            foreach (var notice in Objective.Notices(before, _state, _content, command))
            {
                WriteEvent(notice);
            }
        }

        FlushDarkRun(darkRun);
        _out.Write(MapRenderer.Render(_state, _content));
        AnnounceOutcome();
    }

    /// <summary>
    /// Prints a pending run of dark commands to the console as one line, counted when the run
    /// is longer than one (issue 415); the count never tells a move from a wait (issue 301).
    /// Returns the emptied run.
    /// </summary>
    private int FlushDarkRun(int run)
    {
        if (run > 0)
        {
            _out.WriteLine(DarkRunLine(run));
        }

        return 0;
    }

    /// <summary>The console line for <paramref name="run"/> consecutive dark commands: the bare dark line for one, counted for more.</summary>
    public static string DarkRunLine(int run) => run == 1 ? ProtocolSession.DarkLine : $"{ProtocolSession.DarkLine} (x{run})";

    /// <summary>
    /// Applies one of the enemy planner's commands and records it; the planner only emits
    /// commands the resolver accepts, so a refusal is a harness fault.
    /// </summary>
    private ApplyResult Resolve(Command command)
    {
        var result = Resolver.Apply(_state, _content, command);
        if (!result.Accepted)
        {
            throw new InvalidOperationException($"the enemy AI's {command} was rejected: {result.Rejection!.Message}");
        }

        Record(command);
        return result;
    }

    /// <summary>
    /// Whether an enemy's Move or Wait happens where no player unit sees it, before and after
    /// (DESIGN.md 13.7): the console then prints only that something moved, since naming the
    /// unit or its tiles would light the dark. A Move that ends in sight prints in full.
    /// </summary>
    private bool InTheDark(Command command) => Dusk.InTheDark(_state, _content, command);

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

    /// <summary>The line a rewind prints under what it undoes: rolls are keyed (section 7), so a Recall is a choice and never a reroll.</summary>
    public const string SameRolls = "the rolls do not change: the same attack will roll the same";

    /// <summary>
    /// Keeps <see cref="_made"/> in step with the history for an accepted command, before the
    /// state moves: a Recall truncates it to the state it returns to, anything else names
    /// the command applied from the state it leaves.
    /// </summary>
    private void Record(Command command)
    {
        if (command is Recall recall)
        {
            _made.RemoveRange(recall.ToIndex, _made.Count - recall.ToIndex);
            return;
        }

        if (_made.Count == _state.History.Count)
        {
            _made.Add(CommandText(command));
        }
    }

    /// <summary>
    /// The Recall browser (issue 75): every history state a Recall may return to, with the
    /// turn, the command that made it, and what a rewind there gives back, from
    /// <see cref="RecallCost.Of"/>, which the rewind itself prints too. Spends nothing.
    /// </summary>
    private void ListRecallHistory()
    {
        foreach (var row in RecallRows(_state, _made))
        {
            _out.WriteLine(row.Text);
        }
    }

    /// <summary>
    /// What <c>recall list</c> prints (issue 75), one row per line: the charges, then each
    /// history state a Recall may return to with its index, or why there is none.
    /// <paramref name="made"/> is the command applied from each history state, by index, as
    /// <see cref="CommandText"/> names it. The thin renderer's Recall browser shows the same
    /// rows and recalls the row clicked (issue 353).
    /// </summary>
    public static IReadOnlyList<RecallRow> RecallRows(BattleState state, IReadOnlyList<string> made)
    {
        var total = state.Map.RecallCharges;
        var spent = total - state.RecallCharges;
        var rows = new List<RecallRow>
        {
            new(null, $"recall: {state.RecallCharges} of {total} charges left, {spent} spent; a spent charge does not come back, and the same attack will roll the same"),
        };
        if (state.RecallCharges < 1)
        {
            rows.Add(new(null, "  no charges left: nothing more can be recalled on this map"));
            return rows;
        }

        var targets = state.RecallTargets().ToList();
        if (targets.Count == 0)
        {
            rows.Add(new(null, "  no state to return to yet"));
            return rows;
        }

        foreach (var i in targets)
        {
            var from = i == 0
                ? "the start"
                : state.History[i - 1].Phase != Side.Player
                    ? "turn start"
                    : i - 1 < made.Count ? "after " + made[i - 1] : "after ?";
            rows.Add(new(i, $"  state {i}  turn {state.History[i].Turn}  {from}  undoes: {UndoText(RecallCost.Of(state, i))}"));
        }

        return rows;
    }

    /// <summary>
    /// A rewind's cost in the console's words, the player's gains given back first, then what
    /// comes back to the player. A number printed next to a unit's name is that unit's own
    /// number (issue 552): a returned unit is named with the HP it comes back at, and each other
    /// unit with the HP it gets back, read from <see cref="RecallCost.HpByUnit"/>.
    /// </summary>
    public static string UndoText(RecallCost cost)
    {
        if (cost.IsEmpty)
        {
            return "moves only";
        }

        var back = new List<string>();
        if (cost.KillsGivenBack.Count > 0)
        {
            back.Add($"{cost.KillsGivenBack.Count} {(cost.KillsGivenBack.Count == 1 ? "kill" : "kills")} ({string.Join(", ", cost.KillsGivenBack)})");
        }

        if (cost.ExpGivenBack > 0)
        {
            back.Add($"{cost.ExpGivenBack} exp");
        }

        if (cost.LevelsGivenBack > 0)
        {
            back.Add($"{cost.LevelsGivenBack} {(cost.LevelsGivenBack == 1 ? "level" : "levels")}");
        }

        if (cost.EnemyHpBack > 0)
        {
            back.Add($"{cost.EnemyHpBack} enemy hp");
        }

        var returned = new List<string>();
        returned.AddRange(cost.UnitsReturned.Select(id => $"{id} alive at {cost.HpFor(id)} hp"));
        returned.AddRange(cost.HpByUnit.Where(entry => !cost.UnitsReturned.Contains(entry.Id)).Select(entry => $"{entry.Hp} hp to {entry.Id}"));

        returned.AddRange(cost.ArrivalsUndone.Select(id => id + " not yet arrived"));
        var parts = new List<string>();
        if (back.Count > 0)
        {
            parts.Add("gives back " + string.Join(", ", back));
        }

        if (returned.Count > 0)
        {
            parts.Add("returns " + string.Join(", ", returned));
        }

        return string.Join("; ", parts);
    }

    private void AnnounceOutcome()
    {
        var outcome = _state.Outcome;
        if (outcome.IsOver)
        {
            var after = _state.RecallCharges > 0
                ? "only recall is left" + (_campaign ? ", or leave" : "")
                : "no recall is left" + (_campaign ? ", so leave" : "");
            _out.WriteLine($"battle {(outcome.Result == BattleResult.Won ? "won" : "lost")}: {outcome.Reason}; {after}");
            if (Objective.Verdict(_state, _content) is { } verdict)
            {
                _out.WriteLine(verdict);
            }
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
    /// True when the words after the target can be an <c>attack</c> or <c>forecast</c> slot
    /// (issue 477): none, one number, or a weapon's id or name, whose words are letters,
    /// <c>_</c>, <c>-</c> and <c>'</c> only, so a coordinate or a stray keyword stays a usage error.
    /// </summary>
    private static bool IsSlotText(string[] words) =>
        words.Length == 0
        || (words.Length == 1 && int.TryParse(words[0], out _))
        || words.All(word => word is not ("from" or "art") && word.All(c => char.IsAsciiLetter(c) || c is '_' or '-' or '\''));

    /// <summary>The slot words joined with one space, or null when there are none.</summary>
    private static string? SlotText(string[] words) => words.Length == 0 ? null : string.Join(' ', words);

    /// <summary>
    /// Reads the words after <c>forecast &lt;unit&gt; &lt;target&gt;</c>: an optional slot or
    /// weapon name, then an optional <c>from &lt;x,y&gt;</c> (issues 151, 477). False when they
    /// are anything else.
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
        var fromAt = Array.IndexOf(words, "from", next);
        var slotWords = words[next..(fromAt < 0 ? words.Length : fromAt)];
        if (!IsSlotText(slotWords))
        {
            return false;
        }

        slotText = SlotText(slotWords);
        next += slotWords.Length;

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
        if (Find(unitId) is not { } unit)
        {
            return false;
        }

        var tile = from ?? unit.At;
        if (tile != unit.At && !Queries.CanStandOn(_state, _content, unit, tile))
        {
            Error(unit.Moved ? $"{unit.Id} has already moved this phase; forecast from {unit.At}" : $"{unit.Id} cannot move to {tile}");
            return false;
        }

        if (Find(targetId, unit.Id, tile) is not { } target)
        {
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
        var (with, counterWith) = Arms(content, unit, target, slot, tile);
        var raises = RaisesWith(state, content, unit, slot);
        var lines = new List<string> { ForecastLine(unit, target, forecast, where, with, counterWith, raises) };
        if (LethalCounterLine(unit, target, forecast, raises) is { } lethal)
        {
            lines.Add(lethal);
        }

        if (art is not null)
        {
            lines.Add(ArtLine(content, unit, forecast, slot, art));
        }

        if (RivalryLine(state, content, unit with { At = tile }, countering: false) is { } rivalry)
        {
            lines.Add(rivalry);
        }

        if (SwornLine(unit, target) is { } sworn)
        {
            lines.Add(sworn);
        }

        lines.AddRange(PincerLines(state, unit with { At = tile }, target));
        lines.AddRange(BraceLines(unit, target));
        lines.AddRange(SignatureLines(state, content, unit with { At = tile }, target, forecast));
        lines.AddRange(IgniteLines(state, content, unit, target, tile, slot, forecast.Defender.Strikes));
        lines.AddRange(WindupLines(state, content, unit with { At = tile }, target, slot));
        lines.AddRange(PendingRetreatLines(state, content, unit, tile, target, forecast));
        return string.Join("\n", lines);
    }

    /// <summary>
    /// What <c>end</c> prints before a player phase ends for a unit the coming enemy phase kills
    /// if every strike <c>threat</c> prices lands (issue 558, <see cref="Queries.Lethal"/>):
    /// <c>lethal if all land: wren (soldier-2 9, archer-1 6 against 15 hp)</c>.
    /// </summary>
    public static string LethalLine(LethalThreat lethal) =>
        $"lethal if all land: {lethal.Unit.Id} ({string.Join(", ", lethal.Strikers.Select(s => $"{s.Enemy.Id} {s.Damage}"))} against {lethal.Unit.Hp} hp)";

    /// <summary>
    /// Under a forecast whose counter kills the attacker if every counter strike lands (issue 539):
    /// <c>  counter: lethal to wren (17 against 17 hp)</c>, read by
    /// <see cref="CombatForecast.CounterIsLethal"/>; null otherwise, and for a raise, which draws no counter.
    /// </summary>
    public static string? LethalCounterLine(BattleUnit unit, BattleUnit target, CombatForecast forecast, bool raises) =>
        !raises && forecast.CounterIsLethal(unit.Hp, target.Hp)
            ? $"  counter: lethal to {unit.Id} ({forecast.CounterIfAllLand} against {unit.Hp} hp)"
            : null;

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
    /// can leave, from one hit up to every strike the attack can make, crits aside. Silent when no outcome
    /// sends it anywhere.
    /// </summary>
    private static IEnumerable<string> PendingRetreatLines(BattleState state, GameContent content, BattleUnit unit, Coord tile, BattleUnit target, CombatForecast forecast)
    {
        if (!state.Map.RetreatEnabled || !forecast.Attacker.Strikes)
        {
            yield break;
        }

        var hits = Enumerable.Range(1, forecast.Attacker.StrikeCount);
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
    /// then the total if every strike lands against the unit's HP, one enemy per strike
    /// tile (<see cref="Queries.IfAllLand(IReadOnlyList{ThreatLine})"/>, issue 253). Spends nothing.
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
            Error(unit.Canto is not null && unit.Acted ? $"{unit.Id} cannot canto to {tile}" : unit.Moved ? $"{unit.Id} has already moved this phase; threat from {unit.At}" : $"{unit.Id} cannot move to {tile}");
            return;
        }

        _out.WriteLine(ThreatText(_state, _content, unit, tile, lines, Queries.SleepingThreats(_state, _content, unit, tile)!, Queries.Unseeing(_state, _content, unit, tile), Queries.MoveWins(_state, _content, unit, tile), Queries.Anvils(_state, _content, unit, tile), Queries.StopWakes(_state, _content, unit, tile), Queries.Refusals(_state, _content, unit, tile)));
        if (BracedThreat(_state, _content, unit, tile) is { } braced)
        {
            _out.WriteLine(braced);
        }
    }

    /// <summary>
    /// On a <c>brace: on</c> map (DESIGN.md 13.14), for a unit asked about on its own tile that
    /// would brace if it waited there and something can strike it: the same threat priced braced,
    /// under a line saying so, so the choice between striking and bracing reads as two numbers.
    /// Null otherwise.
    /// </summary>
    public static string? BracedThreat(BattleState state, GameContent content, BattleUnit unit, Coord tile)
    {
        if (tile != unit.At || unit.Acted || unit.Braced || !Brace.BracesOnWait(state, unit))
        {
            return null;
        }

        var braced = unit with { Braced = true };
        var after = state.WithUnit(braced);
        if (Queries.Threats(after, content, braced, tile) is not { Count: > 0 } lines)
        {
            return null;
        }

        return $"if {unit.Id} waits here it braces (hit -{Brace.Hit}):\n"
            + ThreatText(after, content, braced, tile, lines, Queries.SleepingThreats(after, content, braced, tile)!, Queries.Unseeing(after, content, braced, tile), anvils: Queries.Anvils(after, content, braced, tile), refusals: Queries.Refusals(after, content, braced, tile));
    }

    /// <summary>
    /// What <c>threat</c> prints for <see cref="Queries.Threats"/>' lines, one row per line,
    /// an enemy an announced event brings marked with where it arrives, then one row per
    /// group <see cref="Queries.SleepingThreats"/> names, members and tiles and no numbers,
    /// and the wake rule under them (issue 248); the protocol's threat query carries it as
    /// its <c>text</c> (issue 25). On a dusk map (DESIGN.md 13.7) an enemy no player unit sees
    /// is left out of the rows, the total and the sleeping groups, and one line says the dark is
    /// unpriced, whether or not anything in it could strike; while anything is in the dark, the
    /// empty case says <c>no enemy in sight can strike it</c>, never a claim about the whole board (issue 399). The dark
    /// line lists the unseen tiles within <see cref="GameContent.LongestReach"/> of the tile, nearest first with
    /// the distance, unpriced and unnamed (<see cref="Dusk.UnseenNear"/>, issue 403); with none that near it
    /// keeps the bare <c>and whatever is in the dark</c> line. An enemy the player sees that
    /// would strike the unit in daylight but does not know where it is, or whose side cannot
    /// see it from where it would strike (<see cref="Queries.Unseeing"/>, issue 302), is priced
    /// at 0 with the reason: <c>archer-1: cannot see you (dark)</c>. A move that wins the map
    /// (<see cref="Queries.MoveWins"/>, issue 356) is one line saying so, since no enemy phase follows.
    /// On a <c>windup: on</c> map (DESIGN.md 13.16, issue 444) a blow already raised over the tile
    /// is a row before the strikes and is in the total, since it lands for certain at the enemy
    /// phase start; a maul that would raise this phase says so under its row and adds nothing.
    /// On a <c>pincer: on</c> map (DESIGN.md 13.13, issue 457) each of the planner's anvil plans
    /// against the unit (<see cref="Queries.Anvils"/>) is one row after the total, phrased as could
    /// and unpriced: <c>anvil: shieldbearer-1 could step to 17,0 so soldier-1 strikes you pinned
    /// from 19,0</c>, or <c>could hold 17,0</c> when the tile is the anvil's own; a plan with an
    /// enemy the player does not see is left out.
    /// A stop that wakes a sleeping group (<see cref="Queries.StopWakes"/>, issue 458) is one row
    /// before the sleeping groups, naming each group and why, unpriced:
    /// <c>stopping here wakes: ford (proximity), weir (called by ford)</c>.
    /// On a <c>cover: on</c> map (DESIGN.md 13.19, round 142) a strike a cover would swap is
    /// priced against the coverer on the tile, <c>covered by teodor, strikes teodor on 7,5</c>,
    /// and the total says the first strike swaps them and where the unit lands, the strikes after
    /// the swap unpriced.
    /// A boss under the veto that could strike the unit but refuses every tile it would strike
    /// from (<see cref="Queries.Refusals"/>, issue 565) is one row after the strikes, naming the
    /// nearest refused tile and where its plan ends, unpriced:
    /// <c>weir_foreman-1 could reach 12,4 but refuses it: too exposed there; holds 14,6</c>, or
    /// <c>ends on 13,6</c> when the plan moves it; a boss the player does not see is left out.
    /// </summary>
    public static string ThreatText(BattleState state, GameContent content, BattleUnit unit, Coord tile, IReadOnlyList<ThreatLine> lines, IReadOnlyList<SleepingThreat> asleep, IReadOnlyList<BattleUnit>? unseeing = null, bool wins = false, IReadOnlyList<AnvilLine>? anvils = null, IReadOnlyList<GroupWoke>? wakes = null, IReadOnlyList<RefusalLine>? refusals = null)
    {
        var where = $"{tile} ({state.Map.TerrainAt(tile, content).Name})";
        if (wins)
        {
            return $"threat on {unit.Id} at {where}: this move wins the map";
        }

        var rows = new List<string>();
        lines = lines.Where(line => line.Arrives is not null || Dusk.Seen(state, line.Enemy)).ToList();
        asleep = asleep.Select(g => g with { Members = ValueList<BattleUnit>.From(g.Members.Where(m => Dusk.Seen(state, m))) }).Where(g => g.Members.Count > 0).ToList();
        var dark = Dusk.Sight(state) is not null && state.UnitsOf(Side.Enemy).Any(e => !Dusk.Seen(state, e));
        var blow = Queries.RaisedBlowOn(state, content, unit, tile);
        if (lines.Count == 0 && blow is null)
        {
            rows.Add($"threat on {unit.Id} at {where}: no enemy {(dark ? "in sight " : "")}can strike it next phase");
        }
        else
        {
            rows.Add($"threat on {unit.Id} at {where}:");
            if (blow is not null)
            {
                rows.Add($"  {blow.Wielder.Id}'s raised blow lands here at the enemy phase start: {blow.Damage}, sure, unless a hit from within its reach breaks it");
            }

            foreach (var line in lines)
            {
                var arrives = line.Arrives is { } at ? $" (arrives this enemy phase at {at})" : "";
                var covered = line.CoveredBy is { } by ? $"covered by {by.Id}, strikes {by.Id} on {tile}, " : "";
                var answers = line.CoveredBy ?? unit;
                rows.Add($"  {line.Enemy.Id}{arrives} from {line.From} with {line.Weapon.Name}{Keepsake.Suffix(line.Enemy.Unit.Inventory.Items[line.Slot], content)} (slot {line.Slot + 1}): {covered}{(line.Raises ? RaiseText(line.Forecast.Attacker) + "; counter: none" : StrikeText(line.Forecast.Attacker) + "; counter" + (line.Forecast.Defender.Strikes ? CounterWith(content, answers, line.From.DistanceTo(tile)) + ": " + StrikeText(line.Forecast.Defender) : ": none"))}");
                if (line.Raises)
                {
                    rows.Add($"    windup: no strike; {line.Enemy.Id} raises over {tile}, lands next enemy phase for {line.Forecast.Attacker.Damage}, sure, unless a hit from within its reach breaks it (not in the total)");
                }
            }

            if (lines.FirstOrDefault(l => l.CoveredBy is not null)?.CoveredBy is { } coverer)
            {
                var landing = state.Find(coverer.Id)?.At ?? coverer.At;
                var worst = lines.Where(l => l.CoveredBy is not null).Max(l => l.IfAllLand);
                rows.Add($"  if all land: the first strike swaps them; {coverer.Id} takes up to {worst} against {coverer.Hp} hp on {tile}, {unit.Id} lands on {landing}, and any strike after it is unpriced");
            }
            else
            {
                rows.Add($"  if all land: {Queries.IfAllLand(lines, blow)} against {unit.Hp} hp");
            }
        }

        foreach (var refusal in (refusals ?? Array.Empty<RefusalLine>()).Where(r => Dusk.Seen(state, r.Boss)))
        {
            var ends = refusal.Ends == refusal.Boss.At ? $"holds {refusal.Ends}" : $"ends on {refusal.Ends}";
            rows.Add($"  {refusal.Boss.Id} could reach {refusal.Refused} but refuses it: too exposed there; {ends}");
        }

        foreach (var anvil in (anvils ?? Array.Empty<AnvilLine>()).Where(a => Dusk.Seen(state, a.Anvil) && Dusk.Seen(state, a.Follower)))
        {
            var step = anvil.Tile == anvil.Anvil.At ? $"hold {anvil.Tile}" : $"step to {anvil.Tile}";
            rows.Add($"  anvil: {anvil.Anvil.Id} could {step} so {anvil.Follower.Id} strikes you pinned from {anvil.From}");
        }

        foreach (var blind in (unseeing ?? Array.Empty<BattleUnit>()).Where(e => Dusk.Seen(state, e)))
        {
            rows.Add($"  {blind.Id}: cannot see you (dark)");
        }

        if (dark)
        {
            var near = Dusk.UnseenNear(state, tile, content.LongestReach);
            rows.Add(near.Count == 0
                ? $"  and whatever is in the dark ({Dusk.Unseen}), unpriced"
                : $"  in the dark, unpriced: {string.Join(", ", near.Select(n => $"{Dusk.Unseen} at {n.At} ({n.Distance})"))}");
        }

        if (wakes is { Count: > 0 })
        {
            rows.Add($"  stopping here wakes: {string.Join(", ", wakes.Select(w => $"{w.Group} ({WakeCauseText(w)})"))}");
        }

        foreach (var group in asleep)
        {
            rows.Add($"  group {group.Group} asleep, could strike here if woken: {string.Join(", ", group.Members.Select(m => $"{m.Id} at {m.At}"))}");
        }

        if (asleep.Count > 0)
        {
            rows.Add($"  {MapRenderer.WakeLegend(content)}");
        }

        return string.Join("\n", rows);
    }

    /// <summary>Why a group wakes, as the wake event prints it: <c>called by ford</c> for a linked call, else the cause in lower case.</summary>
    public static string WakeCauseText(GroupWoke woke) => woke.CalledBy is { } by ? $"called by {by}" : woke.Cause.ToString().ToLowerInvariant();

    /// <summary>
    /// The one forecast line, printed before an attack from either side and by the
    /// <c>forecast</c> command: the attacker's strike, then the counter or <c>none</c>.
    /// <paramref name="where"/> is the tile suffix of a forecast asked from a tile the
    /// unit has not moved to (issue 151), empty for a forecast on the standing board.
    /// </summary>
    public static string ForecastLine(BattleUnit unit, BattleUnit target, CombatForecast forecast, string where = "", string with = "", string counterWith = "", bool raises = false)
    {
        if (raises)
        {
            return $"forecast {unit.Id} -> {target.Id}{where}{with}: {RaiseText(forecast.Attacker)}; counter: none";
        }

        return $"forecast {unit.Id} -> {target.Id}{where}{with}: {StrikeText(forecast.Attacker)}; counter{(forecast.Defender.Strikes ? counterWith + ": " + StrikeText(forecast.Defender) : ": none")}";
    }

    /// <summary>
    /// The weapon suffixes of a forecast line (DESIGN.md 13.11, issue 313): <c> with Toll Axe</c>
    /// after the target for the attacker's <paramref name="slot"/> (else its equipped weapon), and
    /// after <c>counter</c> for the weapon the defender holds in front, which is the one it last
    /// swung. A side is named only when it could strike from that range with more than one
    /// weapon (<see cref="BattleUnit.WeaponChoicesAt"/>), the attacker counted at
    /// <paramref name="from"/>; an attacker's keepsake is named whatever the count
    /// (<see cref="KeepsakeWith"/>).
    /// </summary>
    public static (string With, string CounterWith) Arms(GameContent content, BattleUnit unit, BattleUnit target, int? slot, Coord from)
    {
        var distance = from.DistanceTo(target.At);
        var with = unit.WeaponChoicesAt(content, distance) > 1
            ? WeaponWith(unit, content, slot ?? unit.EquippedSlot(content))
            : KeepsakeWith(unit, content, slot);
        return (with, CounterWith(content, target, distance));
    }

    /// <summary>
    /// The counter's suffix, <c> with Steel Axe</c>, for a defender that could strike from
    /// <paramref name="distance"/> with more than one weapon; empty otherwise (issue 313).
    /// </summary>
    public static string CounterWith(GameContent content, BattleUnit defender, int distance) =>
        defender.WeaponChoicesAt(content, distance) > 1 ? WeaponWith(defender, content, defender.EquippedSlot(content)) : "";

    private static string WeaponWith(BattleUnit unit, GameContent content, int slot)
    {
        if (slot < 0 || slot >= unit.Unit.Inventory.Count)
        {
            return "";
        }

        var stack = unit.Unit.Inventory.Items[slot];
        return " with " + content.ItemName(stack.ItemId) + Keepsake.Suffix(stack, content);
    }

    /// <summary>
    /// What a forecast line adds when the weapon struck with is a keepsake (DESIGN.md 13.8,
    /// issue 295): <c> with Iron Lance (Teodor's)</c>, the slot named or else the equipped
    /// one; empty for an ordinary weapon, so the line reads as it always has.
    /// </summary>
    public static string KeepsakeWith(BattleUnit unit, GameContent content, int? slot)
    {
        var at = slot ?? unit.EquippedSlot(content);
        if (at < 0 || at >= unit.Unit.Inventory.Count || unit.Unit.Inventory.Items[at] is not { Keepsake: { } fallen } stack)
        {
            return "";
        }

        return " with " + Keepsake.Name(stack.ItemId, fallen, content);
    }

    /// <summary>One side of a forecast as the console prints it: damage, doubles, displayed hit, and crit.</summary>
    private static string StrikeText(SideForecast side) =>
        $"dmg {side.Damage}{(side.StrikeCount > 1 ? $" x{side.StrikeCount}" : "")} hit {side.DisplayedHit}% crit {side.CritChance}%";

    /// <summary>
    /// The strike columns of an attack that raises a blow (DESIGN.md 13.16, issue 447): the
    /// landing's damage and no percentage, since the raise has no roll and the landing is sure.
    /// </summary>
    private static string RaiseText(SideForecast side) => $"dmg {side.Damage} hit -- crit --";

    /// <summary>True when <paramref name="unit"/>'s attack with <paramref name="slot"/> raises a blow instead of fighting.</summary>
    internal static bool RaisesWith(BattleState state, GameContent content, BattleUnit unit, int? slot) =>
        Windup.Raises(state, Resolver.ChooseWeapon(unit, content, slot).Weapon);

    /// <summary>
    /// Reads a one-based slot typed by the player into the core's zero-based one. Null text
    /// is no slot. A slot outside 1..count is refused here, naming the range the player
    /// sees, so the core's zero-based message never reaches the screen. Text that is not a
    /// number names a carried item by id or display name, ignoring case, a space standing for
    /// the id's <c>_</c> (issue 477); a name that matches no slot, or more than one, is refused
    /// with the unit's slots listed by number and name.
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

        var count = unit.Unit.Inventory.Count;
        if (!int.TryParse(text, out var typed))
        {
            var named = SlotsNamed(unit.Unit.Inventory, _content, text);
            if (named.Count == 1)
            {
                slot = named[0];
                return true;
            }

            var slots = count == 0
                ? "carries nothing"
                : "slots: " + string.Join(", ", unit.Unit.Inventory.Items.Select((item, at) => $"{at + 1} {_content.ItemName(item.ItemId)}"));
            Error(named.Count == 0
                ? $"{unit.Id} carries no '{text}'; {slots}"
                : $"{unit.Id} carries '{text}' in slots {string.Join(" and ", named.Select(at => at + 1))}; give the slot number; {slots}");
            return false;
        }

        if (typed < 1 || typed > count)
        {
            Error(count == 0 ? $"{unit.Id} carries nothing" : $"{unit.Id} has nothing in slot {typed}; slots run 1-{count}");
            return false;
        }

        slot = typed - 1;
        return true;
    }

    /// <summary>
    /// The zero-based slots whose item is named by <paramref name="text"/> (issue 477): its id
    /// or its display name, ignoring case, a space standing for the id's <c>_</c>.
    /// </summary>
    public static IReadOnlyList<int> SlotsNamed(Inventory inventory, GameContent content, string text) =>
        inventory.Items
            .Select((item, at) => (item, at))
            .Where(entry => string.Equals(entry.item.ItemId, text.Replace(' ', '_'), StringComparison.OrdinalIgnoreCase)
                || string.Equals(content.ItemName(entry.item.ItemId), text, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.at)
            .ToList();

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
        foreach (var line in ShowLines(_state, _content, unit))
        {
            _out.WriteLine(line);
        }
    }

    /// <summary>
    /// The lines <c>show &lt;unit&gt;</c> prints: who and where, stats, weapon, items, ranks,
    /// arts, abilities, mastery, Canto, targets and rivalry. The Godot client's unit panel
    /// shows the same lines (issue 349).
    /// </summary>
    public static IReadOnlyList<string> ShowLines(BattleState state, GameContent content, BattleUnit unit)
    {
        var lines = new List<string>();
        var stats = content.StatsOf(unit.Unit);
        lines.Add($"{unit.Id}: {unit.Unit.Name}, {content.Class(unit.Unit.ClassId).Name} L{unit.Unit.Level}, at {unit.At} on {state.Map.TerrainAt(unit.At, content).Label(stats.Hp)}");
        var unitClass = content.Class(unit.Unit.ClassId);
        lines.Add($"  hp {unit.Hp}/{stats.Hp}  str {stats.Str} mag {stats.Mag} dex {stats.Dex} spd {stats.Spd} lck {stats.Lck} def {stats.Def} res {stats.Res} cha {stats.Cha}  mov {unitClass.Mov} ({unitClass.Movement.ToString().ToLowerInvariant()})");
        lines.Add($"  weapon: {WeaponLine(unit, content)}");
        var slots = unit.Unit.Inventory.Items.Select((item, slot) => $"{slot + 1}: {content.ItemName(item.ItemId)}{Keepsake.Suffix(item, content)} x{item.Uses}");
        lines.Add($"  items: {(unit.Unit.Inventory.Count == 0 ? "none" : string.Join(", ", slots))}");
        var ranks = content.Class(unit.Unit.ClassId).Weapons
            .Select(type => $"{type.ToString().ToLowerInvariant()} {unit.Unit.Skill.Rank(type)} ({unit.Unit.Skill.Points(type)})");
        lines.Add($"  ranks: {string.Join(", ", ranks)}");
        var arts = content.ArtsOf(unit.Unit).Select(a =>
            $"{a.Ability.Id} ({a.Art.Weapon.ToString().ToLowerInvariant()} {a.Art.Rank}, cost {a.Art.Cost}): {a.Ability.Text}").ToList();
        if (arts.Count > 0)
        {
            lines.Add($"  arts: {string.Join(", ", arts)}");
        }

        var held = content.AbilitiesOf(unit.Unit).Where(a => a.Effect is not CombatArtEffect).Select(a => $"{a.Name} ({a.Text.TrimEnd('.')})").ToList();
        if (held.Count > 0)
        {
            lines.Add($"  abilities: {string.Join(", ", held)}");
        }

        if (MasteryLine(unit, content) is { } mastery)
        {
            lines.Add(mastery);
        }

        if (Signatures.Of(state, content, unit) is { } signature)
        {
            lines.Add($"  signature: {Signatures.Describe(signature)}");
        }

        if (state.CantoReachOf(unit, content) is not null)
        {
            lines.Add($"  canto: {unit.Canto} movement left this phase");
        }

        var targets = string.Join(", ", Queries.Targets(state, content, unit).Select(t => t.Id));
        lines.Add($"  targets from here: {(targets.Length == 0 ? "none" : targets)}");
        if (state.Map.RivalryArm is not null && Rivalry.IsRecruit(unit))
        {
            var rivals = state.UnitsOf(Side.Player)
                .Where(other => Rivalry.AreRivals(state, content, unit, other))
                .Select(other => $"{other.Id} {Rivalry.PointsOf(state, unit.Id, other.Id)}/{content.Rivalry.OverwriteAt}");
            var list = string.Join(", ", rivals);
            lines.Add($"  {unit.Unit.Region}; rapport {Rivalry.RateOf(unit, content)} per phase beside a recruit; rivals: {(list.Length == 0 ? "none" : list)}");
        }

        return lines;
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
            : $"  mastery: {name} ({unit.Unit.Mastery.Points(unitClass.Id)} of {unitClass.MasteryPoints} {(content.Weapons.Values.Any(w => w.Heals && unitClass.CanUse(w.Type)) ? "combats or heals" : "combats")})";
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

    /// <summary>
    /// Under a forecast between a sworn player unit and the enemy sworn on it (issue 331): the
    /// crit avoid the forecast already took off, so the reader sees why the crit is what it is.
    /// Silent otherwise.
    /// </summary>
    public static string? SwornLine(BattleUnit a, BattleUnit b)
    {
        var (enemy, player) = a.Side == Side.Enemy ? (a, b) : (b, a);
        return enemy.Grudge == player.Id && enemy.Side != player.Side
            ? $"  sworn: {enemy.Id} on {player.Id}: {player.Id} crit avoid {Grudges.SwornCritAvoid:+0;-0;0}"
            : null;
    }

    /// <summary>
    /// Under a forecast on a <c>pincer: on</c> map (DESIGN.md 13.13): one line for each side
    /// that is pinned, naming the unit behind it and the hit the forecast already added, the
    /// strike first and the counter second. Silent when neither is pinned.
    /// </summary>
    public static IEnumerable<string> PincerLines(BattleState state, BattleUnit attacker, BattleUnit target)
    {
        if (Pincer.PinnedBy(state, attacker, target) is { } behindTarget)
        {
            yield return $"  pincer: {target.Id} pinned by {behindTarget.Id}: {attacker.Id} hit +{Pincer.Hit}";
        }

        if (Pincer.PinnedBy(state, target, attacker) is { } behindAttacker)
        {
            yield return $"  pincer: {attacker.Id} pinned by {behindAttacker.Id}: {target.Id} hit +{Pincer.Hit}";
        }
    }

    /// <summary>
    /// Under a forecast on a <c>brace: on</c> map (DESIGN.md 13.14): one line when the target is
    /// braced, naming the hit the forecast already took off. Silent otherwise; the striker is never
    /// braced, since its brace ends before it can strike.
    /// </summary>
    public static IEnumerable<string> BraceLines(BattleUnit attacker, BattleUnit target)
    {
        if (target.Braced)
        {
            yield return $"  brace: {target.Id} braced: {attacker.Id} hit -{Brace.Hit}";
        }
    }

    /// <summary>
    /// Under a forecast on a <c>signatures: on</c> map (DESIGN.md 13.18): Teodor's orders on the
    /// striker, Teodor's own watched penalty, each naming the hit the forecast already holds, and
    /// Ottilie's refusal with the displayed hit it refuses. The forecast still answers. Silent otherwise.
    /// </summary>
    public static IEnumerable<string> SignatureLines(BattleState state, GameContent content, BattleUnit attacker, BattleUnit target, CombatForecast forecast)
    {
        if (Signatures.OrderedBy(state, content, attacker) is { } teodor)
        {
            yield return $"  signature: {teodor.Id}'s orders: {attacker.Id} hit +{Signatures.OrdersHit}";
        }

        if (Signatures.Watched(state, content, attacker))
        {
            yield return $"  signature: {attacker.Id} hit -{Signatures.WatchedHit} (ally within {Signatures.OrdersRadius})";
        }

        if (Signatures.Refuses(state, content, attacker, forecast.Attacker.DisplayedHit))
        {
            yield return $"  signature: {attacker.Id} refuses this strike: {forecast.Attacker.DisplayedHit} is under {Signatures.LedgerFloor}";
        }
    }

    /// <summary>
    /// Under a forecast on a <c>wildfire: on</c> map (DESIGN.md 13.15): one line when the strike's
    /// weapon ignites and the target stands on forest, and one when the target counters with a
    /// weapon that does and the striker's tile is forest, naming the tile a hit sets alight. Silent otherwise.
    /// </summary>
    public static IEnumerable<string> IgniteLines(BattleState state, GameContent content, BattleUnit attacker, BattleUnit target, Coord tile, int? slot, bool counters)
    {
        if (!state.Map.WildfireEnabled)
        {
            yield break;
        }

        if (Resolver.ChooseWeapon(attacker, content, slot).Weapon is { Ignites: true } && state.Map.TerrainIdAt(target.At) == Wildfire.ForestTerrainId)
        {
            yield return $"  wildfire: {attacker.Id} ignites {target.At} on a hit";
        }

        if (counters && target.EquippedWeapon(content) is { Ignites: true } && state.Map.TerrainIdAt(tile) == Wildfire.ForestTerrainId)
        {
            yield return $"  wildfire: {target.Id} ignites {tile} on a hit";
        }
    }

    /// <summary>
    /// Under a forecast on a <c>windup: on</c> map (DESIGN.md 13.16): one line when the attack
    /// raises a blow instead of fighting, with what the blow would deal the target where it stands;
    /// one when the target has a raised blow, saying whether a hit from the attacker's tile breaks
    /// it; and one when the attacker's tile is under another unit's blow, with the certain damage
    /// it lands on the attacker. Silent otherwise.
    /// </summary>
    public static IEnumerable<string> WindupLines(BattleState state, GameContent content, BattleUnit attacker, BattleUnit target, int? slot)
    {
        if (!state.Map.WindupEnabled)
        {
            yield break;
        }

        var (armed, weapon, _) = Resolver.ChooseWeapon(attacker, content, slot);
        if (Windup.Raises(state, weapon))
        {
            var damage = Windup.Damage(state, content, armed, target);
            yield return $"  windup: no combat now; {attacker.Id} raises a blow over {target.At}, landing at its next phase start on whoever stands there ({target.Id}: {damage}, sure) unless a hit from within its reach breaks it";
        }

        if (target.WindupAt is { } at)
        {
            yield return Windup.Breaks(content, target, attacker.At)
                ? $"  windup: a hit on {target.Id} breaks its blow over {at}"
                : $"  windup: a hit from {attacker.At} does not break {target.Id}'s blow over {at} (outside its reach)";
        }

        if (Windup.Over(state, attacker.At) is { } wielder && wielder.Id != attacker.Id)
        {
            yield return $"  windup: {wielder.Id}'s blow lands on {attacker.At} at its next phase start: {Windup.Damage(state, content, wielder, attacker)} to {attacker.Id}, sure";
        }
    }

    private string Named(string itemId) => _content.ItemName(itemId);

    /// <summary>
    /// The living unit named <paramref name="id"/>, or null with the console's error. At dusk
    /// an enemy the player does not see is refused as out of sight; given a mover and the
    /// tile a forecast is asked from, an enemy the mover would see from that tile is found
    /// (<see cref="Dusk.Seen(BattleState, BattleUnit, string, Coord)"/>, issue 309).
    /// </summary>
    private BattleUnit? Find(string id, string? moverId = null, Coord? moverAt = null)
    {
        var unit = _state.Find(id);
        if (unit is null)
        {
            Error($"no living unit '{id}'");
        }
        else if (moverId is not null && moverAt is { } tile ? !Dusk.Seen(_state, unit, moverId, tile) : !Dusk.Seen(_state, unit))
        {
            Error($"no unit '{id}' in sight at dusk");
            return null;
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

    /// <summary>A command as a script line types it, the words <c>recall list</c> names a state by.</summary>
    public static string CommandText(Command command) => command switch
    {
        Move m => $"move {m.UnitId} {m.To}",
        Attack a => $"attack {a.UnitId} {a.TargetId}" + (a.Slot is null ? "" : " " + (a.Slot + 1)) + (a.Art is null ? "" : " art " + a.Art),
        Canto c => $"canto {c.UnitId} {c.To}",
        Wait w => $"wait {w.UnitId}",
        Watch w => $"watch {w.UnitId}",
        Cover c => $"cover {c.UnitId} {c.AllyId}",
        Exit x => $"exit {x.UnitId}",
        Recover r => $"recover {r.UnitId}",
        Shove s => $"shove {s.UnitId} {s.TargetId}",
        Retreat r => $"retreat {r.UnitId} {r.To}",
        EndPhase => "end",
        Recall r => $"recall {r.ToIndex}",
        UseItem i => $"item {i.UnitId} {i.Slot + 1}" + (i.TargetId is null ? "" : " " + i.TargetId),
        _ => command.ToString() ?? "?",
    };

    /// <summary>
    /// One line per event, the words a transcript reader sees. Items, weapons, abilities, classes
    /// and terrain print their display names from <paramref name="content"/>; units keep their ids,
    /// the handles a command types.
    /// </summary>
    public static string Describe(GameEvent e, GameContent content)
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
                return $"{m.UnitId} masters the {ClassName(m.ClassId, content)} class and keeps {AbilityName(m.AbilityId, content)}";
            case UnitWaited w:
                return w.Braced ? $"{w.UnitId} waits and braces" : $"{w.UnitId} waits";
            case UnitExited x:
                return $"{x.UnitId} leaves through the exit at {x.At}";
            case UnitLeftBehind b:
                return $"{b.UnitId} is left behind at {b.At}";
            case KeepsakeLeft k:
                return $"{Keepsake.Name(k.ItemId, k.FallenId, content)} lies at {k.At}";
            case KeepsakeRecovered k:
                return $"{k.UnitId} recovers {Keepsake.Name(k.ItemId, k.FallenId, content)}";
            case KeepsakeTaken k:
                return $"{k.UnitId} takes {Keepsake.Name(k.ItemId, k.FallenId, content)}";
            case KeepsakeLost k:
                return k.CarrierId is { } carrier
                    ? $"{Keepsake.Name(k.ItemId, k.FallenId, content)} went with {carrier}"
                    : $"{Keepsake.Name(k.ItemId, k.FallenId, content)} was left at {k.At}";
            case Cantoed c:
                return c.From == c.To
                    ? $"{c.UnitId} stays at {c.To} (canto)"
                    : $"{c.UnitId} cantos {c.From} -> {c.To}" + (c.Path.Count > 1 ? " via " + string.Join(" ", c.Path.Take(c.Path.Count - 1)) : "");
            case Shoved s:
                return $"{s.UnitId} shoves {s.TargetId} {s.From} -> {s.To}";
            case UnitRetreated r:
                return $"{r.UnitId} falls back to {r.To} and will not fight this phase";
            case GrudgeSworn g:
                return $"{g.UnitId} swears a grudge against {g.AgainstId}";
            case UnitHealed h:
                return $"{h.UnitId} heals {h.Amount} (hp {h.HpAfter})";
            case UnitBurned b:
                return $"{b.UnitId} burns {b.Amount} (hp {b.HpAfter})";
            case WatchTaken w:
                return $"{w.UnitId} watches from {w.At}"
                    + (!w.Holds ? "" : w.HoldsInsteadOf is { } instead ? $"; holds instead of {w.At} -> {instead}" : "; holds (no move closer)")
                    + (w.PassedUpTargetId is { } passed ? $"; passes up {passed} at {w.PassedUpHit}" : "; no strike passed up");
            case WatchFired w:
                return $"{w.UnitId}'s watch fires on {w.TargetId} at {w.At}: " + (w.Strike.Hit ? (w.Strike.Crit ? "crit " : "hit ") + w.Strike.Damage : "miss") + $" ({w.TargetId} hp {w.Strike.TargetHpAfter})";
            case WatchHeld w:
                return $"{w.UnitId}'s watch holds on {w.TargetId} at {w.At}: {w.Hit} is under {Signatures.LedgerFloor}; she still watches";
            case WatchEnded w:
                return $"{w.UnitId} is struck and stops watching";
            case CoverTaken c:
                return $"{c.UnitId} covers {c.AllyId}; if struck, {c.AllyId} lands on {c.AllyLandsOn}" + (c.PassedUpTargetId is { } passedUp ? $"; passes up {passedUp} at {c.PassedUpHit}" : "; no strike passed up");
            case CoverFired c:
                return $"{c.UnitId} covers {c.AllyId}: steps onto {c.At}, {c.AllyId} to {c.AllyTo}; {c.AttackerId}'s strike "
                    + (c.WouldHaveKilled ? "would have killed" : "would not have killed") + $" {c.AllyId}" + (c.Counters ? "" : $"; {c.UnitId} cannot counter");
            case BlowRaised b:
                return $"{b.UnitId} raises a blow over {b.At} ({b.TargetId}); it lands at {b.UnitId}'s next phase start";
            case BlowLanded b:
                return $"{b.UnitId}'s blow lands on {b.TargetId} at {b.At} for {b.Damage} (hp {b.TargetHpAfter})";
            case BlowFell b:
                return $"{b.UnitId}'s blow falls on empty ground at {b.At}";
            case BlowBroken b:
                return $"{b.UnitId}'s blow over {b.At} is broken";
            case PhaseEnded p:
                return $"-- {p.Side.ToString().ToLowerInvariant()} phase ends, turn {p.Turn} --";
            case PhaseBegan p:
                return $"-- {p.Side.ToString().ToLowerInvariant()} phase, turn {p.Turn} --";
            case GroupWoke g:
                return $"group {g.Group} wakes: {WakeCauseText(g)}"
                    + (g.Lamps.Count > 0 ? $"; its lamps are lit ({string.Join(", ", g.Lamps.Select(l => $"{l.UnitId} {l.At}"))})" : "");
            case MapEventFired m:
                return $"event {m.Name}" + (m.Blocked ? " is blocked: its tile is held" : "");
            case TerrainChanged t:
                return $"  {t.At} becomes {(content.Terrain.TryGetValue(t.TerrainId, out var terrain) ? terrain.Name : t.TerrainId)}";
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
                return $"{i.UnitId} uses {content.ItemName(i.ItemId)}" + (i.TargetId == i.UnitId ? "" : " on " + i.TargetId) + $" ({i.UsesLeft} left)";
            case WeaponEquipped w:
                return $"{w.UnitId} equips {content.ItemName(w.ItemId)}";
            case ArtDeclared a:
                return $"{a.UnitId} declares {AbilityName(a.ArtId, content)} with {content.ItemName(a.ItemId)}, spending {a.Cost} extra uses";
            case WeaponBroke b:
                return $"{b.UnitId}'s {content.ItemName(b.ItemId)} breaks";
            case SpellSpent s:
                return $"{s.UnitId}'s {content.ItemName(s.ItemId)} is spent for this battle";
            default:
                return e.ToString() ?? "?";
        }
    }

    private static string AbilityName(string id, GameContent content) =>
        content.Abilities.TryGetValue(id, out var ability) ? ability.Name : id;

    private static string ClassName(string id, GameContent content) =>
        content.Classes.TryGetValue(id, out var unitClass) ? unitClass.Name : id;
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
