using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm replay of the field on Keziah's pick under DECISIONS/0237 (seed 1460): the captain
/// spares Rook on turn 2, the pickets fall by turn 3, the line group is woken on turn 5 so the
/// south group's rider drifts to the bridge, Kinsbane wakes on turn 6, Pell's Cinder takes the
/// sentry on turn 8, and Teodor finishes the boss on his fort on turn 10. The camp after the win
/// drills Teodor from L6 to L7 with EXP 0, the same level as the members who never deployed.
/// </summary>
[Collection("console")]
public class FieldKeziahSpareReplayTests
{
    [Fact]
    public void TheFieldOnKeziahsPickWithRookSparedOnSeed1460IsWonOnTurn10WithTeodorFinishingTheBoss()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-06-the_field-1460.script");
        var args = new[] { "campaign", "--seed", "1460", "--from", "the_field", "--pick", "keziah", "--fed", "10", "--level", "5", "--script", script, "--content", Fixture.RealContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("Alder Fenn talks Rook round: spared, off the field (18 hp)\n", output);
        Assert.Contains("Teodor hits Sworn Captain for 6 (hp 0)\n", output);
        Assert.Contains("The Field Before the Keep won: defeat_boss; reward 1800, the purse holds 2300; nobody fell\n", output);
        Assert.Contains("Teodor drilled with the levy: L6 -> L7.\n", output);
        Assert.Contains("Brannock drilled with the levy: L5 -> L7.\n", output);
        Assert.DoesNotContain("Recalled to state", output);
        Assert.DoesNotContain("Sworn Captain attacks", output);
    }
}
