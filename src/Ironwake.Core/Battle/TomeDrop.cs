namespace Ironwake.Core;

/// <summary>
/// A grimoire's way into the company (issue 1246, DECISIONS/0301): an enemy placed on a tile a map's
/// <c>drops:</c> header names (<see cref="MapDefinition.Drops"/>) sends every Lore tome it carries to the
/// battle's wagon when it dies (<see cref="TomeDropped"/>). The wagon is collected onto the record only
/// on a win (<see cref="CampaignRecord.Fought"/>), the rule a chest's overflow already keeps, so a lost
/// map loses the drop too. A Recall restores the wagon with the board.
/// </summary>
public static class TomeDrop
{
    /// <summary>Whether <paramref name="unit"/> drops its tomes on death: an enemy placed on a <c>drops:</c> tile.</summary>
    public static bool Drops(BattleState state, BattleUnit unit) =>
        state.Map.Drops.Count > 0
        && unit.Side == Side.Enemy
        && unit.PlacementIndex >= 0
        && unit.PlacementIndex < state.Map.Placements.Count
        && state.Map.Drops.Contains(state.Map.Placements[unit.PlacementIndex].At);

    /// <summary>
    /// <paramref name="state"/> once <paramref name="fallen"/> has died: its Lore tomes added to the
    /// wagon in pack order, with one <see cref="TomeDropped"/>, when it drops; else unchanged.
    /// </summary>
    public static BattleState After(BattleState state, BattleUnit fallen, GameContent content, List<GameEvent> events)
    {
        if (!Drops(state, fallen))
        {
            return state;
        }

        var tomes = fallen.Unit.Inventory.Items
            .Where(stack => content.Weapons.TryGetValue(stack.ItemId, out var weapon) && weapon.Type == WeaponType.Reason)
            .Select(stack => stack.ItemId)
            .ToList();
        if (tomes.Count == 0)
        {
            return state;
        }

        events.Add(new TomeDropped(fallen.Id, ValueList<string>.From(tomes)));
        return state with { Wagon = ValueList<string>.From(state.Wagon.Concat(tomes)) };
    }
}
