using Ironwake.Content;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The level table's camp line (issue 738): per campaign map, the enemy level, the deployed units'
/// levels as the battle began, and the captain's share of the EXP the company kept from it, so the
/// spread under the top unit is visible beside it.
/// </summary>
public class LevelSpreadTests
{
    private static readonly string Root = Fixture.RealContentDirectory();
    private static readonly GameContent Shipped = ContentLoader.Load(Root);

    private static BattleState TheMillAtItsCamp()
    {
        var record = CampaignRecord.Start(Shipped, 1, permadeath: false) with { MapIndex = 1 };
        var map = MapFiles.Load(MapFiles.CampaignPath(Root, Shipped, record.NextMap(Shipped).MapId), Shipped);
        return record.Begin(map, Shipped);
    }

    private static BattleState Gain(BattleState state, string id, int exp)
    {
        var unit = state.Units.Single(u => u.Id == id);
        var total = LevelRun.TotalExp(unit.Unit) + exp;
        return state.WithUnit(unit with { Unit = unit.Unit with { Level = Unit.MinLevel + total / Experience.LevelUpAt, Exp = total % Experience.LevelUpAt } });
    }

    [Fact]
    public void TheCampReadingIsTheEnemyLevelAndTheDeployedLevelsAsTheBattleBegan()
    {
        var start = TheMillAtItsCamp();

        var camp = LevelRun.Read(start, start, Shipped);

        Assert.Equal(start.Map.EnemyLevel, camp.EnemyLevel);
        Assert.Equal(start.UnitsOf(Side.Player).Select(u => u.Unit.Level), camp.Deployed);
        Assert.True(camp.Deployed.Count > 1);
        Assert.Equal(0, camp.CompanyExp);
        Assert.Null(LevelRun.CaptainShare(camp));
    }

    [Fact]
    public void TheCaptainShareIsTheCaptainsExpOverTheCompanysAcrossALevelUp()
    {
        var start = TheMillAtItsCamp();
        var captain = start.UnitsOf(Side.Player).Single(u => CampaignRecord.IsCaptain(u.Unit, Shipped));
        var other = start.UnitsOf(Side.Player).First(u => u.Id != captain.Id);

        var end = Gain(Gain(start, captain.Id, 150), other.Id, 50);
        var camp = LevelRun.Read(start, end, Shipped);

        Assert.Equal(150, camp.CaptainExp);
        Assert.Equal(200, camp.CompanyExp);
        Assert.Equal(75, LevelRun.CaptainShare(camp));
    }

    [Fact]
    public void AUnitThatFellKeepsNoExpFromTheMap()
    {
        var start = TheMillAtItsCamp();
        var captain = start.UnitsOf(Side.Player).Single(u => CampaignRecord.IsCaptain(u.Unit, Shipped));
        var other = start.UnitsOf(Side.Player).First(u => u.Id != captain.Id);

        var end = Gain(Gain(start, captain.Id, 30), other.Id, 90).WithoutUnit(other.Id);
        var camp = LevelRun.Read(start, end, Shipped);

        Assert.Equal(30, camp.CompanyExp);
        Assert.Equal(100, LevelRun.CaptainShare(camp));
    }

    [Theory]
    [InlineData(new[] { 7, 1, 2, 1, 3 }, 2)]
    [InlineData(new[] { 7, 1, 2, 1 }, 1)]
    [InlineData(new[] { 4 }, 4)]
    [InlineData(new int[0], 0)]
    public void TheDeployedMedianIsTheLowerMiddle(int[] levels, int expected) =>
        Assert.Equal(expected, LevelRun.Median(levels));

    [Fact]
    public void EveryWonCampaignMapPrintsItsCampLineWithEnemyMedianLowestAndCaptainShare()
    {
        var camp1 = new LevelRun.Camp(1, new[] { 1 }, 40, 40);
        var camp2 = new LevelRun.Camp(2, new[] { 3, 1, 1, 2 }, 60, 120);
        var runs = new[]
        {
            new LevelRun.Run(new[] { (1, (IReadOnlyList<int>)new[] { 2 }, 0, camp1), (2, (IReadOnlyList<int>)new[] { 3, 2, 1, 2 }, 0, camp2) }, null),
        };

        var lines = LevelRun.Lines(Shipped, runs).ToList();
        var id1 = Shipped.Campaign.Maps[0].MapId;
        var id2 = Shipped.Campaign.Maps[1].MapId;

        Assert.Contains($"    camp {id1}: enemy 1, deployed top p50 1, median p50 1, lowest p50 1, captain share p50 100%", lines);
        Assert.Contains($"    camp {id2}: enemy 2, deployed top p50 3, median p50 1, lowest p50 1, captain share p50 50%", lines);
    }

    [Fact]
    public void AMapWhereTheCompanyEarnedNothingPrintsNoneEarned()
    {
        var runs = new[] { new LevelRun.Run(new[] { (1, (IReadOnlyList<int>)new[] { 1 }, 0, new LevelRun.Camp(1, new[] { 1 }, 0, 0)) }, null) };

        Assert.Contains(LevelRun.Lines(Shipped, runs), l => l.EndsWith("captain share p50 none earned", StringComparison.Ordinal));
    }
}
