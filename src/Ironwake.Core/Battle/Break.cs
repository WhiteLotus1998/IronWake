namespace Ironwake.Core;

/// <summary>
/// The break (DESIGN.md 13.22, experiment): on a <c>break: on</c> map, when a boss dies, every
/// living member of his group at or below half its max HP breaks and leaves the board at once.
/// A broken unit is not killed: no EXP, no <see cref="UnitDied"/>, one <see cref="UnitBroke"/>.
/// A member above half fights on, and so does a sworn one: a member of an <c>oathbound</c> group
/// never breaks (issue 692), and the forecast says so (<see cref="Sworn"/>). The planner does not read it.
/// </summary>
public static class Break
{
    /// <summary>Whether <paramref name="unit"/> would break if its boss fell now: HP at or below half its max.</summary>
    public static bool Wavers(BattleUnit unit, GameContent content) => unit.Hp * 2 <= unit.MaxHp(content);

    /// <summary>
    /// The members of <paramref name="boss"/>'s group who would break if he fell on
    /// <paramref name="state"/>'s board, in id order: empty off a <c>break: on</c> map, for a
    /// unit that is not a boss or has no group, and for a member above half.
    /// </summary>
    public static IReadOnlyList<BattleUnit> WouldBreak(BattleState state, GameContent content, BattleUnit boss)
    {
        if (!state.Map.BreakEnabled || !boss.IsBoss || boss.Group is not { } group)
        {
            return Array.Empty<BattleUnit>();
        }

        return state.Units
            .Where(u => u.Side == boss.Side && u.Id != boss.Id && !u.IsBoss && u.Group == group && !state.Map.IsOathbound(u) && Wavers(u, content))
            .ToList();
    }

    /// <summary>
    /// The members of <paramref name="boss"/>'s group who are sworn (issue 692): oath-bound, so they
    /// would stand at any HP if he fell. Empty off a <c>break: on</c> map and for a unit that is not
    /// a boss or has no group.
    /// </summary>
    public static IReadOnlyList<BattleUnit> Sworn(BattleState state, BattleUnit boss)
    {
        if (!state.Map.BreakEnabled || !boss.IsBoss || boss.Group is not { } group)
        {
            return Array.Empty<BattleUnit>();
        }

        return state.Units
            .Where(u => u.Side == boss.Side && u.Id != boss.Id && !u.IsBoss && u.Group == group && state.Map.IsOathbound(u))
            .ToList();
    }

    /// <summary>
    /// After a command on a <c>break: on</c> map: for each boss whose <see cref="UnitDied"/> is
    /// among <paramref name="events"/>, every member of his group at or below half on
    /// <paramref name="after"/>'s board leaves it, one <see cref="UnitBroke"/> each. Bosses are
    /// read from <paramref name="before"/>, where they still stand.
    /// </summary>
    public static BattleState After(BattleState before, BattleState after, GameContent content, List<GameEvent> events)
    {
        if (!after.Map.BreakEnabled)
        {
            return after;
        }

        var fallen = events.OfType<UnitDied>()
            .Select(d => before.Find(d.UnitId))
            .Where(u => u is { IsBoss: true, Group: not null })
            .ToList();
        var next = after;
        foreach (var boss in fallen)
        {
            foreach (var member in WouldBreak(next, content, boss!))
            {
                events.Add(new UnitBroke(member.Id, member.At, member.Hp));
                next = next.WithoutUnit(member.Id);
            }
        }

        return next;
    }
}
