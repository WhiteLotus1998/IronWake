namespace Ironwake.Core;

/// <summary>
/// Fires a map's scripted events (issue 32, DESIGN.md section 10). The resolver asks
/// after every accepted command: at a phase start for turn triggers, after a player Move
/// for enter triggers, after a wake for wakes triggers (issue 1365). Events fire in file order, each at most once per battle. A fired
/// event emits <see cref="MapEventFired"/> and then its action's own event. An event whose
/// tile is barred is blocked and spent, with no action: a spawn tile with any unit on it or
/// terrain the template cannot stand on (the event names that terrain, issue 655), or a
/// terrain change that would leave its occupant on ground it cannot enter, or a drop's terrain change
/// on any occupied tile (DESIGN.md 13.26), whose rock strikes the occupant instead. A boss's spawn is
/// never stopped by a unit: it lands on the nearest free tile instead (<see cref="BossLanding"/>).
/// Under <c>arrivals: wait</c> (issue 1259) a blocked non-boss spawn is not spent: it joins the
/// state's waiting list (<see cref="ArrivalWaits"/>), and each enemy phase start, before that phase's
/// own events, lands the oldest waiting arrival of each tile that is open, one a tile. A held terrain
/// change that is blocked is not spent either, so the next stop may fire it.
/// </summary>
public static class MapEvents
{
    /// <summary>The events whose turn trigger names the phase that has just begun.</summary>
    /// <remarks>A late <see cref="WakesTrigger"/> whose group is awake fires at a player phase's start, with them (issue 1365).</remarks>
    public static BattleState AtPhaseStart(BattleState state, GameContent content, List<GameEvent> events) =>
        Fire(LandWaiting(state, content, events), content, events, t => (t is TurnTrigger turn && turn.Turn == state.Turn && turn.Phase == state.Phase)
            || (t is WakesTrigger { Late: true } late && state.Phase == Side.Player && state.IsAwake(late.Group)));

    /// <summary>
    /// At an enemy phase start, the waiting arrivals (issue 1259): for each tile, in the order its
    /// oldest arrival began to wait, that arrival lands if the tile is open (no unit, terrain it can
    /// stand on) and leaves the list; the rest wait. Any other phase, or an empty list, changes nothing.
    /// </summary>
    private static BattleState LandWaiting(BattleState state, GameContent content, List<GameEvent> events)
    {
        if (state.Phase != Side.Enemy || state.Waiting.Count == 0)
        {
            return state;
        }

        var tried = new HashSet<Coord>();
        foreach (var name in state.Waiting)
        {
            var mapEvent = state.Map.Events.First(e => e.Name == name);
            var spawn = (SpawnEnemy)mapEvent.Action;
            if (!tried.Add(spawn.Placement.At) || Barred(state, content, spawn.Placement, spawn.Placement.At) is not null)
            {
                continue;
            }

            state = state with { Waiting = ValueList<string>.From(state.Waiting.Where(w => w != name)) };
            state = Land(state, content, mapEvent, spawn, spawn.Placement.At, events);
        }

        return state;
    }

    /// <summary>The events whose enter trigger lists the tile a player unit has just ended a move on.</summary>
    public static BattleState AfterMove(BattleState state, GameContent content, BattleUnit mover, List<GameEvent> events) =>
        mover.Side != Side.Player ? state : Fire(state, content, events, t => t is EnterTrigger enter && enter.Tiles.Contains(mover.At));

    /// <summary>The events whose trigger is the messenger reaching its road (DESIGN.md 13.24).</summary>
    public static BattleState AfterMessenger(BattleState state, GameContent content, List<GameEvent> events) =>
        Fire(state, content, events, t => t is MessengerTrigger);

    /// <summary>The events whose drop trigger names <paramref name="ledge"/> (DESIGN.md 13.26): a terrain change strikes its occupant first.</summary>
    public static BattleState AfterDrop(BattleState state, GameContent content, Coord ledge, List<GameEvent> events) =>
        Fire(state, content, events, t => t is DropTrigger drop && drop.Ledge == ledge);

    /// <summary>The events whose <see cref="WakesTrigger"/>, not late, names a group awake on <paramref name="state"/> (issue 1365).</summary>
    public static BattleState AfterWake(BattleState state, GameContent content, List<GameEvent> events) =>
        Fire(state, content, events, t => t is WakesTrigger { Late: false } wakes && state.IsAwake(wakes.Group));

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
                    if (mapEvent.Trigger is DropTrigger)
                    {
                        state = Rockfall.Strike(state, change.At, events);
                    }

                    var occupant = state.UnitAt(change.At);
                    if (occupant is not null && (mapEvent.Trigger is DropTrigger || !content.TerrainById(change.TerrainId).IsPassable(content.Class(occupant.Unit.ClassId).Movement)))
                    {
                        events.Add(new MapEventFired(mapEvent.Name, true));
                        if (change.Held)
                        {
                            state = state with { Fired = ValueList<string>.From(state.Fired.Where(f => f != mapEvent.Name)) };
                        }

                        break;
                    }

                    events.Add(new MapEventFired(mapEvent.Name, false));
                    events.Add(new TerrainChanged(change.At, change.TerrainId));
                    if (change.Held && mapEvent.Trigger is EnterTrigger { Tiles: [var holder] })
                    {
                        state = state with { Bars = state.Bars.Add(new HeldBar(mapEvent.Name, holder, change.At, change.TerrainId, state.Map.TerrainIdAt(change.At))) };
                    }

                    state = state with { Map = state.Map.WithTerrain(change.At, change.TerrainId) };
                    break;
                case SpawnEnemy spawn:
                    var at = spawn.Placement.At;
                    if (spawn.Placement.IsBoss && state.UnitAt(at) is not null && BossLanding(state, content, spawn.Placement) is { } landing)
                    {
                        at = landing;
                    }

                    if (Barred(state, content, spawn.Placement, at) is { } barred)
                    {
                        if (state.Map.ArrivalsWait && !spawn.Placement.IsBoss)
                        {
                            events.Add(new ArrivalWaits(mapEvent.Name, spawn.Placement.TemplateId, at, barred.Length == 0 ? null : barred));
                            state = state with { Waiting = state.Waiting.Add(mapEvent.Name) };
                            break;
                        }

                        events.Add(new MapEventFired(mapEvent.Name, true, barred.Length == 0 ? null : barred));
                        break;
                    }

                    state = Land(state, content, mapEvent, spawn, at, events);
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
    /// Why a spawn of <paramref name="placement"/> cannot land on <paramref name="at"/>: an empty string when
    /// a unit stands there, the terrain id when the template cannot stand on it, and null when it can land.
    /// </summary>
    private static string? Barred(BattleState state, GameContent content, EnemyPlacement placement, Coord at)
    {
        if (state.UnitAt(at) is not null)
        {
            return "";
        }

        return state.Map.TerrainAt(at, content).IsPassable(content.Class(content.Unit(placement.TemplateId).ClassId).Movement)
            ? null
            : state.Map.TerrainIdAt(at);
    }

    /// <summary>The spawn of <paramref name="mapEvent"/> placed on <paramref name="at"/>, which is open: <see cref="MapEventFired"/> then <see cref="UnitSpawned"/>.</summary>
    private static BattleState Land(BattleState state, GameContent content, MapEvent mapEvent, SpawnEnemy spawn, Coord at, List<GameEvent> events)
    {
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
        return state with { Units = ValueList<BattleUnit>.From(units) };
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
