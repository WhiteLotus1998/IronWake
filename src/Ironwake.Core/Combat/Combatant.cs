namespace Ironwake.Core;

/// <summary>
/// One side of a combat as the formulas of DESIGN.md section 5 see it: a unit in its
/// class, the weapon it fights with (none means it cannot strike), the terrain under it,
/// and its current HP. <see cref="CritAvoidModifier"/> is the section 5 modifier slot that
/// rivalry (13.1) is the first to fill; it may be negative and is never clamped.
/// </summary>
public sealed record Combatant
{
    public Combatant(Unit unit, UnitClass unitClass, Weapon? weapon, Terrain terrain, int hp, int critAvoidModifier = 0)
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

        var maxHp = unit.EffectiveStats(unitClass).Hp;
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
    }

    public Unit Unit { get; }

    public UnitClass Class { get; }

    public Weapon? Weapon { get; }

    public Terrain Terrain { get; }

    public int Hp { get; }

    public int CritAvoidModifier { get; }

    public string Id => Unit.Id;

    public MovementType Movement => Class.Movement;

    /// <summary>The unit's stats with the class modifiers applied, the numbers the formulas read.</summary>
    public Stats Stats => Unit.EffectiveStats(Class);

    /// <summary>Whether this side can strike a target at <paramref name="distance"/> tiles.</summary>
    public bool CanStrike(int distance) => Weapon is not null && Weapon.InRange(distance);
}
