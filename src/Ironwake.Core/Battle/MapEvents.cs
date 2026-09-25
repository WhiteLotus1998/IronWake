namespace Ironwake.Core;

/// <summary>
/// Fires a map's scripted events (issue 32, DESIGN.md section 10). The resolver asks
/// after every accepted command: at a phase start for turn triggers, after a player Move
/// for enter triggers. Events fire in file order, each at most once per battle. A fired
/// event emits <see cref="MapEventFired"/> and then its action's own event. An event whose
/// tile is held is blocked and spent, with no action: a spawn tile with any unit on it,
/// or a terrain change that would leave its occupant on ground it cannot enter.
/// </summary>
public static class MapEvents
{
    /// <summary>The events whose turn trigger names the phase that has just begun.</summary>
    public static BattleState AtPhaseStart(BattleState state, GameContent content, List<GameEvent> events) =>
        Fire(state, content, events, t => t is TurnTrigger turn && turn.Turn == state.Turn && turn.Phase == state.Phase);

    /// <summary>The events whose enter trigger names the tile a player unit has just ended a move on.</summary>
    public static BattleState AfterMove(BattleState state, GameContent content, BattleUnit mover, List<GameEvent> events) =>
        mover.Side != Side.Player ? state : Fire(state, content, events, t => t is EnterTrigger enter && enter.At == mover.At);

    private static BattleState Fire(BattleState state, GameContent content, List<GameEvent> events, Func<MapEventTrigger, bool> triggered)
    {
        foreach (var mapEvent in state.Map.Events)
        {
            if (state.HasFired(mapEvent.Name) || !triggered(mapEvent.Trigger))
            {
                continue;
            }

            state = state with { Fired = Sorted(state.Fired.Add(mapEvent.Name)) };
            switch (mapEvent.Action)
            {
                case ChangeTerrain change:
                    var occupant = state.UnitAt(change.At);
                    if (occupant is not null && !content.TerrainById(change.TerrainId).IsPassable(content.Class(occupant.Unit.ClassId).Movement))
                    {
                        events.Add(new MapEventFired(mapEvent.Name, true));
                        break;
                    }

                    events.Add(new MapEventFired(mapEvent.Name, false));
                    events.Add(new TerrainChanged(change.At, change.TerrainId));
                    state = state with { Map = state.Map.WithTerrain(change.At, change.TerrainId) };
                    break;
                case SpawnEnemy spawn:
                    var at = spawn.Placement.At;
                    if (state.UnitAt(at) is not null || !state.Map.TerrainAt(at, content).IsPassable(content.Class(content.Unit(spawn.Placement.TemplateId).ClassId).Movement))
                    {
                        events.Add(new MapEventFired(mapEvent.Name, true));
                        break;
                    }

                    var id = state.Map.SpawnId(mapEvent);
                    var unit = state.Map.EnemyUnit(spawn.Placement, content) with { Id = id };
                    var placed = BattleState.Place(unit, Side.Enemy, at, state.Map, content) with
                    {
                        Group = spawn.Placement.Group,
                        Behavior = spawn.Placement.Behavior,
                        PlacementIndex = state.Map.SpawnIndex(mapEvent),
                    };
                    var units = state.Units.ToList();
                    units.Add(placed);
                    units.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
                    events.Add(new MapEventFired(mapEvent.Name, false));
                    events.Add(new UnitSpawned(id, at, placed.Group!, placed.Behavior!.Value));
                    state = state with { Units = ValueList<BattleUnit>.From(units) };
                    break;
                case SetFlag flag:
                    events.Add(new MapEventFired(mapEvent.Name, false));
                    events.Add(new FlagSet(flag.Flag));
                    state = state with { Flags = state.HasFlag(flag.Flag) ? state.Flags : Sorted(state.Flags.Add(flag.Flag)) };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), mapEvent.Action, "unknown map event action");
            }
        }

        return state;
    }

    private static ValueList<string> Sorted(ValueList<string> names)
    {
        var list = names.ToList();
        list.Sort(string.CompareOrdinal);
        return ValueList<string>.From(list);
    }
}
