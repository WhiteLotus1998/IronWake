using System.Diagnostics;
using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// Headless harness. Runs the quality gates from DESIGN.md section 11.
/// --smoke runs the per-PR gates over every map under content/maps; --full runs the
/// per-map gates (1-4) on one map or on all of them, with gates 5-8 re-run on that map,
/// and exits non-zero on any failed gate. Gate 5 (forecast honesty) tallies every combat
/// of gate 6's random stream against the forecast asked before it. The player's roster
/// is the content's cast (units/cast.json, issue 13).
/// </summary>
public static class Program
{
    private const int Gate6Seeds = 100;
    private const int Gate6Commands = 100;
    private const int Gate8Sequences = 10000;
    private const int Gate8Commands = 20;
    private const int Gate7Seeds = 5;
    private const int Gate7LimitMs = 1000;
    private const int Gate5MinimumCombats = 10000;

    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--smoke")
        {
            return Smoke();
        }

        if (args.Length > 1 && args[0] == "--full")
        {
            var seeds = Gates.DefaultSeeds;
            RollScheme? scheme = RollScheme.TwoRollAverage;
            double? taxFloor = FreePrefix.DefaultTaxFloor;
            string? difficulty = null;
            for (var i = 2; i + 1 < args.Length; i++)
            {
                if (args[i] == "--difficulty")
                {
                    difficulty = args[i + 1];
                }

                else if (args[i] == "--seeds" && int.TryParse(args[i + 1], out var n) && n > 0)
                {
                    seeds = n;
                }
                else if (args[i] == "--scheme")
                {
                    scheme = ParseScheme(args[i + 1]);
                }
                else if (args[i] == "--taxfloor")
                {
                    taxFloor = double.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) && f >= 0 && f <= 1 ? f : null;
                }
            }

            if (scheme is { } full && taxFloor is { } floor)
            {
                return Full(args[1], seeds, full, floor, difficulty);
            }
        }

        if (args.Length > 0 && args[0] == "--keep")
        {
            return KeepGates(args.Skip(1).ToList());
        }

        if (args.Length > 1 && args[0] == "--hitband")
        {
            var seeds = HitBandSeeds;
            for (var i = 2; i + 1 < args.Length; i++)
            {
                if (args[i] == "--seeds" && int.TryParse(args[i + 1], out var n) && n > 0)
                {
                    seeds = n;
                }
            }

            return HitBandTable(args[1], seeds);
        }

        if (args.Length > 2 && args[0] == "--trace" && ulong.TryParse(args[2], out var traceSeed))
        {
            RollScheme? scheme = RollScheme.TwoRollAverage;
            for (var i = 3; i + 1 < args.Length; i++)
            {
                if (args[i] == "--scheme")
                {
                    scheme = ParseScheme(args[i + 1]);
                }
            }

            if (scheme is { } trace)
            {
                return Trace(args[1], traceSeed, trace);
            }
        }

        Console.WriteLine(Usage);
        return 2;
    }

    public const string Usage = "usage: ironwake-sim --smoke | --full <map> [--seeds N] [--scheme one|two] [--taxfloor F] [--difficulty D] | --full --all [--seeds N] [--scheme one|two] [--taxfloor F] [--difficulty D] | --trace <map> <seed> [--scheme one|two] | --hitband <map>|--all [--seeds N] | --keep [<edit> <x,y>]... [--seeds N] [--write <path>]";

    private const int HitBandSeeds = 50;

    /// <summary>
    /// The hit-band table of issue 158 for one map or every map: section 5's formulas under
    /// each roll scheme, with both sides' raw-hit histogram, the doubling rate, and gates 1
    /// and 4. A measurement only; nothing here changes what ships.
    /// </summary>
    public static int HitBandTable(string mapId, int seeds)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("hitband: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        var all = MapFiles.LoadAll(contentDir, content);
        var maps = mapId == "--all" ? all : all.Where(m => m.Id == mapId).ToList();
        if (maps.Count == 0)
        {
            Console.WriteLine($"hitband: no map '{mapId}' under {contentDir}; maps are {string.Join(", ", all.Select(m => m.Id))}");
            return 2;
        }

        Console.WriteLine($"hitband: {maps.Count} maps from {contentDir}, {seeds} seeds per cell");
        foreach (var (id, map) in maps)
        {
            foreach (var scheme in new[] { RollScheme.TwoRollAverage, RollScheme.OneRoll })
            {
                foreach (var line in HitBand.Cell(content, map, id, seeds, scheme))
                {
                    Console.WriteLine(line);
                }
            }
        }

        return 0;
    }

    /// <summary>The <c>--scheme</c> argument: <c>one</c> is one roll, <c>two</c> is the two-roll average; anything else is refused with the usage line.</summary>
    public static RollScheme? ParseScheme(string text) => RollSchemes.Parse(text);

    /// <summary>
    /// One game of the heuristic player on a map and a seed, printed as the script the CLI
    /// replays (player commands bare, enemy commands as <c>enemy:</c> lines, deaths and
    /// wakes as comments), so a baseline game can be read turn by turn or handed to
    /// <c>ironwake play --script</c>.
    /// </summary>
    public static int Trace(string mapId, ulong seed, RollScheme scheme = RollScheme.TwoRollAverage)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("trace: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        var maps = MapFiles.LoadAll(contentDir, content).Where(m => m.Id == mapId).ToList();
        if (maps.Count == 0)
        {
            Console.WriteLine($"trace: no map '{mapId}' under {contentDir}");
            return 2;
        }

        var state = BattleState.From(maps[0].Map, content, content.Cast, seed, scheme);
        var player = new HeuristicPlayer();
        Console.WriteLine($"# {mapId} seed {seed}, heuristic player, {Gates.Name(scheme)}");
        while (!state.Outcome.IsOver)
        {
            var enemy = state.Phase == Side.Enemy;
            var commands = enemy ? EnemyAi.Plan(state, content) : player.Next(state, content);
            foreach (var command in commands)
            {
                var result = Resolver.Apply(state, content, command);
                if (!result.Accepted)
                {
                    Console.WriteLine($"# {command} was rejected: {result.Rejection!.Message}");
                    return 1;
                }

                Console.WriteLine((enemy ? "# enemy: " : "") + Script(command));
                foreach (var e in result.Events)
                {
                    switch (e)
                    {
                        case CombatFought f:
                            Console.WriteLine($"#   {f.AttackerId} vs {f.TargetId}: {string.Join(" ", f.Strikes.Select(s => s.Hit ? (s.Crit ? "crit " : "hit ") + s.Damage : "miss"))}; {f.AttackerId} {f.AttackerHpAfter} hp, {f.TargetId} {f.TargetHpAfter} hp");
                            break;
                        case UnitDied d:
                            Console.WriteLine($"#   {d.UnitId} died at {d.At}");
                            break;
                        case GroupWoke w:
                            Console.WriteLine($"#   group {w.Group} woke ({w.Cause})");
                            break;
                        case MapEventFired m:
                            Console.WriteLine($"#   event {m.Name}" + (m.Blocked ? " blocked" : " fired"));
                            break;
                        case PhaseBegan p when p.Side == Side.Player:
                            Console.WriteLine($"# turn {p.Turn}");
                            break;
                    }
                }

                state = result.Next;
                if (state.Outcome.IsOver)
                {
                    break;
                }
            }
        }

        Console.WriteLine($"# {state.Outcome.Result} on turn {state.Turn}: {state.Outcome.Reason}");
        return 0;
    }

    /// <summary>
    /// A command as the CLI's script reader takes it. Slots print one-based, as the CLI reads
    /// them since issue 101 (issue 118); the core's own slot is one lower.
    /// </summary>
    public static string Script(Command command) => command switch
    {
        Move m => $"move {m.UnitId} {m.To}",
        Attack a => a.Slot is { } slot ? $"attack {a.UnitId} {a.TargetId} {slot + 1}" : $"attack {a.UnitId} {a.TargetId}",
        UseItem u => u.TargetId is { } t ? $"item {u.UnitId} {u.Slot + 1} {t}" : $"item {u.UnitId} {u.Slot + 1}",
        Wait w => $"wait {w.UnitId}",
        Exit x => $"exit {x.UnitId}",
        Retreat r => $"retreat {r.UnitId} {r.To}",
        Canto c => $"canto {c.UnitId} {c.To}",
        EndPhase => "end",
        Recall r => $"recall {r.ToIndex}",
        _ => command.ToString() ?? "",
    };

    /// <summary>
    /// All eight gates on one map (or every map with <c>--all</c>): 5 to 8 as the smoke runs
    /// them, then 1 to 4 over <paramref name="seeds"/> seeds under <paramref name="scheme"/>,
    /// then issue 47's free prefix and turn-state counters, printed and never judged, with
    /// first quiet read at <paramref name="taxFloor"/>. Given a <paramref name="difficulty"/> from
    /// rules.json, every map is played under it (issue 76), so Hard is measured without the map
    /// files changing; without one, maps are played as authored.
    /// </summary>
    public static int Full(string mapId, int seeds, RollScheme scheme = RollScheme.TwoRollAverage, double taxFloor = FreePrefix.DefaultTaxFloor, string? difficulty = null)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("full: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        var all = MapFiles.LoadAll(contentDir, content);
        var maps = mapId == "--all" ? all : all.Where(m => m.Id == mapId).ToList();
        if (maps.Count == 0)
        {
            Console.WriteLine($"full: no map '{mapId}' under {contentDir}; maps are {string.Join(", ", all.Select(m => m.Id))}");
            return 2;
        }

        if (difficulty is not null)
        {
            if (!content.Difficulties.TryGetValue(difficulty, out var chosen))
            {
                var known = content.Difficulties.Count == 0 ? "rules.json declares none" : "they are " + string.Join(", ", content.Difficulties.Keys);
                Console.WriteLine($"full: no difficulty '{difficulty}'; {known}");
                return 2;
            }

            maps = maps.Select(m => (m.Id, m.Map.Under(chosen))).ToList();
        }

        Console.WriteLine($"full: {maps.Count} maps from {contentDir}, {seeds} seeds, {Gates.Name(scheme)}" + (difficulty is null ? "" : $", difficulty {difficulty}"));
        var failed = false;
        foreach (var (id, map) in maps)
        {
            var one = new[] { (id, map) };
            var tally = new Gates.ForecastTally();
            Gates.ForecastStream(content, tally, Gate5MinimumCombats);
            var readings = new List<IReadOnlyList<TurnReading>>();
            var (gate1, baseline) = Gates.Gate1(content, map, id, seeds, scheme, readings: readings);
            var rows = new List<GateResult>
            {
                gate1,
                Gates.Gate2(content, map, id, seeds, scheme),
                Gates.Gate3(content, map, id),
                Gates.Gate4(content, map, id, baseline, scheme),
                Gate6(content, one, tally),
                tally.Result(Gate5MinimumCombats),
                Gate7(content, one),
                Gate8(content, one),
            };
            foreach (var row in rows)
            {
                Console.WriteLine(row.Line);
                failed |= !row.Passed;
            }

            foreach (var line in FreePrefix.Report(content, map, id, baseline, readings, scheme, taxFloor))
            {
                Console.WriteLine(line);
            }
        }

        Console.WriteLine(failed ? "full: FAILED" : "full: ok");
        return failed ? 1 : 0;
    }

    /// <summary>
    /// Issue 82's experiment: the keep under <c>content/keep</c> with the named edits made in order,
    /// written by <see cref="MapFormat.Write"/> and parsed back (the edited keep is an ordinary map),
    /// then gates 1, 2 and 4 on it, or with <c>--write</c> only the edited keep to a file for
    /// <c>play</c>. Prints the edited grid so a run is its own record.
    /// </summary>
    private static int KeepGates(IReadOnlyList<string> args)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("keep: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        var menu = content.Campaign.Keep;
        if (menu == KeepMenu.None)
        {
            Console.WriteLine("keep: the campaign has no keep");
            return 2;
        }

        var map = MapFiles.Load(Path.Combine(contentDir, "keep", menu.MapId + ".map"), content);
        var seeds = Gates.DefaultSeeds;
        var spent = 0;
        var made = new List<string>();
        string? write = null;
        for (var i = 0; i + 1 < args.Count; i += 2)
        {
            if (args[i] == "--seeds" && int.TryParse(args[i + 1], out var n) && n > 0)
            {
                seeds = n;
                continue;
            }

            if (args[i] == "--write")
            {
                write = args[i + 1];
                continue;
            }

            var parts = args[i + 1].Split(',');
            if (menu.Edit(args[i]) is not { } edit || parts.Length != 2 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
            {
                Console.WriteLine($"keep: '{args[i]} {args[i + 1]}' is not an edit on the menu at an x,y tile; the menu is {string.Join(", ", menu.Edits.Select(e => e.Id))}");
                return 2;
            }

            var at = new Coord(x, y);
            if (Keep.Refusal(map, edit, at) is { } refusal)
            {
                Console.WriteLine($"keep: {edit.Id} at {at} refused: {refusal}");
                return 2;
            }

            map = Keep.Apply(map, edit, at);
            spent += edit.Price;
            made.Add($"{edit.Id} {at}");
        }

        var text = MapFormat.Write(map, content);
        var reparsed = MapFormat.Parse(menu.MapId + ".map", text, content);
        if (reparsed != map || MapFormat.Write(reparsed, content) != text)
        {
            Console.WriteLine("keep: the edited keep does not read back equal to what was written");
            return 1;
        }

        if (write is not null)
        {
            File.WriteAllText(write, text);
            Console.WriteLine($"keep: written to {write}");
            return 0;
        }

        var id = made.Count == 0 ? menu.MapId : menu.MapId + " + " + string.Join(", ", made);
        Console.WriteLine($"keep: {id}; spent {spent}; {seeds} seeds");
        foreach (var row in text.Split('\n').SkipWhile(l => l.Length > 0).Skip(1).TakeWhile(l => l.Length > 0))
        {
            Console.WriteLine("  " + row);
        }

        var (gate1, baseline) = Gates.Gate1(content, reparsed, id, seeds, RollScheme.TwoRollAverage);
        var rows = new[] { gate1, Gates.Gate2(content, reparsed, id, seeds, RollScheme.TwoRollAverage), Gates.Gate4(content, reparsed, id, baseline, RollScheme.TwoRollAverage) };
        foreach (var row in rows)
        {
            Console.WriteLine(row.Line);
        }

        return rows.All(r => r.Passed) ? 0 : 1;
    }

    private static int Smoke()
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("smoke: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        var maps = MapFiles.LoadAll(contentDir, content);
        Console.WriteLine($"smoke: {maps.Count} maps from {contentDir}");
        var failed = false;
        var tally = new Gates.ForecastTally();
        Gates.ForecastStream(content, tally, Gate5MinimumCombats);
        failed |= !Print(Gate6(content, maps, tally));
        failed |= !Print(tally.Result(Gate5MinimumCombats));
        failed |= !Print(Gate7(content, maps));
        failed |= !Print(Gate8(content, maps));
        Console.WriteLine(failed ? "smoke: FAILED" : "smoke: ok");
        return failed ? 1 : 0;
    }

    private static bool Print(GateResult result)
    {
        Console.WriteLine(result.Line);
        return result.Passed;
    }

    /// <summary>Gate 6: the same seed and commands replay byte-identical through <see cref="BattleState.Canonical"/>. Every combat of the first run feeds gate 5's tally.</summary>
    public static GateResult Gate6(GameContent content, IReadOnlyList<(string Id, MapDefinition Map)> maps, Gates.ForecastTally tally)
    {
        var watch = Stopwatch.StartNew();
        var ok = true;
        var detail = "";
        foreach (var (id, map) in maps)
        {
            for (var seed = 1; seed <= Gate6Seeds; seed++)
            {
                var commands = new List<Command>();
                var random = new Random(seed);
                var (first, firstEvents) = Run(content, map, (ulong)seed, Gate6Commands, random, commands, null, tally);
                var (second, secondEvents) = Run(content, map, (ulong)seed, Gate6Commands, null, null, commands, null);
                if (first != second || firstEvents != secondEvents)
                {
                    detail = $"{id} seed {seed} replayed differently; ";
                    ok = false;
                    break;
                }
            }
        }

        return new GateResult($"gate 6 determinism: {detail}{maps.Count} maps x {Gate6Seeds} seeds x {Gate6Commands} commands, {watch.ElapsedMilliseconds} ms: {Gates.Verdict(ok)}", ok);
    }

    /// <summary>
    /// Gate 7: a full map, the random player against <see cref="EnemyAi.Plan"/>, resolves
    /// in under a second; the same seed replayed lands on the same canonical state, which
    /// is gate 6 over the AI's phases. The slowest game per map is what is printed and judged.
    /// </summary>
    private static GateResult Gate7(GameContent content, IReadOnlyList<(string Id, MapDefinition Map)> maps)
    {
        var ok = true;
        var lines = new List<string>();
        foreach (var (id, map) in maps)
        {
            long slowest = 0;
            var turns = 0;
            for (var seed = 1; seed <= Gate7Seeds; seed++)
            {
                var watch = Stopwatch.StartNew();
                var (first, firstTurns) = FullGame(content, map, (ulong)seed);
                watch.Stop();
                slowest = Math.Max(slowest, watch.ElapsedMilliseconds);
                turns += firstTurns;
                var (second, _) = FullGame(content, map, (ulong)seed);
                if (first != second)
                {
                    lines.Add($"gate 6 determinism: {id} seed {seed} AI-vs-AI game replayed differently: FAILED");
                    ok = false;
                }
            }

            var fast = slowest < Gate7LimitMs;
            ok &= fast;
            lines.Add($"gate 7 speed: {id}, {Gate7Seeds} AI-vs-AI games, {turns} turns, slowest {slowest} ms: {Gates.Verdict(fast)}");
        }

        return new GateResult(string.Join('\n', lines), ok);
    }

    /// <summary>The random player against the enemy AI until the battle is decided. Returns the canonical end state and the turns played.</summary>
    private static (string State, int Turns) FullGame(GameContent content, MapDefinition map, ulong seed)
    {
        var state = BattleState.From(map, content, content.Cast, seed);
        var random = new Random((int)seed);
        while (!state.Outcome.IsOver)
        {
            if (state.Phase == Side.Player)
            {
                var legal = Resolver.Legal(state, content).ToList();
                state = Apply(state, content, legal[random.Next(legal.Count)]);
            }
            else
            {
                foreach (var command in EnemyAi.Plan(state, content))
                {
                    state = Apply(state, content, command);
                }
            }
        }

        return (state.Canonical(), state.Turn);
    }

    private static BattleState Apply(BattleState state, GameContent content, Command command)
    {
        var result = Resolver.Apply(state, content, command);
        if (!result.Accepted)
        {
            throw new InvalidOperationException($"{command} was rejected: {result.Rejection!.Message}");
        }

        return result.Next;
    }

    /// <summary>Gate 8: random legal command sequences throw nothing and are never rejected.</summary>
    private static GateResult Gate8(GameContent content, IReadOnlyList<(string Id, MapDefinition Map)> maps)
    {
        var detail = "";
        var watch = Stopwatch.StartNew();
        var perMap = Math.Max(1, Gate8Sequences / Math.Max(1, maps.Count));
        var ok = true;
        foreach (var (id, map) in maps)
        {
            for (var sequence = 1; sequence <= perMap && ok; sequence++)
            {
                try
                {
                    Run(content, map, (ulong)sequence, Gate8Commands, new Random(sequence), null, null, null);
                }
                catch (Exception ex)
                {
                    detail = $"{id} sequence {sequence}: {ex.GetType().Name}: {ex.Message}; ";
                    ok = false;
                }
            }
        }

        return new GateResult($"gate 8 crash-free: {detail}{maps.Count} maps x {perMap} sequences x {Gate8Commands} commands, {watch.ElapsedMilliseconds} ms: {Gates.Verdict(ok)}", ok);
    }

    /// <summary>
    /// Plays random legal commands (with a Recall now and then) from <paramref name="random"/>,
    /// or replays <paramref name="replay"/>, recording what was played into
    /// <paramref name="record"/>. Returns the final canonical state and the event log.
    /// The random alphabet also recalls to the middle of the raw history, which may fall
    /// inside an enemy phase; that refusal (issue 190) is expected, leaves the state as it
    /// was, and is not recorded. Any other rejected command is a harness fault and throws. Every Attack's forecast, asked
    /// before it is applied, is counted against its combat in <paramref name="tally"/>.
    /// </summary>
    private static (string State, string Events) Run(
        GameContent content, MapDefinition map, ulong seed, int commands, Random? random, List<Command>? record, List<Command>? replay, Gates.ForecastTally? tally)
    {
        var state = BattleState.From(map, content, content.Cast, seed);
        var events = new System.Text.StringBuilder();
        for (var i = 0; i < commands; i++)
        {
            Command command;
            if (replay is not null)
            {
                if (i >= replay.Count)
                {
                    break;
                }

                command = replay[i];
            }
            else
            {
                var legal = Resolver.Legal(state, content).ToList();
                var targets = state.RecallTargets().ToList();
                if (state.RecallCharges > 0 && targets.Count > 0)
                {
                    legal.Add(new Recall(targets[targets.Count / 2]));
                }

                if (state.RecallCharges > 0 && state.History.Count > 0)
                {
                    legal.Add(new Recall(state.History.Count / 2));
                }

                if (legal.Count == 0)
                {
                    break;
                }

                command = legal[random!.Next(legal.Count)];
            }

            CombatForecast? forecast = null;
            if (tally is not null && command is Attack attack)
            {
                forecast = Queries.Forecast(state, content, state.Find(attack.UnitId)!, state.Find(attack.TargetId)!, attack.Slot);
            }

            var result = Resolver.Apply(state, content, command);
            if (!result.Accepted)
            {
                if (replay is null && command is Recall && result.Rejection!.Reason == RejectionReason.NotAPlayerPhase && result.Next == state)
                {
                    continue;
                }

                throw new InvalidOperationException($"{command} was rejected: {result.Rejection!.Message}");
            }

            if (forecast is not null)
            {
                tally!.Count(forecast, result.Events.OfType<CombatFought>().Single());
            }

            record?.Add(command);
            foreach (var e in result.Events)
            {
                events.Append(e).Append('\n');
            }

            state = result.Next;
        }

        return (state.Canonical(), events.ToString());
    }

    private static string? FindContent()
    {
        if (File.Exists(Path.Combine("content", ContentFiles.TerrainName)))
        {
            return Path.GetFullPath("content");
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "content");
            if (File.Exists(Path.Combine(candidate, ContentFiles.TerrainName)))
            {
                return candidate;
            }
        }

        return null;
    }
}
