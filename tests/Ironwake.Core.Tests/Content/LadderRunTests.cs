using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The captain's ladder's bar (issue 705, slice 3): how the Sim fields the captain in each ladder
/// class, the board each tier is fought on, the verdicts the bar reads, and the smoke's twelve
/// origin-by-class captains.
/// </summary>
public class LadderRunTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static Unit Captain => Content.Cast[0];

    private static LadderRun.Reading Row(string id, double rate, bool gate4 = true, int attacks = 10) =>
        new(id, rate, gate4, new ActionMix(attacks, 0, 0, 0));

    [Fact]
    public void TheLadderIsTheThreeBaseClassesThenTheirForms()
    {
        var ladder = LadderRun.Ladder(Content);

        Assert.Equal(new[] { "marshal", "ranger", "vanguard", "champion", "commander", "pathfinder" }, ladder.Select(c => c.Id));
        Assert.Equal(new[] { 1, 1, 1, 2, 2, 2 }, ladder.Select(LadderRun.Tier));
    }

    [Theory]
    [InlineData("vanguard", 3, "iron_lance")]
    [InlineData("marshal", 3, "cinder")]
    [InlineData("ranger", 3, "iron_bow")]
    [InlineData("champion", 10, "iron_axe")]
    [InlineData("commander", 10, "iron_lance")]
    [InlineData("pathfinder", 10, "iron_bow")]
    public void PromoteRaisesTheCaptainToTheClassLevelAndKitsItsWeapons(string classId, int level, string kit)
    {
        var promoted = LadderRun.Promote(Content, Captain, Content.Class(classId));

        Assert.Equal(classId, promoted.ClassId);
        Assert.True(promoted.Mastery.Points(classId) >= Content.Class(classId).MasteryPoints, "mastered");
        Assert.Equal(level, promoted.Level);
        Assert.Contains(promoted.Inventory.Items, s => s.ItemId == kit);
        Assert.Contains(promoted.Inventory.Items, s => s.ItemId == "iron_sword");
        Assert.All(promoted.Inventory.Items.Where(s => Content.Weapons.ContainsKey(s.ItemId)), s => Assert.True(promoted.CanWield(Content.Weapon(s.ItemId), Content.Class(classId)), s.ItemId));
    }

    [Fact]
    public void PromoteKitsNoSecondWeaponOfATypeTheCaptainCarries()
    {
        var promoted = LadderRun.Promote(Content, Captain, Content.Class("ranger"));

        Assert.Single(promoted.Inventory.Items, s => s.ItemId == "iron_sword");
    }

    [Fact]
    public void PromoteRefusesAClassOffTheLadder()
    {
        var refusal = Assert.Throws<ArgumentException>(() => LadderRun.Promote(Content, Captain, Content.Class("pikeman")));

        Assert.Contains("not on the captain's ladder", refusal.Message);
    }

    [Fact]
    public void TheFirstTierIsFoughtWithPartyAndEnemiesRaisedToItsLevel()
    {
        var map = MapFiles.LoadAll(Fixture.RealContentDirectory(), Content).First(m => m.Id == "the_tollgate").Map;

        var (party, board, raise) = LadderRun.Board(Content, map, 1);

        Assert.Equal(2, raise);
        Assert.Equal(map.EnemyLevel + 2, board.EnemyLevel);
        Assert.All(party.Cast.Zip(Content.Cast), pair => Assert.Equal(pair.Second.Level + 2, pair.First.Level));
    }

    [Fact]
    public void TheSecondTierIsFoughtNineLevelsUp()
    {
        var map = MapFiles.LoadAll(Fixture.RealContentDirectory(), Content).First(m => m.Id == "the_tollgate").Map;

        var (_, board, raise) = LadderRun.Board(Content, map, 2);

        Assert.Equal(9, raise);
        Assert.Equal(map.EnemyLevel + 9, board.EnemyLevel);
    }

    [Fact]
    public void ATierWithinFivePointsPasses()
    {
        var tier = new LadderRun.TierReading(1, 2, Row("cadet", 0.70), new[] { Row("marshal", 0.70), Row("ranger", 0.74), Row("vanguard", 0.75) });

        Assert.True(tier.Passed);
    }

    [Fact]
    public void ATierSpreadOverFivePointsFails()
    {
        var tier = new LadderRun.TierReading(1, 2, Row("cadet", 0.70), new[] { Row("marshal", 0.70), Row("ranger", 0.74), Row("vanguard", 0.76) });

        Assert.False(tier.Passed);
        Assert.Equal(0.06, tier.SpreadOf, 6);
    }

    [Fact]
    public void Gate4LostUnderAClassFailsWhereTheUnpromotedCaptainPassedIt()
    {
        var tier = new LadderRun.TierReading(1, 2, Row("cadet", 0.70), new[] { Row("marshal", 0.70, gate4: false), Row("ranger", 0.71), Row("vanguard", 0.72) });

        Assert.False(tier.Passed);
        Assert.Equal(new[] { "marshal" }, tier.Gate4Failures);
    }

    [Fact]
    public void Gate4FailingUnderTheUnpromotedCaptainTooIsNotTheClasses()
    {
        var tier = new LadderRun.TierReading(1, 2, Row("cadet", 0.70, gate4: false), new[] { Row("marshal", 0.70, gate4: false), Row("ranger", 0.71), Row("vanguard", 0.72) });

        Assert.True(tier.Passed);
    }

    [Fact]
    public void ACaptainWhoNeverAttacksFailsTheBar()
    {
        var tier = new LadderRun.TierReading(1, 2, Row("cadet", 0.70), new[] { Row("marshal", 0.70, attacks: 0), Row("ranger", 0.71), Row("vanguard", 0.72) });

        Assert.Equal(new[] { "marshal" }, tier.Gate4Failures);
    }

    [Fact]
    public void ACaptainWhoNeverAttacksOnAnEscapeMapIsLeavingAndPasses()
    {
        var tier = new LadderRun.TierReading(2, 9, Row("cadet", 0.77, gate4: false), new[] { Row("champion", 1.0), Row("commander", 1.0, attacks: 0), Row("pathfinder", 1.0) }, Escape: true);

        Assert.Empty(tier.Gate4Failures);
        Assert.True(tier.Passed);
    }

    [Fact]
    public void TheSmokeFieldsEveryOriginByEveryBaseClass()
    {
        var map = MapFiles.LoadAll(Fixture.RealContentDirectory(), Content).First(m => m.Id == "the_tollgate").Map;

        var result = LadderRun.OriginByClass(Content, "the_tollgate", map);

        Assert.True(result.Passed, result.Line);
        Assert.Contains("12 captains (4 origins x 3 classes)", result.Line);
    }
}
