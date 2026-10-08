using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm play of the Raid on Ironwake at the campaign's floor under DECISIONS/0278 as amended
/// by 0336 (seed 3100, the levy at L3 and Keziah at her join level 4, seated by benching Dunstan):
/// Keziah falls on turn 2 to a 24 percent counter and the opening is recalled; the same keyed miss
/// leaves Wren at 1 and the turn is recalled again; Pell's Overcast and Teodor clear the van; the
/// captain's rally makes Keziah's turn-3 miss survivable, and the last brigand dies on turn 5 with
/// no Recall left.
/// </summary>
[Collection("console")]
public class RaidFloorReplayTests
{
    [Fact]
    public void TheRaidReplayOnSeed3100AtTheFloorIsRoutedOnTurn5WithEveryRecallSpent()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-08-ironwake_raid-3100.script");
        var args = new[] { "campaign", "--seed", "3100", "--from", "ironwake_raid", "--level", "3", "--strict", "--script", script, "--content", Fixture.GustContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("Deploys to Raid on Ironwake: Alder Fenn, Wren, Teodor, Ottilie, Pell, Keziah (deploy 6 of 9)\n", output);
        Assert.Contains("Keziah falls at 11,7\n", output);
        Assert.Contains("Recalled to state 24; 0 charges left\n", output);
        Assert.Contains("Keziah heals 3 (hp 17)\n", output);
        Assert.Contains("Raid on Ironwake won: rout; reward 1000, the purse holds 1500; nobody fell\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-08-ironwake_raid-3100.txt")).ReplaceLineEndings("\n"), output);
    }
}
