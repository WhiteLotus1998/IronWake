namespace Ironwake.Core;

/// <summary>
/// A consumable from <c>items.json</c> (DESIGN.md section 5, issue 9): using it heals
/// its user by <see cref="Heals"/> and spends one of <see cref="Uses"/>; at zero the
/// stack leaves the inventory. Weapons and spells are <see cref="Weapon"/>s, not items.
/// <see cref="Price"/> is what the between-map shop charges for a full stack (issue 74); null when it is never sold.
/// <see cref="Description"/> is the one line the item card prints (issue 650), required by the content validator.
/// </summary>
public sealed record Item(string Id, string Name, int Heals, int Uses, int? Price = null)
{
    /// <summary>The one line the item card prints for this item (issue 650); empty only in an item built outside the content files.</summary>
    public string Description { get; init; } = "";
}
