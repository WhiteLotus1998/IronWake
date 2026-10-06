using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The Sim's range-2 finish on a Seize cork (issue 1206): when an enemy holds the only way to
/// the throne, the captain plans last on a turn it can strike without a counter, and a recruit
/// never strikes from a tile the captain could strike the cork from without one, so the
/// recruit takes the counter in melee and the captain finishes from the safe tile.
/// </summary>
public class HeuristicCorkTests
{
    private static readonly Unit Archer = Recruit("hale", "bowman", new Stats(22, 8, 0, 7, 8, 6, 5, 2, 9), "iron_bow");
    private static readonly Unit BowWren = Recruit("wren", "bowman", new Stats(20, 7, 0, 6, 8, 5, 4, 2, 3), "iron_bow");

    /// <summary>
    /// A corridor to the throne at 2,0 held by a soldier at 2,1: 2,2 strikes it in melee, 2,3 is
    /// the one tile at range 2. The captain at 1,5, Wren at 3,5.
    /// </summary>
    private static string Corridor(string win) => $"""
        name: Corridor
        size: 5x6
        win: {win}
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ##T##
        ##.##
        ##.##
        #...#
        .....
        .....

        units:
        P captain 1,5
        P recruit:wren 3,5
        E soldier 2,1 group:door behavior:hold

        """;

    private static BattleState Board(string win, Unit wren) => Start(roster: ValueList<Unit>.Of(Archer, wren), map: Corridor(win));

    private static Attack AttackOf(IReadOnlyList<Command> plan) => Assert.IsType<Attack>(plan[^1]);

    private static Coord EndOf(IReadOnlyList<Command> plan, BattleUnit unit) => plan.OfType<Move>().Select(m => m.To).DefaultIfEmpty(unit.At).Single();

    [Fact]
    public void OnACorkedSeizeTheCaptainWhoCanStrikeWithoutACounterPlansLast()
    {
        var state = Board("seize", Wren);

        Assert.NotNull(HeuristicPlayer.Corked(state, Starter));
        Assert.Contains(new Coord(2, 3), HeuristicPlayer.SafeStrikeTiles(state, Starter, state.Find("hale")!));
        Assert.Equal(new[] { "wren", "hale" }, HeuristicPlayer.PlanOrder(state, Starter).Select(u => u.Id));
    }

    [Fact]
    public void OnARoutTheCorkRuleDoesNotFireAndThePlanOrderIsTheUnitsOwn()
    {
        var state = Board("rout", Wren);

        Assert.Null(HeuristicPlayer.Corked(state, Starter));
        Assert.Equal(new[] { "hale", "wren" }, HeuristicPlayer.PlanOrder(state, Starter).Select(u => u.Id));
    }

    [Fact]
    public void ARecruitOnACorkStrikesInMeleeAndLeavesTheCaptainTheNoCounterTile()
    {
        var state = Board("seize", Wren);
        var wren = state.Find("wren")!;

        var plan = HeuristicPlayer.PlanUnit(state, Starter, wren);

        Assert.Equal("soldier-1", AttackOf(plan).TargetId);
        Assert.Equal(new Coord(2, 2), EndOf(plan, wren));

        var after = plan.Aggregate(state, (s, c) => s.Do(c));
        var finish = HeuristicPlayer.PlanUnit(after, Starter, after.Find("hale")!);
        Assert.Equal(new Coord(2, 3), EndOf(finish, after.Find("hale")!));
    }

    [Fact]
    public void ARecruitWhoseOnlyAttackTileIsTheCaptainsDoesNotStrikeFromIt()
    {
        var state = Board("seize", BowWren);
        var wren = state.Find("wren")!;

        var plan = HeuristicPlayer.PlanUnit(state, Starter, wren);

        Assert.DoesNotContain(plan, c => c is Attack);
        Assert.NotEqual(new Coord(2, 3), EndOf(plan, wren));
    }

    [Fact]
    public void OnARoutTheSameRecruitStrikesFromTheRangeTwoTile()
    {
        var state = Board("rout", BowWren);
        var wren = state.Find("wren")!;

        var plan = HeuristicPlayer.PlanUnit(state, Starter, wren);

        Assert.Equal("soldier-1", AttackOf(plan).TargetId);
        Assert.Equal(new Coord(2, 3), EndOf(plan, wren));
    }
}
