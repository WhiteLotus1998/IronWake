using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// A class's optional <c>certification</c> block (issue 72): it loads into
/// <see cref="UnitClass.Certification"/>, round-trips through the serializer, and a bad
/// field is refused with the file, the class and the field named.
/// </summary>
public class CertificationContentTests
{
    private static string CadetWith(string certification) =>
        Fixture.Classes.Replace("\"weapons\": [\"sword\"]", "\"weapons\": [\"sword\"], \"certification\": " + certification);

    private static ContentException Fails(string certification) =>
        Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(classes: CadetWith(certification))));

    private static void AssertField(ContentException e, string field)
    {
        Assert.Equal(ContentFiles.ClassesName, e.File);
        Assert.Equal("cadet", e.Entry);
        Assert.Equal(field, e.Field);
    }

    [Fact]
    public void ACertificationBlockLoadsItsLevelRanksAndStats()
    {
        var content = ContentLoader.Parse(Fixture.Files(classes: CadetWith("{ \"level\": 5, \"ranks\": { \"sword\": \"D\", \"lance\": \"C\" }, \"stats\": { \"str\": 8 } }")));

        var certification = content.Class("cadet").Certification;
        Assert.Equal(5, certification.Level);
        Assert.Equal(ValueList<(WeaponType, WeaponRank)>.Of((WeaponType.Sword, WeaponRank.D), (WeaponType.Lance, WeaponRank.C)), certification.Ranks);
        Assert.Equal(Stats.Zero.With(Stat.Str, 8), certification.Stats);
    }

    [Fact]
    public void AClassWithoutTheBlockAsksNothing()
    {
        Assert.Equal(CertificationRequirements.None, ContentLoader.Parse(Fixture.Files()).Class("cadet").Certification);
    }

    [Fact]
    public void CertificationRoundTrips()
    {
        var content = ContentLoader.Parse(Fixture.Files(classes: CadetWith("{ \"level\": 10, \"ranks\": { \"sword\": \"B\" }, \"stats\": { \"spd\": 9, \"def\": 3 } }")));

        var written = ContentSerializer.Write(content);
        var reloaded = ContentLoader.Parse(written);

        Assert.Equal(content, reloaded);
        Assert.Equal(written.Classes.Text, ContentSerializer.Write(reloaded).Classes.Text);
    }

    [Theory]
    [InlineData("{ \"level\": 0 }", "certification.level")]
    [InlineData("{ \"level\": 31 }", "certification.level")]
    [InlineData("{ \"level\": \"five\" }", "certification.level")]
    [InlineData("{ \"ranks\": { \"spear\": \"D\" } }", "certification.ranks.spear")]
    [InlineData("{ \"ranks\": { \"sword\": \"Z\" } }", "certification.ranks.sword")]
    [InlineData("{ \"ranks\": { \"sword\": 2 } }", "certification.ranks.sword")]
    [InlineData("{ \"stats\": { \"might\": 3 } }", "certification.stats.might")]
    [InlineData("{ \"stats\": { \"str\": -1 } }", "certification.stats.str")]
    [InlineData("{ \"roll\": 50 }", "certification.roll")]
    public void ABadCertificationFieldIsNamed(string certification, string field)
    {
        AssertField(Fails(certification), field);
    }
}
