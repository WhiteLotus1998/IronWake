namespace Ironwake.Core;

/// <summary>
/// An area cast (issue 1329, Lotus's round-3 spell rulings, DECISIONS/0322: Spark Storm). A tome that names <c>area</c>
/// (<see cref="Weapon.Area"/>, the radius) is cast through the Item action at a unit or a tile in its range,
/// <c>item &lt;unit&gt; &lt;slot&gt; &lt;unit|x,y&gt;</c>, checked as a strike's tome is (the caster may wield it, a use left, no art):
/// <list type="bullet">
/// <item>Every enemy of the caster within the radius of that tile is struck once, in board order: one forecast, one hit
/// roll keyed by caster and target, no double, and no counter. The caster's side is never struck.</item>
/// <item>It is refused when no enemy is in the area, when the tile is out of range, and when the caster's side cannot
/// see the tile at dusk. A Lightning Rod catches only a single-target cast, so it never catches this; no cover swaps.</item>
/// <item>One use and the action are spent. The caster earns one combat's EXP, from the struck unit that pays most (a kill
/// pays its kill), and one combat's rank and mastery: an area is not an EXP farm.</item>
/// <item>A marking tome marks every unit it hits that survives, cashing any mark already on it first (<see cref="Mark"/>).</item>
/// </list>
/// An area tome is never equipped (<see cref="BattleUnit.UsableWeaponAt"/>), so it never attacks and never counters.
/// <see cref="Resolver.Legal"/>, the Sim's player and the enemy planner never cast it until a shipped unit carries one.
/// </summary>
public static class AreaCast
{
    /// <summary>The tile a cast names: a living unit's, or <c>x,y</c> on the map; null when it names neither.</summary>
    public static Coord? TileOf(BattleState state, string target) =>
        state.Find(target) is { } unit ? unit.At
        : Sunder.TileOf(target) is { } tile && state.Map.Contains(tile) ? tile
        : null;

    /// <summary>Every enemy of <paramref name="caster"/> within <paramref name="spell"/>'s radius of <paramref name="at"/>, board order.</summary>
    public static IReadOnlyList<BattleUnit> Struck(BattleState state, BattleUnit caster, Weapon spell, Coord at) =>
        state.Units.Where(u => u.Side != caster.Side && u.At.DistanceTo(at) <= spell.Area).ToList();

    /// <summary>
    /// The forecast of <paramref name="caster"/>'s cast at <paramref name="at"/> on <paramref name="target"/>: one strike, no
    /// counter, read at the cast's distance (the tile's, in range), since a unit at the area's edge may stand beyond it.
    /// </summary>
    public static CombatForecast Forecast(BattleState state, GameContent content, BattleUnit caster, Weapon spell, BattleUnit target, Coord at) =>
        Combat.Forecast(
            caster.ToCombatant(state, content, against: target, casting: spell),
            target.ToCombatant(state, content, countering: true, against: caster) with { Blind = true },
            caster.At.DistanceTo(at),
            state.Scheme);

    /// <summary>
    /// The preview's line for the cast: <c>Test Storm at 6,5 strikes brigand-1 acc 80% dmg 3 (hp 18), marks; ...</c>,
    /// or what refuses it.
    /// </summary>
    public static string Preview(BattleState state, GameContent content, BattleUnit caster, Weapon spell, Coord at)
    {
        var struck = Struck(state, caster, spell, at);
        if (struck.Count == 0)
        {
            return $"{spell.Name} at {at} would strike no one: no enemy within {spell.Area} of it";
        }

        return $"{spell.Name} at {at} strikes " + string.Join("; ", struck.Select(target =>
        {
            var side = Forecast(state, content, caster, spell, target, at).Attacker;
            var damage = side.CashesMark ? side.MarkedDamage : side.Damage;
            return $"{target.Id} acc {side.DisplayedHit}% dmg {damage} (hp {target.Hp}){Mark.ForecastText(side)}{(spell.Marks ? ", marks" : "")}";
        })) + ", no counter";
    }
}
