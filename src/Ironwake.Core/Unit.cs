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

    /// <summary>
    /// The two cast members this recruit gets on with (DESIGN.md section 9): the support
    /// hooks Phase 3 builds rapport on. Empty for the captain and for enemy templates.
    /// </summary>
    public ValueList<string> Hooks { get; init; } = ValueList<string>.Empty;

    /// <summary>
    /// Rank points per weapon type (issue 67): gained by use for player units, declared in
    /// content for enemy templates, which nothing scales with level.
    /// </summary>
    public WeaponSkill Skill { get; init; } = WeaponSkill.Zero;

    /// <summary>
    /// Mastery points per class (issue 69), earned in combat by player units. A mastered
    /// ability is added to <see cref="Abilities"/>, so it outlives the class.
    /// </summary>
    public MasteryProgress Mastery { get; init; } = MasteryProgress.Empty;

    /// <summary>Whether this unit may equip <paramref name="weapon"/>: its class uses the type and its rank in the type reaches the weapon's.</summary>
    public bool CanWield(Weapon weapon, UnitClass unitClass) =>
        unitClass.CanUse(weapon.Type) && Skill.Rank(weapon.Type) >= weapon.Rank;

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
    /// Scales a template to a higher level without RNG: each stat gains the floor of its
    /// effective growth (unit growth plus the class modifier, DESIGN.md section 3) times
    /// levels gained over 100. That is the rolled level-up in expectation, so a template
    /// authored at a level and a template raised to it gain what the class gains. The
    /// growth is clamped to 0..100 first: under a rolled level-up a negative effective
    /// growth is a probability of zero, never a stat loss, and one above 100 is certainty.
    /// Used for enemy templates on maps, so the same map shows the same enemy numbers on
    /// every seed (DECISIONS/0005). <paramref name="unitClass"/> must be this unit's own
    /// class; scaling by another class's growth would be a different unit.
    /// </summary>
    public Unit AtLevel(int level, UnitClass unitClass)
    {
        if (unitClass.Id != ClassId)
        {
            throw new ArgumentException(
                $"class must be the unit's own: {Id} is a {ClassId}, not a {unitClass.Id}", nameof(unitClass));
        }

        if (level < Level || level > MaxLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, $"level must be {Level}..{MaxLevel}");
        }

        var gained = level - Level;
        var growths = EffectiveGrowths(unitClass);
        var scaled = Stats.Map((stat, value) => value + Math.Clamp(growths.Get(stat), 0, 100) * gained / 100);
        return this with { Level = level, Stats = scaled };
    }

    /// <summary>
    /// The enemy level floor of DESIGN.md section 10 and DECISIONS/0005: a template below
    /// <paramref name="floor"/> is raised to it by <see cref="AtLevel"/>, stats included;
    /// a template already at or above it is returned as it is, so a level-3 boss on a
    /// level-1 map stays level 3. This is the one place the rule lives; the map view and
    /// the battle state both come here through <see cref="MapDefinition.EnemyUnit"/>.
    /// </summary>
    public Unit ScaledTo(int floor, UnitClass unitClass) => Level >= floor ? this : AtLevel(floor, unitClass);

    /// <summary>
    /// Adds EXP and levels up for every 100 crossed (DESIGN.md sections 3 and 6). At the
    /// level cap nothing is gained and the unit is returned unchanged; a level-up that
    /// reaches the cap discards the remainder. <paramref name="unitClass"/> must be the
    /// unit's own, since the rolls test the effective growth.
    /// </summary>
    public ExpResult GainExp(int amount, UnitClass unitClass, IRng rng)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "exp gained must be at least 0");
        }

        if (Level >= MaxLevel || amount == 0)
        {
            return new ExpResult(this, ValueList<LevelUp>.Empty);
        }

        var unit = this;
        var levelUps = new List<LevelUp>();
        var total = Exp + amount;
        while (total >= Experience.LevelUpAt && unit.Level < MaxLevel)
        {
            total -= Experience.LevelUpAt;
            var (next, gains) = unit.LevelUp(unitClass, rng);
            unit = next;
            levelUps.Add(new LevelUp(unit.Level, gains));
        }

        unit = unit with { Exp = unit.Level >= MaxLevel ? 0 : total };
        return new ExpResult(unit, ValueList<LevelUp>.From(levelUps));
    }

    /// <summary>
    /// One level: every stat rolls against its effective growth clamped to 0..100 under
    /// the key (unit, new level, stat) and nothing else (section 3), so the same seed
    /// gives this unit the same level 7 whatever else happened. Returns the unit and the
    /// gains, a <see cref="Stats"/> of 0s and 1s.
    /// </summary>
    public (Unit Unit, Stats Gains) LevelUp(UnitClass unitClass, IRng rng)
    {
        if (unitClass.Id != ClassId)
        {
            throw new ArgumentException(
                $"class must be the unit's own: {Id} is a {ClassId}, not a {unitClass.Id}", nameof(unitClass));
        }

        if (Level >= MaxLevel)
        {
            throw new InvalidOperationException($"{Id} is already at level {MaxLevel}");
        }

        var newLevel = Level + 1;
        var growths = EffectiveGrowths(unitClass);
        var gains = Stats.Zero.Map((stat, _) => rng.Roll(RollKey.Growth(Id, newLevel, stat)) < Math.Clamp(growths.Get(stat), 0, 100) ? 1 : 0);
        return (this with { Level = newLevel, Stats = Stats + gains }, gains);
    }
}
