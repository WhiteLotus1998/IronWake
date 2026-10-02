using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 687, the keep's rooms and beds (DESIGN section 13.20, DECISIONS/0137, 0138): the keep
/// starts with beds, a bought room adds beds, every member who has joined holds one living or
/// fallen, and a meeting with no free bed does not join. Rooms are bought from map 1 on, from the
/// purse the walls spend, which wait for the raid.
/// </summary>
public class KeepRoomTests
{
    private static GameContent Content => MapFixture.Content;

    private static KeepMenu Menu => Content.Campaign.Keep;

    private static int IndexOf(string id) => Content.Campaign.Maps.Select(m => m.MapId).ToList().IndexOf(id);

    private static MapDefinition Map(string id) => MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), Content, id), Content);

    /// <summary>The content with the keep's beds set to <paramref name="beds"/>, below what the loader allows.</summary>
    private static GameContent WithBeds(int beds) => Content with { Campaign = Content.Campaign with { Keep = Menu with { Beds = beds } } };

    /// <summary>A record whose next map is The Mill, where Maud arrives, with <paramref name="purse"/> in hand.</summary>
    private static CampaignRecord AtTheMill(int purse = 1000) =>
        CampaignRecord.Start(Content, 687) with { MapIndex = IndexOf("the_mill"), Purse = purse };

    [Fact]
    public void TheKeepSeatsTheCastPlusOneSpareAndSellsTheBunkRoom()
    {
        Assert.Equal(Content.Cast.Count + 1, Menu.Beds);
        Assert.Equal(new KeepRoom("bunk", "Bunk room", 400, 2, 2), Menu.Rooms[0]);
        Assert.Equal(Content.Cast.Count - 2, CampaignRecord.Start(Content, 1).BedsTaken);
    }

    [Fact]
    public void ABunkRoomAddsTwoBeds()
    {
        var start = CampaignRecord.Start(Content, 1) with { Purse = 500 };

        var built = start.BuildRoom("bunk", Content);

        Assert.True(built.Accepted);
        Assert.Equal(Menu.Beds + 2, built.Record.Beds(Content));
        Assert.Equal(100, built.Record.Purse);
        Assert.Equal(new[] { "bunk" }, built.Record.Rooms);
        Assert.Equal($"Bunk room built for 400, the purse holds 100; beds: 9/{Menu.Beds + 2}", built.Text);
    }

    [Fact]
    public void AThirdBunkRoomIsRefused()
    {
        var two = CampaignRecord.Start(Content, 1) with { Purse = 2000, Rooms = ValueList<string>.Of("bunk", "bunk") };

        var third = two.BuildRoom("bunk", Content);

        Assert.False(third.Accepted);
        Assert.Equal("Bunk room is built 2 of 2", third.Text);
        Assert.Equal(two, third.Record);
    }

    [Fact]
    public void ARoomIsRefusedOnAShortPurseOffTheMenuAndWhenTheKeepSellsNone()
    {
        var start = CampaignRecord.Start(Content, 1) with { Purse = 399 };
        var none = Content with { Campaign = Content.Campaign with { Keep = Menu with { Rooms = ValueList<KeepRoom>.Empty } } };

        Assert.Equal("Bunk room costs 400 and the purse holds 399", start.BuildRoom("bunk", Content).Text);
        Assert.Equal("the keep has no room 'chapel'; it builds bunk, forge, barracks, barracks_wing", start.BuildRoom("chapel", Content).Text);
        Assert.Equal("the keep has no rooms to build", start.BuildRoom("bunk", none).Text);
        Assert.Equal("the campaign is finished", (start with { Purse = 400, MapIndex = Content.Campaign.Maps.Count }).BuildRoom("bunk", Content).Text);
    }

    [Fact]
    public void RoomsCanBeBoughtBeforeTheRaidWallsCannot()
    {
        var first = CampaignRecord.Start(Content, 1) with { Purse = 1000 };
        var bare = Map(Menu.MapId);

        Assert.True(first.BuildRoom("bunk", Content).Accepted);
        var wall = first.Build("wall", new Coord(10, 3), bare, Content);
        Assert.False(wall.Accepted);
        Assert.Equal($"the keep's menu opens after the raid on it ({Menu.RaidId}, map {IndexOf(Menu.RaidId) + 1}) is fought", wall.Text);
    }

    [Fact]
    public void AFallenRecruitKeepsTheirBed()
    {
        var start = CampaignRecord.Start(Content, 1);
        var fallen = start with { Roster = start.Roster.RemoveAt(1), Fallen = ValueList<string>.Of(start.Roster[1].Id) };

        Assert.Equal(start.BedsTaken, fallen.BedsTaken);
        Assert.Equal(start.FreeBeds(Content), fallen.FreeBeds(Content));
    }

    [Fact]
    public void AMeetingWithNoFreeBedDoesNotJoinAndPrintsTheLine()
    {
        var tight = WithBeds(AtTheMill().BedsTaken);
        var mill = AtTheMill();

        Assert.Equal(0, mill.FreeBeds(tight));
        Assert.Equal(new[] { "maud" }, mill.TurnedAway(tight));
        Assert.DoesNotContain(mill.Present(tight), u => u.Id == "maud");
        Assert.Equal(new[] { "no bed free: Maud will not join" }, CampaignSession.TurnedAwayLines(mill, tight));
        Assert.Contains(mill.Present(Content), u => u.Id == "maud");
        Assert.Empty(CampaignSession.TurnedAwayLines(mill, Content));
    }

    [Fact]
    public void AMeetingTurnedAwayNeverJoinsTheRosterOrTheFallen()
    {
        var tight = WithBeds(AtTheMill().BedsTaken);
        var mill = AtTheMill();
        var unprotected = Map("the_mill") with { ProtectId = null };
        var opening = mill.Begin(unprotected, tight);
        var won = opening with { Turn = 4, History = ValueList<BattleState>.Of(opening), Units = ValueList<BattleUnit>.From(opening.UnitsOf(Side.Player)) };

        var after = mill.AfterBattle(won, tight);

        Assert.DoesNotContain(opening.UnitsOf(Side.Player), u => u.Id == "maud");
        Assert.Null(after.Find("maud"));
        Assert.DoesNotContain("maud", after.Fallen);
    }

    [Fact]
    public void ABoughtRoomFreesABedForTheMeeting()
    {
        var tight = WithBeds(AtTheMill().BedsTaken);
        var built = AtTheMill().BuildRoom("bunk", tight).Record;

        Assert.Empty(built.TurnedAway(tight));
        Assert.Contains(built.Present(tight), u => u.Id == "maud");
    }

    [Fact]
    public void TheCampScreenPrintsTheBedsAndEachRoom()
    {
        var record = CampaignRecord.Start(Content, 1) with { Rooms = ValueList<string>.Of("bunk") };

        Assert.Equal(
            new[]
            {
                $"Rooms: beds: 9/{Menu.Beds + 2}; a fallen member keeps their bed",
                "  bunk: Bunk room, 400, +2 beds, built 1 of 2",
                "  forge: Forge, 600, Refine +5 acc or +1 power a step, built 0 of 1; opens once the_tollgate is won",
                "  barracks: Barracks, 500, +2 beds and 4 hires at 300, built 0 of 1; opens once ironwake_raid is won",
                "  barracks_wing: Barracks wing, 300, +1 bed and 3 hires at 300, built 0 of 1; opens once ironwake_raid is won; needs the Barracks first",
                "  Stores: common 0, frozen iron 0; a step costs one and 100, shop weapons to +2 on common, the main line's signatures to +3 on frozen iron",
            },
            CampaignSession.RoomLines(record, Content));
        Assert.Empty(CampaignSession.RoomLines(record with { MapIndex = Content.Campaign.Maps.Count }, Content));
    }

    [Fact]
    public void TheRecordCarriesTheRoomsBoughtAndAnOlderRecordReadsAsNone()
    {
        var record = CampaignRecord.Start(Content, 1) with { Rooms = ValueList<string>.Of("bunk") };

        var json = ProtocolJson.Campaign(record);
        var back = ProtocolJson.ReadCampaign(json, Content);
        var old = ProtocolJson.Campaign(record with { Rooms = ValueList<string>.Empty });
        var unknown = json.Replace("\"rooms\":[\"bunk\"]", "\"rooms\":[\"chapel\"]");

        Assert.Contains("\"rooms\":[\"bunk\"]", json);
        Assert.Equal(record, back);
        Assert.DoesNotContain("\"rooms\"", old);
        Assert.Empty(ProtocolJson.ReadCampaign(old, Content).Rooms);
        var error = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCampaign(unknown, Content));
        Assert.Equal("field 'rooms': 'chapel' is not a room the keep sells", error.Message);
    }

    private static ContentException Fails(string keep) => Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files() with
    {
        Campaign = new ContentFile(ContentFiles.CampaignName, $$"""{ "startingPurse": 0, "certificationPrice": 0, "maps": [ { "map": "keep", "reward": 0, "stock": [] } ], "keep": { "map": "keep", "edits": [] {{keep}} } }"""),
    }));

    [Fact]
    public void BedsBelowTheCastTheCampaignSeatsAreRefused()
    {
        var files = ContentSerializer.Write(Content);
        var campaign = System.Text.Json.Nodes.JsonNode.Parse(files.Campaign!.Text)!;
        campaign["keep"]!["beds"] = Content.Cast.Count - 1;

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(files with { Campaign = files.Campaign with { Text = campaign.ToJsonString() } }));

        Assert.Equal((ContentFiles.CampaignName, "keep.beds"), (error.File, error.Field));
        Assert.Contains($"must be at least {Content.Cast.Count}", error.Message);
        Assert.Equal(Menu.Beds, ContentLoader.Parse(ContentSerializer.Write(Content)).Campaign.Keep.Beds);
    }

    [Fact]
    public void ARoomsPriceBedsAndMaxMustBePositiveAndRoomsNeedBeds()
    {
        const string room = """{ "id": "bunk", "name": "Bunk room", "price": 400, "beds": 1, "max": 2 }""";

        Assert.Equal("price", Fails($$""", "beds": 99, "rooms": [ {{room.Replace("400", "0")}} ]""").Field);
        Assert.Equal("beds", Fails($$""", "beds": 99, "rooms": [ {{room.Replace("\"beds\": 1", "\"beds\": 0")}} ]""").Field);
        Assert.Equal("max", Fails($$""", "beds": 99, "rooms": [ {{room.Replace("2 }", "0 }")}} ]""").Field);
        Assert.Equal("id", Fails($$""", "beds": 99, "rooms": [ {{room}}, {{room}} ]""").Field);
        Assert.Equal("rooms", Fails($$""", "rooms": [ {{room}} ]""").Field);
    }
}
