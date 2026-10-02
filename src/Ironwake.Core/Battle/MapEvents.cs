namespace Ironwake.Core;

/// <summary>
/// Fires a map's scripted events (issue 32, DESIGN.md section 10). The resolver asks
/// after every accepted command: at a phase start for turn triggers, after a player Move
/// for enter triggers. Events fire in file order, each at most once per battle. A fired
/// event emits <see cref="MapEventFired"/> and then its action's own event. An event whose
/// tile is barred is blocked and spent, with no action: a spawn tile with any unit on it or
/// terrain the template cannot stand on (the event names that terrain, issue 655), or a
/// terrain change that would leave its occupant on ground it cannot enter. A boss's spawn is
/// never stopped by a unit: it lands on the nearest free tile instead (<see cref="BossLanding"/>).
/// </summary>
public static class MapEvents
{
    /// <summary>The events whose turn trigger names the phase that has just begun.</summary>
    public static BattleState AtPhaseStart(BattleState state, GameContent content, List<GameEvent> events) =>
        Fire(state, content, events, t => t is TurnTrigger turn && turn.Turn == state.Turn && turn.Phase == state.Phase);

    /// <summary>The events whose enter trigger lists the tile a player unit has just ended a move on.</summary>
    public static BattleState AfterMove(BattleState state, GameContent content, BattleUnit mover, List<GameEvent> events) =>
        mover.Side != Side.Player ? state : Fire(state, content, events, t => t is EnterTrigger enter && enter.Tiles.Contains(mover.At));

    /// <summary>The events whose trigger is the messenger reaching its road (DESIGN.md 13.24).</summary>
    public static BattleState AfterMessenger(BattleState state, GameContent content, List<GameEvent> events) =>
        Fire(state, content, events, t => t is MessengerTrigger);

    /// <summary>The events whose trigger is the fall of <paramref name="front"/> (issue 692).</summary>
    public static BattleState AfterFall(BattleState state, GameContent content, string front, List<GameEvent> events) =>
        Fire(state, content, events, t => t is FallsTrigger falls && falls.Front == front);

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
                    if (spawn.Placement.IsBoss && state.UnitAt(at) is not null && BossLanding(state, content, spawn.Placement) is { } landing)
                    {
                        at = landing;
                    }

                    if (state.UnitAt(at) is not null)
                    {
                        events.Add(new MapEventFired(mapEvent.Name, true));
                        break;
                    }

                    if (!state.Map.TerrainAt(at, content).IsPassable(content.Class(content.Unit(spawn.Placement.TemplateId).ClassId).Movement))
                    {
                        events.Add(new MapEventFired(mapEvent.Name, true, state.Map.TerrainIdAt(at)));
                        break;
                    }

                    var id = state.Map.SpawnId(mapEvent);
                    var unit = state.Map.EnemyUnit(spawn.Placement, content) with { Id = id };
                    var placed = BattleState.Place(unit, Side.Enemy, at, state.Map, content) with
                    {
                        Group = spawn.Placement.Group,
                        Behavior = spawn.Placement.Behavior,
                        IsBoss = spawn.Placement.IsBoss,
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

    /// <summary>
    /// Where a boss's spawn lands when a unit stands on its tile (issue 692): the nearest free
    /// tile the boss can stand on by Manhattan distance, the lower row and then the lower column
    /// on a tie. A held tile never stops a boss, so a hold-then-boss map cannot be won by
    /// standing on his road. Null when no tile is free, and the spawn is blocked as any other.
    /// </summary>
    public static Coord? BossLanding(BattleState state, GameContent content, EnemyPlacement boss)
    {
        var movement = content.Class(content.Unit(boss.TemplateId).ClassId).Movement;
        Coord? best = null;
        var bestDistance = int.MaxValue;
        for (var y = 0; y < state.Map.Height; y++)
        {
            for (var x = 0; x < state.Map.Width; x++)
            {
                var tile = new Coord(x, y);
                var distance = Math.Abs(x - boss.At.X) + Math.Abs(y - boss.At.Y);
                if (distance < bestDistance && state.UnitAt(tile) is null && state.Map.TerrainAt(tile, content).IsPassable(movement))
                {
                    best = tile;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    private static ValueList<string> Sorted(ValueList<string> names)
    {
        var list = names.ToList();
        list.Sort(string.CompareOrdinal);
        return ValueList<string>.From(list);
    }
}
