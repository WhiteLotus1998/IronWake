namespace Ironwake.Core;

/// <summary>
/// A weapon or spell from DESIGN.md section 5. For Reason and Faith, <see cref="Durability"/>
/// is uses per battle and refreshes each map; for physical weapons it is total uses before
/// the weapon breaks. A Faith weapon with <see cref="Heals"/> set is a healing spell:
/// its Mt is unused and <see cref="HealBase"/> feeds the heal formula. <see cref="Rank"/> is
/// the weapon skill rank a unit needs to equip it (issue 67). <see cref="Price"/> is what the
/// between-map shop charges for one at full uses (issue 74); a weapon without one is never sold
/// and never repaired.
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
    int? Price = null)
{
    public bool IsMagic => Type.IsMagic();

    public bool InRange(int distance) => distance >= MinRange && distance <= MaxRange;

    public bool IsEffectiveAgainst(MovementType movement) => EffectiveAgainst.Contains(movement);
}
