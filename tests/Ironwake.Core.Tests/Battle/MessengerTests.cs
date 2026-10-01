using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The messenger (DESIGN.md 13.24, experiment): on a map with a <c>messenger:</c> header the
/// enemy placed on the route's first tile never strikes; once awake it runs for its road, and on
/// reaching it leaves the board (not a kill) and fires the map's messenger events. A player unit
/// on its path blocks it as any unit blocks an enemy. Asleep, killed, or on a map without the
/// header, nothing fires.
/// </summary>
public class MessengerTests
{
    private const string Header = "messenger: 2,2 6,2\n";

    private const string Events = """

        events:
        relief messenger spawn soldier 6,0 group:relief behavior:aggressive

        """;

    /// <summary>A 7x5 field: the rider at 2,2 runs east for 6,2; the captain beside it; a wall row with one gap at 4,2 when <paramref name="lane"/>.</summary>
    private static string Field(string behavior = "aggressive", bool header = true, bool lane = false, string captain = "1,2") =>
        "name: Field\nsize: 7x5\nwin: rout\nturn_limit: 10\nrecall: 3\nenemy_level: 1\n"
        + (header ? Header : "")
        + "\n"
        + (lane ? "....#..\n....#..\n.......\n....#..\n....#..\n" : ".......\n.......\n.......\n.......\n.......\n")
        + $"\nunits:\nP captain {captain}\nP recruit:wren 6,4\nE rider 2,2 group:camp behavior:{behavior}\nE soldier 0,0 group:far behavior:hold\n"
        + (header ? Events : "\n");

    private static ApplyResult EnemyPhase(BattleState state)
    {
        state = state.Do(new EndPhase());
        var events = new List<GameEvent>();
        foreach (var command in EnemyAi.Plan(state, Starter))
        {
            var result = state.Try(command);
            Assert.True(result.Accepted, result.Rejection?.Message);
            events.AddRange(result.Events);
            state = result.Next;
        }

        return new ApplyResult(state, ValueList<GameEvent>.From(events), null);
    }

    [Fact]
    public void TheMessengerRunsForTheRoadInsteadOfStriking()
    {
        var state = Start(map: Field());
        var rider = state.Find("rider-1")!;
        Assert.True(Messenger.Is(state, rider));

        var plan = EnemyAi.PlanUnit(state.Do(new EndPhase()), Starter, state.Do(new EndPhase()).Find("rider-1")!);

        Assert.DoesNotContain(plan, c => c is Attack);
        Assert.Contains(plan, c => c is Move);
    }

    [Fact]
    public void TheMessengerOnTheRoadLeavesTheBoardAndFiresItsEvents()
    {
        var result = EnemyPhase(Start(map: Field()));

        Assert.Contains(result.Events, e => e is MessengerEscaped { UnitId: "rider-1" });
        Assert.Contains(result.Events, e => e is MapEventFired { Name: "relief", Blocked: false });
        Assert.Null(result.Next.Find("rider-1"));
        Assert.DoesNotContain(result.Events, e => e is UnitDied);
        Assert.Contains(result.Next.UnitsOf(Side.Enemy), u => u.Group == "relief");
    }

    [Fact]
    public void ASleepingMessengerWaits()
    {
        var result = EnemyPhase(Start(map: Field(behavior: "guard", captain: "6,0")));

        Assert.Equal(new Coord(2, 2), result.Next.Find("rider-1")!.At);
        Assert.DoesNotContain(result.Events, e => e is MessengerEscaped);
    }

    [Fact]
    public void APlayerUnitOnTheOnlyGapHoldsTheRoad()
    {
        var result = EnemyPhase(Start(map: Field(lane: true, captain: "4,2")));

        Assert.DoesNotContain(result.Events, e => e is MessengerEscaped);
        Assert.NotNull(result.Next.Find("rider-1"));
        Assert.DoesNotContain(result.Events, e => e is MapEventFired);
    }

    [Fact]
    public void TheMessengerThreatensNoTile()
    {
        var state = Start(map: Field());

        Assert.Empty(Threat.StruckByUnit(state, Starter, state.Find("rider-1")!));
    }

    [Fact]
    public void WithoutTheHeaderTheRiderFights()
    {
        var state = Start(map: Field(header: false));
        var enemy = state.Do(new EndPhase());

        Assert.False(Messenger.Is(enemy, enemy.Find("rider-1")!));
        Assert.Contains(EnemyAi.PlanUnit(enemy, Starter, enemy.Find("rider-1")!), c => c is Attack);
    }

    [Fact]
    public void AKilledMessengerFiresNothing()
    {
        var state = Start(map: Field());
        state = state.WithoutUnit("rider-1");

        var result = EnemyPhase(state);

        Assert.DoesNotContain(result.Events, e => e is MapEventFired or MessengerEscaped);
    }

    [Fact]
    public void TheMessengerHeaderRoundTrips()
    {
        var map = MapFixture.Parse(Field());

        Assert.Equal(new MessengerRoute(new Coord(2, 2), new Coord(6, 2)), map.Messenger);
        Assert.Equal(map, MapFixture.Parse(MapFormat.Write(map, Starter)));
    }

    [Theory]
    [InlineData("messenger: 2,2 3,2\n", "edge")]
    [InlineData("messenger: 3,3 6,2\n", "no E line")]
    [InlineData("messenger: 2,2\n", "two tiles")]
    public void ABadMessengerHeaderIsRefused(string header, string expected)
    {
        var text = Field().Replace(Header, header);

        var error = Assert.Throws<MapException>(() => MapFixture.Parse(text));

        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void AMessengerWithNoEventIsRefused()
    {
        var text = Field().Replace("relief messenger spawn", "relief turn 3 enemy spawn");

        var error = Assert.Throws<MapException>(() => MapFixture.Parse(text));

        Assert.Contains("messenger trigger", error.Message);
    }

    [Fact]
    public void AMessengerTriggerWithoutTheHeaderIsRefused()
    {
        var text = Field().Replace(Header, "");

        var error = Assert.Throws<MapException>(() => MapFixture.Parse(text));

        Assert.Contains("no messenger: header", error.Message);
    }

    [Fact]
    public void TheBoardPrintsTheMessengerAndItsDistance()
    {
        var state = Start(map: Field());

        var line = MapRenderer.MessengerLine(state, Starter);

        Assert.Equal("messenger at 2,2, running, 1 of its phases from the road at 6,2 on open ground", line);
    }

    [Fact]
    public void AKilledMessengerReadsAsFallenNotGone()
    {
        BattleState? after = null;
        for (ulong seed = 1; seed <= 40 && after is null; seed++)
        {
            var state = Start(seed, map: Field());
            state = state.WithUnit(state.Find("rider-1")! with { Hp = 1 });
            var result = state.Try(new Attack("hale", "rider-1"));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (result.Next.Find("rider-1") is null)
            {
                after = result.Next;
            }
        }

        Assert.NotNull(after);
        Assert.Equal("messenger: fallen at 2,2; the word never left", MapRenderer.MessengerLine(after, Starter));
        Assert.Contains("messenger fallen 2,2", after.Canonical());
    }

    [Fact]
    public void AnEscapedMessengerReadsAsGoneByTheRoad()
    {
        var result = EnemyPhase(Start(map: Field()));

        Assert.Equal("messenger: gone by the road at 6,2; the word is out", MapRenderer.MessengerLine(result.Next, Starter));
        Assert.Contains("messenger escaped 6,2", result.Next.Canonical());
    }

    [Fact]
    public void AMapWithoutTheHeaderPrintsNoMessengerLine()
    {
        var state = Start(map: Field(header: false));

        Assert.Null(MapRenderer.MessengerLine(state, Starter));
        Assert.Null(MapRenderer.MessengerLine(state.WithoutUnit("rider-1"), Starter));
        Assert.DoesNotContain("messenger", state.WithoutUnit("rider-1").Canonical());
    }
}
