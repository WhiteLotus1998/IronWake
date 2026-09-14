namespace Ironwake.Core;

/// <summary>
/// A class from DESIGN.md section 3: movement type, Mov, stat modifiers applied while
/// in the class, usable weapon types, and growth modifiers. The mastery ability is Phase 3.
/// </summary>
public sealed record UnitClass(
    string Id,
    string Name,
    MovementType Movement,
    int Mov,
    Stats Modifiers,
    ValueList<WeaponType> Weapons,
    Stats GrowthModifiers)
{
    public bool CanUse(WeaponType type) => Weapons.Contains(type);
}
