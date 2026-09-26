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
/// When an ability's effect applies. The set is closed and small on purpose: a bow's range
/// extension joins it with the issue that needs it.
/// </summary>
public enum AbilityTrigger
{
    /// <summary>Always on: a flat delta to the unit's stats, max HP included.</summary>
    Passive,

    /// <summary>In a fight, read by the hit and crit chances, so the forecast shows it.</summary>
    OnCombat,

    /// <summary>Declared with an attack command, before the roll: a combat art (issue 68).</summary>
    Declared,

    /// <summary>After the unit's Attack, Item or Wait: Canto's second move (issue 71).</summary>
    AfterAction,
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
/// added against the opponent's, whenever the opponent matches <see cref="Against"/> and,
/// when <see cref="Wielding"/> is set, the holder strikes or counters with a weapon of that
/// type (issue 245). Lancebreaker (section 5) is <c>+20 hit, +20 avoid against lances</c>;
/// Bloodrush is <c>+15 crit, -10 avoid while wielding an axe</c>.
/// </summary>
public sealed record CombatModifierEffect(OpponentCondition Against, int Hit, int Avoid, int Crit, int CritAvoid) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.OnCombat;

    /// <summary>The weapon type the holder must fight with for the modifier to apply, or null for any; an unarmed holder never matches a type.</summary>
    public WeaponType? Wielding { get; init; }

    /// <summary>Whether the modifier applies to <paramref name="self"/> fighting <paramref name="opponent"/>.</summary>
    public bool AppliesTo(Combatant self, Combatant opponent) =>
        (Wielding is null || self.Weapon?.Type == Wielding) && Against.Matches(opponent);
}

/// <summary>
/// A combat art (issue 68): declared with the attack command, before the roll, on a
/// weapon of <see cref="Weapon"/>'s type at rank <see cref="Rank"/> or above. The art is
/// the weapon with these deltas added (<see cref="Apply"/>), so the forecast, the resolver,
/// burden and doubling all read it through the unchanged section 5 functions, and a Wt
/// delta can cost the unit its double. <see cref="Cost"/> is the extra uses the attack
/// spends on top of one per strike, paid whether the art hits or misses.
/// </summary>
public sealed record CombatArtEffect(WeaponType Weapon, WeaponRank Rank, int Cost, int Mt, int Hit, int Crit, int Wt, int Range) : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.Declared;

    /// <summary>The weapon as the art strikes with it: the deltas added, Mt and Wt floored at zero, the far end of its range extended.</summary>
    public Weapon Apply(Weapon weapon) => weapon with
    {
        Mt = Math.Max(0, weapon.Mt + Mt),
        Hit = weapon.Hit + Hit,
        Crit = weapon.Crit + Crit,
        Wt = Math.Max(0, weapon.Wt + Wt),
        MaxRange = weapon.MaxRange + Range,
    };

    /// <summary>The fewest uses a weapon must have left to pay for the art: its cost and the first strike.</summary>
    public int UsesNeeded => Cost + 1;
}

/// <summary>
/// Canto (issue 71, DESIGN.md section 7): after an Attack, Item or Wait the unit may move
/// again on what its first move left of its Mov, through <see cref="Movement.Reach"/> from
/// where it stands. The effect has no numbers; content decides which classes carry it.
/// </summary>
public sealed record CantoEffect : AbilityEffect
{
    public override AbilityTrigger Trigger => AbilityTrigger.AfterAction;
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

    /// <summary>Whether any of <paramref name="abilities"/> is Canto.</summary>
    public static bool HasCanto(ValueList<Ability> abilities) => abilities.Any(a => a.Effect is CantoEffect);

    /// <summary>The sum of <paramref name="self"/>'s combat modifiers whose conditions hold: <paramref name="opponent"/> meets the opponent condition, and <paramref name="self"/>'s weapon the wielding one.</summary>
    public static CombatBonus Against(Combatant self, Combatant opponent)
    {
        var bonus = CombatBonus.None;
        foreach (var ability in self.Abilities)
        {
            if (ability.Effect is CombatModifierEffect modifier && modifier.AppliesTo(self, opponent))
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
