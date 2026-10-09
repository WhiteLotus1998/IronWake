namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Move Again at the console (issue 71): the <c>again</c> command (and the retired <c>canto</c>, still read), its <c>stay</c> form, the
/// line that says a Move Again is owed, and the journaled hit-and-run on
/// <c>docs/samples/canto_raid.map</c>, replayed under <c>--strict</c> to its transcript.
/// </summary>
[Collection("console")]
public class MoveAgainCliTests
{
    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static string RaidMap => Path.Combine(Repo, "docs", "samples", "canto_raid.map");

    private static string Play(out int exit, string script)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-moveAgain-" + Guid.NewGuid().ToString("N") + ".script");
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
    public void AnActionByAMoveAgainUnitSaysWhatItsMoveAgainHasLeftAndTheRosterRowShowsIt()
    {
        var output = Play(out _, "move ansgar 4,4\nwait ansgar\n");

        Assert.Contains("Ansgar waits\nAnsgar may move again up to 2 movement: again ansgar <x,y|stay>\n", output);
        Assert.Contains("Plain  again 2\n", output);
    }

    [Fact]
    public void MoveAgainStayIsTheMoveAgainToTheUnitsOwnTileAndThenTheUnitIsDone()
    {
        var output = Play(out _, "wait ansgar\nagain ansgar stay\nagain ansgar 2,3\n");

        Assert.Contains("> again ansgar stay\nAnsgar stays at 1,3 (move again)\n", output);
        Assert.Contains("> again ansgar 2,3\nERROR: Ansgar cannot move again: his Move Again is spent this phase\n", output);
    }

    [Fact]
    public void AMoveAgainCommandIsRefusedWithItsUsage()
    {
        var output = Play(out _, "again ansgar\nagain captain 1,1\n");

        Assert.Contains("> again ansgar\nERROR: Usage: again <unit> <x,y|stay>\n", output);
        Assert.Contains("ERROR: Alder Fenn cannot move again: he has no Move Again\n", output);
    }

    [Fact]
    public void TheJournaledRaidWinsWithAHitAndRunAndReplaysToItsTranscript()
    {
        var script = Path.Combine(Repo, "docs", "transcripts", "2026-09-25-canto_raid-3.script");

        var output = Run(out var exit, "play", RaidMap, "--seed", "3", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.Contains("Ansgar hits Archer for 11 (hp 6)", output);
        // The script predates issue 1446 and spells the command `canto`, which is still read.
        Assert.Contains("> canto ansgar 4,5\nAnsgar moves again 4,4 -> 4,5\n", output);
        Assert.Contains("> canto ansgar 5,5\nAnsgar moves again 8,5 -> 5,5 via 7,5 6,5\n", output);
        Assert.Contains("Threat on Ansgar at 5,5 (Plain): no enemy can strike him next phase", output);
        Assert.EndsWith("Battle won: rout\n", output);
        Assert.DoesNotContain("Rejected ", output);
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
