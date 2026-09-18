namespace Ironwake.Core;

/// <summary>
/// What stands on a tile, seen from the moving unit's side. Movement (DESIGN.md
/// section 4) passes through allies, never through enemies, and ends on neither.
/// <see cref="BattleState.OccupantAt"/> answers it during a battle;
/// <see cref="MapDefinition.OccupantAt"/> answers it from starting placements for the map view.
/// </summary>
public enum Occupant
{
    None,
    Ally,
    Enemy,
}
