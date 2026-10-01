using Ironwake.Client;
using Ironwake.Core.Tests.Battle;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The client's objective text (issue 374): the objective line shown for the whole battle, the
/// Seize notices in the event log, a lost battle's reason, and nothing logged after the result.
/// </summary>
public class ClientObjectiveTests
{
    private const string Hall = """
        name: Hall
        size: 16x3
        win: seize
        turn_limit: 1
        recall: 3
        enemy_level: 1

        ................
        ............T...
        ................

        units:
        P captain 1,1
        P recruit:wren 11,2
        E brigand 15,0 group:far behavior:hold

        """;

    private static ClientSession Client() => new(BattleFixture.Starter, BattleFixture.Start(map: Hall));

    [Fact]
    public void TheClientShowsTheCoresObjectiveLine()
    {
        var client = Client();

        Assert.Equal(Objective.Line(client.State, client.Content), client.Objective);
        Assert.Equal("Get the captain to the gate by the end of turn 1. Captain hale must survive.", client.Objective);
    }

    [Fact]
    public void TheObjectiveLineLeavesTheLetterAndTheTileToTheRules()
    {
        var client = Client();

        Assert.DoesNotContain("(A)", client.Objective, StringComparison.Ordinal);
        Assert.DoesNotContain("12,1", client.Objective, StringComparison.Ordinal);
        Assert.Equal("The captain, hale (A), must stand on the gate at 12,1. Only the captain seizes.", Assert.Single(Objective.Rules(client.State, client.Content)));
    }

    [Fact]
    public void ARecruitOnTheThroneLogsThatOnlyTheCaptainSeizes()
    {
        var client = Client();

        Assert.True(client.Submit(new Move("wren", new Coord(12, 1))));

        Assert.Equal("Wren stands on the gate, but only the captain, hale, seizes.", client.Log[^1]);
    }

    [Fact]
    public void ATimedOutSeizeLogsNothingAfterItsLastPhaseAndGivesItsVerdict()
    {
        var client = Client();

        Assert.True(client.Submit(new EndPhase()));
        Assert.Null(client.Verdict);
        client.Continue();

        Assert.Equal("-- Enemy phase ends, turn 1 --", client.Log[^1]);
        Assert.DoesNotContain(client.Log, line => line.Contains("turn 2", StringComparison.Ordinal));
        Assert.Equal("Lost because turn 1 ended and the captain ended at 1,1, not on the gate at 12,1.", client.Verdict);
    }
}
