namespace Ironwake.Core;

/// <summary>
/// Enemy retreat (DESIGN.md 13.10, issue 33), one predicate the planner and the resolver
/// share so they cannot disagree. On a map with <see cref="MapDefinition.RetreatEnabled"/>,
/// an enemy whose effective behavior is Aggressive, below 30 percent HP, that has neither
/// moved this phase nor retreated this battle, and is not already standing on healing
/// terrain, falls back to a healing tile (Fort or Throne) in its reach this phase that no
/// player unit can strike next phase, instead of acting, and heals there by the tile rule
/// at its next phase start. With no such tile it stands and fights, so the dying enemy's
/// last swing survives on every board where retreat cannot pose the chase question
/// (issue 204, DECISIONS/0037 as amended). No flights over several turns: the turn limit
/// stays the map's pressure.
/// </summary>
public static class RetreatRule
{
    /// <summary>A unit strictly below this percent of its max HP may retreat.</summary>
    public const int ThresholdPercent = 30;

    /// <summary>Whether the unit is strictly below <see cref="ThresholdPercent"/> of its max HP, in integers.</summary>
    public static bool IsBelowThreshold(BattleUnit unit, GameContent content) =>
        unit.Hp * 100 < unit.MaxHp(content) * ThresholdPercent;

    /// <summary>
    /// Why the unit may not retreat now, or null when it may (the tile aside). The sentence
    /// is the resolver's rejection message.
    /// </summary>
    public static string? Refusal(BattleState state, GameContent content, BattleUnit unit)
    {
        if (!state.Map.RetreatEnabled)
        {
            return "this map has no retreat";
        }

        if (unit.Side != Side.Enemy)
        {
            return $"{unit.Id} is a player unit; only enemies retreat";
        }

        if (state.EffectiveBehavior(unit) != Behavior.Aggressive)
        {
            return $"{unit.Id} does not move on its own, so it does not retreat";
        }

        if (unit.Retreated)
        {
            return $"{unit.Id} has already retreated once";
        }

        if (unit.Moved)
        {
            return $"{unit.Id} has already moved this phase";
        }

        if (!IsBelowThreshold(unit, content))
        {
            return $"{unit.Id} is at {unit.Hp} of {unit.MaxHp(content)}, not below {ThresholdPercent} percent";
        }

        if (state.Map.TerrainAt(unit.At, content).HealPercent > 0)
        {
            return $"{unit.Id} already stands on healing terrain at {unit.At}; it fights from there";
        }

        return null;
    }

    /// <summary>
    /// The healing tiles the unit can end on this phase that no player unit can strike next
    /// phase (<see cref="Struck"/>), best first: fewest player units whose reach set
    /// contains the tile (section 8's count), then cost from the mover, then row-major.
    /// Empty when there is none.
    /// </summary>
    public static IReadOnlyList<Coord> Tiles(BattleState state, GameContent content, BattleUnit unit)
    {
        var reach = state.ReachOf(unit, content);
        var playerReach = state.UnitsOf(Side.Player).Select(p => state.ReachOf(p, content)).ToList();
        var struck = Struck(state, content);
        return reach.Destinations
            .Where(tile => state.Map.TerrainAt(tile, content).HealPercent > 0 && !struck.Contains(tile))
            .OrderBy(tile => playerReach.Count(r => r.CanEnd(tile)))
            .ThenBy(tile => reach.CostTo(tile)!.Value)
            .ThenBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .ToList();
    }

    /// <summary>
    /// Every tile some player unit could strike next phase on the board as it stands: any
    /// usable weapon it carries, in range from any tile it can end a move on, its own tile
    /// included. This is the strike set, not the move set: a bow that reaches a tile from
    /// where it stands puts that tile in reach without a step (twenty-eighth round). The
    /// set is <see cref="Threat.StruckBy"/>'s, the one the rivalry spike reads.
    /// </summary>
    public static IReadOnlySet<Coord> Struck(BattleState state, GameContent content) =>
        Threat.StruckBy(state, content, Side.Player);

    /// <summary>The tile the unit retreats to now, or null when it may not or has nowhere to go.</summary>
    public static Coord? Choose(BattleState state, GameContent content, BattleUnit unit) =>
        Refusal(state, content, unit) is null && Tiles(state, content, unit) is { Count: > 0 } tiles ? tiles[0] : null;
}
