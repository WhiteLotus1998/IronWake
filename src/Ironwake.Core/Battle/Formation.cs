namespace Ironwake.Core;

/// <summary>
/// The captain's formation effects (issue 705, DESIGN section 3), read on the board where units stand:
/// the Vanguard's stats with an ally beside it (<see cref="BesideStatsEffect"/>) and the Marshal's aura
/// on the allies near it (<see cref="AuraEffect"/>). <see cref="BattleUnit.ToCombatant(BattleState, GameContent, bool, CombatArtEffect?, BattleUnit?)"/>
/// reads both, so every forecast, <c>threat</c>, both planners and the resolver read one number. A unit is
/// read at its own <see cref="BattleUnit.At"/>, so a forecast may pass it at a tile it has not moved to;
/// every other unit is read where it stands on the board.
/// </summary>
public static class Formation
{
    /// <summary>
    /// What <paramref name="unit"/>'s <see cref="BesideStatsEffect"/> adds here: their sum when a living unit
    /// of its side other than itself stands orthogonally beside its tile, else zero.
    /// </summary>
    public static Stats Beside(BattleState state, GameContent content, BattleUnit unit)
    {
        if (!Holds<BesideStatsEffect>(content, unit.Unit))
        {
            return Stats.Zero;
        }

        return state.Units.Any(u => u.Side == unit.Side && u.Id != unit.Id && u.At.DistanceTo(unit.At) == 1)
            ? AbilityRules.Beside(content.AbilitiesOf(unit.Unit))
            : Stats.Zero;
    }

    /// <summary>
    /// The aura <paramref name="unit"/> fights under: for each distinct <see cref="AuraEffect"/> held by a unit
    /// of its side other than itself within that aura's radius of <paramref name="unit"/>'s tile, its hit and
    /// avoid, each aura counted once however many hold it.
    /// </summary>
    public static CombatBonus Aura(BattleState state, GameContent content, BattleUnit unit)
    {
        var bonus = CombatBonus.None;
        List<string>? counted = null;
        foreach (var holder in state.Units)
        {
            if (holder.Side != unit.Side || holder.Id == unit.Id || !Holds<AuraEffect>(content, holder.Unit))
            {
                continue;
            }

            foreach (var ability in content.AbilitiesOf(holder.Unit))
            {
                if (ability.Effect is not AuraEffect aura || holder.At.DistanceTo(unit.At) > aura.Radius)
                {
                    continue;
                }

                counted ??= new List<string>();
                if (counted.Contains(ability.Id))
                {
                    continue;
                }

                counted.Add(ability.Id);
                bonus = new CombatBonus(bonus.Hit + aura.Hit, bonus.Avoid + aura.Avoid, bonus.Crit, bonus.CritAvoid);
            }
        }

        return bonus;
    }

    /// <summary>Whether <paramref name="unit"/> holds an ability of effect <typeparamref name="T"/>, its own or its class's, without resolving the list.</summary>
    private static bool Holds<T>(GameContent content, Unit unit)
        where T : AbilityEffect
    {
        foreach (var id in unit.Abilities)
        {
            if (content.Ability(id).Effect is T)
            {
                return true;
            }
        }

        foreach (var id in content.Class(unit.ClassId).Abilities)
        {
            if (content.Ability(id).Effect is T)
            {
                return true;
            }
        }

        return false;
    }
}
