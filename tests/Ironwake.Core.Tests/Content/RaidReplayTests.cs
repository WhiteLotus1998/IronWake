using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm play of the Raid on Ironwake under DECISIONS/0278 (seed 1570, Keziah picked and
/// left on the bench, the company at L4 against the raid's L6): the van dies in the gaps on turn
/// 2, the archer and the hexer on turn 3, the waves' brigand and soldier on turn 4 and the last
/// brigand on turn 5, with two Recalls spent on exposures `threat` priced and nobody fallen.
/// </summary>
[Collection("console")]
public class RaidReplayTests
{
    [Fact]
    public void TheRaidReplayOnSeed1570IsRoutedOnTurn5WithTwoRecalls()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-06-ironwake_raid-1570.script");
        var args = new[] { "campaign", "--seed", "1570", "--from", "ironwake_raid", "--level", "4", "--strict", "--script", script, "--content", Fixture.RealContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("Recalled to state 46; 2 charges left\n", output);
        Assert.Contains("Recalled to state 65; 1 charge left\n", output);
        Assert.Contains("  Pell hits Brigand 2 for 19 (hp 5)\n", output);
        Assert.Contains("Raid on Ironwake won: rout; reward 1000, the purse holds 1500; nobody fell\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-06-ironwake_raid-1570.txt")).ReplaceLineEndings("\n"), output);
    }
}
