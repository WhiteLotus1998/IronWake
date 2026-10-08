namespace Ironwake.Core;

/// <summary>
/// One side of a combat as the formulas of DESIGN.md section 5 see it: a unit in its
/// class, the weapon it fights with (none means it cannot strike), the terrain under it,
/// and its current HP. <see cref="CritAvoidModifier"/> is the section 5 modifier slot that
/// rivalry (13.1) is the first to fill; it may be negative and is never clamped.
/// <see cref="HitModifier"/> and <see cref="CritModifier"/> are added to Hit and Crit
/// before the clamps; rivalry fills them too (issue 16).
/// <see cref="Broken"/> marks a physical weapon at zero uses: it still strikes, at the
/// section 5 fallback of -5 Mt and -10 hit, so a unit is never helpless.
/// <see cref="Abilities"/> are the unit's resolved abilities (issue 66): their passive
/// deltas are part of <see cref="Stats"/> and their combat modifiers are read by the
/// hit and crit chances through <see cref="AbilityRules"/>. <c>beside</c> is what the unit's
/// <see cref="BesideStatsEffect"/> adds where it fights with an ally beside it (issue 705), read on the
/// board by <see cref="Formation.Beside"/>; it is part of <see cref="Stats"/>.
/// </summary>
public sealed record Combatant
{
    public Combatant(Unit unit, UnitClass unitClass, Weapon? weapon, Terrain terrain, int hp, int critAvoidModifier = 0, bool broken = false, int hitModifier = 0, int critModifier = 0, ValueList<Ability> abilities = default, Stats beside = default)
    {
        if (unitClass.Id != unit.ClassId)
        {
            throw new ArgumentException(
                $"class must be the unit's own: {unit.Id} is a {unit.ClassId}, not a {unitClass.Id}", nameof(unitClass));
        }

        if (weapon is not null && !unitClass.CanUse(weapon.Type))
        {
            throw new ArgumentException($"{unit.Id} is a {unitClass.Id} and cannot use {weapon.Id} ({weapon.Type})", nameof(weapon));
        }

        Abilities = abilities;
        Stats = unit.EffectiveStats(unitClass) + AbilityRules.Passive(abilities) + beside;
        var maxHp = Stats.Hp;
        if (hp < 1 || hp > maxHp)
        {
            throw new ArgumentOutOfRangeException(nameof(hp), hp, $"hp must be 1..{maxHp} for {unit.Id}");
        }

        Unit = unit;
        Class = unitClass;
        Weapon = weapon;
        Terrain = terrain;
        Hp = hp;
        CritAvoidModifier = critAvoidModifier;
        Broken = weapon is not null && broken;
        HitModifier = hitModifier;
        CritModifier = critModifier;
    }

    public Unit Unit { get; }

    public UnitClass Class { get; }

    public Weapon? Weapon { get; }

    public Terrain Terrain { get; }

    public int Hp { get; }

    public int CritAvoidModifier { get; }

    public int HitModifier { get; }

    public int CritModifier { get; }

    /// <summary>Whether the weapon is at zero uses and fights at the broken fallback.</summary>
    public bool Broken { get; }

    public string Id => Unit.Id;

    public MovementType Movement => Class.Movement;

    public ValueList<Ability> Abilities { get; }

    /// <summary>
    /// The aura this side fights under (issue 705, <see cref="AuraEffect"/>): hit added to its strikes and
    /// avoid against the opponent's, read on the board by <see cref="Formation.Aura"/>; none off the board.
    /// </summary>
    public CombatBonus Aura { get; init; }

    /// <summary>The unit's stats with the class modifiers and passive ability deltas applied, the numbers the formulas read.</summary>
    public Stats Stats { get; }

    /// <summary>
    /// Whether this side answers blind: at dusk its side cannot see the tile the strike came
    /// from, so it makes no counter (DESIGN.md 13.7, issue 308). The weapon stays in hand, so
    /// its weight still slows the unit and the attacker's numbers do not change.
    /// </summary>
    public bool Blind { get; init; }

    /// <summary>
    /// Whether this side has spent its answer: on a <c>one_answer: on</c> map it already countered
    /// this phase, so it makes no counter until the next phase begins (DESIGN.md 13.29, <see cref="Answer"/>).
    /// </summary>
    public bool AnswerSpent { get; init; }

    /// <summary>
    /// Whether this side is oath-bound (issue 691): an enemy in a group its map's <c>oathbound:</c>
    /// header names. Read by an ability whose condition asks for it (Unsworn), nothing else.
    /// </summary>
    public bool Oathbound { get; init; }

    /// <summary>
    /// Whether this side's blows pull (issue 1331, Table round 448): the teacher in a yard drill. A strike or bite
    /// from it that would take its target to 0 leaves it at 1 HP instead, counters included
    /// (<see cref="CombatResolver"/>), so the student takes the kill. False everywhere else.
    /// </summary>
    public bool Pulls { get; init; }

    /// <summary>
    /// Whether this side is held by the pair rule here (issue 692): an enemy in a group its map's
    /// <c>pair_rule:</c> header names, striking or answering a unit with an ally beside it
    /// (<see cref="PairRule"/>). It neither doubles (<see cref="Combat.Doubles"/>) nor crits
    /// (<see cref="Combat.CritChance"/> reads 0).
    /// </summary>
    public bool PairHeld { get; init; }

    /// <summary>
    /// Whether this side strikes once whatever the speed gap (issue 739): it declared an art
    /// that carries <see cref="CombatArtEffect.Single"/>. It never doubles (<see cref="Combat.Doubles"/>).
    /// </summary>
    public bool SingleStrike { get; init; }

    /// <summary>
    /// What this side's first strike of the combat adds when it hits (issue 1127, <see cref="Ironwake.Core.Stoop"/>):
    /// the Sky Captain's dive after a long flight, read on the board; 0 off the board and for every counter.
    /// </summary>
    public int Stoop { get; init; }

    /// <summary>
    /// Whether this side is a Hollow (issue 1284, <see cref="Ironwake.Core.Hollow"/>), read on the board from its mark: a light strike
    /// that is effective against Hollows triples its Mt against it (<see cref="Combat.IsEffective"/>, issue 1321). False off the board.
    /// </summary>
    public bool Hollow { get; init; }

    /// <summary>
    /// The school whose first hit on this side is marked (issue 1329, <see cref="Ironwake.Core.Mark"/>), read on the board;
    /// null off the board and for every unmarked unit.
    /// </summary>
    public MagicSchool? Marked { get; init; }

    /// <summary>
    /// The school of this side's Lightning Rod charge (issue 1329, <see cref="Ironwake.Core.LightningRod"/>): a strike, never a counter,
    /// with a tome of it deals x1.25 on final damage. Read on the board; null off it and for every uncharged unit.
    /// </summary>
    public MagicSchool? Charged { get; init; }

    /// <summary>
    /// Whether this side is a Lightning Rod holder struck by the spell it caught (issue 1329, <see cref="Ironwake.Core.LightningRod"/>):
    /// a tome's strikes on it deal x0.5 on final damage. False everywhere else.
    /// </summary>
    public bool Catching { get; init; }

    /// <summary>Whether this side can strike a target at <paramref name="distance"/> tiles: armed, in range, not <see cref="Blind"/>, and not <see cref="AnswerSpent"/>.</summary>
    public bool CanStrike(int distance) => !Blind && !AnswerSpent && Weapon is not null && Weapon.InRange(distance);
}
