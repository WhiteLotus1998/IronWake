namespace Ironwake.Core;

/// <summary>
/// What an item is, in one card (issue 650): one source for the console's <c>about</c> command and
/// the protocol's <c>about</c> query. The numbers are read from the <see cref="Weapon"/> or
/// <see cref="Item"/> record, never written by hand; the last sentence is the item's own
/// <c>description</c> from the content files.
/// </summary>
public static class ItemCard
{
    /// <summary>The weapon or item named by <paramref name="named"/>, its id or its name in any case; null when there is none.</summary>
    public static string? Find(GameContent content, string named)
    {
        var text = named.Trim();
        if (content.Weapons.ContainsKey(text) || content.Items.ContainsKey(text))
        {
            return text;
        }

        return content.Weapons.Values.FirstOrDefault(w => string.Equals(w.Name, text, StringComparison.OrdinalIgnoreCase))?.Id
            ?? content.Items.Values.FirstOrDefault(i => string.Equals(i.Name, text, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    /// <summary>
    /// The card for <paramref name="id"/>: the name; for a weapon its type and rank (and a tome's school, DECISIONS/0296), Acc, Power (or the
    /// heal for a healing spell), Crit, weight, range, uses and the units it is effective
    /// against; for a consumable its heal and uses, or the school a primer teaches (issue 1246); a grimoire's Mag gate beside its rank; then the description.
    /// </summary>
    public static string Text(GameContent content, string id)
    {
        if (content.Items.TryGetValue(id, out var item))
        {
            return item.Teaches is { } teaches
                ? $"{item.Name}. Teaches the {teaches.Label()} school to a Lore class, read at camp. {item.Description}"
                : $"{item.Name}. Heals {item.Heals} HP, {item.Uses} uses. {item.Description}";
        }

        var weapon = content.Weapon(id);
        var type = weapon.Type.Label();
        var power = weapon.Cleanses ? "cleanses burn, chill and stun" : weapon.Heals ? "heals" : $"Power {weapon.Mt}";
        var range = weapon.MinRange == weapon.MaxRange ? $"range {weapon.MinRange}" : $"range {weapon.MinRange}-{weapon.MaxRange}";
        var uses = weapon.IsMagic ? $"{weapon.Durability} uses a battle" : $"{weapon.Durability} uses";
        var school = (weapon.School is { } s ? $", {s.Label()} school" : "") + (weapon.MinMag is { } minMag ? $", needs Mag {minMag}" : "");
        var parts = new List<string> { $"{weapon.Name}, {type} {weapon.Rank}{school}. Acc {weapon.Hit}, {power}, Crit {weapon.Crit}, Wt {weapon.Wt}, {range}, {uses}." };
        if (weapon.EffectiveAgainst.Count > 0)
        {
            parts.Add($"Effective against {string.Join(" and ", weapon.EffectiveAgainst.Select(m => m.ToString().ToLowerInvariant()))}.");
        }

        if (weapon.CritAgainst.Count > 0)
        {
            parts.Add($"Crit +{weapon.CritBonus} against {string.Join(" and ", weapon.CritAgainst.Select(m => m.ToString().ToLowerInvariant()))}.");
        }

        if (weapon.BurnStacks > 1)
        {
            parts.Add($"A hit lays {weapon.BurnStacks} stacks of burn.");
        }

        if (weapon.Type == WeaponType.Bow)
        {
            parts.Add("A crit on a flier grounds it for plain damage.");
        }

        parts.Add(weapon.Description);
        return string.Join(" ", parts.Where(p => p.Length > 0));
    }
}
