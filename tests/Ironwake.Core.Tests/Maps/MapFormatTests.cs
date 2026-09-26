using Ironwake.Content;

namespace Ironwake.Core.Tests.Maps;

public class MapFormatTests
{
    [Fact]
    public void TheDesignDocExampleParsesToItsHeader()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);

        Assert.Equal("Old Mill Road", map.Name);
        Assert.Equal(12, map.Width);
        Assert.Equal(10, map.Height);
        Assert.Equal(WinCondition.Rout, map.Win);
        Assert.Equal(20, map.TurnLimit);
        Assert.Equal(3, map.RecallCharges);
        Assert.Equal(1, map.EnemyLevel);
        Assert.False(map.CheapShotsAllowed);
        Assert.Equal(120, map.TerrainIds.Count);
    }

    [Fact]
    public void TheGridIsRowMajorFromTheTopLeft()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);

        Assert.Equal("plain", map.TerrainIdAt(new Coord(0, 0)));
        Assert.Equal("forest", map.TerrainIdAt(new Coord(2, 1)));
        Assert.Equal("fort", map.TerrainIdAt(new Coord(6, 2)));
        Assert.Equal("road", map.TerrainIdAt(new Coord(11, 3)));
        Assert.Equal("water", map.TerrainIdAt(new Coord(3, 4)));
        Assert.Equal("wall", map.TerrainIdAt(new Coord(9, 6)));
        Assert.Equal("forest", map.TerrainIdAt(new Coord(11, 7)));
        Assert.Equal("Fort", map.TerrainAt(new Coord(6, 2), MapFixture.Content).Name);
        Assert.Throws<ArgumentOutOfRangeException>(() => map.TerrainIdAt(new Coord(12, 0)));
    }

    [Fact]
    public void UnitLinesBecomePlacementsInFileOrder()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);

        Assert.Equal(6, map.Placements.Count);
        Assert.Equal(new PlayerPlacement(new Coord(1, 8), PlayerSlot.Captain), map.Placements[0]);
        Assert.Equal(new PlayerPlacement(new Coord(2, 8), PlayerSlot.NamedRecruit, "wren"), map.Placements[1]);
        Assert.Equal(new EnemyPlacement(new Coord(9, 1), "soldier", "mill", Behavior.Guard, IsBoss: false), map.Placements[2]);
        Assert.Equal(new EnemyPlacement(new Coord(10, 2), "archer", "mill", Behavior.Guard, IsBoss: false), map.Placements[3]);
        Assert.Equal(new EnemyPlacement(new Coord(6, 5), "brigand", "road", Behavior.Aggressive, IsBoss: false), map.Placements[4]);
        Assert.Equal(new EnemyPlacement(new Coord(10, 1), "bandit_leader", "mill", Behavior.Boss, IsBoss: true), map.Placements[5]);
        Assert.Equal(Side.Player, map.Placements[0].Side);
        Assert.Equal(Side.Enemy, map.Placements[5].Side);
        Assert.Same(map.Placements[4], map.PlacementAt(new Coord(6, 5)));
        Assert.Null(map.PlacementAt(new Coord(0, 0)));
    }

    [Fact]
    public void ParseWriteParseRoundTrips()
    {
        var once = MapFixture.Parse(MapFixture.OldMillRoad);
        var text = MapFormat.Write(once, MapFixture.Content);
        var twice = MapFixture.Parse(text);

        Assert.Equal(once, twice);
        Assert.Equal(once.GetHashCode(), twice.GetHashCode());
    }

    [Fact]
    public void WriteIsCanonicalAndTheExampleIsAlreadyCanonical()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);
        var written = MapFormat.Write(map, MapFixture.Content);

        Assert.Equal(MapFixture.OldMillRoad, written);
        Assert.Equal(written, MapFormat.Write(MapFixture.Parse(written), MapFixture.Content));
    }

    [Fact]
    public void OptionalHeadersDefaultAndAreWrittenBack()
    {
        var text = MapFixture.OldMillRoad.Replace("recall: 3\n", "").Replace("enemy_level: 1\n", "");
        var map = MapFixture.Parse(text);

        Assert.Equal(MapDefinition.DefaultRecallCharges, map.RecallCharges);
        Assert.Equal(MapDefinition.DefaultEnemyLevel, map.EnemyLevel);
        Assert.Equal(MapFixture.OldMillRoad, MapFormat.Write(map, MapFixture.Content));
    }

    [Fact]
    public void CheapShotsAllowedRoundTrips()
    {
        var text = MapFixture.OldMillRoad.Replace("enemy_level: 1\n", "enemy_level: 1\ncheap_shots: allowed\n");
        var map = MapFixture.Parse(text);

        Assert.True(map.CheapShotsAllowed);
        Assert.Equal(text, MapFormat.Write(map, MapFixture.Content));
    }

    [Fact]
    public void AnyRecruitSlotsAndEveryBehaviorRoundTrip()
    {
        var text = MapFixture.OldMillRoad
            .Replace("P recruit:wren 2,8", "P recruit 2,8")
            .Replace("E archer 10,2 group:mill behavior:guard", "E archer 10,2 group:mill behavior:hold");
        var map = MapFixture.Parse(text);

        Assert.Equal(new PlayerPlacement(new Coord(2, 8), PlayerSlot.AnyRecruit), map.Placements[1]);
        Assert.Equal(Behavior.Hold, ((EnemyPlacement)map.Placements[3]).Behavior);
        Assert.Equal(text, MapFormat.Write(map, MapFixture.Content));
    }

    [Fact]
    public void WindowsLineEndingsAndAMissingBlankLineBeforeUnitsAreAccepted()
    {
        var crlf = MapFixture.OldMillRoad.Replace("\n", "\r\n");
        var tight = MapFixture.OldMillRoad.Replace("............\n\nunits:", "............\nunits:");

        Assert.Equal(MapFixture.Parse(MapFixture.OldMillRoad), MapFixture.Parse(crlf));
        Assert.Equal(MapFixture.Parse(MapFixture.OldMillRoad), MapFixture.Parse(tight));
    }

    [Fact]
    public void ABossLineMayOmitItsBehavior()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad.Replace(" group:mill behavior:boss", " group:mill"));

        var boss = Assert.IsType<EnemyPlacement>(map.Placements[5]);
        Assert.True(boss.IsBoss);
        Assert.Equal(Behavior.Boss, boss.Behavior);
    }

    [Fact]
    public void ABossLineMayBeAGuardAndRoundTrips()
    {
        var text = MapFixture.OldMillRoad.Replace(" group:mill behavior:boss", " group:mill behavior:guard");
        var map = MapFixture.Parse(text);

        var boss = Assert.IsType<EnemyPlacement>(map.Placements[5]);
        Assert.True(boss.IsBoss);
        Assert.Equal(Behavior.Guard, boss.Behavior);
        Assert.Equal(text, MapFormat.Write(map, MapFixture.Content));
    }

    [Fact]
    public void TilesOfFindsEveryTileOfATerrain()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);

        Assert.Equal(new[] { new Coord(6, 2) }, map.TilesOf("fort"));
        Assert.Equal(12, map.TilesOf("road").Count());
        Assert.Empty(map.TilesOf(MapDefinition.ThroneTerrainId));
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(1, 8, 9, 1, 15)]
    [InlineData(3, 3, 3, 7, 4)]
    public void CoordDistanceIsManhattan(int x1, int y1, int x2, int y2, int expected)
    {
        Assert.Equal(expected, new Coord(x1, y1).DistanceTo(new Coord(x2, y2)));
        Assert.Equal(expected, new Coord(x2, y2).DistanceTo(new Coord(x1, y1)));
        Assert.Equal(x1 + "," + y1, new Coord(x1, y1).ToString());
    }
}
