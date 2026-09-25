namespace Ironwake.Core.Tests.Content;

/// <summary>
/// <c>ironwake campaign</c> at the console (issue 74): the journaled two-map campaign replayed to
/// its transcript, <c>leave</c> refused while the battle is undecided, a script that ends on the
/// screen, and the argument refusals.
/// </summary>
[Collection("console")]
public class CampaignCliTests
{
    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static string Transcript(string name) => Path.Combine(Repo, "docs", "transcripts", name);

    [Fact]
    public void TheJournaledTwoMapCampaignReplaysToItsTranscript()
    {
        var script = Transcript("2026-09-25-campaign-139.script");

        var output = Run(out var exit, "campaign", "--seed", "139", "--script", script, "--content", Fixture.RealContentDirectory());

        Assert.Equal(1, exit);
        Assert.Contains("Old Mill Road won: rout; reward 600, the purse holds 1100; nobody fell\n", output);
        Assert.Contains("brannock certifies from Cadet to Reaver for 500; the purse holds 320\n", output);
        Assert.Contains("deploys to Saltmarsh Ford: captain, wren, teodor, pell\n", output);
        Assert.Contains("Saltmarsh Ford won: rout; reward 800, the purse holds 1120; nobody fell\n", output);
        Assert.Contains("campaign stopped before The Tollgate: the script ended on the screen\n", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    [Fact]
    public void LeaveIsRefusedWhileTheBattleIsUndecided()
    {
        var output = Play(out var exit, "march\nleave\n");

        Assert.Equal(1, exit);
        Assert.Contains("ERROR: the battle is not decided; leave comes after it is won or lost\n", output);
        Assert.Contains("campaign stopped in Old Mill Road at turn 1, undecided\n", output);
    }

    [Fact]
    public void AnUnknownScreenCommandIsRejectedAndSummarised()
    {
        var output = Play(out _, "attack wren brigand-1\nbuy iron_sword\n");

        Assert.Contains("ERROR: unknown command 'attack' between maps; type help\n", output);
        Assert.Contains("ERROR: usage: buy <item> <unit>\n", output);
        Assert.EndsWith("rejected 2 of 2 commands:\n  line 1: attack wren brigand-1: unknown command 'attack' between maps; type help\n  line 2: buy iron_sword: usage: buy <item> <unit>\n", output);
    }

    [Fact]
    public void StrictStopsAtTheFirstRejectedScreenLine()
    {
        var output = Play(out var exit, "bench wren\nmarch\n", "--strict");

        Assert.Equal(3, exit);
        Assert.Contains("strict: stopped at line 1 (bench wren); no later command applied\n", output);
        Assert.DoesNotContain("> march", output);
    }

    [Fact]
    public void AnUnknownDifficultyAndAMissingCampaignAreRefused()
    {
        var difficulty = Run(out var difficultyExit, "campaign", "--difficulty", "brutal", "--content", Fixture.RealContentDirectory());
        var noCampaign = Run(out var noCampaignExit, "campaign", "--content", Path.GetTempPath());

        Assert.Equal(2, difficultyExit);
        Assert.Contains("ERROR: no difficulty 'brutal'; the content declares normal", difficulty);
        Assert.NotEqual(0, noCampaignExit);
    }

    private static string Play(out int exit, string script, params string[] extra)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-campaign-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, script);
        try
        {
            return Run(out exit, new[] { "campaign", "--seed", "3", "--script", path, "--content", Fixture.RealContentDirectory() }.Concat(extra).ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string Run(out int exit, params string[] args)
    {
        var code = 0;
        var output = ConsoleCapture.Run(() => code = Ironwake.Cli.Program.Main(args));
        exit = code;
        return output;
    }
}
