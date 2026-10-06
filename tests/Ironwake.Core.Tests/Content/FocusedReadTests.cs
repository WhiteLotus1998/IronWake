using Ironwake.Sim;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The focused chair's read (issue 1157, round 389): the bar is p50 1 non-captain at L7 and rank C after
/// map 8 on the paying line, the even chair the ceiling at p50 0, and a pass that leaves the captain below
/// L7 reopens the curve; the paying line prints its price.
/// </summary>
public class FocusedReadTests
{
    private static LevelRun.Member M(int level, int rank, bool captain = false, string id = "") => new(level, rank, false, captain) { Id = id };

    private static IReadOnlyList<IReadOnlyList<LevelRun.Member>> Companies(params LevelRun.Member[][] companies) => companies;

    private static readonly IReadOnlyList<IReadOnlyList<LevelRun.Member>> Unfed = Companies([M(7, 160, captain: true), M(3, 30)]);

    [Fact]
    public void TheBarPassesOnThePayingLineWithTheCaptainAtSeven()
    {
        var paying = Companies([M(7, 160, captain: true), M(7, 80)]);

        Assert.EndsWith("the bar passes (paying p50 1 at L7+C, captain p50 L7); a door has to be fed; the ceiling holds (even p50 0)", LevelRun.FocusedVerdict(Unfed, paying));
    }

    [Fact]
    public void APassWithTheCaptainBelowSevenReopensTheCurve()
    {
        var paying = Companies([M(6, 160, captain: true), M(7, 80)]);

        Assert.Contains("but the captain is at p50 L6; the curve reopens", LevelRun.FocusedVerdict(Unfed, paying));
    }

    [Fact]
    public void ABarShortOfOneSendsTheFirstLever()
    {
        Assert.Contains("the bar fails (paying p50 0 at L7+C); the first lever is the joins' levels and ranks", LevelRun.FocusedVerdict(Unfed, Unfed));
    }

    [Fact]
    public void AnEvenChairAtTheBarBreaksTheCeiling()
    {
        var even = Companies([M(7, 160, captain: true), M(7, 80)]);

        Assert.EndsWith("the ceiling is broken (even p50 1)", LevelRun.FocusedVerdict(even, Unfed));
    }

    [Fact]
    public void ThePriceLineReadsThroughMapEight()
    {
        var camp = new LevelRun.Camp(1, [], 0, 0);
        LevelRun.Member[] company = [M(7, 160, captain: true, id: "captain"), M(4, 40, id: FocusedPlayer.Fed)];
        var maps = Enumerable.Range(1, 9).Select(n => (n, (IReadOnlyList<int>)company.Select(m => m.Level).ToList(), 0, camp)).ToList();
        var prices = Enumerable.Range(1, 9).Select(_ => new LevelRun.Price(2, 1, 10, 0)).ToList();
        var run = new LevelRun.Run(maps, null) { Companies = maps.Select(_ => (IReadOnlyList<LevelRun.Member>)company).ToList(), Prices = prices };

        var line = LevelRun.PriceLine([run]);

        Assert.Equal($"  price (paying, through map 8, 1 runs): captain level p50 7 p75 7; {FocusedPlayer.Fed} level p50 4 p75 4, main-weapon rank points p50 40 p75 40; kills handed p50 16 p75 16, given up p50 8 p75 8 (total 8); HP lost in enemy phases p50 80 p75 80, falls 0", line);
    }
}
