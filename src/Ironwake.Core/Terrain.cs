namespace Ironwake.Core;

/// <summary>
/// A terrain type from the table in DESIGN.md section 4. Movement cost is per movement
/// type; null means impassable. Avoid and Def/Res bonuses do not apply to flyers unless
/// <see cref="AppliesToFlyers"/> is set (Fort and Throne).
/// </summary>
public sealed record Terrain(
    string Id,
    string Name,
    char Glyph,
    ValueList<int?> MoveCosts,
    int Avoid,
    int Def,
    int Res,
    int HealPercent,
    bool AppliesToFlyers)
{
    /// <summary>Cost to enter for the given movement type, or null if impassable.</summary>
    public int? MoveCost(MovementType movement) => MoveCosts[(int)movement];

    public bool IsPassable(MovementType movement) => MoveCost(movement) is not null;

    public bool BonusesApplyTo(MovementType movement) => movement != MovementType.Flying || AppliesToFlyers;

    public int AvoidFor(MovementType movement) => BonusesApplyTo(movement) ? Avoid : 0;

    public int DefFor(MovementType movement) => BonusesApplyTo(movement) ? Def : 0;

    public int ResFor(MovementType movement) => BonusesApplyTo(movement) ? Res : 0;
}
