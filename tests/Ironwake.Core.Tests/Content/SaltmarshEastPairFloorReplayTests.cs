using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm play of Saltmarsh Ford's east-pair sample (<c>saltmarsh_ford_east_pair.map</c>, issue 1370)
/// at the campaign's floor under DECISIONS/0278 as amended by 0336 (seed 1370, map 3, the default four at
/// L1): the fort's archer, wingrider and soldier die on the south bank by turn 5, the captain's bait on 10,2
/// on turn 6 brings the pair in on 13,4 and 13,5 that same phase, Teodor dresses rather than braces against
/// a lethal 18, the pair dies on turn 7, and the leader dies on turn 9 to Ottilie's crit and Full Measure,
/// no Recall spent.
/// </summary>
[Collection("console")]
public class SaltmarshEastPairFloorReplayTests
{
    [Fact]
    public void TheSaltmarshEastPairSampleReplayOnSeed1370AtTheFloorIsRoutedOnTurn9WithTheBaitBringingThePair()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-08-saltmarsh_ford_east_pair-1370.script");
        var args = new[] { "campaign", "--seed", "1370", "--from", "saltmarsh_ford", "--level", "1", "--strict", "--script", script, "--content", Fixture.SaltmarshEastPairContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("Deploys to Saltmarsh Ford: Alder Fenn, Wren, Teodor, Ottilie (deploy 4 of 8)\n", output);
        Assert.Contains("Reinforcements arrive\n  Brigand arrives at 13,4 with the ford group, aggressive\n", output);
        Assert.Contains("  Ottilie crits Bandit Leader for 12 (hp 14)\n", output);
        Assert.Contains("Saltmarsh Ford won: rout; reward 800, the purse holds 1300; nobody fell\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-08-saltmarsh_ford_east_pair-1370.txt")).ReplaceLineEndings("\n"), output);
    }
}
