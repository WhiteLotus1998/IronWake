using Ironwake.Core;

namespace Ironwake.Core.Tests;

public class ValueListTests
{
    [Fact]
    public void TwoListsWithTheSameItemsAreEqual()
    {
        var a = ValueList<int>.Of(1, 2, 3);
        var b = ValueList<int>.From(new List<int> { 1, 2, 3 });

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ListsWithDifferentOrderOrLengthAreNotEqual()
    {
        Assert.NotEqual(ValueList<int>.Of(1, 2), ValueList<int>.Of(2, 1));
        Assert.NotEqual(ValueList<int>.Of(1, 2), ValueList<int>.Of(1, 2, 3));
    }

    [Fact]
    public void DefaultValueBehavesAsEmpty()
    {
        ValueList<string> list = default;

        Assert.Empty(list);
        Assert.Equal(ValueList<string>.Empty, list);
        Assert.Equal(list.GetHashCode(), ValueList<string>.Empty.GetHashCode());
    }

    [Fact]
    public void RecordsHoldingValueListsCompareStructurally()
    {
        var a = new UnitClass("c", "C", MovementType.Infantry, 4, Stats.Zero, ValueList<WeaponType>.Of(WeaponType.Sword), Stats.Zero);
        var b = new UnitClass("c", "C", MovementType.Infantry, 4, Stats.Zero, ValueList<WeaponType>.Of(WeaponType.Sword), Stats.Zero);

        Assert.Equal(a, b);
    }

    [Fact]
    public void MutatorsReturnNewListsAndLeaveTheOriginalAlone()
    {
        var original = ValueList<int>.Of(1, 2);
        var added = original.Add(3);
        var replaced = original.SetItem(0, 9);
        var removed = original.RemoveAt(1);

        Assert.Equal(ValueList<int>.Of(1, 2), original);
        Assert.Equal(ValueList<int>.Of(1, 2, 3), added);
        Assert.Equal(ValueList<int>.Of(9, 2), replaced);
        Assert.Equal(ValueList<int>.Of(1), removed);
    }
}
