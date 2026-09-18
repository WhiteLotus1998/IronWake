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
    /// The weapon the unit strikes with: the first inventory item that is a weapon its
    /// class can use and that is not a healing spell. Null when it has none, in which
    /// case it can neither attack nor counter. Issue 9 adds explicit equipping.
    /// </summary>
    public Weapon? EquippedWeapon(GameContent content)
    {
        var unitClass = content.Class(Unit.ClassId);
        foreach (var item in Unit.Inventory.Items)
        {
            if (content.Weapons.TryGetValue(item.ItemId, out var weapon) && unitClass.CanUse(weapon.Type) && !weapon.Heals)
            {
                return weapon;
            }
        }

        return null;
    }

    /// <summary>This unit as the section 5 formulas see it, on the terrain it stands on.</summary>
    public Combatant ToCombatant(MapDefinition map, GameContent content) =>
        new(Unit, content.Class(Unit.ClassId), EquippedWeapon(content), map.TerrainAt(At, content), Hp);
}
