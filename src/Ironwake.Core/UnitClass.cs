namespace Ironwake.Core;

/// <summary>
/// A class from DESIGN.md section 3: movement type, Mov, stat modifiers applied while
/// in the class, usable weapon types, and growth modifiers. <see cref="Mastery"/> names the
/// class's mastery ability in <c>abilities.json</c>, or null; how a unit earns it is the
/// mastery issue's (issue 69), so naming it here grants nothing.
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
    public string? Mastery { get; init; }

    public bool CanUse(WeaponType type) => Weapons.Contains(type);
}
