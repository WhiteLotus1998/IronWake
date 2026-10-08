using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm play of Saltmarsh Ford's late sample (<c>saltmarsh_ford_wakes_late.map</c>, issue 1365,
/// DECISIONS/0339) at the campaign's floor under DECISIONS/0278 as amended by 0336 (seed 3300, map 3,
/// the default four at L1), Table round 472's next read: Teodor wakes the fort from 10,5 on turn 2, the
/// pair arrives at the start of turn 3, Teodor braced on 9,6 faces it on turn 5 while Ottilie and Wren
/// finish the fort's soldier and archer, all three Recalls are spent by turn 10, and the leader dies on
/// turn 12 after his 63 percent axe at Wren, at 6 HP, misses.
/// </summary>
[Collection("console")]
public class SaltmarshWakesLateFloorReplayTests
{
    [Fact]
    public void TheSaltmarshLateSampleReplayOnSeed3300AtTheFloorIsRoutedOnTurn12WithEveryRecallSpent()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-08-saltmarsh_ford_wakes_late-3300.script");
        var args = new[] { "campaign", "--seed", "3300", "--from", "saltmarsh_ford", "--level", "1", "--strict", "--script", script, "--content", Fixture.SaltmarshWakesLateContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("Deploys to Saltmarsh Ford: Alder Fenn, Wren, Teodor, Ottilie (deploy 4 of 8)\n", output);
        Assert.Contains("-- Player phase, turn 3 --\nReinforcements arrive\n  Brigand arrives at 0,9 with the ford group, aggressive\n", output);
        Assert.Contains("Recalled to state 127; 0 charges left\n", output);
        Assert.Contains("  Bandit Leader misses Wren\n", output);
        Assert.Contains("Saltmarsh Ford won: rout; reward 800, the purse holds 1300; nobody fell\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-08-saltmarsh_ford_wakes_late-3300.txt")).ReplaceLineEndings("\n"), output);
    }
}
