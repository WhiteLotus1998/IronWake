namespace Ironwake.Core;

/// <summary>
/// Grounding (issue 703, Lotus's batch item 5, round 216; issue 723, round 220; DESIGN section 5).
/// A bow's crit on a flying unit deals plain damage, not triple (<see cref="SideForecast.CritDamage"/>,
/// <see cref="Weapon.GroundsAgainst"/>), and a flier that survives the combat is landed until the end
/// of its side's next phase (<see cref="BattleUnit.Grounded"/>). Both sides' bows do it.
/// <list type="bullet">
/// <item>While grounded it moves with infantry costs and infantry passability (<see cref="MovementOf"/>);
/// its terrain bonuses stay a flier's, since it is still a flier struck where it fell.</item>
/// <item>On a tile infantry cannot enter (water, a chasm) it cannot move at all, but still acts from
/// where it is (<see cref="Stranded"/>).</item>
/// <item>It does not stack: a second crit refreshes the clock.</item>
/// </list>
/// The clock counts as the chill's does (<see cref="Frost.AtPhaseChange"/>). Everything is board
/// state, so Recall restores it with the board. Bows trade the effective tag for
/// <see cref="Weapon.CritBonus"/> against fliers, so the crit lands the flier instead of killing it.
/// A crit that does not ground (any other weapon, or any weapon on a non-flier) still triples.
/// </summary>
public static class Grounding
{
    /// <summary>Whether a crit from <paramref name="weapon"/> grounds a unit moving by <paramref name="movement"/> on <paramref name="map"/>: a bow against a flier, unless the map keeps the old bows.</summary>
    public static bool Grounds(MapDefinition map, Weapon? weapon, MovementType movement) =>
        !map.EffectiveBows && weapon is { Type: WeaponType.Bow } && movement == MovementType.Flying;

    /// <summary>
    /// <paramref name="weapon"/> as <paramref name="map"/> fights it: on an <c>effective_bows: on</c>
    /// sample (<see cref="MapDefinition.EffectiveBows"/>) a bow is effective against fliers and carries
    /// no crit bonus, the rule before issue 703; anywhere else the weapon unchanged.
    /// </summary>
    public static Weapon? ForMap(MapDefinition map, Weapon? weapon) =>
        map.EffectiveBows && weapon is { Type: WeaponType.Bow } bow
            ? bow with { EffectiveAgainst = ValueList<MovementType>.Of(MovementType.Flying), CritAgainst = ValueList<MovementType>.Empty, CritBonus = 0 }
            : weapon;

    /// <summary>How <paramref name="unit"/> moves on the board now: on foot while grounded, else by its class.</summary>
    public static MovementType MovementOf(BattleUnit unit, GameContent content)
    {
        var movement = content.Class(unit.Unit.ClassId).Movement;
        return unit.Grounded > 0 && movement == MovementType.Flying ? MovementType.Infantry : movement;
    }

    /// <summary>Whether <paramref name="unit"/> is grounded on a tile it could not walk onto, so it has no move this phase.</summary>
    public static bool Stranded(BattleState state, BattleUnit unit, GameContent content) =>
        unit.Grounded > 0
        && content.Class(unit.Unit.ClassId).Movement == MovementType.Flying
        && !state.Map.TerrainAt(unit.At, content).IsPassable(MovementType.Infantry);

    /// <summary>
    /// After a combat or a strike: each flier in <paramref name="strikes"/> still standing that a
    /// crit from a bow landed on is grounded (<see cref="UnitGrounded"/>), its clock set to 1.
    /// <paramref name="aWeapon"/> is what <paramref name="aId"/> struck with and
    /// <paramref name="bWeapon"/> what <paramref name="bId"/> did; a null weapon never grounds.
    /// </summary>
    public static BattleState AfterCombat(BattleState state, GameContent content, string aId, Weapon? aWeapon, string bId, Weapon? bWeapon, ValueList<StrikeEvent> strikes, List<GameEvent> events)
    {
        foreach (var (strikerId, weapon, targetId) in new[] { (aId, aWeapon, bId), (bId, bWeapon, aId) })
        {
            if (state.Find(targetId) is not { } target
                || !Grounds(state.Map, weapon, content.Class(target.Unit.ClassId).Movement)
                || !strikes.Any(s => s.AttackerId == strikerId && s.TargetId == targetId && s.Hit && s.Crit))
            {
                continue;
            }

            events.Add(new UnitGrounded(target.Id, strikerId, target.Side, target.Side == state.Phase));
            state = state.WithUnit(target with { Grounded = 1 });
        }

        return state;
    }

    /// <summary>
    /// The unit card's line for a grounded unit in <paramref name="state"/>: <c>grounded until enemy
    /// phase ends</c>, or <c>until the next enemy phase ends</c> while its clock has not begun on its
    /// own side's phase; null when it is not grounded.
    /// </summary>
    public static string? CardLine(BattleState state, BattleUnit unit) =>
        unit.Grounded > 0 ? $"grounded until {Frost.Until(unit.Side, unit.Grounded == 1 && unit.Side == state.Phase)}" : null;
}
