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
/// hit and crit chances through <see cref="AbilityRules"/>.
/// </summary>
public sealed record Combatant
{
    public Combatant(Unit unit, UnitClass unitClass, Weapon? weapon, Terrain terrain, int hp, int critAvoidModifier = 0, bool broken = false, int hitModifier = 0, int critModifier = 0, ValueList<Ability> abilities = default)
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
        Stats = unit.EffectiveStats(unitClass) + AbilityRules.Passive(abilities);
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

    /// <summary>The unit's stats with the class modifiers and passive ability deltas applied, the numbers the formulas read.</summary>
    public Stats Stats { get; }

    /// <summary>Whether this side can strike a target at <paramref name="distance"/> tiles.</summary>
    public bool CanStrike(int distance) => Weapon is not null && Weapon.InRange(distance);
}
