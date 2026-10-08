using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm replay of the field on Rook's pick under DECISIONS/0237 (seed 1390): the south
/// route taken, so the drift sends the line group down column 12 to 12,14 and it arrives strung
/// out; the boss holds his fort when Rook wakes the camp (issue 1138: before it, he sallied to
/// 18,10 with no strike and was refused home); he falls on his fort on turn 15 to Pell, Teodor and
/// the captain's Full Measure, with no Recall spent.
/// </summary>
[Collection("console")]
public class FieldRookReplayTests
{
    [Fact]
    public void TheFieldOnRooksPickReplayOnSeed1390IsWonOnTurn15()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-05-the_field-1390.script");
        var args = new[] { "campaign", "--seed", "1390", "--from", "the_field", "--pick", "rook", "--level", "5", "--strict", "--script", script, "--content", Fixture.GustContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("The line group wakes and makes for 12,14, the crossing you took: Archer 1, Soldier 1, Soldier 2\n", output);
        Assert.DoesNotContain("Sworn Captain moves", output);
        Assert.Contains("Sworn Captain falls at 18,6\n", output);
        Assert.DoesNotContain("Recalled to state", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-05-the_field-1390.txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Code's warm replay of the field on Rook's pick under DECISIONS/0237 (seed 1440): the captain
    /// spares Keziah on turn 2 instead of Rook turning her, the north route is taken, so the drift
    /// sends the rider to 12,8 alone; the boss sallies twice, refuses and goes home, and falls on his
    /// fort on turn 13 to Pell's Gust, Maud's Radiance and the captain's Full Measure, with no Recall
    /// spent.
    /// </summary>
    [Fact]
    public void TheFieldOnRooksPickReplayOnSeed1440WithKeziahSparedNowFindsTheBossOnHisFort()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-05-the_field-1440.script");
        var args = new[] { "campaign", "--seed", "1440", "--from", "the_field", "--pick", "rook", "--level", "5", "--script", script, "--content", Fixture.GustContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("Alder Fenn talks Keziah round: spared, off the field (14 hp)\n", output);
        Assert.Contains("The south group wakes and makes for 12,8, the crossing you took: Rider\n", output);
        Assert.Contains("Sworn Captain waits\n", EnemyPhase(output, 9));
        Assert.DoesNotContain("Sworn Captain moves", output);
        Assert.Contains("Sworn Captain falls at 18,6\n", output);
        Assert.DoesNotContain("Recalled to state", output);
    }

    private static string EnemyPhase(string output, int turn)
    {
        var start = output.IndexOf($"-- Enemy phase, turn {turn} --", StringComparison.Ordinal);
        return output[start..output.IndexOf($"-- Enemy phase ends, turn {turn} --", start, StringComparison.Ordinal)];
    }

}
