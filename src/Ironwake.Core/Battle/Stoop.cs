namespace Ironwake.Core;

/// <summary>
/// The Sky Captain's Stoop (issue 1127, DECISIONS/0262; Chat's proposal on Table #1112): every Sky Captain but a
/// drake's rider, Edda included. When the holder attacks after flying at least <see cref="StoopEffect.Flight"/>
/// tiles this phase, counted straight from the tile it took off from to the tile it strikes from
/// (<see cref="BattleUnit.FlewFrom"/>), its first strike of the combat deals <see cref="StoopEffect.Damage"/> more
/// when it hits, added after any crit. A counter never stoops. The forecast carries it as
/// <see cref="SideForecast.Stoop"/>, so the screen prints <c>Stoop +2</c>.
/// </summary>
public static class Stoop
{
    /// <summary>What <paramref name="unit"/>'s first strike adds where it stands now, 0 when it does not stoop.</summary>
    public static int Bonus(GameContent content, BattleUnit unit) =>
        AbilityRules.Stoop(content.AbilitiesOf(unit.Unit), unit.Unit) is { } stoop && unit.FlewFrom is { } from && from.DistanceTo(unit.At) >= stoop.Flight
            ? stoop.Damage
            : 0;

    /// <summary>
    /// <paramref name="unit"/> as it would attack from <paramref name="from"/>: there, and, when it has not moved this
    /// phase and would fly there, taken off from where it stands, so a forecast before the move reads the dive.
    /// </summary>
    public static BattleUnit Poised(GameContent content, BattleUnit unit, Coord from) =>
        unit.Moved || from == unit.At
            ? unit with { At = from }
            : unit with { At = from, FlewFrom = Grounding.MovementOf(unit, content) == MovementType.Flying ? unit.At : null };
}
