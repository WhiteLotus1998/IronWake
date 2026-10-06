using System.Diagnostics;
using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// Headless harness. Runs the quality gates from DESIGN.md section 11.
/// --smoke runs the per-PR gates over every map under content/maps; --full runs the
/// per-map gates (1-4) on one map or on all of them, with gates 5-8 re-run on that map
/// (one map may be the keep or its raid under content/keep, issue 288),
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
    private const string LadderSmokeMap = "the_tollgate";

    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--smoke")
        {
            return Smoke();
        }

        if (args.Length > 0 && args[0] == "--ceiling")
        {
            return Ceiling();
        }

        if (args.Length > 1 && args[0] == "--full")
        {
            var seeds = Gates.DefaultSeeds;
            RollScheme? scheme = RollScheme.TwoRollAverage;
            double? taxFloor = FreePrefix.DefaultTaxFloor;
            string? difficulty = null;
            string? origin = null;
            var lead = new List<string>();
            for (var i = 2; i + 1 < args.Length; i++)
            {
                if (args[i] == "--difficulty")
                {
                    difficulty = args[i + 1];
                }

                else if (args[i] == "--lead")
                {
                    lead.Add(args[i + 1]);
                }

                else if (args[i] == "--origin")
                {
                    origin = args[i + 1];
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
                return Full(args[1], seeds, full, floor, difficulty, lead, origin);
            }
        }

        if (args.Length > 1 && args[0] == "--finale")
        {
            var seeds = Gates.DefaultSeeds;
            var level = FinaleRun.DefaultLevel;
            RollScheme? scheme = RollScheme.TwoRollAverage;
            for (var i = 2; i + 1 < args.Length; i++)
            {
                if (args[i] == "--seeds" && int.TryParse(args[i + 1], out var n) && n > 0)
                {
                    seeds = n;
                }
                else if (args[i] == "--level" && int.TryParse(args[i + 1], out var l) && l >= Unit.MinLevel && l <= Unit.MaxLevel)
                {
                    level = l;
                }
                else if (args[i] == "--scheme")
                {
                    scheme = ParseScheme(args[i + 1]);
                }
            }

            if (scheme is { } finale)
            {
                return Finale(args[1], seeds, level, finale, args.Skip(2).Contains("--gates"));
            }
        }

        if (args.Length > 1 && args[0] == "--heirloom")
        {
            var seeds = Gates.DefaultSeeds;
            for (var i = 2; i + 1 < args.Length; i++)
            {
                if (args[i] == "--seeds" && int.TryParse(args[i + 1], out var n) && n > 0)
                {
                    seeds = n;
                }
            }

            return HeirloomTable(args[1], seeds, args.Contains("--quest"));
        }

        if (args.Length > 0 && args[0] == "--kinsbane")
        {
            var seeds = Gates.DefaultSeeds;
            for (var i = 1; i + 1 < args.Length; i++)
            {
                if (args[i] == "--seeds" && int.TryParse(args[i + 1], out var n) && n > 0)
                {
                    seeds = n;
                }
            }

            return KinsbaneTable(seeds, args.Contains("--axe") ? KinsbaneRun.Arm.Axe : args.Contains("--heeding") ? KinsbaneRun.Arm.Heeding : KinsbaneRun.Arm.Committed);
        }

        if (args.Length > 0 && args[0] == "--levels")
        {
            var seeds = Gates.DefaultSeeds;
            for (var i = 1; i + 1 < args.Length; i++)
            {
                if (args[i] == "--seeds" && int.TryParse(args[i + 1], out var n) && n > 0)
                {
                    seeds = n;
                }
            }

            return args.Contains("--drill") ? DrillTable(seeds) : args.Contains("--chip") ? ChipTable(seeds) : args.Contains("--gate") ? GateTable(seeds) : args.Contains("--rank-trace") ? RankTraceTable(seeds) : args.Contains("--focused") ? FocusedTable(seeds) : LevelTable(seeds, args.Contains("--even"));
        }

        if (args.Length > 0 && args[0] == "--supports")
        {
            var seeds = Gates.DefaultSeeds;
            (string A, string B)? pair = null;
            for (var i = 1; i + 1 < args.Length; i++)
            {
                if (args[i] == "--seeds" && int.TryParse(args[i + 1], out var n) && n > 0)
                {
                    seeds = n;
                }
                else if (args[i] == "--pair" && i + 2 < args.Length)
                {
                    pair = (args[i + 1], args[i + 2]);
                }
            }

            return SupportTable(seeds, pair);
        }

        if (args.Length > 0 && args[0] == "--curve")
        {
            var seeds = Gates.DefaultSeeds;
            string? only = null;
            (string Unit, string Item)? carry = null;
            var items = false;
            for (var i = 1; i < args.Length; i++)
            {
                if (args[i] == "--items")
                {
                    items = true;
                }
                else if (i + 1 >= args.Length)
                {
                    break;
                }
                else if (args[i] == "--seeds" && int.TryParse(args[i + 1], out var n) && n > 0)
                {
                    seeds = n;
                }
                else if (args[i] == "--map")
                {
                    only = args[i + 1];
                }
                else if (args[i] == "--carry" && i + 2 < args.Length)
                {
                    carry = (args[i + 1], args[i + 2]);
                }
            }

            return CurveTable(seeds, only, carry, items);
        }

        if (args.Length > 0 && args[0] == "--ladder")
        {
            var seeds = Gates.DefaultSeeds;
            string? only = null;
            for (var i = 1; i + 1 < args.Length; i++)
            {
                if (args[i] == "--seeds" && int.TryParse(args[i + 1], out var n) && n > 0)
                {
                    seeds = n;
                }
                else if (args[i] == "--map")
                {
                    only = args[i + 1];
                }
            }

            return LadderTable(seeds, only);
        }

        if (args.Length > 0 && args[0] == "--keep")
        {
            return KeepGates(args.Skip(1).ToList());
        }

        if (args.Length > 0 && args[0] == "--drover")
        {
            if (FindContent() is not { } droverContent)
            {
                Console.WriteLine("drover: no content directory found from the working directory or the build output");
                return 1;
            }

            var droverLines = DroverMeasure.Lines(ContentLoader.Load(droverContent), RollScheme.TwoRollAverage);
            foreach (var line in droverLines)
            {
                Console.WriteLine(line);
            }

            return droverLines[^1].StartsWith(DroverMeasure.FailVerdict, StringComparison.Ordinal) ? 1 : 0;
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

        if (args.Length > 1 && args[0] == "--campaign-script" && ulong.TryParse(args[1], out var scriptSeed))
        {
            var last = args.Length > 3 && args[2] == "--to" && ulong.TryParse(args[3], out var to) ? to : scriptSeed;
            var write = Array.IndexOf(args, "--write") is var w and >= 0 && w + 1 < args.Length ? args[w + 1] : null;
            var difficulty = Array.IndexOf(args, "--difficulty") is var d and >= 0 && d + 1 < args.Length ? args[d + 1] : CampaignRecord.NormalDifficulty;
            var permadeath = !(Array.IndexOf(args, "--permadeath") is var pd and >= 0 && pd + 1 < args.Length && args[pd + 1] == "off");
            var variant = Array.IndexOf(args, "--variant") is var v and >= 0 && v + 1 < args.Length && int.TryParse(args[v + 1], out var parsedVariant) ? parsedVariant : 0;
            var quest = Array.IndexOf(args, "--quest") is var q and >= 0 && q + 1 < args.Length ? args[q + 1] : null;
            (string, string)? until = Array.IndexOf(args, "--until-certify") is var u and >= 0 && u + 2 < args.Length ? (args[u + 1], args[u + 2]) : null;
            return CampaignScriptRun(scriptSeed, last, write, difficulty, permadeath, variant, quest, until);
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

    public const string Usage = "usage: ironwake-sim --smoke | --ceiling | --full <map|file> [--seeds N] [--scheme one|two] [--taxfloor F] [--difficulty D] [--lead <id>]... [--origin <id>] | --full --all [--seeds N] [--scheme one|two] [--taxfloor F] [--difficulty D] | --trace <map> <seed> [--scheme one|two] | --campaign-script <seed> [--to <seed>] [--write <path>] [--difficulty D] [--permadeath on|off] [--variant N] [--quest <id>] [--until-certify <unit> <class>] | --hitband <map>|--all [--seeds N] | --keep [<edit> <x,y>]... [--seeds N] [--write <path>] | --finale <map|file> [--seeds N] [--level N] [--scheme one|two] [--gates] | --heirloom <item> [--seeds N] [--quest] | --kinsbane [--seeds N] [--axe|--heeding] | --levels [--seeds N] [--even|--focused|--rank-trace|--gate|--chip|--drill] | --supports [--seeds N] [--pair <a> <b>] | --curve [--seeds N] [--map <id>] [--carry <unit> <weapon>] [--items] | --ladder [--seeds N] [--map <id>]";

    private const int HitBandSeeds = 50;

    /// <summary>
    /// The keep finale's measurement (issue 692, <see cref="FinaleRun"/>): a <c>deploy: all</c> map,
    /// a campaign map, the keep, or a file, played with the full, depleted and floor companies at
    /// <paramref name="level"/>, each with its gate 1, length and time lines. With
    /// <paramref name="gates"/> (issue 1149), the full company also takes gate 2 (random) and gate 4
    /// (dead weight) at that level, against its own heuristic games. Exits non-zero when full or
    /// depleted fails gate 1, any company is slow or long, or a gate asked for fails.
    /// </summary>
    public static int Finale(string mapId, int seeds, int level, RollScheme scheme, bool gates = false)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("finale: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        MapDefinition? map = MapFiles.LoadAll(contentDir, content).FirstOrDefault(m => m.Id == mapId).Map;
        if (map is null && content.Campaign.Keep.IsKeepMap(mapId))
        {
            map = MapFiles.Load(MapFiles.CampaignPath(contentDir, content, mapId), content);
        }

        if (map is null && File.Exists(mapId))
        {
            map = MapFiles.Load(mapId, content);
        }

        if (map is null)
        {
            Console.WriteLine($"finale: no map '{mapId}' under {contentDir}, and no such file");
            return 2;
        }

        if (FinaleRun.Refusal(map, mapId) is { } refusal)
        {
            Console.WriteLine(refusal);
            return 2;
        }

        Console.WriteLine($"finale: {mapId}, {seeds} seeds, {Gates.Name(scheme)}, story members at level {level}, hires at {Math.Max(Unit.MinLevel, level - Barracks.LevelsBelow)}");
        var failed = false;
        foreach (var company in new[] { FinaleRun.Company.Full, FinaleRun.Company.Depleted, FinaleRun.Company.Floor })
        {
            var reading = FinaleRun.Measure(content, map, company, level, seeds, scheme);
            foreach (var line in reading.Lines(level))
            {
                Console.WriteLine(line);
            }

            failed |= !reading.Passed;
            if (gates && company == FinaleRun.Company.Full)
            {
                var fielded = FinaleRun.Fielded(content, company, level);
                foreach (var gate in new[] { Gates.Gate2(fielded, map, mapId, seeds, scheme), Gates.Gate4(fielded, map, mapId, reading.Games, scheme) })
                {
                    Console.WriteLine("  " + gate.Line.Replace("\n", "\n  "));
                    failed |= !gate.Passed;
                }
            }
        }

        Console.WriteLine(failed ? "finale: FAILED" : "finale: ok");
        return failed ? 1 : 0;
    }

    /// <summary>
    /// The heirloom's timing table (issue 646, <see cref="HeirloomRun"/>): the campaign map each
    /// stage turns on under the heuristic player, and the combats fought with it by each map's end.
    /// </summary>
    public static int HeirloomTable(string itemId, int seeds, bool quest = false)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("heirloom: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        if (!content.Weapons.TryGetValue(itemId, out var item) || item.Heirloom is null)
        {
            Console.WriteLine($"heirloom: '{itemId}' is not an heirloom; heirlooms are {string.Join(", ", content.Weapons.Values.Where(w => w.Heirloom is not null).Select(w => w.Id))}");
            return 2;
        }

        foreach (var line in HeirloomRun.Lines(content, itemId, HeirloomRun.Measure(contentDir, content, itemId, seeds, quest)))
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    /// <summary>
    /// Kinsbane's timing table (issue 804, <see cref="KinsbaneRun"/>): the campaign map each tooth grows
    /// on, the fifth waking it, with Keziah picked, armed and fielded under the heuristic player;
    /// <paramref name="arm"/> picks the arm: committed, heeding (benched on a <c>keziah_warning</c>
    /// map, issue 871) or the iron-axe control on the same seeds (round 263).
    /// </summary>
    public static int KinsbaneTable(int seeds, KinsbaneRun.Arm arm = KinsbaneRun.Arm.Committed)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("kinsbane: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        foreach (var line in KinsbaneRun.Lines(KinsbaneRun.Measure(contentDir, content, seeds, arm), arm))
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    /// <summary>
    /// The supports' climb (issue 77): <see cref="SupportRun.Lines"/> over <see cref="LevelRun.Measure"/>'s runs,
    /// under <see cref="PairingPlayer"/> when <paramref name="pair"/> names a support pair (slice 5).
    /// </summary>
    public static int SupportTable(int seeds, (string A, string B)? pair = null)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("supports: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        if (pair is { } p && Supports.Pair(content.Campaign, p.A, p.B) is null)
        {
            Console.WriteLine($"supports: {p.A} and {p.B} are not a support pair in campaign.json");
            return 1;
        }

        foreach (var line in SupportRun.Lines(content, LevelRun.Measure(contentDir, content, seeds, pair), pair))
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    /// <summary>
    /// The second tier's timing (issue 704): the company's levels after each campaign map under the
    /// heuristic player, <see cref="LevelRun"/>. A measurement only; nothing here changes what ships.
    /// </summary>
    /// <remarks>With <paramref name="even"/> set it plays the even-company chair and prints its read instead (issue 1150, <see cref="LevelRun.EvenLines"/>).</remarks>
    public static int LevelTable(int seeds, bool even = false)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("levels: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        var runs = LevelRun.Measure(contentDir, content, seeds, even: even);
        foreach (var line in even ? LevelRun.EvenLines(content, runs) : LevelRun.Lines(content, runs))
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    /// <summary>
    /// The focused chair's read (issue 1157, round 389, <see cref="LevelRun.FocusedLines"/>): the even chair,
    /// the guarded, the paying and the striking (issue 1167) focused chairs over the same seeds, one thread each, since every run owns
    /// its player and the content is read-only. A measurement only.
    /// </summary>
    public static int FocusedTable(int seeds)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("levels: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        var even = Task.Run(() => LevelRun.Measure(contentDir, content, seeds, even: true));
        var guarded = Task.Run(() => LevelRun.Measure(contentDir, content, seeds, focused: FocusedPlayer.Guard.Guarded));
        var paying = Task.Run(() => LevelRun.Measure(contentDir, content, seeds, focused: FocusedPlayer.Guard.Paying));
        var striking = Task.Run(() => LevelRun.Measure(contentDir, content, seeds, focused: FocusedPlayer.Guard.Striking));
        foreach (var line in LevelRun.FocusedLines(content, even.Result, guarded.Result, paying.Result, striking.Result))
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    /// <summary>
    /// The door's points gate read (issue 1174, round 394, <see cref="LevelRun.GateLines"/>): the striking chair
    /// and the even chair over the same seeds, each chair's seeds split across four threads in order. A measurement only.
    /// </summary>
    public static int GateTable(int seeds)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("levels: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        var striking = Split(seeds, (count, first) => LevelRun.Measure(contentDir, content, count, focused: FocusedPlayer.Guard.Striking, firstSeed: first));
        var even = Split(seeds, (count, first) => LevelRun.Measure(contentDir, content, count, even: true, firstSeed: first));
        foreach (var line in LevelRun.GateLines(content, even.Result, striking.Result))
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    /// <summary>
    /// The chip's read (issue 1178, round 395, <see cref="LevelRun.ChipLines"/>): the striking chair with and
    /// without the chip and the even chair over the same seeds, each chair's seeds split across four threads in
    /// order. A measurement only.
    /// </summary>
    public static int ChipTable(int seeds)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("levels: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        var striking = Split(seeds, (count, first) => LevelRun.Measure(contentDir, content, count, focused: FocusedPlayer.Guard.Striking, firstSeed: first));
        var chipping = Split(seeds, (count, first) => LevelRun.Measure(contentDir, content, count, focused: FocusedPlayer.Guard.Chipping, firstSeed: first));
        var even = Split(seeds, (count, first) => LevelRun.Measure(contentDir, content, count, even: true, firstSeed: first));
        foreach (var line in LevelRun.ChipLines(even.Result, striking.Result, chipping.Result))
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    /// <summary>
    /// The drill's print (issue 1181, round 396, <see cref="LevelRun.DrillLines"/>): the chipping chair and the even
    /// chair over the same seeds, each chair's seeds split across four threads in order. A measurement only.
    /// </summary>
    public static int DrillTable(int seeds)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("levels: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        var chipping = Split(seeds, (count, first) => LevelRun.Measure(contentDir, content, count, focused: FocusedPlayer.Guard.Chipping, firstSeed: first));
        var even = Split(seeds, (count, first) => LevelRun.Measure(contentDir, content, count, even: true, firstSeed: first));
        foreach (var line in LevelRun.DrillLines(even.Result, chipping.Result))
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    /// <summary>Seeds 1..<paramref name="seeds"/> split across four threads in order, each part measured by <paramref name="measure"/> (count, first seed), the runs joined in seed order.</summary>
    private static Task<List<LevelRun.Run>> Split(int seeds, Func<int, int, IReadOnlyList<LevelRun.Run>> measure)
    {
        const int Threads = 4;
        var chunk = (seeds + Threads - 1) / Threads;
        var parts = Enumerable.Range(0, Threads)
            .Select(k => (First: 1 + k * chunk, Count: Math.Min(chunk, seeds - k * chunk)))
            .Where(p => p.Count > 0)
            .Select(p => Task.Run(() => measure(p.Count, p.First)))
            .ToList();
        return Task.WhenAll(parts).ContinueWith(t => t.Result.SelectMany(r => r).ToList(), TaskScheduler.Default);
    }

    /// <summary>
    /// The rank trace (issue 1170, round 393, <see cref="LevelRun.RankTraceLines"/>): the striking chair alone,
    /// its seeds split across four threads in order, since every run depends on its seed alone. A measurement only.
    /// </summary>
    public static int RankTraceTable(int seeds)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("levels: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        const int Threads = 4;
        var chunk = (seeds + Threads - 1) / Threads;
        var parts = Enumerable.Range(0, Threads)
            .Select(k => (First: 1 + k * chunk, Count: Math.Min(chunk, seeds - k * chunk)))
            .Where(p => p.Count > 0)
            .Select(p => Task.Run(() => LevelRun.Measure(contentDir, content, p.Count, focused: FocusedPlayer.Guard.Striking, firstSeed: p.First)))
            .ToList();
        var runs = parts.SelectMany(t => t.Result).ToList();
        Console.WriteLine(LevelRun.PriceLine(runs, "striking"));
        foreach (var line in LevelRun.RankTraceLines(content, runs))
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    /// <summary>
    /// The campaign's curve (issue 704, rounds 225 and 226): gate 1 on each campaign map as its file
    /// reads and as the campaign fights it (<see cref="CampaignMap.Prepare"/>: the curve's enemy level
    /// and the template swaps), side by side, so a tuned map whose campaign number falls under 60 is
    /// seen before its standalone scores are cited for the campaign, and a third line with the file's party
    /// raised by as many levels as the curve raised the enemy, a proxy for the company a campaign player
    /// brings. Then, on every map, <c>carried</c> and <c>carried, spread</c> (issue 764): the file's party at the
    /// levels <see cref="LevelRun"/>'s campaign brings to that camp, slot by slot, and the same total spread
    /// evenly (<see cref="AtSlotLevels"/>); 0161's bend reads <c>carried</c>. A map the campaign fights as its file reads prints its file line once. <paramref name="only"/> names one map. A measurement only.
    /// </summary>
    public static int CurveTable(int seeds, string? only) => CurveTable(seeds, only, null, false);

    /// <summary>
    /// <see cref="CurveTable(int, string?)"/> with the cast member <paramref name="carry"/> names
    /// carrying that one item alone (issue 757: Pell with Cinder, the weapon she held in front
    /// before the Sim chose per attack), and, given <paramref name="items"/>, each read followed by
    /// the attacks and counters every player unit struck with, by item, summed over the seeds.
    /// </summary>
    public static int CurveTable(int seeds, string? only, (string Unit, string Item)? carry, bool items)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("curve: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        if (carry is { } held)
        {
            if (Carry(content, held.Unit, held.Item) is not { } carried)
            {
                Console.WriteLine($"curve: --carry needs a cast member and a weapon; '{held.Unit}' or '{held.Item}' is not one");
                return 2;
            }

            content = carried;
            Console.WriteLine($"curve: {held.Unit} carries {held.Item} alone");
        }

        var entries = content.Campaign.Maps.Where(m => only is null || m.MapId == only).ToList();
        if (entries.Count == 0)
        {
            Console.WriteLine($"curve: no campaign map '{only}'; the campaign lists {string.Join(", ", content.Campaign.Maps.Select(m => m.MapId))}");
            return 2;
        }

        Console.WriteLine($"curve: {entries.Count} campaign maps, {seeds} seeds, gate 1 as the file reads and as the campaign fights it");
        var runs = LevelRun.Measure(contentDir, content, seeds);
        Console.WriteLine($"curve: carried is the file's party at the levels the heuristic's own campaign brings to each camp, slot by slot, over the runs of {runs.Count} that won the map (the count after `over`; issue 764); spread is the same total, even");
        foreach (var entry in entries)
        {
            var number = content.Campaign.Maps.ToList().IndexOf(entry) + 1;
            var map = MapFiles.Load(MapFiles.CampaignPath(contentDir, content, entry.MapId), content);
            var fought = entry.Prepare(map);
            var swaps = entry.Swaps.Count == 0 ? "no swaps" : string.Join(", ", entry.Swaps.Select(s => $"{s.TemplateId} at {s.At.X},{s.At.Y}"));
            Console.WriteLine($"map {number} {entry.MapId}: enemy level {map.EnemyLevel} in the file, {fought.EnemyLevel} in the campaign, {swaps}");
            var (file, fileGames) = Gates.Gate1(content, map, entry.MapId, seeds);
            Console.WriteLine("  file:     " + file.Line);
            PrintItems(items, fileGames);
            if (fought != map)
            {
                var (campaign, campaignGames) = Gates.Gate1(content, fought, entry.MapId, seeds);
                Console.WriteLine("  campaign: " + campaign.Line);
                PrintItems(items, campaignGames);
                var raise = fought.EnemyLevel - map.EnemyLevel;
                if (raise > 0)
                {
                    // A proxy for the company a campaign player brings: the file's party raised by as many levels as the curve raised the enemy.
                    var raised = ValueList<Unit>.From(content.Cast.Select(u => u.AtLevel(Math.Min(u.Level + raise, Unit.MaxLevel), content.Class(u.ClassId))));
                    var party = content with { Cast = raised, Units = raised.Aggregate(content.Units, (units, u) => units.ContainsKey(u.Id) ? units.SetItem(u.Id, u) : units) };
                    var (lifted, liftedGames) = Gates.Gate1(party, fought, entry.MapId, seeds);
                    Console.WriteLine($"  party +{raise}: " + lifted.Line);
                    PrintItems(items, liftedGames);
                }
            }

            var camps = runs.SelectMany(r => r.Maps.Where(m => m.Map == number).Select(m => m.Camp)).ToList();
            if (camps.Count == 0)
            {
                Console.WriteLine("  carried: no run won this map");
                continue;
            }

            var slots = BattleState.From(fought, content, content.Cast, 1).UnitsOf(Side.Player).Count();
            var carriedLevels = LevelRun.SlotLevels(camps, slots);
            foreach (var (label, levels) in new[] { ("carried", carriedLevels), ("carried, spread", LevelRun.Spread(carriedLevels)) })
            {
                var (company, applied) = AtSlotLevels(content, fought, levels);
                var (read, readGames) = Gates.Gate1(company, fought, entry.MapId, seeds);
                Console.WriteLine($"  {label} {string.Join("/", applied)} over {camps.Count}: " + read.Line);
                PrintItems(items, readGames);
            }
        }

        return 0;
    }

    /// <summary>
    /// The file's party on <paramref name="map"/> at per-slot levels (issue 764): the units the map
    /// deploys from the cast, the captain first and the rest in placement order, each raised to the
    /// level of its place in <paramref name="levels"/>, highest first; a unit already above its place,
    /// or a place reading 0, keeps its own level, since levels only rise. Returns the content with the
    /// raised cast and the level each deployed unit stands at.
    /// </summary>
    public static (GameContent Content, IReadOnlyList<int> Applied) AtSlotLevels(GameContent content, MapDefinition map, IReadOnlyList<int> levels)
    {
        var deployed = BattleState.From(map, content, content.Cast, 1).UnitsOf(Side.Player)
            .OrderByDescending(u => u.IsCaptain).ThenBy(u => u.PlacementIndex).Select(u => u.Id).ToList();
        var cast = content.Cast;
        var applied = new List<int>();
        for (var k = 0; k < deployed.Count; k++)
        {
            var slot = cast.ToList().FindIndex(u => u.Id == deployed[k]);
            if (slot < 0)
            {
                continue;
            }

            var unit = cast[slot];
            var level = Math.Min(Math.Max(unit.Level, k < levels.Count ? levels[k] : 0), Unit.MaxLevel);
            cast = cast.SetItem(slot, unit.AtLevel(level, content.Class(unit.ClassId)));
            applied.Add(level);
        }

        return (content with { Cast = cast, Units = cast.Aggregate(content.Units, (units, u) => units.ContainsKey(u.Id) ? units.SetItem(u.Id, u) : units) }, applied);
    }

    /// <summary>
    /// The content with cast member <paramref name="unitId"/> carrying one full stack of weapon
    /// <paramref name="itemId"/> and nothing else (issue 757; the wildfire trace's adjustment, issue 746);
    /// null when either id names nothing.
    /// </summary>
    public static GameContent? Carry(GameContent content, string unitId, string itemId)
    {
        var slot = content.Cast.ToList().FindIndex(u => u.Id == unitId);
        if (slot < 0 || !content.Weapons.TryGetValue(itemId, out var weapon))
        {
            return null;
        }

        var unit = content.Cast[slot] with { Inventory = Inventory.Empty.Add(new ItemStack(itemId, weapon.Durability)) };
        return content with
        {
            Cast = content.Cast.SetItem(slot, unit),
            Units = content.Units.ContainsKey(unitId) ? content.Units.SetItem(unitId, unit) : content.Units,
        };
    }

    /// <summary>The <c>items:</c> line under a <c>--curve</c> read: per player unit, by id, the items it attacked and countered with over every game.</summary>
    public static string ItemsLine(IReadOnlyList<GameResult> games)
    {
        var units = games.SelectMany(g => g.Items.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var parts = units.Select(id => $"{id} [{games.Aggregate(ItemMix.Zero, (sum, g) => sum.Plus(g.Items.GetValueOrDefault(id, ItemMix.Zero)))}]").ToList();
        return "  items:    " + (parts.Count == 0 ? "none" : string.Join(", ", parts));
    }

    private static void PrintItems(bool items, IReadOnlyList<GameResult> games)
    {
        if (items)
        {
            Console.WriteLine(ItemsLine(games));
        }
    }

    /// <summary>
    /// The captain's ladder's bar (issue 705, <see cref="LadderRun"/>): per content map, or the one
    /// <paramref name="only"/> names, gate 1 and gate 4 with the captain unpromoted and in each ladder
    /// class, each map against the per-map floor, then each tier's campaign means against the spread (round 232). Exits non-zero when either check fails.
    /// </summary>
    public static int LadderTable(int seeds, string? only)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("ladder: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        var maps = MapFiles.LoadAll(contentDir, content).Where(m => only is null || m.Id == only).ToList();
        if (maps.Count == 0)
        {
            Console.WriteLine($"ladder: no map '{only}' under {contentDir}");
            return 2;
        }

        Console.WriteLine($"ladder: {maps.Count} maps, {seeds} seeds, the captain unpromoted and in {string.Join(", ", LadderRun.Ladder(content).Select(c => c.Id))}");
        var failed = false;
        var readings = new List<LadderRun.MapReading>();
        foreach (var (id, map) in maps)
        {
            var reading = LadderRun.Measure(content, id, map, seeds);
            foreach (var line in LadderRun.Lines(reading))
            {
                Console.WriteLine(line);
            }

            readings.Add(reading);
            failed |= !reading.Passed;
        }

        var floor = !failed;
        var campaign = LadderRun.Campaign(content, readings);
        foreach (var tier in campaign)
        {
            Console.WriteLine(LadderRun.Line(tier));
            failed |= !tier.Passed;
        }

        Console.WriteLine($"ladder: per-map floor {Gates.Verdict(floor)}, campaign spread {Gates.Verdict(campaign.All(t => t.Passed))}: {(failed ? "FAILED" : "ok")}");
        return failed ? 1 : 0;
    }

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
    /// The hand plays the full-campaign script opens maps with (issue 786): Starting Alone and
    /// The Mill as Code played them on seeds 631 and 632 (issues 631, 632), read from
    /// <c>docs/transcripts</c> beside the content directory; none when that folder is absent.
    /// </summary>
    public static IReadOnlyDictionary<string, string> HandPlays(string contentDir)
    {
        var transcripts = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(contentDir).TrimEnd(Path.DirectorySeparatorChar))!, "docs", "transcripts");
        var plays = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (map, file) in new[] { ("starting_alone", "2026-10-01-starting_alone-631.script"), ("the_mill", "2026-10-01-the_mill-632.script") })
        {
            var path = Path.Combine(transcripts, file);
            if (File.Exists(path))
            {
                plays[map] = File.ReadAllText(path);
            }
        }

        return plays;
    }

    /// <summary>
    /// The full-campaign parity script (issue 786): one line per seed from <paramref name="first"/>
    /// to <paramref name="last"/> naming the map it was lost on or the win and the kinds it never
    /// took, and with <paramref name="write"/> the first seed's script written there.
    /// </summary>
    public static int CampaignScriptRun(ulong first, ulong last, string? write, string difficulty, bool permadeath, int variant, string? quest = null, (string Unit, string Class)? until = null)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("campaign-script: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        for (var seed = first; seed <= last; seed++)
        {
            var result = CampaignScript.Write(content, contentDir, seed, HandPlays(contentDir), difficulty, permadeath, variant, quest, until);
            if (until is { } door && result.LostOn is null && result.Text.Contains("# stopped at", StringComparison.Ordinal))
            {
                Console.WriteLine($"seed {seed}: stopped after {result.Maps} maps, certify {door.Unit} {door.Class} accepted");
                if (write is not null && seed == first)
                {
                    File.WriteAllText(write, result.Text);
                }

                continue;
            }

            var missed = CampaignScript.Kinds.Where(k => !result.Touched.Contains(k)).ToList();
            Console.WriteLine($"seed {seed}: {(result.LostOn is { } lost ? $"lost on map {lost}" : "won")}, {result.Maps} maps, missed {(missed.Count == 0 ? "none" : string.Join(", ", missed))}");
            if (until is not null && result.Text.TrimEnd('\n').Split('\n')[^1] is { } never && never.StartsWith("# never reached", StringComparison.Ordinal))
            {
                Console.WriteLine("  " + never[2..]);
            }

            if (write is not null && seed == first)
            {
                File.WriteAllText(write, result.Text);
            }
        }

        return 0;
    }

    /// <summary>
    /// One game of the heuristic player on a map and a seed, printed as the script the CLI
    /// replays (player commands bare, enemy commands as <c>enemy:</c> lines, deaths and
    /// wakes as comments), so a baseline game can be read turn by turn or handed to
    /// <c>ironwake play --script</c>. A grudge strike prints the planner's best alternative
    /// beside it (issue 331). <paramref name="mapId"/> may be a map file's path, so a sample
    /// outside the content directory traces too.
    /// </summary>
    public static int Trace(string mapId, ulong seed, RollScheme scheme = RollScheme.TwoRollAverage) => Trace(mapId, seed, scheme, null);

    /// <summary>
    /// <see cref="Trace(string, ulong, RollScheme)"/> with the loaded content passed through
    /// <paramref name="adjust"/> first, so a test can trace a sample with a roster changed (issue 746:
    /// Pell with Cinder alone, since the heuristic otherwise casts Gust and never lights a fire).
    /// </summary>
    public static int Trace(string mapId, ulong seed, RollScheme scheme, Func<GameContent, GameContent>? adjust)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("trace: no content directory found from the working directory or the build output");
            return 1;
        }

        var loaded = ContentLoader.Load(contentDir);
        var content = adjust is null ? loaded : adjust(loaded);
        var maps = File.Exists(mapId)
            ? new List<(string Id, MapDefinition Map)> { (mapId, MapFiles.Load(mapId, content)) }
            : MapFiles.LoadAll(contentDir, content).Where(m => m.Id == mapId).ToList();
        if (maps.Count == 0 && content.Campaign.Keep.IsKeepMap(mapId))
        {
            maps.Add((mapId, MapFiles.Load(MapFiles.CampaignPath(contentDir, content, mapId), content)));
        }

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
            var grudges = new Dictionary<string, EnemyAi.GrudgeStrike?>();
            foreach (var command in commands)
            {
                var grudge = enemy ? EnemyAi.GrudgeLog(state, content, command, grudges) : null;
                if (enemy && CoverRule.PassedLine(state, content, command) is { } passedLine)
                {
                    Console.WriteLine($"#   {passedLine}");
                }
                var scriptLine = enemy ? Script(command) : Script(state, content, command);
                var result = Resolver.Apply(state, content, command);
                if (!result.Accepted)
                {
                    Console.WriteLine($"# {command} was rejected: {result.Rejection!.Message}");
                    return 1;
                }

                Console.WriteLine((enemy ? "# enemy: " : "") + scriptLine);
                if (grudge is not null)
                {
                    Console.WriteLine("#   " + grudge);
                }

                var fire = new FireLines(state.Map);
                foreach (var e in result.Events)
                {
                    if (fire.Take(e) is { } line)
                    {
                        Console.WriteLine(line);
                    }

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
                            Console.WriteLine($"#   event {m.Name}" + (m.Blocked ? " blocked" + (m.Terrain is { } barring ? " by " + barring : "") : " fired"));
                            break;
                        case PhaseBegan p when p.Side == Side.Player:
                            Console.WriteLine($"# turn {p.Turn}");
                            break;
                        case UnitBurned b:
                            Console.WriteLine($"#   {b.UnitId} burns {b.Amount}, {b.HpAfter} hp");
                            break;
                        case WatchTaken t:
                            Console.WriteLine($"#   {t.UnitId} watches from {t.At}" + (t.PassedUpTargetId is { } up ? $", passes up {up} at {t.PassedUpHit}" : ""));
                            break;
                        case WatchFired f:
                            Console.WriteLine($"#   {f.UnitId}'s watch fires on {f.TargetId}: {(f.Strike.Hit ? (f.Strike.Crit ? "crit " : "hit ") + f.Strike.Damage : "miss")}, {f.Strike.TargetHpAfter} hp");
                            break;
                        case WatchHeld h:
                            Console.WriteLine($"#   {h.UnitId}'s watch holds on {h.TargetId} at {h.Hit}");
                            break;
                        case WatchEnded x:
                            Console.WriteLine($"#   {x.UnitId} stops watching");
                            break;
                        case CoverTaken c:
                            Console.WriteLine($"#   {c.UnitId} covers {c.AllyId}, who lands on {c.AllyLandsOn} if struck" + (c.PassedUpTargetId is { } coverUp ? $", passes up {coverUp} at {c.PassedUpHit}" : ""));
                            break;
                        case CoverFired c:
                            Console.WriteLine($"#   {c.UnitId} covers {c.AllyId} against {c.AttackerId}: onto {c.At}, {c.AllyId} to {c.AllyTo}" + (c.WouldHaveKilled ? ", would have killed" : ""));
                            break;
                        case BlowRaised b:
                            Console.WriteLine($"#   {b.UnitId} raises a blow over {b.At} ({b.TargetId})");
                            break;
                        case BlowLanded b:
                            Console.WriteLine($"#   {b.UnitId}'s blow lands on {b.TargetId} for {b.Damage}, {b.TargetHpAfter} hp");
                            break;
                        case BlowFell b:
                            Console.WriteLine($"#   {b.UnitId}'s blow falls on {b.At}");
                            break;
                        case BlowBroken b:
                            Console.WriteLine($"#   {b.UnitId}'s blow is broken");
                            break;
                    }
                }

                if (fire.Flush() is { } spread)
                {
                    Console.WriteLine(spread);
                }

                state = result.Next;
                if (state.Outcome.IsOver)
                {
                    break;
                }

                if (result.Events.OfType<WatchFired>().Any(f => f.Strike.TargetHpAfter == 0))
                {
                    // A watch shot killed the mover (DESIGN.md 13.17): the rest of its plan is void, so plan again.
                    break;
                }
            }
        }

        Console.WriteLine($"# {state.Outcome.Result} on turn {state.Turn}: {state.Outcome.Reason}");
        return 0;
    }

    /// <summary>
    /// The trace's fire lines on a <c>wildfire: on</c> map (issue 438), read from the
    /// <see cref="TerrainChanged"/> events the rules already emit, so no new event is needed. A
    /// tile set alight by a combat prints <c>x,y ignites</c> where it happens; the front's step
    /// at a player phase start (<see cref="Wildfire.Spread"/>) prints as one line,
    /// <c>fire: out a,b; lit c,d e,f</c>, once the step's run of changes ends. Every other
    /// terrain change, and every change on any other map, prints nothing, so a trace without
    /// the header is unchanged.
    /// </summary>
    private sealed class FireLines(MapDefinition before)
    {
        private readonly List<Coord> _out = new();
        private readonly List<Coord> _lit = new();
        private bool _spreading;

        public string? Take(GameEvent e)
        {
            if (!before.WildfireEnabled)
            {
                return null;
            }

            switch (e)
            {
                case PhaseBegan { Side: Side.Player }:
                    _spreading = true;
                    return null;
                case TerrainChanged t when _spreading && t.TerrainId == Wildfire.BurntTerrainId && before.TerrainIdAt(t.At) == Wildfire.FireTerrainId:
                    _out.Add(t.At);
                    return null;
                case TerrainChanged t when _spreading && t.TerrainId == Wildfire.FireTerrainId:
                    _lit.Add(t.At);
                    return null;
                case TerrainChanged t when !_spreading && t.TerrainId == Wildfire.FireTerrainId:
                    return $"#   {t.At} ignites";
                default:
                    return _out.Count + _lit.Count > 0 ? Flush() : null;
            }
        }

        public string? Flush()
        {
            _spreading = false;
            if (_out.Count == 0 && _lit.Count == 0)
            {
                return null;
            }

            var line = "#   fire: out " + string.Join(" ", _out) + (_lit.Count > 0 ? "; lit " + string.Join(" ", _lit) : "");
            _out.Clear();
            _lit.Clear();
            return line;
        }
    }

    /// <summary>
    /// A player command as the CLI's script reader takes it on <paramref name="state"/>, before the
    /// command is applied: an <c>attack</c> whose counter is lethal to the attacker ends in <c>!</c>,
    /// since the CLI refuses that swing unconfirmed (issue 975), and an <c>end</c> while a unit is lethal
    /// if all land ends in <c>!</c> for the same reason (issue 1093), so a trace still replays under <c>--strict</c>.
    /// </summary>
    public static string Script(BattleState state, GameContent content, Command command) =>
        (command switch
        {
            Attack { Slot: { } slot } a when UniqueItemAt(state, a.UnitId, slot) is { } id => $"attack {a.UnitId} {a.TargetId} {id}",
            UseItem u when UniqueItemAt(state, u.UnitId, u.Slot) is { } id => u.TargetId is { } t ? $"item {u.UnitId} {id} {t}" : $"item {u.UnitId} {id}",
            _ => Script(command),
        }) + (command switch
        {
            Attack attack when SwingsIntoLethalCounter(state, content, attack) => " !",
            EndPhase when state.Phase == Side.Player && !state.Outcome.IsOver && Queries.Lethal(state, content).Count > 0 => " !",
            _ => "",
        });

    /// <summary>
    /// The item id in <paramref name="unitId"/>'s zero-based <paramref name="slot"/> when no other
    /// slot of the pack holds the same id, else null (issue 1114). A trace names a weapon or item
    /// this way because a swing reorders the pack and the CLI refuses a slot number the last
    /// listing showed holding something else; a duplicate keeps its number, since either copy
    /// is the same choice.
    /// </summary>
    public static string? UniqueItemAt(BattleState state, string unitId, int slot) =>
        state.Find(unitId) is { } unit && slot >= 0 && slot < unit.Unit.Inventory.Count
        && unit.Unit.Inventory.Items[slot].ItemId is var id
        && unit.Unit.Inventory.Items.Count(item => item.ItemId == id) == 1
            ? id
            : null;

    /// <summary>
    /// True when <paramref name="attack"/>'s forecast on <paramref name="state"/> carries the console's
    /// <c>Counter: lethal to</c> line (issue 539): a counter that kills the attacker if every strike lands,
    /// never for a raise, which draws no counter, or a certain kill.
    /// </summary>
    public static bool SwingsIntoLethalCounter(BattleState state, GameContent content, Attack attack) =>
        state.Find(attack.UnitId) is { } unit
        && state.Find(attack.TargetId) is { } target
        && !Windup.Raises(state, Resolver.ChooseWeapon(unit, content, attack.Slot).Weapon)
        && Queries.Forecast(state, content, unit, target, attack.Slot) is { } forecast
        && forecast.CounterIsLethal(unit.Hp, target.Hp);

    /// <summary>
    /// A command as the CLI's script reader takes it. Slots print one-based, as the CLI reads
    /// them since issue 101 (issue 118); the core's own slot is one lower.
    /// </summary>
    public static string Script(Command command) => command switch
    {
        Move m => m.Via is { } via ? $"move {m.UnitId} {m.To} via {via}" : $"move {m.UnitId} {m.To}",
        Attack a => a.Slot is { } slot ? $"attack {a.UnitId} {a.TargetId} {slot + 1}" : $"attack {a.UnitId} {a.TargetId}",
        UseItem u => u.TargetId is { } t ? $"item {u.UnitId} {u.Slot + 1} {t}" : $"item {u.UnitId} {u.Slot + 1}",
        Wait w => $"wait {w.UnitId}",
        Watch w => $"watch {w.UnitId}",
        Cover c => $"cover {c.UnitId} {c.AllyId}",
        Exit x => $"exit {x.UnitId}",
        Recover r => $"recover {r.UnitId}",
        Open o => $"open {o.UnitId} {o.At}",
        Drop d => $"drop {d.UnitId}",
        Talk t => $"talk {t.UnitId} {t.TargetId}",
        Order o => $"order {Orders.Word(o.Kind)}",
        FallBack f => $"fallback {f.UnitId} {f.To}",
        Shove s => $"shove {s.UnitId} {s.TargetId}",
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
    /// files changing; without one, maps are played as authored. A <paramref name="mapId"/> that
    /// names no content map but is a map file on disk, such as a <c>docs/samples/</c> sample, is
    /// loaded from that file (issue 429), as <c>--trace</c> already does. Given an
    /// <paramref name="origin"/> (issue 681), the captain is played on that origin's card, so the
    /// four origins can be held within 5 points of each other on gate 1.
    /// </summary>
    public static int Full(string mapId, int seeds, RollScheme scheme = RollScheme.TwoRollAverage, double taxFloor = FreePrefix.DefaultTaxFloor, string? difficulty = null, IReadOnlyList<string>? lead = null, string? origin = null)
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("full: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        if (origin is not null)
        {
            if (content.Campaign.Origin(origin) is not { } chosen)
            {
                Console.WriteLine($"full: --origin names '{origin}', which the campaign does not offer; it offers {string.Join(", ", content.Campaign.Origins.Select(o => o.Id))}");
                return 2;
            }

            var captain = chosen.Apply(content.Cast[0]);
            content = content with { Cast = content.Cast.SetItem(0, captain), Units = content.Units.ContainsKey(captain.Id) ? content.Units.SetItem(captain.Id, captain) : content.Units };
            Console.WriteLine($"origin: {chosen.Name}, the captain's card {captain.Stats}");
        }

        if (lead is { Count: > 0 })
        {
            if (lead.FirstOrDefault(id => content.Cast.All(u => u.Id != id)) is { } unknown)
            {
                Console.WriteLine($"full: --lead names '{unknown}', who is not in the cast");
                return 2;
            }

            // A side map (issue 635) is measured with its member in the captain slot and its ally first in the bare slots.
            content = content with { Cast = ValueList<Unit>.From(lead.Select(id => content.Cast.First(u => u.Id == id)).Concat(content.Cast.Where(u => !lead.Contains(u.Id)))) };
        }

        var all = MapFiles.LoadAll(contentDir, content);
        var maps = mapId == "--all" ? all : all.Where(m => m.Id == mapId).ToList();
        if (maps.Count == 0 && File.Exists(Path.Combine(contentDir, MapFiles.QuestsDirectory, mapId + MapFiles.Extension)))
        {
            maps = new[] { (mapId, MapFiles.Load(Path.Combine(contentDir, MapFiles.QuestsDirectory, mapId + MapFiles.Extension), content)) };
        }

        if (maps.Count == 0 && content.Campaign.Keep.IsKeepMap(mapId))
        {
            maps = new[] { (mapId, MapFiles.Load(MapFiles.CampaignPath(contentDir, content, mapId), content)) };
        }

        if (maps.Count == 0 && mapId != "--all" && File.Exists(mapId))
        {
            maps = new[] { (mapId, MapFiles.Load(mapId, content)) };
        }

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
        failed |= !Print(CeilingGate(content));
        var ladderMap = maps.FirstOrDefault(m => m.Id == LadderSmokeMap);
        failed |= !Print(ladderMap.Map is null
            ? new GateResult($"origin by class: no map '{LadderSmokeMap}': FAILED", false)
            : LadderRun.OriginByClass(content, ladderMap.Id, ladderMap.Map));
        Console.WriteLine(failed ? "smoke: FAILED" : "smoke: ok");
        return failed ? 1 : 0;
    }

    /// <summary>
    /// <c>--ceiling</c>: every signature item against the shop (issue 635, <see cref="SignatureCeiling"/>),
    /// one line per item and its arts, then the verdict the smoke prints.
    /// </summary>
    private static int Ceiling()
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("ceiling: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        foreach (var line in CeilingLines(content))
        {
            Console.WriteLine(line);
        }

        return Print(CeilingGate(content)) ? 0 : 1;
    }

    /// <summary>One line per signature item, then one per signature art on it, on the default roll scheme.</summary>
    public static IEnumerable<string> CeilingLines(GameContent content)
    {
        foreach (var reading in SignatureCeiling.ReadAll(content, RollScheme.TwoRollAverage))
        {
            var against = reading.Comparator is { } shop ? $"{reading.Ratio:F2} of {shop.Id}" : "no shop weapon";
            yield return $"  {reading.Item.Id} ({reading.Item.BoundTo}, {reading.Item.Type.ToString().ToLowerInvariant()} {reading.Item.Rank}): {against}, ceiling {SignatureCeiling.MaxRatio:F2}";
            foreach (var art in reading.Arts)
            {
                yield return art.ByConstruction
                    ? $"    {art.ArtId}: a heal art, costs the ally its phase; judged in play (0099)"
                    : $"    {art.ArtId}: loses to the plain attack on {art.LosesTo} of {art.Targets} targets";
            }
        }
    }

    /// <summary>The signature ceiling as a smoke row: passes with no item, fails naming the first item over its ceiling or with an art that never loses.</summary>
    public static GateResult CeilingGate(GameContent content)
    {
        var readings = SignatureCeiling.ReadAll(content, RollScheme.TwoRollAverage);
        if (readings.Count == 0)
        {
            return new GateResult("signature ceiling: no signature item ships: ok", true);
        }

        var failed = readings.FirstOrDefault(r => !r.Passed);
        return failed is null
            ? new GateResult($"signature ceiling: {readings.Count} items, highest {readings.Max(r => r.Ratio):F2} of {SignatureCeiling.MaxRatio:F2}: ok", true)
            : new GateResult($"signature ceiling: {failed.Failure}: FAILED", false);
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
    internal static (string State, int Turns) FullGame(GameContent content, MapDefinition map, ulong seed)
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
                forecast = Queries.Forecast(state, content, state.Find(attack.UnitId)!, state.Find(attack.TargetId)!, attack.Slot, attack.Art);
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

            if (forecast is not null && result.Events.OfType<CombatFought>().SingleOrDefault() is { } fought)
            {
                tally!.Count(forecast, fought);
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
