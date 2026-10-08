namespace Ironwake.Core;

/// <summary>
/// Spark Storm's mark (issue 1329, Lotus's round-3 spell rulings, DECISIONS/0322). A hit from a tome that
/// <see cref="Weapon.Marks"/> on a unit that survives it marks the unit with the tome's school
/// (<see cref="BattleUnit.Mark"/>):
/// <list type="bullet">
/// <item>The first hit on a marked unit from any tome of that school, from any caster, by a strike or a counter, deals
/// <see cref="Times"/> its damage (<see cref="SideForecast.MarkedDamage"/>) and spends the mark. A miss keeps it; a
/// doubled second strike deals plain damage.</item>
/// <item>The multiple applies to final damage, after Def or Res and after the crit, rounded down once (DECISIONS/0322:
/// status and spell multipliers multiply on final damage; effectiveness stays on Mt).</item>
/// <item>A marking hit on a marked unit cashes the old mark, then lays its own: the unit ends marked.</item>
/// <item>The mark has no clock. It lasts until it is cashed or the map ends.</item>
/// <item>A Lightning Rod's catch marks no one, whatever it caught (issue 1400, the partners' lean in DECISIONS/0351): a
/// caught hit on the holder cashes a mark he carries and lays none.</item>
/// </list>
/// The mark is board state, so Recall restores it with the board. No shipped tome marks until Lotus signs Spark Storm's
/// numbers (#1247).
/// </summary>
public static class Mark
{
    /// <summary>The mark's multiple as a fraction, x1.5 (Lotus's rulings): the numerator.</summary>
    public const int Numerator = 3;

    /// <summary>The mark's multiple as a fraction, x1.5 (Lotus's rulings): the denominator.</summary>
    public const int Denominator = 2;

    /// <summary>The mark's multiple as a <see cref="DamageScale"/>.</summary>
    public static DamageScale Scale { get; } = new(Numerator, Denominator);

    /// <summary>The multiple as a line prints it.</summary>
    public const string Times = "x1.5";

    /// <summary><paramref name="damage"/> marked: times the multiple, rounded down once.</summary>
    public static int Of(int damage) => damage * Numerator / Denominator;

    /// <summary>Whether <paramref name="striker"/>'s weapon cashes <paramref name="target"/>'s mark: a tome of the school it is marked with.</summary>
    public static bool Cashes(Combatant striker, Combatant target) =>
        striker.Weapon?.School is { } school && target.Marked == school;

    /// <summary>
    /// After a combat or a strike: for each side, the mark on the other is spent when a hit from a tome of its school
    /// landed (<see cref="MarkCashed"/>), and a hit from a marking tome on a unit still standing marks it
    /// (<see cref="UnitMarked"/>). <paramref name="a"/> and <paramref name="b"/> are the two as they entered the combat,
    /// whose marks are read; else as <paramref name="state"/> finds them. <paramref name="caughtId"/> names a rod holder struck
    /// by the spell it caught: <paramref name="a"/>'s hit on it lays no mark.
    /// </summary>
    public static BattleState AfterCombat(BattleState state, string aId, Weapon? aWeapon, string bId, Weapon? bWeapon, ValueList<StrikeEvent> strikes, List<GameEvent> events, BattleUnit? a = null, BattleUnit? b = null, string? caughtId = null)
    {
        a ??= state.Find(aId);
        b ??= state.Find(bId);
        foreach (var (strikerId, weapon, targetId, struck) in new[] { (aId, aWeapon, bId, b), (bId, bWeapon, aId, a) })
        {
            if (weapon?.School is not { } school || !strikes.Any(s => s.AttackerId == strikerId && s.TargetId == targetId && s.Hit)
                || state.Find(targetId) is not { } target)
            {
                continue;
            }

            if (struck?.Mark == school)
            {
                events.Add(new MarkCashed(targetId, strikerId, school));
                target = target with { Mark = null };
            }

            if (weapon.Marks && !(strikerId == aId && targetId == caughtId))
            {
                events.Add(new UnitMarked(targetId, strikerId, school));
                target = target with { Mark = school };
            }

            state = state.WithUnit(target);
        }

        return state;
    }

    /// <summary>The forecast's words for a side that cashes the mark on the unit it strikes: <c> cashes the mark (x1.5)</c>; empty otherwise.</summary>
    public static string ForecastText(SideForecast side) =>
        side.CashesMark ? $" cashes the mark ({Times})" : "";

    /// <summary>The unit card's line for a marked unit: <c>marked: next lightning x1.5</c>; null when it is not marked.</summary>
    public static string? CardLine(BattleUnit unit) =>
        unit.Mark is { } school ? $"marked: next {school.Label()} {Times}" : null;
}
