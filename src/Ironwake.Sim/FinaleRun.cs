using System.Diagnostics;
using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// The keep finale's measurement (issue 692, slice 5; DECISIONS/0151): a <c>deploy: all</c> board
/// played by the heuristic with three companies, each member a cast member raised to the expected
/// finale level by average growth in its own class or a barracks hire joining
/// <see cref="Barracks.LevelsBelow"/> under it. Full is the whole cast plus the first hires up to
/// <see cref="CampaignRecord.CompanyCap"/>; depleted is the captain, <see cref="DepletedStory"/>
/// story members and hires up to the cap; floor is the captain, <see cref="FloorStory"/> story
/// members and every hire. Full and depleted are held to gate 1; every company is held to the time
/// and length lines; the floor reports its wins as data, since the heuristic is not best play and a
/// cold chair's play of the floor decides it (round 215).
/// </summary>
public static class FinaleRun
{
    /// <summary>The level the company is expected to stand at on the keep (provisional; FinaleStrengthTests holds the bosses to it).</summary>
    public const int DefaultLevel = 8;

    /// <summary>Story members beside the captain in the depleted company (issue 692; <see cref="FinaleCompanies.DepletedStory"/>).</summary>
    public const int DepletedStory = FinaleCompanies.DepletedStory;

    /// <summary>Story members beside the captain in the floor company (issue 692; <see cref="FinaleCompanies.FloorStory"/>).</summary>
    public const int FloorStory = FinaleCompanies.FloorStory;

    /// <summary>The median turn count over which the finale is too long; the lever is one fewer wave (issue 692).</summary>
    public const int LengthLimit = 16;

    /// <summary>AI-vs-AI games timed per company, as gate 7.</summary>
    public const int SpeedSeeds = 5;

    /// <summary>The slowest AI-vs-AI game allowed, in milliseconds, as gate 7.</summary>
    public const int SpeedLimitMs = 1000;

    /// <summary>A company's name as printed.</summary>
    public static string Name(FinaleCompany company) => FinaleCompanies.Name(company);

    /// <summary>The roster of <paramref name="company"/> in deploy order (<see cref="FinaleCompanies.Roster"/>; issue 1217 moved it to Core so <c>play --company</c> fields the same units).</summary>
    public static ValueList<Unit> Roster(GameContent content, FinaleCompany company, int level) => FinaleCompanies.Roster(content, company, level);

    /// <summary>
    /// Why <paramref name="map"/> cannot be measured as the finale, or null: it must field the whole
    /// company (<c>deploy: all</c>), which its loader holds to at least <see cref="CampaignRecord.CompanyCap"/> placements.
    /// </summary>
    public static string? Refusal(MapDefinition map, string id) =>
        map.DeploysAll ? null : $"finale: {id} is not 'deploy: all'; the finale fields the whole company";

    /// <summary>One company's reading: the heuristic's games and the slowest AI-vs-AI game.</summary>
    public sealed record Reading(FinaleCompany Company, int Size, IReadOnlyList<GameResult> Games, long SlowestMs, int TurnLimit, bool Bonded = false)
    {
        /// <summary>The names of the map's events a front's fall fires, in file order (issue 1204); empty on a map without fronts.</summary>
        public IReadOnlyList<string> Falls { get; init; } = Array.Empty<string>();

        public int Wins => Games.Count(g => g.Won);

        /// <summary>The median turns played over every game, won or lost, a timeout counted at the limit: the finale's length line.</summary>
        public int MedianTurns => Median(Games.Select(g => Math.Min(g.Turns, TurnLimit)));

        /// <summary>Gate 1 on full and depleted; the floor is data.</summary>
        public bool Beatable => Company == FinaleCompany.Floor || (double)Wins / Games.Count >= Gates.BeatableRate;

        public bool Fast => SlowestMs < SpeedLimitMs;

        public bool Short => MedianTurns <= LengthLimit;

        public bool Passed => Beatable && Fast && Short;

        public IEnumerable<string> Lines(int level)
        {
            var rate = (double)Wins / Games.Count;
            var verdict = Company == FinaleCompany.Floor ? $"data, a cold chair's play decides ({(Wins > 0 ? "won at least once" : "never won")})" : Gates.Verdict(Beatable);
            yield return $"finale {Name(Company)}: {Size} units at level {level}, heuristic wins {Wins}/{Games.Count} ({rate:P0}), losses {Gates.LossCounts(Games)}: {verdict}";
            yield return $"  length: median {MedianTurns} turns over every game, limit {TurnLimit}: {(Short ? "ok" : $"over {LengthLimit}, the lever is one fewer wave")}";
            yield return $"  time: {SpeedSeeds} AI-vs-AI games, slowest {SlowestMs} ms: {Gates.Verdict(Fast)}";
            if (Bonded)
            {
                yield return "  " + BondLine(Games);
            }

            if (Falls.Count > 0)
            {
                yield return "  " + FallsLine(Falls, Games);
            }

            if (PaceLine(Games) is { } pace)
            {
                yield return "  " + pace;
            }

            if (PaceStallLine(Games) is { } paceStall)
            {
                yield return "  " + paceStall;
            }

            if (StageLine(Games) is { } stage)
            {
                yield return "  " + stage;
            }

            if (StallLine(Games) is { } stall)
            {
                yield return "  " + stall;
            }

            if (CaptainLine(Games) is { } captain)
            {
                yield return "  " + captain;
            }

            if (PlanLine(Games) is { } plan)
            {
                yield return "  " + plan;
            }

            if (FrontLine(Games) is { } front)
            {
                yield return "  " + front;
            }
        }
    }

    /// <summary>
    /// <paramref name="content"/> with <paramref name="company"/> at <paramref name="level"/> as its
    /// cast, so any gate that fields the cast fields the company (issue 1149).
    /// </summary>
    public static GameContent Fielded(GameContent content, FinaleCompany company, int level) =>
        content with { Cast = Roster(content, company, level) };

    /// <summary>Plays <paramref name="company"/> on <paramref name="map"/>: <paramref name="seeds"/> heuristic games and <see cref="SpeedSeeds"/> timed AI-vs-AI games.</summary>
    public static Reading Measure(GameContent content, MapDefinition map, FinaleCompany company, int level, int seeds, RollScheme scheme)
    {
        var fielded = Fielded(content, company, level);
        var roster = fielded.Cast;
        var games = new List<GameResult>();
        for (var seed = 1; seed <= seeds; seed++)
        {
            games.Add(Runner.Play(fielded, map, (ulong)seed, new HeuristicPlayer(), scheme: scheme));
        }

        long slowest = 0;
        for (var seed = 1; seed <= SpeedSeeds; seed++)
        {
            var watch = Stopwatch.StartNew();
            Program.FullGame(fielded, map, (ulong)seed);
            watch.Stop();
            slowest = Math.Max(slowest, watch.ElapsedMilliseconds);
        }

        var size = BattleState.From(map, fielded, roster, 1, scheme).UnitsOf(Side.Player).Count();
        return new Reading(company, size, games, slowest, map.TurnLimit, map.Bond is not null)
        {
            Falls = map.Events.Where(e => e.Trigger is FallsTrigger).Select(e => e.Name).ToList(),
        };
    }

    /// <summary>
    /// How the bound enemy of a <c>freed:</c> header left each game (issue 750): killed, freed, or
    /// standing at the end. Data only; the Sim's player does not price an ending.
    /// </summary>
    public static string BondLine(IReadOnlyList<GameResult> games) =>
        $"bond: hunter killed {games.Count(g => g.Bond == BondFate.Fell)}, freed {games.Count(g => g.Bond == BondFate.Freed)}, standing {games.Count(g => g.Bond is null)} of {games.Count} (data; the Sim does not price an ending)";

    /// <summary>
    /// How each of a fallen front's events went over the games (issue 1204): arrived, blocked by a
    /// unit or terrain on its tile, or never fired because the front stood. Data only; a fall whose
    /// spawn is always blocked is decoration (Design Table round 407).
    /// </summary>
    public static string FallsLine(IReadOnlyList<string> falls, IReadOnlyList<GameResult> games) =>
        "falls: " + string.Join(", ", falls.Select(name =>
        {
            var fired = games.Select(g => g.Fired.FirstOrDefault(f => f.Name == name)).ToList();
            return $"{name} arrived {fired.Count(f => f is { Blocked: false })}, blocked {fired.Count(f => f is { Blocked: true })}, unfired {fired.Count(f => f is null)}";
        })) + $" of {games.Count} (data; all blocked is decoration)";

    /// <summary>
    /// Round 513's pace read of the boss's first stage (<see cref="StageOne"/>): the median turn he stood on the board, how
    /// many games reached the swallow and its median turn (earliest, latest), the median turn of his first blow taken, and
    /// the company's unit-phases from his arrival to the swallow or the end, split into actions that struck him, actions on
    /// the others, and the rest; null when no game fielded such a boss.
    /// </summary>
    public static string? PaceLine(IReadOnlyList<GameResult> games)
    {
        var ones = games.Where(g => g.StageOne is not null).Select(g => g.StageOne!).ToList();
        if (ones.Count == 0)
        {
            return null;
        }

        var swallowed = ones.Where(o => o.SwallowTurn is not null).Select(o => o.SwallowTurn!.Value).ToList();
        var when = swallowed.Count == 0 ? "none" : $"median turn {Median(swallowed)} (earliest {swallowed.Min()}, latest {swallowed.Max()})";
        var struck = ones.Where(o => o.FirstBlowTurn is not null).Select(o => o.FirstBlowTurn!.Value).ToList();
        var first = struck.Count == 0 ? "never struck" : $"his first blow taken median turn {Median(struck)} in {struck.Count}";
        return $"stage 1: on the board median turn {Median(ones.Select(o => o.Arrived))} in {ones.Count} of {games.Count}; swallowed in {swallowed.Count}, {when}; {first}; {Split(ones)}";
    }

    /// <summary>
    /// Round 513's read of the games that ran out the clock in the boss's first stage: his median HP of his bar and the units
    /// standing at the limit, the median actions a player phase on him and on the others, in tenths, and the same unit-phase
    /// split as <see cref="PaceLine"/>; null when none did. Few on him is the fronts eating the phases; many on him with
    /// his bar high is a company that cannot hurt him enough.
    /// </summary>
    public static string? PaceStallLine(IReadOnlyList<GameResult> games)
    {
        var stalled = games.Where(g => g.Cause == LossCause.Timeout && g.Stage is null && g.StageOne is not null).Select(g => g.StageOne!).ToList();
        if (stalled.Count == 0)
        {
            return null;
        }

        static string Tenths(int t) => $"{t / 10}.{t % 10}";
        var onHim = Median(stalled.Select(o => o.PlayerPhases == 0 ? 0 : o.OnHim * 10 / o.PlayerPhases));
        var onOthers = Median(stalled.Select(o => o.PlayerPhases == 0 ? 0 : o.OnOthers * 10 / o.PlayerPhases));
        return $"stage 1 timeouts: {stalled.Count}; at the limit, median boss HP {Median(stalled.Select(o => o.BossHp))} of {Median(stalled.Select(o => o.BossMaxHp))}, {Median(stalled.Select(o => o.StandingEnd))} standing; median actions a player phase on him {Tenths(onHim)}, on the others {Tenths(onOthers)}, over {Median(stalled.Select(o => o.PlayerPhases))} player phases; {Split(stalled)}";
    }

    private static string Split(IReadOnlyList<StageOne> ones)
    {
        var total = ones.Sum(o => o.UnitPhases);
        var him = ones.Sum(o => o.OnHim);
        var others = ones.Sum(o => o.OnOthers);
        var rest = Math.Max(0, total - him - others);
        string Share(int n) => total == 0 ? "0 %" : $"{n * 100 / total} %";
        return $"unit-phases {total}: on him {him} ({Share(him)}), on the others {others} ({Share(others)}), the rest {rest} ({Share(rest)})";
    }

    /// <summary>
    /// The boss's second stage over the games (issue 1385's Sim gate): how many reached it, its median length in phases
    /// begun, the games by player units Frozen Iron killed, and the median company the swallow found (units standing, the
    /// captain's HP; issue 1395); null when no game reached it. Data; the gate is that the clock kills at most one unit
    /// of a party that commits.
    /// </summary>
    public static string? StageLine(IReadOnlyList<GameResult> games)
    {
        var reached = games.Where(g => g.Stage is not null).Select(g => g.Stage!).ToList();
        if (reached.Count == 0)
        {
            return null;
        }

        var won = games.Count(g => g.Stage is not null && g.Won);
        var inReach = reached.Sum(s => s.DamageInReach);
        var beyond = reached.Sum(s => s.DamageBeyond);
        var share = inReach + beyond == 0 ? "none dealt" : $"{beyond * 100 / (inReach + beyond)} % from tiles he cannot reach ({beyond} of {inReach + beyond})";
        return $"stage 2: reached {reached.Count} of {games.Count}, won {won}; median {Median(reached.Select(s => s.Phases))} phases; clock deaths 0 in {reached.Count(s => s.ClockDeaths == 0)}, 1 in {reached.Count(s => s.ClockDeaths == 1)}, 2+ in {reached.Count(s => s.ClockDeaths >= 2)} (gate: at most 1); at the swallow, median {Median(reached.Select(s => s.Standing))} standing, captain at {Median(reached.Select(s => s.CaptainHp))} HP; damage on him, {share}";
    }

    /// <summary>
    /// The stall read (round 502): of the games that reached the second stage and ran out the clock, the median boss HP
    /// and units standing as the limit passed, and the median combats opened on him a player phase, in tenths; null when
    /// none did. Few blows with the company standing is the planner's nerve; many blows the heal outpaces is a shortfall.
    /// </summary>
    public static string? StallLine(IReadOnlyList<GameResult> games)
    {
        var stalled = games.Where(g => g.Stage is not null && g.Cause == LossCause.Timeout).Select(g => g.Stage!).ToList();
        if (stalled.Count == 0)
        {
            return null;
        }

        var rate = Median(stalled.Select(s => s.PlayerPhases == 0 ? 0 : s.Blows * 10 / s.PlayerPhases));
        return $"stage 2 timeouts: {stalled.Count}; at the limit, median boss HP {Median(stalled.Select(s => s.BossHp))}, {Median(stalled.Select(s => s.StandingEnd))} standing; median blows on him a player phase {rate / 10}.{rate % 10} over {Median(stalled.Select(s => s.PlayerPhases))} player phases";
    }

    /// <summary>
    /// The captain losses by stage and by what struck the last blow (round 502's read), most first; null when the captain
    /// never fell. Data: it says whether the finale is lost to the waves, to stage 1, or to the swallowed boss and his clock.
    /// </summary>
    public static string? CaptainLine(IReadOnlyList<GameResult> games)
    {
        var fell = games.Where(g => g.CaptainKiller is not null).ToList();
        if (fell.Count == 0)
        {
            return null;
        }

        string By(IEnumerable<GameResult> some) => string.Join(", ", some.GroupBy(g => g.CaptainKiller!).OrderByDescending(k => k.Count()).ThenBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {k.Count()}"));
        var before = fell.Where(g => !g.CaptainFellInStageTwo).ToList();
        var after = fell.Where(g => g.CaptainFellInStageTwo).ToList();
        return $"captain falls: {before.Count} before the swallow ({(before.Count == 0 ? "none" : By(before))}), {after.Count} after ({(after.Count == 0 ? "none" : By(after))})";
    }

    /// <summary>
    /// Round 505's split of the captain's falls after the swallow by his last player phase (<see cref="CaptainPlan"/>), each
    /// class by what struck the last blow; null when he never fell after one. Cornered is the board's; a lethal tile taken
    /// while one passed, or a tile read safe that was not, is the planner's.
    /// </summary>
    public static string? PlanLine(IReadOnlyList<GameResult> games)
    {
        var after = games.Where(g => g.CaptainFellInStageTwo && g.CaptainPlanRead is not null).ToList();
        if (after.Count == 0)
        {
            return null;
        }

        string By(IEnumerable<GameResult> some) => string.Join(", ", some.GroupBy(g => g.CaptainKiller!).OrderByDescending(k => k.Count()).ThenBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {k.Count()}"));
        var order = new[] { "cornered", "lethal tile", "read safe", "read safe, crit-lethal", "own phase", "unread" };
        return "after the swallow, by his last plan: " + string.Join("; ", order
            .Select(name => (Name: name, Games: after.Where(g => g.CaptainPlanRead == name).ToList()))
            .Where(c => c.Games.Count > 0)
            .Select(c => $"{c.Name} {c.Games.Count} ({By(c.Games)})"));
    }

    /// <summary>
    /// Issue 1423's read of the captain's falls before the swallow, by where the hunt stood as he fell
    /// (<see cref="GameResult.CaptainFront"/>), each class by what struck the last blow, and how many of those blows came
    /// down a line strike; null when he never fell before one. Hunted and alone is the hunter's weakest-front pick finding
    /// a lone defender (round 508); a line is stage 1's reach catching him (round 505).
    /// </summary>
    public static string? FrontLine(IReadOnlyList<GameResult> games)
    {
        var before = games.Where(g => g.CaptainKiller is not null && !g.CaptainFellInStageTwo && g.CaptainFront is not null).ToList();
        if (before.Count == 0)
        {
            return null;
        }

        string By(IEnumerable<GameResult> some) => string.Join(", ", some.GroupBy(g => g.CaptainKiller!).OrderByDescending(k => k.Count()).ThenBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {k.Count()}"));
        var order = new[] { "hunted, alone", "hunted, paired", "hunted, behind the fronts", "not hunted, alone", "not hunted, paired", "not hunted, behind the fronts", "his own phase" };
        return "before the swallow, by the hunt: " + string.Join("; ", order
            .Select(name => (Name: name, Games: before.Where(g => g.CaptainFront == name).ToList()))
            .Where(c => c.Games.Count > 0)
            .Select(c => $"{c.Name} {c.Games.Count} ({By(c.Games)})")) + $"; down a line {before.Count(g => g.CaptainByLine)}";
    }

    private static int Median(IEnumerable<int> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted.Count == 0 ? 0 : sorted[(sorted.Count - 1) / 2];
    }
}
