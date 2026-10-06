using Ironwake.Sim;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The focused chair's read (issue 1157, round 389): the bar is p50 1 non-captain at L7 and rank C after
/// map 8 on the paying line, the even chair the ceiling at p50 0 on L7+C and on L7 alone (round 392), and a pass that leaves the captain below
/// L7 reopens the curve; the paying line prints its price. From issue 1167 the bar is read on the striking line,
/// whose price also prints the fed unit's combats and handed strikes per map.
/// </summary>
public class FocusedReadTests
{
    private static LevelRun.Member M(int level, int rank, bool captain = false, string id = "") => new(level, rank, false, captain) { Id = id };

    private static IReadOnlyList<IReadOnlyList<LevelRun.Member>> Companies(params LevelRun.Member[][] companies) => companies;

    private static readonly IReadOnlyList<IReadOnlyList<LevelRun.Member>> Unfed = Companies([M(7, 160, captain: true), M(3, 30, id: FocusedPlayer.Fed)]);

    [Fact]
    public void TheBarPassesOnTheStrikingLineWithTheCaptainAtSeven()
    {
        var paying = Companies([M(7, 160, captain: true), M(7, 80)]);

        Assert.EndsWith("the bar passes (striking p50 1 at L7+C, captain p50 L7); a door has to be fed; the ceiling holds (even p50 0 at L7+C, p50 0 at L7 alone)", LevelRun.FocusedVerdict(Unfed, paying));
    }

    [Fact]
    public void APassWithTheCaptainBelowSevenReopensTheCurve()
    {
        var paying = Companies([M(6, 160, captain: true), M(7, 80)]);

        Assert.Contains("but the captain is at p50 L6; the curve reopens", LevelRun.FocusedVerdict(Unfed, paying));
    }

    [Fact]
    public void ABarFailingOnRankSendsRankToItsOwnLever()
    {
        var striking = Companies([M(7, 160, captain: true), M(6, 40, id: FocusedPlayer.Fed)]);

        Assert.Contains($"the bar fails (striking p50 0 at L7+C; {FocusedPlayer.Fed} p50 L6, rank points p50 40); his rank is short of C with him striking every safe turn; rank takes its own lever (round 392)", LevelRun.FocusedVerdict(Unfed, striking));
    }

    [Fact]
    public void ABarFailingOnLevelAloneGoesBackToTheTable()
    {
        var striking = Companies([M(7, 160, captain: true), M(6, 80, id: FocusedPlayer.Fed)]);

        Assert.Contains($"the bar fails (striking p50 0 at L7+C; {FocusedPlayer.Fed} p50 L6, rank points p50 80); his rank reaches C and the level half is short; the level half goes back to the Table", LevelRun.FocusedVerdict(Unfed, striking));
    }

    [Fact]
    public void AnEvenChairAtTheBarBreaksTheCeiling()
    {
        var even = Companies([M(7, 160, captain: true), M(7, 80)]);

        Assert.EndsWith("the ceiling is broken (even p50 1 at L7+C, p50 1 at L7 alone); step the levy floor's offset first (round 392)", LevelRun.FocusedVerdict(even, Unfed));
    }

    [Fact]
    public void AnEvenChairAtLevelSevenWithoutRankCBreaksTheCeiling()
    {
        var even = Companies([M(7, 160, captain: true), M(7, 11)]);

        Assert.EndsWith("the ceiling is broken (even p50 0 at L7+C, p50 1 at L7 alone); step the levy floor's offset first (round 392)", LevelRun.FocusedVerdict(even, Unfed));
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

    [Fact]
    public void TheStrikingPriceLinePrintsCombatsAndStrikesPerMap()
    {
        var camp = new LevelRun.Camp(1, [], 0, 0);
        LevelRun.Member[] company = [M(7, 160, captain: true, id: "captain"), M(6, 50, id: FocusedPlayer.Fed)];
        var maps = Enumerable.Range(1, 9).Select(n => (n, (IReadOnlyList<int>)company.Select(m => m.Level).ToList(), 0, camp)).ToList();
        var prices = Enumerable.Range(1, 9).Select(_ => new LevelRun.Price(1, 0, 10, 0) { Combats = 3, Strikes = 2 }).ToList();
        var run = new LevelRun.Run(maps, null) { Companies = maps.Select(_ => (IReadOnlyList<LevelRun.Member>)company).ToList(), Prices = prices };

        var line = LevelRun.PriceLine([run], "striking");

        Assert.StartsWith("  price (striking, through map 8, 1 runs):", line);
        Assert.EndsWith("falls 0; per map, combats p50 3 p75 3, strikes handed p50 2 p75 2", line);
    }
}
