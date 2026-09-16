namespace Ironwake.Core;

/// <summary>
/// What stands on a tile, seen from the moving unit's side. Movement (DESIGN.md
/// section 4) passes through allies, never through enemies, and ends on neither.
/// The battle state answers this once issue 6 lands; until then
/// <see cref="MapDefinition.OccupantAt"/> answers it from starting placements.
/// </summary>
public enum Occupant
{
    None,
    Ally,
    Enemy,
}
