namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Canto at the console (issue 71): the <c>canto</c> command, its <c>stay</c> form, the
/// line that says a Canto is owed, and the journaled hit-and-run on
/// <c>docs/samples/canto_raid.map</c>, replayed under <c>--strict</c> to its transcript.
/// </summary>
[Collection("console")]
public class CantoCliTests
{
    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static string RaidMap => Path.Combine(Repo, "docs", "samples", "canto_raid.map");

    private static string Play(out int exit, string script)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-canto-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, script);
        try
        {
            return Run(out exit, "play", RaidMap, "--seed", "3", "--script", path, "--content", Fixture.RealContentDirectory());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AnActionByACantoUnitSaysWhatItsCantoHasLeftAndTheRosterRowShowsIt()
    {
        var output = Play(out _, "move ansgar 4,4\nwait ansgar\n");

        Assert.Contains("ansgar waits\nansgar may canto up to 2 movement: canto ansgar <x,y|stay>\n", output);
        Assert.Contains("Plain  canto 2\n", output);
    }

    [Fact]
    public void CantoStayIsTheCantoToTheUnitsOwnTileAndThenTheUnitIsDone()
    {
        var output = Play(out _, "wait ansgar\ncanto ansgar stay\ncanto ansgar 2,3\n");

        Assert.Contains("> canto ansgar stay\nansgar stays at 1,3 (canto)\n", output);
        Assert.Contains("> canto ansgar 2,3\nERROR: ansgar cannot Canto: its Canto is spent this phase\n", output);
    }

    [Fact]
    public void ACantoCommandIsRefusedWithItsUsage()
    {
        var output = Play(out _, "canto ansgar\ncanto captain 1,1\n");

        Assert.Contains("> canto ansgar\nERROR: usage: canto <unit> <x,y|stay>\n", output);
        Assert.Contains("ERROR: captain cannot Canto: it has no Canto\n", output);
    }

    [Fact]
    public void TheJournaledRaidWinsWithAHitAndRunAndReplaysToItsTranscript()
    {
        var script = Path.Combine(Repo, "docs", "transcripts", "2026-09-25-canto_raid-3.script");

        var output = Run(out var exit, "play", RaidMap, "--seed", "3", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.Contains("ansgar hits archer-1 for 11 (hp 6)", output);
        Assert.Contains("> canto ansgar 4,5\nansgar cantos 4,4 -> 4,5\n", output);
        Assert.Contains("> canto ansgar 5,5\nansgar cantos 8,5 -> 5,5 via 7,5 6,5\n", output);
        Assert.Contains("threat on ansgar at 5,5 (Plain): no enemy can strike it next phase", output);
        Assert.EndsWith("battle won: rout\n", output);
        Assert.DoesNotContain("rejected ", output);
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
