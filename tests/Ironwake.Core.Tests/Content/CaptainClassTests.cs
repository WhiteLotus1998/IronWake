using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The captain's three classes (issue 705, DESIGN section 3): Vanguard, Marshal and Ranger, each with
/// one advanced form, flagged <c>captain</c> in <c>classes.json</c>. Only the captain is promoted into
/// them, the captain into nothing else, and once on the ladder only up it; the camp row says so.
/// </summary>
public class CaptainClassTests
{
    private static GameContent Content => MapFixture.Content;

    private static Unit Captain => Content.Cast[0];

    private static Unit Recruit(string id) => Content.Cast.Single(u => u.Id == id);

    [Theory]
    [InlineData("vanguard", "champion")]
    [InlineData("marshal", "commander")]
    [InlineData("ranger", "pathfinder")]
    public void TheCaptainsLadderIsThreeClassesEachWithOneForm(string baseId, string formId)
    {
        var basis = Content.Class(baseId);
        var form = Content.Class(formId);

        Assert.True(basis.Captain);
        Assert.True(form.Captain);
        Assert.Null(basis.Advances);
        Assert.Equal(baseId, form.Advances?.Id);
        Assert.Equal("level 3", basis.Certification.Describe());
        Assert.Equal("level 10, sword C", form.Certification.Describe());
        Assert.Equal(6, Content.Classes.Values.Count(c => c.Captain));
    }

    [Fact]
    public void EachCaptainsClassDoesWhatItIsFor()
    {
        Assert.Equal(new[] { WeaponType.Sword }, Content.Class("vanguard").Weapons);
        Assert.Equal(new[] { WeaponType.Sword, WeaponType.Lance, WeaponType.Axe }, Content.Class("champion").Weapons);
        Assert.Equal(new[] { WeaponType.Sword, WeaponType.Reason }, Content.Class("marshal").Weapons);
        Assert.Equal(new[] { WeaponType.Sword, WeaponType.Bow }, Content.Class("ranger").Weapons);
        Assert.Equal(5, Content.Class("ranger").Mov);
        Assert.Equal("canto", Content.Class("ranger").Mastery);
        Assert.True(Content.Class("champion").CanUse(WeaponType.Axe));
        Assert.Equal(MovementType.Cavalry, Content.Class("commander").Movement);
        Assert.Equal(6, Content.Class("commander").Mov);
        Assert.True(Content.Class("commander").CanUse(WeaponType.Lance));
    }

    [Theory]
    [InlineData("vanguard")]
    [InlineData("marshal")]
    [InlineData("ranger")]
    public void TheCaptainAtLevelThreeIsPromotedIntoEachOfTheThree(string classId)
    {
        var captain = Captain with { Level = 3 };

        Assert.Empty(Certifications.Check(captain, Content.Class(classId), Content.Class("cadet"), captain: true));
        Assert.Equal(classId, Certifications.Certify(captain, Content.Class(classId), captain: true).ClassId);
    }

    [Fact]
    public void OnlyTheCaptainIsPromotedIntoTheCaptainsLadderAndTheRefusalIsNamed()
    {
        var wren = Recruit("wren") with { Level = 3 };

        var refusals = Certifications.Check(wren, Content.Class("vanguard"), Content.Class("cadet"), captain: false);

        Assert.Equal(new[] { "only the captain takes Vanguard" }, refusals.Select(r => r.Text));
        Assert.Equal("captain", refusals[0].Requirement);
        Assert.Throws<InvalidOperationException>(() => Certifications.Certify(wren, Content.Class("vanguard")));
    }

    [Theory]
    [InlineData("pikeman")]
    [InlineData("bowman")]
    [InlineData("outrider")]
    [InlineData("halberdier")]
    public void TheCaptainIsRefusedEveryGeneralClass(string classId)
    {
        var captain = Captain with { Level = 10 };

        var refusals = Certifications.Check(captain, Content.Class(classId), Content.Class("cadet"), captain: true);

        Assert.Equal("captain", refusals[0].Requirement);
        Assert.Equal($"{Content.Class(classId).Name} is not on the captain's ladder", refusals[0].Text);
    }

    [Fact]
    public void TheCaptainsLadderIsOneWay()
    {
        var vanguard = Captain with { ClassId = "vanguard", Level = 10, Skill = WeaponSkill.Zero.With(WeaponType.Sword, WeaponRanks.Threshold(WeaponRank.C)) };

        var across = Certifications.Check(vanguard, Content.Class("marshal"), Content.Class("vanguard"), captain: true);
        var over = Certifications.Check(vanguard, Content.Class("commander"), Content.Class("vanguard"), captain: true);

        Assert.Equal(new[] { "the captain's ladder is one-way; Vanguard leads only to its own form" }, across.Select(r => r.Text));
        Assert.Contains(over, r => r.Text == "needs to be a Marshal first");
        Assert.Empty(Certifications.Check(vanguard, Content.Class("champion"), Content.Class("vanguard"), captain: true));
    }

    [Fact]
    public void TheChampionDoesNotStepBackDownToTheVanguard()
    {
        var champion = Captain with { ClassId = "champion", Level = 10 };

        var refusals = Certifications.Check(champion, Content.Class("vanguard"), Content.Class("champion"), captain: true);

        Assert.Equal(new[] { "the captain's ladder is one-way; Champion leads only to its own form" }, refusals.Select(r => r.Text));
    }

    [Fact]
    public void TheCampaignScreenReadsTheCaptainFromTheCast()
    {
        var captain = Captain with { Level = 3 };
        var wren = Recruit("wren") with { Level = 3 };
        var record = CampaignRecord.Start(Content, 5) with { Purse = 2000, Roster = ValueList<Unit>.Of(captain, wren) };

        var general = record.Certify(captain.Id, "pikeman", Content);
        var other = record.Certify("wren", "marshal", Content);
        var promoted = record.Certify(captain.Id, "marshal", Content);

        Assert.False(general.Accepted);
        Assert.Contains("Pikeman is not on the captain's ladder", general.Text);
        Assert.False(other.Accepted);
        Assert.Contains("only the captain takes Marshal", other.Text);
        Assert.True(promoted.Accepted, promoted.Text);
        Assert.Equal("marshal", promoted.Record.Captain(Content)!.ClassId);
        Assert.Equal(1500, promoted.Record.Purse);
    }

    [Fact]
    public void TheCampRowPrintsTheThreeChoicesBeforeTheCaptainTakesOne()
    {
        var record = CampaignRecord.Start(Content, 5);

        var row = CampaignSession.UnitLines(record, Content, record.Captain(Content)!, detail: false)[0];

        Assert.EndsWith("; promotes at 3: Marshal, Ranger or Vanguard", row);
    }

    [Theory]
    [InlineData("ranger", "; ladder: Ranger, then Pathfinder at 10")]
    [InlineData("pathfinder", "; ladder: Ranger, then Pathfinder at 10")]
    public void TheCampRowPrintsTheChosenLadderAfter(string classId, string ending)
    {
        var start = CampaignRecord.Start(Content, 5);
        var record = start with { Roster = ValueList<Unit>.From(start.Roster.Select(u => u.Id == Captain.Id ? u with { ClassId = classId } : u)) };

        var row = CampaignSession.UnitLines(record, Content, record.Captain(Content)!, detail: false)[0];

        Assert.EndsWith(ending, row);
    }

    [Fact]
    public void NoOtherUnitsRowNamesTheLadder()
    {
        var record = CampaignRecord.Start(Content, 5);

        Assert.All(record.Roster.Where(u => u.Id != Captain.Id), u =>
            Assert.DoesNotContain("promotes at", CampaignSession.UnitLines(record, Content, u, detail: false)[0]));
    }

    private const string Ladder = """
        { "classes": [
          { "id": "cadet", "name": "Cadet", "movement": "infantry", "mov": 4, "weapons": ["sword"] },
          { "id": "leader", "name": "Leader", "captain": true, "movement": "infantry", "mov": 4, "weapons": ["sword"], "growthModifiers": { "cha": 10 } },
          { "id": "lord", "name": "Lord", "captain": true, "movement": "infantry", "mov": 5, "weapons": ["sword"], "advances": "leader" }
        ] }
        """;

    [Fact]
    public void ACaptainsClassLoadsAndRoundTrips()
    {
        var content = ContentLoader.Parse(Fixture.Files(classes: Ladder));

        Assert.True(content.Class("leader").Captain);
        Assert.True(content.Class("lord").Captain);
        Assert.False(content.Class("cadet").Captain);

        var written = ContentSerializer.Write(content);
        var reloaded = ContentLoader.Parse(written);
        Assert.Equal(content, reloaded);
    }

    [Fact]
    public void AnAdvancedFormOffItsBasesLadderIsRefused()
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(classes: Ladder.Replace("\"lord\", \"name\": \"Lord\", \"captain\": true,", "\"lord\", \"name\": \"Lord\","))));

        Assert.Equal(ContentFiles.ClassesName, e.File);
        Assert.Equal("lord", e.Entry);
        Assert.Equal("captain", e.Field);
        Assert.Contains("must match its base, 'leader'", e.Message);
    }

    [Fact]
    public void AUnitThatIsNotTheCaptainIsRefusedACaptainsClassInContent()
    {
        var units = Fixture.Units.Replace("\"class\": \"cadet\"", "\"class\": \"leader\"");

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(classes: Ladder, units: units)));

        Assert.Equal("recruit", e.Entry);
        Assert.Equal("class", e.Field);
        Assert.Contains("only the captain, the cast's first, stands in it", e.Message);
    }
}
