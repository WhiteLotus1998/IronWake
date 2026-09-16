namespace Ironwake.Core.Tests.Pathfinding;

public class CoordTests
{
    [Fact]
    public void DistanceIsManhattan()
    {
        Assert.Equal(5, new Coord(1, 1).DistanceTo(new Coord(4, 3)));
        Assert.Equal(0, new Coord(2, 2).DistanceTo(new Coord(2, 2)));
    }

    [Fact]
    public void OrderIsRowMajor()
    {
        var shuffled = new[] { new Coord(2, 1), new Coord(0, 1), new Coord(5, 0), new Coord(0, 0) };

        Assert.Equal(
            new[] { new Coord(0, 0), new Coord(5, 0), new Coord(0, 1), new Coord(2, 1) },
            shuffled.OrderBy(c => c));
    }

    [Fact]
    public void NeighborsComeNorthWestEastSouth()
    {
        Assert.Equal(
            new[] { new Coord(3, 2), new Coord(2, 3), new Coord(4, 3), new Coord(3, 4) },
            new Coord(3, 3).Neighbors());
    }

    [Fact]
    public void WritesAsXCommaY()
    {
        Assert.Equal("7,2", new Coord(7, 2).ToString());
    }
}
