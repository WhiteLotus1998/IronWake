namespace Ironwake.Core;

/// <summary>
/// A unit's duty at a camp (issue 1331, Lotus's yard, round 433): between maps each unit takes
/// one. A unit no command names rests.
/// </summary>
public enum Duty
{
    Rest,
    Quest,
    Forge,
    Yard,
}

/// <summary>The duty <paramref name="UnitId"/> took at this camp (issue 1331).</summary>
public sealed record UnitDuty(string UnitId, Duty Duty);

/// <summary>
/// A unit's part in a yard drill (issue 1331). The teacher (<paramref name="Teaches"/>) earns no
/// EXP, no rank points and no mastery from it. The student takes no level past
/// <paramref name="LevelCeiling"/> and no rank in <paramref name="Weapon"/> past
/// <paramref name="RankCeiling"/>; the EXP and points it earns past them are kept short of the
/// next step (<see cref="YardRules.CapPoints"/>).
/// </summary>
public sealed record YardHand(bool Teaches, int LevelCeiling, WeaponType Weapon, WeaponRank RankCeiling)
{
    /// <summary>
    /// Whether a teacher's blows pull (Table round 448): on in every drill and never a player
    /// option. Only the Sim's ablation arm sets it off, to measure what the pull is worth.
    /// </summary>
    public bool Pulls { get; init; } = true;
}

/// <summary>
/// The yard's rules (issue 1331): a teacher and a student both spend their duty on a short real
/// fight; the cap is the teacher.
/// </summary>
public static class YardRules
{
    /// <summary>The highest level a student trains to under <paramref name="teacher"/>: the teacher's level less one.</summary>
    public static int LevelCeiling(Unit teacher) => Math.Max(Unit.MinLevel, teacher.Level - 1);

    /// <summary>The highest rank in <paramref name="weapon"/> a student trains to under <paramref name="teacher"/>: the teacher's own.</summary>
    public static WeaponRank RankCeiling(Unit teacher, WeaponType weapon) => teacher.Skill.Rank(weapon);

    /// <summary>
    /// <paramref name="points"/> in the taught weapon held under the rank above
    /// <paramref name="ceiling"/>: one short of its threshold, unchanged when the ceiling is the top rank.
    /// </summary>
    public static int CapPoints(int points, WeaponRank ceiling) =>
        ceiling == WeaponRank.S ? points : Math.Min(points, WeaponRanks.Threshold(ceiling + 1) - 1);

    /// <summary>The student's and the teacher's hands for a drill in <paramref name="weapon"/> under <paramref name="teacher"/>.</summary>
    public static (YardHand Student, YardHand Teacher) Hands(Unit teacher, WeaponType weapon)
    {
        var student = new YardHand(false, LevelCeiling(teacher), weapon, RankCeiling(teacher, weapon));
        return (student, student with { Teaches = true });
    }

    /// <summary>The weapon type a <c>yard</c> command names, by its lower-case name, or null.</summary>
    public static WeaponType? Weapon(string name) =>
        Enum.GetValues<WeaponType>().Cast<WeaponType?>().FirstOrDefault(t => string.Equals(t.ToString(), name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The word a duty prints as: <c>rest</c>, <c>quest</c>, <c>forge</c>, <c>yard</c>.</summary>
    public static string Word(Duty duty) => duty.ToString().ToLowerInvariant();

    /// <summary>The duty a word names, or null.</summary>
    public static Duty? Parse(string word) =>
        Enum.GetValues<Duty>().Cast<Duty?>().FirstOrDefault(d => Word(d!.Value) == word);
}
