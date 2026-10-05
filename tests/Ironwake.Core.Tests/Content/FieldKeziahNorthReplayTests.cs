using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm replay of the field on Keziah's pick under DECISIONS/0237 (seed 1400): the north
/// route, the line group woken on turn 7 so the south group's drift waits for it, two Recalls on
/// one turn-4 swing that rolls the same, Kinsbane woken on the camp's brigand, and the boss
/// falling off his fort on turn 13 to Pell's Overcast, Ottilie and the captain's Full Measure.
/// </summary>
[Collection("console")]
public class FieldKeziahNorthReplayTests
{
    [Fact]
    public void TheFieldOnKeziahsPickNorthReplayOnSeed1400IsWonOnTurn13()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-05-the_field-1400.script");
        var args = new[] { "campaign", "--seed", "1400", "--from", "the_field", "--pick", "keziah", "--fed", "10", "--level", "5", "--strict", "--script", script, "--content", Fixture.RealContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("Recalled to state 80; 1 charge left\n", output);
        Assert.Contains("The south group wakes and makes for 12,8, the crossing you took: Rider\n", output);
        Assert.Contains("Sworn Captain falls at 15,6\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-05-the_field-1400.txt")).ReplaceLineEndings("\n"), output);
    }
}
