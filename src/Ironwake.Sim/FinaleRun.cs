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

    /// <summary>Story members beside the captain in the depleted company (issue 692).</summary>
    public const int DepletedStory = 5;

    /// <summary>Story members beside the captain in the floor company (issue 692).</summary>
    public const int FloorStory = 3;

    /// <summary>The median turn count over which the finale is too long; the lever is one fewer wave (issue 692).</summary>
    public const int LengthLimit = 16;

    /// <summary>AI-vs-AI games timed per company, as gate 7.</summary>
    public const int SpeedSeeds = 5;

    /// <summary>The slowest AI-vs-AI game allowed, in milliseconds, as gate 7.</summary>
    public const int SpeedLimitMs = 1000;

    /// <summary>The three companies the finale is measured with.</summary>
    public enum Company
    {
        Full,
        Depleted,
        Floor,
    }

    /// <summary>A company's name as printed.</summary>
    public static string Name(Company company) => company switch
    {
        Company.Full => "full",
        Company.Depleted => "depleted",
        _ => "floor",
    };

    /// <summary>
    /// The roster of <paramref name="company"/> in deploy order, captain first: story members in
    /// cast order raised to <paramref name="level"/>, then hires in the keep's hire order at
    /// <paramref name="level"/> less <see cref="Barracks.LevelsBelow"/>.
    /// </summary>
    public static ValueList<Unit> Roster(GameContent content, Company company, int level)
    {
        var story = company switch
        {
            Company.Full => content.Cast.Count - 1,
            Company.Depleted => DepletedStory,
            _ => FloorStory,
        };
        var hiresLevel = Math.Max(Unit.MinLevel, level - Barracks.LevelsBelow);
        var members = content.Cast.Take(1 + Math.Min(story, content.Cast.Count - 1)).Select(u => u.ScaledTo(level, content.Class(u.ClassId))).ToList();
        var hires = content.Campaign.Keep.Hires.Select(h => Barracks.Recruit(h, hiresLevel, content));
        members.AddRange(company == Company.Floor ? hires : hires.Take(Math.Max(0, CampaignRecord.CompanyCap - members.Count)));
        return ValueList<Unit>.From(members);
    }

    /// <summary>
    /// Why <paramref name="map"/> cannot be measured as the finale, or null: it must field the whole
    /// company (<c>deploy: all</c>), which its loader holds to at least <see cref="CampaignRecord.CompanyCap"/> placements.
    /// </summary>
    public static string? Refusal(MapDefinition map, string id) =>
        map.DeploysAll ? null : $"finale: {id} is not 'deploy: all'; the finale fields the whole company";

    /// <summary>One company's reading: the heuristic's games and the slowest AI-vs-AI game.</summary>
    public sealed record Reading(Company Company, int Size, IReadOnlyList<GameResult> Games, long SlowestMs, int TurnLimit, bool Bonded = false)
    {
        public int Wins => Games.Count(g => g.Won);

        /// <summary>The median turns played over every game, won or lost, a timeout counted at the limit: the finale's length line.</summary>
        public int MedianTurns => Median(Games.Select(g => Math.Min(g.Turns, TurnLimit)));

        /// <summary>Gate 1 on full and depleted; the floor is data.</summary>
        public bool Beatable => Company == Company.Floor || (double)Wins / Games.Count >= Gates.BeatableRate;

        public bool Fast => SlowestMs < SpeedLimitMs;

        public bool Short => MedianTurns <= LengthLimit;

        public bool Passed => Beatable && Fast && Short;

        public IEnumerable<string> Lines(int level)
        {
            var rate = (double)Wins / Games.Count;
            var verdict = Company == Company.Floor ? $"data, a cold chair's play decides ({(Wins > 0 ? "won at least once" : "never won")})" : Gates.Verdict(Beatable);
            yield return $"finale {Name(Company)}: {Size} units at level {level}, heuristic wins {Wins}/{Games.Count} ({rate:P0}), losses {Gates.LossCounts(Games)}: {verdict}";
            yield return $"  length: median {MedianTurns} turns over every game, limit {TurnLimit}: {(Short ? "ok" : $"over {LengthLimit}, the lever is one fewer wave")}";
            yield return $"  time: {SpeedSeeds} AI-vs-AI games, slowest {SlowestMs} ms: {Gates.Verdict(Fast)}";
            if (Bonded)
            {
                yield return "  " + BondLine(Games);
            }
        }
    }

    /// <summary>Plays <paramref name="company"/> on <paramref name="map"/>: <paramref name="seeds"/> heuristic games and <see cref="SpeedSeeds"/> timed AI-vs-AI games.</summary>
    public static Reading Measure(GameContent content, MapDefinition map, Company company, int level, int seeds, RollScheme scheme)
    {
        var roster = Roster(content, company, level);
        var fielded = content with { Cast = roster };
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
        return new Reading(company, size, games, slowest, map.TurnLimit, map.Bond is not null);
    }

    /// <summary>
    /// How the bound enemy of a <c>freed:</c> header left each game (issue 750): killed, freed, or
    /// standing at the end. Data only; the Sim's player does not price an ending.
    /// </summary>
    public static string BondLine(IReadOnlyList<GameResult> games) =>
        $"bond: hunter killed {games.Count(g => g.Bond == BondFate.Fell)}, freed {games.Count(g => g.Bond == BondFate.Freed)}, standing {games.Count(g => g.Bond is null)} of {games.Count} (data; the Sim does not price an ending)";

    private static int Median(IEnumerable<int> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted.Count == 0 ? 0 : sorted[(sorted.Count - 1) / 2];
    }
}
