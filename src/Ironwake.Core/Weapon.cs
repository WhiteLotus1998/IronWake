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

    /// <summary>The one line the item card prints for this weapon (issue 650); empty only in a weapon built outside the content files.</summary>
    public string Description { get; init; } = "";

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
public sealed record HeirloomLadder(int FromMap, string First, ValueList<WeaponStage> Turns);
