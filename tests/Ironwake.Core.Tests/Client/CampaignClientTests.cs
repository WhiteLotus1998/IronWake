using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The thin renderer's campaign presenter (issue 360): its event log against
/// <c>ironwake campaign --log</c> for the same committed script, byte for byte, across a purchase
/// and a map change; the gate firing on a log one line away; the presenter refusing with the
/// core's text; and a campaign opened on any map, as the beta opens on the first tuned one.
/// </summary>
[Collection("console")]
public class CampaignClientTests
{
    private const string Map = "the_tollgate";
    private const ulong Seed = 113;

    private static string ScriptPath() => Path.Combine(ClientParityTests.Root(), "tests", "parity", "campaign", $"{Map}-{Seed}.script");

    private static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    private static CampaignClient Client() => new(Content, Fixture.RealContentDirectory(), CampaignRecord.StartAt(Content, Seed, Map));

    /// <summary>The console's campaign event log for the committed script, as <c>campaign --log</c> writes it.</summary>
    private static string ConsoleLog()
    {
        var log = Path.Combine(Path.GetTempPath(), $"ironwake-campaign-parity-{Guid.NewGuid():N}.log");
        try
        {
            ConsoleCapture.Run(() => CampaignSession.Run(new[]
            {
                "--from", Map, "--seed", Seed.ToString(), "--script", ScriptPath(), "--strict",
                "--content", Fixture.RealContentDirectory(), "--log", log,
            }));
            return File.ReadAllText(log);
        }
        finally
        {
            File.Delete(log);
        }
    }

    private static string ClientLog() => Script.PlayCampaign(Client(), File.ReadAllText(ScriptPath()));

    [Fact]
    public void CampaignEventLogMatchesTheConsoleByteForByteAcrossAPurchaseAndAMapChange()
    {
        var console = ConsoleLog();
        var client = ClientLog();

        Assert.StartsWith("Alder Fenn buys Iron Sword for 400; the purse holds 100\nMap 4 of 10: The Tollgate, seed 113\n", console);
        Assert.Contains("The Tollgate won: seize; reward 1000, the purse holds 1100; fallen: Teodor\nMap 5 of 10: Harrow Weir, seed 114\n", console);
        Assert.Contains("-- Enemy phase, turn 1 --", console[console.IndexOf("Harrow Weir", StringComparison.Ordinal)..]);
        Assert.Null(Parity.FirstDifference(console, client));
    }

    [Fact]
    public void CampaignParityFiresOnALogMissingThePurchase()
    {
        var console = ConsoleLog();
        var client = ClientLog();
        var withoutPurchase = client[(client.IndexOf('\n') + 1)..];

        Assert.StartsWith("event log differs at line 1, column 1: console 'Alder Fenn buys Iron Sword", Parity.FirstDifference(console, withoutPurchase));
    }

    [Fact]
    public void CampaignLogLeavesOutListingsRefusalsAndBoards()
    {
        var console = ConsoleLog();

        Assert.DoesNotContain("Roster:", console);
        Assert.DoesNotContain("shop:", console);
        Assert.DoesNotContain("deploys to", console);
        Assert.DoesNotContain(console.Split('\n'), line => line.StartsWith("> ", StringComparison.Ordinal));
        Assert.DoesNotContain("ERROR", console);
    }

    [Fact]
    public void ScreenLinesAreTheConsolesScreen()
    {
        var script = Path.Combine(Path.GetTempPath(), $"ironwake-screen-{Guid.NewGuid():N}.script");
        File.WriteAllText(script, "help\n");
        string output;
        try
        {
            output = ConsoleCapture.Run(() => CampaignSession.Run(new[] { "--from", Map, "--seed", "1", "--script", script, "--content", Fixture.RealContentDirectory() }));
        }
        finally
        {
            File.Delete(script);
        }

        var lines = Client().ScreenLines();

        Assert.Equal("-- Before map 4 of 10: The Tollgate; the purse holds 500 --", lines[0]);
        Assert.Contains(string.Join("\n", lines) + "\n", output);
    }

    [Fact]
    public void RefusedPurchaseShowsTheCoresRefusalAndLogsNothing()
    {
        var client = Client();

        Assert.False(client.Buy("steel_sword", "captain"));
        var refusal = CampaignRecord.StartAt(Content, Seed, Map).Buy("steel_sword", "captain", Content);

        Assert.False(refusal.Accepted);
        Assert.Equal(CampaignSession.Text(refusal.Record, Content, refusal.Text), client.Status);
        Assert.Equal("", client.LogText);
        Assert.Equal(500, client.Record.Purse);
    }

    [Fact]
    public void ScreenActionsAreRefusedWhileABattleIsOpen()
    {
        var client = Client();
        Assert.True(client.March());

        Assert.False(client.Buy("field_dressing", "captain"));
        Assert.Equal("A battle is open; leave it once it is decided", client.Status);
        Assert.False(client.March());
    }

    [Fact]
    public void LeaveIsRefusedBeforeTheBattleIsDecided()
    {
        var client = Client();
        client.March();

        Assert.False(client.Leave());
        Assert.Equal("The battle is not decided; leave comes after it is won or lost", client.Status);
        Assert.NotNull(client.Battle);
    }

    [Fact]
    public void BenchingANamedRecruitIsRefusedWithTheCoresText()
    {
        var client = Client();

        Assert.False(client.Bench("teodor"));

        Assert.Equal("The Tollgate places Teodor by name at 7,11", client.Status);
        Assert.Contains("Teodor", client.ScreenLines().Single(l => l.StartsWith("Deploys to", StringComparison.Ordinal)));
    }

    [Fact]
    public void FromAMapTheConsoleNamesNoRefusesIt()
    {
        var output = ConsoleCapture.Run(() => Assert.Equal(2, CampaignSession.Run(new[] { "--from", "nowhere", "--content", Fixture.RealContentDirectory() })));

        Assert.Contains("ERROR: the campaign has no map 'nowhere'", output);
    }
}
