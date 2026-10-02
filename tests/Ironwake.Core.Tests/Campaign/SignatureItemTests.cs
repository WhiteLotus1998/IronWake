using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Signature items (issue 635, slice 2; DESIGN section 14): a weapon bound to one cast member,
/// never sold, paid by that member's quest 2 and lost with them; a signature art is declared
/// only with its own item. No signature item ships yet (the story gate, #656), so these tests
/// add placeholder entries to the shipped content: Wren's Test Vow and the art Test Oath.
/// </summary>
public class SignatureItemTests
{
    private static readonly GameContent Shipped = MapFixture.Content;

    private static readonly Weapon Vow = Shipped.Weapon("iron_sword") with { Id = "test_vow", Name = "Test Vow", Price = null, BoundTo = "wren" };

    private static readonly Ability Oath = new("test_oath", "Test Oath", "Placeholder signature art.", new CombatArtEffect(WeaponType.Sword, WeaponRank.E, 1, 2, 0, 0, 0, 0) { Item = "test_vow" });

    private static readonly CampaignQuest Second = new("wren_2", "wren", 2, "the_lazar_house") { Pays = "test_vow", Rare = 3 };

    private static readonly GameContent Content = Shipped with
    {
        Weapons = Shipped.Weapons.Add(Vow.Id, Vow),
        Abilities = Shipped.Abilities.Add(Oath.Id, Oath),
        Campaign = Shipped.Campaign with
        {
            Quests = ValueList<CampaignQuest>.Of(new CampaignQuest("wren_1", "wren", 1, "the_lazar_house"), Second),
        },
    };

    private static MapDefinition Board() =>
        MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), MapFiles.QuestsDirectory, "the_lazar_house.map"), Content);

    /// <summary>A record at the interlude where Wren's quest 2 is open: quest 1 won two maps back.</summary>
    private static CampaignRecord Open()
    {
        var record = CampaignRecord.StartAt(Content, 701, "ironwake_raid");
        return record with { QuestsWon = ValueList<QuestWon>.Of(new QuestWon("wren_1", record.MapIndex - 2)) };
    }

    /// <summary>Wren's quest 2 with Maud beside her, decided with every enemy gone and the turn limit passed, keeping the player units <paramref name="keep"/> keeps.</summary>
    private static BattleState Decided(CampaignRecord record, Func<BattleUnit, bool>? keep = null)
    {
        var opening = record.BeginQuest(Board(), "wren_2", "maud", Content);
        var units = opening.UnitsOf(Side.Player).Where(u => keep is null || keep(u));
        return opening with { Units = ValueList<BattleUnit>.From(units), Turn = opening.Map.TurnLimit + 1, History = ValueList<BattleState>.Of(opening) };
    }

    private static CampaignRecord WithPack(CampaignRecord record, string unitId, params string[] items)
    {
        var unit = record.Find(unitId)!;
        var pack = new Inventory(ValueList<ItemStack>.From(items.Select(id => new ItemStack(id, Content.Weapons.TryGetValue(id, out var w) ? w.Durability : Content.Item(id).Uses))));
        return record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == unitId ? unit with { Inventory = pack } : u)) };
    }

    [Fact]
    public void AWonQuestTwoPutsItsSignatureItemInTheMembersPackAtFullUses()
    {
        var record = Open();
        Assert.Contains(Second, record.QuestsOffered(Content));

        var after = record.AfterQuest(Decided(record), "wren_2", Content);

        Assert.True(after.Accepted);
        Assert.Equal("wren wins wren_2; wren receives Test Vow; the stores take 3 frozen iron; nobody fell", after.Text);
        Assert.Equal(new ItemStack("test_vow", Vow.Durability), after.Record.Find("wren")!.Inventory.Items[^1]);
        Assert.DoesNotContain(after.Record.Find("maud")!.Inventory.Items, s => s.ItemId == "test_vow");
    }

    [Fact]
    public void ALostQuestTwoPaysNothing()
    {
        var record = Open();
        var lost = Decided(record, u => u.Id != "wren");

        var after = record.AfterQuest(lost, "wren_2", Content);

        Assert.DoesNotContain("receives", after.Text);
        Assert.Null(after.Record.Find("wren"));
        Assert.Contains("wren", after.Record.Fallen);
    }

    [Fact]
    public void AQuestThatPaysIsRefusedWhileTheMembersPackIsFull()
    {
        var full = WithPack(Open(), "wren", "iron_sword", "iron_sword", "iron_sword", "iron_sword", "field_dressing");

        Assert.Equal("wren carries 5 items and wren_2 pays Test Vow; drop one first", full.QuestRefusal("wren_2", "maud", Content));

        var dropped = full.Drop("wren", 4, Content);
        Assert.True(dropped.Accepted);
        Assert.Equal("wren drops Field Dressing", dropped.Text);
        Assert.Null(dropped.Record.QuestRefusal("wren_2", "maud", Content));
    }

    [Fact]
    public void DropRefusesASignatureItemAnEmptySlotAndAStranger()
    {
        var record = WithPack(Open(), "wren", "test_vow");

        Assert.Equal("Test Vow is bound to wren and is never dropped", record.Drop("wren", 0, Content).Text);
        Assert.False(record.Drop("wren", 0, Content).Accepted);
        Assert.Equal("wren has no item in slot 2", record.Drop("wren", 1, Content).Text);
        Assert.Equal("no unit 'nobody' on the roster", record.Drop("nobody", 0, Content).Text);
    }

    [Fact]
    public void ASignatureArtIsDeclaredOnlyWithItsOwnItem()
    {
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), Content);
        var wren = Content.Cast.Single(u => u.Id == "wren");
        var knowing = wren with { Abilities = wren.Abilities.Add("test_oath") };
        var cast = ValueList<Unit>.From(Content.Cast.Select(u => u.Id == "wren" ? knowing : u));
        var state = BattleState.From(map, Content, cast, 635);
        var brigand = state.Units.Single(u => u.At == new Coord(6, 5));
        state = state.WithUnit(state.Find("wren")! with { At = new Coord(6, 6) });

        var refused = Resolver.Apply(state, Content, new Attack("wren", brigand.Id, null, "test_oath"));

        Assert.False(refused.Accepted);
        Assert.Equal(RejectionReason.ArtRefused, refused.Rejection!.Reason);
        Assert.EndsWith("Test Oath is declared only with Test Vow", refused.Rejection.Message);

        var armed = state.WithUnit(state.Find("wren")! with { Unit = knowing with { Inventory = new Inventory(ValueList<ItemStack>.Of(new ItemStack("test_vow", Vow.Durability))) } });
        Assert.True(Resolver.Apply(armed, Content, new Attack("wren", brigand.Id, null, "test_oath")).Accepted);
    }

    [Fact]
    public void ASignatureItemIsNeverLeftAsAKeepsake()
    {
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), Content);
        var state = BattleState.From(map, Content, Content.Cast, 635);
        var pack = new Inventory(ValueList<ItemStack>.Of(new ItemStack("test_vow", Vow.Durability), new ItemStack("iron_lance", 30)));
        var wren = state.Find("wren")!;
        var fallen = wren with { Unit = wren.Unit with { Inventory = pack } };

        Assert.Equal("iron_lance", Keepsake.Of(fallen, Content)!.Item.ItemId);
        Assert.DoesNotContain(Keepsake.Dropped(fallen, Content), k => k.Item.ItemId == "test_vow");

        var bare = fallen with { Unit = fallen.Unit with { Inventory = new Inventory(ValueList<ItemStack>.Of(new ItemStack("test_vow", Vow.Durability))) } };
        Assert.Null(Keepsake.Of(bare, Content));
        Assert.Empty(Keepsake.Dropped(bare, Content));
    }

    [Fact]
    public void SignatureItemsArtsAndPayoutsRoundTripThroughTheSerializer()
    {
        var reloaded = ContentLoader.Parse(ContentSerializer.Write(Content));

        Assert.Equal("wren", reloaded.Weapon("test_vow").BoundTo);
        Assert.Equal("test_vow", ((CombatArtEffect)reloaded.Ability("test_oath").Effect).Item);
        Assert.Equal("test_vow", reloaded.Campaign.Quest("wren_2")!.Pays);
    }

    public static TheoryData<string, string, string, string, string> Malformed => new()
    {
        { "bound to nobody", ContentFiles.WeaponsName, "test_vow", "boundTo", "'nobody' is not in the cast" },
        { "priced", ContentFiles.WeaponsName, "test_vow", "price", "never sold" },
        { "art on a missing item", ContentFiles.AbilitiesName, "test_oath", "effect.item", "'no_such' must be a sword in weapons.json" },
        { "art on another type", ContentFiles.AbilitiesName, "test_oath", "effect.item", "'iron_lance' must be a sword in weapons.json" },
        { "quest 1 pays", ContentFiles.CampaignName, "wren_1", "pays", "only a quest 2 pays the signature item" },
        { "pays another's item", ContentFiles.CampaignName, "maud_2", "pays", "'test_vow' must be a weapon bound to 'maud'" },
    };

    [Theory]
    [MemberData(nameof(Malformed))]
    public void TheLoaderRefusesAMalformedSignature(string change, string file, string entry, string field, string problem)
    {
        var content = change switch
        {
            "bound to nobody" => Content with { Weapons = Content.Weapons.SetItem("test_vow", Vow with { BoundTo = "nobody" }) },
            "priced" => Content with { Weapons = Content.Weapons.SetItem("test_vow", Vow with { Price = 900 }) },
            "art on a missing item" => Content with { Abilities = Content.Abilities.SetItem("test_oath", Oath with { Effect = ((CombatArtEffect)Oath.Effect) with { Item = "no_such" } }) },
            "art on another type" => Content with { Abilities = Content.Abilities.SetItem("test_oath", Oath with { Effect = ((CombatArtEffect)Oath.Effect) with { Item = "iron_lance" } }) },
            "quest 1 pays" => Content with { Campaign = Content.Campaign with { Quests = ValueList<CampaignQuest>.Of(new CampaignQuest("wren_1", "wren", 1, "m") { Pays = "test_vow" }) } },
            _ => Content with
            {
                Campaign = Content.Campaign with
                {
                    Quests = ValueList<CampaignQuest>.Of(new CampaignQuest("maud_1", "maud", 1, "m"), new CampaignQuest("maud_2", "maud", 2, "n") { Pays = "test_vow" }),
                },
            },
        };

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(ContentSerializer.Write(content)));

        Assert.Equal((file, entry, field), (e.File, e.Entry, e.Field));
        Assert.Contains(problem, e.Message);
    }
}
