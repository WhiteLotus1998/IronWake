namespace Ironwake.Core;

/// <summary>
/// One arm of the rivalry spike (DESIGN.md 13.1, issue 16), a row of <c>rules.json</c>:
/// the hit and crit a unit gains while a rival stands adjacent, the change to its own crit
/// avoid, and whether the hit and crit apply only when it counters. Crit avoid applies
/// whenever a rival is adjacent, whatever the role.
/// </summary>
public sealed record RivalryArm(string Id, int Hit, int Crit, int CritAvoid, bool CountersOnly);

/// <summary>A step of the rapport rate: a recruit with at least <see cref="Cha"/> adds <see cref="Rate"/> per player phase adjacent.</summary>
public sealed record RapportStep(int Cha, int Rate);

/// <summary>
/// The rivalry and rapport numbers of <c>rules.json</c> (issue 16). All content: the arms,
/// the rate table by Cha (ascending, the first step at Cha 0), and the rapport at which a
/// rival pair stops being rivals. <see cref="None"/> is content without a rivalry block,
/// which no map can turn on.
/// </summary>
public sealed record RivalryRules(ValueList<RivalryArm> Arms, ValueList<RapportStep> RapportRates, int OverwriteAt)
{
    public static RivalryRules None { get; } = new(ValueList<RivalryArm>.Empty, ValueList<RapportStep>.Empty, 0);

    public RivalryArm? Arm(string id) => Arms.FirstOrDefault(a => a.Id == id);

    /// <summary>The rate of the highest step at or below <paramref name="cha"/>; 0 below every step.</summary>
    public int RateFor(int cha)
    {
        var rate = 0;
        foreach (var step in RapportRates)
        {
            if (cha >= step.Cha)
            {
                rate = step.Rate;
            }
        }

        return rate;
    }
}

/// <summary>A pair's rapport, <see cref="A"/> before <see cref="B"/> in ordinal order.</summary>
public sealed record Rapport(string A, string B, int Points);

/// <summary>
/// Rapport and Rivalry (DESIGN.md 13.1, issue 16), behind a map's <c>rivalry:</c> header.
/// A recruit is a deployed player unit other than the captain with a region. Two recruits of
/// different regions are rivals until their rapport reaches the overwrite threshold. While a
/// rival stands adjacent, a unit fights with the map's arm applied. At the end of every
/// player phase each adjacent pair of recruits gains the sum of both recruits' rates, a rate
/// read from the step table by the recruit's effective Cha. With no header nothing here
/// changes a number and no rapport accrues.
/// </summary>
public static class Rivalry
{
    /// <summary>The arm the map turns on, or null when it has none.</summary>
    public static RivalryArm? ArmOf(BattleState state, GameContent content) =>
        state.Map.RivalryArm is { } id
            ? content.Rivalry.Arm(id) ?? throw new ArgumentException($"map '{state.Map.Name}' names rivalry arm '{id}' and the content has none by that id", nameof(state))
            : null;

    public static bool IsRecruit(BattleUnit unit) =>
        unit.Side == Side.Player && !unit.IsCaptain && unit.Unit.Region is not null;

    /// <summary>The pair's rapport on this board, 0 when it has none yet.</summary>
    public static int PointsOf(BattleState state, string a, string b)
    {
        var (first, second) = Ordered(a, b);
        foreach (var entry in state.Rapport)
        {
            if (entry.A == first && entry.B == second)
            {
                return entry.Points;
            }
        }

        return 0;
    }

    /// <summary>Whether two units are rivals on this board: both recruits, of different regions, their rapport short of the threshold.</summary>
    public static bool AreRivals(BattleState state, GameContent content, BattleUnit a, BattleUnit b) =>
        state.Map.RivalryArm is not null
        && IsRecruit(a) && IsRecruit(b) && a.Id != b.Id
        && a.Unit.Region != b.Unit.Region
        && PointsOf(state, a.Id, b.Id) < content.Rivalry.OverwriteAt;

    /// <summary>
    /// The rivals standing next to <paramref name="unit"/> where it stands now, in id order.
    /// The unit itself is skipped by id, so a forecast may pass it at a tile it has not
    /// moved to yet.
    /// </summary>
    public static IReadOnlyList<BattleUnit> AdjacentRivals(BattleState state, GameContent content, BattleUnit unit) =>
        state.Units.Where(other => other.Id != unit.Id && other.At.DistanceTo(unit.At) == 1 && AreRivals(state, content, unit, other)).ToList();

    /// <summary>
    /// The modifiers <paramref name="unit"/> fights with: hit, crit and crit avoid. Hit and
    /// crit apply when a rival is adjacent and the arm is not counters-only or the unit is
    /// <paramref name="countering"/>; crit avoid applies whenever a rival is adjacent.
    /// </summary>
    public static (int Hit, int Crit, int CritAvoid) Modifiers(BattleState state, GameContent content, BattleUnit unit, bool countering)
    {
        if (ArmOf(state, content) is not { } arm || AdjacentRivals(state, content, unit).Count == 0)
        {
            return (0, 0, 0);
        }

        var shows = !arm.CountersOnly || countering;
        return (shows ? arm.Hit : 0, shows ? arm.Crit : 0, arm.CritAvoid);
    }

    /// <summary>The rapport a recruit adds per player phase beside another, from its effective Cha.</summary>
    public static int RateOf(BattleUnit unit, GameContent content) =>
        content.Rivalry.RateFor(unit.Unit.EffectiveStats(content.Class(unit.Unit.ClassId)).Cha);

    /// <summary>
    /// The end of a player phase: every adjacent pair of recruits gains both rates, pairs in
    /// id order, with a <see cref="RapportGained"/> each and a <see cref="RivalryEnded"/>
    /// for a rival pair that crosses the threshold. No header, no change.
    /// </summary>
    public static BattleState Accrue(BattleState state, GameContent content, List<GameEvent> events)
    {
        if (state.Map.RivalryArm is null)
        {
            return state;
        }

        var recruits = state.Units.Where(IsRecruit).OrderBy(u => u.Id, StringComparer.Ordinal).ToList();
        var table = state.Rapport.ToDictionary(r => (r.A, r.B), r => r.Points);
        for (var i = 0; i < recruits.Count; i++)
        {
            for (var j = i + 1; j < recruits.Count; j++)
            {
                var a = recruits[i];
                var b = recruits[j];
                if (a.At.DistanceTo(b.At) != 1)
                {
                    continue;
                }

                var amount = RateOf(a, content) + RateOf(b, content);
                if (amount == 0)
                {
                    continue;
                }

                var before = table.GetValueOrDefault((a.Id, b.Id));
                var after = before + amount;
                table[(a.Id, b.Id)] = after;
                events.Add(new RapportGained(a.Id, b.Id, amount, after));
                if (a.Unit.Region != b.Unit.Region && before < content.Rivalry.OverwriteAt && after >= content.Rivalry.OverwriteAt)
                {
                    events.Add(new RivalryEnded(a.Id, b.Id));
                }
            }
        }

        var entries = table
            .Select(kv => new Rapport(kv.Key.A, kv.Key.B, kv.Value))
            .OrderBy(r => r.A, StringComparer.Ordinal)
            .ThenBy(r => r.B, StringComparer.Ordinal);
        return state with { Rapport = ValueList<Rapport>.From(entries) };
    }

    private static (string, string) Ordered(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
}
