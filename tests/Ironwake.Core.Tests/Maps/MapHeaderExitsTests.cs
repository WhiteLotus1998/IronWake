using Ironwake.Content;

namespace Ironwake.Core.Tests.Maps;

/// <summary>The <c>exit:</c> and <c>protect:</c> headers (issue 7, DESIGN.md section 10).</summary>
public class MapHeaderExitsTests
{
    private static string Escape(string header) =>
        MapFixture.OldMillRoad.Replace("win: rout", "win: escape").Replace("enemy_level: 1\n", "enemy_level: 1\n" + header + "\n");

    [Fact]
    public void ExitsAndProtectRoundTripInCanonicalOrder()
    {
        var text = Escape("exit: 0,9 1,9\nprotect: wren");

        var map = MapFixture.Parse(text);

        Assert.Equal(ValueList<Coord>.Of(new Coord(0, 9), new Coord(1, 9)), map.Exits);
        Assert.Equal("wren", map.ProtectId);
        Assert.True(map.IsExit(new Coord(1, 9)));
        Assert.False(map.IsExit(new Coord(2, 9)));
        Assert.Equal(text, MapFormat.Write(map, MapFixture.Content));
        Assert.Empty(MapFixture.Parse(MapFixture.OldMillRoad).Exits);
        Assert.Null(MapFixture.Parse(MapFixture.OldMillRoad).ProtectId);
    }

    [Theory]
    [InlineData("exit: 0,9", "1 exit tiles for 2 player slots")]
    [InlineData("exit: 0,9 12,9", "exit 12,9 is outside the 12x10 grid")]
    [InlineData("exit: 0,9 0,9", "exit 0,9 is listed twice")]
    [InlineData("exit: 0,9 corner", "got 'corner'")]
    [InlineData("exit: 0,9 1,9\nprotect: ivo", "protect names 'ivo' but no 'P recruit:ivo' line places them")]
    public void BadExitsAndProtectAreRefusedNamingTheProblem(string header, string message)
    {
        var ex = Assert.Throws<MapException>(() => MapFixture.Parse(Escape(header)));

        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public void EscapeNeedsExitsAndOtherWinsRefuseThem()
    {
        var noExits = Assert.Throws<MapException>(() => MapFixture.Parse(Escape("")));
        Assert.Contains("0 exit tiles for 2 player slots", noExits.Message);

        var rout = Assert.Throws<MapException>(() => MapFixture.Parse(MapFixture.OldMillRoad.Replace("enemy_level: 1\n", "enemy_level: 1\nexit: 0,9 1,9\n")));
        Assert.Contains("exit tiles are only for win: escape, and this map's win is rout", rout.Message);
    }
}
