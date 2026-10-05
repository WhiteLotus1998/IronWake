namespace Ironwake.Core;

/// <summary>Where a combat happens in the battle, the part of every combat roll's key that the combat itself does not know.</summary>
public readonly record struct CombatContext(int Turn, Side Phase);

/// <summary>One strike, as the event stream reports it (DESIGN.md section 5).</summary>
public sealed record StrikeEvent(int Index, string AttackerId, string TargetId, bool Hit, bool Crit, int Damage, int TargetHpAfter);

/// <summary>
/// A drake's bite after the exchange (issue 872, <see cref="BiteEffect"/>): the rider whose drake bit, the
/// unit bitten, the damage, and that unit's HP after it. Not a strike: nothing that reads strikes sees it.
/// </summary>
public sealed record BiteEvent(string RiderId, string TargetId, int Damage, int TargetHpAfter);

/// <summary>
/// A resolved combat: every strike in order and both sides' HP when it ended, after a drake's
/// <see cref="Bite"/> when one bit (issue 872), so a bite that kills is the combat's kill.
/// </summary>
public sealed record CombatResult(ValueList<StrikeEvent> Strikes, int AttackerHp, int DefenderHp)
{
    /// <summary>The drake's bite after the exchange, or null when none bit.</summary>
    public BiteEvent? Bite { get; init; }

    public bool AttackerDied => AttackerHp == 0;

    public bool DefenderDied => DefenderHp == 0;
}

/// <summary>
/// Fights a combat strike by strike under the section 5 sequence: the attacker strikes,
/// the defender counters if its weapon reaches, then whoever doubles strikes again; the
/// combat ends early on a death. Each of those turns is a round of the side's
/// <see cref="SideForecast.StrikesPerRound"/> strikes, two for gauntlets (issue 70), and
/// a death ends the combat mid-round. Every roll comes from <see cref="IRng"/> under the key
/// <see cref="RollKey.Combat"/> documents, and the crit roll is drawn only when the hit landed.
/// When the exchange is over and both stand, a side with a <see cref="SideForecast.Bite"/> one of whose
/// strikes hit bites once (issue 872): the attacker's drake, or else the defender's; one bite a combat.
/// The bite draws no roll. A side's <see cref="SideForecast.Stoop"/> is added to its first strike when it hits (issue 1127).
/// </summary>
public static class CombatResolver
{
    public static CombatResult Resolve(
        Combatant attacker, Combatant defender, int distance, CombatContext context, IRng rng, RollScheme scheme)
    {
        var forecast = Combat.Forecast(attacker, defender, distance, scheme);
        var strikes = ValueList<StrikeEvent>.Empty;
        var attackerHp = attacker.Hp;
        var defenderHp = defender.Hp;
        var attackerStrikes = 0;
        var defenderStrikes = 0;

        Round(attacker, defender, forecast.Attacker, ref defenderHp, ref attackerStrikes);
        if (defenderHp > 0 && forecast.Defender.Strikes)
        {
            Round(defender, attacker, forecast.Defender, ref attackerHp, ref defenderStrikes);
        }

        if (attackerHp > 0 && defenderHp > 0)
        {
            if (forecast.Attacker.Doubles)
            {
                Round(attacker, defender, forecast.Attacker, ref defenderHp, ref attackerStrikes);
            }
            else if (forecast.Defender.Strikes && forecast.Defender.Doubles)
            {
                Round(defender, attacker, forecast.Defender, ref attackerHp, ref defenderStrikes);
            }
        }

        BiteEvent? bite = null;
        if (forecast.Attacker.Bite > 0 && attackerHp > 0 && defenderHp > 0 && strikes.Any(s => s.Hit && s.AttackerId == attacker.Id))
        {
            defenderHp = Math.Max(0, defenderHp - forecast.Attacker.Bite);
            bite = new BiteEvent(attacker.Id, defender.Id, forecast.Attacker.Bite, defenderHp);
        }
        else if (forecast.Defender.Bite > 0 && attackerHp > 0 && defenderHp > 0 && strikes.Any(s => s.Hit && s.AttackerId == defender.Id))
        {
            attackerHp = Math.Max(0, attackerHp - forecast.Defender.Bite);
            bite = new BiteEvent(defender.Id, attacker.Id, forecast.Defender.Bite, attackerHp);
        }

        return new CombatResult(strikes, attackerHp, defenderHp) { Bite = bite };

        void Round(Combatant striker, Combatant target, SideForecast side, ref int targetHp, ref int strikeIndex)
        {
            for (var i = 0; i < side.StrikesPerRound && targetHp > 0; i++)
            {
                Strike(striker, target, side, ref targetHp, strikeIndex++);
            }
        }

        void Strike(Combatant striker, Combatant target, SideForecast side, ref int targetHp, int strikeIndex)
        {
            var rollA = rng.Roll(RollKey.Combat(context.Turn, context.Phase, striker.Id, target.Id, strikeIndex, CombatRoll.HitA));
            var rollB = scheme == RollScheme.TwoRollAverage
                ? rng.Roll(RollKey.Combat(context.Turn, context.Phase, striker.Id, target.Id, strikeIndex, CombatRoll.HitB))
                : 0;
            var hit = Combat.Lands(side.HitChance, rollA, rollB, scheme);
            var crit = hit
                && rng.Roll(RollKey.Combat(context.Turn, context.Phase, striker.Id, target.Id, strikeIndex, CombatRoll.Crit)) < side.CritChance;
            var damage = !hit ? 0 : (crit ? side.CritDamage : side.Damage) + (strikeIndex == 0 ? side.Stoop : 0);
            targetHp = Math.Max(0, targetHp - damage);
            strikes = strikes.Add(new StrikeEvent(strikes.Count, striker.Id, target.Id, hit, crit, damage, targetHp));
        }
    }
}
