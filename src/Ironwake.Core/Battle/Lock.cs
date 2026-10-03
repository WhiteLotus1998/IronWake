namespace Ironwake.Core;

/// <summary>
/// The lock (issue 635, rounds 269 to 271; DESIGN section 14): Turn the Key, the First Warden's
/// Lance's art (<see cref="CombatArtEffect.Locks"/>), is the chill turned all the way.
/// <list type="bullet">
/// <item>A hit with a locking art on a unit that survives the combat locks it (<see cref="UnitLocked"/>):
/// Mov 0 on the chill's clock (<see cref="BattleUnit.Chill"/>), which the same hit sets, refreshed and
/// never stacked. It still strikes, counters and uses items from its tile. Bosses are locked like anyone.</item>
/// <item>The tether: it holds only while the striker stands orthogonally beside it. When the striker
/// leaves by any means (its own move, Fall back, a shove) or falls, the lock drops
/// (<see cref="LockDropped"/>) to the chill for the rest of that clock, and never comes back on it.</item>
/// <item>The clock clearing clears the lock with it.</item>
/// </list>
/// Everything is board state, so Recall restores it with the board, and <c>threat</c>, the planners
/// and the Sim read it through <see cref="BattleState.ReachOf"/>.
/// </summary>
public static class Lock
{
    /// <summary>Whether <paramref name="unit"/> is locked in <paramref name="state"/>: its chill clock running and its locker standing beside it.</summary>
    public static bool Holds(BattleState state, BattleUnit unit) =>
        unit.LockedBy is { } by && unit.Chill > 0 && state.Find(by) is { } locker && locker.At.DistanceTo(unit.At) == 1;

    /// <summary>
    /// After an attack: <paramref name="targetId"/> is locked by <paramref name="strikerId"/> when the art
    /// locks, a strike of the striker's landed on it, and both still stand.
    /// </summary>
    public static BattleState AfterAttack(BattleState state, CombatArtEffect? art, string strikerId, string targetId, ValueList<StrikeEvent> strikes, List<GameEvent> events)
    {
        if (art is not { Locks: true }
            || state.Find(strikerId) is null
            || state.Find(targetId) is not { } target
            || !strikes.Any(s => s.AttackerId == strikerId && s.TargetId == targetId && s.Hit))
        {
            return state;
        }

        events.Add(new UnitLocked(target.Id, strikerId, target.Side, target.Side == state.Phase));
        return state.WithUnit(target with { Chill = 1, LockedBy = strikerId });
    }

    /// <summary>After any accepted command: every lock whose striker has fallen or no longer stands beside it drops to the chill.</summary>
    public static BattleState After(BattleState state, List<GameEvent> events)
    {
        foreach (var unit in state.Units)
        {
            if (unit.LockedBy is { } by && !Holds(state, unit))
            {
                if (unit.Chill > 0)
                {
                    events.Add(new LockDropped(unit.Id, by));
                }

                state = state.WithUnit(unit with { LockedBy = null });
            }
        }

        return state;
    }

    /// <summary>
    /// The unit card's line for a locked unit: <c>locked by Teodor: Mov 0 while Teodor stands beside,
    /// else chilled until enemy phase ends</c>; null when it is not locked.
    /// </summary>
    public static string? CardLine(BattleState state, BattleUnit unit, string lockerName) =>
        Holds(state, unit) ? $"locked by {lockerName}: Mov 0 while {lockerName} stands beside, else chilled until {Frost.Until(unit.Side, unit.Chill == 1 && unit.Side == state.Phase)}" : null;
}
