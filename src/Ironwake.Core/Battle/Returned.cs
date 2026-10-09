namespace Ironwake.Core;

/// <summary>
/// Which claimant came back as a foe, and whose talk turns them (issue 633, DESIGN section 14's
/// branch): <paramref name="UnitId"/> is the passed claimant fighting for the enemy,
/// <paramref name="PickId"/> the claimant the company took in their place.
/// </summary>
public sealed record ReturnBond(string UnitId, string PickId);

/// <summary>How the returned claimant left the board (issue 633).</summary>
public enum ReturnFate
{
    /// <summary>Talked round by the pick: they leave the field, and join the company only if a bed is free.</summary>
    Turned,

    /// <summary>Talked round by the captain: they leave the field alive and ride on, never joining.</summary>
    Spared,

    /// <summary>Killed, or removed from the board any way but a talk.</summary>
    Fell,
}

/// <summary>
/// The return (issue 633, DESIGN section 14; provisional): on the campaign map whose
/// <see cref="CampaignMap.Return"/> places them, the claimant passed on at the branch fights for the
/// enemy with their own card at the pick's level (<see cref="BattleState.Return"/>). Until they leave
/// they are an ordinary enemy of their group, and the planner does not read the bond. The pick or the
/// captain, orthogonally beside them, may <see cref="Talk"/> to them as the unit's action, in place of
/// Attack, Item or Wait, after its Move or without one, whether the group is awake or not. The pick's
/// talk turns them; the captain's spares them (STORY draft 6: only the pick's talk wins them back).
/// Either way they leave the board, which is not a kill: no EXP, no <see cref="UnitDied"/>, one
/// <see cref="UnitTalked"/>. No Move Again follows. The board and <c>threat</c> print the bond
/// (<see cref="Line"/>).
/// </summary>
public static class Returned
{
    /// <summary>Whether <paramref name="unit"/> is the returned claimant on the board.</summary>
    public static bool Is(BattleState state, BattleUnit unit) =>
        state.Return is { } bond && unit.Side == Side.Enemy && unit.Id == bond.UnitId;

    /// <summary>
    /// The clause a lethal forecast row opens with when the unit it would kill is the returned claimant
    /// (issue 1068, Design Table round 372): <c>Rook falls for good (the claimant)</c>, by the name a
    /// reader sees, on either arm of the branch. It names the loss and nothing more: no ending, no veto.
    /// Null for any other unit; whether the row is lethal is the caller's check.
    /// </summary>
    public static string? Falls(BattleState state, BattleUnit unit, UnitNames names) =>
        Is(state, unit) ? $"{names[unit.Id]} falls for good (the claimant)" : null;

    /// <summary>
    /// <paramref name="rest"/> with <paramref name="falls"/> before it (issue 1068): the cost first, then the
    /// gain, <c>Rook falls for good (the claimant); Keziah +10 HP</c>. <paramref name="rest"/> alone when null.
    /// </summary>
    public static string Lead(string? falls, string rest) => falls is null ? rest : $"{falls}; {rest}";

    /// <summary>The returned claimant on the board, or null when the battle has none or they have left it.</summary>
    public static BattleUnit? On(BattleState state) =>
        state.Return is { } bond && state.Find(bond.UnitId) is { Side: Side.Enemy } unit ? unit : null;

    /// <summary>
    /// Why <paramref name="unit"/> cannot talk to <paramref name="targetId"/>, in the order the rules
    /// are checked, or null when it can. Whether the unit may act at all is the caller's check.
    /// </summary>
    public static string? Refusal(BattleState state, BattleUnit unit, string targetId)
    {
        if (state.Find(targetId) is not { } target || !Is(state, target))
        {
            return $"{unit.Id} cannot talk to {targetId}: only a claimant who came back as a foe can be talked round";
        }

        if (unit.Side != Side.Player || (!unit.IsCaptain && unit.Id != state.Return!.PickId))
        {
            return $"{unit.Id} cannot talk to {target.Id}: only {state.Return!.PickId} or the captain can";
        }

        if (unit.At.DistanceTo(target.At) != 1)
        {
            return $"{unit.Id} cannot talk to {target.Id}: talking needs them side by side";
        }

        return null;
    }

    /// <summary>The fate a talk by <paramref name="talker"/> gives: the pick's turns, the captain's spares.</summary>
    public static ReturnFate FateOf(BattleState state, BattleUnit talker) =>
        state.Return is { } bond && talker.Id == bond.PickId ? ReturnFate.Turned : ReturnFate.Spared;

    /// <summary>
    /// The bond as the board and <c>threat</c> print it while the returned claimant stands:
    /// <c>Keziah came back with the enemy: Rook's talk turns her, the captain's spares her</c>,
    /// with the pick's clause left out once the pick is off the board. Null otherwise.
    /// </summary>
    public static string? Line(BattleState state, GameContent content, UnitNames names)
    {
        if (On(state) is not { } returned)
        {
            return null;
        }

        var them = Referent.For(content, returned.Unit).Object;
        var pick = state.Find(state.Return!.PickId) is { Side: Side.Player } ? $"{names[state.Return.PickId]}'s talk turns {them}, " : "";
        return $"{names[returned.Id]} came back with the enemy: {pick}the captain's spares {them}";
    }
}
