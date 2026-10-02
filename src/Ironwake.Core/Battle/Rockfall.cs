namespace Ironwake.Core;

/// <summary>
/// Rockfall (DESIGN.md 13.26, experiment): a map's <see cref="DropTrigger"/> events name a ledge.
/// A player unit standing on it takes the drop as its action, and every event on the ledge fires
/// once, in file order. A terrain change under a drop first strikes the unit standing on its tile
/// for <see cref="Damage"/>, never below 1, and leaves that tile as it was (the event is blocked),
/// so a tile someone stands under stays open, whoever stands there. The enemy never drops, and the planner does not read ledges.
/// </summary>
public static class Rockfall
{
    /// <summary>What the rock takes from the unit standing under it, never below 1.</summary>
    public const int Damage = 10;

    /// <summary>The ledges of the map whose events have not all fired, in file order.</summary>
    public static IEnumerable<Coord> Ledges(BattleState state) =>
        state.Map.Events
            .Where(e => e.Trigger is DropTrigger && !state.HasFired(e.Name))
            .Select(e => ((DropTrigger)e.Trigger).Ledge)
            .Distinct();

    /// <summary>
    /// Why <paramref name="unit"/> cannot drop the rock, or null when it can. Whether the unit may
    /// act at all is the caller's check.
    /// </summary>
    public static string? Refusal(BattleState state, BattleUnit unit)
    {
        if (unit.Side != Side.Player)
        {
            return $"{unit.Id} cannot drop the rock: only player units drop it";
        }

        if (!state.Map.Events.Any(e => e.Trigger is DropTrigger drop && drop.Ledge == unit.At))
        {
            return $"{unit.Id} cannot drop the rock: {unit.At} is not a ledge";
        }

        if (!Ledges(state).Contains(unit.At))
        {
            return $"{unit.Id} cannot drop the rock: the rock on {unit.At} has already fallen";
        }

        return null;
    }

    /// <summary>The rock strikes whoever stands on <paramref name="at"/>, never below 1.</summary>
    public static BattleState Strike(BattleState state, Coord at, List<GameEvent> events)
    {
        if (state.UnitAt(at) is not { } struck)
        {
            return state;
        }

        var hp = Math.Max(1, struck.Hp - Damage);
        events.Add(new RockfallStruck(struck.Id, at, struck.Hp - hp, hp));
        return state.WithUnit(struck with { Hp = hp });
    }

    /// <summary>
    /// The board's line for the ledges still standing, or null when there are none:
    /// <c>ledges (drop, an action): 5,2 brings down 6,4 6,5 to Mountain; the rock strikes anyone under it for 10, never below 1, and a tile someone stands on stays open</c>.
    /// </summary>
    public static string? Line(BattleState state, GameContent content)
    {
        var parts = new List<string>();
        foreach (var ledge in Ledges(state))
        {
            var changes = state.Map.Events
                .Where(e => e.Trigger is DropTrigger drop && drop.Ledge == ledge && !state.HasFired(e.Name) && e.Action is ChangeTerrain)
                .Select(e => (ChangeTerrain)e.Action)
                .ToList();
            var named = string.Join("; ", changes.GroupBy(c => c.TerrainId).Select(g => $"{string.Join(' ', g.Select(c => c.At))} to {content.TerrainById(g.Key).Name}"));
            parts.Add(changes.Count == 0 ? $"{ledge}" : $"{ledge} brings down {named}");
        }

        return parts.Count == 0
            ? null
            : $"ledges (drop, an action): {string.Join("; ", parts)}; the rock strikes anyone under it for {Damage}, never below 1, and a tile someone stands on stays open";
    }
}
