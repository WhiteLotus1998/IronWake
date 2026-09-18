namespace Ironwake.Core;

/// <summary>
/// The only way a <see cref="BattleState"/> changes (DESIGN.md section 2): apply one
/// command, get the next state and the events that explain it. An illegal command is
/// answered with a <see cref="Rejection"/> naming the rule, never with an exception.
/// Every accepted command except <see cref="Recall"/> pushes the state it left onto the
/// history; a Recall restores a prior state, truncates the history to before it, and
/// spends a charge (section 7). Rolls come from a <see cref="KeyedRng"/> over the state's
/// seed under the section 5 keys, so the same attack on the same turn draws the same
/// numbers whatever was resolved before it or rewound since.
/// </summary>
public static class Resolver
{
    public static ApplyResult Apply(BattleState state, GameContent content, Command command)
    {
        var events = new List<GameEvent>();
        BattleState next;
        Rejection? rejection;
        switch (command)
        {
            case Move move:
                (next, rejection) = ApplyMove(state, content, move, events);
                break;
            case Attack attack:
                (next, rejection) = ApplyAttack(state, content, attack, events);
                break;
            case UseItem item:
                var actor = Acting(state, item.UnitId, out rejection);
                next = state;
                rejection ??= new Rejection(RejectionReason.NotAvailable, $"{actor!.Id} cannot use an item: items are not in this build (issue 9)");
                break;
            case Wait wait:
                (next, rejection) = ApplyWait(state, wait, events);
                break;
            case EndPhase:
                (next, rejection) = ApplyEndPhase(state, events);
                break;
            case Recall recall:
                return ApplyRecall(state, recall);
            default:
                next = state;
                rejection = new Rejection(RejectionReason.UnknownCommand, $"unknown command {command.GetType().Name}");
                break;
        }

        if (rejection is not null)
        {
            return new ApplyResult(state, ValueList<GameEvent>.Empty, rejection);
        }

        next = next with { History = state.History.Add(state with { History = ValueList<BattleState>.Empty }) };
        return new ApplyResult(next, ValueList<GameEvent>.From(events), null);
    }

    /// <summary>The unit a command names, if it is on the board, on the acting side, and has not acted.</summary>
    private static BattleUnit? Acting(BattleState state, string unitId, out Rejection? rejection)
    {
        var unit = state.Find(unitId);
        if (unit is null)
        {
            rejection = new Rejection(RejectionReason.NoSuchUnit, $"no living unit '{unitId}'");
            return null;
        }

        if (unit.Side != state.Phase)
        {
            rejection = new Rejection(RejectionReason.NotThisSide, $"{unit.Id} is a {unit.Side} unit and it is the {state.Phase} phase");
            return null;
        }

        if (unit.Acted)
        {
            rejection = new Rejection(RejectionReason.AlreadyActed, $"{unit.Id} has already acted this phase");
            return null;
        }

        rejection = null;
        return unit;
    }

    private static (BattleState, Rejection?) ApplyMove(BattleState state, GameContent content, Move move, List<GameEvent> events)
    {
        var unit = Acting(state, move.UnitId, out var rejection);
        if (unit is null)
        {
            return (state, rejection);
        }

        if (unit.Moved)
        {
            return (state, new Rejection(RejectionReason.AlreadyMoved, $"{unit.Id} has already moved this phase"));
        }

        var reach = state.ReachOf(unit, content);
        var entry = reach.EntryAt(move.To);
        if (entry is not { CanEnd: true })
        {
            var why = !state.Map.Contains(move.To) ? "outside the map"
                : entry is null ? $"not within {reach.Mov} movement from {unit.At}"
                : "occupied by an ally";
            return (state, new Rejection(RejectionReason.OutOfReach, $"{unit.Id} cannot move to {move.To}: {why}"));
        }

        events.Add(new UnitMoved(unit.Id, unit.At, move.To, entry.Path));
        return (state.WithUnit(unit with { At = move.To, Moved = true }), null);
    }

    private static (BattleState, Rejection?) ApplyAttack(BattleState state, GameContent content, Attack attack, List<GameEvent> events)
    {
        var unit = Acting(state, attack.UnitId, out var rejection);
        if (unit is null)
        {
            return (state, rejection);
        }

        var target = state.Find(attack.TargetId);
        if (target is null)
        {
            return (state, new Rejection(RejectionReason.NoSuchTarget, $"no living unit '{attack.TargetId}' to attack"));
        }

        if (target.Side == unit.Side)
        {
            return (state, new Rejection(RejectionReason.NotAnEnemy, $"{target.Id} is on {unit.Id}'s own side"));
        }

        var weapon = unit.EquippedWeapon(content);
        if (weapon is null)
        {
            return (state, new Rejection(RejectionReason.NoWeapon, $"{unit.Id} has no weapon to attack with"));
        }

        var distance = unit.At.DistanceTo(target.At);
        if (!weapon.InRange(distance))
        {
            return (state, new Rejection(
                RejectionReason.OutOfRange,
                $"{target.Id} at {target.At} is {distance} tiles from {unit.Id} at {unit.At}; {weapon.Name} reaches {weapon.MinRange}-{weapon.MaxRange}"));
        }

        var result = CombatResolver.Resolve(
            unit.ToCombatant(state.Map, content),
            target.ToCombatant(state.Map, content),
            distance,
            new CombatContext(state.Turn, state.Phase),
            new KeyedRng(state.Seed),
            state.Scheme);
        events.Add(new CombatFought(unit.Id, target.Id, state.Turn, state.Phase, result.Strikes, result.AttackerHp, result.DefenderHp));

        var next = state.WithUnit(unit with { Hp = result.AttackerHp, Moved = true, Acted = true });
        next = next.WithUnit(target with { Hp = result.DefenderHp });
        if (result.DefenderDied)
        {
            events.Add(new UnitDied(target.Id, target.Side, target.At));
            next = next.WithoutUnit(target.Id);
        }

        if (result.AttackerDied)
        {
            events.Add(new UnitDied(unit.Id, unit.Side, unit.At));
            next = next.WithoutUnit(unit.Id);
        }

        return (next, null);
    }

    private static (BattleState, Rejection?) ApplyWait(BattleState state, Wait wait, List<GameEvent> events)
    {
        var unit = Acting(state, wait.UnitId, out var rejection);
        if (unit is null)
        {
            return (state, rejection);
        }

        events.Add(new UnitWaited(unit.Id));
        return (state.WithUnit(unit with { Moved = true, Acted = true }), null);
    }

    private static (BattleState, Rejection?) ApplyEndPhase(BattleState state, List<GameEvent> events)
    {
        var ended = state.Phase;
        var nextPhase = ended == Side.Player ? Side.Enemy : Side.Player;
        var nextTurn = ended == Side.Enemy ? state.Turn + 1 : state.Turn;
        var units = new List<BattleUnit>(state.Units.Count);
        foreach (var unit in state.Units)
        {
            units.Add(unit with { Moved = false, Acted = false });
        }

        events.Add(new PhaseEnded(ended, state.Turn));
        events.Add(new PhaseBegan(nextPhase, nextTurn));
        return (state with { Phase = nextPhase, Turn = nextTurn, Units = ValueList<BattleUnit>.From(units) }, null);
    }

    private static ApplyResult ApplyRecall(BattleState state, Recall recall)
    {
        if (state.RecallCharges < 1)
        {
            return new ApplyResult(state, ValueList<GameEvent>.Empty, new Rejection(RejectionReason.NoRecallCharges, "no Recall charges left on this map"));
        }

        if (recall.ToIndex < 0 || recall.ToIndex >= state.History.Count)
        {
            return new ApplyResult(state, ValueList<GameEvent>.Empty, new Rejection(
                RejectionReason.NoSuchHistoryIndex, $"history holds {state.History.Count} states; there is no state {recall.ToIndex} to recall"));
        }

        var kept = new List<BattleState>(recall.ToIndex);
        for (var i = 0; i < recall.ToIndex; i++)
        {
            kept.Add(state.History[i]);
        }

        var charges = state.RecallCharges - 1;
        var restored = state.History[recall.ToIndex] with { History = ValueList<BattleState>.From(kept), RecallCharges = charges };
        return new ApplyResult(restored, ValueList<GameEvent>.Of(new Recalled(recall.ToIndex, charges)), null);
    }
}
