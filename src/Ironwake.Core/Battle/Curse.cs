namespace Ironwake.Core;

/// <summary>
/// Dark's curse (issue 1328, Lotus's round-3 spell rulings, DECISIONS/0322). A hit from a tome naming its school's
/// <see cref="RiderKind.Curse"/> rider on a unit that survives the combat curses it:
/// <list type="bullet">
/// <item>At the start of each of its own side's next <see cref="SchoolRider.Phases"/> phases it loses its
/// <see cref="Tick"/>, <c>max(1, amount - Res / 2)</c>, never below 1 HP, read beside burn and after it.</item>
/// <item>Each tick heals the caster (<see cref="BattleUnit.CursedBy"/>) by what it actually took, up to max HP. A tick on
/// a unit at 1 HP takes nothing and so heals nothing; a caster off the board is healed by no one, and the tick still lands.</item>
/// <item>While it runs the unit strikes and counters at <see cref="SchoolRider.Blind"/> less Hit (<see cref="HitOf"/>),
/// in the striker's hit slot every forecast, <c>threat</c>, the planner and the resolver read
/// (<see cref="Brace.StrikeHit"/>).</item>
/// <item>One curse a unit: it never stacks and is no burn stack. It sits beside burn, outside burn's cap, and both tick.
/// A second curse refreshes the count and takes its caster as the one it pays.</item>
/// <item>A miss does nothing, a kill leaves no one to curse, and a Hollow is never the caster: its hits lay none.</item>
/// <item>Light's cleanse clears it (<see cref="Cleanse"/>).</item>
/// </list>
/// A learned school's rider fires only past its gate (<see cref="LearnedGate"/>). Everything is board state
/// (<see cref="BattleUnit.Curse"/>, <see cref="BattleUnit.CurseBlind"/>, <see cref="BattleUnit.CursePhases"/>,
/// <see cref="BattleUnit.CursedBy"/>), so Recall restores it with the board. No shipped tome names it until Lotus signs
/// its numbers (#1247); the planners do not price it yet.
/// </summary>
public static class Curse
{
    /// <summary>Whether <paramref name="weapon"/> names its school's curse rider.</summary>
    public static bool Curses(GameContent content, Weapon? weapon) =>
        content.RiderOf(weapon) is { Kind: RiderKind.Curse };

    /// <summary>
    /// After a combat or a strike: each unit in <paramref name="strikes"/> still standing that a hit from a cursing
    /// tome landed on is cursed (<see cref="UnitCursed"/>), its count refreshed and its caster the striker.
    /// <paramref name="a"/> and <paramref name="b"/> are the two as they entered the combat, whose gate is read;
    /// else as <paramref name="state"/> finds them.
    /// </summary>
    public static BattleState AfterCombat(BattleState state, GameContent content, string aId, Weapon? aWeapon, string bId, Weapon? bWeapon, ValueList<StrikeEvent> strikes, List<GameEvent> events, BattleUnit? a = null, BattleUnit? b = null)
    {
        a ??= state.Find(aId);
        b ??= state.Find(bId);
        foreach (var (strikerId, weapon, targetId, striker, struck) in new[] { (aId, aWeapon, bId, a, b), (bId, bWeapon, aId, b, a) })
        {
            if (content.RiderOf(weapon) is not { Kind: RiderKind.Curse } rider || striker is null || striker.Hollow is not null
                || !LearnedGate.Fires(content, striker, weapon, struck) || !strikes.Any(s => s.AttackerId == strikerId && s.TargetId == targetId && s.Hit))
            {
                continue;
            }

            if (state.Find(targetId) is not { } target)
            {
                continue;
            }

            var laid = Laid(target, rider, strikerId);
            events.Add(new UnitCursed(target.Id, strikerId, Tick(content, laid), laid.CursePhases, laid.CurseBlind));
            state = state.WithUnit(laid);
        }

        return state;
    }

    /// <summary><paramref name="unit"/> cursed by <paramref name="casterId"/> with <paramref name="rider"/>'s numbers: a fresh count, never a stack.</summary>
    public static BattleUnit Laid(BattleUnit unit, SchoolRider rider, string casterId) =>
        unit with { Curse = rider.Amount, CurseBlind = rider.Blind, CursePhases = rider.Phases, CursedBy = casterId };

    /// <summary>What <paramref name="unit"/>'s curse takes at its side's next phase start: <c>max(1, amount - Res / 2)</c> while it runs, else 0. The HP floor of 1 is the phase start's.</summary>
    public static int Tick(GameContent content, BattleUnit unit) =>
        unit.CursePhases > 0 ? Math.Max(1, unit.Curse - content.StatsOf(unit.Unit).Res / 2) : 0;

    /// <summary>The Hit <paramref name="striker"/> strikes and counters at: minus its blind while it is cursed, else 0.</summary>
    public static int HitOf(BattleUnit? striker) =>
        striker is { CursePhases: > 0 } ? -striker.CurseBlind : 0;

    /// <summary><paramref name="unit"/> with its curse's count turned once, as its side's phase begins: cleared when the count runs out.</summary>
    public static BattleUnit Ticked(BattleUnit unit) =>
        unit.CursePhases > 1 ? unit with { CursePhases = unit.CursePhases - 1 } : Cleared(unit);

    /// <summary><paramref name="unit"/> with no curse on it.</summary>
    public static BattleUnit Cleared(BattleUnit unit) =>
        unit with { Curse = 0, CurseBlind = 0, CursePhases = 0, CursedBy = null };

    /// <summary>
    /// At a phase start, after heal and burn: <paramref name="hp"/> less <paramref name="unit"/>'s tick, never below 1 HP.
    /// A tick that takes something is a <see cref="CurseTicked"/> and owes its caster that much
    /// (<paramref name="owed"/>, paid by <see cref="PayCasters"/> once every unit has ticked).
    /// </summary>
    public static int TickAtPhaseStart(GameContent content, BattleUnit unit, int hp, List<(string CasterId, string FromId, int Amount)> owed)
    {
        var after = Math.Max(Math.Min(1, hp), hp - Tick(content, unit));
        if (after < hp && unit.CursedBy is { } caster)
        {
            owed.Add((caster, unit.Id, hp - after));
        }

        return after;
    }

    /// <summary>
    /// Pays each tick in <paramref name="owed"/>, in board order: one <see cref="CurseTicked"/> for the tick, its caster
    /// healed by what it took up to max HP when the caster is on the board, and by nothing when it is not.
    /// </summary>
    public static BattleState PayCasters(BattleState state, GameContent content, List<(string CasterId, string FromId, int Amount)> owed, List<GameEvent> events)
    {
        foreach (var (casterId, fromId, amount) in owed)
        {
            var hpAfter = state.Find(fromId)?.Hp ?? 0;
            if (state.Find(casterId) is { } caster)
            {
                var healed = Math.Min(amount, Math.Max(0, caster.MaxHp(content) - caster.Hp));
                events.Add(new CurseTicked(fromId, amount, hpAfter, casterId, healed, caster.Hp + healed));
                state = state.WithUnit(caster with { Hp = caster.Hp + healed });
            }
            else
            {
                events.Add(new CurseTicked(fromId, amount, hpAfter, null, 0, 0));
            }
        }

        return state;
    }

    /// <summary>The forecast's words for a side whose weapon curses: <c> curses</c>; empty when it does not.</summary>
    public static string ForecastText(GameContent content, Weapon? weapon) =>
        Curses(content, weapon) ? " curses" : "";

    /// <summary>The unit card's line for a cursed unit: <c>cursed: -30 Hit, 3 a phase to Pell (2 phases)</c>; null when it is not cursed.</summary>
    public static string? CardLine(GameContent content, BattleUnit unit, UnitNames names) =>
        unit is { CursePhases: > 0, CursedBy: { } by }
            ? $"cursed: -{unit.CurseBlind} Hit, {Tick(content, unit)} a phase to {names[by]} ({unit.CursePhases} {(unit.CursePhases == 1 ? "phase" : "phases")})"
            : null;
}
