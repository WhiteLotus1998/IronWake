using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm hand play of Grit's one reshape (issue 1489): the Tollgate under <c>forms: on</c> and
/// <c>grit_gain: hits</c> (<c>docs/samples/the_tollgate_forms.map</c>), seed 1489. The woods archer, chipped by the
/// captain, spends its Grit on an Aimed Shot at Teodor; Teodor's hit buys his Long Thrust at the warden from the
/// forest; Pell falls to the Bandit Leader's plain swing on turn 9, and the captain takes the gate on turn 10.
/// </summary>
[Collection("console")]
public class TollgateGritHitsReplayTests
{
    [Fact]
    public void TheGritHitsTollgateReplayOnSeed1489IsSeizedOnTurn10()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-09-the_tollgate_forms-1489.script");
        var map = Path.Combine(root, "docs", "samples", "the_tollgate_forms.map");
        var args = new[] { "play", map, "--seed", "1489", "--strict", "--script", script, "--content", Fixture.RealContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("forms: a form costs Grit, not uses; Grit starts at 1, +1 for each hit taken, at most 3\n", output);
        Assert.Contains("Archer 2 declares Aimed Shot with Iron Bow, spending 2 Grit\n", output);
        Assert.Contains("Teodor declares Long Thrust with Iron Lance, spending 2 Grit\n", output);
        Assert.Contains("Battle won: seize\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-09-the_tollgate_forms-1489.txt")).ReplaceLineEndings("\n"), output);
    }
}
