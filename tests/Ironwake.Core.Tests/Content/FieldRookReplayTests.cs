using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm replay of the field on Rook's pick under DECISIONS/0237 (seed 1390): the south
/// route taken, so the drift sends the line group down column 12 to 12,14 and it arrives strung
/// out; the boss sallies to 18,10 when Rook wakes the camp, refuses every strike tile and goes
/// home; he falls on his fort on turn 15 to Pell, Teodor and the captain's Full Measure, with no
/// Recall spent.
/// </summary>
[Collection("console")]
public class FieldRookReplayTests
{
    [Fact]
    public void TheFieldOnRooksPickReplayOnSeed1390IsWonOnTurn15()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-05-the_field-1390.script");
        var args = new[] { "campaign", "--seed", "1390", "--from", "the_field", "--pick", "rook", "--level", "5", "--strict", "--script", script, "--content", Fixture.RealContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("The line group wakes and makes for 12,14, the crossing you took: Archer 1, Soldier 1, Soldier 2\n", output);
        Assert.Contains("Sworn Captain moves 18,6 -> 18,10 via 18,7 18,8 18,9\n", output);
        Assert.Contains("Sworn Captain falls at 18,6\n", output);
        Assert.DoesNotContain("Recalled to state", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-05-the_field-1390.txt")).ReplaceLineEndings("\n"), output);
    }
}
