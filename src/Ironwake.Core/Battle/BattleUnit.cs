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
/// gives that slot for the whole battle, wherever it has moved. <see cref="Retreated"/>
/// is set by a <see cref="Retreat"/> and never cleared, so no unit retreats twice.
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
    int PlacementIndex = -1,
    bool Retreated = false)
{
    public string Id => Unit.Id;

    public int MaxHp(GameContent content) => content.StatsOf(Unit).Hp;

    /// <summary>
    /// The inventory slot of the weapon the unit strikes with: the first slot that
    /// <see cref="UsableWeaponAt"/> accepts. -1 when there is none. An <see cref="Attack"/>
    /// naming another usable slot moves that weapon to the front first.
    /// </summary>
    public int EquippedSlot(GameContent content)
    {
        for (var slot = 0; slot < Unit.Inventory.Count; slot++)
        {
            if (UsableWeaponAt(content, slot) is not null)
            {
                return slot;
            }
        }

        return -1;
    }

    /// <summary>
    /// The weapon in a slot if the unit can strike with it: a weapon its class can use
    /// that is not a healing spell, skipping a spell with no uses left this battle
    /// (section 5: a physical weapon at zero uses still fights, broken; a spent spell
    /// does not). Null for an empty slot, an item, a healing spell, or a spent spell.
    /// </summary>
    public Weapon? UsableWeaponAt(GameContent content, int slot)
    {
        if (slot < 0 || slot >= Unit.Inventory.Count)
        {
            return null;
        }

        var item = Unit.Inventory.Items[slot];
        var unitClass = content.Class(Unit.ClassId);
        return content.Weapons.TryGetValue(item.ItemId, out var weapon) && unitClass.CanUse(weapon.Type) && !weapon.Heals
            && (item.Uses > 0 || !weapon.IsMagic)
            ? weapon
            : null;
    }

    /// <summary>This unit with the item in <paramref name="slot"/> moved to the front of its inventory, the other slots keeping their order.</summary>
    public BattleUnit WithSlotInFront(int slot)
    {
        if (slot == 0)
        {
            return this;
        }

        var items = new List<ItemStack>(Unit.Inventory.Count) { Unit.Inventory.Items[slot] };
        for (var i = 0; i < Unit.Inventory.Count; i++)
        {
            if (i != slot)
            {
                items.Add(Unit.Inventory.Items[i]);
            }
        }

        return this with { Unit = Unit with { Inventory = new Inventory(ValueList<ItemStack>.From(items)) } };
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
        content.CombatantOf(Unit, EquippedWeapon(content), map.TerrainAt(At, content), Hp, 0, WeaponBroken(content));

    /// <summary>
    /// This unit as the formulas see it on a board, rivalry's modifiers included (issue 16):
    /// the neighbours are read from <paramref name="state"/> at this unit's <see cref="At"/>,
    /// so a forecast may pass the unit at a tile it has not moved to yet.
    /// <paramref name="countering"/> is true for the side that is struck first and answers.
    /// </summary>
    public Combatant ToCombatant(BattleState state, GameContent content, bool countering = false)
    {
        var (hit, crit, critAvoid) = Rivalry.Modifiers(state, content, this, countering);
        return content.CombatantOf(Unit, EquippedWeapon(content), state.Map.TerrainAt(At, content), Hp, critAvoid, WeaponBroken(content), hit, crit);
    }
}
