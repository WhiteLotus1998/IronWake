namespace Ironwake.Core;

/// <summary>
/// A school of Lore magic (DECISIONS/0296, after 0264 and Lotus's schools ruling): a tag on a
/// <see cref="WeaponType.Reason"/> tome, <see cref="Weapon.School"/>. A class reaches a school by
/// naming it in <see cref="UnitClass.Schools"/>, and only a unit whose class reaches a tome's school
/// may wield it (<see cref="Unit.CanWield"/>). An unschooled tome is wielded as any Lore tome is.
/// Faith and the dark grimoire are not schools.
/// </summary>
public enum MagicSchool
{
    Fire,
    Ice,
    Lightning,
    Earth,
}

public static class MagicSchoolExtensions
{
    /// <summary>The word a reader sees and content writes: the id's own word, lower case.</summary>
    public static string Label(this MagicSchool school) => school.ToString().ToLowerInvariant();

    /// <summary>
    /// Why <paramref name="unitClass"/> cannot wield <paramref name="weapon"/> for want of its school,
    /// <c>needs the fire school; an adept reaches ice, lightning</c>, or null when the weapon is
    /// unschooled or the class reaches it.
    /// </summary>
    public static string? SchoolShort(UnitClass unitClass, Weapon weapon) =>
        weapon.School is not { } school || unitClass.Reaches(school)
            ? null
            : $"needs the {school.Label()} school; {Article(unitClass.Name)} {unitClass.Name} reaches "
              + (unitClass.Schools.Count == 0 ? "none" : string.Join(", ", unitClass.Schools.Select(s => s.Label())));

    private static string Article(string name) => "AEIOUaeiou".Contains(name[0]) ? "an" : "a";
}
