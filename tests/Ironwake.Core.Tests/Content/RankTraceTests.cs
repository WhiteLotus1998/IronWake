using Ironwake.Sim;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The rank trace (issue 1170, round 393): per map the fed unit's combats and strikes by phase and his
/// main-weapon points earned, kept and lost to a fall, reconciled with the record; the Recalling player's
/// bound is the record's points plus those lost to a fall; the read was agreed before the numbers.
/// </summary>
public class RankTraceTests
{
    private static LevelRun.Member M(int level, int rank, bool captain = false, string id = "") => new(level, rank, false, captain) { Id = id };

    [Fact]
    public void APointsTraceThatDoesNotReconcileIsABugFirst()
    {
        Assert.EndsWith("the count does not reconcile on 2 maps; a bug, and it goes first", LevelRun.TraceVerdict(120, 2, "lost to a fall"));
    }

    [Fact]
    public void ARecallingBoundAtCPullsNoRankLever()
    {
        Assert.EndsWith("the Recalling player's bound reaches C (p50 80); no rank lever", LevelRun.TraceVerdict(80, 0, "lost to a fall"));
    }

    [Fact]
    public void ARecallingBoundShortOfCNamesTheLargestSink()
    {
        Assert.EndsWith("the Recalling player's bound is short of C (p50 79); a rank lever is right, a door-only shape preferred (round 393); the largest sink is maps benched", LevelRun.TraceVerdict(79, 0, "maps benched"));
    }

    [Fact]
    public void AFallLosesTheWonBattlesPointsAndNothingElse()
    {
        var fell = new LevelRun.RankTrace(true, 3, 2, 4, 1, 12, true) { Kept = 0 };
        var stood = new LevelRun.RankTrace(true, 3, 3, 2, 1, 14, false) { Kept = 14 };

        Assert.Equal((12, 0), (fell.LostToFall, fell.Unexplained));
        Assert.Equal((0, 0), (stood.LostToFall, stood.Unexplained));
        Assert.Equal(4, fell.Strikeless);
    }

    [Fact]
    public void PointsTheRecordLostWithoutAFallAreUnexplained()
    {
        var leaked = new LevelRun.RankTrace(true, 2, 2, 0, 0, 6, false) { Kept = 3 };

        Assert.Equal(3, leaked.Unexplained);
    }

    [Fact]
    public void TheTraceReconcilesAndAddsTheFallsToTheBound()
    {
        var camp = new LevelRun.Camp(1, [], 0, 0);
        LevelRun.Member[] company = [M(7, 160, captain: true, id: "captain"), M(6, 30, id: FocusedPlayer.Fed)];
        var maps = Enumerable.Range(1, 8).Select(n => (n, (IReadOnlyList<int>)company.Select(m => m.Level).ToList(), 0, camp)).ToList();
        var prices = Enumerable.Range(1, 8).Select(n => new LevelRun.Price(0, 0, 0, 0)
        {
            Rank = n switch
            {
                1 => LevelRun.RankTrace.Benched,
                3 => new LevelRun.RankTrace(true, 2, 2, 3, 1, 9, true) { Kept = 0, LostTries = 6 },
                _ => new LevelRun.RankTrace(true, 2, 2, 1, 0, 5, false) { Kept = 5 },
            },
        }).ToList();
        var run = new LevelRun.Run(maps, null) { Companies = maps.Select(_ => (IReadOnlyList<LevelRun.Member>)company).ToList(), Prices = prices };
        var content = Ironwake.Content.ContentLoader.Load(Fixture.RealContentDirectory());

        var lines = LevelRun.RankTraceLines(content, [run]).ToList();

        Assert.Contains("    map 1 starting_alone: deployed 0 of 1; combats player - (struck -), enemy - (struck -); points earned -, kept -, lost to a fall - (fell 0), in lost tries -", lines);
        Assert.Contains("  after map 8: the record reads p50 30 p75 30; the Recalling player's bound (plus the points lost to a fall) p50 39 p75 39", lines);
        Assert.Contains("  reconciles: in-battle points against the record on 8 of 8 maps (unexplained 0 points), camps that moved his points 0", lines);
        Assert.Contains("  per run: points earned p50 39 p75 39, kept p50 30 p75 30, lost to a fall p50 9 p75 9, in lost tries p50 6 p75 6, at camps p50 0 p75 0", lines);
        Assert.Equal("  verdict (round 393, after map 8): the Recalling player's bound is short of C (p50 39); a rank lever is right, a door-only shape preferred (round 393); the largest sink is strikeless combats at 3", lines[^1]);
    }
}
