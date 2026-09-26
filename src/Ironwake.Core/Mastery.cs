namespace Ironwake.Core;

/// <summary>
/// Class mastery (issue 69, DESIGN section 3): a class that names a mastery ability also
/// names the points that earn it, and a player unit earns one point per combat it fights
/// in the class and per healing spell it casts on an ally in the class (issue 245). Deterministic, never a roll, like ranks (issue 67). On reaching the
/// requirement the ability joins the unit's own abilities, so it stays when the unit
/// leaves the class.
/// </summary>
public static class Masteries
{
    /// <summary>Points for a combat fought in the class, as attacker or defender, or for a heal cast in it (issue 245).</summary>
    public const int PerCombat = 1;

    /// <summary>
    /// <paramref name="unit"/> after one combat in <paramref name="unitClass"/>, its own
    /// class, and the ability it mastered by it, or null. A class that names no mastery,
    /// or one the unit already holds, earns nothing and returns the unit unchanged.
    /// </summary>
    public static (Unit Unit, string? Mastered) ForCombat(Unit unit, UnitClass unitClass)
    {
        if (unitClass.Id != unit.ClassId)
        {
            throw new ArgumentException(
                $"class must be the unit's own: {unit.Id} is a {unit.ClassId}, not a {unitClass.Id}", nameof(unitClass));
        }

        if (unitClass.Mastery is not { } ability || unit.Abilities.Contains(ability))
        {
            return (unit, null);
        }

        var points = unit.Mastery.Points(unitClass.Id) + PerCombat;
        var progress = unit.Mastery.With(unitClass.Id, points);
        return points >= unitClass.MasteryPoints
            ? (unit with { Mastery = progress, Abilities = unit.Abilities.Add(ability) }, ability)
            : (unit with { Mastery = progress }, null);
    }
}

/// <summary>
/// A unit's mastery points per class id (issue 69), kept for every class it has fought
/// in so a unit returning to a class resumes its count. Ordered by class id and
/// value-equal, so two units built from the same data compare equal.
/// </summary>
public sealed record MasteryProgress
{
    private readonly ValueList<(string ClassId, int Points)> _points;

    private MasteryProgress(ValueList<(string ClassId, int Points)> points) => _points = points;

    /// <summary>No points in any class.</summary>
    public static MasteryProgress Empty { get; } = new(ValueList<(string, int)>.Empty);

    public int Points(string classId)
    {
        foreach (var (id, points) in _points)
        {
            if (id == classId)
            {
                return points;
            }
        }

        return 0;
    }

    /// <summary>This progress with <paramref name="classId"/> at <paramref name="points"/>, which must be at least 0; a class at 0 is not kept.</summary>
    public MasteryProgress With(string classId, int points)
    {
        if (points < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), points, "mastery points must be at least 0");
        }

        var rest = _points.Where(p => p.ClassId != classId);
        IEnumerable<(string ClassId, int Points)> next = points == 0 ? rest : rest.Append((classId, points));
        return new(ValueList<(string, int)>.From(next.OrderBy(p => p.ClassId, StringComparer.Ordinal)));
    }

    /// <summary>Every class with points, ordered by class id.</summary>
    public IEnumerable<(string ClassId, int Points)> All => _points;

    public bool Equals(MasteryProgress? other) => other is not null && _points.Equals(other._points);

    public override int GetHashCode() => _points.GetHashCode();
}
