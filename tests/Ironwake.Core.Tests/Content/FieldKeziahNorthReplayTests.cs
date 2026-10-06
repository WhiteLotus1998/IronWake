using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm replay of the field on Keziah's pick under DECISIONS/0237 (seed 1400): the north
/// route, the line group woken on turn 7 so the south group's drift waits for it, two Recalls on
/// one turn-4 swing that rolls the same, Kinsbane woken on the camp's brigand, and the boss
/// falling off his fort on turn 13 to Pell's Overcast, Ottilie and the captain's Full Measure.
/// History since issue 1138: played, the boss stepped off his fort to 15,6 with no strike on turn
/// 12, onto the tile where he fell; now he holds the fort, and the line's last strikes find nothing.
/// </summary>
[Collection("console")]
public class FieldKeziahNorthReplayTests
{
    [Fact]
    public void TheFieldOnKeziahsPickNorthReplayOnSeed1400NowFindsTheBossOnHisFort()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-05-the_field-1400.script");
        var args = new[] { "campaign", "--seed", "1400", "--from", "the_field", "--pick", "keziah", "--fed", "10", "--level", "5", "--script", script, "--content", Fixture.RealContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("Recalled to state 80; 1 charge left\n", output);
        Assert.Contains("The south group wakes and makes for 12,8, the crossing you took: Rider\n", output);
        Assert.Contains("Sworn Captain waits\n", EnemyPhase(output, 12));
        Assert.DoesNotContain("Sworn Captain moves", output);
        Assert.DoesNotContain("Sworn Captain falls", output);
    }

    private static string EnemyPhase(string output, int turn)
    {
        var start = output.IndexOf($"-- Enemy phase, turn {turn} --", StringComparison.Ordinal);
        return output[start..output.IndexOf($"-- Enemy phase ends, turn {turn} --", start, StringComparison.Ordinal)];
    }

}
