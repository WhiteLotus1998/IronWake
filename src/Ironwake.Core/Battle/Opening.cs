namespace Ironwake.Core;

/// <summary>
/// The Vanguard's Opening (issue 772, round 236; DESIGN section 3). A holder of an <see cref="OpeningEffect"/>
/// that attacks and lands any hit, one for 0 damage included, on an enemy that lives through the combat opens
/// it (<see cref="BattleUnit.Open"/>, the event <see cref="UnitOpened"/>):
/// <list type="bullet">
/// <item>Every strike on the open unit by a unit of another side, other than its opener, reads its Def and Res
/// lower by the effect's numbers, never below 0 (<see cref="Lowered"/>), read where the struck side answers
/// (<see cref="BattleUnit.ToCombatant(BattleState, GameContent, bool, CombatArtEffect?, BattleUnit?, Weapon?)"/>), so
/// the forecast, both planners and the resolver read one number.</item>
/// <item>The opener's own strikes never read it; a counter never opens; a miss opens nothing.</item>
/// <item>A second opening refreshes the mark and takes nothing more: it never stacks.</item>
/// <item>The mark clears when the phase ends, whichever side's (<see cref="Resolver"/>'s end of phase). It is board state, so Recall restores it.</item>
/// </list>
/// </summary>
public static class Opening
{
    /// <summary>Whether a strike on <paramref name="struck"/> by <paramref name="striker"/> reads the open mark: the struck unit is open, and the striker is of another side and not its opener.</summary>
    public static bool Reads(BattleUnit struck, BattleUnit? striker) =>
        struck.Open is { } open && striker is not null && striker.Side != struck.Side && striker.Id != open.By;

    /// <summary>The delta <paramref name="open"/> adds to a unit whose stats read <paramref name="stats"/>: Def and Res lowered by the mark's numbers, each never below 0.</summary>
    public static Stats Lowered(OpenMark open, Stats stats) =>
        Stats.Zero with { Def = -Math.Min(open.Def, Math.Max(0, stats.Def)), Res = -Math.Min(open.Res, Math.Max(0, stats.Res)) };

    /// <summary>
    /// After <paramref name="attacker"/>'s attack on <paramref name="targetId"/>: when the attacker holds Opening, any of
    /// its strikes in <paramref name="strikes"/> hit the target, and the target still stands in <paramref name="state"/>,
    /// the target is open to the attacker's allies (<see cref="UnitOpened"/>). Only the attack opens, never the counter.
    /// </summary>
    public static BattleState AfterAttack(BattleState state, GameContent content, BattleUnit attacker, string targetId, ValueList<StrikeEvent> strikes, List<GameEvent> events)
    {
        if (AbilityRules.Opening(content.AbilitiesOf(attacker.Unit)) is not { } opening
            || !strikes.Any(s => s.AttackerId == attacker.Id && s.TargetId == targetId && s.Hit)
            || state.Find(targetId) is not { } target)
        {
            return state;
        }

        events.Add(new UnitOpened(target.Id, attacker.Id, opening.Def, opening.Res));
        return state.WithUnit(target with { Open = new OpenMark(attacker.Id, opening.Def, opening.Res) });
    }

    /// <summary>The unit card's line for an open unit: <c>open: allies of Captain strike at Def -3, Res -3 until player phase ends</c>; null when it is not open.</summary>
    public static string? CardLine(BattleState state, BattleUnit unit, UnitNames names) =>
        unit.Open is { } open ? $"open: allies of {names[open.By]} strike at Def -{open.Def}, Res -{open.Res} until {Frost.Word(state.Phase)} phase ends" : null;
}

/// <summary>An open mark (issue 772, <see cref="Opening"/>): the unit that opened it and how far Def and Res read lower.</summary>
public sealed record OpenMark(string By, int Def, int Res);
