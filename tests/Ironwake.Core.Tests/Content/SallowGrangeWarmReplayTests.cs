using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm play of Sallow Grange under DECISIONS/0278 (seed 1580, the default six at L5
/// against the grange's L7): the field group dies on turn 3 and its archer on turn 4, the hexer
/// on turn 6 under the captain's fall back, the Reeve on turn 7 and the gate is seized on turn 8,
/// with two Recalls spent on exposures `threat` priced and nobody fallen.
/// </summary>
[Collection("console")]
public class SallowGrangeWarmReplayTests
{
    [Fact]
    public void TheSallowGrangeReplayOnSeed1580IsSeizedOnTurn8WithTwoRecalls()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-06-sallow_grange-1580.script");
        var args = new[] { "campaign", "--seed", "1580", "--from", "sallow_grange", "--level", "5", "--strict", "--script", script, "--content", Fixture.RealContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("Recalled to state 41; 2 charges left\n", output);
        Assert.Contains("Recalled to state 77; 1 charge left\n", output);
        Assert.Contains("  Pell hits Veteran for 19 (hp 0)\n", output);
        Assert.Contains("Teodor falls back 12,7 -> 11,6 via 12,6\n", output);
        Assert.Contains("Sallow Grange won: seize; reward 1400, the purse holds 1900; nobody fell\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-06-sallow_grange-1580.txt")).ReplaceLineEndings("\n"), output);
    }
}
