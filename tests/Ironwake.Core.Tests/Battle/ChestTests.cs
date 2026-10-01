using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Chests (issue 649): a <c>chests:</c> block puts a chest on a tile with its contents; a player
/// unit on the tile or orthogonally beside it opens it as its action, after a Move or without one,
/// and takes everything into its pack, or the chest stays shut; the chest stays open; no Canto
/// follows; a Recall restores it shut; the enemy never opens one; the board prints each closed
/// chest and what is in it.
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
        Assert.Equal(new ChestOpened("hale", Lid, ValueList<string>.Of("steel_sword", "field_dressing")), opened);
        var items = result.Next.Find("hale")!.Unit.Inventory.Items;
        Assert.Equal(before + 2, items.Count);
        Assert.Equal(new ItemStack("steel_sword", Starter.Weapon("steel_sword").Durability), items[before]);
        Assert.Equal(new ItemStack("field_dressing", Starter.Item("field_dressing").Uses), items[before + 1]);
        Assert.Equal(ValueList<Coord>.Of(Lid), result.Next.Opened);
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
    public void OpeningIsTheUnitsActionAndNoCantoFollows()
    {
        var hale = Opened(Start(map: Vault)).Find("hale")!;

        Assert.True(hale.Moved);
        Assert.True(hale.Acted);
        Assert.Null(hale.Canto);
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

    [Fact]
    public void AChestThatWillNotFitInThePackStaysShut()
    {
        var full = Hale with { Inventory = Hale.Inventory.Add(new ItemStack("field_dressing", 3)).Add(new ItemStack("field_dressing", 3)).Add(new ItemStack("field_dressing", 3)) };
        var state = Start(map: Vault, roster: ValueList<Unit>.Of(full, Wren)).Do(new Move("hale", new Coord(1, 1)));

        var refusal = state.Refused(new Open("hale", Lid));
        Assert.Equal(RejectionReason.CannotOpen, refusal.Reason);
        Assert.Contains("it holds 2 and there is room for 1", refusal.Message);
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
        var start = Start(map: Vault);
        var opened = Opened(start);

        var back = opened.Do(new Recall(0));

        Assert.Empty(back.Opened);
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
    }
}
