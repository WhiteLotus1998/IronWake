namespace Ironwake.Core;

/// <summary>
/// The messenger (DESIGN.md 13.24, experiment): on a map with a <c>messenger:</c> header, the
/// enemy placed on the route's first tile never strikes. Once its effective behavior is
/// Aggressive (from the start, or when its guard group wakes) it spends each of its phases
/// running for the road by the shortest path, and when a move ends on the road it leaves the
/// board, not a kill, and every <see cref="MessengerTrigger"/> event fires. Player units block
/// its path as they block any enemy's, so the road can be held as well as raced. It still
/// counters when struck. The planner reads it through <see cref="EnemyAi"/>; the party is not
/// in the path field, as with <see cref="EnemyAi.Drift"/>.
/// </summary>
public static class Messenger
{
    /// <summary>Whether <paramref name="unit"/> is the map's messenger: the enemy that filled the placement on the route's first tile.</summary>
    public static bool Is(BattleState state, BattleUnit unit) =>
        state.Map.Messenger is { } route
        && unit.Side == Side.Enemy
        && unit.PlacementIndex >= 0
        && unit.PlacementIndex < state.Map.Placements.Count
        && state.Map.Placements[unit.PlacementIndex].At == route.From;

    /// <summary>The messenger on the board, or null when there is none or it has fallen or gone.</summary>
    public static BattleUnit? On(BattleState state) =>
        state.Map.Messenger is null ? null : state.UnitsOf(Side.Enemy).FirstOrDefault(u => Is(state, u));

    /// <summary>Whether the messenger runs this phase: it is the messenger, awake (Aggressive) and has not moved.</summary>
    public static bool Runs(BattleState state, GameContent content, BattleUnit unit) =>
        Is(state, unit) && !unit.Moved && state.EffectiveBehavior(unit, content) == Behavior.Aggressive;

    /// <summary>
    /// The path cost from each tile to the road for the messenger's movement, the party left out
    /// of the field and every other enemy standing in it.
    /// </summary>
    public static Distances Field(BattleState state, GameContent content, BattleUnit unit)
    {
        var road = state.Map.Messenger!.Road;
        var movement = content.Class(unit.Unit.ClassId).Movement;
        Occupant GroundOnly(Coord at) =>
            at == unit.At || state.UnitAt(at) is { Side: Side.Player } ? Occupant.None : state.OccupantAt(at, unit.Side);
        return Movement.DistancesTo(state.Map, content, new[] { road }, movement, GroundOnly);
    }

    /// <summary>
    /// How many of its own phases the messenger needs to reach the road on an open field, at its
    /// full Mov each phase: the path cost divided by Mov, rounded up. Null when no path exists.
    /// It ignores the party, so a held road makes the true count larger, never smaller.
    /// </summary>
    public static int? PhasesToRoad(BattleState state, GameContent content, BattleUnit unit)
    {
        if (Field(state, content, unit).From(unit.At) is not { } cost)
        {
            return null;
        }

        var mov = content.Class(unit.Unit.ClassId).Mov;
        return mov <= 0 ? null : (cost + mov - 1) / mov;
    }

    /// <summary>
    /// After a move on the messenger's map: when <paramref name="mover"/> is the messenger and
    /// its move ended on the road, one <see cref="MessengerEscaped"/>, the unit leaves the board,
    /// and the <see cref="MessengerTrigger"/> events fire.
    /// </summary>
    public static BattleState AfterMove(BattleState state, GameContent content, BattleUnit mover, List<GameEvent> events)
    {
        if (state.Map.Messenger is not { } route || mover.At != route.Road || !Is(state, mover))
        {
            return state;
        }

        events.Add(new MessengerEscaped(mover.Id, mover.At));
        return MapEvents.AfterMessenger(state.WithoutUnit(mover.Id) with { MessengerGone = new MessengerFate(mover.At, Escaped: true) }, content, events);
    }
}
