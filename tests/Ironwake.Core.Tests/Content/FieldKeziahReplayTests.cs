using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm replay of the field on Keziah's pick under DECISIONS/0237 (seed 1350): an
/// enemy-phase bow shot from 11,8 is noise within 6 of the camp's archer and wakes the camp,
/// Kinsbane wakes on the rider, and the boss falls on turn 13 to Pell's Overcast and the
/// captain's Full Measure after one Recall. History since issue 1138: played, the boss walked off
/// his fort to 18,10 with no strike on turn 8; now he holds the fort, and the line no longer
/// replays strictly past that phase. Played loose, he still falls on his fort on turn 13.
/// </summary>
[Collection("console")]
public class FieldKeziahReplayTests
{
    [Fact]
    public void TheFieldOnKeziahsPickReplayOnSeed1350NowFindsTheBossOnHisFort()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-05-the_field-1350.script");
        var args = new[] { "campaign", "--seed", "1350", "--from", "the_field", "--pick", "keziah", "--fed", "10", "--level", "5", "--script", script, "--content", Fixture.RealContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("The camp group wakes (noise)\n", output);
        Assert.Contains("Recalled to state 243; 2 charges left\n", output);
        Assert.DoesNotContain("Sworn Captain moves 18,6 -> 18,10", output);
        Assert.Contains("Sworn Captain waits\n", EnemyPhase(output, 8));
        Assert.Contains("Sworn Captain falls at 18,6\n", output);
    }

    private static string EnemyPhase(string output, int turn)
    {
        var start = output.IndexOf($"-- Enemy phase, turn {turn} --", StringComparison.Ordinal);
        return output[start..output.IndexOf($"-- Enemy phase ends, turn {turn} --", start, StringComparison.Ordinal)];
    }

}
