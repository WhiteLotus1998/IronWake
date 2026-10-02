namespace Ironwake.Core;

/// <summary>
/// Fronts (issue 692, the keep finale's first slice): on a map with a <c>fronts:</c> header, after
/// every accepted command each standing front falls, in file order, when an enemy stands on one of
/// its tiles or an enemy's move in that command passed through one: a wave that walks through the
/// breach has broken it, whether or not it stops there. A fall is one <see cref="FrontFell"/>, the front's name on <see cref="BattleState.Fallen"/>,
/// and then its <see cref="FallsTrigger"/> events. A fall never loses the map; the outcome stays the
/// win condition's. A Recall restores the fallen list with the board. The planner does not read it.
/// </summary>
public static class Fronts
{
    /// <summary>The line a fall prints: <c>west breach falls: the wave is inside</c>.</summary>
    public static string FallLine(Front front) => $"{front.Words} falls: the wave is inside";

    /// <summary>The rule the board and <c>help</c> print on a map with fronts.</summary>
    public const string Rule = "A front falls when an enemy stands on or moves through one of its tiles. A fallen front lets its wave in; it does not lose the map.";

    /// <summary>Whether <paramref name="front"/> has fallen on <paramref name="state"/>'s board.</summary>
    public static bool HasFallen(BattleState state, Front front) => state.Fallen.Contains(front.Name);

    /// <summary>
    /// After a command: every standing front with an enemy on one of its tiles, or on whose tiles an
    /// enemy's move among <paramref name="events"/> passed (read from <paramref name="before"/>, where
    /// the mover stood), falls, in file order, each firing its own events before the next is read.
    /// Nothing on a map without fronts or once the battle is over.
    /// </summary>
    public static BattleState After(BattleState before, BattleState state, GameContent content, List<GameEvent> events)
    {
        if (state.Map.Fronts.Count == 0 || state.Outcome.IsOver)
        {
            return state;
        }

        var crossed = events.OfType<UnitMoved>()
            .Where(m => before.Find(m.UnitId) is { Side: Side.Enemy })
            .SelectMany(m => m.Path)
            .ToHashSet();
        foreach (var front in state.Map.Fronts)
        {
            if (HasFallen(state, front) || !front.Tiles.Any(t => crossed.Contains(t) || state.UnitAt(t) is { Side: Side.Enemy }))
            {
                continue;
            }

            var fallen = state.Fallen.Add(front.Name).ToList();
            fallen.Sort(string.CompareOrdinal);
            events.Add(new FrontFell(front.Name));
            state = MapEvents.AfterFall(state with { Fallen = ValueList<string>.From(fallen) }, content, front.Name, events);
        }

        return state;
    }
}
