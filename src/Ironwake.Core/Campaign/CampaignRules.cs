namespace Ironwake.Core;

/// <summary>
/// One map of the campaign as <c>campaign.json</c> lists it (issue 74): the map's id (its file
/// name under <c>content/maps</c> without the extension), the purse's <see cref="Reward"/> for
/// winning it, and the <see cref="Stock"/> the shop sells on the screen before it. Stock is fixed
/// and unlimited, a list the player can plan two maps ahead, never a roll.
/// </summary>
public sealed record CampaignMap(string MapId, int Reward, ValueList<string> Stock);

/// <summary>
/// A certification trial the campaign offers (issue 252, DESIGN section 13.6): the class it
/// certifies into and the trial map's id, its file name under <c>content/trials</c> without the
/// extension. A trial stands in for the seal once the class's requirements are met.
/// </summary>
public sealed record CampaignTrial(string ClassId, string MapId);

/// <summary>
/// The campaign's content (issue 74, DESIGN section 9): the purse a campaign starts with, the
/// price of a certification (the seal, paid from the purse), and the maps in the order they are
/// played. <see cref="None"/> is content without a <c>campaign.json</c>, which plays single maps only.
/// </summary>
public sealed record CampaignRules(int StartingPurse, int CertificationPrice, ValueList<CampaignMap> Maps)
{
    public static CampaignRules None { get; } = new(0, 0, ValueList<CampaignMap>.Empty);

    /// <summary>The certification trials, one per class at most, in class id order (issue 252); a class without one certifies only with a seal.</summary>
    public ValueList<CampaignTrial> Trials { get; init; } = ValueList<CampaignTrial>.Empty;

    /// <summary>The keep's map and the edits sold for it (issue 82, an experiment); <see cref="KeepMenu.None"/> when the campaign has none.</summary>
    public KeepMenu Keep { get; init; } = KeepMenu.None;

    /// <summary>The trial that certifies into <paramref name="classId"/>, or null when that class has none.</summary>
    public CampaignTrial? TrialFor(string classId) => Trials.FirstOrDefault(t => t.ClassId == classId);

    /// <summary>
    /// What repairing one use of <paramref name="weapon"/> costs: its price over its durability,
    /// at least 1. Null for a weapon that cannot be repaired: a spell, which refreshes every map
    /// (section 5), or a weapon without a price, which no shop has ever sold.
    /// </summary>
    public static int? RepairPricePerUse(Weapon weapon) =>
        weapon.IsMagic || weapon.Price is not { } price ? null : Math.Max(1, price / weapon.Durability);
}
