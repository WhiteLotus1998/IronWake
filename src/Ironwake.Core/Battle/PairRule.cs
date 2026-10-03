namespace Ironwake.Core;

/// <summary>
/// The pair rule (issue 692, the keep finale's fourth slice; DECISIONS/0150, provisional): on a map
/// whose <c>pair_rule:</c> header names enemy groups, an enemy in one of them strikes a unit that has
/// an ally orthogonally beside it once, never doubling and never critting, on its strike and on its
/// counter alike. With one plain strike of the finale's strongest units held below every full-HP
/// unit's HP (a content test), a unit standing in a pair cannot be killed by one of them in one
/// combat; a unit standing alone can, on the double or a crit. It sits on <see cref="Combatant.PairHeld"/>, so every forecast, <c>threat</c>, the
/// planner's score and the resolver read one number. Units are matched by id, so a forecast may pass
/// the target at a tile it has not moved to; the ally is read where it stands on the board.
/// </summary>
public static class PairRule
{
    /// <summary>Whether <paramref name="unit"/> is bound by the pair rule here: an enemy whose group the map's header names.</summary>
    public static bool Binds(MapDefinition map, BattleUnit unit) =>
        unit.Side == Side.Enemy && unit.Group is { } group && map.PairRuleGroups.Contains(group);

    /// <summary>
    /// Whether <paramref name="striker"/> is held (one strike, no crit) against <paramref name="target"/> on this
    /// board: the striker is bound, the target is a foe, and a living unit of the target's side other
    /// than the target stands orthogonally adjacent to the target's tile. False when the target is unknown.
    /// </summary>
    public static bool Holds(BattleState state, BattleUnit striker, BattleUnit? target) =>
        target is not null
        && target.Side != striker.Side
        && Binds(state.Map, striker)
        && state.Units.Any(u => u.Side == target.Side && u.Id != target.Id && u.Id != striker.Id && u.At.DistanceTo(target.At) == 1);

    /// <summary>The rule as the board and <c>help</c> print it on a map with the header, or null without one.</summary>
    public static string? Rule(MapDefinition map) =>
        map.PairRuleGroups.Count == 0
            ? null
            : $"Pair rule: an enemy of the {string.Join(" or ", map.PairRuleGroups.Select(g => g.Replace('_', ' ')))} group strikes a unit with an ally beside it once, never doubling, never critting.";

    /// <summary>
    /// The board's line naming the bound enemies standing now, by the names a reader sees:
    /// <c>pair rule binds: Hask, Sworn Hunter</c>. Null when none stands or the map has no header.
    /// </summary>
    public static string? Line(BattleState state, UnitNames names)
    {
        var bound = state.UnitsOf(Side.Enemy).Where(u => Binds(state.Map, u)).Select(u => names[u.Id]).ToList();
        return bound.Count == 0 ? null : "pair rule binds: " + string.Join(", ", bound);
    }
}
