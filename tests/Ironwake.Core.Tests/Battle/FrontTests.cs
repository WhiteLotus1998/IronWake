using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Fronts (issue 692, the keep finale's first slice): on a map with a <c>fronts:</c> header a front
/// falls the first time an enemy stands on one of its tiles after a command, once, firing its
/// <c>falls</c> events, whose spawns may land off the edge. A fall never loses the map. Members of an
/// <c>oathbound</c> group never break, and the boss's forecast says so.
/// </summary>
public class FrontTests
{
    private const string Header = "fronts: gate 3,2; north_breach 3,0\n";

    private const string Events = """

        events:
        inside falls gate spawn brigand 1,1 group:inside behavior:aggressive

        """;

    /// <summary>A 7x5 field: the captain far west, a soldier east of the gate at 3,2.</summary>
    private static string Field(string header = Header, string events = Events) =>
        "name: Field\nsize: 7x5\nwin: rout\nturn_limit: 10\nrecall: 3\nenemy_level: 1\n"
        + header
        + "\n.......\n.......\n.......\n.......\n.......\n"
        + "\nunits:\nP captain 0,4\nP recruit:wren 0,3\nE soldier 5,2 group:van behavior:hold\nE archer 6,4 group:van behavior:hold\n"
        + events;

    /// <summary>The field with the soldier stood on the gate's tile, then the captain's Wait.</summary>
    private static ApplyResult Breached(BattleState? state = null)
    {
        state ??= Start(map: Field());
        state = state.WithUnit(state.Find("soldier-1")! with { At = new Coord(3, 2) });
        var result = state.Try(new Wait("hale"));
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result;
    }

    [Fact]
    public void AFrontFallsWhenAnEnemyStandsOnItsTileAndTheMapGoesOn()
    {
        var result = Breached();

        Assert.Contains(result.Events, e => e is FrontFell { Front: "gate" });
        Assert.Equal(ValueList<string>.Of("gate"), result.Next.Fallen);
        Assert.False(result.Next.Outcome.IsOver);
        Assert.Contains("fallen gate", result.Next.Canonical());
    }

    [Fact]
    public void AnEnemyMovingThroughAFrontsTileFellsIt()
    {
        var state = Start(map: Field()).Do(new EndPhase());
        state = state.WithUnit(state.Find("soldier-1")! with { At = new Coord(4, 2) });

        var result = state.Try(new Move("soldier-1", new Coord(2, 2)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(result.Events, e => e is UnitMoved { Path: var path } && path.Contains(new Coord(3, 2)));
        Assert.Contains(result.Events, e => e is FrontFell { Front: "gate" });
    }

    [Fact]
    public void APlayerMovingThroughAFrontsTileDoesNotFellIt()
    {
        var state = Start(map: Field());
        state = state.WithUnit(state.Find("wren")! with { At = new Coord(2, 2) });

        var result = state.Try(new Move("wren", new Coord(4, 2)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(result.Events, e => e is UnitMoved { Path: var path } && path.Contains(new Coord(3, 2)));
        Assert.DoesNotContain(result.Events, e => e is FrontFell);
    }

    [Fact]
    public void AFallenFrontLetsItsWaveInOffTheEdge()
    {
        var result = Breached();

        Assert.Contains(result.Events, e => e is MapEventFired { Name: "inside", Blocked: false });
        Assert.Equal(new Coord(1, 1), result.Next.Find("brigand-1")!.At);
    }

    [Fact]
    public void AFrontFallsOnce()
    {
        var after = Breached().Next;

        var again = after.Try(new Wait("wren"));

        Assert.True(again.Accepted, again.Rejection?.Message);
        Assert.DoesNotContain(again.Events, e => e is FrontFell);
        Assert.Equal(ValueList<string>.Of("gate"), again.Next.Fallen);
    }

    [Fact]
    public void AFrontWithNoEnemyOnItStands()
    {
        var state = Start(map: Field());

        var result = state.Try(new Wait("hale"));

        Assert.DoesNotContain(result.Events, e => e is FrontFell);
        Assert.Empty(result.Next.Fallen);
        Assert.Null(result.Next.Find("brigand-1"));
    }

    [Fact]
    public void ThePlayerOnAFrontsTileDoesNotFellIt()
    {
        var state = Start(map: Field());
        state = state.WithUnit(state.Find("wren")! with { At = new Coord(3, 2) });

        var result = state.Try(new Wait("hale"));

        Assert.DoesNotContain(result.Events, e => e is FrontFell);
    }

    [Fact]
    public void TheBoardPrintsEachFrontHoldingOrFallen()
    {
        var state = Start(map: Field());

        Assert.Equal("fronts: gate 3,2 holding; north breach 3,0 holding", MapRenderer.FrontsLine(state));
        Assert.Equal("fronts: gate 3,2 fallen; north breach 3,0 holding", MapRenderer.FrontsLine(Breached().Next));
        Assert.Contains(Fronts.Rule, Objective.Rules(state, Starter));
        Assert.Null(MapRenderer.FrontsLine(Start(map: Field(header: "", events: "\n"))));
    }

    [Fact]
    public void TheFallPrintsTheWaveIsInside()
    {
        Assert.Equal("north breach falls: the wave is inside", Fronts.FallLine(new Front("north_breach", ValueList<Coord>.Of(new Coord(3, 0)))));
    }

    [Fact]
    public void TheFrontsHeaderRoundTrips()
    {
        var map = MapFixture.Parse(Field());

        Assert.Equal(2, map.Fronts.Count);
        Assert.Equal(new Front("gate", ValueList<Coord>.Of(new Coord(3, 2))), map.Fronts[0]);
        Assert.Equal(map, MapFixture.Parse(MapFormat.Write(map, Starter)));
    }

    [Theory]
    [InlineData("fronts: gate\n", "a name and at least one tile")]
    [InlineData("fronts: gate 3,2; gate 3,0\n", "listed twice")]
    [InlineData("fronts: gate 3,2; north 3,2\n", "a tile belongs to one front")]
    [InlineData("fronts: Gate 3,2\n", "lower case")]
    [InlineData("fronts: gate 9,2\n", "outside")]
    [InlineData("fronts: gate 5,2\n", "would fall before the first command")]
    [InlineData("fronts: north 3,0\n", "names no such front")]
    public void ABadFrontsHeaderIsRefused(string header, string expected)
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Field(header: header)));

        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void AFallsTriggerWithoutTheHeaderIsRefused()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Field(header: "")));

        Assert.Contains("no fronts: header", error.Message);
    }

    [Fact]
    public void ATurnSpawnOffTheEdgeIsStillRefused()
    {
        var text = Field(events: Events.Replace("falls gate", "turn 3 enemy"));

        var error = Assert.Throws<MapException>(() => MapFixture.Parse(text));

        Assert.Contains("not on the edge", error.Message);
    }

    private static string SwornField(bool oathbound) =>
        "name: Field\nsize: 7x5\nwin: rout\nturn_limit: 10\nrecall: 3\nenemy_level: 1\nbreak: on\n"
        + (oathbound ? "oathbound: keep\n" : "")
        + "\n.......\n.......\n.......\n.......\n.......\n"
        + "\nunits:\nP captain 1,2\nP recruit:wren 0,4\nB bandit_leader 2,2 group:keep behavior:boss\nE soldier 6,0 group:keep behavior:hold\n\n";

    [Fact]
    public void TheSwornNeverBreak()
    {
        var state = Start(map: SwornField(oathbound: true));
        state = state.WithUnit(state.Find("soldier-1")! with { Hp = 1 });
        var boss = state.Find("bandit_leader-1")!;

        Assert.Empty(Break.WouldBreak(state, Starter, boss));
        Assert.Equal(new[] { "soldier-1" }, Break.Sworn(state, boss).Select(u => u.Id));
        Assert.Contains("  sworn: will not break: soldier-1", PlaySession.BreakLines(state, Starter, state.Find("hale")!, boss));
    }

    [Fact]
    public void AnUnswornMemberStillBreaks()
    {
        var state = Start(map: SwornField(oathbound: false));
        state = state.WithUnit(state.Find("soldier-1")! with { Hp = 1 });
        var boss = state.Find("bandit_leader-1")!;

        Assert.Single(Break.WouldBreak(state, Starter, boss));
        Assert.Empty(Break.Sworn(state, boss));
        Assert.DoesNotContain(PlaySession.BreakLines(state, Starter, state.Find("hale")!, boss), l => l.Contains("sworn"));
    }
}
