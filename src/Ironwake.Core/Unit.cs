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

    /// <summary>
    /// The wound a fall left when permadeath is off (issue 664), or null: its penalty is already
    /// taken from <see cref="Stats"/>, and it is given back when the wound expires.
    /// </summary>
    public Wound? Wound { get; init; }

    /// <summary>
    /// Each base class whose second promotion this unit has passed, with the advanced form it took
    /// there (issue 706), in the order taken. A base's second promotion has two doors when a unique
    /// class is offered beside the standard form; a unit that has passed one is refused the other for
    /// good, whatever class it later holds.
    /// </summary>
    public ValueList<(string Base, string Form)> Doors { get; init; } = ValueList<(string, string)>.Empty;

    /// <summary>
    /// The pronoun chosen for this unit at a campaign's start (issue 681, the captain's gender), or
    /// null to read the cast file's (<see cref="GameContent.Pronouns"/>). Text that refers back to
    /// the unit reads it through <see cref="Referent.For(GameContent, Unit)"/>.
    /// </summary>
    public Pronoun? Pronoun { get; init; }

    /// <summary>
    /// The drake this unit rides (issue 805): its stage and the main maps flown, or null for a unit
    /// without one. The campaign gives it to <see cref="DrakeRules.Member"/> as they join.
    /// </summary>
    public DrakeState? Drake { get; init; }

    /// <summary>
    /// The schools this unit learned from a primer at camp (issue 1246, <see cref="CampaignRecord.Read"/>),
    /// in the order learned; empty for every enemy template and for a unit that never read one. A
    /// learned school is reached as a class's is (<see cref="Reaches"/>), but its rider is gated
    /// (<see cref="LearnedGate"/>).
    /// </summary>
    public ValueList<MagicSchool> Learned { get; init; } = ValueList<MagicSchool>.Empty;

    /// <summary>Whether this unit reaches <paramref name="school"/>: its class names it, or it learned it (issue 1246).</summary>
    public bool Reaches(MagicSchool school, UnitClass unitClass) => unitClass.Reaches(school) || Learned.Contains(school);

    /// <summary>
    /// The one line a unit's card prints under its name (issue 806): who this is, in the content's
    /// words, or null for none. The validator holds it to an item description's rule, one line of
    /// at most 72 characters. It is a template's text and the save does not carry it.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether this template is one person rather than a kind (issue 806): text that names it
    /// uses its name as written, <c>Hask</c>, never an article and a lower-cased kind,
    /// <c>a sworn lord</c>. False for every kind of enemy; the cast are people by being the cast.
    /// </summary>
    public bool Named { get; init; }

    /// <summary>
    /// Whether this unit may equip <paramref name="weapon"/>: its class uses the type, its rank in the type
    /// reaches the weapon's, a healing spell is not of a type the class strikes with only (issue 704), and
    /// anything else is not of a type the class heals with only (issue 706), and a schooled tome's school is
    /// one the unit reaches (DECISIONS/0296; learned, issue 1246), and its Mag (unit and class) is at
    /// least a grimoire's <see cref="Weapon.MinMag"/> (issue 1246).
    /// </summary>
    public bool CanWield(Weapon weapon, UnitClass unitClass) =>
        unitClass.CanUse(weapon.Type) && Skill.Rank(weapon.Type) >= weapon.Rank
        && (weapon.Heals ? unitClass.CanHealWith(weapon.Type) : unitClass.CanStrikeWith(weapon.Type))
        && (weapon.School is not { } school || Reaches(school, unitClass))
        && (weapon.MinMag is not { } minMag || EffectiveStats(unitClass).Mag >= minMag);

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
    /// unit's own, since the rolls test the effective growth. Below
    /// <paramref name="ceiling"/> (the yard's teacher's level less one, issue 1331) no level is
    /// taken past it: the EXP is kept, up to <see cref="MaxExp"/>, as the levy drill keeps it.
    /// </summary>
    public ExpResult GainExp(int amount, UnitClass unitClass, IRng rng, int ceiling = MaxLevel)
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
        while (total >= Experience.LevelUpAt && unit.Level < MaxLevel && unit.Level < ceiling)
        {
            total -= Experience.LevelUpAt;
            var (next, gains) = unit.LevelUp(unitClass, rng);
            unit = next;
            levelUps.Add(new LevelUp(unit.Level, gains));
        }

        unit = unit with { Exp = unit.Level >= MaxLevel ? 0 : Math.Min(total, MaxExp) };
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
