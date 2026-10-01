namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The Mill, the campaign's second map (issue 632, DESIGN section 14): Maud holds the miller's
/// house alone and is protected, so leaving her to it loses the map, and Code's journaled play
/// replays to its transcript.
/// </summary>
[Collection("console")]
public class TheMillTests
{
    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static string Transcript(string name) => Path.Combine(Repo, "docs", "transcripts", name);

    /// <summary>The captain never comes: on seed 2 the road pair reaches the fort and Maud falls, which loses the map.</summary>
    [Fact]
    public void LeavingMaudToHoldTheFortAloneLosesTheMap()
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-mill-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, string.Concat(Enumerable.Repeat("wait captain\nwait maud\nend\n", 4)));
        try
        {
            var output = Run(out var exit, "play", "the_mill", "--seed", "2", "--script", path, "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Maud falls at 8,5\n", output);
            Assert.Contains("Lost because Maud fell. This map is lost if Maud falls or is left behind.\n", output);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Code's journaled play (seed 632): Maud leaves the fort on turn 2 to finish the road archer
    /// the captain opened, so only the brigand reaches her; the road pair is dead by turn 3 with no
    /// combat near the mill, and the mill pair falls on turns 5 and 6 with no Recall spent.
    /// </summary>
    [Fact]
    public void TheJournaledPlayWinsOnTurnSixWithNoRecall()
    {
        var script = Transcript("2026-10-01-the_mill-632.script");

        var output = Run(out var exit, "play", "the_mill", "--seed", "632", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.Contains("Brigand hits Maud for 13 (hp 4)\n", output);
        Assert.Contains("Archer 1 falls at 9,1\n", output);
        Assert.EndsWith("Battle won: rout\n", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    private static string Run(out int exit, params string[] args)
    {
        var code = 0;
        var output = ConsoleCapture.Run(() => code = Ironwake.Cli.Program.Main(args));
        exit = code;
        return output;
    }
}
