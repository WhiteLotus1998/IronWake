using static Ironwake.Core.Tests.Combat.CombatFixture;

namespace Ironwake.Core.Tests.Combat;

/// <summary>The section 5 sequence and the issue 31 key contract, strike by strike.</summary>
public class CombatResolverTests
{
    private static readonly CombatContext Turn3 = new(3, Side.Player);

    private static CombatResult Fight(IRng rng, RollScheme scheme = RollScheme.TwoRollAverage, int brigandHp = 22) =>
        CombatResolver.Resolve(WrenOnPlain(), BrigandInForest(brigandHp), 1, Turn3, rng, scheme);

    [Fact]
    public void AttackerStrikesDefenderCountersThenTheDoublerStrikesAgain()
    {
        var result = Fight(new ScriptedRng(50));

        Assert.Equal(
            ValueList<StrikeEvent>.Of(
                new StrikeEvent(0, "wren", "brigand", true, false, 9, 13),
                new StrikeEvent(1, "brigand", "wren", true, false, 10, 10),
                new StrikeEvent(2, "wren", "brigand", true, false, 9, 4)),
            result.Strikes);
        Assert.Equal(10, result.AttackerHp);
        Assert.Equal(4, result.DefenderHp);
        Assert.False(result.AttackerDied);
        Assert.False(result.DefenderDied);
    }

    [Fact]
    public void EveryRollUsesItsDocumentedKeyUnderTwoRolls()
    {
        var rng = new ScriptedRng(50);

        Fight(rng);

        Assert.Equal(
            new[]
            {
                "combat/3/Player/wren/brigand/0/HitA",
                "combat/3/Player/wren/brigand/0/HitB",
                "combat/3/Player/wren/brigand/0/Crit",
                "combat/3/Player/brigand/wren/0/HitA",
                "combat/3/Player/brigand/wren/0/HitB",
                "combat/3/Player/brigand/wren/0/Crit",
                "combat/3/Player/wren/brigand/1/HitA",
                "combat/3/Player/wren/brigand/1/HitB",
                "combat/3/Player/wren/brigand/1/Crit",
            },
            rng.Asked);
    }

    [Fact]
    public void UnderOneRollOnlyTheFirstHitRollIsDrawn()
    {
        var rng = new ScriptedRng(50);

        Fight(rng, RollScheme.OneRoll);

        Assert.Equal(
            new[]
            {
                "combat/3/Player/wren/brigand/0/HitA",
                "combat/3/Player/wren/brigand/0/Crit",
                "combat/3/Player/brigand/wren/0/HitA",
                "combat/3/Player/brigand/wren/0/Crit",
                "combat/3/Player/wren/brigand/1/HitA",
                "combat/3/Player/wren/brigand/1/Crit",
            },
            rng.Asked);
    }

    [Fact]
    public void TheCritRollIsDrawnOnlyWhenTheHitLanded()
    {
        var rng = new ScriptedRng(99);

        var result = Fight(rng);

        Assert.All(result.Strikes, strike => Assert.False(strike.Hit));
        Assert.All(result.Strikes, strike => Assert.Equal(0, strike.Damage));
        Assert.DoesNotContain(rng.Asked, key => key.EndsWith("/Crit", StringComparison.Ordinal));
        Assert.Equal(6, rng.Asked.Count);
    }

    [Fact]
    public void ACritDealsTripleDamage()
    {
        var rng = new ScriptedRng(50).Set(RollKey.Combat(3, Side.Player, "wren", "brigand", 0, CombatRoll.Crit), 3);

        var result = Fight(rng);

        Assert.Equal(new StrikeEvent(0, "wren", "brigand", true, true, 27, 0), result.Strikes[0]);
        Assert.Single(result.Strikes);
        Assert.True(result.DefenderDied);
    }

    [Fact]
    public void ACritRollEqualToTheChanceMisses()
    {
        var rng = new ScriptedRng(50).Set(RollKey.Combat(3, Side.Player, "wren", "brigand", 0, CombatRoll.Crit), 4);

        Assert.False(Fight(rng).Strikes[0].Crit);
    }

    [Fact]
    public void TwoRollHitReadsTheFloorOfTheAverage()
    {
        var hitA = RollKey.Combat(3, Side.Player, "wren", "brigand", 0, CombatRoll.HitA);
        var hitB = RollKey.Combat(3, Side.Player, "wren", "brigand", 0, CombatRoll.HitB);

        Assert.True(Fight(new ScriptedRng(99).Set(hitA, 44).Set(hitB, 99)).Strikes[0].Hit);
        Assert.False(Fight(new ScriptedRng(99).Set(hitA, 45).Set(hitB, 99)).Strikes[0].Hit);
        Assert.True(Fight(new ScriptedRng(99).Set(hitA, 85).Set(hitB, 0)).Strikes[0].Hit);
        Assert.False(Fight(new ScriptedRng(99).Set(hitA, 85).Set(hitB, 0), RollScheme.OneRoll).Strikes[0].Hit);
        Assert.True(Fight(new ScriptedRng(99).Set(hitA, 71), RollScheme.OneRoll).Strikes[0].Hit);
        Assert.False(Fight(new ScriptedRng(99).Set(hitA, 72), RollScheme.OneRoll).Strikes[0].Hit);
    }

    [Fact]
    public void CombatEndsWhenTheDefenderDiesBeforeCountering()
    {
        var result = Fight(new ScriptedRng(50), brigandHp: 9);

        Assert.Single(result.Strikes);
        Assert.Equal(0, result.DefenderHp);
        Assert.Equal(20, result.AttackerHp);
    }

    [Fact]
    public void CombatEndsWhenTheAttackerDiesToTheCounter()
    {
        var result = CombatResolver.Resolve(WrenOnPlain(hp: 10), BrigandInForest(), 1, Turn3, new ScriptedRng(50), RollScheme.TwoRollAverage);

        Assert.Equal(2, result.Strikes.Count);
        Assert.True(result.AttackerDied);
        Assert.Equal(13, result.DefenderHp);
    }

    [Fact]
    public void TheDefenderDoublesWhenItIsTheFasterOne()
    {
        var result = CombatResolver.Resolve(BrigandInForest(), WrenOnPlain(), 1, Turn3, new ScriptedRng(50), RollScheme.TwoRollAverage);

        Assert.Equal(new[] { "brigand", "wren", "wren" }, result.Strikes.Select(s => s.AttackerId));
        Assert.Equal(new[] { 0, 1, 2 }, result.Strikes.Select(s => s.Index));
    }

    [Fact]
    public void ADefenderOutOfRangeDoesNotCounterAndTheAttackerStillDoubles()
    {
        var rng = new ScriptedRng(50);

        var result = CombatResolver.Resolve(ArcherOnPlain(), WrenOnPlain(), 2, Turn3, rng, RollScheme.OneRoll);

        Assert.All(result.Strikes, strike => Assert.Equal("archer", strike.AttackerId));
        Assert.Single(result.Strikes);
        Assert.Equal(17, result.AttackerHp);
        Assert.Contains("combat/3/Player/archer/wren/0/HitA", rng.Asked);
        Assert.DoesNotContain(rng.Asked, key => key.Contains("/wren/archer/", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSameAttackGetsTheSameRollsWhateverWasResolvedBeforeIt()
    {
        var alone = Fight(new KeyedRng(7));

        var rng = new KeyedRng(7);
        CombatResolver.Resolve(HexerOnPlain(), ArcherOnPlain(), 2, Turn3, rng, RollScheme.TwoRollAverage);
        CombatResolver.Resolve(RiderOnPlain(), BrigandInForest(), 1, new CombatContext(3, Side.Enemy), rng, RollScheme.TwoRollAverage);
        var afterOthers = Fight(rng);

        Assert.Equal(alone, afterOthers);
    }

    [Fact]
    public void TheSameAttackOnAnotherTurnIsAnotherSetOfRolls()
    {
        var differs = Enumerable.Range(0, 20).Any(seed =>
            Fight(new KeyedRng((ulong)seed)) != CombatResolver.Resolve(WrenOnPlain(), BrigandInForest(), 1, new CombatContext(4, Side.Player), new KeyedRng((ulong)seed), RollScheme.TwoRollAverage));

        Assert.True(differs);
    }
}
