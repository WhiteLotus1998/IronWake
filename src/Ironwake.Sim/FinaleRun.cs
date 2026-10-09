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
    public const int DefaultLevel = FinaleCompanies.DefaultLevel;

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

            if (IdleLine(Games) is { } idle)
            {
                yield return "  " + idle;
            }

            if (HealerFallLine(Games) is { } healerFalls)
            {
                yield return "  " + healerFalls;
            }

            if (BaitLine(Games) is { } bait)
            {
                yield return "  " + bait;
            }

            if (StageLine(Games) is { } stage)
            {
                yield return "  " + stage;
            }

            if (ShapeLine(Games) is { } shape)
            {
                yield return "  " + shape;
            }

            if (LeaderLine(Games) is { } leader)
            {
                yield return "  " + leader;
            }

            if (ShardLine(Games) is { } shard)
            {
                yield return "  " + shard;
            }

            if (RaceLine(Games) is { } race)
            {
                yield return "  " + race;
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

            if (VetoSplitLine(Games) is { } split)
            {
                yield return "  " + split;
            }

            if (FirstFallLine(Games) is { } first)
            {
                yield return "  " + first;
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
    /// the others, and the rest, the rest split as <see cref="StageOne"/> splits it (round 518); null when no game fielded
    /// such a boss.
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

    /// <summary>
    /// Round 523's split of the boss's first stage's idle unit-phases (<see cref="StageOne"/>): fell before acting, no enemy
    /// seen, an enemy seen out of reach, and how many of those had a nearer tile the unit could end on; the unread ones
    /// (no command of their own in the phase) beside them; then the out-of-reach ones by unit, most first, each with its
    /// kind, its median distance to the nearest enemy it saw, and how many could have closed. Null when no game fielded
    /// such a boss or none stood idle.
    /// </summary>
    public static string? IdleLine(IReadOnlyList<GameResult> games)
    {
        var ones = games.Where(g => g.StageOne is not null).Select(g => g.StageOne!).ToList();
        var idle = ones.Sum(o => o.Idle);
        if (idle == 0)
        {
            return null;
        }

        var held = ones.SelectMany(o => o.HeldBy)
            .GroupBy(p => p.Key, StringComparer.Ordinal)
            .Select(g => (Id: g.Key, Kind: g.First().Value.Kind, Distances: g.SelectMany(p => p.Value.Distances).ToList(), CouldClose: g.Sum(p => p.Value.CouldClose)))
            .OrderByDescending(u => u.Distances.Count)
            .ThenBy(u => u.Id, StringComparer.Ordinal)
            .Select(u => $"{u.Id} {u.Distances.Count} ({u.Kind}, median {Median(u.Distances)} tiles, could close {u.CouldClose})");
        var could = ones.Sum(o => o.HeldBy.Values.Sum(h => h.CouldClose));
        var byUnit = string.Join(", ", held);
        var healerWaits = ones.Sum(o => o.HeldBy.Values.Where(h => h.Kind == "healer").Sum(h => h.Distances.Count));
        return $"stage 1 idle {idle}: fell before acting {ones.Sum(o => o.IdleFell)}, no enemy seen {ones.Sum(o => o.IdleUnseen)}, an enemy seen out of reach {ones.Sum(o => o.IdleHeld)} (could close {could}; an unarmed healer's {healerWaits}, the walk finding no safe tile that closes); unread {ones.Sum(o => o.IdleUnread)}; unarmed healers fallen in stage 1 {ones.Sum(o => o.HealerFalls)}; out of reach by unit: {(byUnit.Length == 0 ? "none" : byUnit)}";
    }

    private static string Split(IReadOnlyList<StageOne> ones)
    {
        var total = ones.Sum(o => o.UnitPhases);
        var him = ones.Sum(o => o.OnHim);
        var others = ones.Sum(o => o.OnOthers);
        var rest = Math.Max(0, total - him - others);
        string Share(int n) => total == 0 ? "0 %" : $"{n * 100 / total} %";
        var healed = ones.Sum(o => o.Healed);
        var refused = ones.Sum(o => o.Refused);
        var moved = ones.Sum(o => o.MovedOnly);
        var idle = ones.Sum(o => o.Idle);
        return $"unit-phases {total}: on him {him} ({Share(him)}), on the others {others} ({Share(others)}), the rest {rest} ({Share(rest)}: an item {healed} ({Share(healed)}), refused {refused} ({Share(refused)}), move only {moved} ({Share(moved)}), idle {idle} ({Share(idle)}))";
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
        return $"stage 2: reached {reached.Count} of {games.Count}, won {won}; median {Median(reached.Select(s => s.Phases))} phases; clock deaths 0 in {reached.Count(s => s.ClockDeaths == 0)}, 1 in {reached.Count(s => s.ClockDeaths == 1)}, 2+ in {reached.Count(s => s.ClockDeaths >= 2)} (data; the gate reads won games); at the swallow, median {Median(reached.Select(s => s.Standing))} standing, captain at {Median(reached.Select(s => s.CaptainHp))} HP, company at {Median(reached.Select(s => s.CompanyMaxHp == 0 ? 0 : s.CompanyHp * 100 / s.CompanyMaxHp))} % HP; damage on him, {share}";
    }

    /// <summary>
    /// Issue 1441's bait read (Table round 530): how often the boss struck out of stage 1 in his own phases and from how far
    /// off the tile he began on, over every game and over the games lost before the swallow. Null when no boss had a stage.
    /// </summary>
    public static string? BaitLine(IReadOnlyList<GameResult> games)
    {
        var reads = games.Where(g => g.StageOne is not null).ToList();
        if (reads.Count == 0)
        {
            return null;
        }

        static string Cells(IEnumerable<GameResult> of)
        {
            var list = of.ToList();
            var strikes = list.SelectMany(g => g.StageOne!.BossStrikes).ToList();
            var struck = list.Count(g => g.StageOne!.BossStrikes.Count > 0);
            var off = strikes.GroupBy(p => p.Off).OrderBy(p => p.Key).Select(p => $"{p.Key}: {p.Count()}");
            var kinds = strikes.GroupBy(p => p.Kind, StringComparer.Ordinal).OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key} {p.Count()}");
            return $"struck in {struck} of {list.Count}, strikes {strikes.Count} ({(strikes.Count == 0 ? "none" : string.Join(", ", kinds))}), tiles off his start {(strikes.Count == 0 ? "-" : string.Join(", ", off))}";
        }

        var lostBefore = reads.Where(g => !g.Won && g.StageOne!.SwallowTurn is null);
        return $"stage 1 bait: every game {Cells(reads)}; lost before the swallow {Cells(lostBefore)}";
    }

    /// <summary>
    /// Issue 1441's read 2: the unarmed healers' stage-1 falls over the games, by what she did in the last player phase that
    /// closed (heal, walk, wait, or she fell in her own phase) and by whether what struck her stood on the board as that
    /// phase closed or arrived after it (<see cref="StageOne.HealerFallsBy"/>). Null when none fell.
    /// </summary>
    public static string? HealerFallLine(IReadOnlyList<GameResult> games)
    {
        var by = games.Where(g => g.StageOne is not null).SelectMany(g => g.StageOne!.HealerFallsBy)
            .GroupBy(p => p.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Sum(p => p.Value), StringComparer.Ordinal);
        var total = by.Values.Sum();
        if (total == 0)
        {
            return null;
        }

        int Where(string where) => by.Where(p => p.Key.StartsWith(where + ",", StringComparison.Ordinal)).Sum(p => p.Value);
        int Who(string who) => by.Where(p => p.Key.EndsWith(", " + who, StringComparison.Ordinal)).Sum(p => p.Value);
        var cells = string.Join(", ", by.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key} {p.Value}"));
        return $"unarmed healer falls in stage 1 {total}, by her last closed phase: after a heal {Where("heal")}, after a walk {Where("walk")}, after a wait {Where("wait")}, in her own phase {Where("her phase")}; struck by a unit on the board as it closed {Who("on the board")}, by one that arrived after {Who("arrived after")}, unread {Who("unread")} ({cells})";
    }

    /// <summary>
    /// Issue 1441's read 1: the company's shape as the swallow found it and who dealt the stage-2 damage. Over the games
    /// that reached the stage: the median of each game's median and farthest distance to him, the median count of units
    /// on tiles he reached; then by unit, its median distance at the swallow and its stage-2 damage on him from tiles he
    /// reached and from tiles he did not (<see cref="StageTwo.DamageBy"/>), most damage first. Null when no game reached it.
    /// </summary>
    public static string? ShapeLine(IReadOnlyList<GameResult> games)
    {
        var reached = games.Where(g => g.Stage is { AtSwallow.Count: > 0 }).Select(g => g.Stage!).ToList();
        if (reached.Count == 0)
        {
            return null;
        }

        var units = reached.SelectMany(s => s.AtSwallow.Keys).Concat(reached.SelectMany(s => s.DamageBy.Keys)).Distinct(StringComparer.Ordinal)
            .Select(id => (Id: id,
                Distance: reached.Where(s => s.AtSwallow.ContainsKey(id)).Select(s => s.AtSwallow[id].Distance).ToList(),
                InReach: reached.Sum(s => s.DamageBy.TryGetValue(id, out var d) ? d.InReach : 0),
                Beyond: reached.Sum(s => s.DamageBy.TryGetValue(id, out var d) ? d.Beyond : 0)))
            .OrderByDescending(u => u.InReach + u.Beyond).ThenBy(u => u.Id, StringComparer.Ordinal)
            .Select(u => $"{u.Id} {(u.Distance.Count == 0 ? "-" : Median(u.Distance).ToString(System.Globalization.CultureInfo.InvariantCulture))} tiles in {u.Distance.Count}, dealt {u.InReach} in reach {u.Beyond} beyond");
        return $"stage 2 shape: at the swallow, median distance to him {Median(reached.Select(s => Median(s.AtSwallow.Values.Select(p => p.Distance))))}, farthest {Median(reached.Select(s => s.AtSwallow.Values.Max(p => p.Distance)))}, on tiles he reaches {Median(reached.Select(s => s.AtSwallow.Values.Count(p => p.InReach)))}; by unit: {string.Join(", ", units)}";
    }

    /// <summary>
    /// Issue 1441's captain read (Table round 533), over the games that reached the second stage: his stage-2 player
    /// phases (a swallow in his own phase before he acted opens one), those he began resting, those with a plain strike on
    /// the boss on offer and those it kills if every strike lands; those with an art on offer, those it kills on its hit,
    /// from a tile his veto allows, the median best hit chance of that kill, and the games lost in the stage that had one; what he struck; where each phase left him
    /// against the boss's reach, by what he did; and the marks on the boss (<see cref="StageTwo.Captain"/>). Null when no
    /// game reached the stage.
    /// </summary>
    public static string? LeaderLine(IReadOnlyList<GameResult> games)
    {
        var reads = games.Where(g => g.Stage is not null).Select(g => g.Stage!.Captain).ToList();
        if (reads.Count == 0)
        {
            return null;
        }

        int Sum(Func<StageTwo.CaptainRead, int> of) => reads.Sum(of);
        var lost = games.Where(g => g.Stage is not null && !g.Won).ToList();
        var lostWithKill = lost.Count(g => g.Stage!.Captain.ArtLethal > 0);
        var hits = reads.SelectMany(r => r.ArtLethalHit).ToList();
        var inReach = reads.SelectMany(r => r.EndedInReach).GroupBy(p => p.Key, StringComparer.Ordinal)
            .Select(g => (g.Key, Count: g.Sum(p => p.Value))).OrderByDescending(p => p.Count).ThenBy(p => p.Key, StringComparer.Ordinal).ToList();
        var split = inReach.Count == 0 ? "none" : string.Join(", ", inReach.Select(p => $"after {p.Key} {p.Count}"));
        return $"stage 2 captain: player phases {Sum(r => r.Phases)} (resting {Sum(r => r.Resting)}); a strike on him on offer in {Sum(r => r.Offer)}, lethal if all land in {Sum(r => r.PlainLethal)}; an art on offer in {Sum(r => r.ArtOffer)}, lethal on its hit in {Sum(r => r.ArtLethal)} (from a tile the veto allows {Sum(r => r.ArtLethalSafe)}, median best hit {(hits.Count == 0 ? "-" : Median(hits).ToString(System.Globalization.CultureInfo.InvariantCulture) + " %")}; in {lostWithKill} of the {lost.Count} games lost in the stage); he struck him in {Sum(r => r.StruckHim)} (with an art {Sum(r => r.StruckHimArt)}), another in {Sum(r => r.StruckOther)}; ended in his reach {Sum(r => r.EndedInReach.Values.Sum())} ({split}), clear {Sum(r => r.EndedClear)}; marks on him: at the swallow in {Sum(r => r.MarkedAtSwallow)} of {reads.Count}, laid in the stage {Sum(r => r.MarksLaid)}, cashed {Sum(r => r.MarksCashed)}";
    }

    /// <summary>
    /// Issue 1386's shard race read: of the games where a beaten boss ran with the shard, how many took it, by the phases
    /// left on the countdown when it went, how many let it run out into the swallow, how many ended otherwise while he ran,
    /// and the player units that fell during the run. Null when no game ran a race.
    /// </summary>
    public static string? ShardLine(IReadOnlyList<GameResult> games)
    {
        var races = games.Where(g => g.ShardRace is not null).Select(g => g.ShardRace!).ToList();
        if (races.Count == 0)
        {
            return null;
        }

        var taken = races.Where(r => r.Taken).ToList();
        var left = taken.Count == 0 ? "-" : string.Join(", ", taken.GroupBy(r => r.Left).OrderByDescending(g => g.Key).Select(g => $"{g.Key}: {g.Count()}"));
        var falls = string.Join(", ", races.GroupBy(r => Math.Min(r.Falls, 2)).OrderBy(g => g.Key).Select(g => $"{(g.Key == 2 ? "2+" : g.Key.ToString(System.Globalization.CultureInfo.InvariantCulture))}: {g.Count()}"));
        return $"shard race: ran in {races.Count} of {games.Count}, run median turn {Median(races.Select(r => r.Turn))}; taken {taken.Count} (phases left {left}); ran out {races.Count(r => r.RanOut)}; ended while he ran {races.Count(r => !r.Taken && !r.RanOut)}; falls in the run {falls}";
    }

    /// <summary>
    /// The race read (Table round 514, Lotus's uncapped dose): in the games won in the second stage, how many player phases
    /// the kill took and how many Frozen Iron landings fell before it, each as a count by value, and the games won with no
    /// clock death, one, and two or more; in the games lost in it, the landing whose dose first killed a player unit, by
    /// value (none when the dose killed no one). Null when no game reached the stage. Data; round 514's target is a kill
    /// in rounds 3 to 4 against the dose's first kill in rounds 5 to 6.
    /// </summary>
    public static string? RaceLine(IReadOnlyList<GameResult> games)
    {
        var won = games.Where(g => g.Stage is not null && g.Won).Select(g => g.Stage!).ToList();
        var lost = games.Where(g => g.Stage is not null && !g.Won).Select(g => g.Stage!).ToList();
        if (won.Count + lost.Count == 0)
        {
            return null;
        }

        static string By(IEnumerable<int?> values) =>
            string.Join(", ", values.GroupBy(v => v).OrderBy(g => g.Key ?? int.MaxValue).Select(g => $"{(g.Key is { } k ? k.ToString(System.Globalization.CultureInfo.InvariantCulture) : "none")}: {g.Count()}"));
        var kills = won.Count == 0 ? "none won" : $"player phases to the kill {By(won.Select(s => (int?)s.PlayerPhases))}; landings before it {By(won.Select(s => (int?)s.Landings))}; clock deaths 0 in {won.Count(s => s.ClockDeaths == 0)}, 1 in {won.Count(s => s.ClockDeaths == 1)}, 2+ in {won.Count(s => s.ClockDeaths >= 2)} (gate: 2+ in at most 10 %: {(won.Count(s => s.ClockDeaths >= 2) * 10 <= won.Count ? "ok" : "FAILED")})";
        var deaths = lost.Count == 0 ? "none lost" : $"the dose's first kill at landing {By(lost.Select(s => s.FirstClockKill))}";
        return $"stage 2 race: won {won.Count}, {kills}; lost {lost.Count}, {deaths}";
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

    /// <summary>
    /// Issue 1441's split of the captain's stage-1 falls (round 539, <see cref="StageOne.CaptainFall"/>): by the veto's
    /// verdict on the tile he ended his last player phase on, then by how the enemy phase killed him. A fall on a tile that
    /// failed while another passed, or one that passed and fell with no crit and no arrival, is the planner's; a cornered
    /// one is the board's, a crit the dice's. Null when he
    /// never fell in stage 1.
    /// </summary>
    public static string? VetoSplitLine(IReadOnlyList<GameResult> games)
    {
        var falls = games.Select(g => g.StageOne?.CaptainFall).OfType<string>().ToList();
        if (falls.Count == 0)
        {
            return null;
        }

        var verdicts = new[] { "passed", "cornered", "failed", "unread" };
        var causes = new[] { "plain", "crit", "line", "arrival" };
        string Of(string verdict)
        {
            var some = falls.Where(f => f.StartsWith(verdict + ",", StringComparison.Ordinal)).ToList();
            return $"{verdict} {some.Count} ({string.Join(", ", causes.Select(c => $"{c} {some.Count(f => f == $"{verdict}, {c}")}"))})";
        }

        return $"stage 1 captain falls {falls.Count}, by the veto on his end tile: {string.Join("; ", verdicts.Select(Of))}; his own phase {falls.Count(f => f == "his own phase")}";
    }

    /// <summary>
    /// Round 539's healer-first read: in the games lost before the swallow, who of the company fell first in stage 1
    /// (<see cref="StageOne.Fallen"/>), and of the captain's stage-1 falls, how many came after an unarmed healer's in the
    /// same game. Null when no game read stage 1.
    /// </summary>
    public static string? FirstFallLine(IReadOnlyList<GameResult> games)
    {
        var reads = games.Where(g => g.StageOne is not null).ToList();
        if (reads.Count == 0)
        {
            return null;
        }

        var lost = reads.Where(g => !g.Won && g.StageOne!.SwallowTurn is null).ToList();
        string First(string kind) => lost.Count(g => g.StageOne!.Fallen.FirstOrDefault() == kind).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var captained = reads.Where(g => g.StageOne!.Fallen.Contains("captain")).ToList();
        var afterHer = captained.Count(g => g.StageOne!.Fallen.IndexOf("healer") is var h and >= 0 && h < g.StageOne!.Fallen.IndexOf("captain"));
        return $"stage 1 first fall, in the {lost.Count} games lost before the swallow: an unarmed healer {First("healer")}, the captain {First("captain")}, another {First("other")}, none {lost.Count(g => g.StageOne!.Fallen.Count == 0)}; the captain's stage-1 falls after a healer's in the same game {afterHer} of {captained.Count}";
    }

    private static int Median(IEnumerable<int> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted.Count == 0 ? 0 : sorted[(sorted.Count - 1) / 2];
    }
}
