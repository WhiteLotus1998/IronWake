using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The throne-holder rule (issue 321, DESIGN.md section 8): on a Seize map an enemy on the
/// throne leaves it only to strike this phase, and otherwise Waits on it.
/// </summary>
public class ThroneHolderTests
{
    /// <summary>A 16x3 hall on <paramref name="win"/>: a throne at 12,1, Hale at <paramref name="hale"/>, and a guard boss alone in group hall at <paramref name="reeve"/>.</summary>
    private static string Hall(string hale, string reeve, string win = "seize") => $"""
        name: Hall
        size: 16x3
        win: {win}
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ................
        ............T...
        ................

        units:
        P captain {hale}
        P recruit:wren 0,2
        B grange_reeve {reeve} group:hall behavior:guard

        """;

    private static (BattleState State, BattleUnit Reeve) Awake(string hale, string reeve, string win = "seize")
    {
        var state = BattleFixture.Start(map: Hall(hale, reeve, win)).Wake("hall").Do(new EndPhase());
        return (state, state.Find("grange_reeve-1")!);
    }

    [Fact]
    public void AThroneHolderThatCannotStrikeThisPhaseWaitsOnTheThrone()
    {
        var (state, reeve) = Awake("1,1", "12,1");

        Assert.True(EnemyAi.HoldsTheThrone(state, reeve));
        Assert.Equal(Behavior.Aggressive, state.EffectiveBehavior(reeve, Starter));
        Assert.Equal(new Command[] { new Wait("grange_reeve-1") }, EnemyAi.PlanUnit(state, Starter, reeve));
    }

    [Fact]
    public void AThroneHolderThatCanStrikeFromOffTheThroneMovesAndStrikes()
    {
        var (state, reeve) = Awake("8,1", "12,1");

        var plan = EnemyAi.PlanUnit(state, Starter, reeve);

        Assert.Equal(2, plan.Count);
        var move = Assert.IsType<Move>(plan[0]);
        Assert.NotEqual(new Coord(12, 1), move.To);
        Assert.Equal("hale", Assert.IsType<Attack>(plan[1]).TargetId);
    }

    [Fact]
    public void TheSeatedReeveStillKitesWithTheTollSpearFromRangeTwo()
    {
        var (state, reeve) = Awake("6,1", "12,1");

        var plan = EnemyAi.PlanUnit(state, Starter, reeve);

        var move = Assert.IsType<Move>(plan[0]);
        var attack = Assert.IsType<Attack>(plan[1]);
        Assert.Equal(2, move.To.DistanceTo(new Coord(6, 1)));
        Assert.Equal("toll_spear", reeve.Unit.Inventory.Items[attack.Slot ?? 0].ItemId);
    }

    [Fact]
    public void AUnitOffTheThroneIsUnaffectedByTheThroneHolderRule()
    {
        var (state, reeve) = Awake("1,1", "14,1");

        Assert.False(EnemyAi.HoldsTheThrone(state, reeve));
        Assert.IsType<Move>(EnemyAi.PlanUnit(state, Starter, reeve)[0]);
    }

    [Fact]
    public void AThroneOnAMapThatIsNotSeizeHoldsNobody()
    {
        var (state, reeve) = Awake("1,1", "12,1", win: "rout");

        Assert.False(EnemyAi.HoldsTheThrone(state, reeve));
        Assert.IsType<Move>(EnemyAi.PlanUnit(state, Starter, reeve)[0]);
    }

    [Fact]
    public void APlayerUnitOnTheThroneIsNoThroneHolder()
    {
        var state = BattleFixture.Start(map: Hall("12,1", "1,1"));

        Assert.False(EnemyAi.HoldsTheThrone(state, state.Find("hale")!));
    }
}
