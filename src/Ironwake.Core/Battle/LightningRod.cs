namespace Ironwake.Core;

/// <summary>
/// Lightning Rod (issue 1280, Lotus's #1247 rulings: "Catchrod becomes Lightning Rod, a passive that works on
/// lightning spells only"; DESIGN section 5's schools). A holder of a <see cref="RodEffect"/> catches an attack
/// made with a tome of the rod's school and aimed at a unit of its own side, the holder itself aside, standing
/// within the rod's radius of it: the attack strikes the holder instead, and the holder counters as any struck
/// unit does, from where it stands.
/// <list type="bullet">
/// <item>The tome must reach the holder from the caster's tile; a bolt that cannot reach it passes to the aimed unit.</item>
/// <item>A stunned holder catches nothing; a fallen one is off the board.</item>
/// <item>Of two holders that would catch, the nearer to the aimed unit catches, then the first in unit order.</item>
/// </list>
/// The resolver, the forecast, <c>threat</c> and the enemy planner all read <see cref="Catcher"/>, so each sees the
/// strike land where it will. A cover swap (DESIGN 13.19) is read after it, on whoever is struck.
/// <para>
/// The catch's price and pay (issue 1329, Lotus's round-3 rulings, DECISIONS/0322 and 0327):
/// </para>
/// <list type="bullet">
/// <item>The caught spell's strikes on the holder deal <see cref="Half"/> (<see cref="Combatant.Catching"/>); his counter is plain.</item>
/// <item>A holder still standing after a catch is charged (<see cref="BattleUnit.RodCharge"/>, <see cref="RodCharged"/>). His next
/// cast of a tome of the rod's school, an attack, an area cast or a watch shot, deals <see cref="Boost"/> on every strike, and the
/// cast spends the charge, hit or miss (<see cref="RodChargeSpent"/>). A counter neither reads nor spends it. Two catches give one
/// charge. It ends with the map.</item>
/// <item>Both are status and spell multipliers: they multiply with the mark on final damage, rounded down once (<see cref="DamageScale"/>).</item>
/// </list>
/// </summary>
public static class LightningRod
{
    /// <summary>What a caught spell deals the holder: x0.5.</summary>
    public static DamageScale Half { get; } = new(1, 2);

    /// <summary>What a charged holder's next cast deals: x1.25.</summary>
    public static DamageScale Boost { get; } = new(5, 4);

    /// <summary>The charge's multiple as a line prints it.</summary>
    public const string BoostText = "x1.25";

    /// <summary>
    /// The rod's multiple on <paramref name="striker"/>'s strikes at <paramref name="target"/>: <see cref="Boost"/> when the striker
    /// is charged for its tome's school, times <see cref="Half"/> when the target is the holder struck by the spell it caught.
    /// </summary>
    public static DamageScale Scale(Combatant striker, Combatant target)
    {
        if (striker.Weapon?.School is not { } school)
        {
            return DamageScale.One;
        }

        var scale = striker.Charged == school ? Boost : DamageScale.One;
        return target.Catching ? scale.Times(Half) : scale;
    }

    /// <summary>
    /// After <paramref name="caster"/>'s cast with <paramref name="weapon"/>: the charge it held for the tome's school is spent, hit
    /// or miss (<see cref="RodChargeSpent"/>). The board unchanged when the caster held none for it or has fallen.
    /// </summary>
    public static BattleState Spend(BattleState state, BattleUnit caster, Weapon? weapon, List<GameEvent> events)
    {
        if (weapon?.School is not { } school || caster.RodCharge != school || state.Find(caster.Id) is not { } now)
        {
            return state;
        }

        events.Add(new RodChargeSpent(caster.Id, school));
        return state.WithUnit(now with { RodCharge = null });
    }

    /// <summary>
    /// After a combat in which <paramref name="holderId"/>'s rod caught a spell of <paramref name="school"/>: a holder still standing is
    /// charged (<see cref="RodCharged"/>). A charge already held stays one charge, and no event repeats it.
    /// </summary>
    public static BattleState Charge(BattleState state, string holderId, MagicSchool school, List<GameEvent> events)
    {
        if (state.Find(holderId) is not { } holder || holder.RodCharge == school)
        {
            return state;
        }

        events.Add(new RodCharged(holderId, school));
        return state.WithUnit(holder with { RodCharge = school });
    }

    /// <summary>The unit card's line for a charged holder: <c>charged: next lightning x1.25</c>; null when it holds no charge.</summary>
    public static string? CardLine(BattleUnit unit) =>
        unit.RodCharge is { } school ? $"charged: next {school.Label()} {BoostText}" : null;

    /// <summary>The forecast's words for a side the rod scales: <c> (charged x1.25)</c>, <c> (caught x0.5)</c>, or both; empty otherwise.</summary>
    public static string ForecastText(SideForecast side)
    {
        var charged = side.Scale == Boost || side.Scale == Boost.Times(Half);
        var caught = side.Scale == Half || side.Scale == Boost.Times(Half);
        return (charged, caught) switch
        {
            (true, true) => $" (charged {BoostText}, caught x0.5)",
            (true, false) => $" (charged {BoostText})",
            (false, true) => " (caught x0.5)",
            _ => "",
        };
    }

    /// <summary>
    /// The unit that catches an attack with <paramref name="weapon"/>, cast from <paramref name="from"/>, aimed at
    /// <paramref name="aimed"/> on <paramref name="state"/>; null when no rod catches it and the aimed unit is struck.
    /// </summary>
    public static BattleUnit? Catcher(BattleState state, GameContent content, Coord from, Weapon? weapon, BattleUnit aimed)
    {
        if (weapon?.School is not { } school)
        {
            return null;
        }

        return state.UnitsOf(aimed.Side)
            .Where(u => u.Id != aimed.Id && u.Stun == 0
                && AbilityRules.Rod(content.AbilitiesOf(u.Unit), school) is { } rod
                && u.At.DistanceTo(aimed.At) <= rod.Radius
                && weapon.InRange(from.DistanceTo(u.At)))
            .OrderBy(u => u.At.DistanceTo(aimed.At))
            .FirstOrDefault();
    }

    /// <summary>The forecast's words for an attack a rod catches: <c> (Lightning Rod: strikes Wren)</c>, the holder's ability named as content names it.</summary>
    public static string ForecastText(GameContent content, BattleUnit holder, string holderName, MagicSchool school) =>
        $" ({RodName(content, holder, school)}: strikes {holderName})";

    /// <summary>The name of the rod <paramref name="holder"/> catches <paramref name="school"/> with, as content names it.</summary>
    public static string RodName(GameContent content, BattleUnit holder, MagicSchool school) =>
        content.AbilitiesOf(holder.Unit).Where(a => a.Effect is RodEffect r && r.School == school).OrderByDescending(a => ((RodEffect)a.Effect).Radius).First().Name;
}
