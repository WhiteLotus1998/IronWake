using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm play of the Raid on Ironwake under DECISIONS/0278 (seed 2055, Keziah picked and
/// seated by benching Dunstan and Maud, the company at L4 against the raid's L6): the van's two
/// melee die in the gaps on turn 2, the company walks out through the north gap, the archer, the
/// hexer and the first wave brigand die on turn 3 after one Recall, and the last two on turn 4.
/// </summary>
[Collection("console")]
public class RaidKeziahReplayTests
{
    [Fact]
    public void TheRaidReplayOnSeed2055WithKeziahSeatedIsRoutedOnTurn4WithOneRecall()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-08-ironwake_raid-2055.script");
        var args = new[] { "campaign", "--seed", "2055", "--from", "ironwake_raid", "--level", "4", "--strict", "--script", script, "--content", Fixture.RealContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("Deploys to Raid on Ironwake: Alder Fenn, Wren, Teodor, Ottilie, Pell, Keziah (deploy 6 of 9)\n", output);
        Assert.Contains("Recalled to state 46; 2 charges left\n", output);
        Assert.Contains("Kinsbane feeds: fed 2, power +1, a tooth grows (teeth 1/5)\n", output);
        Assert.Contains("Raid on Ironwake won: rout; reward 1000, the purse holds 1500; nobody fell\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-08-ironwake_raid-2055.txt")).ReplaceLineEndings("\n"), output);
    }
}
