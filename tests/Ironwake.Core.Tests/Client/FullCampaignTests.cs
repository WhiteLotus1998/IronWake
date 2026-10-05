using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The full-campaign parity script (issue 786, slice 5): one committed script from map 1 to the
/// keep's win, played by the console from a new campaign and by the presenter from the same start,
/// byte for byte; every camp action the campaign offers it and every order taken once; and the
/// script exactly what <c>ironwake-sim --campaign-script</c> writes, so it is regenerated, never
/// hand-edited.
/// </summary>
[Collection("console")]
public class FullCampaignTests
{
    private const ulong Seed = 644;
    private const string Difficulty = "recruit";
    private const int Variant = 66;

    private static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    private static string ScriptPath() => Path.Combine(ClientParityTests.Root(), "tests", "parity", "campaign", $"full-campaign-{Seed}.script");

    private static CampaignRecord Start() => CampaignRecord.Start(Content, Seed, Difficulty, permadeath: false);

    /// <summary>The console's output and event log for the committed script, as <c>campaign --log</c> writes it.</summary>
    private static (string Output, string Log) Console()
    {
        var log = Path.Combine(Path.GetTempPath(), $"ironwake-full-campaign-{Guid.NewGuid():N}.log");
        try
        {
            var output = ConsoleCapture.Run(() => CampaignSession.Run(new[]
            {
                "--seed", Seed.ToString(), "--difficulty", Difficulty, "--permadeath", "off", "--script", ScriptPath(), "--strict",
                "--content", Fixture.RealContentDirectory(), "--log", log,
            }));
            return (output, File.ReadAllText(log));
        }
        finally
        {
            File.Delete(log);
        }
    }

    private static string ClientLog() => Script.PlayCampaign(new CampaignClient(Content, Fixture.RealContentDirectory(), Start()), File.ReadAllText(ScriptPath()));

    [Fact]
    public void FullCampaignMatchesTheConsoleByteForByteFromMapOneToTheKeep()
    {
        var (output, console) = Console();

        Assert.DoesNotContain("ERROR", output);
        Assert.DoesNotContain("Rejected", output);
        Assert.Contains("Campaign won: all 10 maps", console);
        Assert.Null(Parity.FirstDifference(console, ClientLog()));
    }

    [Fact]
    public void TheCampaignsEndingLinesAreACardNotTheLog()
    {
        var client = new CampaignClient(Content, Fixture.RealContentDirectory(), Start());
        var log = Script.PlayCampaign(client, File.ReadAllText(ScriptPath()));
        var cards = new List<IReadOnlyList<string>>();
        while (client.Card is { } card)
        {
            cards.Add(card);
            client.DismissCard();
        }

        Assert.True(client.Over);
        Assert.DoesNotContain("served at the keep", log);
        Assert.Equal(new[] { "Corin Ashby served at the keep." }, cards[^1]);
    }

    [Fact]
    public void FullCampaignParityFiresOnALogMissingTheOrder()
    {
        var (_, console) = Console();
        var client = ClientLog();
        var order = client.IndexOf("Alder Fenn calls fall back", StringComparison.Ordinal);
        var without = client[..order] + client[(client.IndexOf('\n', order) + 1)..];

        Assert.StartsWith("event log differs at line 1195", Parity.FirstDifference(console, without));
    }

    [Fact]
    public void FullCampaignTakesEveryCampActionAndEveryOrderTheCampaignOffers()
    {
        var script = File.ReadAllLines(ScriptPath());

        foreach (var start in new[]
        {
            "buy ", "drop ", "bench ", "unbench ", "repair ", "certify captain ", "quest ", "pick ", "build forge", "build barracks", "hire ", "build wall ",
            "order press", "order rally", "order fall back", "fallback ", "recall ", "exit ", "meet ",
        })
        {
            Assert.Contains(script, line => line.StartsWith(start, StringComparison.Ordinal));
        }

        Assert.Equal(10, script.Count(line => line == "march"));
    }

    [Fact]
    public void CommittedScriptIsWhatTheSimWrites()
    {
        var written = CampaignScript.Write(Content, Fixture.RealContentDirectory(), Seed, Ironwake.Sim.Program.HandPlays(Fixture.RealContentDirectory()), Difficulty, permadeath: false, Variant);

        Assert.Null(written.LostOn);
        Assert.Equal(File.ReadAllText(ScriptPath()), written.Text);
    }
}
