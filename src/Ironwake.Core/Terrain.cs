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

    /// <summary>
    /// HP a unit with <paramref name="maxHp"/> regains at the start of its side's phase on
    /// this tile, before the cap at max: <see cref="HealPercent"/> of max, floored. Every
    /// side and movement type heals alike. The resolver applies it and the console prints
    /// it, so the number read is the number landed (issue 207).
    /// </summary>
    public int HealFor(int maxHp) => maxHp * HealPercent / 100;

    /// <summary>
    /// The terrain's name for the console, with its heal where it has one:
    /// <c>Fort (heals 20 percent)</c>, or given a unit's max HP
    /// <c>Fort (heals 20 percent, 3 hp)</c>. A tile with no heal prints its name alone.
    /// </summary>
    public string Label(int? maxHp = null) =>
        HealPercent <= 0 ? Name
        : maxHp is int max ? $"{Name} (heals {HealPercent} percent, {HealFor(max)} hp)"
        : $"{Name} (heals {HealPercent} percent)";
}
