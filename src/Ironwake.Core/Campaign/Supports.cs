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
}
