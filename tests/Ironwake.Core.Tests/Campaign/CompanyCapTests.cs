using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 689: the company holds at most 12 living members, the fallen keeping their beds and never
/// counting toward it; a map fields what its <c>deploy:</c> header says, 6 by default, and the
/// keep's <c>deploy: all</c> fields the whole living company on at least 12 start tiles.
/// </summary>
public class CompanyCapTests
{
    private static GameContent Content => MapFixture.Content;

    private static int IndexOf(string id) => Content.Campaign.Maps.Select(m => m.MapId).ToList().IndexOf(id);

    private static MapDefinition Map(string id) => MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), Content, id), Content);

    private static GameContent WithBeds(int beds) => Content with { Campaign = Content.Campaign with { Keep = Content.Campaign.Keep with { Beds = beds } } };

    /// <summary>A record whose next map is The Mill, where Maud arrives, its roster padded with copies of a recruit to <paramref name="living"/>.</summary>
    private static CampaignRecord AtTheMill(int living)
    {
        var start = CampaignRecord.Start(Content, 689) with { MapIndex = IndexOf("the_mill") };
        var extra = Enumerable.Range(1, living - start.Roster.Count).Select(i => start.Roster[1] with { Id = "spare" + i });
        return start with { Roster = ValueList<Unit>.From(start.Roster.Concat(extra)) };
    }

    /// <summary>Old Mill Road with <paramref name="header"/> added and <paramref name="bare"/> bare recruit slots on its bottom row.</summary>
    private static string Deployed(string header, int bare)
    {
        var slots = string.Concat(Enumerable.Range(0, bare).Select(x => $"P recruit {x},9\n"));
        var text = MapFixture.OldMillRoad.Replace("P recruit:wren 2,8\n", "P recruit:wren 2,8\n" + slots);
        return header.Length == 0 ? text : text.Replace("enemy_level: 1\n", "enemy_level: 1\n" + header + "\n");
    }

    [Fact]
    public void TheCapRefusesTheThirteenthWithTheLine()
    {
        var full = AtTheMill(CampaignRecord.CompanyCap);
        var roomy = WithBeds(20);

        Assert.Equal(new[] { "maud" }, full.TurnedAway(roomy));
        Assert.DoesNotContain(full.Present(roomy), u => u.Id == "maud");
        Assert.Equal(new[] { "company full (12): Maud will not join" }, CampaignSession.TurnedAwayLines(full, roomy));
    }

    [Fact]
    public void TheEleventhLivingMemberLeavesRoomForATwelfth()
    {
        var eleven = AtTheMill(CampaignRecord.CompanyCap - 1);

        Assert.Empty(eleven.TurnedAway(WithBeds(20)));
        Assert.Contains(eleven.Present(WithBeds(20)), u => u.Id == "maud");
    }

    [Fact]
    public void AFallenMemberDoesNotCountTowardTheCapButKeepsTheirBed()
    {
        var full = AtTheMill(CampaignRecord.CompanyCap);
        var oneFell = full with { Roster = full.Roster.RemoveAt(full.Roster.Count - 1), Fallen = ValueList<string>.Of(full.Roster[^1].Id) };

        Assert.Equal(CampaignRecord.CompanyCap - 1, oneFell.Living);
        Assert.Equal(full.BedsTaken, oneFell.BedsTaken);
        Assert.Empty(oneFell.TurnedAway(WithBeds(20)));
        Assert.Equal(new[] { "no bed free: Maud will not join" }, CampaignSession.TurnedAwayLines(oneFell, WithBeds(CampaignRecord.CompanyCap)));
    }

    [Fact]
    public void TheRosterHeadingCountsTheLivingAgainstTheCap()
    {
        var start = CampaignRecord.Start(Content, 1);

        Assert.Equal($"Roster: company {start.Roster.Count}/12", CampaignSession.RosterLines(start, Content)[0]);
    }

    [Fact]
    public void TheBunkRoomAddsTwoBeds()
    {
        Assert.Equal(new KeepRoom("bunk", "Bunk room", 400, 2, 2), Content.Campaign.Keep.Rooms[0]);
    }

    [Fact]
    public void AMapFieldsSixByDefault()
    {
        Assert.Equal(MapDefinition.DefaultDeploy, MapFixture.Parse(MapFixture.OldMillRoad).Deploy);
        Assert.Equal(6, MapDefinition.DefaultDeploy);
    }

    [Fact]
    public void TheDeployHeaderRoundTripsInCanonicalOrder()
    {
        foreach (var text in new[] { Deployed("deploy: 4", 0), Deployed("deploy: all", 10) })
        {
            Assert.Equal(text, MapFormat.Write(MapFixture.Parse(text), Content));
        }

        Assert.True(MapFixture.Parse(Deployed("deploy: all", 10)).DeploysAll);
        Assert.Equal(4, MapFixture.Parse(Deployed("deploy: 4", 0)).Deploy);
    }

    [Fact]
    public void DeployAllWithFewerThanTwelveStartTilesFailsNamingTheLine()
    {
        var e = Assert.Throws<MapException>(() => MapFixture.Parse(Deployed("deploy: all", 9), "keep.map"));

        Assert.Equal("keep.map", e.File);
        Assert.Equal(7, e.Line);
        Assert.Equal("deploy: all needs at least 12 player placements, one per member of a full company, got 11", e.Problem);
    }

    [Fact]
    public void ACountBelowTheMapsPlacementsFails()
    {
        var e = Assert.Throws<MapException>(() => MapFixture.Parse(Deployed("deploy: 3", 2), "bad.map"));
        Assert.Equal(7, e.Line);
        Assert.Equal("deploy: 3 is fewer than the map's 4 player placements", e.Problem);

        var bare = Assert.Throws<MapException>(() => MapFixture.Parse(Deployed("", 5), "bad.map"));
        Assert.Contains("deploy: the map has 7 player placements and fields 6", bare.Problem);

        var zero = Assert.Throws<MapException>(() => MapFixture.Parse(Deployed("deploy: 0", 0), "bad.map"));
        Assert.Equal("deploy must be an integer 1..12, got '0'", zero.Problem);
    }

    [Fact]
    public void DeployAllFieldsEveryLivingMemberAndLeavesTheRestEmpty()
    {
        var map = MapFixture.Parse(Deployed("deploy: all", 10));
        var start = CampaignRecord.Start(Content, 1);

        var state = start.Begin(map, Content);

        Assert.Equal(start.Roster.Select(u => u.Id).OrderBy(id => id, StringComparer.Ordinal), state.UnitsOf(Side.Player).Select(u => u.Id).OrderBy(id => id, StringComparer.Ordinal));
        Assert.True(start.Roster.Count < map.Placements.Count(p => p is PlayerPlacement));
    }

    [Fact]
    public void DeployAllRefusesTheBenchAndIgnoresOne()
    {
        var map = MapFixture.Parse(Deployed("deploy: all", 10));
        var start = CampaignRecord.Start(Content, 1);
        var recruit = start.Roster[^1].Id;

        var bench = start.Bench(recruit, map);

        Assert.False(bench.Accepted);
        Assert.Equal("the whole company fights on Old Mill Road; nobody is benched", bench.Text);
        Assert.Contains(recruit, (start with { Benched = ValueList<string>.Of(recruit) }).Deployment(map, Content));
    }

    [Fact]
    public void TheCampLineCountsTheDeployedAgainstTheLivingPresent()
    {
        var mill = AtTheMill(CampaignRecord.Start(Content, 1).Roster.Count);
        var present = mill.Present(Content).Count;
        var all = MapFixture.Parse(Deployed("deploy: all", 10));

        Assert.Equal($"Deploys to The Mill: Alder Fenn, Maud (deploy 2 of {present})", CampaignSession.DeploymentLine(mill, Content, Map("the_mill")));
        Assert.StartsWith($"Deploys to Old Mill Road: the whole company fights: {present}; Alder Fenn, ", CampaignSession.DeploymentLine(mill, Content, all));
    }
}
