namespace Ironwake.Core;

/// <summary>
/// The one answer (DESIGN.md 13.29, experiment), behind a map's <c>one_answer: on</c> header. A
/// unit counters every strike that reaches it, so the order of a side's strikes never matters
/// against one target. On a one-answer map a unit that counters (makes at least one strike back)
/// is answered until the next phase begins, on either side, and makes no counter in that time
/// (<see cref="Combatant.AnswerSpent"/>). A first strike draws the answer and the next goes
/// unanswered, on both phases: the party orders its strikes, and the enemy phase's second striker
/// on a unit is not countered either. The mark is read where the answering side is built
/// (<see cref="BattleUnit.Answering"/>), so every forecast, the planner and the resolver agree.
/// A strike the unit could not answer (out of reach, blind, unarmed) does not spend it.
/// </summary>
public static class Answer
{
    /// <summary>The line a <c>one_answer: on</c> map prints under its board, so the rule is on screen.</summary>
    public const string Legend = "one answer: a unit that counters makes no other counter until the next phase begins, on either side";

    /// <summary>Whether <paramref name="unit"/> has spent its answer on this map this phase.</summary>
    public static bool Spent(BattleState state, BattleUnit unit) =>
        state.Map.OneAnswerEnabled && unit.Answered;

    /// <summary>
    /// Whether a combat's strikes spend <paramref name="defenderId"/>'s answer on
    /// <paramref name="map"/>: the header is on and the defender struck back at least once.
    /// </summary>
    public static bool Spends(MapDefinition map, string defenderId, ValueList<StrikeEvent> strikes) =>
        map.OneAnswerEnabled && strikes.Any(s => s.AttackerId == defenderId);
}
