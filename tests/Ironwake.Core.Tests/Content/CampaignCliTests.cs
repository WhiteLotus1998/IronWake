namespace Ironwake.Core.Tests.Content;

/// <summary>
/// <c>ironwake campaign</c> at the console (issue 74): the journaled two-map campaign replayed to
/// its transcript, <c>leave</c> refused while the battle is undecided, a script that ends on the
/// screen, the argument refusals, and a certification trial on the screen (issue 252). The
/// scripts play on the content with the class ladder removed, as the journaled campaign was
/// played before the ladder shipped; the shipped ladder's refusal on the screen has its own test.
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

        var output = Run(out var exit, "campaign", "--seed", "139", "--script", script, "--content", Fixture.LadderFreeContentDirectory());

        Assert.Equal(1, exit);
        Assert.Contains("Old Mill Road won: rout; reward 600, the purse holds 1100; nobody fell\n", output);
        Assert.Contains("brannock certifies from Cadet to Reaver for 500; the purse holds 320\n", output);
        Assert.Contains("deploys to Saltmarsh Ford: captain, wren, teodor, pell\n", output);
        Assert.Contains("Saltmarsh Ford won: rout; reward 800, the purse holds 1120; nobody fell\n", output);
        Assert.Contains("campaign stopped before The Tollgate: the script ended on the screen\n", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    [Fact]
    public void TheJournaledLadderReadingReplaysToItsTranscriptOnTheShippedContent()
    {
        var script = Transcript("2026-09-26-campaign-139-ladder.script");

        var output = Run(out _, "campaign", "--seed", "139", "--script", script, "--content", Fixture.RealContentDirectory());

        Assert.Contains("  Outrider: level 4, sword D; or its trial in place of the seal -- needs level 4, has 3\n", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Issue 288's acceptance: a scripted campaign through the raid, one edit bought at the camp
    /// after it, and the finale fought on the edited keep, on content whose campaign is the raid and
    /// the keep alone. The wall bought on the screen stands on the finale's board.
    /// </summary>
    [Fact]
    public void TheJournaledKeepCampaignReplaysToItsTranscript()
    {
        var script = Transcript("2026-09-26-campaign-keep-288.script");

        var output = Run(out var exit, "campaign", "--seed", "288", "--script", script, "--strict", "--content", Fixture.KeepCampaignContentDirectory());

        Assert.Equal(0, exit);
        Assert.Contains("Raid on Ironwake won: rout; reward 1000, the purse holds 1500; nobody fell\n", output);
        Assert.Contains("keep: Ironwake Keep; built: nothing; the purse holds 1500\n", output);
        Assert.Contains("built for 400, the purse holds 1100: wall 10,8: Wall; no unit can stand on it; the gap 10,7 to 10,8 narrows from 2 tiles to 1 (10,7)\n", output);
        Assert.Contains("map 2 of 2: Ironwake Keep, seed 289\nIronwake Keep  turn 1 of 8  player phase  survive  recall 3\n", output);
        Assert.Contains("\n 8 ...e......#.....\n", output);
        Assert.Contains("campaign won: all 2 maps, the purse holds 3100\n", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    [Fact]
    public void TheKeepsMenuIsRefusedOnTheScreenBeforeTheRaid()
    {
        var output = Play(out _, "keep\nbuild wall 10,3\nbuild wall\n");

        Assert.Contains("> keep\nERROR: the keep's menu opens after the raid on it (ironwake_raid, map 5) is fought\n", output);
        Assert.Contains("> build wall 10,3\nERROR: the keep's menu opens after the raid on it (ironwake_raid, map 5) is fought\n", output);
        Assert.Contains("ERROR: usage: build <edit> <x,y>\n", output);
        Assert.DoesNotContain("keep: Ironwake Keep", output);
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

    [Fact]
    public void ATrialOnTheScreenCertifiesOnAPassWithNoSealAndIsRefusedAfterward()
    {
        const string script = "trial captain outrider\nmove captain 3,3\nattack captain hexer-1 2\ncanto captain 3,0\nleave\ntrial captain outrider\ntrial wren pikeman\n";

        var output = Play(out var exit, script, "--seed", "4");

        Assert.Equal(1, exit);
        Assert.Contains("trials in place of a seal (one attempt per unit and class before each map): Bulwark, Outrider\n", output);
        Assert.Contains("> trial captain outrider\ntrial: Trial of the Outrider, seed 12\ncertification trial: captain plays as Outrider with iron_lance, iron_sword\n", output);
        Assert.Contains("battle won: seize; no recall is left, so leave\n", output);
        Assert.Contains("captain passes the Outrider trial and certifies from Cadet to Outrider with no seal; L1 exp 30\n", output);
        Assert.Contains("ERROR: captain cannot certify as Outrider: ", output);
        Assert.Contains("ERROR: Pikeman has no trial; certify with a seal\n", output);
        Assert.Contains("-- before map 1 of 8: Old Mill Road; the purse holds 500 --", output);
    }

    [Fact]
    public void AFailedTrialOnTheScreenOpensAgainOnlyAfterTheNextMap()
    {
        const string script = "trial captain outrider\nmove captain 3,3\nattack captain hexer-1 2\ncanto captain stay\nend\nleave\ntrial captain outrider\n";

        var output = Play(out _, script, "--seed", "5");

        Assert.Contains("trial: Trial of the Outrider, seed 13\n", output);
        Assert.Contains("battle lost: turn 1 passed; no recall is left, so leave\n", output);
        Assert.Contains("captain fails the Outrider trial and stays a Cadet; it opens again after the next map\n", output);
        Assert.Contains("ERROR: captain has tried the Outrider trial since the last map; it opens again after the next one\n", output);
    }

    [Fact]
    public void TheShippedLadderIsOnTheScreenAndRefusesALevelOneRecruitNamingEveryRequirement()
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-campaign-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, "trial captain outrider\ncertify brannock reaver\nclasses captain\nclasses nobody\n");
        try
        {
            var output = Run(out _, "campaign", "--seed", "3", "--script", path, "--content", Fixture.RealContentDirectory());

            Assert.Contains("ERROR: captain cannot certify as Outrider: needs level 4, has 1; needs sword D, has E\n", output);
            Assert.Contains("ERROR: brannock cannot certify as Reaver: needs level 3, has 1; needs axe D, has E\n", output);
            Assert.Contains("classes: what each asks, read against a unit's own stats without its class's; a seal costs 500\n", output);
            Assert.Contains("  Cadet: nothing -- captain's class\n", output);
            Assert.Contains("  Outrider: level 4, sword D; or its trial in place of the seal -- needs level 4, has 1; needs sword D, has E\n", output);
            Assert.Contains("  Bowman: level 3, dex 8 -- needs level 3, has 1; needs dex 8, has 7\n", output);
            Assert.Contains("ERROR: no unit 'nobody' on the roster\n", output);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string Play(out int exit, string script, params string[] extra)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-campaign-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, script);
        try
        {
            return Run(out exit, new[] { "campaign", "--seed", "3", "--script", path, "--content", Fixture.LadderFreeContentDirectory() }.Concat(extra).ToArray());
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
