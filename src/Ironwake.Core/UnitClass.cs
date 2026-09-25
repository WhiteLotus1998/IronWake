namespace Ironwake.Core;

/// <summary>
/// A class from DESIGN.md section 3: movement type, Mov, stat modifiers applied while
/// in the class, usable weapon types, and growth modifiers. <see cref="Mastery"/> names the
/// class's mastery ability in <c>abilities.json</c>, or null, and <see cref="MasteryPoints"/>
/// the combats fought in the class that earn it (issue 69, <see cref="Masteries"/>).
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

    /// <summary>The mastery points that earn <see cref="Mastery"/>; content names both or neither, and 0 when there is none.</summary>
    public int MasteryPoints { get; init; }

    public bool CanUse(WeaponType type) => Weapons.Contains(type);
}
