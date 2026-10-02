namespace Ironwake.Core;

/// <summary>
/// What a support pair is about before a word of it is written (issue 77, round 192): no two of
/// one recruit's pairs share a kind. <see cref="Respect"/> is a rivalry turned respect.
/// </summary>
public enum SupportKind
{
    Argument,
    Comedy,
    Debt,
    Mentor,
    Respect,
    Romance,
}

/// <summary>
/// One support pair of <c>campaign.json</c> (issue 77): two cast members and the pair's kind.
/// A pair with the captain may carry <see cref="Romance"/>, the captain pronouns the recruit can
/// romance: with a captain of one of them the pair is a romance instead of its kind (orientation,
/// round 192). Empty on every pair between two recruits, whose kind is fixed.
/// </summary>
public sealed record SupportPair(string A, string B, SupportKind Kind)
{
    public ValueList<Pronoun> Romance { get; init; } = ValueList<Pronoun>.Empty;

    public bool Involves(string unitId) => A == unitId || B == unitId;

    /// <summary>The other member of the pair from <paramref name="unitId"/>'s side.</summary>
    public string Partner(string unitId) => A == unitId ? B : A;

    /// <summary>The pair's kind with a captain of <paramref name="captain"/>: a romance if the recruit can romance that captain, else <see cref="Kind"/>.</summary>
    public SupportKind KindFor(Pronoun? captain) =>
        captain is { } pronoun && Romance.Contains(pronoun) ? SupportKind.Romance : Kind;
}

/// <summary>Queries over a campaign's support pairs (issue 77).</summary>
public static class Supports
{
    /// <summary>The pairs <paramref name="unitId"/> belongs to, in file order.</summary>
    public static IReadOnlyList<SupportPair> PairsOf(CampaignRules campaign, string unitId) =>
        campaign.Supports.Where(p => p.Involves(unitId)).ToList();

    /// <summary>The pair of <paramref name="a"/> and <paramref name="b"/> in either order, or null when they have none.</summary>
    public static SupportPair? Pair(CampaignRules campaign, string a, string b) =>
        campaign.Supports.FirstOrDefault(p => p.Involves(a) && p.Involves(b) && a != b);

    /// <summary>
    /// The support tier <paramref name="a"/> and <paramref name="b"/> stand at on
    /// <paramref name="points"/> of rapport (issue 77): the highest of the rivalry block's
    /// <see cref="RivalryRules.SupportTiers"/> reached, or null when they are no support pair or
    /// below C. Rapport between two people who are not a pair still ends a rivalry, and never
    /// reaches a tier.
    /// </summary>
    public static SupportTier? TierOf(GameContent content, string a, string b, int points) =>
        Pair(content.Campaign, a, b) is null ? null : content.Rivalry.TierFor(points);

    /// <summary>
    /// The support bonus <paramref name="unit"/> fights with where it stands (issue 77): of the
    /// living allies adjacent to it with whom it is a support pair, the best tier their rapport on
    /// this board reaches, as that tier's hit, avoid and crit. Two partners beside it give the best
    /// of the two, never the sum. None when no partner beside it stands at C or better. The unit
    /// itself is skipped by id, so a forecast may pass it at a tile it has not moved to yet.
    /// </summary>
    public static CombatBonus Bonus(BattleState state, GameContent content, BattleUnit unit)
    {
        if (content.Rivalry.SupportTiers.Count == 0 || content.Campaign.Supports.Count == 0)
        {
            return CombatBonus.None;
        }

        SupportTier? best = null;
        foreach (var other in state.Units)
        {
            if (other.Id == unit.Id || other.Side != unit.Side || other.At.DistanceTo(unit.At) != 1)
            {
                continue;
            }

            if (TierOf(content, unit.Id, other.Id, Rivalry.PointsOf(state, unit.Id, other.Id)) is { } tier && (best is null || tier.At > best.At))
            {
                best = tier;
            }
        }

        return best is null ? CombatBonus.None : new CombatBonus(best.Hit, best.Avoid, best.Crit, 0);
    }
}
