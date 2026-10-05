using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 805 slice 1 (STORY draft 6, Rook's drake): the drake grows on its rider, never a weapon,
/// in three stages. Half-grown as she joins; Grown once her quest 1 is won and she has flown two
/// main maps (deployed, standing at the end of a won map); Unbroken once her quest 2 is won. It
/// never goes back. A passed Rook comes back Grown. The card prints the stage, the save carries it,
/// and the loader refuses a drake whose quests are not its rider's.
/// </summary>
public class DrakeTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static string Dir => Fixture.RealContentDirectory();

    private static DrakeRules Rules => Content.Campaign.Drake!;

    private static MapDefinition Map(string id) => MapFiles.Load(MapFiles.CampaignPath(Dir, Content, id), Content);

    private static MapDefinition SideMap(string id) =>
        MapFiles.Load(Path.Combine(Dir, MapFiles.QuestsDirectory, id + MapFiles.Extension), Content);

    private static BattleState Won(CampaignRecord record, MapDefinition map, Func<BattleUnit, bool>? keep = null)
    {
        var opening = record.Begin(map, Content);
        var units = opening.UnitsOf(Side.Player).Where(u => keep is null || keep(u));
        return opening with { Units = ValueList<BattleUnit>.From(units), Turn = 4, History = ValueList<BattleState>.Of(opening) };
    }

    /// <summary>The field's record with Rook picked and everyone but the field's named slots and Rook benched, so she fills the bare slot.</summary>
    private static CampaignRecord OnTheField()
    {
        var record = CampaignRecord.StartAt(Content, 805, "the_field", pick: "rook");
        var fielded = new[] { Content.Cast[0].Id, "teodor", "ottilie", "pell", "maud", "rook" };
        return record with { Benched = ValueList<string>.From(record.Present(Content).Select(u => u.Id).Where(id => !fielded.Contains(id))) };
    }

    private static CampaignRecord WithRook(CampaignRecord record, DrakeState drake) =>
        record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == "rook" ? u with { Drake = drake } : u)) };

    [Fact]
    public void TheShippedDrakeIsRooksGrownByHerFirstQuestAndTwoMapsUnbrokenByHerSecond()
    {
        Assert.Equal(new DrakeRules("rook", "rook_1", 2, "rook_2"), Rules);
    }

    [Fact]
    public void TheDrakeJoinsHalfGrownWithItsRiderAndNoOneElseRidesOne()
    {
        Assert.Equal(new DrakeState(DrakeStage.HalfGrown, 0), CampaignRecord.Kitted(Content.Unit("rook"), Content).Drake);
        Assert.Null(CampaignRecord.Kitted(Content.Unit("keziah"), Content).Drake);
        Assert.Null(Content.Unit("rook").Drake);

        var picked = CampaignRecord.StartAt(Content, 805, "ironwake_raid").PickClaimant("rook", Content).Record;

        Assert.Equal(DrakeStage.HalfGrown, picked.Present(Content).Single(u => u.Id == "rook").Drake?.Stage);
    }

    [Theory]
    [InlineData(5, new string[0], DrakeStage.HalfGrown)]
    [InlineData(1, new[] { "rook_1" }, DrakeStage.HalfGrown)]
    [InlineData(2, new string[0], DrakeStage.HalfGrown)]
    [InlineData(2, new[] { "rook_1" }, DrakeStage.Grown)]
    [InlineData(0, new[] { "rook_1", "rook_2" }, DrakeStage.Unbroken)]
    public void TheDrakeGrowsOnlyWithItsQuestWonAndItsMapsFlown(int flown, string[] won, DrakeStage stage)
    {
        Assert.Equal(stage, Rules.StageFor(new DrakeState(DrakeStage.HalfGrown, flown), won));
    }

    [Fact]
    public void TheDrakeNeverGoesBack()
    {
        Assert.Equal(DrakeStage.Grown, Rules.StageFor(new DrakeState(DrakeStage.Grown, 0), Array.Empty<string>()));
        Assert.Equal(DrakeStage.Unbroken, Rules.StageFor(new DrakeState(DrakeStage.Unbroken, 0), Array.Empty<string>()));
    }

    [Fact]
    public void AWonMainMapCountsAFlownMapForADeployedRiderStandingAtTheEnd()
    {
        var record = OnTheField();
        Assert.Contains(record.Begin(Map("the_field"), Content).UnitsOf(Side.Player), u => u.Id == "rook");

        var after = record.AfterBattle(Won(record, Map("the_field")), Content);

        Assert.Equal(record.Find("rook")!.Drake!.Flown + 1, after.Find("rook")!.Drake!.Flown);
    }

    [Fact]
    public void ABenchedRiderFliesNoMap()
    {
        var record = CampaignRecord.StartAt(Content, 805, "the_field", pick: "rook") with { Benched = ValueList<string>.Of("rook") };

        var after = record.AfterBattle(Won(record, Map("the_field")), Content);

        Assert.Equal(record.Find("rook")!.Drake!.Flown, after.Find("rook")!.Drake!.Flown);
    }

    [Fact]
    public void TheSecondMapFlownAfterHerFirstQuestGrowsTheDrakeAndTheCampSaysSo()
    {
        var record = WithRook(OnTheField(), new DrakeState(DrakeStage.HalfGrown, 1)) with { QuestsWon = ValueList<QuestWon>.Of(new QuestWon("rook_1", 7)) };

        var after = record.AfterBattle(Won(record, Map("the_field")), Content);

        Assert.Equal(new DrakeState(DrakeStage.Grown, 2), after.Find("rook")!.Drake);
        Assert.Equal(new[] { "Rook's drake is grown." }, CampaignSession.DrakesGrown(record, after));
        Assert.Empty(CampaignSession.DrakesGrown(after, after));
    }

    [Fact]
    public void WinningHerSecondQuestMakesTheDrakeUnbrokenAndTheLineSaysSo()
    {
        var start = CampaignRecord.StartAt(Content, 805, "ironwake_keep", pick: "rook");
        var ally = start.Roster.First(u => u.Id != "rook" && u.Id != Content.Cast[0].Id).Id;
        var record = WithRook(start, new DrakeState(DrakeStage.Grown, 3)) with { QuestsWon = ValueList<QuestWon>.Of(new QuestWon("rook_1", 7)) };
        var opening = record.BeginQuest(SideMap("the_lazar_house"), "rook_2", ally, Content);
        var end = opening with { Units = ValueList<BattleUnit>.From(opening.UnitsOf(Side.Player)), Turn = opening.Map.TurnLimit + 1, History = ValueList<BattleState>.Of(opening) };

        var result = record.AfterQuest(end, "rook_2", Content);

        Assert.Equal(DrakeStage.Unbroken, result.Record.Find("rook")!.Drake!.Stage);
        Assert.Contains("Rook's drake is unbroken now", result.Text);
    }

    [Fact]
    public void APassedRookComesBackWithTheDrakeGrown()
    {
        Assert.Equal(DrakeStage.Grown, CampaignRecord.Returning(Content.Unit("rook"), Content).Drake?.Stage);
        Assert.Null(CampaignRecord.Returning(Content.Unit("keziah"), Content).Drake);
    }

    [Fact]
    public void TheCardSaysWhatGrowsAHalfGrownDrakeAndTheVerbsEachStageOpens()
    {
        var rook = CampaignRecord.Kitted(Content.Unit("rook"), Content);

        Assert.Equal("Drake: half-grown; grows once Rook's first quest is won and she has flown 2 maps (0 so far).", Drake.Card(rook, Content));
        Assert.Equal("Drake: grown; carries an ally (carry).", Drake.Card(rook with { Drake = new DrakeState(DrakeStage.Grown, 2) }, Content));
        Assert.Equal("Drake: unbroken; carries an ally (carry), breathes rime once a map (breathe).", Drake.Card(rook with { Drake = new DrakeState(DrakeStage.Unbroken, 2) }, Content));
        Assert.Null(Drake.Card(Content.Unit("rook"), Content));
    }

    [Fact]
    public void TheDrakeRoundTripsThroughTheSave()
    {
        var record = WithRook(CampaignRecord.StartAt(Content, 805, "the_field", pick: "rook"), new DrakeState(DrakeStage.Grown, 3));

        var read = ProtocolJson.ReadCampaign(ProtocolJson.Campaign(record), Content);

        Assert.Equal(new DrakeState(DrakeStage.Grown, 3), read.Find("rook")!.Drake);
        Assert.DoesNotContain("\"drake\"", ProtocolJson.Campaign(CampaignRecord.Start(Content, 805)));
    }

    [Fact]
    public void ASavedDrakeWithAnUnknownStageIsRefused()
    {
        var json = ProtocolJson.Campaign(WithRook(CampaignRecord.StartAt(Content, 805, "the_field", pick: "rook"), new DrakeState(DrakeStage.Grown, 3)));

        var e = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCampaign(json.Replace("\"stage\":\"grown\"", "\"stage\":\"huge\""), Content));

        Assert.Contains("drake.stage", e.Message);
    }

    [Theory]
    [InlineData("\"member\": \"rook\"", "\"member\": \"nobody\"", "member")]
    [InlineData("\"grownAfter\": \"rook_1\"", "\"grownAfter\": \"maud_1\"", "grownAfter")]
    [InlineData("\"unbrokenAfter\": \"rook_2\"", "\"unbrokenAfter\": \"rook_1\"", "unbrokenAfter")]
    public void ADrakeWhoseRiderOrQuestsDoNotMatchIsRefusedNamingTheField(string from, string to, string field)
    {
        var files = ContentSerializer.Write(Content);
        var text = files.Campaign!.Text;
        Assert.Contains(from.Replace(" ", ""), text.Replace(" ", ""));
        var broken = files with { Campaign = new ContentFile(ContentFiles.CampaignName, System.Text.RegularExpressions.Regex.Replace(text, from.Replace(" ", "\\s*"), to)) };

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(broken));

        Assert.Equal((ContentFiles.CampaignName, field), (e.File, e.Field));
    }

    [Fact]
    public void ARiderWhoFallsForGoodOnAMainMapLeavesHerDrakesStageOnTheRecord()
    {
        var record = WithRook(OnTheField(), new DrakeState(DrakeStage.Grown, 3));
        Assert.Null(record.DrakeFlew);

        var after = record.AfterBattle(Won(record, Map("the_field"), u => u.Id != "rook"), Content);

        Assert.Equal(DrakeStage.Grown, after.DrakeFlew);
        Assert.Contains("rook", after.Fallen);
        Assert.Null(after.Find("rook"));
    }

    [Fact]
    public void AnotherMemberFallingFliesNoDrake()
    {
        var record = OnTheField();

        var after = record.AfterBattle(Won(record, Map("the_field"), u => u.Id != "teodor"), Content);

        Assert.Contains("teodor", after.Fallen);
        Assert.Null(after.DrakeFlew);
    }

    [Fact]
    public void WithPermadeathOffTheRiderComesBackWoundedAndKeepsTheDrake()
    {
        var record = WithRook(OnTheField(), new DrakeState(DrakeStage.Grown, 3)) with { Permadeath = false };

        var after = record.AfterBattle(Won(record, Map("the_field"), u => u.Id != "rook"), Content);

        Assert.Null(after.DrakeFlew);
        Assert.Equal(DrakeStage.Grown, after.Find("rook")!.Drake!.Stage);
    }

    [Fact]
    public void AFlownDrakeStaysOnTheRecordForTheRestOfTheCampaign()
    {
        var record = OnTheField() with { DrakeFlew = DrakeStage.HalfGrown };

        Assert.Equal(DrakeStage.HalfGrown, record.AfterBattle(Won(record, Map("the_field")), Content).DrakeFlew);
    }

    [Fact]
    public void ARiderWhoFallsOnHerSideMapLeavesHerDrakesStageOnTheRecord()
    {
        var start = CampaignRecord.StartAt(Content, 805, "ironwake_keep", pick: "rook");
        var ally = start.Roster.First(u => u.Id != "rook" && u.Id != Content.Cast[0].Id).Id;
        var record = WithRook(start, new DrakeState(DrakeStage.Grown, 3)) with { QuestsWon = ValueList<QuestWon>.Of(new QuestWon("rook_1", 7)) };
        var opening = record.BeginQuest(SideMap("the_lazar_house"), "rook_2", ally, Content);
        var end = opening with { Units = ValueList<BattleUnit>.From(opening.UnitsOf(Side.Player).Where(u => u.Id != "rook")), Turn = opening.Map.TurnLimit + 1, History = ValueList<BattleState>.Of(opening) };

        var result = record.AfterQuest(end, "rook_2", Content);

        Assert.Contains("rook", result.Record.Fallen);
        Assert.Equal(DrakeStage.Grown, result.Record.DrakeFlew);
    }

    [Fact]
    public void TheFlownDrakeRoundTripsThroughTheSaveAndIsWrittenOnlyOnceSet()
    {
        var record = CampaignRecord.StartAt(Content, 805, "the_field", pick: "rook") with { DrakeFlew = DrakeStage.Unbroken };

        var json = ProtocolJson.Campaign(record);

        Assert.Contains("\"drakeFlew\":\"unbroken\"", json);
        Assert.Equal(DrakeStage.Unbroken, ProtocolJson.ReadCampaign(json, Content).DrakeFlew);
        Assert.DoesNotContain("drakeFlew", ProtocolJson.Campaign(record with { DrakeFlew = null }));
        Assert.Null(ProtocolJson.ReadCampaign(ProtocolJson.Campaign(record with { DrakeFlew = null }), Content).DrakeFlew);
    }

    [Fact]
    public void ASavedFlownDrakeWithAnUnknownStageIsRefused()
    {
        var json = ProtocolJson.Campaign(CampaignRecord.StartAt(Content, 805, "the_field", pick: "rook") with { DrakeFlew = DrakeStage.Grown });

        var e = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCampaign(json.Replace("\"drakeFlew\":\"grown\"", "\"drakeFlew\":\"gone\""), Content));

        Assert.Contains("drakeFlew", e.Message);
    }

    [Fact]
    public void ARidersFallLineSaysTheDrakeLeavesTheFieldAndNoOneElsesDoes()
    {
        var record = WithRook(OnTheField(), new DrakeState(DrakeStage.Grown, 3));
        var opening = record.Begin(Map("the_field"), Content);
        var names = UnitNames.Of(opening, Content);

        Assert.True(names.Rides("rook"));
        Assert.False(names.Rides("teodor"));
        Assert.Equal("Rook falls at 4,5; her drake leaves the field", PlaySession.Describe(new UnitDied("rook", Side.Player, new Coord(4, 5)), Content, names));
        Assert.Equal($"{names["teodor"]} falls at 4,5", PlaySession.Describe(new UnitDied("teodor", Side.Player, new Coord(4, 5)), Content, names));
    }

    [Fact]
    public void TheCampsFallenListSaysTheDrakeFlewBesideItsRider()
    {
        var record = WithRook(OnTheField(), new DrakeState(DrakeStage.Grown, 3));
        var after = record.AfterBattle(Won(record, Map("the_field"), u => u.Id != "rook" && u.Id != "teodor"), Content);
        var names = UnitNames.Of(after, Content);

        var fallen = CampaignSession.RosterLines(after, Content).Single(l => l.StartsWith("  Fallen: ", StringComparison.Ordinal));

        Assert.Contains($"Rook (fell on {Map("the_field").Name}; the drake flew, grown)", fallen);
        Assert.Contains($"{names["teodor"]} (fell on {Map("the_field").Name})", fallen);
    }
}
