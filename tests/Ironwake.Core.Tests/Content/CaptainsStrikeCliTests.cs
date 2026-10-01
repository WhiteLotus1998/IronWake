using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The captain's strike at the console (issue 636): the forecast's art line prints the uses left
/// this map and the next-phase cost, the phase that pays it says so, and Code's journaled Tollgate
/// play on seed 636 replays whole under <c>--strict</c> on the shipped content.
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

        var output = ConsoleCapture.Run(() => code = Program.Main(new[] { "play", "the_tollgate", "--seed", "636", "--script", script, "--strict", "--content", Fixture.RealContentDirectory() }));

        Assert.Equal(0, code);
        Assert.Contains("  Art Full Measure: Iron Sword at mt 13 hit 105 crit 20 wt 5 range 1-1; spends up to 4 of 40 uses, 2 of them hit or miss; 1 of 1 left this map; costs the next phase: no move, no act\n", output);
        Assert.Contains("-- Player phase, turn 4 --\nAlder Fenn is spent from the strike and cannot move or act this phase\n", output);
        Assert.DoesNotContain("Rejected ", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }
}
