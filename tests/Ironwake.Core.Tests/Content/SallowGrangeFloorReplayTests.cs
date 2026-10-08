using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm play of Sallow Grange at the campaign's floor under DECISIONS/0278 as amended by
/// 0336 (seed 3400, map 7, the floor six at L4 with Rook picked): the field group dies on turns 2
/// and 3, the Reeve is woken from the gap on turn 4 and leaves his post to throw at Rook over the
/// wall, and the captain walks past him to the gate on turn 6 with the Reeve and the hexer
/// untouched, no Recall spent and nobody fallen (issue 1372).
/// </summary>
[Collection("console")]
public class SallowGrangeFloorReplayTests
{
    [Fact]
    public void TheSallowGrangeReplayOnSeed3400AtTheFloorIsSeizedOnTurn6PastALivingReeve()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-08-sallow_grange-3400.script");
        var args = new[] { "campaign", "--seed", "3400", "--from", "sallow_grange", "--level", "4", "--pick", "rook", "--strict", "--script", script, "--content", Fixture.RealContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("Deploys to Sallow Grange: Alder Fenn, Rook, Wren, Teodor, Pell, Ottilie (deploy 6 of 9)\n", output);
        Assert.Contains("Grange Reeve moves 15,6 -> 12,7 via 14,6 13,6 12,6\n", output);
        Assert.Contains("  Grange Reeve hits Rook for 13 (hp 5)\n", output);
        Assert.Contains("Alder Fenn moves 14,5 -> 16,6 via 15,5 16,5\n", output);
        Assert.Contains("Sallow Grange won: seize; reward 1400, the purse holds 1750; nobody fell\n", output);
        Assert.DoesNotContain("Recalled to state", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-08-sallow_grange-3400.txt")).ReplaceLineEndings("\n"), output);
    }
}
