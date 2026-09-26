namespace Ironwake.Core;

/// <summary>
/// A weapon a fallen player unit left on its tile (DESIGN.md 13.8, Carry the fallen; experiment).
/// <see cref="Item"/> carries the fallen's id in <see cref="ItemStack.Keepsake"/>, so the weapon
/// keeps their name in whoever's hands it ends up.
/// </summary>
public sealed record Keepsake(Coord At, string FallenId, ItemStack Item)
{
    /// <summary>
    /// What <paramref name="fallen"/> leaves on a <c>keepsakes: on</c> map: the weapon in its
    /// equipped slot, else the first slot holding any weapon, with the uses it had; null when it
    /// carried no weapon. A stack already named for someone else keeps that name.
    /// </summary>
    public static Keepsake? Of(BattleUnit fallen, GameContent content)
    {
        var slot = fallen.EquippedSlot(content);
        if (slot < 0)
        {
            var items = fallen.Unit.Inventory.Items;
            for (var i = 0; i < items.Count && slot < 0; i++)
            {
                if (content.Weapons.ContainsKey(items[i].ItemId))
                {
                    slot = i;
                }
            }
        }

        if (slot < 0)
        {
            return null;
        }

        var stack = fallen.Unit.Inventory.Items[slot];
        return new Keepsake(fallen.At, fallen.Id, stack with { Keepsake = stack.Keepsake ?? fallen.Id });
    }

    /// <summary>
    /// What a stack's name gains from being a keepsake: <c> (Dunstan's)</c>, the fallen's cast
    /// name, or the id when the fallen is not in the cast; empty for an ordinary stack.
    /// </summary>
    public static string Suffix(ItemStack stack, GameContent content) =>
        stack.Keepsake is not { } id ? "" : Owner(id, content);

    /// <summary>
    /// The display form of a keepsake wherever the console names it: <c>Iron Sword (Wren's)</c>,
    /// the weapon's name and then the fallen's cast name, as <see cref="Suffix"/> reads in a roster.
    /// </summary>
    public static string Name(string itemId, string fallenId, GameContent content) =>
        content.ItemName(itemId) + Owner(fallenId, content);

    private static string Owner(string fallenId, GameContent content) =>
        $" ({content.Cast.FirstOrDefault(u => u.Id == fallenId)?.Name ?? fallenId}'s)";
}
