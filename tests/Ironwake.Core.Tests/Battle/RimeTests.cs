using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The drake's rime breath (issue 805, experiment): on a <c>breath:</c> map a rider whose drake is Unbroken,
/// once a map, as its action, breathes a line of three tiles out through the adjacent tile it names. Every
/// unit on the line, either side, is chilled; every Water tile on it turns to Rime ice, which thaws back to
/// Water when the breather's side's next phase ends, unless a unit stands on it.
/// </summary>
public class RimeTests
{
    private const string River = """
        name: River
        size: 8x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        breath: rook

        ...~~...
        ...~~...
        ...~~...
        ...~~...
        ...~~...

        units:
        P captain 0,0
        P recruit:rook 2,2
        P recruit:wren 2,3
        E brigand 5,2 group:near behavior:guard
        E brigand 7,0 group:far behavior:guard
        """;

    private static readonly Unit Rook = Recruit("rook", "skyrider", new Stats(20, 7, 0, 6, 8, 4, 4, 2, 3), "iron_lance");

    private static readonly Coord East = new(3, 2);

    private static BattleState Start(string? map = null) =>
        BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Rook, Wren), map ?? River);

    private static string TerrainAt(BattleState state, int x, int y) => state.Map.TerrainAt(new Coord(x, y), Starter).Id;

    [Fact]
    public void TheHeadersRiderIsPlacedWithAnUnbrokenDrake()
    {
        Assert.Equal(DrakeStage.Unbroken, Start().Find("rook")!.Unit.Drake!.Stage);
    }

    [Fact]
    public void TheBreathFreezesTheWaterOnItsLineToRimeIce()
    {
        var result = Start().Try(new Breathe("rook", East));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal("rime", TerrainAt(result.Next, 3, 2));
        Assert.Equal("rime", TerrainAt(result.Next, 4, 2));
        Assert.Equal("plain", TerrainAt(result.Next, 5, 2));
        Assert.Equal("water", TerrainAt(result.Next, 3, 1));
        Assert.Contains(result.Events, e => e is Breathed { UnitId: "rook" } b && b.Line.SequenceEqual(new[] { East, new Coord(4, 2), new Coord(5, 2) }) && b.Frozen.Count == 2);
    }

    [Fact]
    public void EveryEnemyOnTheLineIsChilled()
    {
        var state = Start().Do(new Breathe("rook", East));

        Assert.Equal(1, state.Find("brigand-1")!.Chill);
        Assert.Equal(0, state.Find("brigand-2")!.Chill);
    }

    [Fact]
    public void AnAllyOnTheLineIsChilledToo()
    {
        var state = Start().Do(new Breathe("rook", new Coord(2, 3)));

        Assert.Equal(1, state.Find("wren")!.Chill);
    }

    [Fact]
    public void TheLineStopsAtTheMapsEdge()
    {
        var line = Rime.LineOf(Start().Map, new Coord(2, 2), new Coord(2, 3));

        Assert.Equal(new[] { new Coord(2, 3), new Coord(2, 4) }, line);
    }

    [Fact]
    public void TheBreathIsTheRidersAction()
    {
        var rook = Start().Do(new Breathe("rook", East)).Find("rook")!;

        Assert.True(rook.Acted);
        Assert.True(rook.Breathed);
        Assert.Null(rook.MoveAgain);
    }

    [Fact]
    public void TheBreathMayFollowAMove()
    {
        var state = Start().Do(new Move("rook", new Coord(2, 1)));

        Assert.True(state.Try(new Breathe("rook", new Coord(3, 1))).Accepted);
    }

    [Fact]
    public void TheBreathIsOnceAMap()
    {
        var state = Start().Do(new Breathe("rook", East)).Do(new EndPhase()).Do(new EndPhase());

        Assert.Equal(RejectionReason.CannotBreathe, state.Refused(new Breathe("rook", new Coord(2, 1))).Reason);
    }

    [Fact]
    public void TheBreathIsRefusedWithoutTheHeader()
    {
        var start = Start(River.Replace("breath: rook\n", ""));
        var rook = start.Find("rook")!;
        var unbroken = start.WithUnit(rook with { Unit = rook.Unit with { Drake = new DrakeState(DrakeStage.Unbroken, 2) } });

        Assert.Equal(RejectionReason.CannotBreathe, unbroken.Refused(new Breathe("rook", East)).Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheBreathIsOpenOnEveryCampaignMapWithoutAHeader(bool sideMap)
    {
        var start = Start(River.Replace("breath: rook\n", ""));
        var rook = start.Find("rook")!;
        var unbroken = start.WithUnit(rook with { Unit = rook.Unit with { Drake = new DrakeState(DrakeStage.Unbroken, 2) } });
        var campaign = sideMap ? unbroken with { SideMap = true } : unbroken with { CampaignMap = 4 };

        var result = campaign.Try(new Breathe("rook", East));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.NotNull(Rime.Line(campaign));
        Assert.Null(Rime.Line(unbroken));
    }

    [Fact]
    public void AGrownDrakeCannotBreathe()
    {
        var start = Start();
        var rook = start.Find("rook")!;
        var grown = start.WithUnit(rook with { Unit = rook.Unit with { Drake = new DrakeState(DrakeStage.Grown, 2) } });

        Assert.Equal(RejectionReason.CannotBreathe, grown.Refused(new Breathe("rook", East)).Reason);
    }

    [Fact]
    public void TheNamedTileMustBeBesideTheRider()
    {
        Assert.Equal(RejectionReason.CannotBreathe, Start().Refused(new Breathe("rook", new Coord(4, 2))).Reason);
    }

    [Fact]
    public void FootWalksTheIceWhileItHolds()
    {
        var state = Start().Do(new Breathe("rook", East));

        var result = state.Try(new Move("wren", new Coord(4, 2)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal("rime", TerrainAt(result.Next, 3, 2));
    }

    [Fact]
    public void TheIceHoldsThroughTheEnemyPhaseAndThePlayersNext()
    {
        var state = Start().Do(new Breathe("rook", East)).Do(new EndPhase());
        Assert.Equal("rime", TerrainAt(state, 3, 2));

        state = state.Do(new EndPhase());
        Assert.Equal(Side.Player, state.Phase);
        Assert.Equal("rime", TerrainAt(state, 3, 2));
        Assert.Equal(2, state.Rime[0].Clock);
    }

    [Fact]
    public void TheIceThawsWhenTheBreathersNextPhaseEnds()
    {
        var state = Start().Do(new Breathe("rook", East)).Do(new EndPhase()).Do(new EndPhase());

        var result = state.Try(new EndPhase());

        Assert.Equal("water", TerrainAt(result.Next, 3, 2));
        Assert.Equal("water", TerrainAt(result.Next, 4, 2));
        Assert.Empty(result.Next.Rime);
        Assert.Contains(new TerrainChanged(East, "water"), result.Events);
    }

    [Fact]
    public void AnOccupiedTileHoldsUntilItIsEmpty()
    {
        var state = Start().Do(new Breathe("rook", East)).Do(new EndPhase()).Do(new EndPhase())
            .Do(new Move("wren", new Coord(4, 2))).Do(new EndPhase());

        Assert.Equal("rime", TerrainAt(state, 4, 2));
        Assert.Equal("water", TerrainAt(state, 3, 2));
        Assert.Equal(3, Assert.Single(state.Rime).Clock);
        Assert.Equal("rime: 4,2 (thaws once no one stands on it)", Rime.Line(state));

        state = state.Do(new EndPhase()).WithoutUnit("brigand-1").Do(new Move("wren", new Coord(5, 2))).Do(new EndPhase());

        Assert.Equal("water", TerrainAt(state, 4, 2));
        Assert.Empty(state.Rime);
    }

    [Fact]
    public void RecallRestoresTheWaterAndTheBreath()
    {
        var start = Start();
        var state = start.Do(new Breathe("rook", East));

        var recalled = state.Do(new Recall(0));

        Assert.Equal("water", TerrainAt(recalled, 3, 2));
        Assert.False(recalled.Find("rook")!.Breathed);
        Assert.Empty(recalled.Rime);
    }

    [Fact]
    public void TheBoardPrintsTheBreathAndTheIce()
    {
        var start = Start();
        Assert.Contains("breathe rook <x,y beside it>", Rime.Line(start));

        var state = start.Do(new Breathe("rook", East));
        Assert.Equal("rime: 3,2 4,2 (thaws as the next player phase ends)", Rime.Line(state));
        Assert.Contains("rime: 3,2 4,2", MapRenderer.Render(state, Starter));
    }

    [Fact]
    public void TheBreathHeaderWritesBackCanonically()
    {
        var map = MapFixture.Parse(River.Replace("\r\n", "\n"));

        Assert.Equal("rook", map.BreathRider);
        Assert.Contains("breath: rook\n", MapFormat.Write(map, MapFixture.Content));
    }

    [Fact]
    public void ABreathRiderMustBePlacedByName()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(River.Replace("breath: rook", "breath: teodor")));

        Assert.Contains("no 'P recruit:teodor' line", error.Message);
    }

    [Fact]
    public void ABreathHeaderNamesOneRider()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(River.Replace("breath: rook", "breath: rook wren")));

        Assert.Contains("breath needs", error.Message);
    }

    [Theory]
    [InlineData("nowhere", "names no terrain 'nowhere'")]
    [InlineData("plain", "cannot name the terrain itself")]
    public void AThawsToThatNamesNoOtherTerrainIsRefusedOnLoad(string thawsTo, string problem)
    {
        var path = Path.Combine(Fixture.RealContentDirectory(), "terrain.json");
        var json = File.ReadAllText(path).Replace("\"id\": \"plain\",", $"\"id\": \"plain\", \"thawsTo\": \"{thawsTo}\",");

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(terrain: json)));

        Assert.Equal("terrain.json", e.File);
        Assert.Equal("plain", e.Entry);
        Assert.Equal("thawsTo", e.Field);
        Assert.Contains(problem, e.Problem);
    }

    [Fact]
    public void TheShippedSampleLoadsWithTheBreath()
    {
        var path = Path.Combine(Fixture.RealContentDirectory(), "..", "docs", "samples", "kestrow_water_rime.map");
        var map = MapFormat.Parse(path, File.ReadAllText(path), ContentLoader.Load(Fixture.RealContentDirectory()));

        Assert.Equal("rook", map.BreathRider);
    }
}
