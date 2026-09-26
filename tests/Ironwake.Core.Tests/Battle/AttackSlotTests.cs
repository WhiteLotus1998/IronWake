using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 99: an Attack may name the weapon slot to strike with; the weapon moves to the
/// front so the counter uses it too. Hale with an iron sword (5 Mt, 90 hit) and a steel
/// sword (8 Mt, 80 hit) beside the brigand (Def 2, avoid -3): iron hits at 100 for 11,
/// steel at 93 for 14.
/// </summary>
public class AttackSlotTests
{
    private static readonly Unit TwoSwords = Recruit("hale", new Stats(22, 8, 0, 7, 8, 6, 5, 2, 9), "iron_sword", "steel_sword", "field_dressing");

    private static BattleState Beside() =>
        Start(roster: ValueList<Unit>.Of(TwoSwords, Wren)).Do(new Move("hale", new Coord(2, 1)));

    [Fact]
    public void ANamedSlotStrikesWithThatWeaponAndTheForecastShowsItsNumbers()
    {
        var state = Beside();
        var hale = state.Find("hale")!;
        var brigand = state.Find("brigand-1")!;

        Assert.Equal(11, Queries.Forecast(state, Starter, hale, brigand)!.Attacker.Damage);
        var steel = Queries.Forecast(state, Starter, hale, brigand, 1)!;
        Assert.Equal(14, steel.Attacker.Damage);
        Assert.Equal(82, steel.Attacker.HitChance);

        var result = state.Try(new Attack("hale", "brigand-1", 1));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(new WeaponEquipped("hale", "steel_sword"), result.Events[0]);
        var fought = Assert.IsType<CombatFought>(result.Events[1]);
        Assert.All(fought.Strikes.Where(s => s.AttackerId == "hale" && s.Hit && !s.Crit), s => Assert.Equal(14, s.Damage));
        var after = result.Next.Find("hale")?.Unit.Inventory.Items ?? result.Next.History[^1].Find("hale")!.Unit.Inventory.Items;
        Assert.Equal("steel_sword", after[0].ItemId);
        Assert.Equal("iron_sword", after[1].ItemId);
        Assert.Equal("field_dressing", after[2].ItemId);
        Assert.Equal(40, after[1].Uses);
        Assert.Equal(30 - fought.Strikes.Count(s => s.AttackerId == "hale"), after[0].Uses);
    }

    [Fact]
    public void NamingTheEquippedSlotOrNoSlotChangesNothingAndEmitsNoEquip()
    {
        var state = Beside();

        var named = state.Try(new Attack("hale", "brigand-1", 0));
        var bare = state.Try(new Attack("hale", "brigand-1"));

        Assert.True(named.Accepted);
        Assert.DoesNotContain(named.Events, e => e is WeaponEquipped);
        Assert.Equal(bare.Next.Canonical(), named.Next.Canonical());
    }

    [Theory]
    [InlineData(2, RejectionReason.NotUsable, "hale cannot attack with field_dressing: an item, not a weapon")]
    [InlineData(3, RejectionReason.EmptySlot, "hale has nothing in slot 3; slots run 0-2")]
    [InlineData(-1, RejectionReason.EmptySlot, "hale has nothing in slot -1; slots run 0-2")]
    public void ASlotThatHoldsNoUsableWeaponIsRefusedByName(int slot, RejectionReason reason, string message)
    {
        var rejection = Beside().Refused(new Attack("hale", "brigand-1", slot));

        Assert.Equal(reason, rejection.Reason);
        Assert.Equal(message, rejection.Message);
    }

    [Fact]
    public void AHealingSpellOrASpentSpellInTheSlotIsRefused()
    {
        var mira = Recruit("mira", "chaplain", new Stats(16, 1, 4, 4, 4, 3, 1, 5, 3), "radiance", "salve");
        var state = Start(roster: ValueList<Unit>.Of(Hale, mira), map: Yard.Replace("recruit:wren", "recruit")).Do(new Move("mira", new Coord(2, 2)));

        var healing = state.Refused(new Attack("mira", "soldier-1", 1));
        Assert.Equal(RejectionReason.NotUsable, healing.Reason);
        Assert.Equal("mira cannot attack with salve: a healing spell; use it with item", healing.Message);

        var spent = state.WithUnit(state.Find("mira")! with { Unit = state.Find("mira")!.Unit.WithUses(0) });
        var refused = spent.Refused(new Attack("mira", "soldier-1", 0));
        Assert.Equal(RejectionReason.NotUsable, refused.Reason);
        Assert.Equal("mira cannot attack with radiance: spent for this battle", refused.Message);
        Assert.Null(Queries.Forecast(spent, Starter, spent.Find("mira")!, spent.Find("soldier-1")!, 0));
    }

    [Fact]
    public void LegalListsOneAttackPerUsableWeaponSlotAndNoSlotForASingleWeapon()
    {
        var two = Beside();
        var attacks = Resolver.Legal(two, Starter).OfType<Attack>().Where(a => a.UnitId == "hale").ToList();
        Assert.Equal(new[] { new Attack("hale", "brigand-1", 0), new Attack("hale", "brigand-1", 1) }, attacks);
        Assert.All(attacks, a => Assert.True(two.Try(a).Accepted));

        var one = Start().Do(new Move("hale", new Coord(2, 1)));
        Assert.Equal(new[] { new Attack("hale", "brigand-1") }, Resolver.Legal(one, Starter).OfType<Attack>().Where(a => a.UnitId == "hale"));
    }

    /// <summary>Issue 101: an unarmed unit says so on the board and in <c>show</c>, and a spent spell is named as the cause.</summary>
    [Fact]
    public void AnUnarmedUnitSaysSoOnTheBoardAndInShow()
    {
        var mira = Recruit("mira", "chaplain", new Stats(16, 1, 4, 4, 4, 3, 1, 5, 3), "radiance", "salve");
        var state = Start(roster: ValueList<Unit>.Of(Hale, mira), map: Yard.Replace("recruit:wren", "recruit"));
        Assert.DoesNotContain("unarmed", Ironwake.Core.MapRenderer.Render(state, Starter));
        Assert.Equal("Radiance (mt 6 hit 85 crit 0 wt 4 range 1-2)", Ironwake.Cli.PlaySession.WeaponLine(state.Find("mira")!, Starter));

        var spent = state.WithUnit(state.Find("mira")! with { Unit = state.Find("mira")!.Unit.WithUses(0) });
        var board = Ironwake.Core.MapRenderer.Render(spent, Starter);
        Assert.Contains("mira ", board);
        Assert.Matches(@"mira .*  unarmed", board);
        Assert.DoesNotMatch(@"hale .*  unarmed", board);
        Assert.Equal("unarmed (spell spent)", Ironwake.Cli.PlaySession.WeaponLine(spent.Find("mira")!, Starter));

        var bare = Start(roster: ValueList<Unit>.Of(Hale, Unarmed), map: Yard.Replace("recruit:wren", "recruit"));
        Assert.Matches(@"pell .*  unarmed", Ironwake.Core.MapRenderer.Render(bare, Starter));
        Assert.Equal("unarmed", Ironwake.Cli.PlaySession.WeaponLine(bare.Find("pell")!, Starter));
    }

    /// <summary>Issue 313: the count that decides whether a line names a weapon covers only weapons that reach the distance, and two copies of one weapon count once.</summary>
    [Fact]
    public void WeaponChoicesCountDistinctWeaponsThatReachTheDistance()
    {
        var copies = Recruit("hale", new Stats(22, 8, 0, 7, 8, 6, 5, 2, 9), "iron_sword", "iron_sword", "field_dressing");
        var state = Start(roster: ValueList<Unit>.Of(TwoSwords, Wren));
        var hale = state.Find("hale")!;
        var twins = hale with { Unit = copies };

        Assert.Equal(2, hale.WeaponChoicesAt(Starter, 1));
        Assert.Equal(0, hale.WeaponChoicesAt(Starter, 2));
        Assert.Equal(1, twins.WeaponChoicesAt(Starter, 1));
    }
}
