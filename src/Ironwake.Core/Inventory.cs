namespace Ironwake.Core;

/// <summary>An item a unit carries and how many uses it has left.</summary>
public readonly record struct ItemStack(string ItemId, int Uses);

/// <summary>
/// A five-slot inventory. The equipped weapon is the first slot holding a weapon the
/// unit's class can use; issue 9 adds explicit equipping and consumables.
/// </summary>
public sealed record Inventory
{
    public const int Capacity = 5;

    public Inventory(ValueList<ItemStack> items)
    {
        if (items.Count > Capacity)
        {
            throw new ArgumentException($"inventory holds at most {Capacity} items, got {items.Count}", nameof(items));
        }

        Items = items;
    }

    public static Inventory Empty { get; } = new(ValueList<ItemStack>.Empty);

    public ValueList<ItemStack> Items { get; init; }

    public int Count => Items.Count;

    public bool IsFull => Items.Count >= Capacity;

    public Inventory Add(ItemStack item) => new(Items.Add(item));

    public Inventory RemoveAt(int index) => new(Items.RemoveAt(index));
}
