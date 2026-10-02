using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The hold-then-boss objective (issue 692, the keep finale's third slice): a <c>spawn boss</c>
/// event brings the boss on an announced turn. A Defeat Boss map is not won until every boss
/// spawn has fired and no boss stands, a unit on the boss's tile does not stop him (he lands on
/// the nearest free tile), and a turn limit passed with the boss standing is his escape.
/// </summary>
public class BossArrivalTests
{
    private const string Assault = """

        events:
        assault turn 2 enemy spawn boss bandit_leader 6,2 group:assault behavior:boss

        """;

    /// <summary>A 7x5 field: the captain west, one soldier holding east; the boss comes on turn 2.</summary>
    private static string Field(int limit = 4, string events = Assault) =>
        $"name: Field\nsize: 7x5\nwin: defeat_boss\nturn_limit: {limit}\nrecall: 3\nenemy_level: 1\nannounce: on\n"
        + "\n.......\n.......\n.......\n.......\n.......\n"
        + "\nunits:\nP captain 0,4\nP recruit:wren 0,3\nE soldier 5,0 group:van behavior:hold\n"
        + events;

    /// <summary>The field at the start of turn 2's enemy phase, when the boss arrives.</summary>
    private static ApplyResult ToArrival(BattleState? state = null)
    {
        state ??= Start(map: Field());
        state = state.Do(new EndPhase()).Do(new EndPhase());
        var result = state.Try(new EndPhase());
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result;
    }

    [Fact]
    public void ADefeatBossMapIsNotWonBeforeItsBossArrives()
    {
        var state = Start(map: Field());

        Assert.DoesNotContain(state.Units, u => u.IsBoss);
        Assert.False(state.Outcome.IsOver);
    }

    [Fact]
    public void TheBossArrivesOnTheAnnouncedTurnAsABoss()
    {
        var result = ToArrival();

        var boss = Assert.Single(result.Next.Units, u => u.IsBoss);
        Assert.Equal(new Coord(6, 2), boss.At);
        Assert.Equal(Behavior.Boss, boss.Behavior);
        Assert.Contains(result.Events, e => e is UnitSpawned { At: var at } && at == new Coord(6, 2));
    }

    [Fact]
    public void AUnitOnTheBossesTileDoesNotStopHimHeTakesTheNearestFreeTile()
    {
        var start = Start(map: Field());
        start = start.WithUnit(start.Find("wren")! with { At = new Coord(6, 2) });

        var result = ToArrival(start);

        var boss = Assert.Single(result.Next.Units, u => u.IsBoss);
        Assert.Equal(new Coord(6, 1), boss.At);
        Assert.Contains(result.Events, e => e is MapEventFired { Name: "assault", Blocked: false });
    }

    [Fact]
    public void TheMapIsWonWhenTheArrivedBossFalls()
    {
        var arrived = ToArrival().Next;

        var fallen = arrived with { Units = ValueList<BattleUnit>.From(arrived.Units.Where(u => !u.IsBoss)) };

        Assert.Equal(BattleResult.Won, fallen.Outcome.Result);
    }

    [Fact]
    public void TheTurnLimitPassedWithTheBossStandingIsHisEscape()
    {
        var state = ToArrival(Start(map: Field(limit: 2))).Next;
        var lost = state.Do(new EndPhase());

        Assert.Equal(BattleResult.Lost, lost.Outcome.Result);
        Assert.Equal("Lost because turn 2 ended and the boss got away.", Objective.Verdict(lost, Starter));
    }

    [Fact]
    public void TheObjectiveLineSaysHoldThenDefeatTheBoss()
    {
        var state = Start(map: Field());

        Assert.StartsWith("Hold until the boss arrives on turn 2, then defeat the boss by the end of turn 4.", Objective.Line(state, Starter));
        Assert.Contains(Objective.Rules(state, Starter), r => r.Contains("A unit there does not stop the boss", StringComparison.Ordinal));
    }

    [Fact]
    public void ABossSpawnRoundTrips()
    {
        var map = MapFixture.Parse(Field());

        Assert.True(Assert.IsType<SpawnEnemy>(map.Events[0].Action).Placement.IsBoss);
        Assert.Equal(map, MapFixture.Parse(MapFormat.Write(map, Starter)));
    }

    [Fact]
    public void ABossSpawnNeedsATurnTrigger()
    {
        var events = "\nevents:\nassault enter 1,1 spawn boss bandit_leader 6,2 group:assault behavior:boss\n";

        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Field(events: events)));

        Assert.Contains("a boss spawn needs a turn trigger", error.Message);
    }

    [Fact]
    public void ADefeatBossMapNeedsABossLineOrABossSpawn()
    {
        var events = "\nevents:\nassault turn 2 enemy spawn bandit_leader 6,2 group:assault behavior:aggressive\n";

        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Field(events: events)));

        Assert.Contains("no B line and no boss spawn", error.Message);
    }
}
