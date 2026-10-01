using Ironwake.Client;
using Xunit;

namespace Ironwake.Core.Tests.Client;

/// <summary>Issue 625: the game speed is buttons, 1x, 2x and 5x, drawn as one, two and three triangles, and instant (issue 677), three against a bar; S cycles them.</summary>
public class ClientGameSpeedTests
{
    [Fact]
    public void TheGameSpeedsAreOneTwoAndFiveTimesWithOneTwoAndThreeTrianglesThenInstantWithABar()
    {
        Assert.Equal(new[] { ("1x", 1, false), ("2x", 2, false), ("5x", 3, false), ("instant", 3, true) }, GameSpeed.Speeds.Select(s => (s.Name, s.Triangles, s.Bar)));
        Assert.Equal("Game speed", GameSpeed.Label);
    }

    [Theory]
    [InlineData(0, 1f)]
    [InlineData(1, 0.5f)]
    [InlineData(2, 0.2f)]
    [InlineData(3, 0.001f)]
    public void EachSpeedDividesABeatsLengthByItsMultiple(int index, float factor)
    {
        Assert.Equal(factor, GameSpeed.Factor(index), 5);
    }

    [Fact]
    public void ASessionOpensAtOneTimes()
    {
        Assert.Equal("1x", GameSpeed.Speeds[GameSpeed.Default].Name);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 0)]
    public void SStepsTheFourAndWrapsToOneTimes(int from, int to)
    {
        Assert.Equal(to, GameSpeed.Next(from));
    }
}
