using Ironwake.Core;

namespace Ironwake.Core.Tests;

public class InventoryTests
{
    private static ItemStack Item(int n) => new("item" + n, 10);

    [Fact]
    public void FiveItemsFit()
    {
        var inventory = new Inventory(ValueList<ItemStack>.From(Enumerable.Range(1, 5).Select(Item)));

        Assert.Equal(5, inventory.Count);
        Assert.True(inventory.IsFull);
    }

    [Fact]
    public void SixItemsAreRejected()
    {
        var six = ValueList<ItemStack>.From(Enumerable.Range(1, 6).Select(Item));

        Assert.Throws<ArgumentException>(() => new Inventory(six));
    }

    [Fact]
    public void AddingToAFullInventoryIsRejected()
    {
        var full = new Inventory(ValueList<ItemStack>.From(Enumerable.Range(1, 5).Select(Item)));

        Assert.Throws<ArgumentException>(() => full.Add(Item(6)));
    }

    [Fact]
    public void AddAndRemoveReturnNewInventories()
    {
        var one = Inventory.Empty.Add(Item(1));
        var two = one.Add(Item(2));
        var back = two.RemoveAt(1);

        Assert.Equal(0, Inventory.Empty.Count);
        Assert.Equal(1, one.Count);
        Assert.Equal(2, two.Count);
        Assert.Equal(one, back);
    }
}
