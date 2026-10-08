using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm play of Saltmarsh Ford at the campaign's floor under DECISIONS/0278 as amended by
/// 0336 (seed 3200, map 3, the default four at L1): the fort group is woken across the ford and
/// four keyed misses on the wounded wingrider spend the first Recall; turn 4 is recalled twice
/// until the kills land in an order that leaves nobody lethal; the leader's brace is stripped by
/// a missed throw at Teodor, the captain's Full Measure crits him dead on turn 7, and the late
/// pair is routed on turn 12 with no Recall left.
/// </summary>
[Collection("console")]
public class SaltmarshFloorReplayTests
{
    [Fact]
    public void TheSaltmarshReplayOnSeed3200AtTheFloorIsRoutedOnTurn12WithEveryRecallSpent()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-08-saltmarsh_ford-3200.script");
        var args = new[] { "campaign", "--seed", "3200", "--from", "saltmarsh_ford", "--level", "1", "--strict", "--script", script, "--content", Fixture.GustContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("Deploys to Saltmarsh Ford: Alder Fenn, Wren, Teodor, Ottilie (deploy 4 of 8)\n", output);
        Assert.Contains("Recalled to state 48; 0 charges left\n", output);
        Assert.Contains("  Alder Fenn crits Bandit Leader for 45 (hp 0)\n", output);
        Assert.Contains("Saltmarsh Ford won: rout; reward 800, the purse holds 1300; nobody fell\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-08-saltmarsh_ford-3200.txt")).ReplaceLineEndings("\n"), output);
    }
}
