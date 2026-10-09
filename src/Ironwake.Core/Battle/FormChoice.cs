namespace Ironwake.Core;

/// <summary>
/// When a unit declares a form on a <c>forms: on</c> map (issues 1461, 1489): one rule for the enemy planner and
/// the Sim's player alike (DECISIONS/0400). By default the capped expected-damage rule (Table rounds 560, 561):
/// a form is offered when its expected damage, each outcome capped at the target's current HP, is strictly higher
/// than the plain strike's, doubles and crits counted on both; ties go to the plain strike. Under
/// <c>form_rule: lethal</c> (<see cref="MapDefinition.FormRuleLethal"/>) 0400's rule: a form is offered when it kills
/// on its hit and the plain strike does not.
/// </summary>
public static class FormChoice
{
    /// <summary>
    /// The attacker's expected damage on a target at <paramref name="targetHp"/>, each outcome capped at that HP: every
    /// strike it lives to make (<see cref="CombatForecast.AttackerStrikesLivedFor"/>) misses, hits or crits on the
    /// displayed hit and the crit chance, its first strike carrying its Stoop when it hits, its first hit a mark's gain and a shell's loss. 0 when it
    /// does not strike. A counter that kills between the attacker's rounds is read as the forecast's "if all land" reads it.
    /// </summary>
    public static double CappedExpected(CombatForecast forecast, int targetHp, int attackerHp)
    {
        var side = forecast.Attacker;
        if (!side.Strikes)
        {
            return 0;
        }

        var strikes = forecast.AttackerStrikesLivedFor(attackerHp);
        var hit = side.DisplayedHit / 100.0;
        var crit = side.CritChance / 100.0;

        double From(int strike, int dealt, bool landed)
        {
            if (dealt >= targetHp || strike == strikes)
            {
                return Math.Min(dealt, targetHp);
            }

            var stoop = strike == 0 ? side.Stoop : 0;
            var plain = (landed ? side.Damage : side.FirstHit(crit: false)) + stoop;
            var crits = (landed ? side.CritDamage : side.FirstHit(crit: true)) + stoop;
            return (1 - hit) * From(strike + 1, dealt, landed)
                + hit * (1 - crit) * From(strike + 1, dealt + plain, true)
                + hit * crit * From(strike + 1, dealt + crits, true);
        }

        return From(0, 0, false);
    }

    /// <summary>
    /// The forms <paramref name="unit"/> is offered on its strike on <paramref name="target"/> from <paramref name="from"/>
    /// with <paramref name="slot"/>'s weapon under the map's rule, in the order the unit knows them
    /// (<see cref="GameContent.FormsOf"/>), each with whether the unit's Grit pays for it now. Every offer is listed
    /// whatever its Grit, read as if the unit held <see cref="Grit.Cap"/>, so Grit's kill criterion can count the
    /// ones it priced out. Empty off a <c>forms: on</c> map and for a boss under the veto (which prices plain
    /// weapons only, DECISIONS/0251). A form that costs the next phase is never listed.
    /// </summary>
    public static IReadOnlyList<FormOffer> Offers(BattleState state, GameContent content, BattleUnit unit, Coord from, BattleUnit target, int slot)
    {
        if (!state.Map.FormsEnabled || EnemyAi.BossVetoApplies(state, content, unit))
        {
            return Array.Empty<FormOffer>();
        }

        var plain = Queries.Forecast(state, content, unit, target, from, slot);
        if (plain is null)
        {
            return Array.Empty<FormOffer>();
        }

        var lethal = state.Map.FormRuleLethal;
        if (lethal && plain.Attacker.Strikes && plain.Attacker.Damage >= target.Hp)
        {
            return Array.Empty<FormOffer>();
        }

        var plainExpected = CappedExpected(plain, target.Hp, unit.Hp);
        var flush = unit with { Grit = Grit.Cap };
        var board = state.WithUnit(flush);
        var offers = new List<FormOffer>();
        foreach (var (ability, art) in content.FormsOf(unit, true))
        {
            if (art.CostsNextPhase)
            {
                continue;
            }

            var forecast = Queries.Forecast(board, content, flush, target, from, slot, ability.Id);
            if (forecast is null || !forecast.Attacker.Strikes)
            {
                continue;
            }

            var expected = CappedExpected(forecast, target.Hp, unit.Hp);
            if (lethal ? forecast.Attacker.Damage >= target.Hp : expected > plainExpected)
            {
                offers.Add(new FormOffer(ability.Id, art.Grit, unit.Grit >= art.Grit, forecast.Attacker.HitChance) { Weapon = art.Weapon, Expected = expected });
            }
        }

        return offers;
    }

    /// <summary>
    /// The form declared from <paramref name="offers"/>: of the affordable ones, the best expected damage under the
    /// capped rule or the best hit under <c>form_rule: lethal</c>, the first known on a tie; null when none is affordable.
    /// </summary>
    public static string? Pick(MapDefinition map, IEnumerable<FormOffer> offers)
    {
        FormOffer? pick = null;
        foreach (var offer in offers.Where(o => o.Affordable))
        {
            if (pick is null || (map.FormRuleLethal ? offer.Hit > pick.Hit : offer.Expected > pick.Expected))
            {
                pick = offer;
            }
        }

        return pick?.Id;
    }
}
