namespace Ironwake.Core;

/// <summary>
/// A chest on a map (issue 649), from the <c>chests:</c> block of a map file: a tile and what
/// lies in it, weapon or item ids from the content, at full uses when taken. A player unit on
/// the tile or orthogonally beside it opens it as its action (<see cref="Open"/>); the chest
/// and its contents are printed, the way to it is not.
/// </summary>
public sealed record Chest(Coord At, ValueList<string> Items)
{
    /// <summary>The most a chest holds: one opener's whole pack.</summary>
    public const int MaxItems = Inventory.Capacity;

    /// <summary>Whether a unit on <paramref name="from"/> can reach the chest's lid: the tile itself or one orthogonal step away.</summary>
    public bool OpensFrom(Coord from) => from.DistanceTo(At) <= 1;

    /// <summary>The stacks the chest hands over, in file order, each at the full uses its content entry gives.</summary>
    public IEnumerable<ItemStack> Stacks(GameContent content) =>
        Items.Select(id => new ItemStack(id, content.Weapons.TryGetValue(id, out var weapon) ? weapon.Durability : content.Item(id).Uses));
}
