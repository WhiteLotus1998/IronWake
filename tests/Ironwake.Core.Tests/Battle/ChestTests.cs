using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Chests (issue 649): a <c>chests:</c> block puts a chest on a tile with its contents; a player
/// unit on the tile or orthogonally beside it opens it as its action, after a Move or without one,
/// unless an enemy stands on the tile; what fits goes to its pack in file order and the rest to
/// the wagon (issue 679); the chest stays open; no Move Again follows; a Recall restores it shut and
/// the wagon as it was; the enemy never opens one; the board prints each closed chest and what is
/// in it.
/// </summary>
public class ChestTests
{
    private static readonly Coord Lid = new(2, 1);

    private const string Vault =
        """
        name: Vault
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
        P recruit:wren 0,2
        E soldier 5,3 group:yard behavior:aggressive

        chests:
        2,1 steel_sword field_dressing

        """;

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "strongbox_chests.map");

    private static BattleState Opened(BattleState state) => state.Do(new Move("hale", new Coord(1, 1))).Do(new Open("hale", Lid));

    [Fact]
    public void TheChestsBlockParsesAndRoundTrips()
    {
        var map = MapFixture.Parse(Vault, "vault.map");

        var chest = Assert.Single(map.Chests);
        Assert.Equal(new Chest(Lid, ValueList<string>.Of("steel_sword", "field_dressing")), chest);
        Assert.Equal(map, MapFixture.Parse(MapFormat.Write(map, Starter), "again.map"));
    }

    [Fact]
    public void TheSampleIsCanonicalAndHoldsOneChestPerShape()
    {
        var sample = MapFiles.Load(SamplePath, Starter);

        Assert.Equal(File.ReadAllText(SamplePath).Replace("\r\n", "\n"), MapFormat.Write(sample, Starter));
        Assert.Equal(4, sample.Chests.Count);
    }

    [Fact]
    public void TheWagonSampleIsCanonicalWithAnOverflowChestAndAHeldVault()
    {
        var path = Path.Combine(Path.GetDirectoryName(SamplePath)!, "strongbox_wagon.map");
        var sample = MapFiles.Load(path, Starter);

        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), MapFormat.Write(sample, Starter));
        Assert.Contains(sample.Chests, c => c.Items.Count == Chest.MaxItems);
        Assert.Contains(sample.Chests, c => sample.Placements.Any(p => p.At == c.At));
    }

    [Theory]
    [InlineData("2,1 steel_sword", "2,1 steel_sword\n2,1 iron_bow", "already has the chest on line")]
    [InlineData("2,1 steel_sword", "2,1", "a chest holds 1 to 5 items, got 0")]
    [InlineData("2,1 steel_sword", "2,1 iron_sword iron_sword iron_sword iron_sword iron_sword iron_sword", "a chest holds 1 to 5 items, got 6")]
    [InlineData("2,1 steel_sword", "2,1 gold_crown", "'gold_crown', which is not a weapon in weapons.json or an item in items.json")]
    [InlineData("2,1 steel_sword", "9,1 steel_sword", "outside the 6x4 grid")]
    public void AMalformedChestLineIsRefusedNamingTheProblem(string from, string to, string message)
    {
        var text = Vault.Replace("2,1 steel_sword field_dressing", from).Replace(from, to);

        var error = Assert.Throws<MapException>(() => MapFixture.Parse(text, "vault.map"));
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void ASignatureItemIsNeverInAChest()
    {
        var bound = Starter.Weapons.Values.FirstOrDefault(w => w.BoundTo is not null);
        Assert.NotNull(bound);
        var text = Vault.Replace("2,1 steel_sword field_dressing", "2,1 " + bound!.Id);

        var error = Assert.Throws<MapException>(() => MapFixture.Parse(text, "vault.map"));
        Assert.Contains("lost with its owner, never found", error.Message);
    }

    [Fact]
    public void AUnitBesideAChestOpensItAndTakesEverythingAtFullUses()
    {
        var start = Start(map: Vault);
        var before = start.Find("hale")!.Unit.Inventory.Count;

        var result = start.Do(new Move("hale", new Coord(1, 1))).Try(new Open("hale", Lid));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var opened = Assert.Single(result.Events.OfType<ChestOpened>());
        Assert.Equal(new ChestOpened("hale", Lid, ValueList<string>.Of("steel_sword", "field_dressing"), ValueList<string>.Empty), opened);
        var items = result.Next.Find("hale")!.Unit.Inventory.Items;
        Assert.Equal(before + 2, items.Count);
        Assert.Equal(new ItemStack("steel_sword", Starter.Weapon("steel_sword").Durability), items[before]);
        Assert.Equal(new ItemStack("field_dressing", Starter.Item("field_dressing").Uses), items[before + 1]);
        Assert.Equal(ValueList<Coord>.Of(Lid), result.Next.Opened);
    }

    /// <summary>
    /// The shipped content with an adept reaching ice and lightning only, and two test grimoires on
    /// Cinder's numbers: <c>test_grimoire_fire</c> (fire, Mag 8) and <c>test_grimoire_ice</c> (ice, Mag 8).
    /// </summary>
    private static readonly GameContent Tomes = Starter with
    {
        Weapons = Starter.Weapons
            .SetItem("test_grimoire_fire", Starter.Weapon("cinder") with { Id = "test_grimoire_fire", Name = "Test Fire Grimoire", MinMag = 8 })
            .SetItem("test_grimoire_ice", Starter.Weapon("cinder") with { Id = "test_grimoire_ice", Name = "Test Ice Grimoire", School = MagicSchool.Ice, Ignites = false, MinMag = 8 }),
        Classes = Starter.Classes.SetItem("adept", Starter.Class("adept") with { Schools = ValueList<MagicSchool>.Of(MagicSchool.Ice, MagicSchool.Lightning) }),
    };

    /// <summary>An adept with Hale's id and Mag <paramref name="mag"/> (unit plus class), rank E in lore, carrying nothing.</summary>
    private static Unit Ash(int mag) =>
        Recruit("hale", "adept", new Stats(18, 1, mag - Tomes.Class("adept").Modifiers.Mag, 6, 6, 4, 2, 5, 4));

    /// <summary><paramref name="opener"/> on 1,1, beside a Vault chest holding <paramref name="items"/>, opens it.</summary>
    private static ApplyResult OpenWith(Unit opener, string items)
    {
        var map = Vault.Replace("2,1 steel_sword field_dressing", "2,1 " + items).Replace("P captain 0,1", "P captain 1,1");
        var state = BattleState.From(MapFormat.Parse("vault.map", map, Tomes), Tomes, ValueList<Unit>.Of(opener, Wren), 7);
        var result = Resolver.Apply(state, Tomes, new Open("hale", Lid));
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result;
    }

    [Fact]
    public void AChestHoldingATomeAndAGrimoireHandsBothToTheOpenerAtFullUses()
    {
        var result = OpenWith(Ash(9), "cinder test_grimoire_ice");

        var opened = Assert.Single(result.Events.OfType<ChestOpened>());
        Assert.Equal(ValueList<string>.Of("cinder", "test_grimoire_ice"), opened.ItemIds);
        var items = result.Next.Find("hale")!.Unit.Inventory.Items;
        Assert.Equal(new ItemStack("cinder", Tomes.Weapon("cinder").Durability), items[^2]);
        Assert.Equal(new ItemStack("test_grimoire_ice", Tomes.Weapon("test_grimoire_ice").Durability), items[^1]);
    }

    [Fact]
    public void AnOpenerWhoWieldsEveryTomeInTheChestIsNamedShortOfNone()
    {
        var opened = Assert.Single(OpenWith(Ash(9), "test_grimoire_ice steel_sword").Events.OfType<ChestOpened>());

        Assert.Empty(opened.CannotWield);
    }

    [Theory]
    [InlineData("cinder", 9, "fire school")]
    [InlineData("test_grimoire_fire", 9, "fire school")]
    [InlineData("test_grimoire_ice", 7, "Mag 8")]
    [InlineData("bolt", 9, "rank D")]
    public void AnOpenerWhoCannotWieldATomeStillTakesItAndTheEventSaysWhy(string tome, int mag, string why)
    {
        var result = OpenWith(Ash(mag), tome);

        var opened = Assert.Single(result.Events.OfType<ChestOpened>());
        Assert.Equal(ValueList<string>.Of(tome), opened.ItemIds);
        Assert.Equal(ValueList<WieldShort>.Of(new WieldShort(tome, why)), opened.CannotWield);
        Assert.Equal(tome, result.Next.Find("hale")!.Unit.Inventory.Items[^1].ItemId);
    }

    [Fact]
    public void AnOpenerWhoseClassCastsNoLoreIsShortOfTheType()
    {
        var opened = Assert.Single(OpenWith(Hale with { Inventory = Inventory.Empty }, "test_grimoire_ice").Events.OfType<ChestOpened>());

        Assert.Equal(ValueList<WieldShort>.Of(new WieldShort("test_grimoire_ice", "lore")), opened.CannotWield);
    }

    [Fact]
    public void AWeaponTheOpenerCannotWieldIsNamedShortOnlyIfItIsATome()
    {
        var opened = Assert.Single(OpenWith(Ash(9), "steel_sword iron_bow").Events.OfType<ChestOpened>());

        Assert.Empty(opened.CannotWield);
    }

    [Fact]
    public void ATomeTwiceInAChestIsNamedShortOnce()
    {
        var opened = Assert.Single(OpenWith(Ash(9), "cinder cinder").Events.OfType<ChestOpened>());

        Assert.Equal(ValueList<string>.Of("cinder", "cinder"), opened.ItemIds);
        Assert.Equal(ValueList<WieldShort>.Of(new WieldShort("cinder", "fire school")), opened.CannotWield);
    }

    [Fact]
    public void AnUnknownChestItemIsRefusedNamingTheFileTheChestAndTheItem()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Vault.Replace("2,1 steel_sword field_dressing", "2,1 cinder ember_grimoire"), "vault.map"));

        Assert.Contains("vault.map", error.Message);
        Assert.Contains("2,1", error.Message);
        Assert.Contains("'ember_grimoire'", error.Message);
    }

    [Fact]
    public void AUnitOnTheChestsTileOpensIt()
    {
        var state = Start(map: Vault).Do(new Move("hale", Lid));

        Assert.True(state.Try(new Open("hale", Lid)).Accepted);
    }

    [Fact]
    public void AUnitOpensWithoutMovingFirst()
    {
        var state = Start(map: Vault.Replace("P captain 0,1", "P captain 1,1"));

        Assert.True(state.Try(new Open("hale", Lid)).Accepted);
    }

    [Fact]
    public void OpeningIsTheUnitsActionAndNoMoveAgainFollows()
    {
        var hale = Opened(Start(map: Vault)).Find("hale")!;

        Assert.True(hale.Moved);
        Assert.True(hale.Acted);
        Assert.Null(hale.MoveAgain);
    }

    [Fact]
    public void AChestOutOfReachIsRefused()
    {
        var state = Start(map: Vault).Do(new Move("hale", new Coord(1, 2)));

        var refusal = state.Refused(new Open("hale", Lid));
        Assert.Equal(RejectionReason.CannotOpen, refusal.Reason);
        Assert.Contains("open it from its tile or one beside it", refusal.Message);
    }

    [Fact]
    public void ATileWithNoChestIsRefused()
    {
        var refusal = Start(map: Vault).Refused(new Open("hale", new Coord(1, 1)));

        Assert.Equal(RejectionReason.CannotOpen, refusal.Reason);
        Assert.Contains("there is none", refusal.Message);
    }

    [Fact]
    public void AnOpenChestStaysOpen()
    {
        var state = Opened(Start(map: Vault)).Do(new Move("wren", new Coord(2, 2)));

        var refusal = state.Refused(new Open("wren", Lid));
        Assert.Equal(RejectionReason.CannotOpen, refusal.Reason);
        Assert.Contains("already open", refusal.Message);
    }

    /// <summary>Hale with <paramref name="extra"/> dressings added to his starting pack.</summary>
    private static Unit HaleWith(int extra) =>
        Hale with { Inventory = Enumerable.Range(0, extra).Aggregate(Hale.Inventory, (inv, _) => inv.Add(new ItemStack("field_dressing", 3))) };

    [Fact]
    public void AChestAlwaysOpensAndWhatDoesNotFitGoesToTheWagonInFileOrder()
    {
        var full = HaleWith(Inventory.Capacity - Hale.Inventory.Count - 1);
        var three = Vault.Replace("2,1 steel_sword field_dressing", "2,1 steel_sword iron_bow field_dressing");
        var state = Start(map: three, roster: ValueList<Unit>.Of(full, Wren)).Do(new Move("hale", new Coord(1, 1)));

        var result = state.Try(new Open("hale", Lid));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(new ChestOpened("hale", Lid, ValueList<string>.Of("steel_sword"), ValueList<string>.Of("iron_bow", "field_dressing")), Assert.Single(result.Events.OfType<ChestOpened>()));
        Assert.Equal(Inventory.Capacity, result.Next.Find("hale")!.Unit.Inventory.Count);
        Assert.Equal("steel_sword", result.Next.Find("hale")!.Unit.Inventory.Items[^1].ItemId);
        Assert.Equal(ValueList<string>.Of("iron_bow", "field_dressing"), result.Next.Wagon);
        Assert.Equal(ValueList<Coord>.Of(Lid), result.Next.Opened);
    }

    [Fact]
    public void AFullPackOpenerSendsEverythingToTheWagon()
    {
        var full = HaleWith(Inventory.Capacity - Hale.Inventory.Count);
        var state = Start(map: Vault, roster: ValueList<Unit>.Of(full, Wren)).Do(new Move("hale", new Coord(1, 1)));

        var next = state.Do(new Open("hale", Lid));

        Assert.Equal(full.Inventory, next.Find("hale")!.Unit.Inventory);
        Assert.Equal(ValueList<string>.Of("steel_sword", "field_dressing"), next.Wagon);
        Assert.Contains("wagon steel_sword field_dressing", next.Canonical());
    }

    [Fact]
    public void AnEmptyWagonLeavesTheCanonicalStateAsItWas()
    {
        Assert.DoesNotContain("wagon", Opened(Start(map: Vault)).Canonical());
    }

    [Fact]
    public void AChestWithAnEnemyOnItsTileIsShutUntilTheEnemyLeavesIt()
    {
        var twoEnemies = Vault.Replace("E soldier 5,3 group:yard behavior:aggressive", "E soldier 2,1 group:yard behavior:aggressive\nE soldier 5,3 group:yard behavior:aggressive");
        var guarded = Start(map: twoEnemies).Do(new Move("hale", new Coord(1, 1)));
        var guard = guarded.UnitAt(Lid)!;

        var refusal = guarded.Refused(new Open("hale", Lid));
        Assert.Equal(RejectionReason.CannotOpen, refusal.Reason);
        Assert.Contains($"cannot open the chest at 2,1: {guard.Id} stands on it", refusal.Message);
        Assert.DoesNotContain(Resolver.Legal(guarded, Starter), c => c is Open);

        var gone = guarded with { Units = ValueList<BattleUnit>.From(guarded.Units.Where(u => u.Id != guard.Id)) };
        Assert.True(gone.Try(new Open("hale", Lid)).Accepted);
        var movedOff = guarded.WithUnit(guard with { At = new Coord(3, 1) });
        Assert.True(movedOff.Try(new Open("hale", Lid)).Accepted);
    }

    [Fact]
    public void AnEnemyBesideTheChestDoesNotShutIt()
    {
        var state = Start(map: Vault.Replace("E soldier 5,3", "E soldier 3,1")).Do(new Move("hale", new Coord(1, 1)));

        Assert.True(state.Try(new Open("hale", Lid)).Accepted);
    }

    [Fact]
    public void AUnitThatHasActedCannotOpen()
    {
        var state = Start(map: Vault).Do(new Move("hale", new Coord(1, 1))).Do(new Wait("hale"));

        Assert.Equal(RejectionReason.AlreadyActed, state.Refused(new Open("hale", Lid)).Reason);
    }

    [Fact]
    public void TheEnemyNeverOpensAChest()
    {
        var enemyPhase = Start(map: Vault.Replace("E soldier 5,3", "E soldier 3,1")) with { Phase = Side.Enemy };

        var refusal = enemyPhase.Refused(new Open("soldier-1", Lid));
        Assert.Equal(RejectionReason.CannotOpen, refusal.Reason);
        Assert.Contains("only player units open chests", refusal.Message);
        Assert.DoesNotContain(Resolver.Legal(enemyPhase, Starter), c => c is Open);
    }

    [Fact]
    public void ARecallRestoresTheChestShutAndThePackAsItWas()
    {
        var start = Start(map: Vault, roster: ValueList<Unit>.Of(HaleWith(Inventory.Capacity - Hale.Inventory.Count - 1), Wren));
        var opened = Opened(start);
        Assert.NotEmpty(opened.Wagon);

        var back = opened.Do(new Recall(0));

        Assert.Empty(back.Opened);
        Assert.Empty(back.Wagon);
        Assert.Equal(start.Find("hale")!.Unit.Inventory, back.Find("hale")!.Unit.Inventory);
        Assert.Contains(back.ClosedChests, c => c.At == Lid);
    }

    [Fact]
    public void LegalListsAnOpenOnlyWhereTheResolverTakesIt()
    {
        var beside = Start(map: Vault).Do(new Move("hale", new Coord(1, 1)));
        var away = Start(map: Vault);

        Assert.Contains(new Open("hale", Lid), Resolver.Legal(beside, Starter));
        Assert.DoesNotContain(Resolver.Legal(away, Starter), c => c is Open);
        Assert.DoesNotContain(Resolver.Legal(Opened(away), Starter), c => c is Open);
    }

    [Fact]
    public void TheBoardDrawsAClosedChestAndListsWhatIsInIt()
    {
        var start = Start(map: Vault);

        var board = MapRenderer.Render(start, Starter);

        Assert.Contains("chests ($): 2,1 Steel Sword, Field Dressing (" + MapRenderer.ChestRule + ")", board);
        Assert.Contains(" 1 A.$...", board);
        var after = MapRenderer.Render(Opened(start), Starter);
        Assert.DoesNotContain("chests ($)", after);
    }

    [Fact]
    public void TheMapViewBeforeBattleListsTheChests()
    {
        var board = MapRenderer.Render(MapFixture.Parse(Vault, "vault.map"), Starter);

        Assert.Contains("chests ($): 2,1 Steel Sword, Field Dressing", board);
    }

    [Fact]
    public void TheProtocolCarriesEachChestAndWhetherItIsOpen()
    {
        var opened = Opened(Start(map: Vault));

        var json = ProtocolJson.State(opened, Starter);

        Assert.Contains("\"chests\":[{\"at\":{\"x\":2,\"y\":1},\"items\":[\"steel_sword\",\"field_dressing\"],\"open\":true}]", json);
        Assert.Equal(opened.Opened, ProtocolJson.ReadState(json, Starter).Opened);
        Assert.DoesNotContain("\"wagon\"", json);
    }

    [Fact]
    public void TheProtocolCarriesTheWagonAndReadsItBack()
    {
        var full = HaleWith(Inventory.Capacity - Hale.Inventory.Count);
        var opened = Opened(Start(map: Vault, roster: ValueList<Unit>.Of(full, Wren)));

        var json = ProtocolJson.State(opened, Starter);

        Assert.Contains("\"wagon\":[\"steel_sword\",\"field_dressing\"]", json);
        Assert.Equal(opened.Wagon, ProtocolJson.ReadState(json, Starter).Wagon);
        Assert.Equal(opened.Canonical(), ProtocolJson.ReadState(json, Starter).Canonical());
    }
}
