namespace Ironwake.Core;

/// <summary>
/// Taking back a move (issue 676, DESIGN.md section 7). A misclicked move is a tax on the
/// interface, not a decision, so a player unit that has moved and not acted may return to the
/// tile it began the phase on without spending Recall. The take-back is refused unless the move
/// was the last command and changed nothing but the unit's tile: a wake, a fired map event, an
/// enemy brought into sight on a dusk map (round 207), or any other change makes the move final,
/// since a free take-back would otherwise probe a wake ring, a trigger or the dark, which is the
/// knowledge Recall is priced for. A move rolls nothing, so an undo touches no dice.
/// </summary>
public static class TakeBack
{
    /// <summary>
    /// Why <paramref name="unitId"/>'s move cannot be taken back in <paramref name="state"/>, or
    /// null when it can. Each reason is checked in the order a player would ask it: the unit,
    /// whether it acted or moved, whether another command followed, then what the move changed.
    /// </summary>
    public static Rejection? Refusal(BattleState state, string unitId)
    {
        var unit = state.Find(unitId);
        if (unit is null)
        {
            return new Rejection(RejectionReason.NoSuchUnit, $"no living unit '{unitId}'");
        }

        if (unit.Side != state.Phase)
        {
            return new Rejection(RejectionReason.NotThisSide, $"{unit.Id} is a {unit.Side} unit and it is the {state.Phase} phase");
        }

        if (unit.Acted)
        {
            return Refused($"{unit.Id} has acted this phase; its move is final");
        }

        if (!unit.Moved)
        {
            return Refused($"{unit.Id} has not moved this phase");
        }

        if (state.History.Count == 0 || state.History[^1] is not { } left || left.Find(unit.Id) is not { Moved: false } start || left.Phase != state.Phase)
        {
            return Refused($"another command has followed {unit.Id}'s move");
        }

        var woke = state.AwakeGroups.Where(g => !left.AwakeGroups.Contains(g)).ToList();
        if (woke.Count > 0)
        {
            return Refused($"{unit.Id}'s move woke {string.Join(" and ", woke.Select(UnitNames.Group))}");
        }

        var fired = state.Fired.Where(f => !left.Fired.Contains(f)).ToList();
        if (fired.Count > 0)
        {
            return Refused($"{unit.Id}'s move set off the map's {string.Join(" and ", fired)} event");
        }

        var lit = state.UnitsOf(Side.Enemy).Count(e => Dusk.Seen(state, e) && left.Find(e.Id) is { } was && !Dusk.Seen(left, was));
        if (lit > 0)
        {
            return Refused($"{unit.Id}'s move brought {(lit == 1 ? "an unseen enemy" : $"{lit} unseen enemies")} into sight");
        }

        if (state.WithUnit(start) with { History = left.History } != left)
        {
            return Refused($"{unit.Id}'s move changed more than its tile");
        }

        return null;
    }

    /// <summary>
    /// Applies <paramref name="undo"/>: the state the move left, with the history it had, and one
    /// <see cref="MoveUndone"/>; or the refusal, the state unchanged.
    /// </summary>
    public static ApplyResult Apply(BattleState state, Undo undo)
    {
        if (Refusal(state, undo.UnitId) is { } refusal)
        {
            return new ApplyResult(state, ValueList<GameEvent>.Empty, refusal);
        }

        var left = state.History[^1];
        var restored = left with { History = state.History.RemoveAt(state.History.Count - 1) };
        var moved = new MoveUndone(undo.UnitId, state.Find(undo.UnitId)!.At, left.Find(undo.UnitId)!.At);
        return new ApplyResult(restored, ValueList<GameEvent>.Of(moved), null);
    }

    private static Rejection Refused(string why) => new(RejectionReason.CannotUndo, "undo refused: " + why);
}
