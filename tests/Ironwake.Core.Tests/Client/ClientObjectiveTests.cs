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
        Assert.Contains("throne at 12,1", client.Objective);
    }

    [Fact]
    public void ARecruitOnTheThroneLogsThatOnlyTheCaptainSeizes()
    {
        var client = Client();

        Assert.True(client.Submit(new Move("wren", new Coord(12, 1))));

        Assert.Equal("wren stands on the throne, but only the captain, hale (A), seizes", client.Log[^1]);
    }

    [Fact]
    public void ATimedOutSeizeLogsNothingAfterItsLastPhaseAndGivesItsVerdict()
    {
        var client = Client();

        Assert.True(client.Submit(new EndPhase()));
        Assert.Null(client.Verdict);
        client.Continue();

        Assert.Equal("-- enemy phase ends, turn 1 --", client.Log[^1]);
        Assert.DoesNotContain(client.Log, line => line.Contains("turn 2", StringComparison.Ordinal));
        Assert.Equal("lost because turn 1 ended and the captain ended at 1,1, not on the throne at 12,1", client.Verdict);
    }
}
