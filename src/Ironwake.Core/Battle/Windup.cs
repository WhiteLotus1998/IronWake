namespace Ironwake.Core;

/// <summary>
/// The windup (DESIGN.md 13.16, experiment), behind a map's <c>windup: on</c> header. An attack
/// with a windup weapon (<see cref="Weapon.Windup"/>) does not fight: it raises a blow over the
/// target's tile and ends the unit's action, with no roll, no counter and no use spent. At the
/// start of the wielder's side's next phase, after heal and burn, the blow lands on whatever unit
/// stands on that tile, of either side, the wielder excepted: one strike, a certain hit, no crit,
/// no counter, for the damage a normal hit from the wielder would deal to that unit on that tile.
/// An empty tile takes nothing, and either way the blow is spent. A hit on the wielder before
/// then from within its weapon's range breaks the blow; a miss, or a hit from farther, does not. The wielder counters with its weapon as usual. The
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
    /// Whether a hit from <paramref name="from"/> on <paramref name="wielder"/> breaks its raised
    /// blow: only a hit the wielder could counter does (rounds 98 and 99), one struck from within
    /// its weapon's range, so every break costs an exchange. False when no blow is raised.
    /// </summary>
    public static bool Breaks(GameContent content, BattleUnit wielder, Coord from) =>
        wielder.WindupAt is not null && wielder.EquippedWeapon(content) is { } weapon && weapon.InRange(wielder.At.DistanceTo(from));

    /// <summary>
    /// After a combat between <paramref name="a"/> and <paramref name="b"/>, read where they stood:
    /// each one still living with a raised blow that the other hit from within its weapon's range
    /// (<see cref="Breaks"/>) has the blow broken, one <see cref="BlowBroken"/> each. A miss, or a
    /// hit from outside that range, leaves the blow raised.
    /// </summary>
    public static BattleState AfterCombat(BattleState state, GameContent content, BattleUnit a, BattleUnit b, ValueList<StrikeEvent> strikes, List<GameEvent> events)
    {
        foreach (var (struck, striker) in new[] { (a, b), (b, a) })
        {
            if (!strikes.Any(s => s.TargetId == struck.Id && s.AttackerId == striker.Id && s.Hit) || !Breaks(content, struck, striker.At))
            {
                continue;
            }

            if (state.Find(struck.Id) is { WindupAt: { } at } living)
            {
                events.Add(new BlowBroken(living.Id, at));
                state = state.WithUnit(living with { WindupAt = null });
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
