using Ironwake.Core;

namespace Ironwake.Core.Tests;

public class TerrainAndWeaponTests
{
    private static Terrain Forest => new(
        "forest", "Forest", '^', ValueList<int?>.Of(2, 3, 1, 2), 20, 1, 0, 0, AppliesToFlyers: false);

    private static Terrain Fort => new(
        "fort", "Fort", 'F', ValueList<int?>.Of(1, 1, 1, 1), 30, 2, 2, 20, AppliesToFlyers: true);

    private static Terrain Water => new(
        "water", "Water", '~', ValueList<int?>.Of(null, null, 1, null), 0, 0, 0, 0, AppliesToFlyers: false);

    [Theory]
    [InlineData(MovementType.Infantry, 2)]
    [InlineData(MovementType.Cavalry, 3)]
    [InlineData(MovementType.Flying, 1)]
    [InlineData(MovementType.Armored, 2)]
    public void MoveCostIsLookedUpByMovementType(MovementType movement, int expected)
    {
        Assert.Equal(expected, Forest.MoveCost(movement));
    }

    [Fact]
    public void NullCostMeansImpassable()
    {
        Assert.False(Water.IsPassable(MovementType.Infantry));
        Assert.Null(Water.MoveCost(MovementType.Cavalry));
        Assert.True(Water.IsPassable(MovementType.Flying));
    }

    [Fact]
    public void ForestBonusesDoNotApplyToFlyers()
    {
        Assert.Equal(20, Forest.AvoidFor(MovementType.Infantry));
        Assert.Equal(1, Forest.DefFor(MovementType.Armored));
        Assert.Equal(0, Forest.AvoidFor(MovementType.Flying));
        Assert.Equal(0, Forest.DefFor(MovementType.Flying));
    }

    [Fact]
    public void FortBonusesApplyToFlyers()
    {
        Assert.Equal(30, Fort.AvoidFor(MovementType.Flying));
        Assert.Equal(2, Fort.DefFor(MovementType.Flying));
        Assert.Equal(2, Fort.ResFor(MovementType.Flying));
    }

    private static Weapon IronBow => new(
        "iron_bow", "Iron Bow", WeaponType.Bow, 5, 85, 0, 5, 2, 2, 40, ValueList<MovementType>.Of(MovementType.Flying));

    private static Weapon Cinder => new(
        "cinder", "Cinder", WeaponType.Reason, 5, 90, 0, 3, 1, 2, 8, ValueList<MovementType>.Empty);

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void ABowOnlyReachesRangeTwo(int distance, bool expected)
    {
        Assert.Equal(expected, IronBow.InRange(distance));
    }

    [Fact]
    public void ReasonAndFaithAreMagicAndOthersAreNot()
    {
        Assert.True(Cinder.IsMagic);
        Assert.True(WeaponType.Faith.IsMagic());
        Assert.False(IronBow.IsMagic);
        Assert.False(WeaponType.Sword.IsMagic());
    }

    [Fact]
    public void EffectiveTagsMatchOnlyTheListedMovementTypes()
    {
        Assert.True(IronBow.IsEffectiveAgainst(MovementType.Flying));
        Assert.False(IronBow.IsEffectiveAgainst(MovementType.Cavalry));
        Assert.False(Cinder.IsEffectiveAgainst(MovementType.Flying));
    }

    [Fact]
    public void ClassCanUseOnlyItsListedWeaponTypes()
    {
        var bowman = new UnitClass("bowman", "Bowman", MovementType.Infantry, 4, Stats.Zero, ValueList<WeaponType>.Of(WeaponType.Bow), Stats.Zero);

        Assert.True(bowman.CanUse(WeaponType.Bow));
        Assert.False(bowman.CanUse(WeaponType.Sword));
    }
}
