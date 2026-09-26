using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// <c>campaign.json</c> and <c>price</c> in content (issue 74): what the loader refuses, naming
/// file, entry and field; the serializer round trip; and the campaign record through the protocol.
/// </summary>
public class CampaignContentTests
{
    private const string PricedWeapons = """
        { "weapons": [
          { "id": "iron_sword", "name": "Iron Sword", "type": "sword", "mt": 5, "hit": 90, "crit": 0, "wt": 5, "minRange": 1, "maxRange": 1, "durability": 40, "rank": "E", "price": 400 },
          { "id": "toll_sword", "name": "Toll Sword", "type": "sword", "mt": 5, "hit": 90, "crit": 0, "wt": 5, "minRange": 1, "maxRange": 1, "durability": 20, "rank": "E" }
        ] }
        """;

    private static ContentFiles With(string campaign, string? weapons = null) =>
        Fixture.Files(weapons: weapons ?? PricedWeapons) with { Campaign = new ContentFile(ContentFiles.CampaignName, campaign) };

    private static string Campaign(string maps, int purse = 500, int seal = 500) =>
        $$"""{ "startingPurse": {{purse}}, "certificationPrice": {{seal}}, "maps": [ {{maps}} ] }""";

    private static ContentException Fails(ContentFiles files) => Assert.Throws<ContentException>(() => ContentLoader.Parse(files));

    [Fact]
    public void ACampaignFileLoadsItsPurseSealAndMapsInOrder()
    {
        var content = ContentLoader.Parse(With(Campaign("""{ "map": "one", "reward": 100, "stock": ["iron_sword"] }, { "map": "two", "reward": 0, "stock": [] }""")));

        Assert.Equal(500, content.Campaign.StartingPurse);
        Assert.Equal(new[] { "one", "two" }, content.Campaign.Maps.Select(m => m.MapId));
        Assert.Equal(ValueList<string>.Of("iron_sword"), content.Campaign.Maps[0].Stock);
        Assert.Equal(400, content.Weapon("iron_sword").Price);
        Assert.Null(content.Weapon("toll_sword").Price);
    }

    [Fact]
    public void ContentWithoutACampaignFileHasNoCampaign()
    {
        Assert.Equal(CampaignRules.None, ContentLoader.Parse(Fixture.Files()).Campaign);
    }

    [Fact]
    public void AStockEntryWithoutAPriceIsRefusedNamingTheMapAndField()
    {
        var e = Fails(With(Campaign("""{ "map": "one", "reward": 100, "stock": ["toll_sword"] }""")));

        Assert.Equal((ContentFiles.CampaignName, "one", "stock"), (e.File, e.Entry, e.Field));
        Assert.Contains("'toll_sword' has no price, so no shop can sell it", e.Message);
    }

    [Fact]
    public void AStockEntryThatIsNeitherWeaponNorItemIsRefused()
    {
        var e = Fails(With(Campaign("""{ "map": "one", "reward": 100, "stock": ["mystery"] }""")));

        Assert.Contains("'mystery' is neither a weapon nor an item", e.Message);
    }

    [Fact]
    public void AMapListedTwiceANegativeRewardAndAnEmptyListAreRefused()
    {
        var twice = Fails(With(Campaign("""{ "map": "one", "reward": 1, "stock": [] }, { "map": "one", "reward": 1, "stock": [] }""")));
        var negative = Fails(With(Campaign("""{ "map": "one", "reward": -1, "stock": [] }""")));
        var empty = Fails(With(Campaign("")));
        var purse = Fails(With(Campaign("""{ "map": "one", "reward": 1, "stock": [] }""", purse: -5)));

        Assert.Equal(("one", "map"), (twice.Entry, twice.Field));
        Assert.Equal(("one", "reward"), (negative.Entry, negative.Field));
        Assert.Equal("maps", empty.Field);
        Assert.Equal("startingPurse", purse.Field);
    }

    [Fact]
    public void APriceBelowOneIsRefused()
    {
        var e = Fails(Fixture.Files(items: """{ "items": [ { "id": "field_dressing", "name": "Field Dressing", "heals": 10, "uses": 3, "price": 0 } ] }"""));

        Assert.Equal((ContentFiles.ItemsName, "field_dressing", "price"), (e.File, e.Entry, e.Field));
    }

    [Fact]
    public void TheShippedCampaignAndPricesRoundTripThroughTheSerializer()
    {
        var written = ContentSerializer.Write(MapFixture.Content);
        var reloaded = ContentLoader.Parse(written);

        Assert.Equal(MapFixture.Content.Campaign, reloaded.Campaign);
        Assert.Equal(MapFixture.Content.Weapons, reloaded.Weapons);
        Assert.Equal(MapFixture.Content.Items, reloaded.Items);
        Assert.Equal(written.Campaign!.Text, ContentSerializer.Write(reloaded).Campaign!.Text);
    }

    [Fact]
    public void TheShippedCampaignPlaysTheSixMapsInOrderEachOnDisk()
    {
        var maps = MapFixture.Content.Campaign.Maps;

        Assert.Equal(new[] { "old_mill_road", "saltmarsh_ford", "the_tollgate", "harrow_weir", "sallow_grange", "brackwater_cut" }, maps.Select(m => m.MapId));
        Assert.All(maps, m => Assert.True(File.Exists(Path.Combine(MapFixture.MapsDirectory, m.MapId + ".map"))));
    }

    [Fact]
    public void TheCampaignRecordRoundTripsThroughTheProtocol()
    {
        var content = MapFixture.Content;
        var record = CampaignRecord.Start(content, ulong.MaxValue - 3).Buy("field_dressing", "pell", content).Record.Certify("brannock", "reaver", content).Record;
        record = record with
        {
            Roster = ValueList<Unit>.From(record.Roster.Where(u => u.Id != "rook").Select(u => u.Id == "wren" ? u with { Exp = 12, Skill = u.Skill.Add(WeaponType.Sword, 33), Mastery = u.Mastery.With("cadet", 4) } : u)),
            Fallen = ValueList<string>.Of("rook"),
            Benched = ValueList<string>.Of("ottilie"),
            MapIndex = 1,
        };

        var json = ProtocolJson.Campaign(record);
        var read = ProtocolJson.ReadCampaign(json, content);

        Assert.Equal(record, read);
        Assert.Equal(json, ProtocolJson.Campaign(read));
    }

    [Fact]
    public void ACampaignRecordFromAnotherProtocolVersionIsRefused()
    {
        var json = ProtocolJson.Campaign(CampaignRecord.Start(MapFixture.Content, 1)).Replace("\"protocolVersion\":1", "\"protocolVersion\":99");

        var e = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCampaign(json, MapFixture.Content));

        Assert.Contains("protocolVersion 99", e.Message);
    }
}
