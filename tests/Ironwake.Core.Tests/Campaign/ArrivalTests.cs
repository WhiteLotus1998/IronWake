using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Arrivals (issue 632, DESIGN section 14): a recruit a <c>campaign.json</c> map names in
/// <c>arrives</c> is off the roster until that map, joins its battle, and stays if it stands. Maud
/// arrives on The Mill, map 2, where she is the protected unit. The loader refuses an arrival
/// outside the cast, the captain, and a recruit arriving twice.
/// </summary>
public class ArrivalTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static MapDefinition Map(string id) => MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), Content, id), Content);

    /// <summary>The record after Starting Alone is won with nobody touched: The Mill is next.</summary>
    private static CampaignRecord AtTheMill()
    {
        var start = CampaignRecord.Start(Content, 7);
        var opening = start.Begin(Map("starting_alone"), Content);
        var won = opening with { Units = ValueList<BattleUnit>.From(opening.UnitsOf(Side.Player)), Turn = 4, History = ValueList<BattleState>.Of(opening) };
        return start.AfterBattle(won, Content);
    }

    private static BattleState Won(CampaignRecord record, MapDefinition map, Func<BattleUnit, bool>? keep = null)
    {
        var opening = record.Begin(map, Content);
        var units = opening.UnitsOf(Side.Player).Where(u => keep is null || keep(u));
        return opening with { Units = ValueList<BattleUnit>.From(units), Turn = 4, History = ValueList<BattleState>.Of(opening) };
    }

    [Fact]
    public void MaudArrivesOnTheMillTheCampaignsSecondMap()
    {
        Assert.Equal(1, Content.Campaign.ArrivalIndex("maud"));
        Assert.Equal("the_mill", Content.Campaign.Maps[1].MapId);
        Assert.Equal(-1, Content.Campaign.ArrivalIndex("wren"));
    }

    [Fact]
    public void AnArrivalIsOffTheRosterBeforeItsMap()
    {
        var start = CampaignRecord.Start(Content, 7);
        var mill = AtTheMill();

        Assert.Null(start.Find("maud"));
        Assert.Null(mill.Find("maud"));
        Assert.Equal("no unit 'maud' on the roster", mill.Bench("maud", Map("the_mill")).Text);
    }

    [Fact]
    public void AnArrivalJoinsTheBattleOnItsMap()
    {
        var mill = AtTheMill();

        Assert.Equal(new[] { "captain", "maud" }, mill.Deployment(Map("the_mill"), Content));
        Assert.Contains(mill.Present(Content), u => u.Id == "maud");
    }

    [Fact]
    public void AnArrivalWhoStandsStaysOnTheRosterInCastOrder()
    {
        var mill = AtTheMill();

        var after = mill.AfterBattle(Won(mill, Map("the_mill")), Content);

        Assert.NotNull(after.Find("maud"));
        Assert.Equal(Content.Cast.Select(u => u.Id), after.Roster.Select(u => u.Id));
        Assert.Empty(after.Fallen);
    }

    [Fact]
    public void AnArrivalWhoFallsIsFallenLikeAnyoneDeployed()
    {
        var mill = AtTheMill();
        var unprotected = Map("the_mill") with { ProtectId = null };

        var after = mill.AfterBattle(Won(mill, unprotected, u => u.Id != "maud"), Content);

        Assert.Null(after.Find("maud"));
        Assert.Equal(ValueList<string>.Of("maud"), after.Fallen);
    }

    [Fact]
    public void StartingAtAMapPutsEveryEarlierArrivalOnTheRosterAndLeavesTheMapsOwnToJoinIt()
    {
        var atMill = CampaignRecord.StartAt(Content, 7, "the_mill");
        var atFord = CampaignRecord.StartAt(Content, 7, "saltmarsh_ford");

        Assert.Null(atMill.Find("maud"));
        Assert.Equal(new[] { "captain", "maud" }, atMill.Deployment(Map("the_mill"), Content));
        Assert.NotNull(atFord.Find("maud"));
    }

    [Fact]
    public void EveryArrivalIsPlacedByNameOnItsArrivalMap()
    {
        foreach (var entry in Content.Campaign.Maps.Where(m => m.Arrives.Count > 0))
        {
            var map = Map(entry.MapId);
            foreach (var id in entry.Arrives)
            {
                Assert.Contains(map.Placements.OfType<PlayerPlacement>(), p => p.Slot == PlayerSlot.NamedRecruit && p.RecruitId == id);
            }
        }
    }

    [Fact]
    public void TheMillProtectsMaud()
    {
        Assert.Equal("maud", Map("the_mill").ProtectId);
    }

    /// <summary>The shipped content's files with <paramref name="maps"/> as the campaign's maps.</summary>
    private static ContentFiles With(string maps) =>
        ContentSerializer.Write(Content) with { Campaign = new ContentFile(ContentFiles.CampaignName, $$"""{ "startingPurse": 0, "certificationPrice": 0, "maps": [ {{maps}} ] }""") };

    private static string Cast(int index) => Content.Cast[index].Id;

    [Fact]
    public void AnArrivalRoundTripsThroughTheSerializer()
    {
        var content = ContentLoader.Parse(With($$"""{ "map": "one", "reward": 0, "stock": [], "arrives": ["{{Cast(1)}}"] }"""));

        var reloaded = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.Equal(content.Campaign, reloaded.Campaign);
        Assert.Equal(ValueList<string>.Of(Cast(1)), reloaded.Campaign.Maps[0].Arrives);
    }

    [Fact]
    public void AnArrivalOutsideTheCastIsRefused()
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(With("""{ "map": "one", "reward": 0, "stock": [], "arrives": ["nobody"] }""")));

        Assert.Equal((ContentFiles.CampaignName, "one", "arrives"), (e.File, e.Entry, e.Field));
        Assert.Contains("'nobody' is not in the cast", e.Message);
    }

    [Fact]
    public void TheCaptainCannotArrive()
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(With($$"""{ "map": "one", "reward": 0, "stock": [], "arrives": ["{{Cast(0)}}"] }""")));

        Assert.Contains("is the captain, who leads from the first map", e.Message);
    }

    [Fact]
    public void ARecruitArrivingTwiceIsRefused()
    {
        var twice = $$"""{ "map": "one", "reward": 0, "stock": [], "arrives": ["{{Cast(1)}}"] }, { "map": "two", "reward": 0, "stock": [], "arrives": ["{{Cast(1)}}"] }""";

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(With(twice)));

        Assert.Equal(("two", "arrives"), (e.Entry, e.Field));
        Assert.Contains("arrives on more than one map", e.Message);
    }
}
