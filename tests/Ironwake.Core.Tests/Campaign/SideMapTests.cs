using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Side maps (issue 635, DESIGN section 14): a member's quest is a trial-shaped board, the member
/// in its captain slot and one ally the player picks, never the captain. Quest 1 opens after the
/// member's second map, quest 2 two maps after quest 1 is won, and an interlude offers at most two.
/// The price is permadeath and nothing else: who falls there is fallen for good, and a lost side
/// map never ends the campaign.
/// </summary>
public class SideMapTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static MapDefinition Side(string id) =>
        MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), MapFiles.QuestsDirectory, id + MapFiles.Extension), Content);

    private static CampaignRecord At(string mapId, GameContent? content = null) => CampaignRecord.StartAt(content ?? Content, 701, mapId);

    /// <summary>The shipped content with <paramref name="quests"/> as its side maps, each on the Lazar House's board.</summary>
    private static GameContent With(params CampaignQuest[] quests) =>
        Content with { Campaign = Content.Campaign with { Quests = ValueList<CampaignQuest>.From(quests) } };

    private static CampaignQuest Quest(string id, string member, int part) => new(id, member, part, "the_lazar_house");

    /// <summary>The side map's opening with the enemies gone, the turn limit passed, and the player units <paramref name="keep"/> keeps.</summary>
    private static BattleState Decided(CampaignRecord record, string questId, string ally, GameContent content, Func<BattleUnit, bool>? keep = null, Func<BattleUnit, BattleUnit>? change = null)
    {
        var opening = record.BeginQuest(Side("the_lazar_house"), questId, ally, content);
        var units = opening.UnitsOf(Ironwake.Core.Side.Player).Where(u => keep is null || keep(u)).Select(u => change is null ? u : change(u));
        return opening with { Units = ValueList<BattleUnit>.From(units), Turn = opening.Map.TurnLimit + 1, History = ValueList<BattleState>.Of(opening) };
    }

    [Fact]
    public void TheShippedCampaignOffersMaudsFirstQuestOnDiskInTheSideMapShape()
    {
        var quest = Assert.Single(Content.Campaign.Quests, q => q.OpensAfter is null);

        Assert.Equal(("maud_1", "maud", 1, "the_lazar_house"), (quest.Id, quest.MemberId, quest.Part, quest.MapId));
        Assert.Null(CampaignRecord.QuestMapRefusal(Side(quest.MapId)));
        Assert.NotEmpty(quest.Before);
        Assert.NotEmpty(quest.After);
    }

    [Fact]
    public void TheLazarHouseIsCanonical()
    {
        var path = Path.Combine(Fixture.RealContentDirectory(), MapFiles.QuestsDirectory, "the_lazar_house.map");
        var text = File.ReadAllText(path).ReplaceLineEndings("\n");

        Assert.Equal(text, MapFormat.Write(MapFormat.Parse(path, text, Content), Content));
    }

    [Fact]
    public void QuestOneOpensAfterTheMembersSecondMap()
    {
        Assert.Empty(At("saltmarsh_ford").QuestsOffered(Content));
        Assert.Equal(new[] { "maud_1" }, At("the_tollgate").QuestsOffered(Content).Select(q => q.Id));
    }

    [Fact]
    public void QuestTwoOpensTwoMapsAfterQuestOneIsWon()
    {
        var content = With(Quest("maud_1", "maud", 1), Quest("maud_2", "maud", 2));
        var record = At("the_tollgate", content);
        Assert.Equal(new[] { "maud_1" }, record.QuestsOffered(content).Select(q => q.Id));

        var won = record with { QuestsWon = ValueList<QuestWon>.Of(new QuestWon("maud_1", 3)) };

        Assert.Empty(won.QuestsOffered(content));
        Assert.Empty((won with { MapIndex = 4 }).QuestsOffered(content));
        Assert.Equal(new[] { "maud_2" }, (won with { MapIndex = 5 }).QuestsOffered(content).Select(q => q.Id));
        Assert.Null(record.QuestOpensAt(content.Campaign.Quests[1], content));
    }

    [Fact]
    public void AnInterludeOffersAtMostTwoSideMapsAndTheOverflowWaits()
    {
        var content = With(Quest("wren_1", "wren", 1), Quest("teodor_1", "teodor", 1), Quest("pell_1", "pell", 1));
        var record = At("the_tollgate", content);

        Assert.Equal(new[] { "wren_1", "teodor_1" }, record.QuestsOffered(content).Select(q => q.Id));

        var oneWon = record with { QuestsWon = ValueList<QuestWon>.Of(new QuestWon("wren_1", record.MapIndex)) };
        Assert.Equal(new[] { "teodor_1" }, oneWon.QuestsOffered(content).Select(q => q.Id));
        Assert.Equal(new[] { "teodor_1", "pell_1" }, (oneWon with { MapIndex = record.MapIndex + 1 }).QuestsOffered(content).Select(q => q.Id));
    }

    [Fact]
    public void AnEarlierOpeningIsOfferedBeforeALaterOneWhateverTheFileOrder()
    {
        var content = With(Quest("wren_1", "wren", 1), Quest("teodor_1", "teodor", 1), Quest("maud_1", "maud", 1));

        Assert.Equal(new[] { "wren_1", "teodor_1" }, At("the_tollgate", content).QuestsOffered(content).Select(q => q.Id));
        Assert.Equal(-1, content.Campaign.ArrivalIndex("wren"));
        Assert.Equal(3, At("the_tollgate", content).QuestOpensAt(content.Campaign.Quests[2], content));
    }

    [Theory]
    [InlineData("maud_1", "captain", "captain is the captain and stays with the company; pick another ally")]
    [InlineData("maud_1", "maud", "maud is the side map's own; pick an ally beside maud")]
    [InlineData("maud_1", "nobody", "no unit 'nobody' on the roster")]
    [InlineData("wren_9", "wren", "no side map 'wren_9'; the campaign has maud_1, bet_postern")]
    public void ASideMapIsRefusedTheCaptainTheMemberAStrangerAndAnUnknownQuest(string quest, string ally, string refusal)
    {
        Assert.Equal(refusal, At("the_tollgate").QuestRefusal(quest, ally, Content));
    }

    [Fact]
    public void ASideMapIsRefusedBeforeItOpens()
    {
        Assert.Equal("side map maud_1 is not open before this map", At("saltmarsh_ford").QuestRefusal("maud_1", "wren", Content));
        Assert.Null(At("the_tollgate").QuestRefusal("maud_1", "wren", Content));
    }

    [Fact]
    public void TheMemberLeadsInTheCaptainSlotAndTheAllyFillsTheBareSlot()
    {
        var battle = At("the_tollgate").BeginQuest(Side("the_lazar_house"), "maud_1", "teodor", Content);

        var players = battle.UnitsOf(Ironwake.Core.Side.Player).OrderBy(u => u.PlacementIndex).ToList();
        Assert.Equal(new[] { ("maud", true), ("teodor", false) }, players.Select(u => (u.Id, u.IsCaptain)));
        Assert.Equal("Hold out until the end of turn 6. Maud must survive.", Objective.Line(battle, Content));
    }

    [Fact]
    public void AWonSideMapIsRecordedAndItsSurvivorsComeBackAsTheBattleLeftThem()
    {
        var record = At("the_tollgate");
        var end = Decided(record, "maud_1", "wren", Content, change: u => u with { Unit = u.Unit with { Exp = 77 } });
        Assert.Equal(BattleResult.Won, end.Outcome.Result);

        var after = record.AfterQuest(end, "maud_1", Content);

        Assert.True(after.Accepted);
        Assert.Equal("maud wins maud_1; the stores take 2 common material; nobody fell", after.Text);
        Assert.Equal(2, after.Record.CommonMaterial);
        Assert.Equal(ValueList<QuestWon>.Of(new QuestWon("maud_1", 3)), after.Record.QuestsWon);
        Assert.Equal(77, after.Record.Find("maud")!.Exp);
        Assert.Equal(77, after.Record.Find("wren")!.Exp);
        Assert.Equal((record.MapIndex, record.Purse), (after.Record.MapIndex, after.Record.Purse));
        Assert.Empty(after.Record.QuestsOffered(Content));
        Assert.Equal("side map maud_1 is won already", after.Record.QuestRefusal("maud_1", "wren", Content));
    }

    [Fact]
    public void WhoFallsOnASideMapIsFallenForGood()
    {
        var record = At("the_tollgate");
        var end = Decided(record, "maud_1", "wren", Content, keep: u => u.Id != "wren");

        var after = record.AfterQuest(end, "maud_1", Content).Record;

        Assert.Null(after.Find("wren"));
        Assert.Equal(ValueList<string>.Of("wren"), after.Fallen);
        Assert.False(after.IsFinished(Content));
        Assert.Equal("the_tollgate", after.NextMap(Content).MapId);
    }

    [Fact]
    public void ASideMapLostByTheMemberNamesTheMemberAndNeverTheCaptain()
    {
        var end = Decided(At("the_tollgate"), "maud_1", "wren", Content, keep: u => u.Id != "maud");

        Assert.Equal(LossCause.Captain, end.Outcome.Cause);
        Assert.Equal("maud is dead", Objective.Reason(end, Content));
        Assert.Equal("Maud is dead", UnitNames.Of(end, Content).Named(Objective.Reason(end, Content)));
        Assert.Contains("\"reason\":\"maud is dead\"", ProtocolJson.State(end, Content));
        Assert.DoesNotContain("captain", Objective.Reason(end, Content));
    }

    [Fact]
    public void ASideMapLostWithTheMemberWoundedNamesTheMemberInTheRecordLine()
    {
        var record = CampaignRecord.StartAt(Content, 701, "the_tollgate", permadeath: false);
        var end = Decided(record, "maud_1", "wren", Content, keep: u => u.Id != "maud");

        var after = record.AfterQuest(end, "maud_1", Content);

        Assert.StartsWith("side map maud_1 is lost: maud is dead;", after.Text);
    }

    [Fact]
    public void TheCompanyCaptainsDeathStillReadsTheCaptainIsDead()
    {
        var record = At("the_tollgate");
        var opening = record.Begin(MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate" + MapFiles.Extension), Content), Content);
        var end = opening with { Units = ValueList<BattleUnit>.From(opening.Units.Where(u => !u.IsCaptain)), History = ValueList<BattleState>.Of(opening) };

        Assert.Equal("the captain is dead", Objective.Reason(end, Content));
    }

    [Fact]
    public void AMemberWhoFallsClosesTheSideMapAndTheCampaignGoesOn()
    {
        var record = At("the_tollgate");
        var end = Decided(record, "maud_1", "wren", Content, keep: u => u.Id != "maud");
        Assert.Equal(BattleResult.Lost, end.Outcome.Result);
        Assert.Equal("Lost because Maud fell.", Objective.Verdict(end, Content));

        var after = record.AfterQuest(end, "maud_1", Content);

        Assert.Equal("maud falls on maud_1, which closes for good; fallen for good: maud", after.Text);
        Assert.Equal(ValueList<string>.Of("maud"), after.Record.Fallen);
        Assert.Empty(after.Record.QuestsWon);
        Assert.Empty(after.Record.QuestsOffered(Content));
        Assert.Equal("side map maud_1 closed when maud fell", after.Record.QuestRefusal("maud_1", "wren", Content));
    }

    [Fact]
    public void ASideMapLostWithItsMemberStandingOpensAgainAfterTheNextMap()
    {
        var record = At("the_tollgate");
        var opening = record.BeginQuest(Side("the_lazar_house"), "maud_1", "wren", Content);
        var timedOut = opening with { Map = opening.Map with { Win = WinCondition.Rout }, Turn = opening.Map.TurnLimit + 1, History = ValueList<BattleState>.Of(opening) };
        Assert.Equal(BattleResult.Lost, timedOut.Outcome.Result);

        var after = record.AfterQuest(timedOut, "maud_1", Content).Record;

        Assert.Equal("side map maud_1 was fought since the last map; it opens again after the next one", after.QuestRefusal("maud_1", "wren", Content));
        Assert.Equal(new[] { "maud_1" }, after.QuestsOffered(Content).Select(q => q.Id));
    }

    [Fact]
    public void TheNextMapReopensASideMapFoughtThisInterlude()
    {
        var record = At("the_mill") with { QuestsTried = ValueList<string>.Of("maud_1") };
        var mill = MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), Content, "the_mill"), Content);
        var opening = record.Begin(mill, Content);
        var won = opening with { Units = ValueList<BattleUnit>.From(opening.UnitsOf(Ironwake.Core.Side.Player)), History = ValueList<BattleState>.Of(opening) };

        Assert.Empty(record.AfterBattle(won, Content).QuestsTried);
    }

    [Fact]
    public void ABoardIsASideMapOnlyWithABareSlotAndNoNamedRecruitOrCertification()
    {
        var board = Side("the_lazar_house");
        var named = board with { Placements = ValueList<Placement>.From(board.Placements.Select(p => p is PlayerPlacement { Slot: PlayerSlot.AnyRecruit } bare ? bare with { Slot = PlayerSlot.NamedRecruit, RecruitId = "wren" } : p)) };
        var none = board with { Placements = ValueList<Placement>.From(board.Placements.Where(p => p is not PlayerPlacement { Slot: PlayerSlot.AnyRecruit })) };
        var two = board with { Placements = board.Placements.Add(new PlayerPlacement(new Coord(0, 4), PlayerSlot.AnyRecruit)) };
        var trial = board with { Certification = new CertificationTrial("outrider", ValueList<string>.Of("iron_lance")) };

        Assert.Equal("the side map 'The Lazar House' places a recruit by name; its slots are the member's (captain) and the ally's (recruit)", CampaignRecord.QuestMapRefusal(named));
        Assert.Equal("the side map 'The Lazar House' needs a bare recruit slot, for the ally", CampaignRecord.QuestMapRefusal(none));
        Assert.Null(CampaignRecord.QuestMapRefusal(two));
        Assert.Equal(2, CampaignRecord.QuestAllies(two));
        Assert.Equal("the side map 'The Lazar House' is a certification trial", CampaignRecord.QuestMapRefusal(trial));
    }

    [Fact]
    public void ARecordsSideMapsRoundTripThroughTheProtocol()
    {
        var record = At("the_tollgate") with { QuestsWon = ValueList<QuestWon>.Of(new QuestWon("maud_1", 3)), QuestsTried = ValueList<string>.Of("maud_1") };

        var read = ProtocolJson.ReadCampaign(ProtocolJson.Campaign(record), Content);

        Assert.Equal(record.QuestsWon, read.QuestsWon);
        Assert.Equal(record.QuestsTried, read.QuestsTried);
    }

    [Fact]
    public void ARecordNamingAnUnknownSideMapIsRefused()
    {
        var json = ProtocolJson.Campaign(At("the_tollgate") with { QuestsWon = ValueList<QuestWon>.Of(new QuestWon("ghost_1", 3)) });

        var e = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCampaign(json, Content));

        Assert.Contains("'ghost_1' is not a side map of the campaign", e.Message);
    }

    /// <summary>The shipped content's files with <paramref name="quests"/> as the campaign's side maps.</summary>
    private static ContentFiles WithQuests(string quests) =>
        ContentSerializer.Write(Content) with { Campaign = new ContentFile(ContentFiles.CampaignName, $$"""{ "startingPurse": 0, "certificationPrice": 0, "maps": [ { "map": "one", "reward": 0, "stock": [] } ], "quests": [ {{quests}} ] }""") };

    [Fact]
    public void ASideMapRoundTripsThroughTheSerializer()
    {
        var content = ContentLoader.Parse(WithQuests("""{ "id": "maud_1", "member": "maud", "part": 1, "map": "the_lazar_house", "before": ["One."] }"""));

        var reloaded = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.Equal(content.Campaign.Quests, reloaded.Campaign.Quests);
    }

    [Theory]
    [InlineData("""{ "id": "x", "member": "nobody", "part": 1, "map": "m" }""", "x", "member", "'nobody' is not in the cast")]
    [InlineData("""{ "id": "x", "member": "captain", "part": 1, "map": "m" }""", "x", "member", "'captain' is the captain")]
    [InlineData("""{ "id": "x", "member": "maud", "part": 3, "map": "m" }""", "x", "part", "must be 1 or 2")]
    [InlineData("""{ "id": "x", "member": "maud", "part": 2, "map": "m" }""", "x", "part", "'maud' has no quest 1 listed before this quest 2")]
    [InlineData("""{ "id": "x", "member": "maud", "part": 1, "map": "m" }, { "id": "y", "member": "maud", "part": 1, "map": "n" }""", "y", "part", "'maud' has a quest 1 already")]
    [InlineData("""{ "id": "x", "member": "maud", "part": 1, "map": "m" }, { "id": "x", "member": "wren", "part": 1, "map": "n" }""", "x", "id", "is listed twice")]
    public void TheLoaderRefusesAMalformedSideMap(string quests, string entry, string field, string problem)
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(WithQuests(quests)));

        Assert.Equal((ContentFiles.CampaignName, entry, field), (e.File, e.Entry, e.Field));
        Assert.Contains(problem, e.Message);
    }
}
