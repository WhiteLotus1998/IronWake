using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Battle;

using static BattleFixture;

/// <summary>
/// Issue 263: on an Escape map gate 1's row prints the median number of recruits that got
/// out over the wins and the count of captain-alone wins, and each gate 4 row prints the
/// same over its benched arm. A recruit left behind is not out (issue 269). Printed on
/// Escape maps only, and never gated.
/// </summary>
public class EscapeSurvivorsTests
{
    private static string Yard(bool walled) => $"""
        name: Yard
        size: 6x4
        win: escape
        turn_limit: 3
        recall: 3
        enemy_level: 1
        exit: 2,0 2,1 2,2

        {(walled ? "...#..\n...#..\n...#..\n...#.." : "......\n......\n......\n......")}

        units:
        P captain 0,1
        P recruit:wren {(walled ? "5,0" : "0,2")}
        E brigand 5,3 group:far behavior:guard

        """;

    /// <summary>Wren shares the exits' side of the yard.</summary>
    private static MapDefinition Open => MapFixture.Parse(Yard(walled: false));

    /// <summary>Wren starts in a pocket walled off from every exit, so the captain leaves her behind.</summary>
    private static MapDefinition Walled => MapFixture.Parse(Yard(walled: true));

    private static GameResult Game(bool won, int recruits, int recruitsOut) =>
        new(won ? BattleResult.Won : BattleResult.Lost, 3, new Dictionary<string, ActionMix>(), won ? LossCause.None : LossCause.Timeout) { Recruits = recruits, RecruitsOut = recruitsOut };

    [Fact]
    public void SurvivorsPrintsTheMedianOverTheWinsAndTheCaptainAloneWins()
    {
        var games = new[] { Game(true, 4, 0), Game(true, 4, 3), Game(true, 4, 0), Game(false, 4, 4), Game(true, 4, 2) };
        Assert.Equal("survivors p50 1 of 4, captain alone 2", Gates.Survivors(games));
    }

    [Fact]
    public void SurvivorsPrintsADashWhenNothingWasWon()
    {
        Assert.Equal("survivors -", Gates.Survivors(new[] { Game(false, 4, 0) }));
    }

    [Fact]
    public void AWinWithNoRecruitsDeployedIsNotACaptainAloneWin()
    {
        Assert.Equal("survivors p50 0 of 0, captain alone 0", Gates.Survivors(new[] { Game(true, 0, 0) }));
    }

    [Fact]
    public void GateOneOnAnEscapeMapCountsARecruitThatGotOut()
    {
        var (gate1, games) = Gates.Gate1(Starter, Open, "yard", 3);
        Assert.All(games, g => Assert.Equal((1, 1), (g.Recruits, g.RecruitsOut)));
        Assert.Contains("survivors p50 1 of 1, captain alone 0, two-roll average", gate1.Line);
    }

    [Fact]
    public void GateOneOnAnEscapeMapCountsARecruitLeftBehindAsNotOut()
    {
        var (gate1, games) = Gates.Gate1(Starter, Walled, "walled", 3);
        Assert.All(games, g => Assert.True(g.Won));
        Assert.Contains("heuristic wins 3/3", gate1.Line);
        Assert.Contains("survivors p50 0 of 1, captain alone 3", gate1.Line);
    }

    [Fact]
    public void GateFourRowsOnAnEscapeMapPrintTheBenchedArmsSurvivors()
    {
        var map = Open;
        var (_, baseline) = Gates.Gate1(Starter, map, "yard", 3);
        var result = Gates.Gate4(Starter, map, "yard", baseline);
        Assert.Contains("wren: drop", result.Line);
        Assert.Contains("benched survivors p50 0 of 0, captain alone 0", result.Line);
    }

    [Fact]
    public void RowsOnAMapThatIsNotEscapePrintNoSurvivors()
    {
        var rout = MapFixture.Parse(Yard(walled: false).Replace("win: escape", "win: rout").Replace("exit: 2,0 2,1 2,2\n", ""));
        var (gate1, baseline) = Gates.Gate1(Starter, rout, "rout", 2);
        var gate4 = Gates.Gate4(Starter, rout, "rout", baseline);
        Assert.DoesNotContain("survivors", gate1.Line);
        Assert.DoesNotContain("survivors", gate4.Line);
    }

    private static GameResult Out(bool won, params string[] ids) =>
        Game(won, 3, ids.Length) with { Out = ids.ToHashSet(StringComparer.Ordinal) };

    [Fact]
    public void UnitsOutScoresAWinAsTheCaptainPlusTheRecruitsOut()
    {
        Assert.Equal(3, Gates.UnitsOut(Out(true, "wren", "pell"), except: "teodor"));
        Assert.Equal(1, Gates.UnitsOut(Out(true), except: "teodor"));
    }

    [Fact]
    public void UnitsOutScoresALossZeroWhateverGotOut()
    {
        Assert.Equal(0, Gates.UnitsOut(Out(false, "wren", "pell"), except: "teodor"));
    }

    [Fact]
    public void UnitsOutLeavesTheBenchedRecruitOutOfTheCount()
    {
        Assert.Equal(2, Gates.UnitsOut(Out(true, "wren", "pell"), except: "wren"));
    }

    [Fact]
    public void GateFourOnAnEscapeMapPairsUnitsOutAndPrintsTheWinDropBeside()
    {
        var map = Open;
        var (_, baseline) = Gates.Gate1(Starter, map, "yard", 3);
        Assert.All(baseline, g => Assert.Contains("wren", g.Out));
        var result = Gates.Gate4(Starter, map, "yard", baseline);
        Assert.Contains("paired on units out (a win scores 1 to 1, a loss 0)", result.Line);
        Assert.Contains("wren: drop 0.000 se 0.000 win drop 0.000 se 0.000 own", result.Line);
    }

    [Fact]
    public void GateFourOnAMapThatIsNotEscapePairsWins()
    {
        var rout = MapFixture.Parse(Yard(walled: false).Replace("win: escape", "win: rout").Replace("exit: 2,0 2,1 2,2\n", ""));
        var (_, baseline) = Gates.Gate1(Starter, rout, "rout", 2);
        var gate4 = Gates.Gate4(Starter, rout, "rout", baseline);
        Assert.DoesNotContain("units out", gate4.Line);
        Assert.DoesNotContain("win drop", gate4.Line);
    }
}
