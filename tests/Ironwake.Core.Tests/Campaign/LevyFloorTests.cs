using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 1164 (round 391): the levy floor. Before map number N every living levy member below
/// N less <c>levyFloor</c> is raised to it on the average growth, EXP zeroed, ranks untouched, with a
/// printed rules line. The levy is the cast on the roster from the start, the captain aside; a fed
/// unit at or above the floor is never touched, and a joiner is never drilled.
/// </summary>
public class LevyFloorTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static readonly string[] Levy = { "wren", "teodor", "ottilie", "pell", "dunstan", "brannock" };

    /// <summary>The campaign standing at map 8's camp, every member at their cast card's level.</summary>
    private static CampaignRecord AtMapEight() => CampaignRecord.StartAt(Content, 1164, "brackwater_cut", pick: "rook");

    [Fact]
    public void TheShippedFloorIsMapNumberLessThree()
    {
        Assert.Equal(3, Content.Campaign.LevyFloor);
        Assert.Equal(8, Content.Campaign.MapIndexOf("brackwater_cut") + 1);
        Assert.Equal(5, AtMapEight().LevyFloorLevel(Content));
    }

    [Fact]
    public void TheLevyIsTheCastOnTheRosterFromTheStartLessTheCaptain()
    {
        var levy = Content.Cast.Select(u => u.Id).Where(id => CampaignRecord.IsLevy(id, Content));

        Assert.Equal(Levy.Order(), levy.Order());
        Assert.False(CampaignRecord.IsLevy(Content.Cast[0].Id, Content));
    }

    [Fact]
    public void ACampRaisesEveryLevyMemberBelowTheFloorToIt()
    {
        var record = AtMapEight();

        var drills = record.Drills(Content);
        var drilled = record.Drill(Content);

        Assert.Equal(Levy.Order(), drills.Select(d => d.Id).Order());
        Assert.All(drills, d => Assert.Equal((1, 5), (d.From, d.To)));
        foreach (var id in Levy)
        {
            var unit = drilled.Find(id)!;
            Assert.Equal(record.Find(id)!.AtLevel(5, Content.Class(unit.ClassId)).Stats, unit.Stats);
            Assert.Equal((5, 0), (unit.Level, unit.Exp));
        }
    }

    [Fact]
    public void TheDrillLeavesWeaponRanksAlone()
    {
        var record = AtMapEight();
        var drilled = record.Drill(Content);

        Assert.All(Levy, id => Assert.Equal(record.Find(id)!.Skill, drilled.Find(id)!.Skill));
    }

    [Fact]
    public void AUnitAtOrAboveTheFloorIsNeverTouched()
    {
        var record = AtMapEight();
        var fed = record.Find("teodor")!.AtLevel(9, Content.Class("pikeman")) with { Exp = 40 };
        var level = record.Find("wren")!.AtLevel(6, Content.Class("cadet")) with { Exp = 70 };
        record = record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == "teodor" ? fed : u.Id == "wren" ? level : u)) };

        var drilled = record.Drill(Content);

        Assert.DoesNotContain(record.Drills(Content), d => d.Id is "teodor" or "wren");
        Assert.Equal(fed, drilled.Find("teodor"));
        Assert.Equal(level, drilled.Find("wren"));
    }

    [Fact]
    public void TheCaptainAndTheJoinersAreNeverDrilled()
    {
        var record = AtMapEight();
        var drilled = record.Drill(Content);

        foreach (var id in new[] { Content.Cast[0].Id, "maud", "rook" })
        {
            Assert.Equal(record.Find(id), drilled.Find(id));
        }
    }

    [Theory]
    [InlineData("starting_alone")]
    [InlineData("the_mill")]
    [InlineData("saltmarsh_ford")]
    public void TheFirstThreeMapsRaiseNobody(string mapId)
    {
        var record = CampaignRecord.StartAt(Content, 1164, mapId);

        Assert.Empty(record.Drills(Content));
        Assert.Same(record, record.Drill(Content));
    }

    [Fact]
    public void ACampaignWithoutTheFloorDrillsNobody()
    {
        var content = Content with { Campaign = Content.Campaign with { LevyFloor = 0 } };
        var record = CampaignRecord.StartAt(content, 1164, "brackwater_cut", pick: "rook");

        Assert.Equal(Unit.MinLevel, record.LevyFloorLevel(content));
        Assert.Empty(record.Drills(content));
    }

    [Fact]
    public void TheCampPrintsOneRulesLinePerDrilledUnit()
    {
        var lines = CampaignSession.DrillLines(AtMapEight(), Content);

        Assert.Equal(Levy.Length, lines.Count);
        Assert.Contains("Teodor drilled with the levy: L1 -> L5.", lines);
    }

    [Fact]
    public void TheFloorRoundTripsThroughTheSerializer()
    {
        var content = ContentLoader.Parse(ContentSerializer.Write(Content));

        Assert.Equal(3, content.Campaign.LevyFloor);
    }

    [Fact]
    public void ANegativeFloorIsRefusedNamingTheField()
    {
        var files = ContentSerializer.Write(Content) with { Scenes = null, Campaign = new ContentFile(ContentFiles.CampaignName, """{ "startingPurse": 0, "certificationPrice": 0, "levyFloor": -1, "maps": [ { "map": "one", "reward": 0, "stock": [] } ] }""") };

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(files));

        Assert.Equal((ContentFiles.CampaignName, "levyFloor"), (e.File, e.Field));
        Assert.Contains("must be at least 0", e.Message);
    }
}
