namespace Ironwake.Core.Tests.Maps;

public class MapRendererTests
{
    private static readonly MapDefinition Map = MapFixture.Parse(MapFixture.OldMillRoad);

    private static string View => MapRenderer.Render(Map, MapFixture.Content);

    [Fact]
    public void PlayersAreUppercaseEnemiesLowercaseAndTheBossIsABang()
    {
        Assert.Equal(new[] { 'A', 'B', 'a', 'b', 'c', '!' }, MapRenderer.Letters(Map, MapFixture.Content));
    }

    [Fact]
    public void TheSixthPlayerIsNotDrawnAsAFort()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad.Replace(
            "P recruit:wren 2,8",
            "P recruit 2,8\nP recruit 3,8\nP recruit 4,8\nP recruit 5,8\nP recruit 6,8"));

        var lines = MapRenderer.Render(map, MapFixture.Content).Split('\n');

        Assert.Equal(" 8 .ABCDEG.....", lines[10]);
        Assert.Equal(" 2 ..^^..F.nnb.", lines[4]);
    }

    [Fact]
    public void LettersSkipEveryTerrainGlyphOnBothSides()
    {
        var map = MapFixture.Parse(WithUnits(26, 26));
        var letters = MapRenderer.Letters(map, MapFixture.Content);

        Assert.Equal("ABCDEGHIJKLNOPQRSUVWXYZABC", new string(letters, 0, 26));
        Assert.Equal("abcdefghijklmopqrstuvwxyza", new string(letters, 26, 26));
    }

    [Theory]
    [InlineData("old_mill_road.map")]
    [InlineData("saltmarsh_ford.map")]
    [InlineData("the_tollgate.map")]
    public void NoUnitGlyphIsATerrainGlyph(string file)
    {
        var map = MapFixture.Parse(File.ReadAllText(Path.Combine(MapFixture.MapsDirectory, file)), file);

        foreach (var letter in MapRenderer.Letters(map, MapFixture.Content).Append(MapRenderer.BossGlyph).Append(MapRenderer.ReachGlyph).Append(MapRenderer.ExitGlyph))
        {
            Assert.Null(MapFixture.Content.TerrainByGlyph(letter));
        }
    }

    [Fact]
    public void ContentWhoseGlyphsUseEveryLetterIsRefused()
    {
        var terrain = MapFixture.Content.Terrain;
        var fort = terrain["fort"];
        for (var offset = 0; offset < 26; offset++)
        {
            var glyph = (char)('A' + offset);
            terrain = terrain.SetItem("letter_" + glyph, fort with { Id = "letter_" + glyph, Glyph = glyph });
        }

        var content = MapFixture.Content with { Terrain = terrain };

        var error = Assert.Throws<InvalidOperationException>(() => MapRenderer.Letters(Map, content));
        Assert.Contains("every letter A..Z", error.Message);
    }

    /// <summary>The example map's grid with a full row of players along y=9 and y=8, and enemies along y=0 and y=1.</summary>
    private static string WithUnits(int players, int enemies)
    {
        var sb = new System.Text.StringBuilder(MapFixture.OldMillRoad[..MapFixture.OldMillRoad.IndexOf("units:", StringComparison.Ordinal)]);
        sb.Append("units:\nP captain 0,9\n");
        for (var i = 1; i < players; i++)
        {
            sb.Append("P recruit ").Append(i % 12).Append(',').Append(9 - i / 12).Append('\n');
        }

        for (var i = 0; i < enemies; i++)
        {
            sb.Append("E soldier ").Append(i % 12).Append(',').Append(i / 12).Append(" group:line behavior:hold\n");
        }

        return sb.ToString();
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
