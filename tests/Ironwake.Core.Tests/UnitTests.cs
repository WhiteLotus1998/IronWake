using Ironwake.Core;

namespace Ironwake.Core.Tests;

public class UnitTests
{
    private static readonly Stats Base = new(19, 6, 0, 4, 5, 2, 3, 1, 2);
    private static readonly Stats Growths = new(45, 35, 5, 30, 30, 15, 30, 15, 10);

    private static Unit Soldier(int level = 1, int exp = 0) =>
        new("soldier", "Soldier", "pikeman", level, exp, Base, Growths, Inventory.Empty, ValueList<string>.Empty);

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void LevelOutsideOneToThirtyIsRejectedOnConstruction(int level)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Soldier(level));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void ExpOutsideZeroToNinetyNineIsRejectedOnConstruction(int exp)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Soldier(exp: exp));
    }

    [Fact]
    public void LevelGuardAlsoFiresOnWith()
    {
        var unit = Soldier();

        Assert.Throws<ArgumentOutOfRangeException>(() => unit with { Level = 31 });
        Assert.Throws<ArgumentOutOfRangeException>(() => unit with { Exp = 100 });
    }

    [Fact]
    public void BoundaryLevelsAndExpAreAccepted()
    {
        Assert.Equal(1, Soldier(1).Level);
        Assert.Equal(30, Soldier(30).Level);
        Assert.Equal(99, Soldier(exp: 99).Exp);
    }

    [Fact]
    public void AtLevelAddsFlooredGrowthPerLevelGainedWithoutRng()
    {
        var scaled = Soldier().AtLevel(11);

        Assert.Equal(11, scaled.Level);
        Assert.Equal(new Stats(19 + 4, 6 + 3, 0 + 0, 4 + 3, 5 + 3, 2 + 1, 3 + 3, 1 + 1, 2 + 1), scaled.Stats);
        Assert.Equal(Growths, scaled.Growths);
    }

    [Fact]
    public void AtLevelOnTheSameLevelIsAnIdentity()
    {
        var unit = Soldier(3);

        Assert.Equal(unit, unit.AtLevel(3));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(31)]
    public void AtLevelRejectsLevelsBelowCurrentOrAboveCap(int target)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Soldier(3).AtLevel(target));
    }

    [Fact]
    public void EffectiveStatsAddTheClassModifiers()
    {
        var pikeman = new UnitClass(
            "pikeman", "Pikeman", MovementType.Infantry, 4,
            new Stats(1, 1, 0, 0, 0, 0, 1, 0, 0),
            ValueList<WeaponType>.Of(WeaponType.Lance),
            new Stats(5, 0, 0, 0, 0, 0, 5, 0, 0));

        Assert.Equal(new Stats(20, 7, 0, 4, 5, 2, 4, 1, 2), Soldier().EffectiveStats(pikeman));
        Assert.Equal(new Stats(50, 35, 5, 30, 30, 15, 35, 15, 10), Soldier().EffectiveGrowths(pikeman));
    }
}
