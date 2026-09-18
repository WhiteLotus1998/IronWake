namespace Ironwake.Core;

/// <summary>
/// The formulas of DESIGN.md section 5, one function each, and the forecast built from
/// them. All division is integer floor. The forecast, the resolver, and the AI scorer
/// (issue 10) all price a hit through <see cref="HitProbability"/> and nothing else.
/// </summary>
public static class Combat
{
    public const int DoubleThreshold = 4;
    public const int CritMultiplier = 3;
    public const int EffectiveMultiplier = 3;
    public const int BrokenMtPenalty = 5;
    public const int BrokenHitPenalty = 10;
    public const int HealBase = 5;

    public static int Burden(Combatant unit) =>
        unit.Weapon is null ? 0 : Math.Max(0, unit.Weapon.Wt - unit.Stats.Str / 5);

    public static int AttackSpeed(Combatant unit) => unit.Stats.Spd - Burden(unit);

    public static bool Doubles(Combatant attacker, Combatant target) =>
        AttackSpeed(attacker) >= AttackSpeed(target) + DoubleThreshold;

    /// <summary>The weapon's Mt as this side fights with it: the content number, less 5 (floored at zero) when the weapon is broken.</summary>
    public static int Mt(Combatant attacker)
    {
        var weapon = Armed(attacker);
        return attacker.Broken ? Math.Max(0, weapon.Mt - BrokenMtPenalty) : weapon.Mt;
    }

    /// <summary>Str or Mag plus Mt, with Mt tripled first when the weapon is effective against the target.</summary>
    public static int Atk(Combatant attacker, Combatant target)
    {
        var weapon = Armed(attacker);
        var mt = weapon.IsEffectiveAgainst(target.Movement) ? Mt(attacker) * EffectiveMultiplier : Mt(attacker);
        return (weapon.IsMagic ? attacker.Stats.Mag : attacker.Stats.Str) + mt;
    }

    public static int Damage(Combatant attacker, Combatant target)
    {
        var defence = Armed(attacker).IsMagic
            ? target.Stats.Res + target.Terrain.ResFor(target.Movement)
            : target.Stats.Def + target.Terrain.DefFor(target.Movement);
        return Math.Max(0, Atk(attacker, target) - defence);
    }

    /// <summary>Weapon hit (less 10 when broken) plus Dex plus half Lck.</summary>
    public static int Hit(Combatant attacker) =>
        Armed(attacker).Hit - (attacker.Broken ? BrokenHitPenalty : 0) + attacker.Stats.Dex + attacker.Stats.Lck / 2;

    /// <summary>Section 5's Faith heal: Mag / 2 + 5 + the spell's base. <paramref name="spell"/> must be a healing spell.</summary>
    public static int Heal(Combatant healer, Weapon spell)
    {
        if (!spell.Heals)
        {
            throw new ArgumentException($"{spell.Id} is not a healing spell", nameof(spell));
        }

        return healer.Stats.Mag / 2 + HealBase + spell.HealBase;
    }

    /// <summary>Avoid against a physical or a magic strike; magic ignores burden.</summary>
    public static int Avoid(Combatant target, bool againstMagic)
    {
        var terrain = target.Terrain.AvoidFor(target.Movement);
        return againstMagic
            ? (target.Stats.Spd + target.Stats.Lck) / 2 + terrain
            : AttackSpeed(target) + target.Stats.Lck / 2 + terrain;
    }

    public static int HitChance(Combatant attacker, Combatant target) =>
        Math.Clamp(Hit(attacker) - Avoid(target, Armed(attacker).IsMagic), 0, 100);

    public static int Crit(Combatant attacker) =>
        Armed(attacker).Crit + (attacker.Stats.Dex + attacker.Stats.Lck) / 2;

    /// <summary>Lck plus modifiers, deliberately unclamped: a negative crit avoid is a cost a modifier may impose.</summary>
    public static int CritAvoid(Combatant target) => target.Stats.Lck + target.CritAvoidModifier;

    public static int CritChance(Combatant attacker, Combatant target) =>
        Math.Clamp(Crit(attacker) - CritAvoid(target), 0, 100);

    /// <summary>
    /// The probability a strike with <paramref name="hitChance"/> lands under
    /// <paramref name="scheme"/>. Under two rolls a hit lands when the floor of the two
    /// rolls' average is below the chance, so the probability is the share of the 10000
    /// ordered pairs whose sum is below twice the chance: a raw 75 lands 87.75 percent of
    /// the time, a raw 30 lands 18.3.
    /// </summary>
    public static double HitProbability(int hitChance, RollScheme scheme)
    {
        if (hitChance < 0 || hitChance > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(hitChance), hitChance, "hit chance must be 0..100");
        }

        switch (scheme)
        {
            case RollScheme.OneRoll:
                return hitChance / 100.0;
            case RollScheme.TwoRollAverage:
                var pairs = 0;
                for (var a = 0; a < 100; a++)
                {
                    pairs += Math.Clamp(2 * hitChance - a, 0, 100);
                }

                return pairs / 10000.0;
            default:
                throw new ArgumentOutOfRangeException(nameof(scheme), scheme, "unknown roll scheme");
        }
    }

    /// <summary>The resolved probability as the forecast prints it: a percentage rounded to the nearest integer, halves away from zero.</summary>
    public static int DisplayedHit(int hitChance, RollScheme scheme) =>
        (int)Math.Round(HitProbability(hitChance, scheme) * 100, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Whether a strike lands given its rolls: under one roll the first roll alone is read;
    /// under two the floor of their average. The resolver and the statistical test share this.
    /// </summary>
    public static bool Lands(int hitChance, int rollA, int rollB, RollScheme scheme) => scheme switch
    {
        RollScheme.OneRoll => rollA < hitChance,
        RollScheme.TwoRollAverage => (rollA + rollB) / 2 < hitChance,
        _ => throw new ArgumentOutOfRangeException(nameof(scheme), scheme, "unknown roll scheme"),
    };

    /// <summary>
    /// The forecast for an attacker striking a defender <paramref name="distance"/> tiles
    /// away. The attacker must be able to strike at that distance; the defender counters
    /// only when its weapon reaches back.
    /// </summary>
    public static CombatForecast Forecast(Combatant attacker, Combatant defender, int distance, RollScheme scheme)
    {
        if (!attacker.CanStrike(distance))
        {
            throw new ArgumentException($"{attacker.Id} cannot strike at distance {distance}", nameof(distance));
        }

        var defenderSide = defender.CanStrike(distance) ? ForSide(defender, attacker, scheme) : SideForecast.None;
        return new CombatForecast(ForSide(attacker, defender, scheme), defenderSide, scheme);
    }

    private static SideForecast ForSide(Combatant striker, Combatant target, RollScheme scheme)
    {
        var hit = HitChance(striker, target);
        return new SideForecast(
            Strikes: true,
            Damage: Damage(striker, target),
            HitChance: hit,
            DisplayedHit: DisplayedHit(hit, scheme),
            CritChance: CritChance(striker, target),
            Doubles: Doubles(striker, target));
    }

    private static Weapon Armed(Combatant unit) =>
        unit.Weapon ?? throw new ArgumentException($"{unit.Id} has no weapon to strike with", nameof(unit));
}
