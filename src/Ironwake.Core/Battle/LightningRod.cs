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
/// </summary>
public static class LightningRod
{
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
