using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// DESIGN.md section 5's durability, broken fallback, spell refresh, and healing, and
/// section 7's Item action (issue 9). Wren's iron sword is 5 Mt, 90 hit: whole, she hits
/// the brigand (avoid -3) at 100 for 10; broken, at 91 for 5.
/// </summary>
public class ItemTests
{
    private const string Field = """
        name: Field
        size: 6x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ......
        ......
        ......
        ......

        units:
        P captain 0,1
        P recruit 0,2
        P recruit 3,3
        E brigand 3,1 group:field behavior:aggressive

        """;

    private static readonly Unit Mira = Recruit("mira", "chaplain", new Stats(16, 1, 4, 4, 4, 3, 1, 5, 3), "salve");
    private static readonly Unit Dressed = Recruit("hale", new Stats(22, 8, 0, 7, 8, 6, 5, 2, 9), "iron_sword", "field_dressing");

    private static BattleState Field3(Unit captain, Unit second, Unit third) =>
        Start(roster: ValueList<Unit>.Of(captain, second, third), map: Field);

    private static BattleState Wounded(BattleState state, string id, int hp) => state.WithUnit(state.Find(id)! with { Hp = hp });

    private static int Uses(BattleState state, string id, int slot) => state.Find(id)!.Unit.Inventory.Items[slot].Uses;

    [Fact]
    public void EveryStrikeMadeSpendsOneUseOnBothSidesLandedOrNot()
    {
        var result = Start().Do(new Move("hale", new Coord(2, 1))).Try(new Attack("hale", "brigand-1"));

        var fought = Assert.IsType<CombatFought>(result.Events[0]);
        var byHale = fought.Strikes.Count(s => s.AttackerId == "hale");
        var byBrigand = fought.Strikes.Count(s => s.AttackerId == "brigand-1");
        Assert.True(byHale >= 1);
        Assert.Equal(40 - byHale, Uses(result.Next, "hale", 0));
        if (result.Next.Find("brigand-1") is { } brigand)
        {
            Assert.Equal(40 - byBrigand, brigand.Unit.Inventory.Items[0].Uses);
        }
    }

    [Fact]
    public void TheStrikeThatEmptiesAWeaponBreaksItAndTheForecastShowsTheFallback()
    {
        var state = Start(roster: ValueList<Unit>.Of(Hale, Wren.WithUses(1))).Do(new Move("wren", new Coord(2, 1)));
        var whole = Queries.Forecast(state, Starter, state.Find("wren")!, state.Find("brigand-1")!)!;
        Assert.Equal(10, whole.Attacker.Damage);
        Assert.Equal(100, whole.Attacker.HitChance);

        var result = state.Try(new Attack("wren", "brigand-1"));

        Assert.Contains(new WeaponBroke("wren", "iron_sword"), result.Events);
        Assert.Equal(0, Uses(result.Next, "wren", 0));
        var wren = result.Next.Find("wren")!;
        Assert.True(wren.WeaponBroken(Starter));
        Assert.NotNull(wren.EquippedWeapon(Starter));
        var next = result.Next.WithUnit(wren with { Moved = false, Acted = false });
        var broken = Queries.Forecast(next, Starter, next.Find("wren")!, next.Find("brigand-1")!)!;
        Assert.Equal(5, broken.Attacker.Damage);
        Assert.Equal(91, broken.Attacker.HitChance);
        var again = next.Try(new Attack("wren", "brigand-1"));
        Assert.DoesNotContain(again.Events, e => e is WeaponBroke);
        Assert.Equal(0, Uses(again.Next, "wren", 0));
    }

    [Fact]
    public void SpellsRefreshToFullAtMapStartAndPhysicalWeaponsDoNot()
    {
        var state = Field3(Hale.WithUses(3), Mira.WithUses(1), Wren);

        Assert.Equal(3, Uses(state, "hale", 0));
        Assert.Equal(8, Uses(state, "mira", 0));
    }

    [Fact]
    public void ASpentSpellCannotBeCastAndASpentAttackSpellLeavesTheUnitUnarmed()
    {
        var state = Wounded(Field3(Hale, Mira, Wren), "wren", 5);
        var healed = state.Do(new Move("mira", new Coord(2, 3))).Do(new UseItem("mira", 0, "wren"));
        var spent = healed with { Units = healed.Units };
        spent = spent.WithUnit(spent.Find("mira")! with { Acted = false, Unit = spent.Find("mira")!.Unit.WithUses(0) });
        var refused = spent.Refused(new UseItem("mira", 0, "wren"));
        Assert.Equal(RejectionReason.NotUsable, refused.Reason);
        Assert.Equal("Salve has no uses left this battle", refused.Message);

        var caster = Recruit("mira", "chaplain", new Stats(16, 1, 4, 4, 4, 3, 1, 5, 3), "radiance");
        var field = Field3(Hale, caster, Wren).Do(new Move("mira", new Coord(2, 1)));
        field = field.WithUnit(field.Find("mira")! with { Unit = field.Find("mira")!.Unit.WithUses(1) });
        var result = field.Try(new Attack("mira", "brigand-1"));
        Assert.Contains(new SpellSpent("mira", "radiance"), result.Events);
        var again = result.Next.Do(new EndPhase()).Do(new EndPhase());
        Assert.Null(again.Find("mira")!.EquippedWeapon(Starter));
        Assert.Equal(RejectionReason.NoWeapon, again.Refused(new Attack("mira", "brigand-1")).Reason);
    }

    [Fact]
    public void AFieldDressingHealsTenEndsTheActionAndLeavesAtZero()
    {
        var state = Wounded(Start(roster: ValueList<Unit>.Of(Dressed, Wren)), "hale", 5);

        var result = state.Try(new UseItem("hale", 1));

        Assert.Equal(ValueList<GameEvent>.Of(new ItemUsed("hale", "field_dressing", "hale", 2), new UnitHealed("hale", 10, 15)), result.Events);
        var hale = result.Next.Find("hale")!;
        Assert.Equal(15, hale.Hp);
        Assert.True(hale.Acted);
        Assert.Equal(2, Uses(result.Next, "hale", 1));
        Assert.Equal(RejectionReason.AlreadyActed, result.Next.Refused(new UseItem("hale", 1)).Reason);

        var last = Wounded(Start(roster: ValueList<Unit>.Of(Dressed with { Inventory = Dressed.Inventory.Replace(1, new ItemStack("field_dressing", 1)) }, Wren)), "hale", 20);
        var used = last.Do(new UseItem("hale", 1));
        Assert.Equal(22, used.Find("hale")!.Hp);
        Assert.Single(used.Find("hale")!.Unit.Inventory.Items);
    }

    [Fact]
    public void AHealingSpellHealsAnAllyInRangeByTheFormulaAndPaysTheHealer()
    {
        var state = Wounded(Field3(Hale, Mira, Wren), "wren", 9).Do(new Move("mira", new Coord(2, 3)));

        var result = state.Try(new UseItem("mira", 0, "wren"));

        Assert.Equal(ValueList<GameEvent>.Of(new ItemUsed("mira", "salve", "wren", 7), new UnitHealed("wren", 7, 16), new ExpGained("mira", 16, 16)), result.Events);
        Assert.Equal(16, result.Next.Find("wren")!.Hp);
        Assert.True(result.Next.Find("mira")!.Acted);

        var aboveHalf = Wounded(Field3(Hale, Mira, Wren), "wren", 10).Do(new Move("mira", new Coord(2, 3))).Try(new UseItem("mira", 0, "wren"));
        Assert.Contains(new ExpGained("mira", 11, 11), aboveHalf.Events);
        Assert.Equal(17, aboveHalf.Next.Find("wren")!.Hp);
    }

    [Fact]
    public void UseItemOnAnEmptySlotIsEmptySlot()
    {
        var rejection = Start().Refused(new UseItem("hale", 1));

        Assert.Equal(RejectionReason.EmptySlot, rejection.Reason);
        Assert.Equal("hale has nothing in slot 1; slots run 0-0", rejection.Message);
        Assert.Equal(RejectionReason.EmptySlot, Start().Refused(new UseItem("hale", -1)).Reason);
        Assert.Equal(RejectionReason.NoSuchUnit, Start().Refused(new UseItem("nobody", 0)).Reason);
    }

    [Fact]
    public void AWeaponInTheSlotIsNotUsableAndADressingCannotTargetAnother()
    {
        var weapon = Start().Refused(new UseItem("hale", 0));
        Assert.Equal(RejectionReason.NotUsable, weapon.Reason);
        Assert.Equal("Iron Sword is a weapon, not an item; attack with it", weapon.Message);

        var other = Wounded(Start(roster: ValueList<Unit>.Of(Dressed, Wren)), "hale", 5).Refused(new UseItem("hale", 1, "wren"));
        Assert.Equal(RejectionReason.NotUsable, other.Reason);
    }

    [Fact]
    public void ASpellNeedsALivingAllyInRangeWithSomethingToHeal()
    {
        var state = Wounded(Field3(Hale, Mira, Wren), "wren", 9);

        Assert.Equal(RejectionReason.NoTarget, state.Refused(new UseItem("mira", 0)).Reason);
        Assert.Equal(RejectionReason.NoSuchTarget, state.Refused(new UseItem("mira", 0, "nobody")).Reason);
        Assert.Equal(RejectionReason.NotAnAlly, state.Refused(new UseItem("mira", 0, "brigand-1")).Reason);
        var far = state.Refused(new UseItem("mira", 0, "wren"));
        Assert.Equal(RejectionReason.OutOfRange, far.Reason);
        Assert.Equal("wren at 3,3 is 4 tiles from mira at 0,2; Salve reaches 1-1", far.Message);
        Assert.Equal(RejectionReason.NothingToHeal, state.Refused(new UseItem("mira", 0, "hale")).Reason);
        Assert.Equal(RejectionReason.NothingToHeal, Start(roster: ValueList<Unit>.Of(Dressed, Wren)).Refused(new UseItem("hale", 1)).Reason);
    }

    [Fact]
    public void LegalEnumeratesEveryItemUseThatWouldHeal()
    {
        var whole = Field3(Dressed, Mira, Wren);
        Assert.DoesNotContain(Resolver.Legal(whole, Starter), c => c is UseItem);

        var state = Wounded(Wounded(whole, "hale", 5), "wren", 9);
        var uses = Resolver.Legal(state, Starter).OfType<UseItem>().ToList();

        Assert.Equal(new[] { new UseItem("hale", 1), new UseItem("mira", 0, "hale") }, uses);
        Assert.All(uses, use => Assert.True(state.Try(use).Accepted));
    }
}
