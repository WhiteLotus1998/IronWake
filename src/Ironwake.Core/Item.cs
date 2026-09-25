namespace Ironwake.Core;

/// <summary>
/// A consumable from <c>items.json</c> (DESIGN.md section 5, issue 9): using it heals
/// its user by <see cref="Heals"/> and spends one of <see cref="Uses"/>; at zero the
/// stack leaves the inventory. Weapons and spells are <see cref="Weapon"/>s, not items.
/// <see cref="Price"/> is what the between-map shop charges for a full stack (issue 74); null when it is never sold.
/// </summary>
public sealed record Item(string Id, string Name, int Heals, int Uses, int? Price = null);
