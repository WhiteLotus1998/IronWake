using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The shipped class ladder (issue 72, DESIGN section 3, DECISIONS/0058): what each class in
/// <c>classes.json</c> asks of a unit certifying into it. The cadet asks nothing; the foot
/// classes ask level 3, the mounted and armored ones level 4; a class a cadet can train toward
/// asks a weapon rank, and one it cannot asks a stat.
/// </summary>
public class ClassLadderTests
{
    private static GameContent Content => MapFixture.Content;

    private static Unit Recruit(string id) => Content.Cast.Single(u => u.Id == id);

    [Theory]
    [InlineData("cadet", 1, "", "")]
    [InlineData("pikeman", 3, "lance D", "")]
    [InlineData("reaver", 3, "axe D", "")]
    [InlineData("bowman", 3, "", "dex 8")]
    [InlineData("adept", 3, "", "mag 6")]
    [InlineData("chaplain", 3, "", "mag 4, res 4")]
    [InlineData("outrider", 4, "sword D", "")]
    [InlineData("skyrider", 4, "lance D", "spd 8")]
    [InlineData("bulwark", 4, "", "def 5")]
    public void EachShippedClassAsksItsLadderNumbers(string classId, int level, string ranks, string stats)
    {
        var asks = Content.Class(classId).Certification;

        Assert.Equal(level, asks.Level);
        Assert.Equal(ranks, string.Join(", ", asks.Ranks.Select(r => $"{r.Type.ToString().ToLowerInvariant()} {r.Rank}")));
        Assert.Equal(stats, string.Join(", ", Stats.All.Where(s => asks.Stats.Get(s) > 0).Select(s => $"{s.ToString().ToLowerInvariant()} {asks.Stats.Get(s)}")));
    }

    [Theory]
    [InlineData("cadet", "nothing")]
    [InlineData("reaver", "level 3, axe D")]
    [InlineData("chaplain", "level 3, mag 4, res 4")]
    [InlineData("skyrider", "level 4, lance D, spd 8")]
    public void TheScreenDescribesWhatAClassAsksLevelThenRanksThenStats(string classId, string text)
    {
        Assert.Equal(text, Content.Class(classId).Certification.Describe());
    }

    [Fact]
    public void EveryRankTheLadderAsksIsInAWeaponTheCadetTrains()
    {
        var cadet = Content.Class("cadet");

        Assert.All(Content.Classes.Values.SelectMany(c => c.Certification.Ranks), r => Assert.True(cadet.CanUse(r.Type), r.Type.ToString()));
    }

    [Fact]
    public void ALevelOneRecruitIsRefusedEveryClassButTheCadetNamingTheLevel()
    {
        var brannock = Recruit("brannock");

        foreach (var target in Content.Classes.Values.Where(c => c.Id != "cadet"))
        {
            Assert.Contains(Certifications.Check(brannock, target), r => r.Text == $"needs level {target.Certification.Level}, has 1");
        }
    }

    [Fact]
    public void TheRefusalIntoTheUnitsOwnClassReadsWithTheRightArticle()
    {
        Assert.Equal("pell is already an Adept", Certifications.Check(Recruit("pell"), Content.Class("adept"))[0].Text);
    }

    [Fact]
    public void AnyRecruitMayCertifyBackToTheCadet()
    {
        Assert.Empty(Certifications.Check(Recruit("teodor"), Content.Class("cadet")));
    }

    [Fact]
    public void BrannockAtLevelThreeWithAnAxeAtDCertifiesAsAReaver()
    {
        var trained = Recruit("brannock") with { Level = 3, Skill = WeaponSkill.Zero.With(WeaponType.Axe, WeaponRanks.Threshold(WeaponRank.D)) };

        Assert.Equal("needs axe D, has E", Assert.Single(Certifications.Check(trained with { Skill = WeaponSkill.Zero.With(WeaponType.Axe, 29) }, Content.Class("reaver"))).Text);
        Assert.Equal("reaver", Certifications.Certify(trained, Content.Class("reaver")).ClassId);
    }

    [Fact]
    public void AStatTheLadderAsksReadsTheUnitsOwnStatsNotItsClasss()
    {
        var pell = Recruit("pell") with { Level = 3 };

        Assert.Empty(Certifications.Check(pell, Content.Class("chaplain")));
        Assert.Equal("needs res 4, has 3", Assert.Single(Certifications.Check(pell with { Stats = pell.Stats with { Res = 3 } }, Content.Class("chaplain"))).Text);
    }
}
