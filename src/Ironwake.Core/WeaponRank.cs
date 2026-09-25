namespace Ironwake.Core;

/// <summary>Weapon skill ranks from DESIGN.md section 12 (issue 67), lowest first, so ranks compare as their order.</summary>
public enum WeaponRank
{
    E,
    D,
    C,
    B,
    A,
    S,
}

/// <summary>
/// The rank table of issue 67: a unit earns rank points per combat in which it used a
/// weapon, more for a kill, and its rank in a weapon type is the highest threshold its
/// points reach. Deterministic, never a roll: a rank that arrived a combat earlier in one
/// arm of gate 4's ablation than in the other would reshuffle what the unit can equip for
/// the rest of the run, the argument that keys growth rolls on (unit, level, stat) alone.
/// </summary>
public static class WeaponRanks
{
    /// <summary>Points for a combat in which the unit struck with the weapon, or a heal for Faith.</summary>
    public const int PerCombat = 3;

    /// <summary>Points for a combat in which the unit's strike killed.</summary>
    public const int PerKill = 5;

    /// <summary>The points at which each rank begins, E first.</summary>
    public static IReadOnlyList<int> Thresholds { get; } = [0, 30, 80, 160, 280, 450];

    /// <summary>One award per combat from its best outcome, like EXP in section 6.</summary>
    public static int ForCombat(bool killed) => killed ? PerKill : PerCombat;

    /// <summary>The rank a point total reaches.</summary>
    public static WeaponRank RankAt(int points)
    {
        if (points < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), points, "rank points must be at least 0");
        }

        var rank = WeaponRank.E;
        for (var i = 1; i < Thresholds.Count; i++)
        {
            if (points >= Thresholds[i])
            {
                rank = (WeaponRank)i;
            }
        }

        return rank;
    }

    /// <summary>The fewest points that reach <paramref name="rank"/>.</summary>
    public static int Threshold(WeaponRank rank) => Thresholds[(int)rank];
}

/// <summary>
/// A unit's rank points in every weapon type (issue 67), indexed by <see cref="WeaponType"/>.
/// Value-equal, so two units built from the same data compare equal.
/// </summary>
public sealed record WeaponSkill
{
    private static readonly int TypeCount = Enum.GetValues<WeaponType>().Length;

    private readonly ValueList<int> _points;

    private WeaponSkill(ValueList<int> points) => _points = points;

    /// <summary>Rank E in everything: the skill of a unit whose content declares no ranks.</summary>
    public static WeaponSkill Zero { get; } = new(ValueList<int>.From(new int[TypeCount]));

    /// <summary>A skill with the given points per type, every type not named at 0.</summary>
    public static WeaponSkill From(IEnumerable<KeyValuePair<WeaponType, int>> points)
    {
        var skill = Zero;
        foreach (var (type, value) in points)
        {
            skill = skill.With(type, value);
        }

        return skill;
    }

    public int Points(WeaponType type) => _points[(int)type];

    public WeaponRank Rank(WeaponType type) => WeaponRanks.RankAt(Points(type));

    /// <summary>This skill with <paramref name="type"/> set to <paramref name="points"/>, which must be at least 0.</summary>
    public WeaponSkill With(WeaponType type, int points)
    {
        if (points < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), points, "rank points must be at least 0");
        }

        return new(_points.SetItem((int)type, points));
    }

    public WeaponSkill Add(WeaponType type, int points) => With(type, Points(type) + points);

    /// <summary>Every type with its points, in <see cref="WeaponType"/> order.</summary>
    public IEnumerable<(WeaponType Type, int Points)> All =>
        Enum.GetValues<WeaponType>().Select(type => (type, Points(type)));

    public bool Equals(WeaponSkill? other) => other is not null && _points.Equals(other._points);

    public override int GetHashCode() => _points.GetHashCode();
}
