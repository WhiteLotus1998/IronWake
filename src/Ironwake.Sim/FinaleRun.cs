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

            if (StageLine(Games) is { } stage)
            {
                yield return "  " + stage;
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
    /// The boss's second stage over the games (issue 1385's Sim gate): how many reached it, its median length in phases
    /// begun, and the games by player units Frozen Iron killed; null when no game reached it. Data; the gate is that the
    /// clock kills at most one unit of a party that commits.
    /// </summary>
    public static string? StageLine(IReadOnlyList<GameResult> games)
    {
        var reached = games.Where(g => g.Stage is not null).Select(g => g.Stage!).ToList();
        if (reached.Count == 0)
        {
            return null;
        }

        var won = games.Count(g => g.Stage is not null && g.Won);
        return $"stage 2: reached {reached.Count} of {games.Count}, won {won}; median {Median(reached.Select(s => s.Phases))} phases; clock deaths 0 in {reached.Count(s => s.ClockDeaths == 0)}, 1 in {reached.Count(s => s.ClockDeaths == 1)}, 2+ in {reached.Count(s => s.ClockDeaths >= 2)} (gate: at most 1)";
    }

    private static int Median(IEnumerable<int> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted.Count == 0 ? 0 : sorted[(sorted.Count - 1) / 2];
    }
}
