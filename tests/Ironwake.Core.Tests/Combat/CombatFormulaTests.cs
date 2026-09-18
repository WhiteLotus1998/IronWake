using static Ironwake.Core.Tests.Combat.CombatFixture;

namespace Ironwake.Core.Tests.Combat;

/// <summary>DESIGN.md section 5's formulas with the fixture's hand-computed numbers.</summary>
public class CombatFormulaTests
{
    [Fact]
    public void BurdenIsWeightMinusAFifthOfStrengthFlooredAtZero()
    {
        Assert.Equal(4, Core.Combat.Burden(WrenOnPlain()));
        Assert.Equal(7, Core.Combat.Burden(BrigandInForest()));
        Assert.Equal(0, Core.Combat.Burden(new Combatant(Wren, Cadet, null, Plain, 20)));
        Assert.Equal(0, Core.Combat.Burden(new Combatant(Wren with { Stats = Wren.Stats with { Str = 30 } }, Cadet, IronSword, Plain, 20)));
    }

    [Fact]
    public void AttackSpeedIsSpeedMinusBurdenAndMayGoNegative()
    {
        Assert.Equal(4, Core.Combat.AttackSpeed(WrenOnPlain()));
        Assert.Equal(-3, Core.Combat.AttackSpeed(BrigandInForest()));
    }

    [Fact]
    public void DoublesAtFourAttackSpeedOverTheTarget()
    {
        Assert.True(Core.Combat.Doubles(WrenOnPlain(), BrigandInForest()));
        Assert.False(Core.Combat.Doubles(BrigandInForest(), WrenOnPlain()));

        var wren = WrenOnPlain();
        var threeSlower = new Combatant(Hexer, Adept, Spark, Plain, 16);
        Assert.Equal(3, Core.Combat.AttackSpeed(wren) - Core.Combat.AttackSpeed(threeSlower));
        Assert.False(Core.Combat.Doubles(wren, threeSlower));
    }

    [Fact]
    public void PhysicalDamageIsStrengthPlusMightMinusDefenceAndTerrain()
    {
        Assert.Equal(12, Core.Combat.Atk(WrenOnPlain(), BrigandInForest()));
        Assert.Equal(9, Core.Combat.Damage(WrenOnPlain(), BrigandInForest()));
        Assert.Equal(10, Core.Combat.Damage(BrigandInForest(), WrenOnPlain()));
    }

    [Fact]
    public void MagicDamageIsMagicPlusMightMinusResistance()
    {
        Assert.Equal(10, Core.Combat.Atk(HexerOnPlain(), WrenOnPlain()));
        Assert.Equal(8, Core.Combat.Damage(HexerOnPlain(), WrenOnPlain()));
    }

    [Fact]
    public void DamageFloorsAtZero()
    {
        var wall = new Combatant(Wren with { Stats = Wren.Stats with { Def = 30 } }, Cadet, IronSword, Plain, 20);

        Assert.Equal(0, Core.Combat.Damage(BrigandInForest(), wall));
    }

    [Fact]
    public void EffectiveWeaponsTripleMightBeforeAddingStrength()
    {
        Var(out var ridgeblade, new Combatant(Wren, Cadet, Ridgeblade, Plain, 20));

        Assert.Equal(7 + 18, Core.Combat.Atk(ridgeblade, RiderOnPlain()));
        Assert.Equal(7 + 6, Core.Combat.Atk(ridgeblade, BrigandInForest()));
    }

    [Fact]
    public void HitIsWeaponHitPlusDexterityPlusHalfLuck()
    {
        Assert.Equal(98, Core.Combat.Hit(WrenOnPlain()));
        Assert.Equal(73, Core.Combat.Hit(BrigandInForest()));
        Assert.Equal(91, Core.Combat.Hit(HexerOnPlain()));
    }

    [Fact]
    public void PhysicalAvoidIsAttackSpeedPlusHalfLuckPlusTerrain()
    {
        Assert.Equal(-3 + 0 + 20, Core.Combat.Avoid(BrigandInForest(), againstMagic: false));
        Assert.Equal(4 + 2 + 0, Core.Combat.Avoid(WrenOnPlain(), againstMagic: false));
    }

    [Fact]
    public void MagicAvoidIsHalfOfSpeedPlusLuckPlusTerrainIgnoringBurden()
    {
        Assert.Equal((8 + 5) / 2 + 0, Core.Combat.Avoid(WrenOnPlain(), againstMagic: true));
        Assert.Equal((4 + 1) / 2 + 20, Core.Combat.Avoid(BrigandInForest(), againstMagic: true));
    }

    [Fact]
    public void TerrainBonusesDoNotApplyToFlyersUnlessTheTerrainSaysSo()
    {
        var inForest = WingriderInForest();
        var onFort = WingriderOnFort();

        Assert.Equal(Core.Combat.AttackSpeed(inForest) + 3 / 2, Core.Combat.Avoid(inForest, againstMagic: false));
        Assert.Equal(Core.Combat.AttackSpeed(onFort) + 3 / 2 + 20, Core.Combat.Avoid(onFort, againstMagic: false));
        Assert.Equal(12 - 2, Core.Combat.Damage(WrenOnPlain(), inForest));
        Assert.Equal(12 - 4, Core.Combat.Damage(WrenOnPlain(), onFort));
    }

    [Fact]
    public void HitChanceIsHitMinusAvoidClampedToZeroToOneHundred()
    {
        Assert.Equal(81, Core.Combat.HitChance(WrenOnPlain(), BrigandInForest()));
        Assert.Equal(67, Core.Combat.HitChance(BrigandInForest(), WrenOnPlain()));
        Assert.Equal(85, Core.Combat.HitChance(HexerOnPlain(), WrenOnPlain()));

        var brigandOnPlain = new Combatant(Brigand, Reaver, HeavyAxe, Plain, 22);
        Assert.Equal(100, Core.Combat.HitChance(WrenOnPlain(), brigandOnPlain));
        var blind = new Combatant(Brigand with { Stats = Brigand.Stats with { Dex = 0, Lck = 0 } }, Reaver, HeavyAxe, Plain, 22);
        var untouchable = new Combatant(Wren with { Stats = Wren.Stats with { Spd = 60, Lck = 40 } }, Cadet, IronSword, Forest, 20);
        Assert.Equal(0, Core.Combat.HitChance(blind, untouchable));
    }

    [Fact]
    public void CritIsWeaponCritPlusHalfOfDexterityPlusLuck()
    {
        Assert.Equal(5, Core.Combat.Crit(WrenOnPlain()));
        Assert.Equal(2, Core.Combat.Crit(BrigandInForest()));
        Assert.Equal(10 + 5, Core.Combat.Crit(new Combatant(Wren, Cadet, Ridgeblade, Plain, 20)));
    }

    [Fact]
    public void CritChanceIsCritMinusLuckClamped()
    {
        Assert.Equal(4, Core.Combat.CritChance(WrenOnPlain(), BrigandInForest()));
        Assert.Equal(0, Core.Combat.CritChance(BrigandInForest(), WrenOnPlain()));
    }

    [Fact]
    public void CritAvoidIsNotClampedSoAModifierBelowZeroIsACost()
    {
        var showingOff = BrigandInForest(critAvoidModifier: -10);

        Assert.Equal(-9, Core.Combat.CritAvoid(showingOff));
        Assert.Equal(14, Core.Combat.CritChance(WrenOnPlain(), showingOff));
    }

    [Theory]
    [InlineData(75, 0.8775, 88)]
    [InlineData(30, 0.183, 18)]
    [InlineData(0, 0.0, 0)]
    [InlineData(100, 1.0, 100)]
    [InlineData(50, 0.505, 51)]
    [InlineData(81, 0.9297, 93)]
    [InlineData(67, 0.7855, 79)]
    public void TwoRollAverageResolvesToTheShareOfPairsBelowTwiceTheChance(int hitChance, double probability, int displayed)
    {
        Assert.Equal(probability, Core.Combat.HitProbability(hitChance, RollScheme.TwoRollAverage), 10);
        Assert.Equal(displayed, Core.Combat.DisplayedHit(hitChance, RollScheme.TwoRollAverage));
    }

    [Theory]
    [InlineData(75)]
    [InlineData(30)]
    [InlineData(0)]
    [InlineData(100)]
    public void OneRollResolvesToTheRawChance(int hitChance)
    {
        Assert.Equal(hitChance / 100.0, Core.Combat.HitProbability(hitChance, RollScheme.OneRoll), 10);
        Assert.Equal(hitChance, Core.Combat.DisplayedHit(hitChance, RollScheme.OneRoll));
    }

    [Fact]
    public void HitProbabilityRejectsAChanceOutsideZeroToOneHundred()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Core.Combat.HitProbability(-1, RollScheme.OneRoll));
        Assert.Throws<ArgumentOutOfRangeException>(() => Core.Combat.HitProbability(101, RollScheme.TwoRollAverage));
    }

    [Theory]
    [InlineData(RollScheme.TwoRollAverage, 75)]
    [InlineData(RollScheme.TwoRollAverage, 30)]
    [InlineData(RollScheme.OneRoll, 75)]
    [InlineData(RollScheme.OneRoll, 30)]
    public void DisplayedHitEqualsRealizedHitOverOneHundredThousandTrials(RollScheme scheme, int hitChance)
    {
        var rng = new KeyedRng(20260918);
        const int trials = 100_000;
        var landed = 0;
        for (var i = 0; i < trials; i++)
        {
            var rollA = rng.Roll(RollKey.Combat(i, Side.Player, "a", "b", 0, CombatRoll.HitA));
            var rollB = rng.Roll(RollKey.Combat(i, Side.Player, "a", "b", 0, CombatRoll.HitB));
            if (Core.Combat.Lands(hitChance, rollA, rollB, scheme))
            {
                landed++;
            }
        }

        var realized = 100.0 * landed / trials;
        Assert.InRange(realized, Core.Combat.DisplayedHit(hitChance, scheme) - 1.0, Core.Combat.DisplayedHit(hitChance, scheme) + 1.0);
        Assert.InRange(realized / 100, Core.Combat.HitProbability(hitChance, scheme) - 0.01, Core.Combat.HitProbability(hitChance, scheme) + 0.01);
    }

    [Fact]
    public void ForecastShowsBothSidesAndTheResolvedProbability()
    {
        var forecast = Core.Combat.Forecast(WrenOnPlain(), BrigandInForest(), 1, RollScheme.TwoRollAverage);

        Assert.Equal(new SideForecast(true, 9, 81, 93, 4, true), forecast.Attacker);
        Assert.Equal(new SideForecast(true, 10, 67, 79, 0, false), forecast.Defender);
        Assert.Equal(RollScheme.TwoRollAverage, forecast.Scheme);

        var oneRoll = Core.Combat.Forecast(WrenOnPlain(), BrigandInForest(), 1, RollScheme.OneRoll);
        Assert.Equal(81, oneRoll.Attacker.DisplayedHit);
        Assert.Equal(67, oneRoll.Defender.DisplayedHit);
    }

    [Fact]
    public void ATwoRangeOnlyUnitCannotCounterAtOneAndViceVersa()
    {
        var bowAtOne = Core.Combat.Forecast(WrenOnPlain(), ArcherOnPlain(), 1, RollScheme.OneRoll);
        Assert.Equal(SideForecast.None, bowAtOne.Defender);

        var swordAtTwo = Core.Combat.Forecast(ArcherOnPlain(), WrenOnPlain(), 2, RollScheme.OneRoll);
        Assert.Equal(SideForecast.None, swordAtTwo.Defender);
        Assert.True(swordAtTwo.Attacker.Strikes);

        var tomeAtTwo = Core.Combat.Forecast(ArcherOnPlain(), HexerOnPlain(), 2, RollScheme.OneRoll);
        Assert.True(tomeAtTwo.Defender.Strikes);
    }

    [Fact]
    public void ForecastRefusesAnAttackerWhoseWeaponCannotReach()
    {
        Assert.Throws<ArgumentException>(() => Core.Combat.Forecast(WrenOnPlain(), ArcherOnPlain(), 2, RollScheme.OneRoll));
        Assert.Throws<ArgumentException>(() => Core.Combat.Forecast(ArcherOnPlain(), WrenOnPlain(), 1, RollScheme.OneRoll));
        Assert.Throws<ArgumentException>(() => Core.Combat.Forecast(new Combatant(Wren, Cadet, null, Plain, 20), BrigandInForest(), 1, RollScheme.OneRoll));
    }

    [Fact]
    public void AnUnarmedUnitHasNoAttackToCompute()
    {
        var unarmed = new Combatant(Wren, Cadet, null, Plain, 20);

        Assert.Throws<ArgumentException>(() => Core.Combat.Atk(unarmed, BrigandInForest()));
        Assert.Throws<ArgumentException>(() => Core.Combat.Hit(unarmed));
    }

    [Fact]
    public void CombatantRefusesAClassThatIsNotTheUnitsOwn()
    {
        Assert.Throws<ArgumentException>(() => new Combatant(Wren, Reaver, HeavyAxe, Plain, 20));
    }

    [Fact]
    public void CombatantRefusesAWeaponTheClassCannotUse()
    {
        Assert.Throws<ArgumentException>(() => new Combatant(Wren, Cadet, ShortBow, Plain, 20));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(23)]
    public void CombatantRefusesHpOutsideOneToTheEffectiveMaximum(int hp)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Combatant(Brigand, Reaver, HeavyAxe, Forest, hp));
        Assert.Equal(22, new Combatant(Brigand, Reaver, HeavyAxe, Forest, 22).Hp);
    }

    private static void Var<T>(out T value, T set) => value = set;
}
