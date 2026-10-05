namespace Ironwake.Core;

/// <summary>
/// Brace (DESIGN.md 13.14, experiment), behind a map's <c>brace: on</c> header: a unit that
/// takes Wait on the tile it began its phase on braces (a Guard of a sleeping group does not),
/// and until its side's next phase begins every strike against it is at <see cref="Hit"/> less
/// hit. The brace is decided when the Wait is taken and stored on the unit, never evaluated at
/// strike time, so a group woken later in the phase is not braced. An ally's shove takes it
/// off. It sits in the striker's hit slot beside the pincer (<see cref="StrikeHit"/>), so every
/// forecast, <c>threat</c>, the planner's score and the resolver read one number, and a pin and
/// a brace cancel. A braced unit is never struck by a counter, since it cannot strike before
/// its brace ends.
/// </summary>
public static class Brace
{
    /// <summary>The hit a strike against a braced unit loses (DESIGN.md 13.14, provisional).</summary>
    public const int Hit = 15;

    /// <summary>
    /// Whether <paramref name="unit"/> braces when it waits now: on a <c>brace: on</c> map, or on
    /// any map for a unit holding Banked (issue 691, <see cref="BraceEffect"/>), only
    /// on the tile it began its phase on (it has neither moved nor been shoved), and never a
    /// Guard whose group still sleeps, since a sleeper caught off guard is the ambush the wake
    /// rule promises.
    /// </summary>
    public static bool BracesOnWait(BattleState state, GameContent content, BattleUnit unit) =>
        (state.Map.BraceEnabled || AbilityRules.Braces(content.AbilitiesOf(unit.Unit))) && !unit.Moved && !unit.Shoved && !Asleep(state, unit);

    private static bool Asleep(BattleState state, BattleUnit unit) =>
        unit.Behavior == Behavior.Guard && unit.Group is { } group && !state.IsAwake(group);

    /// <summary>The hit modifier a strike against <paramref name="target"/> carries: minus <see cref="Hit"/> when it is braced, else 0.</summary>
    public static int HitAgainst(BattleUnit? target) =>
        target is { Braced: true } ? -Hit : 0;

    /// <summary>
    /// The hit modifier the board gives <paramref name="striker"/> against <paramref name="target"/>:
    /// the pin (<see cref="Pincer.HitAgainst"/>) plus the brace plus the winded mark of a dash
    /// (<see cref="Winded.HitAgainst"/>, DESIGN.md 13.27). The one function
    /// <see cref="BattleUnit.ToCombatant(BattleState, GameContent, bool, CombatArtEffect?, BattleUnit?)"/>
    /// and <see cref="EnemyAi.Score"/> both call, so the planner's target choice reads what the
    /// forecast and the resolver read.
    /// </summary>
    public static int StrikeHit(BattleState state, BattleUnit striker, BattleUnit? target) =>
        Pincer.HitAgainst(state, striker, target) + HitAgainst(target) + Winded.HitAgainst(target);
}
