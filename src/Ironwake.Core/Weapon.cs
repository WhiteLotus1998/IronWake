namespace Ironwake.Core;

/// <summary>
/// A weapon or spell from DESIGN.md section 5. For Reason and Faith, <see cref="Durability"/>
/// is uses per battle and refreshes each map; for physical weapons it is total uses before
/// the weapon breaks. A Faith weapon with <see cref="Heals"/> set is a healing spell:
/// its Mt is unused and <see cref="HealBase"/> feeds the heal formula. <see cref="Rank"/> is
/// the weapon skill rank a unit needs to equip it (issue 67). <see cref="Price"/> is what the
/// between-map shop charges for one at full uses (issue 74); a weapon without one is never sold
/// and never repaired. <see cref="Ignites"/> marks a fire weapon: on a <c>wildfire: on</c> map a
/// hit from it sets a forest tile alight (DESIGN.md 13.15, experiment; <see cref="Wildfire"/>).
/// <see cref="Windup"/> marks a slow weapon: on a <c>windup: on</c> map an attack with it raises a
/// blow over the target's tile that lands at the wielder's side's next phase start
/// (DESIGN.md 13.16, experiment; <see cref="Ironwake.Core.Windup"/>). It counters as any weapon does.
/// <see cref="BoundTo"/> marks a signature item (issue 635, DESIGN section 14): bound to one cast
/// member, never sold, paid by their quest 2, and lost with them.
/// <see cref="Description"/> is the one line the item card prints (issue 650), required by the content validator.
/// </summary>
public sealed record Weapon(
    string Id,
    string Name,
    WeaponType Type,
    int Mt,
    int Hit,
    int Crit,
    int Wt,
    int MinRange,
    int MaxRange,
    int Durability,
    ValueList<MovementType> EffectiveAgainst,
    bool Heals = false,
    int HealBase = 0,
    WeaponRank Rank = WeaponRank.E,
    int? Price = null,
    bool Ignites = false,
    bool Windup = false)
{
    /// <summary>The cast id this signature item is bound to (issue 635), or null for an ordinary weapon.</summary>
    public string? BoundTo { get; init; }

    /// <summary>
    /// Marks a hungering weapon (DESIGN.md 13.23, experiment; <see cref="Kinsbane"/>): it drains
    /// its carrier at each phase start it went unfed, feeds on its kills and grows with them.
    /// </summary>
    public bool Hungers { get; init; }

    /// <summary>
    /// Marks an heirloom (issue 646; <see cref="Ironwake.Core.Heirloom"/>): the weapon's own numbers
    /// are its first stage, and the ladder's turns replace them as a hidden combat counter grows.
    /// Null for an ordinary weapon.
    /// </summary>
    public HeirloomLadder? Heirloom { get; init; }

    /// <summary>
    /// What a hungering weapon says to its carrier (issue 804 item 3; <see cref="Kinsbane.Speak"/>),
    /// or null for a weapon that never speaks. Content only on a weapon that <see cref="Hungers"/>.
    /// </summary>
    public HungerVoice? Voice { get; init; }

    /// <summary>
    /// Marks a glass weapon (issue 702, obsidian): the sharpest edge sold, spent as it is used. It is
    /// never repaired and never Refined; the smith refuses it with <see cref="GlassRefusal"/>.
    /// </summary>
    public bool Glass { get; init; }

    /// <summary>
    /// Marks a frozen-iron weapon (issue 702 slice 2; <see cref="Frost"/>): a hit from it chills.
    /// Content may set it on any physical weapon that does not hunger; <see cref="Frost.Shape"/>
    /// also sets it on a woken heirloom and on a signature at its last rare Refine step.
    /// </summary>
    public bool FrozenIron { get; init; }

    /// <summary>
    /// The movement types this weapon crits more easily against (issue 703): <see cref="CritBonus"/>
    /// is added to its Crit when the target moves so. Bows carry it against fliers in place of the
    /// effective tag, so a crit lands a flier (<see cref="Grounding"/>) instead of killing it.
    /// </summary>
    public ValueList<MovementType> CritAgainst { get; init; }

    /// <summary>The Crit added against a target whose movement is in <see cref="CritAgainst"/> (issue 703).</summary>
    public int CritBonus { get; init; }

    /// <summary>The Crit this weapon adds against a target moving by <paramref name="movement"/>: <see cref="CritBonus"/> when listed, else 0.</summary>
    public int CritBonusAgainst(MovementType movement) => CritAgainst.Contains(movement) ? CritBonus : 0;

    /// <summary>
    /// Whether this weapon's crit against a target moving by <paramref name="movement"/> grounds it
    /// instead of tripling (issue 723, round 220): a bow against a flier, unless the bow is
    /// effective against fliers, as on an <c>effective_bows: on</c> sample (<see cref="Grounding.ForMap"/>).
    /// </summary>
    public bool GroundsAgainst(MovementType movement) =>
        Type == WeaponType.Bow && movement == MovementType.Flying && !IsEffectiveAgainst(movement);

    /// <summary>The smith's line when asked to repair or Refine a glass weapon (issue 702).</summary>
    public const string GlassRefusal = "You don't mend glass. You buy another.";

    /// <summary>The one line the item card prints for this weapon (issue 650); empty only in a weapon built outside the content files.</summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// The school of Lore magic this tome belongs to (DECISIONS/0296), or null for an unschooled
    /// tome and for every weapon that is not <see cref="WeaponType.Reason"/>. Only a unit whose
    /// class reaches the school may wield it (<see cref="Unit.CanWield"/>).
    /// </summary>
    public MagicSchool? School { get; init; }

    /// <summary>
    /// The rider this tome opts into (issue 1250, DECISIONS/0298), or null for a plain tome. The kind
    /// is named here; its amount and phases are the school's (<see cref="GameContent.RiderOf"/>), so a
    /// school's first tome stays plain while a found grimoire of the same school carries the rider.
    /// </summary>
    public RiderKind? Rider { get; init; }

    /// <summary>
    /// The least Mag a unit needs to wield this tome (issue 1246, a grimoire's gate), or null for
    /// none. It stands beside the rank requirement, never in its place (<see cref="Unit.CanWield"/>).
    /// </summary>
    public int? MinMag { get; init; }

    /// <summary>
    /// What an armor tome lays on its caster (issue 1282, <see cref="Core.Armor"/>), or null for any other weapon.
    /// Present exactly when <see cref="Rider"/> is <see cref="RiderKind.Armor"/>: Earth Armor and Obsidian Armor
    /// carry their own numbers, where every other rider takes its school's.
    /// </summary>
    public ArmorSpell? Armor { get; init; }

    public bool IsMagic => Type.IsMagic();

    public bool InRange(int distance) => distance >= MinRange && distance <= MaxRange;

    public bool IsEffectiveAgainst(MovementType movement) => EffectiveAgainst.Contains(movement);
}

/// <summary>
/// One stage an heirloom turns to (issue 646): its id (the art key and the word the card prints),
/// its numbers, the combats fought with it at which it turns (<paramref name="At"/>), and the one
/// line its card prints in place of the weapon's description.
/// </summary>
public sealed record WeaponStage(string Id, int Mt, int Hit, int Crit, int Wt, int At, string Description);

/// <summary>
/// An heirloom's ladder (issue 646): the campaign map, counted from 1, before which no stage turns
/// (<paramref name="FromMap"/>), the id of the first stage, whose numbers are the weapon's own
/// (<paramref name="First"/>), and the stages it turns to, in order, their thresholds rising.
/// </summary>
public sealed record HeirloomLadder(int FromMap, string First, ValueList<WeaponStage> Turns)
{
    /// <summary>
    /// The card's line while the last stage is held (issue 635, round 266), or null for a ladder
    /// with no gate. With it, the last stage turns only on a stack whose gate a won quest has
    /// opened (<see cref="ItemStack.GateOpen"/>, <see cref="CampaignQuest.Wakes"/>); the counter
    /// runs on regardless.
    /// </summary>
    public string? Held { get; init; }

    /// <summary>
    /// The weapon's true name once a won quest 2 names its stack (issue 635, rounds 268 to 270;
    /// <see cref="ItemStack.Named"/>, <see cref="CampaignQuest.Names"/>), or null for a ladder no
    /// quest names. The id never changes, so the save and the stages key on what they did.
    /// </summary>
    public string? Named { get; init; }
}

/// <summary>
/// One line a hungering weapon may say (issue 804 item 3): a stable id that is never reused, and its
/// text, a bark under WRITING.md (one line, at most <see cref="HungerVoice.WordsMax"/> words). The
/// token <c>{name}</c> stands for the carrier's name.
/// </summary>
public sealed record VoiceLine(string Id, string Text);

/// <summary>
/// A hungering weapon's voice (issue 804 item 3), in three registers: <paramref name="Starved"/>, said
/// when a drain starves it; <paramref name="Tooth"/>, said when a kill grows a tooth, the tooth's
/// number choosing the line; <paramref name="Woken"/>, said on the kill that wakes it, in place of
/// that kill's tooth line. A register may be empty, and then that moment is silent.
/// </summary>
public sealed record HungerVoice(ValueList<VoiceLine> Starved, ValueList<VoiceLine> Tooth, ValueList<VoiceLine> Woken)
{
    /// <summary>The most words a line may run: WRITING.md's budget for a bark.</summary>
    public const int WordsMax = 12;
}
