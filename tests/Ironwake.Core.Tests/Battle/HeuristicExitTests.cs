using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The Sim's Escape approach under issues 269 and 377: a unit that began its turn on an exit
/// leaves, and one that can reach an exit this turn moves there and stands, ahead of any
/// attack, to leave on the next; the captain plans last and leaves only once no other player
/// unit stands on an exit or can reach one this turn, or on the last turn; the rest plan
/// farthest from an exit first (issue 332). A map with <c>exit_after_move: on</c> keeps
/// issue 269's move and exit in one turn.
/// </summary>
public class HeuristicExitTests
{
    /// <summary>A 6x4 yard: Hale at 0,1, Wren at 0,2, a holding soldier at 2,2 beside Wren's way, exits on column 3.</summary>
    private const string Yard = """
        name: Yard
        size: 6x4
        win: escape
        turn_limit: 10
        recall: 3
        enemy_level: 1
        exit: 3,0 3,1 3,2 3,3

        ......
        ......
        ......
        ......

        units:
        P captain 0,1
        P recruit:wren 0,2
        E soldier 2,3 group:g behavior:hold

        """;

    private static BattleState Start() => BattleFixture.Start(map: Yard);

    [Fact]
    public void ARecruitThatCanReachAnExitMovesThereAndStandsAheadOfAnAttack()
    {
        var state = Start();

        var plan = HeuristicPlayer.PlanUnit(state, Starter, state.Find("wren")!);

        Assert.Equal(new Command[] { new Move("wren", new Coord(3, 2)), new Wait("wren") }, plan);
    }

    [Fact]
    public void ARecruitThatBeganItsTurnOnAnExitLeaves()
    {
        var state = Start().Stood("wren", new Coord(3, 2));

        var plan = HeuristicPlayer.PlanUnit(state, Starter, state.Find("wren")!);

        Assert.Equal(new Command[] { new Exit("wren") }, plan);
    }

    [Fact]
    public void OnAnExitAfterMoveMapARecruitMovesOntoAnExitAndLeavesInOneTurn()
    {
        var state = BattleFixture.Start(map: Yard.Replace("exit: 3,0 3,1 3,2 3,3\n", "exit: 3,0 3,1 3,2 3,3\nexit_after_move: on\n"));

        var plan = HeuristicPlayer.PlanUnit(state, Starter, state.Find("wren")!);

        Assert.Equal(new Command[] { new Move("wren", new Coord(3, 2)), new Exit("wren") }, plan);
    }

    [Fact]
    public void TheCaptainPlansLastOnAnEscapeMap()
    {
        var plan = new HeuristicPlayer().Next(Start(), Starter);

        Assert.Equal("wren", plan[0] switch { Move m => m.UnitId, var other => other.ToString() });
    }

    [Fact]
    public void TheCaptainDoesNotExitWhileAnotherUnitCanStillReachAnExitThisTurn()
    {
        var state = Start();

        var plan = HeuristicPlayer.PlanUnit(state, Starter, state.Find("hale")!);

        Assert.DoesNotContain(plan, c => c is Exit);
    }

    [Fact]
    public void TheCaptainOnAnExitLeavesOnceNoOtherUnitStandsOnOrCanReachAnExit()
    {
        var state = Start().Stood("hale", new Coord(3, 1)).Do(new Wait("wren"));

        var plan = HeuristicPlayer.PlanUnit(state, Starter, state.Find("hale")!);

        Assert.Equal(new Command[] { new Exit("hale") }, plan);
    }

    [Fact]
    public void TheCaptainOnAnExitHoldsItWhileAnotherUnitStandsOnAnExitToLeaveNextTurn()
    {
        var state = Start().Stood("hale", new Coord(3, 1)).Do(new Move("wren", new Coord(3, 2))).Do(new Wait("wren"));

        var plan = HeuristicPlayer.PlanUnit(state, Starter, state.Find("hale")!);

        Assert.Equal(new Command[] { new Wait("hale") }, plan);
    }

    [Fact]
    public void OnTheLastTurnTheCaptainOnAnExitLeavesWhoeverElseStandsOnOne()
    {
        var state = Start().Stood("hale", new Coord(3, 1)).Do(new Move("wren", new Coord(3, 2))).Do(new Wait("wren"));
        state = state with { Turn = state.Map.TurnLimit };

        var plan = HeuristicPlayer.PlanUnit(state, Starter, state.Find("hale")!);

        Assert.Equal(new Command[] { new Exit("hale") }, plan);
    }

    [Fact]
    public void OnAnyOtherMapNoExitIsPlanned()
    {
        var state = BattleFixture.Start(map: Yard.Replace("win: escape", "win: rout").Replace("exit: 3,0 3,1 3,2 3,3\n", ""));

        Assert.Null(HeuristicPlayer.ExitTile(state, Starter, state.Find("wren")!, new[] { new Coord(3, 2) }, state.ReachOf(state.Find("wren")!, Starter)));
    }

    /// <summary>A 6x4 lane: Ivo one step from the exits on column 5, Wren and Hale at the far end.</summary>
    private const string Lane = """
        name: Lane
        size: 6x4
        win: escape
        turn_limit: 10
        recall: 3
        enemy_level: 1
        exit: 5,0 5,1 5,2 5,3

        ......
        ......
        ......
        ......

        units:
        P captain 0,1
        P recruit:ivo 4,2
        P recruit:wren 0,2
        E soldier 2,3 group:g behavior:hold

        """;

    private static BattleState LaneStart(string map = Lane) =>
        BattleFixture.Start(map: map, roster: ValueList<Unit>.Of(Hale, Wren, Ivo));

    [Fact]
    public void OnAnEscapeMapTheUnitFarthestFromAnExitPlansFirst()
    {
        var order = HeuristicPlayer.PlanOrder(LaneStart()).Select(u => u.Id).ToList();

        Assert.Equal(new[] { "wren", "ivo", "hale" }, order);
    }

    [Fact]
    public void OnAnEscapeMapTheRearUnitTakesTheFirstTurn()
    {
        var plan = new HeuristicPlayer().Next(LaneStart(), Starter);

        Assert.Equal("wren", plan[0] switch { Move m => m.UnitId, var other => other.ToString() });
    }

    [Fact]
    public void OnAnyOtherMapThePlanOrderIsTheUnitsOwnOrder()
    {
        var state = LaneStart(Lane.Replace("win: escape", "win: rout").Replace("exit: 5,0 5,1 5,2 5,3\n", ""));

        Assert.Equal(state.UnitsOf(Side.Player).Select(u => u.Id), HeuristicPlayer.PlanOrder(state).Select(u => u.Id));
    }
}
