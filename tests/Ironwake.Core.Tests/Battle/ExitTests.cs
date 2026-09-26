using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Escape by leaving (issue 269, DESIGN.md section 7): on an Escape map a unit standing on
/// an exit takes Exit as its action and leaves the board; the captain's exit wins the
/// battle and leaves every player unit still on the board behind, which counts as fallen;
/// a protected recruit left behind loses it; Recall undoes an exit.
/// </summary>
public class ExitTests
{
    private static string Yard(string extraHeader = "", string win = "escape") =>
        $"""
        name: Yard
        size: 6x4
        win: {win}
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(win == "escape" ? "exit: 1,3 2,3" : "")}
        {extraHeader}

        ......
        ......
        ......
        ......

        units:
        P captain 0,1
        P recruit:wren 0,2
        E brigand 3,1 group:yard behavior:aggressive
        E soldier 3,2 group:yard behavior:aggressive

        """.Replace("\n\n\n", "\n\n").Replace("\n\n\n", "\n\n");

    private static BattleState Start(string extraHeader = "", string win = "escape") => BattleFixture.Start(map: Yard(extraHeader, win));

    [Fact]
    public void AUnitOnAnExitLeavesTheBoardAsItsActionAndIsSafe()
    {
        var onExit = Start().Do(new Move("wren", new Coord(1, 3)));

        var result = onExit.Try(new Exit("wren"));

        Assert.True(result.Accepted);
        Assert.Equal(new GameEvent[] { new UnitExited("wren", new Coord(1, 3)) }, result.Events.ToArray());
        Assert.Null(result.Next.Find("wren"));
        Assert.True(result.Next.HasEscaped("wren"));
        Assert.Equal(BattleOutcome.Ongoing, result.Next.Outcome);
        Assert.Null(result.Next.UnitAt(new Coord(1, 3)));
        Assert.DoesNotContain(Resolver.Legal(result.Next, Starter), c => c is Move { UnitId: "wren" } or Wait { UnitId: "wren" } or Exit { UnitId: "wren" });
    }

    [Fact]
    public void AUnitMayExitWithoutMovingWhenItAlreadyStandsOnAnExit()
    {
        var state = Start();
        state = state.WithUnit(state.Find("wren")! with { At = new Coord(1, 3) });

        Assert.True(state.Do(new Exit("wren")).HasEscaped("wren"));
    }

    [Fact]
    public void TheCaptainsExitWinsAndLeavesEveryUnitOnTheBoardBehind()
    {
        var state = Start().Do(new Move("hale", new Coord(1, 3)));

        var result = state.Try(new Exit("hale"));

        Assert.Equal(new GameEvent[] { new UnitExited("hale", new Coord(1, 3)), new UnitLeftBehind("wren", new Coord(0, 2)) }, result.Events.ToArray());
        Assert.Equal(new BattleOutcome(BattleResult.Won, "escape"), result.Next.Outcome);
        Assert.Equal(new[] { "wren" }, result.Next.LeftBehind().Select(u => u.Id));
        Assert.Equal(new[] { "hale" }, result.Next.Survivors().Select(u => u.Id));
        Assert.Equal(RejectionReason.BattleOver, result.Next.Refused(new Wait("wren")).Reason);
    }

    [Fact]
    public void NobodyIsLeftBehindWhenEveryoneExitsBeforeTheCaptain()
    {
        var state = Start().Do(new Move("wren", new Coord(2, 3))).Do(new Exit("wren")).Do(new Move("hale", new Coord(1, 3)));

        var result = state.Try(new Exit("hale"));

        Assert.Equal(new GameEvent[] { new UnitExited("hale", new Coord(1, 3)) }, result.Events.ToArray());
        Assert.Empty(result.Next.LeftBehind());
        Assert.Equal(new[] { "wren", "hale" }, result.Next.Survivors().Select(u => u.Id));
    }

    [Fact]
    public void ExitIsRefusedOffAnExitTile()
    {
        var rejection = Start().Refused(new Exit("wren"));

        Assert.Equal(RejectionReason.NotOnAnExit, rejection.Reason);
        Assert.Equal("wren cannot exit: 0,2 is not an exit tile", rejection.Message);
    }

    [Fact]
    public void ExitIsRefusedAfterTheUnitHasActedSoAUnitCannotStrikeAndLeave()
    {
        var waited = Start().Do(new Move("wren", new Coord(1, 3))).Do(new Wait("wren"));

        Assert.Equal(RejectionReason.AlreadyActed, waited.Refused(new Exit("wren")).Reason);
    }

    [Fact]
    public void ExitIsRefusedAfterAnAttack()
    {
        var map = Yard().Replace("E soldier 3,2", "E soldier 1,2");
        var state = BattleFixture.Start(map: map).Do(new Move("wren", new Coord(1, 3))).Do(new Attack("wren", "soldier-1"));

        Assert.Equal(RejectionReason.AlreadyActed, state.Refused(new Exit("wren")).Reason);
    }

    [Fact]
    public void ExitIsRefusedOnAMapThatIsNotEscape()
    {
        var rejection = Start(win: "rout").Refused(new Exit("wren"));

        Assert.Equal(RejectionReason.NotOnAnExit, rejection.Reason);
        Assert.Equal("wren cannot exit: Yard is not an Escape map", rejection.Message);
    }

    [Fact]
    public void AnEnemyOnAnExitCannotExit()
    {
        var state = Start();
        state = state.WithUnit(state.Find("brigand-1")! with { At = new Coord(2, 3) }) with { Phase = Side.Enemy };

        Assert.Equal(RejectionReason.NotOnAnExit, state.Refused(new Exit("brigand-1")).Reason);
        Assert.DoesNotContain(Resolver.Legal(state, Starter), c => c is Exit);
    }

    [Fact]
    public void AProtectedRecruitLeftBehindLosesTheBattle()
    {
        var state = Start("protect: wren").Do(new Move("hale", new Coord(1, 3)));

        var left = state.Do(new Exit("hale"));

        Assert.Equal(new BattleOutcome(BattleResult.Lost, "wren is left behind", LossCause.Protected), left.Outcome);
    }

    [Fact]
    public void AProtectedRecruitThatExitsFirstIsNotLeftBehind()
    {
        var state = Start("protect: wren").Do(new Move("wren", new Coord(2, 3))).Do(new Exit("wren"));
        Assert.Equal(BattleOutcome.Ongoing, state.Outcome);

        var won = state.Do(new Move("hale", new Coord(1, 3))).Do(new Exit("hale"));

        Assert.Equal(new BattleOutcome(BattleResult.Won, "escape"), won.Outcome);
    }

    [Fact]
    public void RecallUndoesAnExit()
    {
        var before = Start().Do(new Move("wren", new Coord(1, 3)));
        var exited = before.Do(new Exit("wren"));

        var recalled = exited.Do(new Recall(before.History.Count));

        Assert.Equal(new Coord(1, 3), recalled.Find("wren")?.At);
        Assert.Empty(recalled.Escaped);
        Assert.Equal(before.Canonical(), recalled.Canonical().Replace("recall 2", "recall 3"));
    }

    [Fact]
    public void ARecallOverAnExitDoesNotCountTheUnitAsBroughtBack()
    {
        var before = Start().Do(new Move("wren", new Coord(1, 3)));
        var exited = before.Do(new Exit("wren"));

        var cost = RecallCost.Of(exited, before.History.Count);

        Assert.Empty(cost.UnitsReturned);
        Assert.Equal(0, cost.HpReturned);
    }

    [Fact]
    public void LegalOffersExitExactlyToAPlayerUnitOnAnExitThatHasNotActed()
    {
        var state = Start();
        Assert.DoesNotContain(Resolver.Legal(state, Starter), c => c is Exit);

        var onExit = state.Do(new Move("wren", new Coord(1, 3)));
        Assert.Contains(new Exit("wren"), Resolver.Legal(onExit, Starter));
        Assert.DoesNotContain(new Exit("wren"), Resolver.Legal(onExit.Do(new Wait("wren")), Starter));
    }

    [Fact]
    public void TheCanonicalStateNamesTheEscapedOnAnEscapeMapOnly()
    {
        var exited = Start().Do(new Move("wren", new Coord(1, 3))).Do(new Exit("wren"));

        Assert.Contains("\nescaped wren\n", exited.Canonical());
        Assert.DoesNotContain("escaped", Start(win: "rout").Canonical());
    }
}
