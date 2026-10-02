using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Rotten planks (DESIGN.md 13.25, experiment): a tile whose terrain wears steps toward what
/// it wears to each time a unit walks off it, once on foot, twice under a horse or armour,
/// never under a flyer; the tile a walk ends on does not wear until it is left, and a tile
/// another unit stands on does not wear.
/// </summary>
public class PlanksTests
{
    private static readonly Unit Rider = Recruit("rider", "outrider", new Stats(20, 7, 0, 6, 8, 5, 4, 2, 3), "iron_lance");
    private static readonly Unit Flyer = Recruit("flyer", "skyrider", new Stats(20, 7, 0, 6, 8, 5, 4, 2, 3), "iron_lance");

    private const string Bridge = """
        name: Bridge
        size: 7x3
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        .......
        ~~+++~~
        .......

        units:
        {0}
        E brigand 6,2 group:far behavior:guard
        """;

    private static BattleState Start(ValueList<Unit> roster, string units) =>
        BattleFixture.Start(7, roster, Bridge.Replace("{0}", units));

    private static string At(BattleState state, int x, int y) => state.Map.TerrainIdAt(new Coord(x, y));

    [Fact]
    public void AUnitOnFootWearsEveryPlankTileItWalksOffOneStep()
    {
        var state = Start(ValueList<Unit>.Of(Hale), "P captain 2,1").Do(new Move("hale", new Coord(4, 1)));

        Assert.Equal("split_planks", At(state, 2, 1));
        Assert.Equal("split_planks", At(state, 3, 1));
        Assert.Equal("planks", At(state, 4, 1));
    }

    [Fact]
    public void TheTileAWalkEndsOnWearsOnlyWhenItIsLeft()
    {
        var state = Start(ValueList<Unit>.Of(Hale), "P captain 2,0").Do(new Move("hale", new Coord(2, 1)));

        Assert.Equal("planks", At(state, 2, 1));
    }

    [Fact]
    public void SplitPlanksWalkedOffBecomeWater()
    {
        var state = Start(ValueList<Unit>.Of(Hale), "P captain 2,1");
        state = state with { Map = state.Map.WithTerrain(new Coord(2, 1), "split_planks") };

        var result = state.Try(new Move("hale", new Coord(2, 2)));

        Assert.Equal("water", At(result.Next, 2, 1));
        Assert.Contains(new TerrainChanged(new Coord(2, 1), "water"), result.Events);
    }

    [Fact]
    public void AHorseWearsAPlankTileTwoStepsSoOneCrossingDropsIt()
    {
        var state = Start(ValueList<Unit>.Of(Hale, Rider), "P captain 0,0\nP recruit:rider 2,1").Do(new Move("rider", new Coord(4, 1)));

        Assert.Equal("water", At(state, 2, 1));
        Assert.Equal("water", At(state, 3, 1));
        Assert.Equal("planks", At(state, 4, 1));
    }

    [Fact]
    public void AFlyerNeverWearsPlanks()
    {
        var state = Start(ValueList<Unit>.Of(Hale, Flyer), "P captain 0,0\nP recruit:flyer 2,1").Do(new Move("flyer", new Coord(4, 1)));

        Assert.Equal("planks", At(state, 2, 1));
        Assert.Equal("planks", At(state, 3, 1));
    }

    [Fact]
    public void APlankTileAnAllyStandsOnIsCrossedNotWorn()
    {
        var state = Start(ValueList<Unit>.Of(Hale, Wren), "P captain 2,1\nP recruit:wren 3,1").Do(new Move("hale", new Coord(4, 1)));

        Assert.Equal("split_planks", At(state, 2, 1));
        Assert.Equal("planks", At(state, 3, 1));
    }

    [Fact]
    public void GroundThatDoesNotWearIsUnchangedByAWalk()
    {
        var result = Start(ValueList<Unit>.Of(Hale), "P captain 0,0").Try(new Move("hale", new Coord(3, 0)));

        Assert.DoesNotContain(result.Events, e => e is TerrainChanged);
    }

    [Fact]
    public void TheTerrainCardPrintsTheWearAndItsChain()
    {
        var state = Start(ValueList<Unit>.Of(Hale), "P captain 0,0");

        Assert.Contains("Wears: each unit that walks off it wears it one step, a horse or armour two, a flyer none (Planks, Split planks, Water).", TerrainCard.Text(state, Starter, "planks"));
        Assert.DoesNotContain("Wears", TerrainCard.Text(state, Starter, "road"));
    }

    [Fact]
    public void TheBoardNamesEveryWearingTileAndTheRule()
    {
        var state = Start(ValueList<Unit>.Of(Hale), "P captain 2,1").Do(new Move("hale", new Coord(2, 2)));

        Assert.Equal(
            "wears: Split planks 2,1; Planks 3,1 4,1 (a unit that walks off one wears it a step, a horse or armour two, a flyer none: Planks, Split planks, Water)",
            Planks.Line(state.Map, Starter));
        Assert.Contains(Planks.Line(state.Map, Starter)!, MapRenderer.Render(state, Starter));
    }

    [Fact]
    public void ABoardWithNoWearingGroundPrintsNoWearLine()
    {
        Assert.Null(Planks.Line(YardMap, Starter));
    }

    [Theory]
    [InlineData("nowhere", "names no terrain 'nowhere'")]
    [InlineData("plain", "cannot name the terrain itself")]
    public void AWearsToThatNamesNoOtherTerrainIsRefusedOnLoad(string wearsTo, string problem)
    {
        var path = Path.Combine(Fixture.RealContentDirectory(), "terrain.json");
        var json = File.ReadAllText(path).Replace("\"id\": \"plain\",", $"\"id\": \"plain\", \"wearsTo\": \"{wearsTo}\",");

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(terrain: json)));

        Assert.Equal("terrain.json", e.File);
        Assert.Equal("plain", e.Entry);
        Assert.Equal("wearsTo", e.Field);
        Assert.Contains(problem, e.Problem);
    }
}
