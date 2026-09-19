using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Battle;

using static BattleFixture;

/// <summary>
/// Issue 12's acceptance for gates 1 to 5 as functions: a one-shot enemy at deployment
/// fails gate 3 and the waiver prints instead of failing; a walled-off recruit fails gate
/// 4; a recruit within its own margin of the threshold passes; the heuristic player's
/// captain refuses a lethal tile and takes a survivable one.
/// </summary>
public class SimGateTests
{
    private const string OneShot = """
        name: Cheap shots
        size: 6x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 30

        ......
        ......
        ......
        ......

        units:
        P captain 0,0
        P recruit 5,3
        E brigand 4,3 group:yard behavior:aggressive
        E soldier 5,2 group:yard behavior:aggressive

        """;

    [Fact]
    public void GateThreeFailsWhenTheEnemyPhaseKillsFromDeployment()
    {
        var result = Gates.Gate3(Starter, MapFixture.Parse(OneShot), "cheap");
        Assert.False(result.Passed, result.Line);
        Assert.Contains("killed from deployment: FAILED", result.Line);
    }

    [Fact]
    public void GateThreePrintsTheWaiverInsteadOfSkippingSilently()
    {
        var waived = OneShot.Replace("enemy_level: 30", "enemy_level: 30\ncheap_shots: allowed");
        var result = Gates.Gate3(Starter, MapFixture.Parse(waived), "cheap");
        Assert.True(result.Passed, result.Line);
        Assert.Contains("waived by cheap_shots: allowed", result.Line);
        Assert.Contains("1 player units killed", result.Line);
    }

    private const string WalledOff = """
        name: Walled off
        size: 8x5
        win: survive
        turn_limit: 6
        recall: 3
        enemy_level: 1

        ........
        ..###...
        ..#.#...
        ..###...
        ........

        units:
        P captain 0,0
        P recruit 0,1
        P recruit 3,2
        E brigand 7,4 group:yard behavior:aggressive
        E brigand 7,3 group:yard behavior:aggressive
        E brigand 6,4 group:yard behavior:aggressive

        """;

    [Fact]
    public void GateFourFailsARecruitDeployedOutOfReachOfEverything()
    {
        var map = MapFixture.Parse(WalledOff);
        var (gate1, baseline) = Gates.Gate1(Starter, map, "walled", 30);
        var result = Gates.Gate4(Starter, map, "walled", baseline);
        Assert.False(result.Passed, gate1.Line + "\n" + result.Line);
        Assert.Contains("recruit-2: drop 0.000 se 0.000", result.Line);
        Assert.Contains("recruit-2: drop 0.000 se 0.000 baseline [atk 0 dmg 0 heal 0 abs 0]", result.Line);
        Assert.Contains("DEAD WEIGHT", result.Line);
        Assert.DoesNotContain("wren: drop 0.000 se 0.000", result.Line);
    }

    [Fact]
    public void ARecruitWithinItsOwnMarginOfTheThresholdPasses()
    {
        var rows = new[]
        {
            new Gates.AblationRow("a", 0.40, 0.03, ActionMix.Zero, ActionMix.Zero),
            new Gates.AblationRow("b", 0.40, 0.03, ActionMix.Zero, ActionMix.Zero),
            new Gates.AblationRow("c", 0.15, 0.03, ActionMix.Zero, ActionMix.Zero),
        };
        Assert.Empty(Gates.Judge(rows));

        var failing = rows.Append(new Gates.AblationRow("d", 0.05, 0.03, ActionMix.Zero, ActionMix.Zero)).ToList();
        Assert.Equal(new[] { "d" }, Gates.Judge(failing));
    }

    [Fact]
    public void TheCaptainRefusesATileWhereTheNoCritSumReachesHisHpAndTakesOneThatPasses()
    {
        const string map = """
            name: Veto
            size: 7x3
            win: rout
            turn_limit: 10
            recall: 3
            enemy_level: 12

            .......
            .......
            .......

            units:
            P captain 0,1
            E brigand 4,1 group:yard behavior:hold
            E soldier 3,0 group:yard behavior:hold
            E soldier 3,2 group:yard behavior:hold

            """;
        var state = Start(map: map, roster: ValueList<Unit>.Of(Hale));
        var hale = state.Find("hale")!;
        var plan = HeuristicPlayer.PlanUnit(state, Starter, hale);
        var move = Assert.IsType<Move>(plan[0]);
        var sum = Exposure.Of(state, Starter, hale, move.To, plan.Count > 1 && plan[1] is Attack a ? state.Find(a.TargetId) : null);
        Assert.True(sum.NoCrit < hale.Hp, $"{move.To}: no-crit sum {sum.NoCrit} against {hale.Hp} HP");
        Assert.True(Exposure.Of(state, Starter, hale, new Coord(3, 1)).NoCrit >= hale.Hp, "the tile beside all three is lethal, so the test is a real refusal");
        Assert.NotEqual(new Coord(3, 1), move.To);
    }

    [Fact]
    public void GateOneAndTwoRunOnTheYard()
    {
        var (gate1, games) = Gates.Gate1(Starter, YardMap, "yard", 10);
        Assert.Equal(10, games.Count);
        Assert.Contains("gate 1 beatable: yard, heuristic wins", gate1.Line);
        var gate2 = Gates.Gate2(Starter, YardMap, "yard", 10);
        Assert.Contains("gate 2 decisions matter: yard, random wins", gate2.Line);
    }
}
