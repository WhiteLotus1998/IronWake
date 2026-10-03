using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 633 slice 3 (DESIGN section 14, side characters met by choice): a map's <c>meets</c> keeps a
/// side character off the roster until that map's camp, where <c>meet &lt;unit&gt;</c> takes them on
/// like a joiner if a bed is free, final and one a map. Not met, they never join. Ansgar is met on
/// the field (STORY draft 6, map 9). The record and the save carry who was met.
/// </summary>
public class MeetingTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static string Dir => Fixture.RealContentDirectory();

    private static MapDefinition Map(string id) => MapFiles.Load(MapFiles.CampaignPath(Dir, Content, id), Content);

    private static CampaignRecord AtTheField() => CampaignRecord.StartAt(Content, 633, "the_field", pick: "keziah");

    /// <summary>The record with every free bed held by a fallen stand-in.</summary>
    private static CampaignRecord NoBedFree(CampaignRecord record) =>
        record with { Fallen = ValueList<string>.From(Enumerable.Range(0, record.FreeBeds(Content)!.Value).Select(i => "lost" + i)) };

    /// <summary>A won battle from <paramref name="opening"/>: the enemy gone, every player unit standing.</summary>
    private static BattleState Won(BattleState opening) =>
        opening with { Units = ValueList<BattleUnit>.From(opening.UnitsOf(Side.Player)), Turn = 4, History = ValueList<BattleState>.Of(opening) };

    [Fact]
    public void TheFieldOffersAnsgarAndHeIsOffTheRosterUntilThen()
    {
        Assert.Equal(new[] { "ansgar" }, Content.Campaign.Maps[Content.Campaign.MapIndexOf("the_field")].Meets);
        Assert.Single(Content.Campaign.Maps, m => m.Meets.Count > 0);
        Assert.Equal(Content.Campaign.MapIndexOf("the_field"), Content.Campaign.ArrivalIndex("ansgar"));
        Assert.Null(CampaignRecord.Start(Content, 1).Find("ansgar"));
        Assert.Null(AtTheField().Find("ansgar"));
        Assert.DoesNotContain(AtTheField().Present(Content), u => u.Id == "ansgar");
    }

    [Fact]
    public void ASlotNamingAnUnmetSideCharacterIsABareSlot()
    {
        var grange = Map("sallow_grange");
        Assert.Contains(grange.Placements.OfType<PlayerPlacement>(), p => p.RecruitId == "ansgar");
        var record = CampaignRecord.StartAt(Content, 1, "sallow_grange", pick: "rook");

        var opening = record.Begin(grange, Content);

        Assert.DoesNotContain(opening.UnitsOf(Side.Player), u => u.Id == "ansgar");
        Assert.Equal(grange.Placements.OfType<PlayerPlacement>().Count(), opening.UnitsOf(Side.Player).Count());
    }

    [Fact]
    public void MeetJoinsTheSideCharacterForTheMapAtNoLessThanTheJoinLevel()
    {
        var record = AtTheField();
        record = record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.ScaledTo(5, Content.Class(u.ClassId)))) };

        var met = record.Meet("ansgar", Content);

        Assert.True(met.Accepted, met.Text);
        Assert.Equal("Ansgar joins the company at level 5", met.Text);
        Assert.Equal(new[] { "ansgar" }, met.Record.Met);
        Assert.Equal(5, met.Record.Present(Content).Single(u => u.Id == "ansgar").Level);
    }

    [Fact]
    public void AMetSideCharacterStaysOnTheRosterAfterTheMap()
    {
        var met = AtTheField().Meet("ansgar", Content).Record;
        var opening = met.Begin(Map("the_field"), Content);

        var after = met.AfterBattle(Won(opening), Content);

        Assert.NotNull(after.Find("ansgar"));
        Assert.Equal(after.Roster.Select(u => u.Id), Content.Cast.Select(u => u.Id).Where(id => after.Find(id) is not null));
    }

    [Fact]
    public void ASideCharacterNotMetNeverJoins()
    {
        var record = AtTheField();
        var opening = record.Begin(Map("the_field"), Content);

        var after = record.AfterBattle(Won(opening), Content);

        Assert.Null(after.Find("ansgar"));
        Assert.Empty(after.Met);
        Assert.Equal("nobody is met at this camp", after.Meet("ansgar", Content).Text);
    }

    [Theory]
    [InlineData("ANSGAR")]
    [InlineData("Ansgar")]
    public void ASideCharacterIsMetByNameInAnyCase(string typed)
    {
        Assert.True(AtTheField().Meet(typed, Content).Accepted);
    }

    [Fact]
    public void TheMeetingIsFinalAndOneAMap()
    {
        var met = AtTheField().Meet("ansgar", Content).Record;

        var again = met.Meet("ansgar", Content);

        Assert.False(again.Accepted);
        Assert.Equal("the meeting is made: Ansgar is with the company", again.Text);
        Assert.Same(met, again.Record);
    }

    [Fact]
    public void AMeetingIsRefusedForANameNotMetHereAndAtACampWithNoMeeting()
    {
        Assert.Equal("'wren' is not met here; meet ansgar", AtTheField().Meet("wren", Content).Text);
        Assert.Equal("nobody is met at this camp", CampaignRecord.StartAt(Content, 1, "the_tollgate").Meet("ansgar", Content).Text);
        Assert.False(CampaignRecord.StartAt(Content, 1, "the_tollgate").Meet("ansgar", Content).Accepted);
    }

    [Fact]
    public void AMeetingIsRefusedWhenNoBedIsFree()
    {
        var full = NoBedFree(AtTheField());

        var refused = full.Meet("ansgar", Content);

        Assert.False(refused.Accepted);
        Assert.Equal("no bed free: Ansgar will not join", refused.Text);
        Assert.Empty(refused.Record.Met);
    }

    [Fact]
    public void AMetSideCharacterHoldsTheBedATurnedClaimantWouldHaveNeeded()
    {
        var record = AtTheField();
        record = record with { Fallen = ValueList<string>.From(Enumerable.Range(0, record.FreeBeds(Content)!.Value - 1).Select(i => "lost" + i)) };

        var met = record.Meet("ansgar", Content).Record;
        var after = met.AfterBattle(Won(met.Begin(Map("the_field"), Content)), Content);

        Assert.NotNull(after.Find("ansgar"));
        Assert.Equal(0, after.FreeBeds(Content));
    }

    [Fact]
    public void WhoWasMetRoundTripsThroughTheSave()
    {
        var met = AtTheField().Meet("ansgar", Content).Record;

        Assert.Equal(new[] { "ansgar" }, ProtocolJson.ReadCampaign(ProtocolJson.Campaign(met), Content).Met);
        Assert.Empty(ProtocolJson.ReadCampaign(ProtocolJson.Campaign(AtTheField()), Content).Met);
        Assert.DoesNotContain("\"met\"", ProtocolJson.Campaign(AtTheField()));
    }

    [Fact]
    public void ASavedMeetingWithSomeoneNoMapOffersIsRefused()
    {
        var json = ProtocolJson.Campaign(AtTheField().Meet("ansgar", Content).Record).Replace("\"met\":[\"ansgar\"]", "\"met\":[\"wren\"]");

        var e = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCampaign(json, Content));

        Assert.Contains("met 'wren' is not a side character the campaign offers", e.Message);
    }

    [Fact]
    public void ACampaignOpeningPastTheMeetingHasMadeIt()
    {
        var record = CampaignRecord.StartAt(Content, 1, "ironwake_keep", pick: "rook");

        Assert.NotNull(record.Find("ansgar"));
        Assert.Equal(new[] { "ansgar" }, record.Met);
    }

    [Fact]
    public void TheCampPrintsTheMeetingWithTheBedsAndWhoWasMetAfter()
    {
        var record = AtTheField();
        var level = Math.Max(Content.Unit("ansgar").Level, record.JoinLevel(Content));
        var beds = $"{record.BedsTaken + record.Present(Content).Count - record.Roster.Count}/{record.Beds(Content)}";

        Assert.Equal(
            new[]
            {
                $"Met on the road: Ansgar (Outrider, level {level}) (meet <unit>; one, final).",
                $"Ansgar joins only if a bed is free (beds: {beds}), and a death never frees one. Not met, he goes on alone.",
            },
            CampaignSession.MeetingLines(record, Content));
        Assert.Equal(new[] { "Met: Ansgar, with the company." }, CampaignSession.MeetingLines(record.Meet("ansgar", Content).Record, Content));
        Assert.Equal(new[] { $"Met on the road: Ansgar (Outrider, level {level}). No bed free: Ansgar will not join." }, CampaignSession.MeetingLines(NoBedFree(record), Content));
        Assert.Empty(CampaignSession.MeetingLines(CampaignRecord.StartAt(Content, 1, "the_tollgate"), Content));
    }

    [Fact]
    public void TheClientOffersTheMeetingUntilItIsMade()
    {
        var client = new CampaignClient(Content, Dir, AtTheField());

        var rows = CampActions.For(client, null, Array.Empty<string>()).Where(r => r.Command.StartsWith("meet ", StringComparison.Ordinal)).ToList();

        Assert.Equal(new[] { "meet ansgar" }, rows.Select(r => r.Command));
        Assert.True(client.Meet("ansgar"));
        Assert.DoesNotContain(CampActions.For(client, null, Array.Empty<string>()), r => r.Command.StartsWith("meet ", StringComparison.Ordinal));
    }

    /// <summary>The shipped content's files with <paramref name="maps"/> as the campaign's maps.</summary>
    private static ContentFiles With(string maps) =>
        ContentSerializer.Write(Content) with { Campaign = new ContentFile(ContentFiles.CampaignName, $$"""{ "startingPurse": 0, "certificationPrice": 0, "maps": [ {{maps}} ] }""") };

    [Fact]
    public void AMeetingRoundTripsThroughTheSerializer()
    {
        var content = ContentLoader.Parse(With("""{ "map": "one", "reward": 0, "stock": [], "meets": ["ansgar", "dunstan"] }"""));

        Assert.Equal(ValueList<string>.Of("ansgar", "dunstan"), ContentLoader.Parse(ContentSerializer.Write(content)).Campaign.Maps[0].Meets);
    }

    [Theory]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "meets": ["ansgar", "dunstan", "pell"] }""", "one", "may name one or two side characters")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "meets": ["nobody"] }""", "one", "'nobody' is not in the cast")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "meets": ["captain"] }""", "one", "is the captain")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "joins": ["ansgar"] }, { "map": "two", "reward": 0, "stock": [], "meets": ["ansgar"] }""", "two", "arrives or joins on more than one map")]
    public void ABadMeetingIsRefusedNamingTheMapAndTheField(string maps, string entry, string message)
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(With(maps)));

        Assert.Equal((ContentFiles.CampaignName, entry, "meets"), (e.File, e.Entry, e.Field));
        Assert.Contains(message, e.Message);
    }
}
