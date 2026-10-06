using Ironwake.Content;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The level table's median-unit read (issue 1135, round 381): beside the top unit, the company's
/// median unit level and main-weapon rank points, how many living units stand at the tier-2 bar,
/// and the camp at which the first unit other than the captain meets some advanced form.
/// </summary>
public class MedianUnitTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly LevelRun.Camp NoCamp = new(1, [], 0, 0);

    private static LevelRun.Member M(int level, int rank, bool ready = false, bool captain = false) => new(level, rank, ready, captain);

    private static LevelRun.Run RunOf(params (int Map, LevelRun.Member[] Company)[] maps) =>
        new(maps.Select(m => (m.Map, (IReadOnlyList<int>)m.Company.Select(u => u.Level).ToList(), m.Company.Count(u => u.Ready), NoCamp)).ToList(), null)
        {
            Companies = maps.Select(m => (IReadOnlyList<LevelRun.Member>)m.Company).ToList(),
        };

    [Theory]
    [InlineData(7, 80, true)]
    [InlineData(6, 80, false)]
    [InlineData(7, 79, false)]
    [InlineData(9, 160, true)]
    public void TheTierTwoBarIsLevelSevenAndRankCInTheMainWeapon(int level, int rank, bool expected) =>
        Assert.Equal(expected, M(level, rank).AtBar);

    [Fact]
    public void TheMainWeaponIsTheFirstWeaponOnTheCastCard()
    {
        var teodor = Shipped.Unit("teodor");
        var pell = Shipped.Unit("pell");

        Assert.Equal(Shipped.Weapons["iron_lance"].Type, LevelRun.MainType(teodor, Shipped));
        Assert.Equal(Shipped.Weapons["cinder"].Type, LevelRun.MainType(pell, Shipped));
    }

    [Fact]
    public void AMemberReadsItsMainWeaponsRankPointsNotAnotherTypes()
    {
        var teodor = Shipped.Unit("teodor");
        var lance = Shipped.Weapons["iron_lance"].Type;
        var sword = Shipped.Weapons["iron_sword"].Type;
        var trained = teodor with { Level = 7, Skill = teodor.Skill.With(lance, 85).With(sword, 200) };

        var member = LevelRun.Member.Of(trained, Shipped);

        Assert.Equal(85, member.MainRank);
        Assert.True(member.AtBar);
        Assert.False(member.Captain);
        Assert.True(LevelRun.Member.Of(Shipped.Unit("captain"), Shipped).Captain);
    }

    [Fact]
    public void TheMedianLineReadsTheMedianUnitAndTheCountAtTheBar()
    {
        var companies = new List<IReadOnlyList<LevelRun.Member>>
        {
            new[] { M(9, 160, captain: true), M(7, 80), M(3, 20), M(2, 10), M(1, 0) },
            new[] { M(8, 120, captain: true), M(4, 30), M(4, 40) },
        };

        var line = LevelRun.MedianLine("the_mill", companies);

        Assert.Equal("    median the_mill: unit level p25 3 p50 4 p75 4, main-weapon rank points p25 20 p50 40 p75 40, at L7 and rank C p50 2 p75 2", line);
        Assert.Null(LevelRun.MedianLine("the_mill", []));
    }

    [Fact]
    public void TheFirstCertifiedCampIsTheFirstMapAfterWhichANonCaptainMeetsAForm()
    {
        var early = RunOf((1, [M(3, 30, ready: true, captain: true), M(2, 10)]), (2, [M(7, 80, ready: true, captain: true), M(7, 80, ready: true)]));
        var never = RunOf((1, [M(7, 80, ready: true, captain: true), M(2, 10)]));

        Assert.Equal(2, LevelRun.FirstCertifiedMap(early));
        Assert.Null(LevelRun.FirstCertifiedMap(never));
        Assert.Equal("  first non-captain to meet a form: after map p50 2 p75 2 over 1 runs; none in 1 of 2", LevelRun.FirstCertified([early, never]));
        Assert.Equal("  first non-captain to meet a form: none in 1 runs", LevelRun.FirstCertified([never]));
        Assert.Null(LevelRun.FirstCertified([]));
    }

    [Fact]
    public void TheCompaniesAfterAMapComeFromTheRunsThatWonIt()
    {
        var a = RunOf((1, [M(2, 10)]), (2, [M(3, 20)]));
        var b = RunOf((1, [M(4, 40)]));

        Assert.Equal(2, LevelRun.Companies([a, b], 1).Count);
        Assert.Single(LevelRun.Companies([a, b], 2));
    }
}
