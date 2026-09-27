namespace Ironwake.Core;

/// <summary>
/// Brace (DESIGN.md 13.14, experiment), behind a map's <c>brace: on</c> header: a unit that
/// takes Wait without having moved this turn braces (a Guard of a sleeping group does not), and until its side's next phase begins
/// every strike against it, counters included, is at <see cref="Hit"/> less hit. An ally's
/// shove takes the brace off. It sits in the striker's hit slot beside the pincer
/// (<see cref="Combatant.HitModifier"/>), so every forecast, <c>threat</c>, the planner's score
/// and the resolver read one number, and a pin and a brace cancel.
/// </summary>
public static class Brace
{
    /// <summary>The hit a strike against a braced unit loses (DESIGN.md 13.14, provisional).</summary>
    public const int Hit = 15;

    /// <summary>
    /// Whether <paramref name="unit"/> braces when it waits now: on a <c>brace: on</c> map, only
    /// if it has not moved, and never a Guard whose group still sleeps, since a sleeper caught
    /// off guard is the ambush the wake rule promises.
    /// </summary>
    public static bool BracesOnWait(BattleState state, BattleUnit unit) =>
        state.Map.BraceEnabled && !unit.Moved && !Asleep(state, unit);

    private static bool Asleep(BattleState state, BattleUnit unit) =>
        unit.Behavior == Behavior.Guard && unit.Group is { } group && !state.IsAwake(group);

    /// <summary>The hit modifier a strike against <paramref name="target"/> carries: minus <see cref="Hit"/> when it is braced, else 0.</summary>
    public static int HitAgainst(BattleUnit? target) =>
        target is { Braced: true } ? -Hit : 0;
}
