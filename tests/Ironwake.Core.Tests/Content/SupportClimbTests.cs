using Ironwake.Content;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The supports' climb (issue 77, slice 4): the Sim's <c>--supports</c> reads the record's rapport
/// after each won campaign map against the support tiers, support pairs only.
/// </summary>
public class SupportClimbTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static SupportPair CaptainPair => Shipped.Campaign.Supports.First(p => p.Involves(Shipped.Cast[0].Id));

    private static SupportPair RecruitPair => Shipped.Campaign.Supports.First(p => !p.Involves(Shipped.Cast[0].Id));

    private static LevelRun.Run RunOf(int? lostOn, params IReadOnlyList<Rapport>[] afterEachMap) =>
        new(afterEachMap.Select((_, i) => (i + 1, (IReadOnlyList<int>)new[] { 1 }, 0, new LevelRun.Camp(1, new[] { 1 }, 0, 0))).ToList(), lostOn)
        {
            Rapport = afterEachMap,
        };

    [Fact]
    public void APairsPointsAreReadInEitherOrder()
    {
        var pair = RecruitPair;

        Assert.Equal(20, SupportRun.PointsOf(new[] { new Rapport(pair.B, pair.A, 20) }, pair));
        Assert.Equal(20, SupportRun.PointsOf(new[] { new Rapport(pair.A, pair.B, 20) }, pair));
        Assert.Equal(0, SupportRun.PointsOf(Array.Empty<Rapport>(), pair));
    }

    [Fact]
    public void EachMapCountsThePairsAtEachTierAndTheBestRecruitAndCaptainPairs()
    {
        var map1 = new[] { new Rapport(RecruitPair.A, RecruitPair.B, 40), new Rapport(CaptainPair.A, CaptainPair.B, 16) };
        var lines = SupportRun.Lines(Shipped, new[] { RunOf(null, map1) }).ToList();
        var id = Shipped.Campaign.Maps[0].MapId;

        Assert.Contains($"  map 1 {id}: won 1, at C p25 2 p50 2 p75 2, at B p25 1 p50 1 p75 1, at A p25 0 p50 0 p75 0; best pair p25 40 p50 40 p75 40, best recruit pair p25 40 p50 40 p75 40, best captain pair p25 16 p50 16 p75 16", lines);
    }

    [Fact]
    public void RapportBetweenTwoWhoAreNoSupportPairIsNotCounted()
    {
        var ids = Shipped.Cast.Select(c => c.Id).ToList();
        var (a, b) = ids.SelectMany(x => ids, (x, y) => (x, y)).First(p => p.x != p.y && Supports.Pair(Shipped.Campaign, p.x, p.y) is null);
        var lines = SupportRun.Lines(Shipped, new[] { RunOf(null, new[] { new Rapport(a, b, 90) }) }).ToList();

        Assert.Contains(lines, l => l.StartsWith("  map 1 ", StringComparison.Ordinal) && l.Contains("at C p25 0 p50 0 p75 0", StringComparison.Ordinal) && l.Contains("best pair p25 0 p50 0 p75 0", StringComparison.Ordinal));
    }

    [Fact]
    public void ThePerPairLinesCountOnlyRunsThatFinishedTheCampaign()
    {
        var high = new[] { new Rapport(RecruitPair.A, RecruitPair.B, 72) };
        var finished = RunOf(null, Array.Empty<Rapport>(), high);
        var lost = RunOf(3, high, high);

        var lines = SupportRun.Lines(Shipped, new[] { finished, lost }).ToList();

        Assert.Contains("  per pair over the 1 runs that finished the campaign: final points p50 p75, and the runs reaching each tier", lines);
        Assert.Contains($"    {RecruitPair.A} and {RecruitPair.B} ({RecruitPair.Kind.ToString().ToLowerInvariant()}): p50 72 p75 72, C 1, B 1, A 1", lines);
    }

    [Fact]
    public void TheHeaderNamesTheTiersAndThatTheReadIsAFloor()
    {
        var header = SupportRun.Lines(Shipped, Array.Empty<LevelRun.Run>()).First();

        Assert.Contains("tiers C 16, B 40, A 72", header, StringComparison.Ordinal);
        Assert.Contains("the floor of the climb", header, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSimsUsageNamesSupports()
    {
        Assert.Contains("| --supports [--seeds N] [--pair <a> <b>] |", Program.Usage);
    }
}
