using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 763 (rounds 228 and 234): a recruit who joins below the living company's median joins at
/// it. Rook joins at the raid's camp (<c>joins</c> in <c>campaign.json</c>), placed on no tile of her
/// own, raised to the median whether or not she deploys, never lowered. The rule is join-time: the
/// bench is never raised.
/// </summary>
public class JoinTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static MapDefinition Map(string id) => MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), Content, id), Content);

    private static int RaidIndex => Content.Campaign.MapIndexOf("ironwake_raid");

    /// <summary>A campaign at the raid's camp whose living members stand at <paramref name="levels"/>, in roster order, the rest left as they are.</summary>
    private static CampaignRecord AtTheRaid(params int[] levels)
    {
        var start = CampaignRecord.StartAt(Content, 7, "ironwake_raid");
        var roster = start.Roster.Select((u, i) => i < levels.Length ? u.ScaledTo(levels[i], Content.Class(u.ClassId)) : u);
        return start with { Roster = ValueList<Unit>.From(roster.Take(Math.Max(levels.Length, 1))) };
    }

    private static BattleState Won(CampaignRecord record, MapDefinition map, Func<BattleUnit, bool> keep)
    {
        var opening = record.Begin(map, Content);
        var units = opening.UnitsOf(Side.Player).Where(keep);
        return opening with { Units = ValueList<BattleUnit>.From(units), Turn = 4, History = ValueList<BattleState>.Of(opening) };
    }

    [Fact]
    public void RookJoinsAtTheRaidsCampAndIsOffTheRosterBeforeIt()
    {
        Assert.Equal(new[] { "rook" }, Content.Campaign.Maps[RaidIndex].Joins);
        Assert.Equal(RaidIndex, Content.Campaign.ArrivalIndex("rook"));
        Assert.Null(CampaignRecord.Start(Content, 7).Find("rook"));
        Assert.Null(CampaignRecord.StartAt(Content, 7, "harrow_weir").Find("rook"));
        Assert.Contains(AtTheRaid(1).Present(Content), u => u.Id == "rook");
    }

    [Theory]
    [InlineData(new[] { 5 }, 5)]
    [InlineData(new[] { 7, 3, 1 }, 3)]
    [InlineData(new[] { 6, 1, 4, 2 }, 3)]
    [InlineData(new[] { 6, 5, 1, 2 }, 3)]
    [InlineData(new[] { 8, 8, 1, 1, 1 }, 1)]
    public void TheJoinLevelIsTheLivingMediansRoundedDown(int[] levels, int expected)
    {
        Assert.Equal(expected, AtTheRaid(levels).JoinLevel(Content));
    }

    [Fact]
    public void AJoinerBelowTheMedianIsRaisedToItOnTheAverageGrowthAndTheCampSaysSo()
    {
        var record = AtTheRaid(7, 5, 5, 4, 1);
        var cast = Content.Unit("rook");

        var rook = record.Present(Content).Single(u => u.Id == "rook");

        Assert.Equal(cast.ScaledTo(5, Content.Class("skyrider")), rook);
        Assert.Equal(5, rook.Level);
        Assert.Equal(new[] { "Rook joins at level 5 (the company's median)" }, CampaignSession.JoinLines(record, Content));
    }

    [Fact]
    public void AJoinerIsNeverLoweredAndAtTheMedianNothingIsPrinted()
    {
        var record = AtTheRaid(1, 1, 1);

        Assert.Equal(Content.Unit("rook"), record.Present(Content).Single(u => u.Id == "rook"));
        Assert.Empty(record.RaisedOnJoining(Content));
        Assert.Empty(CampaignSession.JoinLines(record, Content));
    }

    [Fact]
    public void AJoinerIsRaisedWhetherOrNotSheDeploys()
    {
        var record = AtTheRaid(6, 6, 6, 6, 6, 6, 6);
        var raid = Map("ironwake_raid");

        Assert.DoesNotContain("rook", record.Deployment(raid, Content));
        var after = record.AfterBattle(Won(record, raid, _ => true), Content);

        Assert.Equal(6, after.Find("rook")!.Level);
    }

    [Fact]
    public void ABenchedMemberIsNeverRaised()
    {
        var record = AtTheRaid(6, 6, 6, 1);

        Assert.Equal(1, record.Present(Content).Single(u => u.Id == record.Roster[3].Id).Level);
    }

    [Fact]
    public void MaudOnTheMillJoinsAtTheCompanysMedianOfOne()
    {
        var mill = CampaignRecord.StartAt(Content, 7, "the_mill");

        Assert.Equal(1, mill.JoinLevel(Content));
        Assert.Equal(Content.Unit("maud"), mill.Present(Content).Single(u => u.Id == "maud"));
    }

    /// <summary>The shipped content's files with <paramref name="maps"/> as the campaign's maps.</summary>
    private static ContentFiles With(string maps) =>
        ContentSerializer.Write(Content) with { Campaign = new ContentFile(ContentFiles.CampaignName, $$"""{ "startingPurse": 0, "certificationPrice": 0, "maps": [ {{maps}} ] }""") };

    [Fact]
    public void AJoinerRoundTripsThroughTheSerializer()
    {
        var content = ContentLoader.Parse(With("""{ "map": "one", "reward": 0, "stock": [], "joins": ["rook"] }"""));

        var reloaded = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.Equal(ValueList<string>.Of("rook"), reloaded.Campaign.Maps[0].Joins);
    }

    [Fact]
    public void AJoinerOutsideTheCastIsRefused()
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(With("""{ "map": "one", "reward": 0, "stock": [], "joins": ["nobody"] }""")));

        Assert.Equal((ContentFiles.CampaignName, "one", "joins"), (e.File, e.Entry, e.Field));
        Assert.Contains("'nobody' is not in the cast", e.Message);
    }

    [Fact]
    public void TheCaptainCannotJoin()
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(With($$"""{ "map": "one", "reward": 0, "stock": [], "joins": ["{{Content.Cast[0].Id}}"] }""")));

        Assert.Equal(("one", "joins"), (e.Entry, e.Field));
        Assert.Contains("is the captain", e.Message);
    }

    [Fact]
    public void ARecruitWhoArrivesAndJoinsIsRefused()
    {
        var both = """{ "map": "one", "reward": 0, "stock": [], "arrives": ["rook"] }, { "map": "two", "reward": 0, "stock": [], "joins": ["rook"] }""";

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(With(both)));

        Assert.Equal(("two", "joins"), (e.Entry, e.Field));
        Assert.Contains("arrives or joins on more than one map", e.Message);
    }
}
