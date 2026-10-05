using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 1130 (rounds 380 and 381, Lotus's amendment): the picked claimant joins trained. The raid's
/// <c>branch</c> authors the seat, L4 and 30 rank points (D) in the claimant's main weapon, the type
/// of the first weapon on their cast card; the living median is a floor, and nothing reads the
/// company's top level, so feeding one unit never raises the pick. The passed claimant returns with
/// the same rank floor. Ordinary joiners keep the median.
/// </summary>
public class SeatTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static string Dir => Fixture.RealContentDirectory();

    private static CampaignMap Raid => Content.Campaign.Maps[Content.Campaign.MapIndexOf("ironwake_raid")];

    private static CampaignRecord AtTheRaid() => CampaignRecord.StartAt(Content, 1130, "ironwake_raid");

    private static CampaignRecord Raised(CampaignRecord record, Func<Unit, int> level) =>
        record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.ScaledTo(level(u), Content.Class(u.ClassId)))) };

    private static Unit Joined(CampaignRecord record, string pick) =>
        record.PickClaimant(pick, Content).Record.Present(Content).Single(u => u.Id == pick);

    [Fact]
    public void TheRaidAuthorsTheSeatAtLevelFourAndRankD()
    {
        Assert.Equal(4, Raid.SeatLevel);
        Assert.Equal(30, Raid.SeatRank);
        Assert.Equal(WeaponRank.D, WeaponRanks.RankAt(Raid.SeatRank));
    }

    [Theory]
    [InlineData("rook", WeaponType.Lance)]
    [InlineData("keziah", WeaponType.Axe)]
    public void ThePickJoinsAtTheAuthoredLevelWithTheirMainWeaponAtD(string pick, WeaponType main)
    {
        var record = AtTheRaid();
        Assert.True(record.JoinLevel(Content) < 4);

        var unit = Joined(record, pick);

        Assert.Equal(4, unit.Level);
        Assert.Equal(30, unit.Skill.Points(main));
        Assert.Equal(WeaponRank.D, unit.Skill.Rank(main));
        Assert.Equal($"{Content.Unit(pick).Name} takes the seat at level 4; {Content.Unit(pick == "rook" ? "keziah" : "rook").Name} rides home", record.PickClaimant(pick, Content).Text);
    }

    [Fact]
    public void TheMedianFloorWinsWhenItIsHigherThanTheSeat()
    {
        var record = Raised(AtTheRaid(), _ => 6);

        Assert.Equal(6, record.SeatLevel(Content));
        Assert.Equal(6, Joined(record, "rook").Level);
    }

    [Fact]
    public void RaisingTheTopUnitDoesNotChangeTheJoin()
    {
        var even = AtTheRaid();
        var fed = Raised(even, u => u.Id == Content.Cast[0].Id ? 15 : u.Level);

        Assert.Equal(Joined(even, "rook").Level, Joined(fed, "rook").Level);
        Assert.Equal(4, Joined(fed, "keziah").Level);
    }

    [Fact]
    public void ThePicksRankFloorNeverLowersPointsTheyHave()
    {
        var card = Content.Unit("rook") with { Skill = WeaponSkill.Zero.With(WeaponType.Lance, 50) };

        Assert.Equal(50, CampaignRecord.Trained(card, Content).Skill.Points(WeaponType.Lance));
    }

    [Fact]
    public void AUnitNoBranchNamesIsNotTrainedByTheSeat()
    {
        var pell = Content.Unit("pell");

        Assert.Equal(pell, CampaignRecord.Trained(pell, Content));
    }

    [Fact]
    public void AnOrdinaryJoinerKeepsTheMedianNotTheSeat()
    {
        var record = CampaignRecord.StartAt(Content, 1130, "the_field", pick: "rook");
        var meeting = Content.Campaign.Maps[Content.Campaign.MapIndexOf("the_field")].Meets[0];

        var met = record.Meet(meeting, Content).Record;

        Assert.Equal(Math.Max(Content.Unit(meeting).Level, met.JoinLevel(Content)), met.Present(Content).Single(u => u.Id == meeting).Level);
    }

    [Theory]
    [InlineData("rook", "keziah", WeaponType.Axe)]
    [InlineData("keziah", "rook", WeaponType.Lance)]
    public void TheReturnedClaimantComesBackWithTheSeatsRankFloor(string pick, string passed, WeaponType main)
    {
        var record = CampaignRecord.StartAt(Content, 1130, "the_field", pick: pick);
        var field = MapFiles.Load(MapFiles.CampaignPath(Dir, Content, "the_field"), Content);

        var foe = record.Begin(field, Content).Find(passed)!;

        Assert.Equal(Side.Enemy, foe.Side);
        Assert.True(foe.Unit.Skill.Points(main) >= 30);
    }

    [Fact]
    public void AFallenPickSendsThePassedClaimantBackAtTheSeatNotTheBareMedian()
    {
        var record = CampaignRecord.StartAt(Content, 1130, "the_field", pick: "keziah");
        record = record with { Roster = ValueList<Unit>.From(record.Roster.Where(u => u.Id != "keziah")), Fallen = record.Fallen.Add("keziah") };

        Assert.Equal(record.SeatLevel(Content), record.ReturnLevel(Content));
        Assert.True(record.ReturnLevel(Content) >= 4);
    }

    [Fact]
    public void ThePickRaisedAboveTheMedianSaysTheSeat()
    {
        var picked = AtTheRaid().PickClaimant("rook", Content).Record;

        Assert.Contains("Rook joins at level 4 (the seat)", CampaignSession.JoinLines(picked, Content));
    }

    /// <summary>The shipped content's files with <paramref name="maps"/> as the campaign's maps.</summary>
    private static ContentFiles With(string maps) =>
        ContentSerializer.Write(Content) with { Scenes = null, Campaign = new ContentFile(ContentFiles.CampaignName, $$"""{ "startingPurse": 0, "certificationPrice": 0, "maps": [ {{maps}} ] }""") };

    [Fact]
    public void TheSeatRoundTripsThroughTheSerializer()
    {
        var content = ContentLoader.Parse(With("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"], "seatLevel": 4, "seatRank": 30 }"""));

        var map = ContentLoader.Parse(ContentSerializer.Write(content)).Campaign.Maps[0];

        Assert.Equal((4, 30), (map.SeatLevel, map.SeatRank));
    }

    [Theory]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "seatLevel": 4 }""", "seatLevel", "needs a branch")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"], "seatLevel": 0 }""", "seatLevel", "must be between")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"], "seatLevel": 31 }""", "seatLevel", "must be between")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "seatRank": 30 }""", "seatRank", "needs a branch")]
    [InlineData("""{ "map": "one", "reward": 0, "stock": [], "branch": ["keziah", "rook"], "seatRank": -1 }""", "seatRank", "must be at least 0")]
    public void ABadSeatIsRefusedNamingTheMapAndTheField(string maps, string field, string message)
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(With(maps)));

        Assert.Equal((ContentFiles.CampaignName, "one", field), (e.File, e.Entry, e.Field));
        Assert.Contains(message, e.Message);
    }
}
