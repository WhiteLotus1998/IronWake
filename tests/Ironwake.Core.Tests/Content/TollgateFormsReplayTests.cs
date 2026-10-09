using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm hand play of Grit on both sides (issue 1461 a2): the Tollgate under <c>forms: on</c>
/// (<c>docs/samples/the_tollgate_forms.map</c>), seed 1461. Pell's Overcast takes the woods archer on
/// turn 2, Teodor falls to the keep archer's crit on turn 6, the Bandit Leader declares Cleave on Pell
/// on turn 7 (the form `threat` and `end` priced) and misses, and the captain takes the gate on turn 8.
/// </summary>
[Collection("console")]
public class TollgateFormsReplayTests
{
    [Fact]
    public void TheFormsTollgateReplayOnSeed1461IsSeizedOnTurn8WithTheLeadersCleavePriced()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-09-the_tollgate_forms-1461.script");
        var map = Path.Combine(root, "docs", "samples", "the_tollgate_forms.map");
        var args = new[] { "play", map, "--seed", "1461", "--strict", "--script", script, "--content", Fixture.RealContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("  Bandit Leader from 7,2 with Toll Axe (slot 1) as Cleave (2 of its 3 Grit): acc 25% dmg 20 crit 9%;", output);
        Assert.Contains("Lethal if all land: Pell (Bandit Leader for 20, against 17 hp)\n", output);
        Assert.Contains("enemy: attack bandit_leader-1 pell form cleave\nForecast Bandit Leader -> Pell: acc 25% dmg 20 crit 9%;", output);
        Assert.Contains("Bandit Leader declares Cleave with Toll Axe, spending 2 Grit\n", output);
        Assert.Contains("Battle won: seize\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-09-the_tollgate_forms-1461.txt")).ReplaceLineEndings("\n"), output);
    }
}
