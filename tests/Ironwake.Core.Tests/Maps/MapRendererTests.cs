namespace Ironwake.Core.Tests.Maps;

public class MapRendererTests
{
    private static readonly MapDefinition Map = MapFixture.Parse(MapFixture.OldMillRoad);

    private static string View => MapRenderer.Render(Map, MapFixture.Content);

    [Fact]
    public void PlayersAreUppercaseEnemiesLowercaseAndTheBossIsABang()
    {
        Assert.Equal(new[] { 'A', 'B', 'a', 'b', 'c', '!' }, MapRenderer.Letters(Map));
    }

    [Fact]
    public void UnitsAreDrawnOverTheTerrain()
    {
        var lines = View.Split('\n');

        Assert.Equal("   012345678901", lines[1]);
        Assert.Equal(" 1 ..^^....na!.", lines[3]);
        Assert.Equal(" 2 ..^^..F.nnb.", lines[4]);
        Assert.Equal(" 5 ..~~..c..#..", lines[7]);
        Assert.Equal(" 8 .AB.........", lines[10]);
    }

    [Fact]
    public void TheLegendNamesEachUnitItsTileAndTheTerrainUnderIt()
    {
        var view = View;

        Assert.Contains("A  captain", view);
        Assert.Contains("1,8    Plain", view);
        Assert.Contains("B  recruit wren", view);
        Assert.Contains("a  Soldier L1", view);
        Assert.Contains("group mill, guard", view);
        Assert.Contains("c  Brigand L1", view);
        Assert.Contains("group road, aggressive", view);
        Assert.Contains("!  Bandit Leader L3", view);
        Assert.Contains("group mill, boss", view);
    }

    [Fact]
    public void TheLegendListsOnlyTerrainOnTheMap()
    {
        var terrainLine = View.Split('\n').Last(l => l.StartsWith("terrain:", StringComparison.Ordinal));

        Assert.Contains("F Fort", terrainLine);
        Assert.Contains("# Wall", terrainLine);
        Assert.DoesNotContain("Mountain", terrainLine);
        Assert.DoesNotContain("Gate", terrainLine);
    }

    [Fact]
    public void TheHeaderLineCarriesTheRules()
    {
        Assert.StartsWith("Old Mill Road  12x10  rout  turn limit 20  recall 3  enemy level 1\n", View);
    }

    [Fact]
    public void EnemyLevelShownIsTheScaledLevelAndABossKeepsItsOwn()
    {
        var raised = Map with { EnemyLevel = 2 };
        var view = MapRenderer.Render(raised, MapFixture.Content);

        Assert.Contains("Soldier L2", view);
        Assert.Contains("Bandit Leader L3", view);
    }

    [Fact]
    public void AnyRecruitSlotsAreLabelled()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad.Replace("P recruit:wren 2,8", "P recruit 2,8"));

        Assert.Contains("B  recruit (any)", MapRenderer.Render(map, MapFixture.Content));
    }

    [Fact]
    public void AReachOverlayMarksDestinationsUnderTheUnitsAndSaysWhoseItIs()
    {
        var reach = Movement.Reach(Map, MapFixture.Content, new Coord(1, 8), MovementType.Infantry, 4, at => Map.OccupantAt(at, Side.Player));

        var view = MapRenderer.Render(Map, MapFixture.Content, reach);
        var lines = view.Split('\n');

        Assert.Equal(" 8 *AB***......", lines[10]);
        Assert.Equal(" 6 ****.....#..", lines[8]);
        Assert.Contains("*  reach from 1,8, infantry mov 4: 21 tiles\n", view);
        Assert.DoesNotContain("*", View);
    }

    [Fact]
    public void OutputIsPlainAscii()
    {
        Assert.All(View, c => Assert.True(c < 128 && (c >= 32 || c == '\n'), "non-ASCII or control character in the view"));
    }
}
