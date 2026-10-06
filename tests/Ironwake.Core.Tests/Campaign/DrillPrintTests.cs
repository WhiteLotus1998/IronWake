using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 1181 (round 396): the drill's print. Per won map the levels harness reads the fed unit's EXP kept
/// from the battle and the EXP the camp's drill zeroed after it, and round 396's read compares the chipping
/// chair's zeroed total with the even chair's.
/// </summary>
public class DrillPrintTests
{
    private static readonly GameContent Content = MapFixture.Content;

    /// <summary>The campaign standing at map 8's camp (floor L5), every member at their cast card's level.</summary>
    private static CampaignRecord AtMapEight() => CampaignRecord.StartAt(Content, 1181, "brackwater_cut", pick: "rook");

    private static CampaignRecord WithTeodor(CampaignRecord record, int level, int exp)
    {
        var teodor = record.Find(FocusedPlayer.Fed)!.AtLevel(level, Content.Class("pikeman")) with { Exp = exp };
        return record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == FocusedPlayer.Fed ? teodor : u)) };
    }

    [Fact]
    public void TheDrillPrintCountsTheExpTheDrillZeroesOnAFedUnitBelowTheFloor()
    {
        var before = WithTeodor(AtMapEight(), 3, 50);
        var fought = WithTeodor(before, 4, 80);

        var exp = LevelRun.FedExp.Of(before, fought, [FocusedPlayer.Fed], Content);

        Assert.Equal(new LevelRun.FedExp(true, Experience.LevelUpAt + 30, 80), exp);
        Assert.Equal(0, fought.Drill(Content).Find(FocusedPlayer.Fed)!.Exp);
    }

    [Fact]
    public void TheDrillPrintCountsNothingZeroedOnAFedUnitAtTheFloor()
    {
        var before = WithTeodor(AtMapEight(), 4, 50);
        var fought = WithTeodor(before, 5, 80);

        var exp = LevelRun.FedExp.Of(before, fought, [FocusedPlayer.Fed], Content);

        Assert.Equal(new LevelRun.FedExp(true, Experience.LevelUpAt + 30, 0), exp);
    }

    [Fact]
    public void TheDrillPrintMarksABenchedFedUnitUndeployedAndAnAbsentOneAbsent()
    {
        var before = AtMapEight();
        var gone = before with { Roster = ValueList<Unit>.From(before.Roster.Where(u => u.Id != FocusedPlayer.Fed)) };

        Assert.False(LevelRun.FedExp.Of(before, before, ["wren"], Content).Deployed);
        Assert.Equal(LevelRun.FedExp.Absent, LevelRun.FedExp.Of(gone, gone, [], Content));
    }

    [Theory]
    [InlineData(100, 50, "the drill deletes the feeding (zeroed p50 chipping 100, even 50, 50 apart, at least 50); the lever is the drill carrying EXP over")]
    [InlineData(99, 50, "the drill is not the cause (zeroed p50 chipping 99, even 50, 49 apart, under 50); the cold hand play feeding teodor through map 8 decides the bar")]
    public void TheDrillVerdictReadsAsRound396Agreed(int chipping, int even, string expected)
    {
        Assert.Contains(expected, LevelRun.DrillVerdict(chipping, even), StringComparison.Ordinal);
    }
}
