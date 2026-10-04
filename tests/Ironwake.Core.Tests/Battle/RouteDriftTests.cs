using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The untaken route's drift (issue 81, rounds 272 to 274; DECISIONS/0226): on a map with a
/// <c>route_drift:</c> header the first of its two groups to wake fixes the route, and on the
/// header's turn the other group wakes once and makes for the taken route's crossing.
/// </summary>
public class RouteDriftTests
{
    private static readonly Coord LineCrossing = new(8, 6);
    private static readonly Coord SouthCrossing = new(5, 5);

    private static string Field(string header = "route_drift: line 8,6; south 5,5; turn 2") =>
        $"""
        name: Field
        size: 12x7
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {header}

        ............
        ............
        ............
        ............
        ............
        ............
        ............

        units:
        P captain 0,3
        P recruit:wren 0,4
        E brigand 5,0 group:line behavior:guard
        E brigand 11,2 group:south behavior:guard
        E archer 11,6 group:camp behavior:hold

        """;

    private static BattleState Start() => BattleFixture.Start(map: Field());

    private static string South(BattleState state) => state.UnitsOf(Side.Enemy).Single(u => u.Group == "south").Id;

    /// <summary>The captain's move to 2,1, within the wake radius of the line group's brigand.</summary>
    private static readonly Move WakeLine = new("hale", new Coord(2, 1));

    /// <summary>
    /// The board after the captain's move wakes the line group, with that group then taken off the
    /// board so no fight with it runs the clock down.
    /// </summary>
    private static BattleState LineTaken()
    {
        var state = Start().Do(WakeLine);
        return state with { Units = ValueList<BattleUnit>.From(state.Units.Where(u => u.Group != "line")) };
    }

    /// <summary>Ends phases until the enemy phase of <paramref name="turn"/> has begun, collecting every event.</summary>
    private static BattleState ToEnemyPhase(BattleState state, int turn, List<GameEvent> events)
    {
        while (!(state.Phase == Side.Enemy && state.Turn == turn))
        {
            var result = state.Try(new EndPhase());
            Assert.True(result.Accepted, result.Rejection?.Message);
            events.AddRange(result.Events);
            state = result.Next;
        }

        return state;
    }

    [Fact]
    public void TheFirstRouteGroupToWakeFixesTheRoute()
    {
        Assert.Null(Start().RouteTaken);

        var state = LineTaken();

        Assert.Equal("line", state.RouteTaken);
        Assert.False(state.IsAwake("south"));
    }

    [Fact]
    public void AGroupOutsideTheHeaderFixesNoRoute()
    {
        var state = Start().Do(new EndPhase());
        state = state.Wake("camp");

        Assert.Null(Routes.AfterWake(state, new[] { "camp" }).RouteTaken);
    }

    [Fact]
    public void TheUntakenRoutesGroupWakesOnTheHeadersTurnAndNotBefore()
    {
        var events = new List<GameEvent>();
        var first = ToEnemyPhase(LineTaken(), 1, events);
        Assert.DoesNotContain(events, e => e is RouteDrifted);
        Assert.False(first.IsAwake("south"));

        var second = ToEnemyPhase(first, 2, events);

        var drifted = Assert.Single(events.OfType<RouteDrifted>());
        Assert.Equal("south", drifted.Group);
        Assert.Equal(LineCrossing, drifted.To);
        Assert.Equal(new[] { South(second) }, drifted.Units);
        Assert.True(second.IsAwake("south"));
        Assert.True(second.Drifted);
        Assert.Equal(new[] { South(second) }, second.Drifting);
    }

    [Fact]
    public void TheDriftMovesOnceABattle()
    {
        var events = new List<GameEvent>();
        var state = ToEnemyPhase(LineTaken(), 2, events);
        ToEnemyPhase(state, 4, events);

        Assert.Single(events.OfType<RouteDrifted>());
    }

    [Fact]
    public void ARouteFixedAfterTheHeadersTurnDriftsOnTheNextEnemyPhase()
    {
        var events = new List<GameEvent>();
        var late = ToEnemyPhase(Start(), 3, events).Do(new EndPhase());
        Assert.Null(late.RouteTaken);
        Assert.DoesNotContain(events, e => e is RouteDrifted);

        late = late.Do(WakeLine);
        Assert.Equal("line", late.RouteTaken);
        var result = late.Try(new EndPhase());

        Assert.Contains(result.Events, e => e is RouteDrifted { Group: "south" });
    }

    [Fact]
    public void AGroupAlreadyAwakeWhenTheDriftComesDueDoesNotMoveForIt()
    {
        var events = new List<GameEvent>();
        var state = ToEnemyPhase(LineTaken().Wake("south"), 2, events);

        Assert.DoesNotContain(events, e => e is RouteDrifted);
        Assert.True(state.Drifted);
        Assert.Empty(state.Drifting);
    }

    [Fact]
    public void ADriftingUnitWithNothingToStrikeMarchesOnTheCrossing()
    {
        var state = ToEnemyPhase(LineTaken(), 2, new List<GameEvent>());
        var rider = state.Find(South(state))!;

        var plan = EnemyAi.PlanUnit(state, Starter, rider);

        var move = Assert.IsType<Move>(plan[0]);
        Assert.True(move.To.DistanceTo(LineCrossing) < rider.At.DistanceTo(LineCrossing));
        Assert.Equal(EnemyAi.MarchTo(state, Starter, rider, new[] { LineCrossing }, state.ReachOf(rider, Starter), state.UnitsOf(Side.Player).Select(p => state.ReachOf(p, Starter)).ToList()), move.To);
    }

    [Fact]
    public void ADriftingUnitWithinTheArriveRadiusHasArrived()
    {
        var state = ToEnemyPhase(LineTaken(), 2, new List<GameEvent>());
        var rider = state.Find(South(state))!;
        Assert.Equal(LineCrossing, Routes.CrossingFor(state, rider));

        var near = rider with { At = new Coord(LineCrossing.X + Routes.ArriveRadius, LineCrossing.Y) };
        Assert.Null(Routes.CrossingFor(state.WithUnit(near), near));

        var events = new List<GameEvent>();
        var arrived = Routes.AtPhaseStart(state.WithUnit(near), events);
        Assert.Empty(arrived.Drifting);
    }

    [Fact]
    public void ARecallRestoresTheDrift()
    {
        var state = ToEnemyPhase(LineTaken(), 2, new List<GameEvent>()).Do(new EndPhase());
        Assert.True(state.Drifted);

        var index = state.RecallTargets().First();
        var recalled = state.Do(new Recall(index));

        Assert.False(recalled.Drifted);
        Assert.Empty(recalled.Drifting);
        Assert.False(recalled.IsAwake("south"));
    }

    [Fact]
    public void TheBoardPrintsTheDriftWithItsTurn()
    {
        Assert.Equal(
            "drift: on turn 2's enemy phase the untaken route's group wakes and makes for your crossing: the south group for 8,6 if the line group wakes first, the line group for 5,5 if the south group does",
            Routes.Line(Start()));
        Assert.Equal(
            "drift: the south group wakes on turn 2's enemy phase and makes for 8,6, the crossing you took, behind you",
            Routes.Line(LineTaken()));

        var state = ToEnemyPhase(LineTaken(), 2, new List<GameEvent>());
        Assert.Equal("drift: the south group is making for 8,6, the crossing you took", Routes.Line(state));
        Assert.Contains("drift: the south group is making for 8,6", MapRenderer.Render(state, Starter));
    }

    [Fact]
    public void TheLineBeforeARouteIsFixedPrintsTheCurrentTurnOnceTheHeadersTurnHasPassed()
    {
        var late = ToEnemyPhase(Start(), 2, new List<GameEvent>()).Do(new EndPhase());
        Assert.Equal(3, late.Turn);
        Assert.Null(late.RouteTaken);

        Assert.Equal(
            "drift: on turn 3's enemy phase the untaken route's group wakes and makes for your crossing: the south group for 8,6 if the line group wakes first, the line group for 5,5 if the south group does",
            Routes.Line(late));
        Assert.Contains("drift: on turn 3's enemy phase", MapRenderer.Render(late, Starter));
    }

    [Fact]
    public void TheHeaderRoundTripsThroughTheWriter()
    {
        var map = MapFixture.Parse(Field());

        Assert.Equal(new RouteDrift("line", LineCrossing, "south", SouthCrossing, 2), map.RouteDrift);
        Assert.Equal(map.RouteDrift, MapFixture.Parse(MapFormat.Write(map, Starter)).RouteDrift);
    }

    [Theory]
    [InlineData("route_drift: line 8,6; south 5,5", "needs two route groups with their crossings and a turn")]
    [InlineData("route_drift: line 8,6; line 5,5; turn 2", "is named twice")]
    [InlineData("route_drift: line 8,6; camp 5,5; turn 2", "group 'camp' has no guard member")]
    [InlineData("route_drift: line 8,6; south 50,5; turn 2", "is outside the map")]
    [InlineData("route_drift: line 8,6; south 5,5; turn 11", "outside 1 to the turn limit 10")]
    [InlineData("route_drift: line 8; south 5,5; turn 2", "is not a group and its crossing")]
    public void ABadHeaderIsRefusedNamingTheField(string header, string message)
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Field(header)));

        Assert.Contains("route_drift", error.Message);
        Assert.Contains(message, error.Message);
    }
}
