using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The captain's strike at the console (issue 636): the forecast's art line prints the uses left
/// this map and the next-phase cost, the phase that pays it says so, and Code's journaled Tollgate
/// play on seed 636 replays whole under <c>--strict</c> on the content it was played on, before
/// Full Measure struck once (issue 739), and the shipped forecast says it never doubles.
/// </summary>
[Collection("console")]
public class CaptainsStrikeCliTests
{
    private static string Transcript(string name) =>
        Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "transcripts", name);

    [Fact]
    public void TheJournaledStrikePlayReplaysAndPrintsTheCostWhereItIsChosenAndWhereItIsPaid()
    {
        var script = Transcript("2026-10-01-the_tollgate-636-strike.script");
        var code = 0;

        var output = ConsoleCapture.Run(() => code = Program.Main(new[] { "play", "the_tollgate", "--seed", "636", "--script", script, "--strict", "--content", Fixture.DoublingStrikeContentDirectory() }));

        Assert.Equal(0, code);
        Assert.Contains("  Technique Full Measure: Iron Sword at acc 105 power 13 crit 20 wt 5 range 1-1; spends up to 4 of 40 uses, 2 of them hit or miss; 1 of 1 left this map; costs the next phase: no move, no act\n", output);
        Assert.Contains("-- Player phase, turn 4 --\nAlder Fenn is spent from the strike and cannot move or act this phase\n", output);
        Assert.DoesNotContain("Rejected ", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    [Fact]
    public void TheShippedForecastSaysTheStrikeNeverDoubles()
    {
        var lines = File.ReadAllLines(Transcript("2026-10-01-the_tollgate-636-strike.script")).Take(13)
            .Append("forecast captain toll_brigand-1").Append("forecast captain toll_brigand-1 art full_measure");
        var script = Path.Combine(Path.GetTempPath(), $"ironwake-739-{Guid.NewGuid():N}.script");
        File.WriteAllLines(script, lines);

        var output = ConsoleCapture.Run(() => Program.Main(new[] { "play", "the_tollgate", "--seed", "636", "--script", script, "--content", Fixture.RealContentDirectory() }));

        Assert.Contains("Forecast Alder Fenn -> Toll Brigand: acc 63% dmg 10 x2 crit 5%; counter: acc 39% dmg 10 crit 0%\n", output);
        Assert.Contains("Forecast Alder Fenn -> Toll Brigand: acc 97% dmg 18 crit 25%; counter: acc 39% dmg 10 crit 0%\n", output);
        Assert.Contains("  Technique Full Measure: Iron Sword at acc 105 power 13 crit 20 wt 5 range 1-1; spends up to 3 of 40 uses, 2 of them hit or miss; 1 of 1 left this map; x1, never doubles; costs the next phase: no move, no act\n", output);
    }

    [Fact]
    public void TheJournaledSingleStrikePlayReplaysOnTheShippedContent()
    {
        var script = Transcript("2026-10-02-the_tollgate-739.script");
        var code = 0;

        var output = ConsoleCapture.Run(() => code = Program.Main(new[] { "play", "the_tollgate", "--seed", "739", "--script", script, "--strict", "--content", Fixture.RealContentDirectory() }));

        Assert.Equal(0, code);
        Assert.Contains("  Alder Fenn hits Bandit Leader for 17 (hp 0)\n", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }
}
