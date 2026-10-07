namespace Ironwake.Core;

/// <summary>
/// A school's stun rider (issue 1244, DESIGN section 5's schools; lightning's, the storm-warden's alone).
/// A hit from a tome that names its school's <see cref="RiderKind.Stun"/> rider, struck by a caster
/// whose class the rider names (<see cref="SchoolRider.Classes"/>), on a unit that survives the combat,
/// stuns it: its side's next phase it neither moves nor acts, and it still counters.
/// <list type="bullet">
/// <item>A miss does nothing, and a kill leaves no one to stun.</item>
/// <item>Bosses are spared: the hit lands, the stun does nothing to a boss.</item>
/// <item>Once a map per caster (<see cref="BattleUnit.StunSpent"/>), spent only when it stuns.</item>
/// </list>
/// The clock counts as the chill's does (<see cref="Frost.AtPhaseChange"/>): a stun sets 1, the
/// stunned side's phase beginning turns 1 to 2 and marks the unit moved and acted
/// (<see cref="StunSkipped"/>), and that phase ending clears 2. Everything is board state
/// (<see cref="BattleUnit.Stun"/>), so Recall restores it with the board.
/// </summary>
public static class Stun
{
    /// <summary>The stun rider <paramref name="weapon"/> carries in <paramref name="caster"/>'s hands, spent or not; null when the tome carries none or the caster's class is not one the rider names.</summary>
    public static SchoolRider? Rider(GameContent content, BattleUnit? caster, Weapon? weapon) =>
        caster is not null && content.RiderOf(weapon) is { Kind: RiderKind.Stun } rider && rider.Classes.Contains(caster.Unit.ClassId) ? rider : null;

    /// <summary>Whether a hit by <paramref name="caster"/> with <paramref name="weapon"/> would stun <paramref name="target"/> were it to survive: the rider fires, unspent, and the target is no boss.</summary>
    public static bool Stuns(GameContent content, BattleUnit? caster, Weapon? weapon, BattleUnit target) =>
        Rider(content, caster, weapon) is not null && !caster!.StunSpent && !target.IsBoss;

    /// <summary>Whether <paramref name="unit"/> is skipping the phase under way: stunned, its clock begun on its own side's phase.</summary>
    public static bool Skipping(BattleUnit unit) => unit.Stun == 2;

    /// <summary>
    /// After a combat or a strike: each unit in <paramref name="strikes"/> still standing that a hit
    /// from a stunning caster landed on (<see cref="Stuns"/>) is stunned (<see cref="UnitStunned"/>),
    /// its clock set to 1, and the caster's stun is spent. <paramref name="aWeapon"/> is what
    /// <paramref name="aId"/> struck with and <paramref name="bWeapon"/> what <paramref name="bId"/> did.
    /// </summary>
    public static BattleState AfterCombat(BattleState state, GameContent content, string aId, Weapon? aWeapon, string bId, Weapon? bWeapon, ValueList<StrikeEvent> strikes, List<GameEvent> events)
    {
        foreach (var (strikerId, weapon, targetId) in new[] { (aId, aWeapon, bId), (bId, bWeapon, aId) })
        {
            if (state.Find(strikerId) is not { } caster || state.Find(targetId) is not { } target
                || !Stuns(content, caster, weapon, target)
                || !strikes.Any(s => s.AttackerId == strikerId && s.TargetId == targetId && s.Hit))
            {
                continue;
            }

            events.Add(new UnitStunned(target.Id, caster.Id, target.Side, target.Side == state.Phase));
            state = state.WithUnit(target with { Stun = 1 }).WithUnit(caster with { StunSpent = true });
        }

        return state;
    }

    /// <summary>The forecast's words for a side whose weapon stuns, after its strike columns: <c> stuns</c>, <c> stun: bosses spared</c> against a boss, <c> stun spent</c> once spent; empty when it carries none.</summary>
    public static string ForecastText(GameContent content, BattleUnit caster, Weapon? weapon, BattleUnit target) =>
        Rider(content, caster, weapon) is null ? ""
        : caster.StunSpent ? " stun spent"
        : target.IsBoss ? " stun: bosses spared"
        : " stuns";

    /// <summary>The unit card's line for a stunned unit: <c>stunned: skips its next phase</c>, or <c>stunned: skips this phase</c> while it is skipping; null when it is not stunned.</summary>
    public static string? CardLine(BattleUnit unit) =>
        unit.Stun switch
        {
            1 => "stunned: skips its next phase",
            2 => "stunned: skips this phase",
            _ => null,
        };
}
