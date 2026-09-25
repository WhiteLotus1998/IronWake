using static Ironwake.Core.Tests.Combat.CombatFixture;

namespace Ironwake.Core.Tests.Combat;

/// <summary>
/// Issue 66: a passive flat delta and a conditional combat modifier each change the
/// forecast and the resolver through the one path the section 5 functions read. Wren
/// (hit 98, avoid 18) fights the brigand in forest (hit 73, avoid 26): 72 one way, 55 the other.
/// </summary>
public class AbilityRulesTests
{
    private static readonly CombatContext Turn3 = new(3, Side.Player);

    private static Ability Passive(Stats delta) => new("passive", "Passive", "a delta", new StatDeltaEffect(delta));

    private static Ability Modifier(OpponentCondition against, int hit = 0, int avoid = 0, int crit = 0, int critAvoid = 0) =>
        new("modifier", "Modifier", "a modifier", new CombatModifierEffect(against, hit, avoid, crit, critAvoid));

    private static readonly OpponentCondition AgainstAxes = new(WeaponType.Axe, null);

    private static Combatant WrenWith(params Ability[] abilities) =>
        new(Wren, Cadet, IronSword, Plain, 20, abilities: ValueList<Ability>.Of(abilities));

    [Fact]
    public void APassiveStatDeltaIsPartOfTheStatsTheFormulasRead()
    {
        var wren = WrenWith(Passive(Stats.Zero with { Def = 3 }));

        Assert.Equal(7, wren.Stats.Def);
        Assert.Equal(7, Core.Combat.Damage(BrigandInForest(), wren));
    }

    [Fact]
    public void APassiveStatDeltaChangesForecastAndResolverAlike()
    {
        var wren = WrenWith(Passive(Stats.Zero with { Def = 3 }));

        var forecast = Core.Combat.Forecast(wren, BrigandInForest(), 1, RollScheme.TwoRollAverage);
        var result = CombatResolver.Resolve(wren, BrigandInForest(), 1, Turn3, new ScriptedRng(50), RollScheme.TwoRollAverage);

        // The brigand's 14 Atk against Def 4 + 3: 7, where it is 10 without the ability.
        Assert.Equal(7, forecast.Defender.Damage);
        Assert.Equal(new StrikeEvent(1, "brigand", "wren", true, false, 7, 13), result.Strikes[1]);
        Assert.Equal(13, result.AttackerHp);
    }

    [Fact]
    public void APassiveHpDeltaRaisesMaxHp()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Combatant(Wren, Cadet, IronSword, Plain, 23));

        var wren = new Combatant(Wren, Cadet, IronSword, Plain, 23, abilities: ValueList<Ability>.Of(Passive(Stats.Zero with { Hp = 3 })));

        Assert.Equal(23, wren.Stats.Hp);
    }

    [Fact]
    public void ACombatModifierAddsHitAgainstAMatchingOpponentOnly()
    {
        var wren = WrenWith(Modifier(AgainstAxes, hit: 20));

        Assert.Equal(92, Core.Combat.HitChance(wren, BrigandInForest()));
        Assert.Equal(Core.Combat.HitChance(WrenOnPlain(), RiderOnPlain()), Core.Combat.HitChance(wren, RiderOnPlain()));
    }

    [Fact]
    public void ACombatModifierAddsAvoidAgainstTheOpponentsStrikes()
    {
        var wren = WrenWith(Modifier(AgainstAxes, avoid: 20));

        var forecast = Core.Combat.Forecast(wren, BrigandInForest(), 1, RollScheme.OneRoll);

        Assert.Equal(72, forecast.Attacker.HitChance);
        Assert.Equal(35, forecast.Defender.HitChance);
    }

    [Fact]
    public void ACombatModifierMovesCritAndCritAvoid()
    {
        // Wren's crit (6 + 5) / 2 = 5 less the brigand's Lck 1: 4. The brigand's crit
        // (3 + 1) / 2 = 2 less Wren's Lck 5 clamps to 0; a crit avoid of -10 makes it 7.
        var wren = WrenWith(Modifier(AgainstAxes, crit: 10, critAvoid: -10));

        Assert.Equal(14, Core.Combat.CritChance(wren, BrigandInForest()));
        Assert.Equal(7, Core.Combat.CritChance(BrigandInForest(), wren));
        Assert.Equal(0, Core.Combat.CritChance(BrigandInForest(), WrenOnPlain()));
    }

    [Fact]
    public void TheResolverLandsWhatTheModifierLifts()
    {
        // Rolls of 99 miss at 72 and land at 100: the modifier reaches the resolver.
        var rng = new ScriptedRng(99);

        var without = CombatResolver.Resolve(WrenOnPlain(), BrigandInForest(), 1, Turn3, rng, RollScheme.TwoRollAverage);
        var with = CombatResolver.Resolve(WrenWith(Modifier(AgainstAxes, hit: 30)), BrigandInForest(), 1, Turn3, rng, RollScheme.TwoRollAverage);

        Assert.False(without.Strikes[0].Hit);
        Assert.True(with.Strikes[0].Hit);
    }

    [Fact]
    public void AMovementConditionMatchesThatMovementOnly()
    {
        var wren = WrenWith(Modifier(new OpponentCondition(null, MovementType.Flying), hit: 10));

        Assert.Equal(Core.Combat.HitChance(WrenOnPlain(), WingriderInForest()) + 10, Core.Combat.HitChance(wren, WingriderInForest()));
        Assert.Equal(72, Core.Combat.HitChance(wren, BrigandInForest()));
    }

    [Fact]
    public void BothConditionsMustMatchWhenBothAreNamed()
    {
        var condition = new OpponentCondition(WeaponType.Axe, MovementType.Flying);

        Assert.False(condition.Matches(BrigandInForest()));
        Assert.True(new OpponentCondition(WeaponType.Axe, MovementType.Infantry).Matches(BrigandInForest()));
        Assert.True(OpponentCondition.Any.Matches(BrigandInForest()));
    }

    [Fact]
    public void AnUnarmedOpponentNeverMatchesAWeaponCondition()
    {
        var unarmed = new Combatant(Brigand, Reaver, null, Forest, 22);

        Assert.False(AgainstAxes.Matches(unarmed));
        Assert.True(new OpponentCondition(null, MovementType.Infantry).Matches(unarmed));
    }

    [Fact]
    public void ModifiersFromSeveralAbilitiesSum()
    {
        var wren = WrenWith(Modifier(AgainstAxes, hit: 5), Modifier(OpponentCondition.Any, hit: 3), Passive(Stats.Zero with { Dex = 2 }));

        // Dex 2 more is 2 hit and 1 crit; the modifiers add 8 hit.
        Assert.Equal(82, Core.Combat.HitChance(wren, BrigandInForest()));
        Assert.Equal(new CombatBonus(8, 0, 0, 0), AbilityRules.Against(wren, BrigandInForest()));
    }

    [Fact]
    public void EachEffectDeclaresItsTrigger()
    {
        Assert.Equal(AbilityTrigger.Passive, Passive(Stats.Zero with { Def = 1 }).Trigger);
        Assert.Equal(AbilityTrigger.OnCombat, Modifier(AgainstAxes, hit: 1).Trigger);
    }
}
