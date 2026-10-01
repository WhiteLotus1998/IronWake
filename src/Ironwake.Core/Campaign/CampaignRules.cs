namespace Ironwake.Core;

/// <summary>
/// One map of the campaign as <c>campaign.json</c> lists it (issue 74): the map's id (its file
/// name under <c>content/maps</c> without the extension), the purse's <see cref="Reward"/> for
/// winning it, and the <see cref="Stock"/> the shop sells on the screen before it. Stock is fixed
/// and unlimited, a list the player can plan two maps ahead, never a roll.
/// </summary>
public sealed record CampaignMap(string MapId, int Reward, ValueList<string> Stock)
{
    /// <summary>
    /// The text card printed before the map's screen (issue 631, DESIGN section 14), one paragraph
    /// per entry, in the captain's voice; empty for a map without one.
    /// </summary>
    public ValueList<string> Before { get; init; } = ValueList<string>.Empty;

    /// <summary>The text card printed after the map is won (issue 631), as <see cref="Before"/>; empty for a map without one.</summary>
    public ValueList<string> After { get; init; } = ValueList<string>.Empty;

    /// <summary>
    /// The recruits who arrive on this map (issue 632, DESIGN section 14), as cast ids: each is off
    /// the roster until this map, joins its battle, and stays if it stands. Empty for a map nobody
    /// arrives on; a recruit no map names is on the roster from the first map.
    /// </summary>
    public ValueList<string> Arrives { get; init; } = ValueList<string>.Empty;
}

/// <summary>
/// A certification trial the campaign offers (issue 252, DESIGN section 13.6): the class it
/// certifies into and the trial map's id, its file name under <c>content/trials</c> without the
/// extension. A trial stands in for the seal once the class's requirements are met.
/// </summary>
public sealed record CampaignTrial(string ClassId, string MapId);

/// <summary>
/// A side map of a member's story (issue 635, DESIGN section 14): its id, the member it belongs
/// to, which of their two quests it is (1 or 2), and the map's id, its file name under
/// <c>content/quests</c> without the extension. The board is the trial shape: its <c>captain</c>
/// slot is the member's, so the member's death loses it, and its one bare <c>recruit</c> slot is
/// the ally the player picks. <see cref="Before"/> and <see cref="After"/> are its text cards.
/// <see cref="Pays"/> is a part 2 quest's payout: the member's signature item, a weapon bound to them.
/// </summary>
public sealed record CampaignQuest(string Id, string MemberId, int Part, string MapId)
{
    /// <summary>The signature item a win puts in the member's pack at full uses (part 2 only), or null for none.</summary>
    public string? Pays { get; init; }

    /// <summary>The common material a win puts in the company's stores (issue 647); 0 for none.</summary>
    public int Common { get; init; }

    /// <summary>The rare material a win puts in the company's stores (issue 647), held by the validator to exactly what the issued signatures need (<see cref="Forge.RareRefusal"/>).</summary>
    public int Rare { get; init; }

    public ValueList<string> Before { get; init; } = ValueList<string>.Empty;

    public ValueList<string> After { get; init; } = ValueList<string>.Empty;
}

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

    /// <summary>The side maps of the members' stories (issue 635), in file order, which breaks ties in the order they are offered.</summary>
    public ValueList<CampaignQuest> Quests { get; init; } = ValueList<CampaignQuest>.Empty;

    /// <summary>The side map <paramref name="questId"/>, or null when the campaign has none by that id.</summary>
    public CampaignQuest? Quest(string questId) => Quests.FirstOrDefault(q => q.Id == questId);

    /// <summary>The keep's map and the edits sold for it (issue 82, an experiment); <see cref="KeepMenu.None"/> when the campaign has none.</summary>
    public KeepMenu Keep { get; init; } = KeepMenu.None;

    /// <summary>The forge's numbers (issue 647); <see cref="ForgeRules.None"/> when the campaign has no <c>forge</c>.</summary>
    public ForgeRules Forge { get; init; } = ForgeRules.None;

    /// <summary>The captain's origins (issue 681), in file order; empty when the campaign offers none and the captain is the cast file's.</summary>
    public ValueList<CaptainOrigin> Origins { get; init; } = ValueList<CaptainOrigin>.Empty;

    /// <summary>The origin <paramref name="originId"/>, or null when the campaign has none by that id.</summary>
    public CaptainOrigin? Origin(string originId) => Origins.FirstOrDefault(o => o.Id == originId);

    /// <summary>
    /// The index of the map <paramref name="unitId"/> arrives on (issue 632), or -1 for a unit no
    /// map names, who is on the roster from the first map.
    /// </summary>
    public int ArrivalIndex(string unitId)
    {
        for (var i = 0; i < Maps.Count; i++)
        {
            if (Maps[i].Arrives.Contains(unitId))
            {
                return i;
            }
        }

        return -1;
    }

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
