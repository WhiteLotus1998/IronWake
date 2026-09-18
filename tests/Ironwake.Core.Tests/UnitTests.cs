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

    private static readonly UnitClass Pikeman = new(
        "pikeman", "Pikeman", MovementType.Infantry, 4,
        new Stats(1, 1, 0, 0, 0, 0, 1, 0, 0),
        ValueList<WeaponType>.Of(WeaponType.Lance),
        new Stats(5, 0, 0, 0, 0, 0, 5, 0, 0));

    /// <summary>A pikeman whose growth modifiers push the soldier's growth outside 0..100 on two stats.</summary>
    private static readonly UnitClass Pikeman_OutsideTheRange = Pikeman with
    {
        GrowthModifiers = new Stats(60, 0, -10, 0, 0, 0, 0, 0, 0),
    };

    [Fact]
    public void AtLevelAddsFlooredEffectiveGrowthPerLevelGainedWithoutRng()
    {
        var scaled = Soldier().AtLevel(11, Pikeman);

        Assert.Equal(11, scaled.Level);
        Assert.Equal(new Stats(19 + 5, 6 + 3, 0 + 0, 4 + 3, 5 + 3, 2 + 1, 3 + 3, 1 + 1, 2 + 1), scaled.Stats);
        Assert.Equal(Growths, scaled.Growths);
    }

    [Fact]
    public void AtLevelClampsANegativeEffectiveGrowthToZeroRatherThanLosingTheStat()
    {
        var scaled = Soldier().AtLevel(30, Pikeman_OutsideTheRange);

        Assert.Equal(-5, Soldier().EffectiveGrowths(Pikeman_OutsideTheRange).Mag);
        Assert.Equal(0, scaled.Stats.Mag);
    }

    [Fact]
    public void AtLevelClampsAnEffectiveGrowthAboveOneHundredToOnePointPerLevel()
    {
        var scaled = Soldier().AtLevel(21, Pikeman_OutsideTheRange);

        Assert.Equal(105, Soldier().EffectiveGrowths(Pikeman_OutsideTheRange).Hp);
        Assert.Equal(19 + 20, scaled.Stats.Hp);
    }

    [Fact]
    public void AtLevelOnTheSameLevelIsAnIdentity()
    {
        var unit = Soldier(3);

        Assert.Equal(unit, unit.AtLevel(3, Pikeman));
        Assert.Equal(unit, unit.AtLevel(3, Pikeman_OutsideTheRange));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(31)]
    public void AtLevelRejectsLevelsBelowCurrentOrAboveCap(int target)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Soldier(3).AtLevel(target, Pikeman));
    }

    [Fact]
    public void AtLevelRejectsAClassThatIsNotTheUnitsOwn()
    {
        var reaver = Pikeman with { Id = "reaver", Name = "Reaver" };

        var error = Assert.Throws<ArgumentException>(() => Soldier().AtLevel(5, reaver));

        Assert.Contains("soldier is a pikeman, not a reaver", error.Message);
        Assert.Throws<ArgumentException>(() => Soldier().ScaledTo(5, reaver));
    }

    [Theory]
    [InlineData(1, 3, 3)]
    [InlineData(3, 3, 3)]
    [InlineData(5, 3, 5)]
    public void ScaledToRaisesATemplateBelowTheFloorAndLeavesOneAtOrAboveIt(int level, int floor, int expected)
    {
        var template = Soldier(level);

        var scaled = template.ScaledTo(floor, Pikeman);

        Assert.Equal(expected, scaled.Level);
        Assert.Equal(expected == level ? template : template.AtLevel(floor, Pikeman), scaled);
    }

    [Fact]
    public void ScaledToRaisesTheStatsAsWellAsTheLevel()
    {
        var scaled = Soldier().ScaledTo(11, Pikeman);

        Assert.Equal(Soldier().AtLevel(11, Pikeman).Stats, scaled.Stats);
        Assert.NotEqual(Base, scaled.Stats);
    }

    [Fact]
    public void ScaledToAboveTheCapIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Soldier().ScaledTo(31, Pikeman));
    }

    [Fact]
    public void EffectiveStatsAddTheClassModifiers()
    {
        Assert.Equal(new Stats(20, 7, 0, 4, 5, 2, 4, 1, 2), Soldier().EffectiveStats(Pikeman));
        Assert.Equal(new Stats(50, 35, 5, 30, 30, 15, 35, 15, 10), Soldier().EffectiveGrowths(Pikeman));
    }
}
