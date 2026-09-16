using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Pathfinding;

public class ReachTests
{
    /// <summary>A map from rows of glyphs and unit lines, with the header filled in.</summary>
    private static MapDefinition Grid(string rows, string units = "P captain 0,0")
    {
        var lines = rows.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var text = $"name: Test\nsize: {lines[0].Length}x{lines.Length}\nwin: rout\nturn_limit: 10\n\n{string.Join('\n', lines)}\n\nunits:\n{units}\n";
        return MapFixture.Parse(text);
    }

    private static Reach ReachFrom(MapDefinition map, Coord from, MovementType movement, int mov, Side side = Side.Player) =>
        Movement.Reach(map, MapFixture.Content, from, movement, mov, at => map.OccupantAt(at, side));

    // Every cell of the terrain table in DESIGN.md section 4: the cost to step from a
    // plain tile onto the terrain, or null where the table says impassable.
    [Theory]
    [InlineData('.', MovementType.Infantry, 1)]
    [InlineData('.', MovementType.Cavalry, 1)]
    [InlineData('.', MovementType.Flying, 1)]
    [InlineData('.', MovementType.Armored, 1)]
    [InlineData('=', MovementType.Infantry, 1)]
    [InlineData('=', MovementType.Cavalry, 1)]
    [InlineData('=', MovementType.Flying, 1)]
    [InlineData('=', MovementType.Armored, 1)]
    [InlineData('^', MovementType.Infantry, 2)]
    [InlineData('^', MovementType.Cavalry, 3)]
    [InlineData('^', MovementType.Flying, 1)]
    [InlineData('^', MovementType.Armored, 2)]
    [InlineData('n', MovementType.Infantry, 2)]
    [InlineData('n', MovementType.Cavalry, 3)]
    [InlineData('n', MovementType.Flying, 1)]
    [InlineData('n', MovementType.Armored, 3)]
    [InlineData('M', MovementType.Infantry, 3)]
    [InlineData('M', MovementType.Cavalry, null)]
    [InlineData('M', MovementType.Flying, 1)]
    [InlineData('M', MovementType.Armored, null)]
    [InlineData('~', MovementType.Infantry, null)]
    [InlineData('~', MovementType.Cavalry, null)]
    [InlineData('~', MovementType.Flying, 1)]
    [InlineData('~', MovementType.Armored, null)]
    [InlineData('F', MovementType.Infantry, 1)]
    [InlineData('F', MovementType.Cavalry, 1)]
    [InlineData('F', MovementType.Flying, 1)]
    [InlineData('F', MovementType.Armored, 1)]
    [InlineData('#', MovementType.Infantry, null)]
    [InlineData('#', MovementType.Cavalry, null)]
    [InlineData('#', MovementType.Flying, null)]
    [InlineData('#', MovementType.Armored, null)]
    [InlineData('T', MovementType.Infantry, 1)]
    [InlineData('T', MovementType.Cavalry, 1)]
    [InlineData('T', MovementType.Flying, 1)]
    [InlineData('T', MovementType.Armored, 1)]
    public void EnteringEachTerrainCostsWhatSectionFourSays(char glyph, MovementType movement, int? expected)
    {
        var map = Grid("." + glyph + ".");

        var reach = ReachFrom(map, new Coord(0, 0), movement, 3);

        Assert.Equal(expected, reach.CostTo(new Coord(1, 0)));
        Assert.Equal(expected is not null, reach.CanEnd(new Coord(1, 0)));
    }

    [Fact]
    public void TheOriginIsADestinationAtCostZeroWithAnEmptyPath()
    {
        var reach = ReachFrom(Grid("..."), new Coord(0, 0), MovementType.Infantry, 0);

        Assert.True(reach.CanEnd(new Coord(0, 0)));
        Assert.Equal(0, reach.CostTo(new Coord(0, 0)));
        Assert.Equal(ValueList<Coord>.Empty, reach.PathTo(new Coord(0, 0)));
        Assert.Equal(new[] { new Coord(0, 0) }, reach.Destinations);
    }

    [Fact]
    public void AnEnemyBlocksACorridor()
    {
        var map = Grid(".....", "P captain 0,0\nE soldier 2,0 group:g behavior:hold");

        var reach = ReachFrom(map, new Coord(0, 0), MovementType.Infantry, 4);

        Assert.Equal(new[] { new Coord(0, 0), new Coord(1, 0) }, reach.Destinations);
        Assert.False(reach.CanCross(new Coord(2, 0)));
        Assert.Null(reach.CostTo(new Coord(3, 0)));
    }

    [Fact]
    public void AnAllyInACorridorIsPassableButNotADestination()
    {
        var map = Grid(".....", "P captain 0,0\nP recruit 2,0");

        var reach = ReachFrom(map, new Coord(0, 0), MovementType.Infantry, 4);

        Assert.True(reach.CanCross(new Coord(2, 0)));
        Assert.False(reach.CanEnd(new Coord(2, 0)));
        Assert.Equal(new[] { new Coord(0, 0), new Coord(1, 0), new Coord(3, 0), new Coord(4, 0) }, reach.Destinations);
        Assert.Equal(ValueList<Coord>.Of(new Coord(1, 0), new Coord(2, 0), new Coord(3, 0)), reach.PathTo(new Coord(3, 0)));
    }

    [Fact]
    public void TheSameCorridorSeenFromTheEnemySideSwapsWhoBlocks()
    {
        var map = Grid(".....", "P captain 0,0\nE soldier 2,0 group:g behavior:hold\nE archer 4,0 group:g behavior:hold");

        var reach = ReachFrom(map, new Coord(4, 0), MovementType.Infantry, 4, Side.Enemy);

        Assert.True(reach.CanCross(new Coord(2, 0)));
        Assert.False(reach.CanEnd(new Coord(2, 0)));
        Assert.True(reach.CanEnd(new Coord(1, 0)));
        Assert.False(reach.CanCross(new Coord(0, 0)));
    }

    [Fact]
    public void AFlyerCrossesWaterAndIsStoppedByAWall()
    {
        var map = Grid(".~~.#.");

        var reach = ReachFrom(map, new Coord(0, 0), MovementType.Flying, 6);

        Assert.Equal(3, reach.CostTo(new Coord(3, 0)));
        Assert.True(reach.CanEnd(new Coord(1, 0)));
        Assert.False(reach.CanCross(new Coord(4, 0)));
        Assert.False(reach.CanCross(new Coord(5, 0)));
    }

    [Fact]
    public void InfantryCannotCrossWaterAtAll()
    {
        var reach = ReachFrom(Grid(".~~.#."), new Coord(0, 0), MovementType.Infantry, 6);

        Assert.Equal(new[] { new Coord(0, 0) }, reach.Destinations);
    }

    [Fact]
    public void TheBudgetIsStrictSoATileCostingMoreThanTheRemainderIsNotEntered()
    {
        var reach = ReachFrom(Grid(".n^"), new Coord(0, 0), MovementType.Armored, 4);

        Assert.Equal(3, reach.CostTo(new Coord(1, 0)));
        Assert.Null(reach.CostTo(new Coord(2, 0)));
    }

    [Fact]
    public void ThePathIsTheCheapestOneSoInfantryCutsThroughAForestRatherThanWalkingAround()
    {
        var map = Grid("...\n.^.\n...", "P captain 0,1");

        var reach = ReachFrom(map, new Coord(0, 1), MovementType.Infantry, 4);

        Assert.Equal(3, reach.CostTo(new Coord(2, 1)));
        Assert.Equal(ValueList<Coord>.Of(new Coord(1, 1), new Coord(2, 1)), reach.PathTo(new Coord(2, 1)));
    }

    [Fact]
    public void CavalryRidesAroundAMountainAndEqualCostRoutesBreakTiesToTheNorth()
    {
        // Both routes around the mountain cost 4. The tie-break in DESIGN.md section 4
        // settles tiles in (cost, row-major) order, so the northern route is found first
        // and the southern one never replaces it.
        var map = Grid("...\n.M.\n...", "P captain 0,1");

        var reach = ReachFrom(map, new Coord(0, 1), MovementType.Cavalry, 4);

        Assert.False(reach.CanCross(new Coord(1, 1)));
        Assert.Equal(4, reach.CostTo(new Coord(2, 1)));
        Assert.Equal(
            ValueList<Coord>.Of(new Coord(0, 0), new Coord(1, 0), new Coord(2, 0), new Coord(2, 1)),
            reach.PathTo(new Coord(2, 1)));
    }

    [Fact]
    public void EntriesAreInRowMajorOrderAndTheSameInputsGiveTheSameReach()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);

        var first = ReachFrom(map, new Coord(1, 8), MovementType.Infantry, 4);
        var second = ReachFrom(map, new Coord(1, 8), MovementType.Infantry, 4);

        Assert.Equal(first, second);
        Assert.Equal(first.Entries.Select(e => e.At).OrderBy(c => c), first.Entries.Select(e => e.At));
    }

    [Fact]
    public void OnTheExampleMapTheCaptainWalksPastTheRecruitAndNotIntoTheWater()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);

        var reach = ReachFrom(map, new Coord(1, 8), MovementType.Infantry, 4);

        Assert.False(reach.CanEnd(new Coord(2, 8)));
        Assert.Equal(ValueList<Coord>.Of(new Coord(2, 8), new Coord(3, 8)), reach.PathTo(new Coord(3, 8)));
        Assert.Equal(4, reach.CostTo(new Coord(1, 4)));
        Assert.Null(reach.CostTo(new Coord(2, 4)));
        Assert.Equal(22, reach.Destinations.Count());
    }

    [Fact]
    public void ANegativeMovIsRefused()
    {
        var map = Grid("...");

        Assert.Throws<ArgumentOutOfRangeException>(() => ReachFrom(map, new Coord(0, 0), MovementType.Infantry, -1));
    }

    [Fact]
    public void AnOriginOutsideTheMapIsRefused()
    {
        var map = Grid("...");

        Assert.Throws<ArgumentOutOfRangeException>(() => ReachFrom(map, new Coord(3, 0), MovementType.Infantry, 1));
    }

    [Fact]
    public void TheOccupancyFunctionIsNeverAskedAboutTheOrigin()
    {
        var map = Grid("...");

        var reach = Movement.Reach(map, MapFixture.Content, new Coord(0, 0), MovementType.Infantry, 2,
            at => at == new Coord(0, 0) ? throw new InvalidOperationException("asked about the origin") : Occupant.None);

        Assert.Equal(3, reach.Destinations.Count());
    }

    [Fact]
    public void OccupantAtReportsAllyAndEnemyRelativeToTheMoversSide()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);

        Assert.Equal(Occupant.None, map.OccupantAt(new Coord(0, 0), Side.Player));
        Assert.Equal(Occupant.Ally, map.OccupantAt(new Coord(2, 8), Side.Player));
        Assert.Equal(Occupant.Enemy, map.OccupantAt(new Coord(2, 8), Side.Enemy));
        Assert.Equal(Occupant.Enemy, map.OccupantAt(new Coord(10, 1), Side.Player));
        Assert.Equal(Occupant.Ally, map.OccupantAt(new Coord(10, 1), Side.Enemy));
    }
}
