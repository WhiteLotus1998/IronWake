using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Pathfinding;

/// <summary>CLI tests redirect the process-wide console, so they share one collection and never run in parallel with each other.</summary>
[Collection("console")]
public class CliReachTests
{
    private static string OldMillRoad => Path.Combine(Fixture.RealContentDirectory(), "maps", "old_mill_road.map");

    private static string SaltmarshFord => Path.Combine(Fixture.RealContentDirectory(), "maps", "saltmarsh_ford.map");

    [Fact]
    public void ReachMarksAPlayerSlotsTilesAsInfantryMovFour()
    {
        var output = Run(out var exit, "reach", OldMillRoad, "1,8", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.Contains(" 8 *AB***......", output);
        Assert.Contains(" 4 .*~~........", output);
        Assert.EndsWith("*  reach from 1,8, infantry mov 4: 21 tiles\n", output);
        Assert.All(output, c => Assert.True(c < 128, "non-ASCII character in CLI output"));
    }

    [Fact]
    public void ReachUsesAnEnemysClassForItsMovement()
    {
        var output = Run(out var exit, "reach", SaltmarshFord, "12,2", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.Contains("flying mov 6", output);
        Assert.Contains(" 3 ~~~=~~~*******", output);
    }

    [Fact]
    public void ReachProbesAnEmptyTileWhenGivenAMovementAndMov()
    {
        var output = Run(out var exit, "reach", OldMillRoad, "5,0", "cavalry:2", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.Contains(" 0 ...*****....", output);
        Assert.Contains("cavalry mov 2: 8 tiles", output);
    }

    [Fact]
    public void ReachRefusesAnEmptyTileWithoutAMovement()
    {
        var output = Run(out var exit, "reach", OldMillRoad, "5,0", Fixture.RealContentDirectory());

        Assert.Equal(1, exit);
        Assert.StartsWith("ERROR: no unit at 5,0", output);
    }

    [Fact]
    public void ReachRefusesATileOutsideTheMap()
    {
        var output = Run(out var exit, "reach", OldMillRoad, "12,0", Fixture.RealContentDirectory());

        Assert.Equal(1, exit);
        Assert.StartsWith("ERROR: 12,0 is outside the 12x10 map", output);
    }

    [Theory]
    [InlineData("9,5", "infantry:4", "ERROR: infantry cannot stand on Wall at 9,5")]
    [InlineData("2,4", "armored:4", "ERROR: armored cannot stand on Water at 2,4")]
    public void ReachRefusesToProbeFromATileTheMovementTypeCannotStandOn(string at, string movement, string expected)
    {
        var output = Run(out var exit, "reach", OldMillRoad, at, movement, Fixture.RealContentDirectory());

        Assert.Equal(1, exit);
        Assert.StartsWith(expected, output);
        Assert.DoesNotContain("*", output);
    }

    [Fact]
    public void ReachProbesAFlyerFromWaterBecauseTheOriginCheckIsPerMovementType()
    {
        var output = Run(out var exit, "reach", OldMillRoad, "2,4", "flying:1", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.Contains(" 4 .***........", output);
        Assert.Contains(" 5 ..*~..c..#..", output);
        Assert.Contains("flying mov 1: 4 tiles", output);
    }

    [Fact]
    public void ReachWithoutACoordinatePrintsUsage()
    {
        var output = Run(out var exit, "reach", OldMillRoad);
        Run(out var garbled, "reach", OldMillRoad, "one,two");

        Assert.Equal(2, exit);
        Assert.Equal(2, garbled);
        Assert.StartsWith("usage: ironwake reach", output);
    }

    private static string Run(out int exit, params string[] args)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            exit = Ironwake.Cli.Program.Main(args);
        }
        finally
        {
            Console.SetOut(original);
        }

        return writer.ToString();
    }
}
