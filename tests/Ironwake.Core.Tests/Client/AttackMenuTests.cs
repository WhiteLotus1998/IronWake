using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The attack menu and the first combat arts (issue 611): the six shipped arts and who knows
/// them, <see cref="Queries.AttackOptions"/>'s rows and each refusal it greys, and the client's
/// menu, which opens only when there is a choice, strikes with the row chosen, and backs out.
/// Played on the Tollgate with the party moved beside its woods.
/// </summary>
public class AttackMenuTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Coord Brigand = new(6, 5);

    private static BattleState Tollgate(GameContent? content = null)
    {
        content ??= Shipped;
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), content);
        return BattleState.From(map, content, content.Cast, 611);
    }

    /// <summary>The Tollgate with <paramref name="id"/> standing on <paramref name="at"/>.</summary>
    private static BattleState Placed(string id, Coord at, BattleState? state = null)
    {
        state ??= Tollgate();
        return state.WithUnit(state.Find(id)! with { At = at });
    }

    private static BattleUnit EnemyAt(BattleState state, Coord at) => state.Units.Single(u => u.At == at);

    private static BattleState WithUses(BattleState state, string id, int uses)
    {
        var unit = state.Find(id)!;
        var slot = unit.EquippedSlot(Shipped);
        var stack = unit.Unit.Inventory.Items[slot];
        return state.WithUnit(unit with { Unit = unit.Unit with { Inventory = unit.Unit.Inventory.Replace(slot, stack with { Uses = uses }) } });
    }

    [Theory]
    [InlineData("feint", WeaponType.Sword, 1, -2, 25, 0, 0, 0)]
    [InlineData("heavy_cut", WeaponType.Sword, 2, 4, -10, 0, 3, 0)]
    [InlineData("long_thrust", WeaponType.Lance, 2, -3, 0, 0, 0, 1)]
    [InlineData("cleave", WeaponType.Axe, 3, 6, -20, 10, 0, 0)]
    [InlineData("aimed_shot", WeaponType.Bow, 2, 0, 20, 10, 2, 0)]
    [InlineData("overcast", WeaponType.Reason, 2, 5, 0, 0, 4, 0)]
    public void TheShippedArtsCarryTheIssuesNumbers(string id, WeaponType weapon, int cost, int mt, int hit, int crit, int wt, int range)
    {
        Assert.Equal(new CombatArtEffect(weapon, WeaponRank.E, cost, mt, hit, crit, wt, range), Shipped.Ability(id).Effect);
    }

    [Theory]
    [InlineData("captain", "feint,full_measure")]
    [InlineData("wren", "heavy_cut")]
    [InlineData("teodor", "long_thrust")]
    [InlineData("ottilie", "aimed_shot")]
    [InlineData("pell", "overcast,read_ahead")]
    [InlineData("keziah", "cleave")]
    public void EachArtIsKnownAtTheStartByOneOfTheCast(string unit, string art)
    {
        Assert.Equal(art.Split(','), Shipped.ArtsOf(Shipped.Cast.Single(u => u.Id == unit)).Select(a => a.Ability.Id));
    }

    [Fact]
    public void TheMenuListsThePlainAttackPerWeaponThenEachArtUnderEachWeaponOfItsType()
    {
        var state = Placed("pell", new Coord(6, 6));

        var rows = Queries.AttackOptions(state, Shipped, state.Find("pell")!, EnemyAt(state, Brigand));

        Assert.Equal(new (string?, string?, int?)[] { ("cinder", null, null), ("gust", null, 1), ("cinder", "overcast", null), ("gust", "overcast", 1) },
            rows.Select(r => (r.WeaponId, r.Art?.Id, r.Command.Slot)));
        Assert.All(rows, r => Assert.True(r.Legal));
    }

    [Fact]
    public void ASignatureArtIsListedOnlyUnderItsOwnItemAndNotAtAllWithoutIt()
    {
        var state = Placed("pell", new Coord(6, 6));
        var pell = state.Find("pell")!;
        var pack = pell.Unit.Inventory.Items.Add(new ItemStack("pell_commonplace", Shipped.Weapon("pell_commonplace").Durability));
        var armed = state.WithUnit(pell with { Unit = pell.Unit with { Inventory = new Inventory(pack) } });

        var rows = Queries.AttackOptions(armed, Shipped, armed.Find("pell")!, EnemyAt(armed, Brigand));

        Assert.Equal(new[] { "pell_commonplace" }, rows.Where(r => r.Art?.Id == "read_ahead").Select(r => r.WeaponId));
        Assert.DoesNotContain(Queries.AttackOptions(state, Shipped, pell, EnemyAt(state, Brigand)), r => r.Art?.Id == "read_ahead");
    }

    [Fact]
    public void ALegalRowCarriesTheForecastOfItsOwnAttack()
    {
        var state = Placed("captain", new Coord(6, 6));
        var captain = state.Find("captain")!;
        var brigand = EnemyAt(state, Brigand);

        var feint = Queries.AttackOptions(state, Shipped, captain, brigand).Single(r => r.Art?.Id == "feint");

        Assert.Equal(Queries.Forecast(state, Shipped, captain, brigand, null, "feint"), feint.Forecast);
        Assert.Null(feint.Refusal);
    }

    [Fact]
    public void LongThrustReachesTwoTilesWhereThePlainLanceIsOutOfReach()
    {
        var state = Placed("teodor", new Coord(6, 7));

        var rows = Queries.AttackOptions(state, Shipped, state.Find("teodor")!, EnemyAt(state, Brigand));

        Assert.Equal(RejectionReason.OutOfRange, rows.Single(r => r.Art is null).Refusal!.Reason);
        Assert.True(rows.Single(r => r.Art?.Id == "long_thrust").Legal);
    }

    [Fact]
    public void HeavyCutCostsWrenHerDoubleOnTheBrigand()
    {
        var state = Placed("wren", new Coord(6, 6));
        var rows = Queries.AttackOptions(state, Shipped, state.Find("wren")!, EnemyAt(state, Brigand));

        Assert.True(rows.Single(r => r.Art is null).Forecast!.Attacker.Doubles);
        Assert.False(rows.Single(r => r.Art?.Id == "heavy_cut").Forecast!.Attacker.Doubles);
    }

    [Fact]
    public void AnArtTheUnitCarriesNoWeaponOfItsTypeForIsGreyedWithTheTypeRefusal()
    {
        var state = Placed("captain", new Coord(6, 6));
        var captain = state.Find("captain")!;
        state = state.WithUnit(captain with { Unit = captain.Unit with { Abilities = ValueList<string>.From(new[] { "long_thrust" }) } });

        var row = Queries.AttackOptions(state, Shipped, state.Find("captain")!, EnemyAt(state, Brigand)).Single(r => r.Art is not null);

        Assert.Equal(RejectionReason.ArtRefused, row.Refusal!.Reason);
        Assert.Contains("Long Thrust is a lance technique and Iron Sword is a sword", row.Refusal.Message);
        Assert.Null(row.Forecast);
    }

    [Fact]
    public void AnArtAboveTheUnitsRankIsGreyedWithTheRankRefusal()
    {
        var feint = Shipped.Ability("feint");
        var content = Shipped with { Abilities = Shipped.Abilities.SetItem("feint", feint with { Effect = (CombatArtEffect)feint.Effect with { Rank = WeaponRank.D } }) };
        var state = Placed("captain", new Coord(6, 6), Tollgate(content));

        var row = Queries.AttackOptions(state, content, state.Find("captain")!, EnemyAt(state, Brigand)).Single(r => r.Art?.Id == "feint");

        Assert.Contains("rank E in sword, and Feint needs D", row.Refusal!.Message);
    }

    [Fact]
    public void AnArtTheWeaponsUsesCannotPayIsGreyedWithTheUsesRefusal()
    {
        var state = WithUses(Placed("captain", new Coord(6, 6)), "captain", 1);

        var row = Queries.AttackOptions(state, Shipped, state.Find("captain")!, EnemyAt(state, Brigand)).Single(r => r.Art?.Id == "feint");

        Assert.Contains("Feint costs 2 uses with the strike and Iron Sword has 1 left", row.Refusal!.Message);
    }

    [Fact]
    public void AnArtOnABrokenWeaponIsGreyedWithTheBrokenRefusal()
    {
        var state = WithUses(Placed("captain", new Coord(6, 6)), "captain", 0);

        var rows = Queries.AttackOptions(state, Shipped, state.Find("captain")!, EnemyAt(state, Brigand));

        Assert.True(rows.Single(r => r.Art is null).Legal);
        Assert.Contains("Iron Sword is broken and cannot pay for a technique", rows.Single(r => r.Art?.Id == "feint").Refusal!.Message);
    }

    private static ClientSession Selected(BattleState state, string id)
    {
        var client = new ClientSession(Shipped, state);
        client.Click(state.Find(id)!.At);
        return client;
    }

    [Fact]
    public void AClickOnAnEnemyWithTwoLegalRowsOpensTheMenuAndStrikesNothing()
    {
        var client = Selected(Placed("captain", new Coord(6, 6)), "captain");

        Assert.Null(client.Click(Brigand));

        var menu = client.Menu!;
        Assert.Equal(new[] { "Iron Sword", "Feint (Iron Sword)", "Full Measure (Iron Sword)" }, menu.Rows.Select(r => r.Label));
        Assert.Empty(client.Log);
        Assert.Equal("captain", client.Selected);
    }

    [Fact]
    public void ChoosingAnArtRowStrikesWithItAndLogsTheConsolesDeclareLine()
    {
        var client = Selected(Placed("captain", new Coord(6, 6)), "captain");
        client.Click(Brigand);

        var command = client.Choose(1);

        Assert.Equal(new Attack("captain", EnemyAt(client.State.History[^1], Brigand).Id, null, "feint"), command);
        Assert.Equal("Alder Fenn declares Feint with Iron Sword, spending 1 extra use", client.Log[0]);
        Assert.Null(client.Menu);
    }

    [Fact]
    public void HoveringARowShowsItsCardWithTheArtsNumbers()
    {
        var client = Selected(Placed("captain", new Coord(6, 6)), "captain");
        client.Click(Brigand);
        var plain = client.Menu!.Card!;

        client.MenuHover(1);

        var feint = client.Menu!.Card!;
        Assert.Equal("Feint (Iron Sword)", feint.Attacker.Weapon);
        Assert.Equal("Iron Sword", plain.Attacker.Weapon);
        Assert.True(feint.Attacker.Strike.DisplayedHit > plain.Attacker.Strike.DisplayedHit);
    }

    [Fact]
    public void AGreyedRowLeavesTheMenuOpenWithItsRefusal()
    {
        var client = Selected(WithUses(Placed("pell", new Coord(5, 6)), "pell", 2), "pell");
        client.Click(Brigand);
        var greyed = client.Menu!.Rows.ToList().FindIndex(r => !r.Legal);

        Assert.Equal("Overcast (Cinder)", client.Menu.Rows[greyed].Label);
        Assert.Null(client.Choose(greyed));
        Assert.NotNull(client.Menu);
        Assert.Contains("Overcast costs 3 uses with the strike and Cinder has 2 left", client.Status);
        Assert.Empty(client.Log);
    }

    [Fact]
    public void ClosingTheMenuStrikesNothingAndKeepsTheSelection()
    {
        var client = Selected(Placed("captain", new Coord(6, 6)), "captain");
        client.Click(Brigand);

        client.CloseMenu();

        Assert.Null(client.Menu);
        Assert.Equal("captain", client.Selected);
        Assert.Empty(client.Log);
    }

    [Fact]
    public void AUnitWithOneLegalRowStrikesOnClickAsBefore()
    {
        var client = Selected(WithUses(Placed("captain", new Coord(6, 6)), "captain", 1), "captain");

        var command = client.Click(Brigand);

        Assert.Equal(new Attack("captain", EnemyAt(client.State.History[^1], Brigand).Id), command);
        Assert.Null(client.Menu);
    }
}
