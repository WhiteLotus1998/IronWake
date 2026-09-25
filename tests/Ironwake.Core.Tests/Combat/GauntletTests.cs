using static Ironwake.Core.Tests.Combat.CombatFixture;

namespace Ironwake.Core.Tests.Combat;

/// <summary>
/// Issue 70: a gauntlet attack is a round of two strikes, each rolling hit and crit on its
/// own, and every turn of the section 5 sequence it takes is such a round. The gauntlet
/// and the brawler class are test content; no shipped unit carries one yet.
/// </summary>
public class GauntletTests
{
    private static readonly CombatContext Turn3 = new(3, Side.Player);

    // Mt 2, hit 90, no crit, Wt 1, range 1.
    private static readonly Weapon IronGauntlet = new("iron_gauntlet", "Iron Gauntlet", WeaponType.Gauntlet, 2, 90, 0, 1, 1, 1, 20, ValueList<MovementType>.Empty);

    private static readonly UnitClass Brawler = new("brawler", "brawler", MovementType.Infantry, 4, Stats.Zero, ValueList<WeaponType>.Of(WeaponType.Gauntlet), Stats.Zero);

    // Wren's numbers in a brawler: attack speed 8, Def 4, avoid 18. Against the brigand in
    // forest, 7 + 2 - (2 + 1) = 6 a strike at hit 98 - 26 = 72; against Wren, 9 - 4 = 5 at 80.
    private static readonly Unit FistUnit = new("fist", "fist", "brawler", 1, 0, new Stats(20, 7, 0, 6, 8, 5, 4, 2, 3), Stats.Zero, Inventory.Empty, ValueList<string>.Empty);

    private static Combatant Fist(int hp = 20) => new(FistUnit, Brawler, IronGauntlet, Plain, hp);

    private static StrikeEvent S(int index, string attacker, string target, int damage, int hpAfter) =>
        new(index, attacker, target, true, false, damage, hpAfter);

    [Fact]
    public void AGauntletAttackIsTwoStrikesBeforeTheCounter()
    {
        var result = CombatResolver.Resolve(Fist(), WrenOnPlain(), 1, Turn3, new ScriptedRng(50), RollScheme.TwoRollAverage);

        Assert.Equal(
            ValueList<StrikeEvent>.Of(S(0, "fist", "wren", 5, 15), S(1, "fist", "wren", 5, 10), S(2, "wren", "fist", 8, 12)),
            result.Strikes);
    }

    [Fact]
    public void ADoublingGauntletLandsFourStrikes()
    {
        var result = CombatResolver.Resolve(Fist(), BrigandInForest(), 1, Turn3, new ScriptedRng(50), RollScheme.TwoRollAverage);

        Assert.Equal(
            ValueList<StrikeEvent>.Of(
                S(0, "fist", "brigand", 6, 16),
                S(1, "fist", "brigand", 6, 10),
                S(2, "brigand", "fist", 10, 10),
                S(3, "fist", "brigand", 6, 4),
                S(4, "fist", "brigand", 6, 0)),
            result.Strikes);
        Assert.True(result.DefenderDied);
    }

    [Fact]
    public void ACounteringGauntletStrikesTwice()
    {
        var result = CombatResolver.Resolve(WrenOnPlain(), Fist(), 1, Turn3, new ScriptedRng(50), RollScheme.TwoRollAverage);

        Assert.Equal(
            ValueList<StrikeEvent>.Of(S(0, "wren", "fist", 8, 12), S(1, "fist", "wren", 5, 15), S(2, "fist", "wren", 5, 10)),
            result.Strikes);
    }

    [Fact]
    public void ADoublingCounterWithGauntletsIsTwoRoundsOfTwo()
    {
        var brigandOnPlain = new Combatant(Brigand, Reaver, HeavyAxe, Plain, 22);
        var result = CombatResolver.Resolve(brigandOnPlain, new Combatant(FistUnit, Brawler, IronGauntlet, Forest, 20), 1, Turn3, new ScriptedRng(50), RollScheme.TwoRollAverage);

        // The brigand on plain takes 7 + 2 - 2 = 7 a strike; its axe at 73 - 38 = 35 misses the fist in forest on 50.
        Assert.Equal(
            ValueList<StrikeEvent>.Of(
                new StrikeEvent(0, "brigand", "fist", false, false, 0, 20),
                S(1, "fist", "brigand", 7, 15),
                S(2, "fist", "brigand", 7, 8),
                S(3, "fist", "brigand", 7, 1),
                S(4, "fist", "brigand", 7, 0)),
            result.Strikes);
    }

    [Fact]
    public void ADeathEndsTheCombatInTheMiddleOfARound()
    {
        var result = CombatResolver.Resolve(Fist(), BrigandInForest(5), 1, Turn3, new ScriptedRng(50), RollScheme.TwoRollAverage);

        Assert.Equal(ValueList<StrikeEvent>.Of(S(0, "fist", "brigand", 6, 0)), result.Strikes);
    }

    [Fact]
    public void EachStrikeOfARoundRollsItsOwnHitAndCritUnderItsOwnIndex()
    {
        var rng = new ScriptedRng(50)
            .Set(RollKey.Combat(3, Side.Player, "fist", "brigand", 0, CombatRoll.HitA), 99)
            .Set(RollKey.Combat(3, Side.Player, "fist", "brigand", 0, CombatRoll.HitB), 99)
            .Set(RollKey.Combat(3, Side.Player, "fist", "brigand", 1, CombatRoll.Crit), 0);

        var result = CombatResolver.Resolve(Fist(), BrigandInForest(), 1, Turn3, rng, RollScheme.TwoRollAverage);

        // Crit 5 against the brigand's Lck 1 is 4, so a crit roll of 0 lands for 3 x 6.
        Assert.Equal(new StrikeEvent(0, "fist", "brigand", false, false, 0, 22), result.Strikes[0]);
        Assert.Equal(new StrikeEvent(1, "fist", "brigand", true, true, 18, 4), result.Strikes[1]);
        Assert.Equal(
            new[]
            {
                "combat/3/Player/fist/brigand/0/HitA",
                "combat/3/Player/fist/brigand/0/HitB",
                "combat/3/Player/fist/brigand/1/HitA",
                "combat/3/Player/fist/brigand/1/HitB",
                "combat/3/Player/fist/brigand/1/Crit",
            },
            rng.Asked.Take(5));
    }

    [Fact]
    public void TheForecastCountsEveryStrikeOfEveryRound()
    {
        var doubling = Core.Combat.Forecast(Fist(), BrigandInForest(), 1, RollScheme.TwoRollAverage);
        var single = Core.Combat.Forecast(Fist(), WrenOnPlain(), 1, RollScheme.TwoRollAverage);
        var sword = Core.Combat.Forecast(WrenOnPlain(), BrigandInForest(), 1, RollScheme.TwoRollAverage);
        var unanswered = Core.Combat.Forecast(ArcherOnPlain(), Fist(), 2, RollScheme.TwoRollAverage);

        Assert.Equal((2, 2, 4), (doubling.Attacker.StrikesPerRound, doubling.Attacker.Rounds, doubling.Attacker.StrikeCount));
        Assert.Equal(1, doubling.Defender.StrikeCount);
        Assert.Equal((2, 1, 2), (single.Attacker.StrikesPerRound, single.Attacker.Rounds, single.Attacker.StrikeCount));
        Assert.Equal(1, single.Defender.StrikeCount);
        Assert.Equal((1, 2, 2), (sword.Attacker.StrikesPerRound, sword.Attacker.Rounds, sword.Attacker.StrikeCount));
        Assert.Equal(0, unanswered.Defender.StrikeCount);
    }

    [Fact]
    public void AGauntletSpendsOneUseForTheWholeCombatAndAnArtsCostOnTop()
    {
        var gauntlet = Core.Combat.Forecast(Fist(), BrigandInForest(), 1, RollScheme.TwoRollAverage);
        var sword = Core.Combat.Forecast(WrenOnPlain(), BrigandInForest(), 1, RollScheme.TwoRollAverage);

        Assert.Equal(1, gauntlet.AttackerSpendsAtMost);
        Assert.Equal(3, (gauntlet with { ArtCost = 2 }).AttackerSpendsAtMost);
        Assert.Equal(2, sword.AttackerSpendsAtMost);
    }

    [Fact]
    public void AFirstRoundOfCertainGauntletStrikesIsACertainKill()
    {
        var round = new SideForecast(true, 5, 100, 100, 0, false, StrikesPerRound: 2);

        Assert.True(Exposure.KillsWithCertainty(round, 10));
        Assert.False(Exposure.KillsWithCertainty(round, 11));
        Assert.False(Exposure.KillsWithCertainty(round with { StrikesPerRound = 1 }, 10));
    }
}
