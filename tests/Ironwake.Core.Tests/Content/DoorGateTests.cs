using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The door's points gate (issue 1174, round 394): a door may ask rank points in a weapon type, strictly
/// between D's threshold and C's, in place of a letter. The ten level-7 doors ask 50 in the base's main
/// weapon; the captain's level-10 forms keep sword C; the camp row shows the points toward the door; and
/// the Sim's read names the bar, the ceiling and the pick as round 394 agreed before the numbers.
/// </summary>
public class DoorGateTests
{
    private static GameContent Content => MapFixture.Content;

    private static string CadetWith(string certification) =>
        Fixture.Classes.Replace("\"weapons\": [\"sword\"]", "\"weapons\": [\"sword\"], \"certification\": " + certification);

    private static Unit Recruit(string id) => Content.Cast.Single(u => u.Id == id);

    [Fact]
    public void APointsGateLoadsAndRoundTrips()
    {
        var content = ContentLoader.Parse(Fixture.Files(classes: CadetWith("{ \"level\": 7, \"points\": { \"sword\": 50 } }")));

        Assert.Equal(ValueList<(WeaponType, int)>.Of((WeaponType.Sword, 50)), content.Class("cadet").Certification.Points);
        var written = ContentSerializer.Write(content);
        Assert.Equal(content, ContentLoader.Parse(written));
    }

    [Theory]
    [InlineData("{ \"points\": { \"sword\": 30 } }", "certification.points.sword")]
    [InlineData("{ \"points\": { \"sword\": 80 } }", "certification.points.sword")]
    [InlineData("{ \"points\": { \"sword\": \"fifty\" } }", "certification.points.sword")]
    [InlineData("{ \"points\": { \"spear\": 50 } }", "certification.points.spear")]
    [InlineData("{ \"ranks\": { \"sword\": \"C\" }, \"points\": { \"sword\": 50 } }", "certification.points.sword")]
    public void AGateOutsideDAndCOrBesideALetterIsRefusedNamingTheField(string certification, string field)
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(classes: CadetWith(certification))));

        Assert.Equal(ContentFiles.ClassesName, e.File);
        Assert.Equal("cadet", e.Entry);
        Assert.Equal(field, e.Field);
    }

    [Theory]
    [InlineData(31, true)]
    [InlineData(79, true)]
    [InlineData(30, false)]
    [InlineData(80, false)]
    public void AGateSitsStrictlyBetweenDAndC(int points, bool gate)
    {
        Assert.Equal(gate, CertificationRequirements.IsGate(points));
    }

    [Theory]
    [InlineData(49, "needs lance 50, has 49")]
    [InlineData(50, null)]
    public void TheGateIsMetAtItsPointsAndRefusedBelowThem(int points, string? refusal)
    {
        var pikeman = Recruit("teodor") with { ClassId = "pikeman", Level = 7, Skill = WeaponSkill.Zero.With(WeaponType.Lance, points) };

        var refusals = Certifications.Check(pikeman, Content.Class("halberdier"));

        Assert.Equal(refusal is null ? [] : new[] { refusal }, refusals.Select(r => r.Text));
        Assert.All(refusals, r => Assert.Equal("points.lance", r.Requirement));
    }

    [Fact]
    public void EveryLevelSevenDoorAsksTheSimsGateAndNoLetter()
    {
        var doors = Content.Classes.Values.Where(c => c.Advances is not null && !c.Captain).ToList();

        Assert.Equal(10, doors.Count);
        Assert.All(doors, d =>
        {
            Assert.Equal(7, d.Certification.Level);
            Assert.Empty(d.Certification.Ranks);
            Assert.Equal(LevelRun.Gate, Assert.Single(d.Certification.Points).Points);
        });
    }

    [Fact]
    public void TheCaptainsFormsKeepSwordC()
    {
        Assert.All(Content.Classes.Values.Where(c => c.Captain && c.Advances is not null), f =>
        {
            Assert.Equal(ValueList<(WeaponType, WeaponRank)>.Of((WeaponType.Sword, WeaponRank.C)), f.Certification.Ranks);
            Assert.Empty(f.Certification.Points);
        });
    }

    [Theory]
    [InlineData(5, 41, "; door: lance 41/50")]
    [InlineData(4, 41, null)]
    public void TheCampRowShowsPointsTowardTheDoorFromLevelFive(int level, int points, string? ending)
    {
        var record = CampaignRecord.Start(Content, 5);
        var teodor = Recruit("teodor") with { ClassId = "pikeman", Level = level, Skill = WeaponSkill.Zero.With(WeaponType.Lance, points) };

        var row = CampaignSession.UnitLines(record, Content, teodor, detail: false)[0];

        if (ending is null)
        {
            Assert.DoesNotContain("door:", row);
        }
        else
        {
            Assert.EndsWith(ending, row);
        }
    }

    [Fact]
    public void AClassWithNoDoorAboveItShowsNone()
    {
        var levy = Recruit("teodor") with { ClassId = "cadet", Level = 9 };

        Assert.Null(CampaignSession.DoorText(Content, levy));
    }

    [Fact]
    public void TwoDoorsOnOneGateShowOnce()
    {
        var rook = Recruit("rook") with { ClassId = "skyrider", Level = 6, Skill = WeaponSkill.Zero.With(WeaponType.Lance, 33) };

        Assert.Equal("door: lance 33/50", CampaignSession.DoorText(Content, rook));
    }

    [Fact]
    public void TheGateReadPassesOnABoundWithItsMarginAndTheLevel()
    {
        Assert.Contains("the bar passes (bound p50 55, at least 55;", LevelRun.GateVerdict(55, 7, 12, 0, 0, 5, 40));
    }

    [Theory]
    [InlineData(54, 7)]
    [InlineData(70, 6)]
    public void TheGateReadFailsShortOfTheMarginOrTheLevel(int bound, int level)
    {
        Assert.Contains($"the bar fails (bound p50 {bound}, needs 55;", LevelRun.GateVerdict(bound, level, 12, 0, 0, 5, 40));
    }

    [Fact]
    public void TheKeptPointsDoNotDecideTheGateRead()
    {
        Assert.Equal(LevelRun.GateVerdict(60, 7, 0, 0, 0, 5, 40).Replace("kept p50 0", "kept p50 60"), LevelRun.GateVerdict(60, 7, 60, 0, 0, 5, 40));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    public void AnEvenChairAtTheDoorOrTheLevelAloneBreaksTheCeiling(int door, int alone)
    {
        Assert.Contains($"the ceiling is broken (even p50 {door} at L7 and the gate, p50 {alone} at L7 alone); step the levy floor's offset first", LevelRun.GateVerdict(60, 7, 12, door, alone, 5, 40));
    }

    [Theory]
    [InlineData(7, 50, true)]
    [InlineData(7, 49, false)]
    [InlineData(6, 60, false)]
    public void APickAtTheDoorOnTheEvenChairRaisesTheGateFirst(int level, int points, bool rises)
    {
        var verdict = LevelRun.GateVerdict(60, 7, 12, 0, 0, level, points);

        Assert.Equal(rises, verdict.EndsWith("the gate rises first (round 394)", StringComparison.Ordinal));
    }
}
