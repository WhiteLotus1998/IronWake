using System.Globalization;
using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// Issue 47's prefix arm: the random legal player for turns 1 to <c>N</c>, then the
/// heuristic player from turn <c>N + 1</c> on, in the same game. The runner asks again
/// after every command, so the hand-over falls on the first player phase past the prefix.
/// </summary>
public sealed class PrefixPlayer : IPlayer
{
    private readonly int _prefix;
    private readonly RandomLegalPlayer _random;
    private readonly HeuristicPlayer _heuristic = new();

    public PrefixPlayer(int prefix, int seed)
    {
        _prefix = prefix;
        _random = new RandomLegalPlayer(seed);
    }

    /// <summary>The heuristic's highest refused kill over the turns it played (issue 125).</summary>
    public double? HighestRefusedKill => _heuristic.HighestRefusedKill;

    public IReadOnlyList<Command> Next(BattleState state, GameContent content) =>
        state.Turn <= _prefix ? _random.Next(state, content) : _heuristic.Next(state, content);
}

/// <summary>A player phase's state for issue 47's counters: dead, quiet, or live.</summary>
public enum TurnKind
{
    Dead,
    Quiet,
    Live,
}

/// <summary>
/// One player phase read at its start (issue 47). <see cref="TaxCount"/> and
/// <see cref="TaxFraction"/> are the wake tax: the destinations inside a sleeping Guard
/// group's wake radius, own tile included, from the player unit with the largest fraction.
/// </summary>
public sealed record TurnReading(int Turn, TurnKind Kind, int TaxCount, int Destinations)
{
    public double TaxFraction => Destinations == 0 ? 0 : (double)TaxCount / Destinations;
}

/// <summary>
/// The dead, quiet and live counters of issue 47, the diagnostic beside the free prefix.
/// A turn is live when some player unit can reach a tile from which a weapon it carries
/// strikes an enemy, or some enemy can reach a tile from which a weapon it carries strikes
/// a player unit (an enemy that holds strikes from its own tile only, the reading
/// <see cref="Exposure"/> uses). A member of a sleeping Guard group counts on neither side:
/// it will not move this turn and striking it is a decision to wake it, which is the quiet
/// state's. A turn is quiet when it is not live and some player unit can end its move
/// within the wake radius of a living member of a sleeping group; dead otherwise.
/// Healing staves are not weapons here.
/// </summary>
public static class TurnState
{
    public static TurnReading Read(BattleState state, GameContent content)
    {
        var sleeping = state.UnitsOf(Side.Enemy).Where(u => IsAsleep(state, u)).ToList();
        var awake = state.UnitsOf(Side.Enemy).Where(u => !IsAsleep(state, u)).ToList();
        var players = state.UnitsOf(Side.Player).ToList();
        var live = false;
        var bestCount = 0;
        var bestDestinations = 0;
        var bestFraction = -1.0;
        foreach (var player in players)
        {
            var destinations = state.ReachOf(player, content).Destinations.ToList();
            if (!live && awake.Any(enemy => Strikes(content, player, destinations, enemy.At)))
            {
                live = true;
            }

            var inside = destinations.Count(tile => sleeping.Any(s => s.At.DistanceTo(tile) <= content.WakeRadius));
            var fraction = destinations.Count == 0 ? 0 : (double)inside / destinations.Count;
            if (fraction > bestFraction)
            {
                bestFraction = fraction;
                bestCount = inside;
                bestDestinations = destinations.Count;
            }
        }

        if (!live)
        {
            foreach (var enemy in awake)
            {
                var from = state.EffectiveBehavior(enemy) == Behavior.Aggressive
                    ? state.ReachOf(enemy, content).Destinations.ToList()
                    : new List<Coord> { enemy.At };
                if (players.Any(player => Strikes(content, enemy, from, player.At)))
                {
                    live = true;
                    break;
                }
            }
        }

        var kind = live ? TurnKind.Live : bestCount > 0 ? TurnKind.Quiet : TurnKind.Dead;
        return new TurnReading(state.Turn, kind, bestCount, bestDestinations);
    }

    /// <summary>Whether a member of a Guard group that has not woken.</summary>
    public static bool IsAsleep(BattleState state, BattleUnit unit) =>
        unit is { Behavior: Behavior.Guard, Group: { } group } && !state.IsAwake(group);

    private static bool Strikes(GameContent content, BattleUnit unit, IReadOnlyList<Coord> from, Coord target)
    {
        for (var slot = 0; slot < unit.Unit.Inventory.Count; slot++)
        {
            if (unit.UsableWeaponAt(content, slot) is { Heals: false } weapon && from.Any(tile => weapon.InRange(tile.DistanceTo(target))))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Issue 47's measure, printed by <c>--full</c> and wired to no exit code: the free prefix
/// (the largest N of 2, 4, 6 whose paired drop against gate 1's baseline is within twice
/// its standard error, every shorter N free too), both slack figures, the refunded re-run
/// at the boundary N, and the dead, quiet and live counters over the baseline's games.
/// </summary>
public static class FreePrefix
{
    public static readonly IReadOnlyList<int> Lengths = new[] { 2, 4, 6 };

    /// <summary>
    /// The provisional wake-tax floor for <c>first quiet</c>: a quiet turn counts from the
    /// first turn whose tax reaches a quarter of the reach set. Issue 47 names 6 percent as
    /// noise and 44 percent as a decision and leaves the value to the journals; a quarter is
    /// the lean between them, and <c>--taxfloor</c> sets another.
    /// </summary>
    public const double DefaultTaxFloor = 0.25;

    /// <summary>One prefix arm against the baseline: the paired drop and its standard error, sqrt(b + c) / n over the discordant seeds, as gate 4 reads them.</summary>
    public sealed record Arm(int Prefix, int Wins, double Drop, double StandardError, bool Refunded = false)
    {
        /// <summary>Not distinguishable from the baseline: the drop within twice its standard error of zero, or below it.</summary>
        public bool Free => Drop <= 2 * StandardError;
    }

    /// <summary>The prefix arm at <paramref name="prefix"/> over the baseline's seeds; with <paramref name="refund"/> the turn limit is raised by the prefix and the baseline is not.</summary>
    public static Arm Play(GameContent content, MapDefinition map, int prefix, IReadOnlyList<GameResult> baseline, RollScheme scheme, bool refund = false)
    {
        var played = refund ? map with { TurnLimit = map.TurnLimit + prefix } : map;
        var down = 0;
        var up = 0;
        var wins = 0;
        for (var seed = 1; seed <= baseline.Count; seed++)
        {
            var won = Runner.Play(content, played, (ulong)seed, new PrefixPlayer(prefix, seed), scheme: scheme).Won;
            wins += won ? 1 : 0;
            var before = baseline[seed - 1].Won;
            if (before && !won)
            {
                down++;
            }
            else if (!before && won)
            {
                up++;
            }
        }

        var n = (double)baseline.Count;
        return new Arm(prefix, wins, (down - up) / n, Math.Sqrt(down + up) / n, refund);
    }

    /// <summary>The largest N such that every tried length up to it reads free; zero when the first does not.</summary>
    public static int Length(IReadOnlyList<Arm> arms)
    {
        var free = 0;
        foreach (var arm in arms.OrderBy(a => a.Prefix))
        {
            if (!arm.Free)
            {
                break;
            }

            free = arm.Prefix;
        }

        return free;
    }

    /// <summary>The first tried length that does not read free, or null on a map free at every length tried; the only N the refund re-runs.</summary>
    public static int? Boundary(IReadOnlyList<Arm> arms) =>
        arms.OrderBy(a => a.Prefix).FirstOrDefault(a => !a.Free)?.Prefix;

    /// <summary>
    /// The free prefix lines for one map: the arms, the refunded arm at the boundary when
    /// there is one, both slack figures, and the turn-state counters over
    /// <paramref name="readings"/>, one list per baseline game.
    /// </summary>
    public static IReadOnlyList<string> Report(GameContent content, MapDefinition map, string id, IReadOnlyList<GameResult> baseline, IReadOnlyList<IReadOnlyList<TurnReading>> readings, RollScheme scheme, double taxFloor = DefaultTaxFloor)
    {
        var arms = Lengths.Select(n => Play(content, map, n, baseline, scheme)).ToList();
        var lines = new List<string>
        {
            $"free prefix: {id}, {Length(arms)} of {string.Join(", ", Lengths)} tried, {Slack(baseline, map)} (printed, not gated)",
        };
        foreach (var arm in arms)
        {
            lines.Add("  " + Row(arm, baseline.Count));
        }

        if (Boundary(arms) is { } boundary)
        {
            var refunded = Play(content, map, boundary, baseline, scheme, refund: true);
            var reading = refunded.Free ? "at most tempo" : "positional";
            lines.Add($"  {Row(refunded, baseline.Count)}, limit raised by {boundary} against the unraised baseline: {reading}");
        }

        lines.AddRange(Counters(readings, taxFloor));
        return lines;
    }

    private static string Row(Arm arm, int seeds) =>
        $"prefix {arm.Prefix}{(arm.Refunded ? " refunded" : "")}: wins {arm.Wins}/{seeds}, drop {F3(arm.Drop)} se {F3(arm.StandardError)}, {(arm.Free ? "free" : "not free")}";

    /// <summary>Gate 1's median slack beside the limit less the p90 winning turn, the tempo a prefix can absorb before seeds flip.</summary>
    public static string Slack(IReadOnlyList<GameResult> baseline, MapDefinition map)
    {
        var winning = baseline.Where(g => g.Won).Select(g => g.Turns).OrderBy(t => t).ToList();
        if (winning.Count == 0)
        {
            return "slack - (no wins)";
        }

        return $"slack median {map.TurnLimit - Percentile(winning, 0.5)} p90 {map.TurnLimit - Percentile(winning, 0.9)}";
    }

    /// <summary>
    /// The turn-state counters over the baseline's games, medians across games: dead turns,
    /// quiet turns, the longest leading run of dead turns, the turn of first contact, the
    /// turn of first quiet at or above the tax floor; then one row per turn with the count
    /// of games in each state and the median wake tax of the games that reached it.
    /// </summary>
    public static IReadOnlyList<string> Counters(IReadOnlyList<IReadOnlyList<TurnReading>> readings, double taxFloor)
    {
        var games = readings.Where(r => r.Count > 0).ToList();
        if (games.Count == 0)
        {
            return new[] { "  turns: no player phases read" };
        }

        var lines = new List<string>
        {
            "  turns, medians over " + games.Count + " games: "
            + $"dead {MedianText(games.Select(g => (double?)g.Count(t => t.Kind == TurnKind.Dead)))}, "
            + $"quiet {MedianText(games.Select(g => (double?)g.Count(t => t.Kind == TurnKind.Quiet)))}, "
            + $"leading dead {MedianText(games.Select(g => (double?)g.TakeWhile(t => t.Kind == TurnKind.Dead).Count()))}, "
            + $"first contact {MedianText(games.Select(g => (double?)g.FirstOrDefault(t => t.Kind == TurnKind.Live)?.Turn))}, "
            + $"first quiet at tax {taxFloor.ToString("F2", CultureInfo.InvariantCulture)} {MedianText(games.Select(g => (double?)g.FirstOrDefault(t => t.Kind == TurnKind.Quiet && t.TaxFraction >= taxFloor)?.Turn))}",
        };
        var last = games.Max(g => g.Max(t => t.Turn));
        for (var turn = 1; turn <= last; turn++)
        {
            var here = games.Select(g => g.FirstOrDefault(t => t.Turn == turn)).OfType<TurnReading>().ToList();
            if (here.Count == 0)
            {
                continue;
            }

            var tax = MedianText(here.Select(t => (double?)t.TaxFraction), "F2");
            lines.Add($"  turn {turn}: dead {here.Count(t => t.Kind == TurnKind.Dead)} quiet {here.Count(t => t.Kind == TurnKind.Quiet)} live {here.Count(t => t.Kind == TurnKind.Live)}, wake tax {tax}");
        }

        return lines;
    }

    /// <summary>
    /// The median of the values, a game with no value (no contact, no quiet turn) counted
    /// as later than every game with one, so the median is a dash when at least half the
    /// games never got there.
    /// </summary>
    private static string MedianText(IEnumerable<double?> values, string format = "F1")
    {
        var list = values.OrderBy(v => v ?? double.MaxValue).ToList();
        var lower = list[(list.Count - 1) / 2];
        var upper = list[list.Count / 2];
        if (lower is null || upper is null)
        {
            return "-";
        }

        return ((lower.Value + upper.Value) / 2).ToString(format, CultureInfo.InvariantCulture);
    }

    private static string F3(double value) => value.ToString("F3", CultureInfo.InvariantCulture);

    private static int Percentile(List<int> sorted, double p) => sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(p * sorted.Count) - 1)];
}
