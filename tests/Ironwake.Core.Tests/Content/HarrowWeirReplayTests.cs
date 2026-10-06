using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm replay of Harrow Weir under DECISIONS/0237 (seed 1360): a fight with the
/// south brigand at 9,7 wakes the ford and, through the link, the weir; the woken Foreman,
/// with no strike in reach, walks off his hill down the east bank, strikes Teodor from
/// 12,10 on a tile the plain-weapon veto passes, and falls there on turn 9 to Dunstan,
/// Teodor's Long Thrust and the captain's Full Measure, after two Recalls on turn 6. History since
/// issue 1138: the walk off the hill on turn 7 was a step with no strike; now he holds his hill,
/// and the line's turn-9 strikes find nothing.
/// The second replay (seed 1410) takes the whole party over the north crossing: the stop on
/// 12,2 wakes the weir, the Foreman refuses every strike tile and holds his hill, and he falls
/// there on turn 6 to Pell's Cinder, Ottilie's Aimed Shot and Full Measure, having never swung
/// on his own phase.
/// The third replay (seed 1450) crosses by the south ford: the ford's fight wakes the weir by
/// its link, the party masses on the east bank's south plain, the Foreman refuses every strike
/// tile from that side too and holds his hill, and he falls there on turn 8 to Full Measure and
/// Ottilie's critical shot, having never moved or swung.
/// </summary>
[Collection("console")]
public class HarrowWeirReplayTests
{
    [Fact]
    public void HarrowWeirReplayOnSeed1360NowFindsTheForemanOnHisHill()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-05-harrow_weir-1360.script");
        var args = new[] { "play", "harrow_weir", "--seed", "1360", "--scheme", "two", "--script", script, "--content", Fixture.RealContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("The weir group wakes (noise)\n", output);
        Assert.Contains("Weir Foreman waits\n", EnemyPhase(output, 7));
        Assert.DoesNotContain("Weir Foreman moves", output);
        Assert.DoesNotContain("Battle won", output);
    }

    [Fact]
    public void HarrowWeirReplayOnSeed1410IsWonOnTheHillOnTurn6()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-05-harrow_weir-1410.script");
        var args = new[] { "play", "harrow_weir", "--seed", "1410", "--scheme", "two", "--strict", "--script", script, "--content", Fixture.RealContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("The weir group wakes (proximity)\n", output);
        Assert.DoesNotContain("enemy: move weir_foreman-1", output);
        Assert.DoesNotContain("enemy: attack weir_foreman-1", output);
        Assert.Contains("Weir Foreman falls at 13,6\n", output);
        Assert.Contains("Battle won: defeat_boss\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-05-harrow_weir-1410.txt")).ReplaceLineEndings("\n"), output);
    }

    [Fact]
    public void HarrowWeirReplayOnSeed1450FromTheFordIsWonOnTheHillOnTurn8()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-06-harrow_weir-1450.script");
        var args = new[] { "play", "harrow_weir", "--seed", "1450", "--scheme", "two", "--strict", "--script", script, "--content", Fixture.RealContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("The weir group wakes (called by the ford group)\n", output);
        Assert.Contains("Reinforcements are blocked: a unit holds 9,11\n", output);
        Assert.DoesNotContain("enemy: move weir_foreman-1", output);
        Assert.DoesNotContain("enemy: attack weir_foreman-1", output);
        Assert.Contains("Weir Foreman falls at 13,6\n", output);
        Assert.Contains("Battle won: defeat_boss\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-06-harrow_weir-1450.txt")).ReplaceLineEndings("\n"), output);
    }

    private static string EnemyPhase(string output, int turn)
    {
        var start = output.IndexOf($"-- Enemy phase, turn {turn} --", StringComparison.Ordinal);
        return output[start..output.IndexOf($"-- Enemy phase ends, turn {turn} --", start, StringComparison.Ordinal)];
    }
}
