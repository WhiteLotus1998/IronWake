using Ironwake.Client;
using Xunit;

namespace Ironwake.Core.Tests.Client;

/// <summary>Issue 625: the game speed is three buttons, 1x, 2x and 5x, drawn as one, two and three triangles; S cycles them.</summary>
public class ClientGameSpeedTests
{
    [Fact]
    public void TheGameSpeedsAreOneTwoAndFiveTimesWithOneTwoAndThreeTriangles()
    {
        Assert.Equal(new[] { ("1x", 1), ("2x", 2), ("5x", 3) }, GameSpeed.Speeds.Select(s => (s.Name, s.Triangles)));
        Assert.Equal("Game speed", GameSpeed.Label);
    }

    [Theory]
    [InlineData(0, 1f)]
    [InlineData(1, 0.5f)]
    [InlineData(2, 0.2f)]
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
    [InlineData(2, 0)]
    public void SStepsTheSameThreeAndWrapsToOneTimes(int from, int to)
    {
        Assert.Equal(to, GameSpeed.Next(from));
    }
}
