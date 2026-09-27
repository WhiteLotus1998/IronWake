namespace Ironwake.Core;

/// <summary>
/// The cost of a grudge on a <c>grudges: on</c> map (DESIGN.md 13.4, experiment, issue 331):
/// a player unit an enemy is sworn against (<see cref="BattleUnit.Grudge"/>) fights that one
/// enemy at <see cref="SwornCritAvoid"/> crit avoid, on every strike between the two, the
/// sworn enemy's counters included, on either phase. It sits in rivalry's modifier slot
/// (<see cref="Combatant.CritAvoidModifier"/>), so a sworn unit beside a rival fights at
/// one resolved crit and every forecast, <c>threat</c>, the planner's strike choice and the
/// resolver read it from the same combatant. The one exception is the Sim's exposure sum
/// (<see cref="Exposure"/>), which builds the combatant with no opponent: its no-crit sum, the
/// veto's, is unaffected, and its with-crit sum does not see the sworn penalty (issue 402).
/// </summary>
public static class Grudges
{
    /// <summary>The crit avoid a sworn unit loses against the enemy sworn on it (Design Table, fifty-seventh round).</summary>
    public const int SwornCritAvoid = -20;

    /// <summary>
    /// The crit avoid modifier <paramref name="unit"/> fights <paramref name="opponent"/> with:
    /// <see cref="SwornCritAvoid"/> when the opponent is sworn against it, else 0.
    /// </summary>
    public static int CritAvoidAgainst(BattleUnit unit, BattleUnit? opponent) =>
        opponent is { Grudge: { } id } && id == unit.Id && opponent.Side != unit.Side ? SwornCritAvoid : 0;
}
