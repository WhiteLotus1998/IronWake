namespace Ironwake.Core;

/// <summary>
/// A unit as it exists on the roster or as an enemy template: DESIGN.md section 3.
/// Current HP and map position belong to the battle state, not here. <see cref="Stats"/>
/// are the unit's own numbers; the class modifiers are added on top by
/// <see cref="EffectiveStats"/>, so changing class changes what a unit does on the map.
/// </summary>
public sealed record Unit(
    string Id,
    string Name,
    string ClassId,
    int Level,
    int Exp,
    Stats Stats,
    Stats Growths,
    Inventory Inventory,
    ValueList<string> Abilities,
    string? Region = null,
    string? Personality = null)
{
    public const int MinLevel = 1;
    public const int MaxLevel = 30;
    public const int MaxExp = 99;

    private readonly int _level = Guard(Level, MinLevel, MaxLevel, nameof(Level));
    private readonly int _exp = Guard(Exp, 0, MaxExp, nameof(Exp));

    /// <summary>Level, guarded to 1..30 on construction and on every <c>with</c>.</summary>
    public int Level
    {
        get => _level;
        init => _level = Guard(value, MinLevel, MaxLevel, nameof(Level));
    }

    /// <summary>EXP toward the next level, guarded to 0..99.</summary>
    public int Exp
    {
        get => _exp;
        init => _exp = Guard(value, 0, MaxExp, nameof(Exp));
    }

    private static int Guard(int value, int min, int max, string name) =>
        value >= min && value <= max
            ? value
            : throw new ArgumentOutOfRangeException(name, value, $"{name} must be {min}..{max}");

    public Stats EffectiveStats(UnitClass unitClass) => Stats + unitClass.Modifiers;

    public Stats EffectiveGrowths(UnitClass unitClass) => Growths + unitClass.GrowthModifiers;

    /// <summary>
    /// Scales a template to a higher level without RNG: each stat gains the floor of
    /// growth times levels gained over 100. Used for enemy templates on maps, so the same
    /// map shows the same enemy numbers on every seed.
    /// </summary>
    public Unit AtLevel(int level)
    {
        if (level < Level || level > MaxLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, $"level must be {Level}..{MaxLevel}");
        }

        var gained = level - Level;
        var scaled = Stats.Map((stat, value) => value + Growths.Get(stat) * gained / 100);
        return this with { Level = level, Stats = scaled };
    }

    /// <summary>
    /// The enemy level floor of DESIGN.md section 10 and DECISIONS/0005: a template below
    /// <paramref name="floor"/> is raised to it by <see cref="AtLevel"/>, stats included;
    /// a template already at or above it is returned as it is, so a level-3 boss on a
    /// level-1 map stays level 3. This is the one place the rule lives; the map view and
    /// the battle state both come here through <see cref="MapDefinition.EnemyUnit"/>.
    /// </summary>
    public Unit ScaledTo(int floor) => Level >= floor ? this : AtLevel(floor);
}
