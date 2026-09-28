namespace Ironwake.Core;

/// <summary>
/// The windup (DESIGN.md 13.16, experiment), behind a map's <c>windup: on</c> header. An attack
/// with a windup weapon (<see cref="Weapon.Windup"/>) does not fight: it raises a blow over the
/// target's tile and ends the unit's action, with no roll, no counter and no use spent. At the
/// start of the wielder's side's next phase, after heal and burn, the blow lands on whatever unit
/// stands on that tile, of either side, the wielder excepted: one strike, a certain hit, no crit,
/// no counter, for the damage a normal hit from the wielder would deal to that unit on that tile.
/// An empty tile takes nothing, and either way the blow is spent. A hit on the wielder before
/// then breaks the blow; a miss does not. The wielder counters with its weapon as usual. The
/// pending tile is <see cref="BattleUnit.WindupAt"/>, so Recall restores it with the unit.
/// </summary>
public static class Windup
{
    /// <summary>Whether an attack with <paramref name="weapon"/> raises a blow instead of fighting on this map.</summary>
    public static bool Raises(BattleState state, Weapon? weapon) =>
        state.Map.WindupEnabled && weapon is { Windup: true };

    /// <summary>The tiles under a raised blow, row-major. Empty on a map without the header.</summary>
    public static IReadOnlyList<Coord> Marked(BattleState state) =>
        !state.Map.WindupEnabled
            ? Array.Empty<Coord>()
            : state.Units.Where(u => u.WindupAt is not null).Select(u => u.WindupAt!.Value).Distinct().OrderBy(c => c).ToList();

    /// <summary>The living unit whose raised blow will land on <paramref name="at"/>, if any, the first in unit order.</summary>
    public static BattleUnit? Over(BattleState state, Coord at) =>
        state.Units.FirstOrDefault(u => u.WindupAt == at);

    /// <summary>
    /// After a combat: every unit still living with a raised blow that took a hit in
    /// <paramref name="strikes"/> has the blow broken, one <see cref="BlowBroken"/> each.
    /// </summary>
    public static BattleState AfterCombat(BattleState state, ValueList<StrikeEvent> strikes, List<GameEvent> events)
    {
        foreach (var id in strikes.Where(s => s.Hit).Select(s => s.TargetId).Distinct().ToList())
        {
            if (state.Find(id) is { WindupAt: { } at } unit)
            {
                events.Add(new BlowBroken(unit.Id, at));
                state = state.WithUnit(unit with { WindupAt = null });
            }
        }

        return state;
    }

    /// <summary>
    /// The damage a raised blow from <paramref name="wielder"/> deals to <paramref name="target"/>
    /// standing where it stands: what one normal hit would, read from the section 5 forecast at
    /// the two units' distance (the weapon's shortest reach if the wielder no longer reaches the
    /// tile), so terrain Def and weapon effectiveness count and crit does not.
    /// </summary>
    public static int Damage(BattleState state, GameContent content, BattleUnit wielder, BattleUnit target)
    {
        var striker = wielder.ToCombatant(state, content, against: target);
        var distance = wielder.At.DistanceTo(target.At);
        if (wielder.EquippedWeapon(content) is { } weapon && !weapon.InRange(distance))
        {
            distance = weapon.MinRange;
        }

        var forecast = Combat.Forecast(striker, target.Answering(state, content, wielder.At, wielder), distance, state.Scheme);
        return forecast.Attacker.Damage;
    }
}
