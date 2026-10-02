namespace Ironwake.Core;

/// <summary>
/// A class from DESIGN.md section 3: movement type, Mov, stat modifiers applied while
/// in the class, usable weapon types, and growth modifiers. <see cref="Mastery"/> names the
/// class's mastery ability in <c>abilities.json</c>, or null, and <see cref="MasteryPoints"/>
/// the combats fought in the class that earn it (issue 69, <see cref="Masteries"/>).
/// <see cref="Abilities"/> are held while in the class and lost on leaving it, as Canto on
/// the cavalry classes is (issue 71).
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

    /// <summary>Ability ids every unit in the class holds while it is in the class, in content order.</summary>
    public ValueList<string> Abilities { get; init; } = ValueList<string>.Empty;

    /// <summary>What a unit needs to certify into the class (issue 72, <see cref="Certifications"/>).</summary>
    public CertificationRequirements Certification { get; init; } = CertificationRequirements.None;

    /// <summary>
    /// Whether the class is earned rather than certified (issue 691): nobody certifies or trials
    /// into it, the screen's class list leaves it out, and a side map's win is the only way in
    /// (<see cref="CampaignQuest.Promotes"/>).
    /// </summary>
    public bool Hidden { get; init; }

    /// <summary>
    /// The class this one is the advanced form of (issue 704, DESIGN section 3), or null for a
    /// first-tier class. Only a unit in that class may be promoted into this one; the form keeps
    /// every weapon type of its base and its growths are the base's.
    /// </summary>
    public UnitClass? Advances { get; init; }

    /// <summary>The first-tier class this one belongs to: <see cref="Advances"/>'s id for an advanced form, else its own.</summary>
    public string BaseId => Advances?.Id ?? Id;

    public bool CanUse(WeaponType type) => Weapons.Contains(type);
}
