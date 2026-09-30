namespace Ironwake.Core;

/// <summary>
/// Cover (DESIGN.md 13.19, experiment), behind a map's <c>cover: on</c> header. A player unit
/// orthogonally beside an ally may take <see cref="Cover"/> on it as its action. Until the
/// covered ally's side's next phase begins, the first Attack aimed at the ally while the two
/// still stand side by side swaps them before the combat: the coverer steps onto the ally's
/// tile and takes the fight there, counter and all, on the ally's terrain, and the ally lands
/// on the coverer's old tile. The cover is then spent. The cover is the ally's
/// <see cref="BattleUnit.CoveredBy"/>, so Recall restores it with the unit. One cover per
/// ally, and no chains: a covering unit cannot be covered and a covered unit cannot cover.
/// Watch shots and burns are not Attacks and pass it by. The enemy planner prices every
/// strike on a covered ally through the swap (<see cref="Swapped"/>), so a cover mostly turns
/// a strike away; <see cref="PassedLine"/> says when it did. The enemy never covers in the spike.
/// </summary>
public static class CoverRule
{
    /// <summary>Why <paramref name="unit"/> cannot cover <paramref name="ally"/> now, or null when it can.</summary>
    public static string? Refusal(BattleState state, BattleUnit unit, BattleUnit ally) =>
        !state.Map.CoverEnabled ? "this map has no cover (cover: on)"
        : unit.Side != Side.Player ? $"{unit.Id} is an enemy; the enemy never covers"
        : ally.Id == unit.Id ? $"{unit.Id} cannot cover itself"
        : ally.Side != unit.Side ? $"{ally.Id} is not {unit.Id}'s ally"
        : unit.At.DistanceTo(ally.At) != 1 ? $"{ally.Id} at {ally.At} is not beside {unit.Id} at {unit.At}"
        : ally.CoveredBy is { } already ? $"{ally.Id} is already covered by {already}"
        : unit.CoveredBy is { } mine ? $"{unit.Id} is covered by {mine} and cannot cover"
        : Covering(state, ally) is { } other ? $"{ally.Id} is covering {other.Id} and cannot be covered"
        : null;

    /// <summary>The ally <paramref name="unit"/> is covering, or null.</summary>
    public static BattleUnit? Covering(BattleState state, BattleUnit unit) =>
        state.Units.FirstOrDefault(u => u.CoveredBy == unit.Id);

    /// <summary>
    /// The board a strike aimed at <paramref name="target"/> resolves on, and the unit it
    /// strikes: when a living coverer stands beside the target, the two swapped, the
    /// coverer on the target's tile and the target on the coverer's, the cover spent.
    /// Null when no cover fires, so the strike lands on the target as aimed.
    /// </summary>
    public static (BattleState Board, BattleUnit Struck, BattleUnit Ally)? Swapped(BattleState state, BattleUnit target)
    {
        if (!state.Map.CoverEnabled || target.CoveredBy is not { } id || state.Find(id) is not { } coverer || coverer.At.DistanceTo(target.At) != 1)
        {
            return null;
        }

        var struck = coverer with { At = target.At };
        var ally = target with { At = coverer.At, CoveredBy = null };
        return (state.WithUnit(struck).WithUnit(ally), struck, ally);
    }

    /// <summary>The board with every cover lifted: what the planner would see had nobody covered.</summary>
    public static BattleState Lifted(BattleState state) =>
        state.Units.Any(u => u.CoveredBy is not null)
            ? state with { Units = ValueList<BattleUnit>.From(state.Units.Select(u => u with { CoveredBy = null })) }
            : state;

    /// <summary>
    /// The line an enemy phase prints before <paramref name="command"/> when it is the enemy's
    /// first command and the cover turned it away (round 142): the planner with the covers
    /// lifted would strike a covered ally, and on the board as it stands it does not strike
    /// that ally. <c>soldier-1 passed ottilie (covered by teodor)</c>. Null otherwise. One
    /// extra plan per enemy, only while a cover stands.
    /// </summary>
    public static string? PassedLine(BattleState state, GameContent content, Command command)
    {
        var id = command switch { Move m => m.UnitId, Attack a => a.UnitId, Wait w => w.UnitId, Watch w => w.UnitId, Retreat r => r.UnitId, _ => null };
        if (id is null || !state.Map.CoverEnabled || state.Find(id) is not { Side: Side.Enemy, Moved: false, Acted: false } unit
            || !state.Units.Any(u => u.CoveredBy is not null))
        {
            return null;
        }

        var bare = Target(EnemyAi.PlanUnit(Lifted(state), content, unit));
        if (bare is null || state.Find(bare) is not { CoveredBy: { } coverer } || Target(EnemyAi.PlanUnit(state, content, unit)) == bare)
        {
            return null;
        }

        return $"{id} passed {bare} (covered by {coverer})";
    }

    private static string? Target(IReadOnlyList<Command> plan) => plan.OfType<Attack>().FirstOrDefault()?.TargetId;
}
