namespace Ironwake.Core;

/// <summary>An item a unit carries and how many uses it has left.</summary>
public readonly record struct ItemStack(string ItemId, int Uses)
{
    /// <summary>
    /// The id of the fallen unit this weapon was recovered from (DESIGN.md 13.8, experiment), or
    /// null for an ordinary stack. The name stays with the stack for the rest of the campaign.
    /// </summary>
    public string? Keepsake { get; init; }

    /// <summary>
    /// The kills a hungering weapon has fed on (DESIGN.md 13.23, experiment; <see cref="Kinsbane"/>),
    /// 0 for any other stack. It stays with the stack, so a carried scythe keeps its growth.
    /// </summary>
    public int Fed { get; init; }

    /// <summary>Whether a hungering weapon is in its starved form (DESIGN.md 13.23): half Mt, uses held at 1.</summary>
    public bool Starved { get; init; }

    /// <summary>
    /// The combats an heirloom has been fought with (issue 646; <see cref="Heirloom"/>), 0 for any
    /// other stack. Never printed: only the stage it has reached is.
    /// </summary>
    public int Combats { get; init; }

    /// <summary>An heirloom's stage: 0 is its first, n is the ladder's nth turn. 0 for any other stack.</summary>
    public int Stage { get; init; }
}

/// <summary>
/// A five-slot inventory. The equipped weapon is the first slot holding a usable weapon
/// (<see cref="BattleUnit.EquippedSlot"/>); consumables are the entries of items.json.
/// The cap counts ordinary stacks only: an enemy that takes a stack of keepsakes carries
/// them past it (DESIGN.md 13.8, issue 295), so a fallen recruit's weapon is never dropped
/// for want of a slot.
/// </summary>
public sealed record Inventory
{
    public const int Capacity = 5;

    public Inventory(ValueList<ItemStack> items)
    {
        var ordinary = items.Count(stack => stack.Keepsake is null);
        if (ordinary > Capacity)
        {
            throw new ArgumentException($"inventory holds at most {Capacity} items, got {ordinary}", nameof(items));
        }

        Items = items;
    }

    public static Inventory Empty { get; } = new(ValueList<ItemStack>.Empty);

    public ValueList<ItemStack> Items { get; init; }

    public int Count => Items.Count;

    public bool IsFull => Items.Count >= Capacity;

    public Inventory Add(ItemStack item) => new(Items.Add(item));

    public Inventory RemoveAt(int index) => new(Items.RemoveAt(index));

    public Inventory Replace(int index, ItemStack item) => new(Items.SetItem(index, item));
}
