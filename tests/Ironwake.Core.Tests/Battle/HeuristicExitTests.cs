using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The Sim's Escape approach under issue 269: a unit that can reach an exit this turn moves
/// there and exits, ahead of any attack; the captain plans last and exits only once no other
/// player unit that has not acted can reach an exit this turn.
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
    public void ARecruitThatCanReachAnExitMovesThereAndExitsAheadOfAnAttack()
    {
        var state = Start();

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
    public void TheCaptainExitsOnceNoOtherUnitCanReachAnExitThisTurn()
    {
        var state = Start().Do(new Wait("wren"));

        var plan = HeuristicPlayer.PlanUnit(state, Starter, state.Find("hale")!);

        Assert.Equal(new Command[] { new Move("hale", new Coord(3, 1)), new Exit("hale") }, plan);
    }

    [Fact]
    public void OnAnyOtherMapNoExitIsPlanned()
    {
        var state = BattleFixture.Start(map: Yard.Replace("win: escape", "win: rout").Replace("exit: 3,0 3,1 3,2 3,3\n", ""));

        Assert.Null(HeuristicPlayer.ExitTile(state, Starter, state.Find("wren")!, new[] { new Coord(3, 2) }, state.ReachOf(state.Find("wren")!, Starter)));
    }
}
