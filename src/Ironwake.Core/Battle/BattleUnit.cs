namespace Ironwake.Core;

/// <summary>
/// A unit as it stands in a battle: the roster or template <see cref="Unit"/> it is, which
/// side it fights for, where it stands, its current HP, and whether it has moved or acted
/// this phase (DESIGN.md section 7: Move is optional and comes first, then one of Attack,
/// Item, Wait). Enemies also carry their group and behavior from the map (section 8).
/// The unit's id is its <see cref="Unit"/>'s id: for a player unit the roster id, for an
/// enemy the template id with a per-template counter (<c>brigand-1</c>), so every roll key
/// and every event names one body on the map. <see cref="PlacementIndex"/> is the index of
/// the map placement the unit filled, so a renderer draws it with the letter section 10
/// gives that slot for the whole battle, wherever it has moved.
/// </summary>
public sealed record BattleUnit(
    Unit Unit,
    Side Side,
    Coord At,
    int Hp,
    bool Moved,
    bool Acted,
    string? Group = null,
    Behavior? Behavior = null,
    bool IsBoss = false,
    bool IsCaptain = false,
    int PlacementIndex = -1)
{
    public string Id => Unit.Id;

    public int MaxHp(GameContent content) => Unit.EffectiveStats(content.Class(Unit.ClassId)).Hp;

    /// <summary>
    /// The inventory slot of the weapon the unit strikes with: the first item that is a
    /// weapon its class can use and that is not a healing spell, skipping a spell with no
    /// uses left this battle (section 5: a physical weapon at zero uses still fights,
    /// broken; a spent spell does not). -1 when there is none. Choosing another slot is
    /// not in this build; the PR for issue 9 says why.
    /// </summary>
    public int EquippedSlot(GameContent content)
    {
        var unitClass = content.Class(Unit.ClassId);
        for (var slot = 0; slot < Unit.Inventory.Count; slot++)
        {
            var item = Unit.Inventory.Items[slot];
            if (content.Weapons.TryGetValue(item.ItemId, out var weapon) && unitClass.CanUse(weapon.Type) && !weapon.Heals
                && (item.Uses > 0 || !weapon.IsMagic))
            {
                return slot;
            }
        }

        return -1;
    }

    /// <summary>The weapon in <see cref="EquippedSlot"/>, or null when the unit has none, in which case it can neither attack nor counter.</summary>
    public Weapon? EquippedWeapon(GameContent content)
    {
        var slot = EquippedSlot(content);
        return slot < 0 ? null : content.Weapon(Unit.Inventory.Items[slot].ItemId);
    }

    /// <summary>Whether the equipped weapon is at zero uses and fights at the broken fallback.</summary>
    public bool WeaponBroken(GameContent content)
    {
        var slot = EquippedSlot(content);
        return slot >= 0 && Unit.Inventory.Items[slot].Uses == 0;
    }

    /// <summary>This unit as the section 5 formulas see it, on the terrain it stands on, its weapon broken or whole.</summary>
    public Combatant ToCombatant(MapDefinition map, GameContent content) =>
        new(Unit, content.Class(Unit.ClassId), EquippedWeapon(content), map.TerrainAt(At, content), Hp, 0, WeaponBroken(content));
}
