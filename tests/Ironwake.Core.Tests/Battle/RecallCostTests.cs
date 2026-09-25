using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// What a Recall gives back (issue 75): <see cref="RecallCost.Of"/> reads the difference
/// between a history state and the present, and a rewind to that state undoes exactly it.
/// </summary>
public class RecallCostTests
{
    private static readonly Coord BesideBrigand = new(2, 1);

    /// <summary>Hale kills a 1 HP brigand from the opening: the kill, its EXP and the brigand's 1 HP are what state 0 gives back.</summary>
    private static BattleState KilledTheBrigand()
    {
        var start = Start();
        start = start.WithUnit(start.Find("brigand-1")! with { Hp = 1 });
        return start.Do(new Move("hale", BesideBrigand)).Do(new Attack("hale", "brigand-1"));
    }

    [Fact]
    public void ARecallGivesBackTheKillsTheExpAndTheEnemyHpSinceItsState()
    {
        var now = KilledTheBrigand();
        var then = now.History[0];

        var cost = RecallCost.Of(now, 0);

        Assert.Equal(new[] { "brigand-1" }, cost.KillsGivenBack);
        Assert.Equal(now.Find("hale")!.Unit.Exp - then.Find("hale")!.Unit.Exp, cost.ExpGivenBack);
        Assert.True(cost.ExpGivenBack > 0);
        Assert.Equal(0, cost.LevelsGivenBack);
        Assert.Equal(1, cost.EnemyHpBack);
        Assert.Empty(cost.UnitsReturned);
        Assert.Empty(cost.ArrivalsUndone);
        Assert.Equal((0, 1), (cost.ToIndex, cost.Turn));
        Assert.False(cost.IsEmpty);
    }

    /// <summary>
    /// The acceptance's second test: the printed cost is the state difference the Recall
    /// undoes. Every counted kill is alive again after it, the EXP and the enemy HP it names
    /// are what the restored board holds over the present one.
    /// </summary>
    [Fact]
    public void TheCostOfARecallIsTheDifferenceTheRecallUndoes()
    {
        var now = KilledTheBrigand();
        var cost = RecallCost.Of(now, 0);

        var restored = now.Do(new Recall(0));

        Assert.All(cost.KillsGivenBack, id => Assert.NotNull(restored.Find(id)));
        Assert.Equal(cost.ExpGivenBack, now.Find("hale")!.Unit.Exp - restored.Find("hale")!.Unit.Exp);
        var enemyHp = (BattleState s) => s.UnitsOf(Side.Enemy).Sum(u => u.Hp);
        Assert.Equal(cost.EnemyHpBack, enemyHp(restored) - enemyHp(now));
    }

    [Fact]
    public void ALevelGainedSinceIsGivenBackWithItsExpCountedAtAHundred()
    {
        var start = Start();
        start = start.WithUnit(start.Find("brigand-1")! with { Hp = 1 });
        var hale = start.Find("hale")!;
        start = start.WithUnit(hale with { Unit = hale.Unit with { Exp = 99 } });
        var now = start.Do(new Move("hale", BesideBrigand)).Do(new Attack("hale", "brigand-1"));
        var after = now.Find("hale")!.Unit;
        Assert.Equal(2, after.Level);

        var cost = RecallCost.Of(now, 0);

        Assert.Equal(1, cost.LevelsGivenBack);
        Assert.Equal(100 + after.Exp - 99, cost.ExpGivenBack);
    }

    [Fact]
    public void ARecallReturnsTheUnitsLostAndTheHpLostSinceAndUndoesArrivals()
    {
        var then = Start().Do(new Wait("hale"));
        var wren = then.Find("wren")!;
        var spawned = then.Find("soldier-1")! with { Unit = then.Find("soldier-1")!.Unit with { Id = "soldier-2" }, At = new Coord(5, 3) };
        var now = then with
        {
            Units = then.WithoutUnit("hale").WithUnit(wren with { Hp = wren.Hp - 6 }).Units.Add(spawned),
            History = then.History.Add(then with { History = ValueList<BattleState>.Empty }),
        };

        var cost = RecallCost.Of(now, 1);

        Assert.Equal(new[] { "hale" }, cost.UnitsReturned);
        Assert.Equal(then.Find("hale")!.Hp + 6, cost.HpReturned);
        Assert.Equal(new[] { "soldier-2" }, cost.ArrivalsUndone);
        Assert.Empty(cost.KillsGivenBack);
        Assert.Equal(0, cost.EnemyHpBack);
    }

    [Fact]
    public void AHealSinceIsNotCountedAsHpReturned()
    {
        var then = Start().Do(new Wait("hale"));
        var wren = then.Find("wren")!;
        var hurt = then.WithUnit(wren with { Hp = wren.Hp - 5 });
        var now = hurt.Do(new Wait("wren")).WithUnit(wren);

        Assert.Equal(0, RecallCost.Of(now, 1).HpReturned);
    }

    [Fact]
    public void ARecallOverMovesAndWaitsOnlyIsEmpty()
    {
        var now = Start().Do(new Move("hale", new Coord(1, 1))).Do(new Wait("hale"));

        Assert.True(RecallCost.Of(now, 0).IsEmpty);
    }

    [Fact]
    public void TheCostOfAStateOutsideTheHistoryIsRefused()
    {
        var now = Start().Do(new Wait("hale"));

        Assert.Throws<ArgumentOutOfRangeException>(() => RecallCost.Of(now, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => RecallCost.Of(now, -1));
    }
}
