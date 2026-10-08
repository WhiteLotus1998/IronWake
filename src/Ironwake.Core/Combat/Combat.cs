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
    public const int AvoidSpeedWeight = 2;

    /// <summary>Weight less the whole of Str, never below zero (issue 158, DECISIONS/0028).</summary>
    public static int Burden(Combatant unit)
    {
        if (unit.Weapon is null)
        {
            return 0;
        }

        return Math.Max(0, unit.Weapon.Wt - unit.Stats.Str);
    }

    public static int AttackSpeed(Combatant unit) => unit.Stats.Spd - Burden(unit);

    /// <summary>
    /// Attack speed at least <see cref="DoubleThreshold"/> over the target's; never for a side the pair rule holds (<see cref="Combatant.PairHeld"/>),
    /// one striking with a single-strike art (<see cref="Combatant.SingleStrike"/>), or one in a single-strike class (<see cref="UnitClass.SingleStrike"/>, issue 872), striking or countering.
    /// </summary>
    public static bool Doubles(Combatant attacker, Combatant target) =>
        !attacker.PairHeld && !attacker.SingleStrike && !attacker.Class.SingleStrike && AttackSpeed(attacker) >= AttackSpeed(target) + DoubleThreshold;

    /// <summary>The weapon's Mt as this side fights with it: the content number, less 5 (floored at zero) when the weapon is broken.</summary>
    public static int Mt(Combatant attacker)
    {
        var weapon = Armed(attacker);
        return attacker.Broken ? Math.Max(0, weapon.Mt - BrokenMtPenalty) : weapon.Mt;
    }

    /// <summary>
    /// Whether the attacker's weapon is effective against the target: it names the target's movement type, or it is
    /// effective against Hollows and the target is one (issue 1321). Either way the multiplier is the one
    /// <see cref="EffectiveMultiplier"/>, never stacked.
    /// </summary>
    public static bool IsEffective(Combatant attacker, Combatant target)
    {
        var weapon = Armed(attacker);
        return weapon.IsEffectiveAgainst(target.Movement) || (weapon.EffectiveAgainstHollows && target.Hollow);
    }

    /// <summary>Str or Mag plus Mt, with Mt tripled first when the weapon is effective against the target (<see cref="IsEffective"/>).</summary>
    public static int Atk(Combatant attacker, Combatant target)
    {
        var weapon = Armed(attacker);
        var mt = IsEffective(attacker, target) ? Mt(attacker) * EffectiveMultiplier : Mt(attacker);
        return (weapon.IsMagic ? attacker.Stats.Mag : attacker.Stats.Str) + mt;
    }

    /// <summary>Atk less the target's Def or Res with its terrain's, never below 0; <paramref name="shell"/> is Def a shell adds against a physical strike (issue 1403).</summary>
    public static int Damage(Combatant attacker, Combatant target, int shell = 0)
    {
        var defence = Armed(attacker).IsMagic
            ? target.Stats.Res + target.Terrain.ResFor(target.Movement)
            : target.Stats.Def + shell + target.Terrain.DefFor(target.Movement);
        return Math.Max(0, Atk(attacker, target) - defence);
    }

    /// <summary>Weapon hit (less 10 when broken) plus Dex plus half Lck, plus the hit modifier.</summary>
    public static int Hit(Combatant attacker) =>
        Armed(attacker).Hit - (attacker.Broken ? BrokenHitPenalty : 0) + attacker.Stats.Dex + attacker.Stats.Lck / 2 + attacker.HitModifier;

    /// <summary>
    /// Section 5's Faith heal: Mag / 2 + 5 + the spell's base, times the healer's mending factor
    /// (issue 706, <see cref="AbilityRules.HealFactor"/>; 1 for everyone but the Field Surgeon).
    /// <paramref name="spell"/> must be a healing spell.
    /// </summary>
    public static int Heal(Combatant healer, Weapon spell)
    {
        if (!spell.Heals)
        {
            throw new ArgumentException($"{spell.Id} is not a healing spell", nameof(spell));
        }

        return (healer.Stats.Mag / 2 + HealBase + spell.HealBase) * AbilityRules.HealFactor(healer.Abilities);
    }

    /// <summary>Avoid against a physical or a magic strike; magic ignores burden. Attack speed counts twice against a physical strike.</summary>
    public static int Avoid(Combatant target, bool againstMagic)
    {
        var terrain = target.Terrain.AvoidFor(target.Movement);
        if (againstMagic)
        {
            return (target.Stats.Spd + target.Stats.Lck) / 2 + terrain;
        }

        return AvoidSpeedWeight * AttackSpeed(target) + target.Stats.Lck / 2 + terrain;
    }

    /// <summary>
    /// Hit less avoid, clamped to 0..100, with each side's on-combat ability modifiers
    /// against the other added first (issue 66): the attacker's hit, the target's avoid.
    /// </summary>
    public static int HitChance(Combatant attacker, Combatant target) =>
        Math.Clamp(
            Hit(attacker) + AbilityRules.Against(attacker, target).Hit
            - Avoid(target, Armed(attacker).IsMagic) - AbilityRules.Against(target, attacker).Avoid,
            0,
            100);

    public static int Crit(Combatant attacker) =>
        Armed(attacker).Crit + (attacker.Stats.Dex + attacker.Stats.Lck) / 2 + attacker.CritModifier;

    /// <summary>Lck plus modifiers, deliberately unclamped: a negative crit avoid is a cost a modifier may impose.</summary>
    public static int CritAvoid(Combatant target) => target.Stats.Lck + target.CritAvoidModifier;

    /// <summary>
    /// Crit less crit avoid, clamped to 0..100, with the on-combat ability modifiers added as for <see cref="HitChance"/>
    /// and the weapon's bonus against the target's movement type (issue 703, <see cref="Weapon.CritBonusAgainst"/>);
    /// 0 when the pair rule holds the attacker (<see cref="Combatant.PairHeld"/>).
    /// </summary>
    public static int CritChance(Combatant attacker, Combatant target) =>
        attacker.PairHeld ? 0 : Math.Clamp(
            Crit(attacker) + Armed(attacker).CritBonusAgainst(target.Movement) + AbilityRules.Against(attacker, target).Crit
            - CritAvoid(target) - AbilityRules.Against(target, attacker).CritAvoid,
            0,
            100);

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

    /// <summary>
    /// The resolved probability as the forecast prints it: a percentage rounded to the nearest
    /// integer, halves away from zero, then held inside 1 to 99 unless the hit is certain or
    /// impossible, so 100 prints only for a hit that cannot miss and 0 only for one that cannot
    /// land (issue 452): a raw 95 under two rolls lands 99.55 percent and prints 99.
    /// </summary>
    public static int DisplayedHit(int hitChance, RollScheme scheme)
    {
        var probability = HitProbability(hitChance, scheme);
        var rounded = (int)Math.Round(probability * 100, MidpointRounding.AwayFromZero);
        return probability switch
        {
            <= 0 => 0,
            >= 1 => 100,
            _ => Math.Clamp(rounded, 1, 99),
        };
    }

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

        var defenderSide = defender.CanStrike(distance) ? ForSide(defender, attacker, distance, scheme) : SideForecast.None;
        return new CombatForecast(ForSide(attacker, defender, distance, scheme), defenderSide, scheme) { CounterAnswered = defender.AnswerSpent && (defender with { AnswerSpent = false }).CanStrike(distance) };
    }

    /// <summary>
    /// What <paramref name="striker"/>'s drake bites for at <paramref name="distance"/>
    /// (issue 872, <see cref="BiteEffect"/>): its bite at its drake's stage when adjacent, else 0.
    /// </summary>
    public static int Bite(Combatant striker, int distance) =>
        distance == 1 ? AbilityRules.Bite(striker.Abilities, striker.Unit) : 0;

    private static SideForecast ForSide(Combatant striker, Combatant target, int distance, RollScheme scheme)
    {
        var hit = HitChance(striker, target);
        var raw = Damage(striker, target);
        var scale = LightningRod.Scale(striker, target);
        return new SideForecast(
            Strikes: true,
            Damage: scale.Of(raw),
            HitChance: hit,
            DisplayedHit: DisplayedHit(hit, scheme),
            CritChance: CritChance(striker, target),
            Doubles: Doubles(striker, target),
            StrikesPerRound: Armed(striker).Type.StrikesPerRound(),
            CritGrounds: Armed(striker).GroundsAgainst(target.Movement),
            Bite: Bite(striker, distance),
            NeverDoubles: striker.Class.SingleStrike,
            Stoop: striker.Stoop,
            CashesMark: Mark.Cashes(striker, target)) { Unscaled = scale.IsOne ? null : raw, Scale = scale, Shell = target.Shell, Shelled = target.Shell > 0 ? Damage(striker, target, target.Shell) : null };
    }

    private static Weapon Armed(Combatant unit) =>
        unit.Weapon ?? throw new ArgumentException($"{unit.Id} has no weapon to strike with", nameof(unit));
}
