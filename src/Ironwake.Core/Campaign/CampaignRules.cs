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

    /// <summary>
    /// The recruits who join the company at this map's camp (issue 763, DESIGN section 14), as cast
    /// ids: each is off the roster until this map and is then present like an arrival, but placed on
    /// no tile of its own; the map's bare slots take them in roster order, so they may sit the battle
    /// out and still join. A recruit who joins in the middle of the campaign could never have been fed,
    /// so every joiner and arrival joins at no less than the living company's median level
    /// (<see cref="CampaignRecord.JoinLevel"/>). Empty for a map nobody joins at.
    /// </summary>
    public ValueList<string> Joins { get; init; } = ValueList<string>.Empty;

    /// <summary>
    /// The two claimants offered at this map's camp (issue 633, DESIGN section 14's branch), as cast
    /// ids: both are off the roster until this map, the camp's <c>pick</c> chooses one, who then joins
    /// like a <see cref="Joins"/> entry, and the one passed on never joins the company. A march to this
    /// map is refused until the pick is made (<see cref="CampaignRecord.MarchRefusal"/>). Empty for a
    /// map with no branch; at most one map carries one.
    /// </summary>
    public ValueList<string> Branch { get; init; } = ValueList<string>.Empty;

    /// <summary>The most words a claimant's <see cref="Pitch"/> line holds: WRITING.md's spoken line.</summary>
    public const int PitchWordsMax = 25;

    /// <summary>
    /// Each claimant's own line at the branch's camp (issue 804, item 5: the choice screen), in
    /// <see cref="Branch"/> order, one per claimant: what they offer, printed beside their name until
    /// the pick is made. Empty when the map has no branch or the branch carries no lines.
    /// </summary>
    public ValueList<string> Pitch { get; init; } = ValueList<string>.Empty;

    /// <summary>
    /// The side characters who may be met at this map's camp (issue 633 slice 3, DESIGN section 14),
    /// as cast ids, one or two: each is off the roster until this map, the camp's <c>meet</c> takes at
    /// most one, who then joins like a <see cref="Joins"/> entry if a bed is free, and anyone not met
    /// never joins. A march is never refused for want of a meeting. Empty for a map with no meeting.
    /// </summary>
    public ValueList<string> Meets { get; init; } = ValueList<string>.Empty;

    /// <summary>
    /// Where the claimant passed on at the branch comes back as a foe on this map (issue 633, DESIGN
    /// section 14), or null on every other map. Read only once the pick is made; the claimant fights
    /// with their own card at the pick's level (<see cref="CampaignRecord.Begin"/>). At most one map
    /// carries it, and only after the map with the branch.
    /// </summary>
    public CampaignReturn? Return { get; init; }

    /// <summary>
    /// The enemy level this map is fought at in the campaign (issue 704, the curve of rounds 223 to 226),
    /// in place of its file's <c>enemy_level</c>, or null to keep the file's. The standalone map is never
    /// changed by it; a difficulty's offset is added on top, as on any map.
    /// </summary>
    public int? EnemyLevel { get; init; }

    /// <summary>
    /// The enemy placements this map fields a different template on in the campaign (issue 704): the
    /// placement on each tile keeps its group, behaviour and boss flag and takes the named template.
    /// The standalone map is never changed by it. Empty for a map fought as its file reads.
    /// </summary>
    public ValueList<TemplateSwap> Swaps { get; init; } = ValueList<TemplateSwap>.Empty;

    /// <summary>
    /// <paramref name="map"/> as the campaign fights it (issue 704): at <see cref="EnemyLevel"/> when one
    /// is named and with every <see cref="Swaps"/> entry made. Throws when a swap's tile holds no enemy
    /// placement, which the content tests hold for every shipped map.
    /// </summary>
    public MapDefinition Prepare(MapDefinition map)
    {
        var placements = map.Placements;
        foreach (var swap in Swaps)
        {
            var index = placements.ToList().FindIndex(p => p is EnemyPlacement && p.At == swap.At);
            if (index < 0)
            {
                throw new InvalidOperationException($"campaign map '{MapId}': swap at {swap.At} names a tile with no enemy placement");
            }

            placements = placements.SetItem(index, ((EnemyPlacement)placements[index]) with { TemplateId = swap.TemplateId });
        }

        return map with { EnemyLevel = EnemyLevel ?? map.EnemyLevel, Placements = placements };
    }
}

/// <summary>A campaign-only template swap (issue 704): the enemy placement on <see cref="At"/> fields <see cref="TemplateId"/>.</summary>
public sealed record TemplateSwap(Coord At, string TemplateId);

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

    /// <summary>
    /// The gated heirloom a win opens (part 1 only; issue 635, round 266): the member's own, whose
    /// last stage is held until this quest is won (<see cref="HeirloomLadder.Held"/>,
    /// <see cref="Heirloom.OpenGate"/>), or null for none.
    /// </summary>
    public string? Wakes { get; init; }

    /// <summary>
    /// The heirloom a win names (part 2 only; issue 635, rounds 268 to 270): the member's own, whose
    /// ladder carries a true name (<see cref="HeirloomLadder.Named"/>). The stack takes the name at
    /// whatever stage it has reached, and its woken-only art opens (<see cref="Heirloom.ArtOpen"/>).
    /// Counts as an issued signature for the rare material (<see cref="Forge.RareNeeded"/>).
    /// </summary>
    public string? Names { get; init; }

    /// <summary>The common material a win puts in the company's stores (issue 647); 0 for none.</summary>
    public int Common { get; init; }

    /// <summary>The rare material a win puts in the company's stores (issue 647), held by the validator to exactly what the issued signatures need (<see cref="Forge.RareRefusal"/>).</summary>
    public int Rare { get; init; }

    public ValueList<string> Before { get; init; } = ValueList<string>.Empty;

    public ValueList<string> After { get; init; } = ValueList<string>.Empty;

    /// <summary>
    /// The main map after whose win the quest opens (issue 691), in place of the part's own timing,
    /// or null for a member's quest timed by arrival. A quest with it is offered at every camp
    /// from then on while its member stands, and at no camp before: nothing names it earlier. A
    /// member whose arrival map the campaign does not name yet (Pell's quest 1, issue 635 slice 6)
    /// takes it to hold DESIGN 14's slot.
    /// </summary>
    public string? OpensAfter { get; init; }

    /// <summary>
    /// The hidden class a win puts the member in (issue 691), its mastery held at once, or null for
    /// none. Only a <see cref="UnitClass.Hidden"/> class is named here, by validator.
    /// </summary>
    public string? Promotes { get; init; }

    /// <summary>
    /// The ending line a won quest gives its member in place of <see cref="Barracks.EndingLine(string)"/>
    /// (issue 691), or null for none; read only when the member is a hire alive at the end.
    /// </summary>
    public string? Ending { get; init; }
}

/// <summary>
/// The campaign's content (issue 74, DESIGN section 9): the purse a campaign starts with, the
/// price of a certification (the seal, paid from the purse), and the maps in the order they are
/// played. <see cref="None"/> is content without a <c>campaign.json</c>, which plays single maps only.
/// </summary>
public sealed record CampaignRules(int StartingPurse, int CertificationPrice, ValueList<CampaignMap> Maps)
{
    public static CampaignRules None { get; } = new(0, 0, ValueList<CampaignMap>.Empty);

    /// <summary>
    /// The seal for a step into an advanced form (issue 704), <c>advancedCertificationPrice</c>;
    /// <see cref="CertificationPrice"/> when the file names none.
    /// </summary>
    public int AdvancedCertificationPrice { get; init; }

    /// <summary>The seal a promotion into <paramref name="target"/> costs: the advanced price for an advanced form, else the plain one.</summary>
    public int SealFor(UnitClass target) => target.Advances is null ? CertificationPrice : AdvancedCertificationPrice;

    /// <summary>The certification trials, one per class at most, in class id order (issue 252); a class without one certifies only with a seal.</summary>
    public ValueList<CampaignTrial> Trials { get; init; } = ValueList<CampaignTrial>.Empty;

    /// <summary>
    /// The weapons the campaign issues to a cast member as they join (issue 804, round 263), cast id
    /// to weapon id, from <c>issues</c>: the weapon goes in front of their pack at full uses in place
    /// of the first weapon of its type they carry (Keziah's iron axe gives way to Kinsbane). A battle
    /// outside the campaign reads the cast file and carries none of them.
    /// </summary>
    public ValueList<CampaignIssue> Issues { get; init; } = ValueList<CampaignIssue>.Empty;

    /// <summary>The weapon the campaign issues <paramref name="unitId"/> as they join, or null.</summary>
    public string? IssuedTo(string unitId) => Issues.FirstOrDefault(i => i.UnitId == unitId)?.WeaponId;

    /// <summary>The side maps of the members' stories (issue 635), in file order, which breaks ties in the order they are offered.</summary>
    public ValueList<CampaignQuest> Quests { get; init; } = ValueList<CampaignQuest>.Empty;

    /// <summary>The side map <paramref name="questId"/>, or null when the campaign has none by that id.</summary>
    public CampaignQuest? Quest(string questId) => Quests.FirstOrDefault(q => q.Id == questId);

    /// <summary>The keep's map and the edits sold for it (issue 82, an experiment); <see cref="KeepMenu.None"/> when the campaign has none.</summary>
    public KeepMenu Keep { get; init; } = KeepMenu.None;

    /// <summary>The drake a cast member rides (issue 805), from <c>drake</c>; null when the campaign has none.</summary>
    public DrakeRules? Drake { get; init; }

    /// <summary>The forge's numbers (issue 647); <see cref="ForgeRules.None"/> when the campaign has no <c>forge</c>.</summary>
    public ForgeRules Forge { get; init; } = ForgeRules.None;

    /// <summary>The captain's origins (issue 681), in file order; empty when the campaign offers none and the captain is the cast file's.</summary>
    public ValueList<CaptainOrigin> Origins { get; init; } = ValueList<CaptainOrigin>.Empty;

    /// <summary>The support pairs (issue 77), in file order; empty when the campaign has none.</summary>
    public ValueList<SupportPair> Supports { get; init; } = ValueList<SupportPair>.Empty;

    /// <summary>The origin <paramref name="originId"/>, or null when the campaign has none by that id.</summary>
    public CaptainOrigin? Origin(string originId) => Origins.FirstOrDefault(o => o.Id == originId);

    /// <summary>
    /// The index of the map <paramref name="unitId"/> arrives on (issue 632), joins at (issue 763), is
    /// offered at as a claimant (issue 633) or may be met at (issue 633 slice 3),
    /// or -1 for a unit no map names, who is on the roster from the first map.
    /// </summary>
    public int ArrivalIndex(string unitId)
    {
        for (var i = 0; i < Maps.Count; i++)
        {
            if (Maps[i].Arrives.Contains(unitId) || Maps[i].Joins.Contains(unitId) || Maps[i].Branch.Contains(unitId) || Maps[i].Meets.Contains(unitId))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The index of the main map <paramref name="mapId"/> in play order, or -1 when the campaign does not list it.</summary>
    public int MapIndexOf(string mapId)
    {
        for (var i = 0; i < Maps.Count; i++)
        {
            if (Maps[i].MapId == mapId)
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
    /// (section 5), a weapon without a price, which no shop has ever sold, or a glass weapon (issue 702).
    /// </summary>
    public static int? RepairPricePerUse(Weapon weapon) =>
        weapon.IsMagic || weapon.Glass || weapon.Price is not { } price ? null : Math.Max(1, price / weapon.Durability);
}

/// <summary>
/// The tile, group and behaviour the passed claimant is placed under on the map that brings them back
/// (issue 633, <see cref="CampaignMap.Return"/>); the group is one the map's own enemies use, so they
/// sleep and wake with it.
/// </summary>
public sealed record CampaignReturn(Coord At, string Group, Behavior Behavior);

/// <summary>A weapon the campaign issues a cast member as they join (issue 804): <c>issues</c> in <c>campaign.json</c>.</summary>
public sealed record CampaignIssue(string UnitId, string WeaponId);
