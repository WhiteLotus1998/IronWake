namespace Ironwake.Core;

/// <summary>
/// An ability from <c>abilities.json</c> (issue 66): an id, a name, one line of text, and
/// one typed effect. Abilities are data, never a handler per ability, so the forecast, the
/// resolver and both planners price them through the same section 5 functions without
/// knowing any ability by name. Adding a Breaker is a content entry; adding a new kind of
/// effect is a new <see cref="AbilityEffect"/> record and a case in <see cref="AbilityRules"/>.
/// </summary>
public sealed record Ability(string Id, string Name, string Text, AbilityEffect Effect)
{
    public AbilityTrigger Trigger => Effect.Trigger;
}

/// <summary>
/// When an ability's effect applies. The set is closed and small on purpose: Canto's
/// after-action movement and a bow's range extension join it with the issues that need them.
/// </summary>
public enum AbilityTrigger
{
    /// <summary>Always on: a flat delta to the unit's stats, max HP included.</summary>
    Passive,

    /// <summary>In a fight, read by the hit and crit chances, so the forecast shows it.</summary>
    OnCombat,
}

/// <summary>The closed set of ability effects. Each record names its own trigger.</summary>
public abstract record AbilityEffect
{
    private protected AbilityEffect()
    {
    }

    public abstract AbilityTrigger Trigger { get; }
}

/// <summary>A passive flat delta added to the unit's stats after its class modifiers.</summary>
public sealed record StatDeltaEffect(Stats Delta) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.Passive;
}

/// <summary>
/// An on-combat modifier: hit and crit added to this side's strikes, avoid and crit avoid
/// added against the opponent's, whenever the opponent matches <see cref="Against"/>.
/// Lancebreaker (section 5) is <c>+20 hit, +20 avoid against lances</c>.
/// </summary>
public sealed record CombatModifierEffect(OpponentCondition Against, int Hit, int Avoid, int Crit, int CritAvoid) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.OnCombat;
}

/// <summary>
/// Which opponents a combat modifier answers to: a weapon type, a movement type, both
/// (both must match), or neither (every opponent). An opponent with no weapon never
/// matches a weapon condition.
/// </summary>
public sealed record OpponentCondition(WeaponType? Weapon, MovementType? Movement)
{
    public static OpponentCondition Any { get; } = new(null, null);

    public bool Matches(Combatant opponent) =>
        (Weapon is null || opponent.Weapon?.Type == Weapon)
        && (Movement is null || opponent.Movement == Movement);
}

/// <summary>The summed on-combat modifiers one side carries against one opponent.</summary>
public readonly record struct CombatBonus(int Hit, int Avoid, int Crit, int CritAvoid)
{
    public static CombatBonus None => default;
}

/// <summary>
/// The one place abilities are applied. <see cref="Combatant.Stats"/> reads
/// <see cref="Passive"/> and <see cref="Combat"/>'s hit and crit chances read
/// <see cref="Against"/>; nothing else reads an ability's effect.
/// </summary>
public static class AbilityRules
{
    /// <summary>The sum of every passive stat delta in <paramref name="abilities"/>.</summary>
    public static Stats Passive(ValueList<Ability> abilities)
    {
        var total = Stats.Zero;
        foreach (var ability in abilities)
        {
            if (ability.Effect is StatDeltaEffect delta)
            {
                total += delta.Delta;
            }
        }

        return total;
    }

    /// <summary>The sum of <paramref name="self"/>'s combat modifiers whose condition <paramref name="opponent"/> meets.</summary>
    public static CombatBonus Against(Combatant self, Combatant opponent)
    {
        var bonus = CombatBonus.None;
        foreach (var ability in self.Abilities)
        {
            if (ability.Effect is CombatModifierEffect modifier && modifier.Against.Matches(opponent))
            {
                bonus = new CombatBonus(
                    bonus.Hit + modifier.Hit,
                    bonus.Avoid + modifier.Avoid,
                    bonus.Crit + modifier.Crit,
                    bonus.CritAvoid + modifier.CritAvoid);
            }
        }

        return bonus;
    }
}
