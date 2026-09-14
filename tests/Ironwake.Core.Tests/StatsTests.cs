using Ironwake.Core;

namespace Ironwake.Core.Tests;

public class StatsTests
{
    private static readonly Stats Sample = new(20, 7, 1, 5, 6, 3, 4, 2, 8);

    [Theory]
    [InlineData(Stat.Hp, 20)]
    [InlineData(Stat.Str, 7)]
    [InlineData(Stat.Mag, 1)]
    [InlineData(Stat.Dex, 5)]
    [InlineData(Stat.Spd, 6)]
    [InlineData(Stat.Lck, 3)]
    [InlineData(Stat.Def, 4)]
    [InlineData(Stat.Res, 2)]
    [InlineData(Stat.Cha, 8)]
    public void GetReadsEachStat(Stat stat, int expected)
    {
        Assert.Equal(expected, Sample.Get(stat));
    }

    [Theory]
    [InlineData(Stat.Hp)]
    [InlineData(Stat.Str)]
    [InlineData(Stat.Mag)]
    [InlineData(Stat.Dex)]
    [InlineData(Stat.Spd)]
    [InlineData(Stat.Lck)]
    [InlineData(Stat.Def)]
    [InlineData(Stat.Res)]
    [InlineData(Stat.Cha)]
    public void WithReplacesOnlyTheNamedStat(Stat stat)
    {
        var changed = Sample.With(stat, 99);

        foreach (var other in Stats.All)
        {
            Assert.Equal(other == stat ? 99 : Sample.Get(other), changed.Get(other));
        }
    }

    [Fact]
    public void AllListsTheNineStatsInGrowthDrawOrder()
    {
        Assert.Equal(
            new[] { Stat.Hp, Stat.Str, Stat.Mag, Stat.Dex, Stat.Spd, Stat.Lck, Stat.Def, Stat.Res, Stat.Cha },
            Stats.All);
    }

    [Fact]
    public void AdditionAndSubtractionAreComponentWise()
    {
        var modifiers = new Stats(1, 2, -1, 0, 0, 0, 3, 0, -2);

        Assert.Equal(new Stats(21, 9, 0, 5, 6, 3, 7, 2, 6), Sample + modifiers);
        Assert.Equal(Sample, (Sample + modifiers) - modifiers);
    }

    [Fact]
    public void MapAppliesTheFunctionToEveryStat()
    {
        var doubled = Sample.Map((_, value) => value * 2);

        Assert.Equal(new Stats(40, 14, 2, 10, 12, 6, 8, 4, 16), doubled);
    }
}
