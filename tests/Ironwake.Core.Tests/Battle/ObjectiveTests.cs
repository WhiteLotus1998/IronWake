using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The objective in player words (issue 374): the line a battle opens with, why a lost battle
/// was lost, the Seize notices, and no phase beginning past the turn limit.
/// </summary>
public class ObjectiveTests
{
    /// <summary>A 16x3 hall with a throne at 12,1, Hale the captain at 1,1, Wren at <paramref name="wren"/>, and one brigand far off.</summary>
    private static string Hall(string win = "seize", int limit = 10, string wren = "0,2", string protect = "") => $"""
        name: Hall
        size: 16x3
        win: {win}
        turn_limit: {limit}
        recall: 3
        enemy_level: 1
        {protect}

        ................
        ............T...
        ................

        units:
        P captain 1,1
        P recruit:wren {wren}
        E brigand 15,0 group:far behavior:hold

        """;

    [Fact]
    public void TheSeizeObjectiveSaysWhereTheCaptainGoesByWhenAndWhoMustSurvive()
    {
        var line = Objective.Line(Start(map: Hall()), Starter);

        Assert.Equal("Get the captain to the gate by the end of turn 10. Captain hale must survive.", line);
    }

    [Fact]
    public void TheSeizeRulesNameTheCaptainWithItsLetterAndTheTile()
    {
        var rule = Assert.Single(Objective.Rules(Start(map: Hall()), Starter));

        Assert.Equal("The captain, hale (A), must stand on the gate at 12,1. Only the captain seizes.", rule);
    }

    [Theory]
    [InlineData("rout")]
    [InlineData("survive")]
    public void ARoutOrSurviveMapNeedsNoRulesBeyondItsLine(string win)
    {
        Assert.Empty(Objective.Rules(Start(map: Hall(win)), Starter));
    }

    [Theory]
    [InlineData("rout", "Defeat every enemy by the end of turn 10. Captain hale must survive.")]
    [InlineData("survive", "Hold out until the end of turn 10. Captain hale must survive.")]
    public void EveryWinConditionHasAnObjectiveLine(string win, string expected)
    {
        Assert.Equal(expected, Objective.Line(Start(map: Hall(win)), Starter));
    }

    [Fact]
    public void AProtectedUnitIsNamedInTheObjectivesLossClause()
    {
        var line = Objective.Line(Start(map: Hall(protect: "protect: wren")), Starter);

        Assert.EndsWith(" Captain hale and wren must survive.", line);
    }

    [Fact]
    public void PastTheTurnLimitTheTurnAdvancesButNoPhaseBegins()
    {
        var enemyPhase = Start(map: Hall(limit: 1)).Do(new EndPhase());

        var end = enemyPhase.Try(new EndPhase());

        Assert.Equal(new GameEvent[] { new PhaseEnded(Side.Enemy, 1) }, end.Events);
        Assert.Equal(2, end.Next.Turn);
        Assert.Equal(LossCause.Timeout, end.Next.Outcome.Cause);
    }

    [Fact]
    public void BeforeTheTurnLimitTheNextPhaseStillBegins()
    {
        var enemyPhase = Start(map: Hall(limit: 2)).Do(new EndPhase());

        var end = enemyPhase.Try(new EndPhase());

        Assert.Contains(new PhaseBegan(Side.Player, 2), end.Events);
        Assert.False(end.Next.Outcome.IsOver);
    }

    [Fact]
    public void ALostSeizeOnTheClockSaysWhereTheCaptainStoodInsteadOfTheThrone()
    {
        var lost = Start(map: Hall(limit: 1)).Do(new EndPhase()).Do(new EndPhase());

        Assert.Equal("Lost because turn 1 ended and the captain ended at 1,1, not on the gate at 12,1.", Objective.Verdict(lost, Starter));
    }

    [Fact]
    public void ALostRoutOnTheClockCountsTheEnemiesLeft()
    {
        var lost = Start(map: Hall("rout", limit: 1)).Do(new EndPhase()).Do(new EndPhase());

        Assert.Equal("Lost because turn 1 ended and 1 enemy still stands.", Objective.Verdict(lost, Starter));
    }

    [Fact]
    public void ADeadCaptainIsNamedInTheVerdict()
    {
        var state = Start(map: Hall()).Do(new Wait("wren"));
        var dead = state with { Units = ValueList<BattleUnit>.From(state.Units.Where(u => !u.IsCaptain)) };

        Assert.Equal("Lost because the captain, hale, fell.", Objective.Verdict(dead, Starter));
    }

    [Fact]
    public void AnOngoingOrWonBattleHasNoVerdict()
    {
        var state = Start(map: Hall("survive", limit: 1));
        Assert.Null(Objective.Verdict(state, Starter));

        var won = state.Do(new EndPhase()).Do(new EndPhase());
        Assert.Equal(BattleResult.Won, won.Outcome.Result);
        Assert.Null(Objective.Verdict(won, Starter));
    }

    [Fact]
    public void ClearingASeizeMapSaysTheThroneIsStillTheObjective()
    {
        var before = Start(map: Hall());
        var after = before with { Units = ValueList<BattleUnit>.From(before.UnitsOf(Side.Player)) };

        var notice = Assert.Single(Objective.Notices(before, after, Starter, new Attack("hale", "brigand-1")));

        Assert.Equal("No enemy is left, but the map is not won: the captain, hale, must still stand on the gate at 12,1 by the end of turn 10 (now turn 1).", notice);
    }

    [Fact]
    public void ClearingARoutMapGivesNoNotice()
    {
        var before = Start(map: Hall("rout"));
        var after = before with { Units = ValueList<BattleUnit>.From(before.UnitsOf(Side.Player)) };

        Assert.Empty(Objective.Notices(before, after, Starter, new Attack("hale", "brigand-1")));
    }

    [Fact]
    public void ARecruitOnTheThroneIsToldOnlyTheCaptainSeizes()
    {
        var before = Start(map: Hall(wren: "11,2"));
        var after = before.Do(new Move("wren", new Coord(12, 1)));

        var notice = Assert.Single(Objective.Notices(before, after, Starter, new Move("wren", new Coord(12, 1))));

        Assert.Equal("Wren stands on the gate, but only the captain, hale, seizes.", notice);
        Assert.False(after.Outcome.IsOver);
    }

    [Fact]
    public void ARecruitOffTheThroneGetsNoNotice()
    {
        var before = Start(map: Hall(wren: "11,2"));
        var after = before.Do(new Move("wren", new Coord(13, 1)));

        Assert.Empty(Objective.Notices(before, after, Starter, new Move("wren", new Coord(13, 1))));
    }
}
