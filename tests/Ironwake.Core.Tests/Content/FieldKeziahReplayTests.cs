using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm replay of the field on Keziah's pick under DECISIONS/0237 (seed 1350): an
/// enemy-phase bow shot from 11,8 is noise within 6 of the camp's archer and wakes the camp,
/// Kinsbane wakes on the rider, and the boss falls on turn 13 to Pell's Overcast and the
/// captain's Full Measure after one Recall.
/// </summary>
[Collection("console")]
public class FieldKeziahReplayTests
{
    [Fact]
    public void TheFieldOnKeziahsPickReplayOnSeed1350IsWonOnTurn13()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-05-the_field-1350.script");
        var args = new[] { "campaign", "--seed", "1350", "--from", "the_field", "--pick", "keziah", "--fed", "10", "--level", "5", "--strict", "--script", script, "--content", Fixture.RealContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("The camp group wakes (noise)\n", output);
        Assert.Contains("Recalled to state 243; 2 charges left\n", output);
        Assert.Contains("Sworn Captain falls at 18,6\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-05-the_field-1350.txt")).ReplaceLineEndings("\n"), output);
    }
}
