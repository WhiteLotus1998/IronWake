using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's seed 3400 floor line on Sallow Grange replayed under the <c>seize_hold: 1</c> sample
/// (issue 1383): the step onto the gate on turn 6 no longer wins on the step, but the hold costs
/// nothing, because the Gate tile's avoid leaves the Reeve a 9 percent swing at the holder and he
/// walks off his post to strike Wren instead; the map is won when turn 6's enemy phase ends, with no
/// Recall spent and nobody fallen.
/// </summary>
[Collection("console")]
public class SallowGrangeHoldReplayTests
{
    [Fact]
    public void UnderSeizeHoldTheGateTilePricesTheReevesSwingAtNineAndTheSeedThreeFourHundredLineHoldsFree()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-08-sallow_grange_hold-3400.script");
        var args = new[] { "campaign", "--seed", "3400", "--from", "sallow_grange", "--level", "4", "--pick", "rook", "--strict", "--script", script, "--content", Fixture.SallowHoldContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("Alder Fenn stands on the gate. The map is won if Alder Fenn still stands there when turn 6's enemy phase ends.\n", output);
        Assert.Contains("  Grange Reeve from 15,5 with Toll Spear (slot 1): acc 9% dmg 6 crit 0%; counter: none\n", output);
        Assert.Contains("Grange Reeve moves 15,6 -> 13,6 via 14,6\n", output);
        Assert.Contains("  Grange Reeve hits Wren for 10 (hp 11)\n", output);
        Assert.Contains("Battle won: seize; only recall is left, or leave\n", output);
        Assert.DoesNotContain("Recalled to state", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-08-sallow_grange_hold-3400.txt")).ReplaceLineEndings("\n"), output);
    }
}
