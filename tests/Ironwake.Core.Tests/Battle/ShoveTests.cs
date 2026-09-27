using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Shove (DESIGN.md 13.12, experiment): on a <c>shove: on</c> map a player unit pushes an
/// orthogonally adjacent unit one tile directly away as its action. The tile beyond must be on
/// the map, passable for the pushed unit and empty, and the pusher's heft (Str + Def) must be at
/// least the target's. The pushed unit keeps its flags; the AI never shoves.
/// </summary>
public class ShoveTests
{
    private static string Lane(bool shove, string units) =>
        $"""
        name: Lane
        size: 6x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(shove ? "shove: on" : "")}

        ......
        ......
        ...~..
        ......

        units:
        {units}
        """.Replace("\n\n\n", "\n\n");

    private const string Beside = """
        P captain 1,1
        P recruit:wren 0,3
        E brigand 2,1 group:lane behavior:aggressive
        E soldier 5,3 group:lane behavior:aggressive

        """;

    /// <summary>A lane with Hale made heavy enough to move anything in the starter content.</summary>
    private static BattleState Start(string units = Beside, bool shove = true)
    {
        var state = BattleFixture.Start(map: Lane(shove, units));
        var hale = state.Find("hale")!;
        return state.WithUnit(hale with { Unit = hale.Unit with { Stats = hale.Unit.Stats with { Str = 30 } } });
    }

    [Fact]
    public void AShovePushesTheTargetOneTileDirectlyAwayAndIsThePushersAction()
    {
        var state = Start();

        var result = state.Try(new Shove("hale", "brigand-1"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(new Shoved("hale", "brigand-1", new Coord(2, 1), new Coord(3, 1)), result.Events);
        Assert.Equal(new Coord(3, 1), result.Next.Find("brigand-1")!.At);
        var hale = result.Next.Find("hale")!;
        Assert.Equal(new Coord(1, 1), hale.At);
        Assert.True(hale.Acted);
        Assert.True(hale.Moved);
        Assert.Null(hale.Canto);
    }

    [Fact]
    public void AShovedUnitKeepsItsOwnMovedAndActedFlags()
    {
        var state = Start(units: """
            P captain 1,1
            P recruit:wren 1,2
            E brigand 5,0 group:lane behavior:aggressive

            """);

        var next = state.Do(new Shove("hale", "wren"));

        var wren = next.Find("wren")!;
        Assert.Equal(new Coord(1, 3), wren.At);
        Assert.False(wren.Moved);
        Assert.False(wren.Acted);
        Assert.True(next.Try(new Move("wren", new Coord(2, 3))).Accepted);
    }

    [Fact]
    public void AShoveAfterAMoveIsLegal()
    {
        var state = Start(units: """
            P captain 0,1
            P recruit:wren 0,3
            E brigand 2,1 group:lane behavior:aggressive
            E soldier 5,3 group:lane behavior:aggressive

            """).Do(new Move("hale", new Coord(1, 1)));

        Assert.True(state.Try(new Shove("hale", "brigand-1")).Accepted);
    }

    [Fact]
    public void AShoveNeedsTheShoveHeader()
    {
        var state = Start(shove: false);

        Assert.Equal(RejectionReason.CannotShove, state.Refused(new Shove("hale", "brigand-1")).Reason);
        Assert.DoesNotContain(Resolver.Legal(state, Starter), c => c is Shove);
    }

    [Fact]
    public void AShoveNeedsALivingTarget()
    {
        Assert.Equal(RejectionReason.NoSuchTarget, Start().Refused(new Shove("hale", "nobody")).Reason);
    }

    [Fact]
    public void AShoveNeedsTheTargetOrthogonallyAdjacent()
    {
        var state = Start(units: """
            P captain 1,1
            P recruit:wren 0,3
            E brigand 2,0 group:lane behavior:aggressive

            """);

        var rejection = state.Refused(new Shove("hale", "brigand-1"));

        Assert.Equal(RejectionReason.CannotShove, rejection.Reason);
        Assert.Contains("not beside", rejection.Message);
    }

    [Fact]
    public void AShoveOffTheMapIsRefused()
    {
        var state = Start(units: """
            P captain 4,1
            P recruit:wren 0,3
            E brigand 5,1 group:lane behavior:aggressive

            """);

        Assert.Contains("off the map", state.Refused(new Shove("hale", "brigand-1")).Message);
    }

    [Fact]
    public void AShoveOntoTerrainTheTargetCannotStandOnIsRefused()
    {
        var state = Start(units: """
            P captain 3,0
            P recruit:wren 0,3
            E brigand 3,1 group:lane behavior:aggressive

            """);

        Assert.Contains("cannot stand on 3,2", state.Refused(new Shove("hale", "brigand-1")).Message);
    }

    [Fact]
    public void AShoveIntoAnOccupiedTileIsRefused()
    {
        var state = Start(units: """
            P captain 1,1
            P recruit:wren 0,3
            E brigand 2,1 group:lane behavior:aggressive
            E soldier 3,1 group:lane behavior:aggressive

            """);

        Assert.Contains("stands on 3,1", state.Refused(new Shove("hale", "brigand-1")).Message);
    }

    [Fact]
    public void AShoveNeedsHeftAtLeastTheTargets()
    {
        var state = Start();
        var brigand = state.Find("brigand-1")!;
        state = state.WithUnit(brigand with { Unit = brigand.Unit with { Stats = brigand.Unit.Stats with { Def = 60 } } });

        var rejection = state.Refused(new Shove("hale", "brigand-1"));

        Assert.Contains("heft", rejection.Message);
        Assert.True(Resolver.Heft(Starter, state.Find("hale")!) < Resolver.Heft(Starter, state.Find("brigand-1")!));
    }

    [Fact]
    public void AnAllyShoveIgnoresHeftAndAnEnemyShoveStillChecksIt()
    {
        var state = BattleFixture.Start(map: Lane(true, """
            P captain 1,2
            P recruit:wren 1,1
            E brigand 2,1 group:lane behavior:aggressive

            """));
        var brigand = state.Find("brigand-1")!;
        state = state.WithUnit(brigand with { Unit = brigand.Unit with { Stats = brigand.Unit.Stats with { Def = 60 } } });
        Assert.True(Resolver.Heft(Starter, state.Find("wren")!) < Resolver.Heft(Starter, state.Find("hale")!));

        var ally = state.Try(new Shove("wren", "hale"));
        var enemy = state.Refused(new Shove("wren", "brigand-1"));

        Assert.True(ally.Accepted, ally.Rejection?.Message);
        Assert.Equal(new Coord(1, 3), ally.Next.Find("hale")!.At);
        Assert.Contains("heft", enemy.Message);
        Assert.Contains(new Shove("wren", "hale"), Resolver.Legal(state, Starter));
        Assert.DoesNotContain(new Shove("wren", "brigand-1"), Resolver.Legal(state, Starter));
    }

    [Fact]
    public void APushedAllyFiresTheEnterEventOfTheTileItLandsOn()
    {
        var map = Lane(true, """
            P captain 1,1
            P recruit:wren 1,2
            E brigand 5,0 group:lane behavior:aggressive

            events:
            crossed enter 1,3 flag crossed

            """);

        var result = BattleFixture.Start(map: map).Try(new Shove("hale", "wren"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(new FlagSet("crossed"), result.Events);
        Assert.True(result.Next.HasFired("crossed"));
    }

    [Fact]
    public void APushedEnemyFiresNoEnterEvent()
    {
        var map = Lane(true, """
            P captain 1,1
            P recruit:wren 5,3
            E brigand 2,1 group:lane behavior:aggressive

            events:
            crossed enter 3,1 flag crossed

            """);
        var state = BattleFixture.Start(map: map);
        var hale = state.Find("hale")!;
        state = state.WithUnit(hale with { Unit = hale.Unit with { Stats = hale.Unit.Stats with { Str = 30 } } });

        var result = state.Try(new Shove("hale", "brigand-1"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.DoesNotContain(result.Events, e => e is FlagSet);
    }

    [Fact]
    public void AnAllyPushedOntoAnExitStaysUntilItTakesItsOwnExit()
    {
        var map = Lane(true, """
            P captain 1,1
            P recruit:wren 1,2
            E brigand 5,0 group:lane behavior:aggressive

            """).Replace("win: rout", "win: escape").Replace("enemy_level: 1\n", "enemy_level: 1\nexit: 1,3 2,3\n");

        var pushed = BattleFixture.Start(map: map).Do(new Shove("hale", "wren"));

        Assert.Equal(new Coord(1, 3), pushed.Find("wren")!.At);
        Assert.False(pushed.HasEscaped("wren"));
        Assert.True(pushed.Try(new Exit("wren")).Accepted);
    }

    [Fact]
    public void OnlyPlayerUnitsShove()
    {
        var state = Start().Do(new Wait("hale")).Do(new Wait("wren")).Do(new EndPhase());

        var rejection = state.Refused(new Shove("brigand-1", "hale"));

        Assert.Equal(RejectionReason.CannotShove, rejection.Reason);
        Assert.Contains("only player units", rejection.Message);
    }

    [Fact]
    public void LegalListsEachShoveTheResolverWouldAccept()
    {
        var state = Start();

        var shoves = Resolver.Legal(state, Starter).OfType<Shove>().ToList();

        Assert.Equal(new[] { new Shove("hale", "brigand-1") }, shoves);
        Assert.All(shoves, s => Assert.True(state.Try(s).Accepted));
    }

    [Fact]
    public void AShoveIsNoiseThatWakesASleepingGroup()
    {
        var state = Start(units: """
            P captain 1,1
            P recruit:wren 0,3
            E brigand 2,1 group:post behavior:guard
            E soldier 5,3 group:far behavior:aggressive

            """);

        var result = state.Try(new Shove("hale", "brigand-1"));

        Assert.Contains(result.Events, e => e is GroupWoke { Group: "post" });
    }

    [Fact]
    public void RecallPutsTheShovedUnitBack()
    {
        var state = Start();
        var shoved = state.Do(new Shove("hale", "brigand-1"));

        var back = shoved.Do(new Recall(state.History.Count));

        Assert.Equal(new Coord(2, 1), back.Find("brigand-1")!.At);
    }

    [Fact]
    public void TheShoveHeaderRoundTripsThroughTheMapFormat()
    {
        var map = MapFixture.Parse(Lane(true, Beside), "lane.map");

        Assert.True(map.ShoveEnabled);
        Assert.Contains("shove: on\n", Ironwake.Content.MapFormat.Write(map, Starter));
        Assert.Equal(map, MapFixture.Parse(Ironwake.Content.MapFormat.Write(map, Starter), "lane.map"));
    }

    [Fact]
    public void AShoveRoundTripsThroughTheProtocol()
    {
        var shove = new Shove("hale", "brigand-1");

        Assert.Equal(shove, Ironwake.Content.Protocol.ProtocolJson.ReadCommand(Ironwake.Content.Protocol.ProtocolJson.Command(shove)));
    }
}
