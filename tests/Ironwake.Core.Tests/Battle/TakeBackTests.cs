using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Taking back a move (issue 676, DESIGN.md section 7): a unit that has moved and not acted
/// returns to its start tile, unmoved, with no charge and no history entry, unless the move
/// was followed by another command or changed anything but its tile: a wake, a fired map event,
/// an enemy brought into sight at dusk (round 207), or any other change.
/// </summary>
public class TakeBackTests
{
    /// <summary>A 12x4 field: Hale at 0,1 and Wren at 0,2, a sleeping camp at 8,1, and a flag on a stop at 1,3.</summary>
    private const string Field = """
        name: Field
        size: 12x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ............
        ............
        ............
        ............

        units:
        P captain 0,1
        P recruit:wren 0,2
        E soldier 8,1 group:camp behavior:guard

        events:
        trip enter 1,3 flag tripped

        """;

    /// <summary>A dusk field at sight 1: Hale at 0,0 and a soldier holding at 3,0, unseen until Hale stands beside it.</summary>
    private const string Dark = """
        name: Dark
        size: 8x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        dusk: 1

        ........
        ........
        ........
        ........

        units:
        P captain 0,0
        E soldier 3,0 group:field behavior:hold

        """;

    private static BattleState Start() => BattleFixture.Start(map: Field);

    private static readonly Coord Home = new(0, 1);

    [Fact]
    public void AnUndoReturnsTheUnitUnmoved()
    {
        var start = Start();
        var moved = start.Do(new Move("hale", new Coord(2, 1)));

        var result = moved.Try(new Undo("hale"));

        Assert.True(result.Accepted);
        Assert.Equal(start, result.Next);
        Assert.Equal(new MoveUndone("hale", new Coord(2, 1), Home), Assert.Single(result.Events));
        Assert.True(result.Next.Try(new Move("hale", new Coord(1, 0))).Accepted);
    }

    [Fact]
    public void AnUndoIsRefusedAfterTheUnitActed()
    {
        var acted = Start().Do(new Move("hale", new Coord(2, 1))).Do(new Wait("hale"));

        var refusal = acted.Refused(new Undo("hale"));

        Assert.Equal(RejectionReason.CannotUndo, refusal.Reason);
        Assert.Equal("undo refused: hale has acted this phase; its move is final", refusal.Message);
    }

    [Fact]
    public void AnUndoIsRefusedForAUnitThatHasNotMoved()
    {
        var refusal = Start().Refused(new Undo("hale"));

        Assert.Equal(RejectionReason.CannotUndo, refusal.Reason);
        Assert.Equal("undo refused: hale has not moved this phase", refusal.Message);
    }

    [Fact]
    public void AnUndoIsRefusedAfterAProximityWake()
    {
        var moved = Start().Do(new Move("hale", new Coord(4, 1)));
        Assert.True(moved.IsAwake("camp"));

        var refusal = moved.Refused(new Undo("hale"));

        Assert.Equal(RejectionReason.CannotUndo, refusal.Reason);
        Assert.Equal("undo refused: hale's move woke the camp group", refusal.Message);
    }

    [Fact]
    public void AnUndoIsRefusedAfterAStopTriggerFired()
    {
        var moved = Start().Do(new Move("hale", new Coord(1, 3)));
        Assert.Contains("trip", moved.Fired);

        var refusal = moved.Refused(new Undo("hale"));

        Assert.Equal(RejectionReason.CannotUndo, refusal.Reason);
        Assert.Equal("undo refused: hale's move set off the map's trip event", refusal.Message);
    }

    [Fact]
    public void AnUndoIsRefusedAfterAnotherUnitsCommand()
    {
        var both = Start().Do(new Move("hale", new Coord(2, 1))).Do(new Move("wren", new Coord(1, 2)));

        var refusal = both.Refused(new Undo("hale"));

        Assert.Equal(RejectionReason.CannotUndo, refusal.Reason);
        Assert.Equal("undo refused: another command has followed hale's move", refusal.Message);
        Assert.True(both.Try(new Undo("wren")).Accepted);
    }

    [Fact]
    public void AnUndoIsRefusedWhenTheMoveBroughtAnUnseenEnemyIntoSight()
    {
        var start = BattleFixture.Start(map: Dark);
        Assert.False(Dusk.Seen(start, start.Find("soldier-1")!));
        var moved = start.Do(new Move("hale", new Coord(2, 0)));
        Assert.True(Dusk.Seen(moved, moved.Find("soldier-1")!));

        var refusal = moved.Refused(new Undo("hale"));

        Assert.Equal(RejectionReason.CannotUndo, refusal.Reason);
        Assert.Equal("undo refused: hale's move brought an unseen enemy into sight", refusal.Message);
    }

    [Fact]
    public void AnUndoAtDuskIsAcceptedWhenTheMoveRevealedNothing()
    {
        var start = BattleFixture.Start(map: Dark);
        var moved = start.Do(new Move("hale", new Coord(0, 2)));

        Assert.Equal(start, moved.Try(new Undo("hale")).Next);
    }

    [Fact]
    public void AnUndoIsRefusedWhenTheMoveChangedMoreThanItsTile()
    {
        var moved = Start().Do(new Move("hale", new Coord(2, 1)));
        var wren = moved.Find("wren")!;
        var changed = moved.WithUnit(wren with { Hp = wren.Hp - 1 });

        var refusal = changed.Refused(new Undo("hale"));

        Assert.Equal(RejectionReason.CannotUndo, refusal.Reason);
        Assert.Equal("undo refused: hale's move changed more than its tile", refusal.Message);
    }

    [Fact]
    public void TheRecallHistoryIsUnchangedByAnUndo()
    {
        var before = Start().Do(new Move("wren", new Coord(1, 2))).Do(new Wait("wren"));
        var moved = before.Do(new Move("hale", new Coord(2, 1)));

        var undone = moved.Do(new Undo("hale"));

        Assert.Equal(before.History, undone.History);
        Assert.Equal(before.RecallCharges, undone.RecallCharges);
        Assert.Equal(before, undone);
    }

    [Fact]
    public void AnUndoIsNeverALegalCommandForTheRandomPlayer()
    {
        var moved = Start().Do(new Move("hale", new Coord(2, 1)));

        Assert.True(moved.Try(new Undo("hale")).Accepted);
        Assert.DoesNotContain(Resolver.Legal(moved, Starter), c => c is Undo);
    }
}
