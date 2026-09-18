namespace Ironwake.Core;

/// <summary>
/// The questions a renderer asks the core instead of counting for itself (DESIGN.md
/// section 2; the presentation protocol of issue 25 exposes these by name): where a unit
/// can move, whom it can attack from where it stands, and what a combat would look like.
/// The CLI computes none of this.
/// </summary>
public static class Queries
{
    /// <summary>Where the unit may move, section 4, on the board as it stands.</summary>
    public static Reach Reachable(BattleState state, GameContent content, BattleUnit unit) => state.ReachOf(unit, content);

    /// <summary>The enemy units the unit's equipped weapon reaches from where it stands, in id order. Empty when it has no weapon.</summary>
    public static IEnumerable<BattleUnit> Targets(BattleState state, GameContent content, BattleUnit unit)
    {
        var weapon = unit.EquippedWeapon(content);
        if (weapon is null)
        {
            yield break;
        }

        foreach (var other in state.UnitsOf(unit.Side == Side.Player ? Side.Enemy : Side.Player))
        {
            if (weapon.InRange(unit.At.DistanceTo(other.At)))
            {
                yield return other;
            }
        }
    }

    /// <summary>
    /// The section 5 forecast of the unit attacking the target from where it stands with
    /// its equipped weapon, or with the weapon in <paramref name="slot"/>; null when it
    /// cannot (no weapon, a slot that holds no usable weapon, the target out of range or
    /// on its own side), the same combatants the resolver would build.
    /// </summary>
    public static CombatForecast? Forecast(BattleState state, GameContent content, BattleUnit unit, BattleUnit target, int? slot = null)
    {
        var (armed, weapon, rejection) = Resolver.ChooseWeapon(unit, content, slot);
        var distance = unit.At.DistanceTo(target.At);
        if (rejection is not null || !weapon!.InRange(distance) || target.Side == unit.Side)
        {
            return null;
        }

        return Combat.Forecast(armed.ToCombatant(state.Map, content), target.ToCombatant(state.Map, content), distance, state.Scheme);
    }
}
