namespace Ironwake.Core;

/// <summary>
/// Light's cleanse (issue 1321, Lotus's second spell rulings, DECISIONS/0317: "a cleanse that clears burn, chill and
/// stun"; issue 1328 adds curse, issue 1330 the freeze). A healing spell that names <c>cleanses</c> (<see cref="Weapon.Cleanses"/>) is cast through the Item action
/// on an ally in its range, checked as a heal is (a class that heals with its type, the rank, a use left, no art).
/// <list type="bullet">
/// <item>It clears the ally's burn (every stack and its phase count), its chill, its stun, its curse and its freeze, and heals nothing.</item>
/// <item>A lock is the chill turned all the way (<see cref="Lock"/>), so clearing the chill drops the lock with it.</item>
/// <item>An ally skipping this phase to a stun is freed: it may move and act again, unless it is also resting.</item>
/// <item>It is refused (<see cref="RejectionReason.NothingToCleanse"/>) on an ally that carries none of the five.</item>
/// <item>The healer spends one use and its action, and earns a heal's EXP and rank as for an ally above half HP.</item>
/// </list>
/// Everything it clears is board state, so Recall restores it with the board. <see cref="Resolver.Legal"/> offers it
/// on an afflicted ally in range; the Sim's player and the enemy planner never cast it until a shipped unit carries one.
/// </summary>
public static class Cleanse
{
    /// <summary>Whether <paramref name="unit"/> carries anything a cleanse clears: a burn, a chill, a stun, a curse, or a freeze.</summary>
    public static bool Afflicted(BattleUnit unit) => unit.BurnPhases > 0 || unit.Chill > 0 || unit.Stun > 0 || unit.CursePhases > 0 || unit.Frozen > 0;

    /// <summary>
    /// <paramref name="target"/> cleansed by <paramref name="byId"/>: its burn, chill (and lock), stun and curse cleared, and
    /// a stun being skipped this phase lifted unless it is resting. Emits <see cref="UnitCleansed"/>.
    /// </summary>
    public static BattleUnit Clear(BattleUnit target, string byId, List<GameEvent> events)
    {
        var freed = target.Stun == 2 && target.Spent != 2;
        events.Add(new UnitCleansed(target.Id, byId, target.BurnPhases > 0, target.Chill > 0, target.Stun > 0, freed, target.CursePhases > 0, target.Frozen > 0));
        var cleared = Curse.Cleared(target with { Burn = 0, BurnStacks = 0, BurnPhases = 0, Chill = 0, LockedBy = null, Stun = 0, Frozen = 0 });
        return freed ? cleared with { Moved = false, Acted = false } : cleared;
    }

}
