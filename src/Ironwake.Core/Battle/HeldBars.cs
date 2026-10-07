namespace Ironwake.Core;

/// <summary>
/// The held bar (issue 1259, experiment, the Lazar House sample): a terrain change marked
/// <c>held</c> on a one-tile enter trigger lasts only while a player unit stands on that tile.
/// The resolver asks after every accepted command, and the <c>threat ... from</c> read asks after
/// its stop: a bar whose holder tile has no player unit on it gives its tile back
/// (<see cref="BarReleased"/>, then <see cref="TerrainChanged"/>), and its event is unfired, so the
/// next stop there fires it again. A bar whose tile something else retiled meanwhile is dropped and
/// keeps what it became. A Recall restores the bars with the board.
/// </summary>
public static class HeldBars
{
    /// <summary>The state with every bar whose holder has left released.</summary>
    public static BattleState After(BattleState state, List<GameEvent> events)
    {
        if (state.Bars.Count == 0)
        {
            return state;
        }

        var kept = new List<HeldBar>();
        var map = state.Map;
        var fired = state.Fired;
        foreach (var bar in state.Bars)
        {
            if (map.TerrainIdAt(bar.At) != bar.TerrainId)
            {
                continue;
            }

            if (Holder(state, bar) is not null)
            {
                kept.Add(bar);
                continue;
            }

            events.Add(new BarReleased(bar.Event, bar.Holder, bar.At, bar.UnderId));
            events.Add(new TerrainChanged(bar.At, bar.UnderId));
            map = map.WithTerrain(bar.At, bar.UnderId);
            fired = ValueList<string>.From(fired.Where(f => f != bar.Event));
        }

        return kept.Count == state.Bars.Count ? state : state with { Map = map, Fired = fired, Bars = ValueList<HeldBar>.From(kept) };
    }

    /// <summary>The player unit standing on <paramref name="bar"/>'s holder tile, or null.</summary>
    public static BattleUnit? Holder(BattleState state, HeldBar bar) =>
        state.UnitAt(bar.Holder) is { Side: Side.Player } unit ? unit : null;

    /// <summary>
    /// The board's lines for a map with held bars or waiting arrivals, one per held event in map order, named by the event with its underscores as spaces,
    /// then one per tile with arrivals waiting and no bar: <c>north bar (8,0): barred while teodor holds 8,1;
    /// waiting: brigand, hexer</c>, <c>north bar (8,0): open, barred only while one of yours holds 8,1</c>,
    /// <c>waiting at 6,7: brigand</c>. Null when there is nothing to say.
    /// </summary>
    public static string? Line(BattleState state, GameContent content)
    {
        var lines = new List<string>();
        var waiting = state.Waiting
            .Select(name => (SpawnEnemy)state.Map.Events.First(e => e.Name == name).Action)
            .ToList();
        var barTiles = new HashSet<Coord>();
        foreach (var mapEvent in state.Map.Events)
        {
            if (mapEvent is not { Action: ChangeTerrain { Held: true } change, Trigger: EnterTrigger { Tiles: [var holder] } })
            {
                continue;
            }

            barTiles.Add(change.At);
            var bar = state.Bars.FirstOrDefault(b => b.Event == mapEvent.Name);
            var held = bar is not null && Holder(state, bar) is { } unit
                ? $"barred while {unit.Id} holds {holder}"
                : $"open, barred only while one of yours holds {holder}";
            var behind = Behind(waiting, change.At, content);
            lines.Add($"{mapEvent.Name.Replace('_', ' ')} ({change.At}): {held}" + (behind is null ? "" : "; waiting: " + behind));
        }

        foreach (var at in waiting.Select(w => w.Placement.At).Distinct().Where(t => !barTiles.Contains(t)))
        {
            lines.Add($"waiting at {at}: {Behind(waiting, at, content)}");
        }

        return lines.Count == 0 ? null : string.Join("\n", lines);
    }

    /// <summary>The arrivals waiting at <paramref name="at"/>, oldest first, by kind: <c>brigand, hexer</c>; null when none.</summary>
    private static string? Behind(List<SpawnEnemy> waiting, Coord at, GameContent content)
    {
        var here = waiting.Where(w => w.Placement.At == at).Select(w => content.Unit(w.Placement.TemplateId).Name.ToLowerInvariant()).ToList();
        return here.Count == 0 ? null : string.Join(", ", here);
    }
}
