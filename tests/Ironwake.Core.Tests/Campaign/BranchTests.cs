using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 633 slice 1 (DESIGN section 14, the branch): the raid's camp offers two claimants, Keziah
/// and Rook, both off the roster before it. <c>pick &lt;unit&gt;</c> fills the last seat, final; the
/// pick joins like a joiner and the one passed on rides home and never joins. The march is refused
/// until the pick is made. A map slot naming an absent claimant is a bare slot.
/// </summary>
public class BranchTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static string Dir => Fixture.RealContentDirectory();

    private static MapDefinition Map(string id) => MapFiles.Load(MapFiles.CampaignPath(Dir, Content, id), Content);

    private static CampaignRecord AtTheRaid() => CampaignRecord.StartAt(Content, 633, "ironwake_raid");

    private static BattleState Won(CampaignRecord record, MapDefinition map)
    {
        var opening = record.Begin(map, Content);
        return opening with { Units = ValueList<BattleUnit>.From(opening.UnitsOf(Side.Player)), Turn = 4, History = ValueList<BattleState>.Of(opening) };
    }

    [Fact]
    public void TheRaidOffersKeziahAndRookAndNeitherIsOnTheRosterBeforeIt()
    {
        Assert.Equal(new[] { "keziah", "rook" }, Content.Campaign.Maps[Content.Campaign.MapIndexOf("ironwake_raid")].Branch);
        Assert.Empty(Content.Campaign.Maps[Content.Campaign.MapIndexOf("ironwake_raid")].Joins);
        foreach (var record in new[] { CampaignRecord.Start(Content, 1), CampaignRecord.StartAt(Content, 1, "harrow_weir"), AtTheRaid() })
        {
            Assert.Null(record.Find("keziah"));
            Assert.Null(record.Find("rook"));
        }

        Assert.DoesNotContain(AtTheRaid().Present(Content), u => u.Id is "keziah" or "rook");
    }

    [Fact]
    public void TheMarchIsRefusedUntilTheSeatIsFilled()
    {
        var raid = Map("ironwake_raid");

        Assert.Equal("the seat is not filled; pick keziah or rook first", AtTheRaid().MarchRefusal(raid, Content));
        Assert.Null(AtTheRaid().PickClaimant("rook", Content).Record.MarchRefusal(raid, Content));
    }

    [Theory]
    [InlineData("keziah", "rook")]
    [InlineData("rook", "keziah")]
    public void ThePickJoinsAtTheRaidAndThePassedNeverJoins(string pick, string passed)
    {
        var result = AtTheRaid().PickClaimant(pick, Content);

        Assert.True(result.Accepted);
        Assert.Equal(pick, result.Record.Pick);
        Assert.Equal(passed, result.Record.Passed(Content));
        Assert.Contains(result.Record.Present(Content), u => u.Id == pick);
        Assert.DoesNotContain(result.Record.Present(Content), u => u.Id == passed);

        var after = result.Record.AfterBattle(Won(result.Record, Map("ironwake_raid")), Content);

        Assert.NotNull(after.Find(pick));
        Assert.Null(after.Find(passed));
        Assert.DoesNotContain(after.Present(Content), u => u.Id == passed);
        Assert.Equal(pick, after.Pick);
    }

    [Theory]
    [InlineData("Keziah")]
    [InlineData("KEZIAH")]
    public void AClaimantIsPickedByNameInAnyCase(string typed)
    {
        Assert.Equal("keziah", AtTheRaid().PickClaimant(typed, Content).Record.Pick);
    }

    [Fact]
    public void ThePickIsFinal()
    {
        var picked = AtTheRaid().PickClaimant("keziah", Content).Record;

        var again = picked.PickClaimant("rook", Content);

        Assert.False(again.Accepted);
        Assert.Equal("the pick is made: Keziah, and it is final", again.Text);
        Assert.Equal("keziah", again.Record.Pick);
    }

    [Fact]
    public void ANameThatIsNotAClaimantIsRefused()
    {
        var result = AtTheRaid().PickClaimant("wren", Content);

        Assert.False(result.Accepted);
        Assert.Equal("'wren' is not a claimant; pick keziah or rook", result.Text);
        Assert.Null(result.Record.Pick);
    }

    [Fact]
    public void APickAtACampWithNoBranchIsRefused()
    {
        var result = CampaignRecord.StartAt(Content, 1, "harrow_weir").PickClaimant("rook", Content);

        Assert.False(result.Accepted);
        Assert.Equal("no claimant is offered at this camp", result.Text);
    }

    [Fact]
    public void ThePickSaysWhoTakesTheSeatAtWhatLevelAndWhoRidesHome()
    {
        var record = AtTheRaid();
        var level = Math.Max(Content.Unit("rook").Level, record.JoinLevel(Content));

        Assert.Equal($"Rook takes the seat at level {level}; Keziah rides home", record.PickClaimant("rook", Content).Text);
    }

    [Fact]
    public void ThePickRoundTripsThroughTheSave()
    {
        var picked = AtTheRaid().PickClaimant("keziah", Content).Record;

        Assert.Equal("keziah", ProtocolJson.ReadCampaign(ProtocolJson.Campaign(picked), Content).Pick);
        Assert.Null(ProtocolJson.ReadCampaign(ProtocolJson.Campaign(AtTheRaid()), Content).Pick);
        Assert.DoesNotContain("\"pick\"", ProtocolJson.Campaign(AtTheRaid()));
    }

    [Fact]
    public void ASavedPickThatIsNotAClaimantIsRefused()
    {
        var json = ProtocolJson.Campaign(AtTheRaid().PickClaimant("rook", Content).Record).Replace("\"pick\":\"rook\"", "\"pick\":\"wren\"");

        var e = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCampaign(json, Content));

        Assert.Contains("pick 'wren' is not a claimant the campaign offers", e.Message);
    }

    [Fact]
    public void ASlotNamingAnAbsentClaimantIsABareSlot()
    {
        var harrow = Map("harrow_weir");
        Assert.Contains(harrow.Placements.OfType<PlayerPlacement>(), p => p.RecruitId == "keziah");
        var record = CampaignRecord.StartAt(Content, 1, "harrow_weir");

        var opening = record.Begin(harrow, Content);

        Assert.DoesNotContain(opening.UnitsOf(Side.Player), u => u.Id == "keziah");
        Assert.Equal(harrow.Placements.OfType<PlayerPlacement>().Count(), opening.UnitsOf(Side.Player).Count());
    }

    [Fact]
    public void AFallenClaimantsNamedSlotStaysEmpty()
    {
        var record = AtTheRaid().PickClaimant("keziah", Content).Record;
        record = record with { MapIndex = Content.Campaign.MapIndexOf("the_field"), Fallen = record.Fallen.Add("keziah") };

        var field = Map("the_field");
        var slot = field.Placements.OfType<PlayerPlacement>().Single(p => p.RecruitId == "keziah").At;

        var opening = record.Begin(field, Content);

        Assert.DoesNotContain(opening.UnitsOf(Side.Player), u => u.Id == "keziah");
        Assert.DoesNotContain(opening.UnitsOf(Side.Player), u => u.At == slot);
    }

    [Fact]
    public void TheCampPrintsTheBranchBeforeThePickAndWhoTookTheSeatAfter()
    {
        Assert.Equal(
            new[]
            {
                "The last seat: Keziah (Reaver) or Rook (Skyrider) (pick <unit>). The one passed on rides home.",
                "Keziah: \"Everyone here is offering you a promise. Mine's hungry, and you'll hear it ask.\"",
                "Rook: \"She's the last one. Pick me and you're the second person alive who knows her name.\"",
                "They come back before the keep. Turned, they join only if a bed is free, and a death never frees one.",
            },
            CampaignSession.BranchLines(AtTheRaid(), Content));
        Assert.Equal(new[] { "The seat: Rook. Keziah rode home." }, CampaignSession.BranchLines(AtTheRaid().PickClaimant("rook", Content).Record, Content));
        Assert.Empty(CampaignSession.BranchLines(CampaignRecord.StartAt(Content, 1, "harrow_weir"), Content));
    }

    [Fact]
    public void TheClientOffersBothClaimantsUntilOneIsPicked()
    {
        var client = new CampaignClient(Content, Dir, AtTheRaid());

        var rows = CampActions.For(client, null, Array.Empty<string>()).Where(r => r.Command.StartsWith("pick ", StringComparison.Ordinal)).ToList();

        Assert.Equal(new[] { "pick keziah", "pick rook" }, rows.Select(r => r.Command));
        Assert.True(client.Pick("keziah"));
        Assert.DoesNotContain(CampActions.For(client, null, Array.Empty<string>()), r => r.Command.StartsWith("pick ", StringComparison.Ordinal));
    }

    /// <summary>The shipped content's files with <paramref name="maps"/> as the campaign's maps.</summary>
    private static ContentFiles With(string maps) =>
        ContentSerializer.Write(Content) with { Scenes = null, Campaign = new ContentFile(ContentFiles.CampaignName, $$"""{ "startingPurse": 0, "certificationPrice": 0, "maps": [ {{maps}} ] }""") };

    [Fact]
    public void ABranchRoundTripsThroughTheSerializer()
    {
        var content = ContentLoader.Parse(With("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"] }"""));

        Assert.Equal(ValueList<string>.Of("keziah", "rook"), ContentLoader.Parse(ContentSerializer.Write(content)).Campaign.Maps[0].Branch);
    }

    [Theory]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["rook"] }""", "one", "must name exactly two claimants")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "nobody"] }""", "one", "'nobody' is not in the cast")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["rook", "rook"] }""", "one", "arrives or joins on more than one map")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["captain", "rook"] }""", "one", "is the captain")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "joins": ["rook"] }, { "map": "two", "reward": 0, "stock": [], "branch": ["keziah", "rook"] }""", "two", "arrives or joins on more than one map")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"] }, { "map": "two", "reward": 0, "stock": [], "branch": ["pell", "wren"] }""", "two", "only one map may offer a branch")]
    public void ABadBranchIsRefusedNamingTheMapAndTheField(string maps, string entry, string message)
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(With(maps)));

        Assert.Equal((ContentFiles.CampaignName, entry, "branch"), (e.File, e.Entry, e.Field));
        Assert.Contains(message, e.Message);
    }

    [Fact]
    public void EachClaimantsPitchPrintsInBranchOrderAndNotAfterThePick()
    {
        var content = ContentLoader.Parse(With("""{ "map": "one", "reward": 0, "stock": [], "branch": ["rook", "keziah"], "pitch": { "keziah": "Something hungry.", "rook": "The last one." } }"""));
        var record = CampaignRecord.StartAt(content, 0, "one");

        var lines = CampaignSession.BranchLines(record, content);

        Assert.Equal(new[] { "Rook: \"The last one.\"", "Keziah: \"Something hungry.\"" }, lines.Skip(1).Take(2));
        Assert.DoesNotContain(CampaignSession.BranchLines(record.PickClaimant("rook", content).Record, content), l => l.Contains("The last one.", StringComparison.Ordinal));
    }

    [Fact]
    public void APitchRoundTripsThroughTheSerializer()
    {
        var content = ContentLoader.Parse(With("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"], "pitch": { "rook": "The last one.", "keziah": "Something hungry." } }"""));

        Assert.Equal(ValueList<string>.Of("Something hungry.", "The last one."), ContentLoader.Parse(ContentSerializer.Write(content)).Campaign.Maps[0].Pitch);
    }

    [Theory]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "pitch": { "rook": "Hi." } }""", "pitch", "needs a branch")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"], "pitch": { "rook": "Hi.", "pell": "Hi." } }""", "pitch", "'pell' is not one of the branch's claimants")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"], "pitch": { "rook": "Hi." } }""", "pitch", "'keziah' has none")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"], "pitch": { "rook": "Hi.", "keziah": " Hi." } }""", "pitch.keziah", "no leading or trailing space")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"], "pitch": { "rook": "Hi.", "keziah": 3 } }""", "pitch.keziah", "non-blank text")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"], "pitch": { "rook": "Hi.\u00e9", "keziah": "Hi." } }""", "pitch.rook", "printable ASCII")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"], "pitch": { "rook": "a a a a a a a a a a a a a a a a a a a a a a a a a a", "keziah": "Hi." } }""", "pitch.rook", "at most 25 words, not 26")]
    public void ABadPitchIsRefusedNamingTheMapAndTheField(string maps, string field, string message)
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(With(maps)));

        Assert.Equal((ContentFiles.CampaignName, "one", field), (e.File, e.Entry, e.Field));
        Assert.Contains(message, e.Message);
    }
}
