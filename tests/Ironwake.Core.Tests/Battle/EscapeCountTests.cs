using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The count on an Escape map (issue 928, round 313): the fewest player phases of walking each
/// unit needs to stand on an exit, the last turn it can start, the board's <c>count:</c> line and
/// the <c>end</c> warning when a phase's end passes a unit's last start.
/// </summary>
public class EscapeCountTests
{
    private static string Corridor(string row, string units = "", int limit = 10, string extraHeader = "", string exits = "11,1") =>
        $"""
        name: Corridor
        size: 12x3
        win: escape
        turn_limit: {limit}
        recall: 0
        enemy_level: 1
        exit: {exits}
        {extraHeader}

        ............
        {row}
        ............

        units:
        P captain 0,1
        {units}

        """.Replace("\n\n\n", "\n\n").Replace("\n\n\n", "\n\n");

    private static BattleState Start(string row = "............", string units = "", int limit = 10, string extraHeader = "", string exits = "11,1") =>
        BattleFixture.Start(map: Corridor(row, units, limit, extraHeader, exits));

    private static ExitCount CaptainCount(BattleState state) =>
        Assert.Single(EscapeCount.Of(state, Starter), c => c.Unit.IsCaptain);

    [Fact]
    public void TheCountIsTheFewestPhasesToStandOnAnExitAndTheLastStartIsTheLimitLessThem()
    {
        var state = Start();
        var mov = Starter.Class(Captain(state).Unit.ClassId).Mov;
        var phases = (11 + mov - 1) / mov;

        var count = CaptainCount(state);

        Assert.Equal(phases, count.Phases);
        Assert.Equal(10 - phases, count.LastStart);
        Assert.Equal(1 + phases, count.LeavesOn);
        Assert.True(count.CanLeave(10));
    }

    [Fact]
    public void TheCountReadsTerrainCostsSoForestCostsAPhaseThatPlainDoesNot()
    {
        var plain = CaptainCount(Start()).Phases!.Value;

        var forest = CaptainCount(Start(".^^^^^^^^^^.")).Phases!.Value;

        Assert.True(forest > plain, $"forest {forest} against plain {plain}");
    }

    [Fact]
    public void ABlockingEnemyRaisesTheCountByTheDetour()
    {
        var mov = Mov(Start());
        Assert.Equal(4, mov);

        var open = CaptainCount(Start());
        var blocked = CaptainCount(Start(units: "E soldier 5,1 group:g behavior:hold"));

        Assert.Equal(3, open.Phases);
        Assert.Equal(4, blocked.Phases);
    }

    [Fact]
    public void AnEnemyThatSealsTheOnlyRoadLeavesNoWayToAnExit()
    {
        var sealedOff = Start(units: "E soldier 5,1 group:g behavior:hold\nE soldier 5,0 group:g behavior:hold\nE soldier 5,2 group:g behavior:hold");

        var count = CaptainCount(sealedOff);

        Assert.Null(count.Phases);
        Assert.False(count.CanLeave(10));
        Assert.Equal($"count: {UnitNames.Of(sealedOff, Starter)[Captain(sealedOff).Id]} no way to an exit", EscapeCount.Line(sealedOff, Starter, UnitNames.Of(sealedOff, Starter)));
    }

    [Fact]
    public void ACountPastTheLimitReadsCannotLeave()
    {
        var late = Start(limit: 10) with { Turn = 9 };

        Assert.False(CaptainCount(late).CanLeave(10));
        Assert.Contains("cannot leave by turn 10", EscapeCount.Line(late, Starter, UnitNames.Of(late, Starter)));
    }

    [Fact]
    public void EndWarnsOnTheTurnItPassesTheCaptainsLastStart()
    {
        var lastStart = CaptainCount(Start()).LastStart!.Value;
        var onIt = Start() with { Turn = lastStart };

        var passed = EscapeCount.PassedByEnding(onIt, Starter);

        var warned = Assert.Single(passed);
        Assert.Equal($"Count: after this phase {UnitNames.Of(onIt, Starter)[Captain(onIt).Id]} cannot reach an exit by turn 10", EscapeCount.Warning(warned, 10, UnitNames.Of(onIt, Starter)));
    }

    [Fact]
    public void EndDoesNotWarnOneTurnBeforeTheLastStart()
    {
        var lastStart = CaptainCount(Start()).LastStart!.Value;

        Assert.Empty(EscapeCount.PassedByEnding(Start() with { Turn = lastStart - 1 }, Starter));
    }

    [Fact]
    public void EndDoesNotWarnForAUnitThatHasAlreadyMovedThisPhase()
    {
        var lastStart = CaptainCount(Start()).LastStart!.Value;
        var moved = (Start() with { Turn = lastStart }).Do(new Move(Captain(Start()).Id, new Coord(1, 1)));

        Assert.Empty(EscapeCount.PassedByEnding(moved, Starter));
    }

    [Fact]
    public void AnAllysWarningSaysTheCaptainsExitLeavesThemBehind()
    {
        var state = Start(units: "P recruit:wren 0,0", exits: "11,1 11,0");
        var lastStart = Assert.Single(EscapeCount.Of(state, Starter), c => c.Unit.Id == "wren").LastStart!.Value;
        var onIt = state with { Turn = lastStart };

        var wren = Assert.Single(EscapeCount.PassedByEnding(onIt, Starter), c => c.Unit.Id == "wren");

        Assert.Equal($"Count: after this phase {UnitNames.Of(onIt, Starter)["wren"]} cannot reach an exit by turn 10; the captain's exit leaves them behind", EscapeCount.Warning(wren, 10, UnitNames.Of(onIt, Starter)));
    }

    [Fact]
    public void AUnitThatMovedOntoAnExitLeavesNextTurnUnderTheStartOnItRule()
    {
        var mov = Mov(Start());
        var near = Start();
        near = near.WithUnit(Captain(near) with { At = new Coord(11 - mov, 1) });
        var stood = near.Do(new Move(Captain(near).Id, new Coord(11, 1)));

        var count = CaptainCount(stood);

        Assert.Equal(0, count.Phases);
        Assert.Equal(stood.Turn + 1, count.LeavesOn);
    }

    [Fact]
    public void UnderExitAfterMoveTheLastStartIsOneLater()
    {
        var phases = CaptainCount(Start()).Phases!.Value;

        var count = CaptainCount(Start(extraHeader: "exit_after_move: on"));

        Assert.Equal(10 - phases + 1, count.LastStart);
        Assert.Equal(phases, count.LeavesOn);
    }

    [Fact]
    public void OffAnEscapeMapThereIsNoCount()
    {
        var rout = BattleFixture.Start();

        Assert.Empty(EscapeCount.Of(rout, Starter));
        Assert.Null(EscapeCount.Line(rout, Starter, UnitNames.Of(rout, Starter)));
        Assert.Empty(EscapeCount.PassedByEnding(rout, Starter));
    }

    private static BattleUnit Captain(BattleState state) => state.UnitsOf(Side.Player).First(u => u.IsCaptain);

    private static int Mov(BattleState state) => Starter.Class(Captain(state).Unit.ClassId).Mov;
}
