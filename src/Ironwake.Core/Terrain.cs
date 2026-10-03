namespace Ironwake.Core;

/// <summary>
/// A terrain type from the table in DESIGN.md section 4. Movement cost is per movement
/// type; null means impassable. Avoid and Def/Res bonuses do not apply to flyers unless
/// <see cref="AppliesToFlyers"/> is set (Fort and Throne). <see cref="BurnPercent"/> is the
/// heal inverted: a unit on the tile at the start of its side's phase loses that percent of
/// its max HP, never below 1 (Fire, DESIGN.md 13.15, experiment). <see cref="WearsTo"/> is the
/// terrain a unit walking off the tile turns it into, null for ground that never wears
/// (rotten planks, DESIGN.md 13.25, experiment; <see cref="Planks"/>). <see cref="ThawsTo"/> is
/// the terrain the tile turns back into when its clock runs out, null for ground that never thaws
/// (the drake's Rime ice, issue 805, experiment; <see cref="Rime"/>).
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
    bool AppliesToFlyers,
    int BurnPercent = 0,
    string? WearsTo = null,
    string? ThawsTo = null)
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
    /// HP a unit with <paramref name="maxHp"/> loses at the start of its side's phase on this
    /// tile: <see cref="BurnPercent"/> of max, floored. The resolver never takes a unit below 1
    /// with it (DESIGN.md 13.15, experiment).
    /// </summary>
    public int BurnFor(int maxHp) => maxHp * BurnPercent / 100;

    /// <summary>
    /// The terrain's name for the console, with its heal where it has one:
    /// <c>Fort (heals 20 percent)</c>, or given a unit's max HP
    /// <c>Fort (heals 20 percent, 3 hp)</c>. A tile with no heal prints its name alone.
    /// </summary>
    public string Label(int? maxHp = null) =>
        BurnPercent > 0 ? (maxHp is int burnt ? $"{Name} (burns {BurnPercent} percent, {BurnFor(burnt)} hp)" : $"{Name} (burns {BurnPercent} percent)")
        : HealPercent <= 0 ? Name
        : maxHp is int max ? $"{Name} (heals {HealPercent} percent, {HealFor(max)} hp)"
        : $"{Name} (heals {HealPercent} percent)";
}
