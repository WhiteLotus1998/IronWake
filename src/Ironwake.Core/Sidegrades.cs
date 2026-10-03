namespace Ironwake.Core;

/// <summary>
/// The sidegrade test (issue 706, DESIGN section 3): a unique class names the measure it loses on
/// to its unit's standard advanced form (<see cref="UnitClass.Loses"/>), and this reads that measure
/// for a unit in either class, so a test can hold the loss.
/// </summary>
public static class Sidegrades
{
    /// <summary>
    /// <paramref name="measure"/> for <paramref name="unit"/> in <paramref name="unitClass"/>, read on the
    /// content's weapons as the unit's abilities in that class shape them. <see cref="SidegradeMeasure.Reach"/>
    /// is the farthest tile any healing spell the unit may cast in the class reaches, 0 when it casts none;
    /// <see cref="SidegradeMeasure.Damage"/> the most any weapon it may strike with in the class adds to
    /// its Str (Mag for a spell) at its level, before the target's defence, 0 when it strikes with none;
    /// <see cref="SidegradeMeasure.Doubling"/> the strikes one combat may make, 1 in a single-strike class, else 2.
    /// </summary>
    public static int Measure(Unit unit, UnitClass unitClass, GameContent content, SidegradeMeasure measure)
    {
        var inClass = unit with { ClassId = unitClass.Id };
        return measure switch
        {
            SidegradeMeasure.Reach => content.Weapons.Values
                .Where(w => w.Heals && inClass.CanWield(w, unitClass))
                .Select(w => content.WeaponOf(inClass, w).MaxRange)
                .DefaultIfEmpty(0)
                .Max(),
            SidegradeMeasure.Damage => content.Weapons.Values
                .Where(w => !w.Heals && inClass.CanWield(w, unitClass))
                .Select(w => (w.IsMagic ? content.StatsOf(inClass).Mag : content.StatsOf(inClass).Str) + content.WeaponOf(inClass, w).Mt)
                .DefaultIfEmpty(0)
                .Max(),
            SidegradeMeasure.Doubling => unitClass.SingleStrike ? 1 : 2,
            _ => throw new ArgumentOutOfRangeException(nameof(measure), measure, "no such measure"),
        };
    }
}
