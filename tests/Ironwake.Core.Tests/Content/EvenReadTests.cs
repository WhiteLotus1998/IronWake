using Ironwake.Content;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The even chair's read (issue 1150): per map after map 5, the non-captains at L7, L6 and L5 with
/// rank C, each half alone, what the closest form still refuses, and round 387's verdict ladder read
/// on the company standing after map 8.
/// </summary>
public class EvenReadTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static LevelRun.Member M(int level, int rank, bool captain = false) => new(level, rank, false, captain);

    private static IReadOnlyList<IReadOnlyList<LevelRun.Member>> Companies(params LevelRun.Member[][] companies) => companies;

    [Theory]
    [InlineData(7, "the bar stands")]
    [InlineData(6, "the level half drops to L6, rank C kept")]
    [InlineData(5, "the level half drops to L5, rank C kept")]
    [InlineData(4, "the bar is not the lever, the curve is (back to the Table)")]
    public void TheVerdictLadderReadsTheMapEightMedian(int level, string verdict)
    {
        var companies = Companies([M(9, 160, captain: true), M(level, 80)], [M(9, 160, captain: true), M(level, 80)], [M(9, 160, captain: true), M(1, 0)]);

        Assert.EndsWith(verdict, LevelRun.Verdict(companies));
    }

    [Fact]
    public void TheVerdictCountsNoCaptain()
    {
        var companies = Companies([M(9, 160, captain: true), M(1, 0)]);

        Assert.EndsWith("the curve is (back to the Table)", LevelRun.Verdict(companies));
    }

    [Fact]
    public void TheVerdictNeedsTheRankHalfToo()
    {
        var companies = Companies([M(8, 79)]);

        Assert.EndsWith("the curve is (back to the Table)", LevelRun.Verdict(companies));
    }

    [Fact]
    public void AUnitThatMeetsAFormHasNothingRefused()
    {
        var pell = Shipped.Unit("pell");
        var reason = Shipped.Weapons["cinder"].Type;
        var ready = pell with { Level = 7, Skill = pell.Skill.With(reason, WeaponRanks.Threshold(WeaponRank.C)) };

        var member = LevelRun.Member.Of(ready, Shipped);

        Assert.True(member.Ready);
        Assert.Empty(member.Refused);
    }

    [Fact]
    public void TheClosestFormNamesWhatItRefuses()
    {
        var pell = Shipped.Unit("pell");
        var reason = Shipped.Weapons["cinder"].Type;
        var short1 = pell with { Level = 6, Skill = pell.Skill.With(reason, WeaponRanks.Threshold(WeaponRank.C)) };

        var member = LevelRun.Member.Of(short1, Shipped);

        Assert.False(member.Ready);
        Assert.Equal(["level"], member.Refused);
    }

    [Fact]
    public void TheEvenLinesStartAfterMapFiveAndEndOnTheVerdict()
    {
        var camp = new LevelRun.Camp(1, [], 0, 0);
        LevelRun.Member[] company = [M(9, 160, captain: true), M(7, 80), M(6, 80)];
        var maps = Enumerable.Range(1, 8).Select(n => (n, (IReadOnlyList<int>)company.Select(m => m.Level).ToList(), 0, camp)).ToList();
        var run = new LevelRun.Run(maps, null) { Companies = maps.Select(_ => (IReadOnlyList<LevelRun.Member>)company).ToList(), Handed = 3 };

        var lines = LevelRun.EvenLines(Shipped, [run]).ToList();

        Assert.EndsWith("kills handed 3", lines[0]);
        Assert.StartsWith("  map 6 ", lines[1]);
        Assert.Contains("non-captains at L7+C p50 1 p75 1, L6+C p50 2 p75 2, L5+C p50 2 p75 2; L7 alone p50 1 p75 1, rank C alone p50 2 p75 2", lines[1]);
        Assert.Equal("    at L7+C meeting no form: 1", lines[2]);
        Assert.DoesNotContain(lines, l => l.StartsWith("  map 5 ", StringComparison.Ordinal));
        Assert.Equal("  verdict (round 387, after map 8, 1 companies): p50 1 non-captains at L7+C; the bar stands", lines[^1]);
    }
}
