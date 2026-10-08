using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's seed 3400 floor line on Sallow Grange (issue 1372) replayed under the <c>goes_home: hall</c>
/// sample: the Reeve still leaves the gate on enemy phase 4 to throw at Rook, but on enemy phase 5 he
/// walks home to 15,6 and strikes the captain from the gate instead of throwing at Wren from 12,7; the
/// captain still seizes on turn 6 past a living Reeve, with no Recall spent and nobody fallen, which
/// is the issue's kill criterion firing on the old line.
/// </summary>
[Collection("console")]
public class SallowGrangeHomeReplayTests
{
    [Fact]
    public void UnderGoesHomeTheReeveWalksBackToTheGateButTheSeedThreeFourHundredLineStillSeizes()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-08-sallow_grange-3400.script");
        var args = new[] { "campaign", "--seed", "3400", "--from", "sallow_grange", "--level", "4", "--pick", "rook", "--strict", "--script", script, "--content", Fixture.SallowHomeContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("goes_home: the hall group strikes out from its posts, and off them walks home and strikes only from there\n", output);
        Assert.Contains("Grange Reeve moves 15,6 -> 12,7 via 14,6 13,6 12,6\n", output);
        Assert.Contains("Grange Reeve moves 12,7 -> 15,6 via 12,6 13,6 14,6\n", output);
        Assert.Contains("  Grange Reeve hits Alder Fenn for 9 (hp 14)\n", output);
        Assert.Contains("Sallow Grange won: seize; reward 1400, the purse holds 1750; nobody fell\n", output);
        Assert.DoesNotContain("Recalled to state", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-08-sallow_grange_home-3400.txt")).ReplaceLineEndings("\n"), output);
    }
}
