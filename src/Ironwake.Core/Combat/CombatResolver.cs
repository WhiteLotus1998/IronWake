namespace Ironwake.Core;

/// <summary>Where a combat happens in the battle, the part of every combat roll's key that the combat itself does not know.</summary>
public readonly record struct CombatContext(int Turn, Side Phase);

/// <summary>One strike, as the event stream reports it (DESIGN.md section 5).</summary>
public sealed record StrikeEvent(int Index, string AttackerId, string TargetId, bool Hit, bool Crit, int Damage, int TargetHpAfter);

/// <summary>A resolved combat: every strike in order and both sides' HP when it ended.</summary>
public sealed record CombatResult(ValueList<StrikeEvent> Strikes, int AttackerHp, int DefenderHp)
{
    public bool AttackerDied => AttackerHp == 0;

    public bool DefenderDied => DefenderHp == 0;
}

/// <summary>
/// Fights a combat strike by strike under the section 5 sequence: the attacker strikes,
/// the defender counters if its weapon reaches, then whoever doubles strikes again; the
/// combat ends early on a death. Every roll comes from <see cref="IRng"/> under the key
/// <see cref="RollKey.Combat"/> documents, and the crit roll is drawn only when the hit landed.
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

        Strike(attacker, defender, forecast.Attacker, ref defenderHp, attackerStrikes++);
        if (defenderHp > 0 && forecast.Defender.Strikes)
        {
            Strike(defender, attacker, forecast.Defender, ref attackerHp, defenderStrikes++);
        }

        if (attackerHp > 0 && defenderHp > 0)
        {
            if (forecast.Attacker.Doubles)
            {
                Strike(attacker, defender, forecast.Attacker, ref defenderHp, attackerStrikes++);
            }
            else if (forecast.Defender.Strikes && forecast.Defender.Doubles)
            {
                Strike(defender, attacker, forecast.Defender, ref attackerHp, defenderStrikes++);
            }
        }

        return new CombatResult(strikes, attackerHp, defenderHp);

        void Strike(Combatant striker, Combatant target, SideForecast side, ref int targetHp, int strikeIndex)
        {
            var rollA = rng.Roll(RollKey.Combat(context.Turn, context.Phase, striker.Id, target.Id, strikeIndex, CombatRoll.HitA));
            var rollB = scheme == RollScheme.TwoRollAverage
                ? rng.Roll(RollKey.Combat(context.Turn, context.Phase, striker.Id, target.Id, strikeIndex, CombatRoll.HitB))
                : 0;
            var hit = Combat.Lands(side.HitChance, rollA, rollB, scheme);
            var crit = hit
                && rng.Roll(RollKey.Combat(context.Turn, context.Phase, striker.Id, target.Id, strikeIndex, CombatRoll.Crit)) < side.CritChance;
            var damage = !hit ? 0 : crit ? side.Damage * Combat.CritMultiplier : side.Damage;
            targetHp = Math.Max(0, targetHp - damage);
            strikes = strikes.Add(new StrikeEvent(strikes.Count, striker.Id, target.Id, hit, crit, damage, targetHp));
        }
    }
}
