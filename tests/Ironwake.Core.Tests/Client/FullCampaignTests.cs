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
    /// <summary>The camp variant: 20 lost the field once the Mill's fort went south (issue 1210), 30 map 6 once the planner left a corked captain its no-counter tile (issue 1206); 44 keeps its side map and its spent purse.</summary>
    private const int Variant = 44;
    /// <summary>The committed script's variant: 44 lost the field once the unarmed healer stepped back from exposed tiles (issue 1441), and 6 wins it; the other reads keep <see cref="Variant"/>.</summary>
    private const int ScriptVariant = 6;

    /// <summary>The side map the script takes (issue 1166): at levy floor three only a quest-off variant wins seed 644, so it names one.</summary>
    private const string Quest = "pell_1";

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

        Assert.StartsWith("event log differs at line 516", Parity.FirstDifference(console, without));
    }

    [Fact]
    public void FullCampaignTakesEveryCampActionAndEveryOrderTheCampaignOffers()
    {
        var script = File.ReadAllLines(ScriptPath());

        foreach (var start in new[]
        {
            "buy ", "drop ", "bench ", "unbench ", "repair ", "certify captain ", "quest ", "pick ", "build forge", "build barracks", "hire ", "build wall ",
            "order press", "order rally", "order fall back", "fallback ", "recall ", "exit ", "meet ", "item pell gust ",
        })
        {
            Assert.Contains(script, line => line.StartsWith(start, StringComparison.Ordinal));
        }

        Assert.Equal(10, script.Count(line => line == "march"));
    }

    [Fact]
    public void CommittedScriptIsWhatTheSimWrites()
    {
        var written = CampaignScript.Write(Content, Fixture.RealContentDirectory(), Seed, Ironwake.Sim.Program.HandPlays(Fixture.RealContentDirectory()), Difficulty, permadeath: false, ScriptVariant, Quest);

        Assert.Null(written.LostOn);
        Assert.Equal(File.ReadAllText(ScriptPath()), written.Text);
    }

    [Fact]
    public void ASideMapWithAHandPlayIsFoughtWithItsAlliesAndWonInTheCampaign()
    {
        // Read on Gust (DECISIONS/0348): on Spark Storm the heuristic loses this seed's keep, after the side map.
        var written = CampaignScript.Write(ContentLoader.Load(Fixture.GustContentDirectory()), Fixture.GustContentDirectory(), Seed, Ironwake.Sim.Program.HandPlays(Fixture.RealContentDirectory()), Difficulty, permadeath: false, Variant, Quest + ",rook_1");
        var lines = written.Text.Split('\n');
        var quest = Array.IndexOf(lines, "quest rook_1 wren");

        Assert.Null(written.LostOn);
        Assert.True(quest >= 0);
        Assert.Equal("move rook 12,1", lines[Array.IndexOf(lines, "leave", quest) - 1]);
    }

    [Fact]
    public void TheLazarHouseAndShrineHandPlaysPayThePsalterInsideTheCampaign()
    {
        // With casts since the seize step (issue 1409): the Shrine's hand play runs out a turn early and the heuristic takes the altar.
        var written = CampaignScript.Write(Content, Fixture.RealContentDirectory(), Seed, Ironwake.Sim.Program.HandPlays(Fixture.RealContentDirectory()), Difficulty, permadeath: false, Variant, Quest + ",maud_1,maud_2");
        var lines = written.Text.Split('\n');
        var client = new CampaignClient(Content, Fixture.RealContentDirectory(), Start());
        Script.PlayCampaign(client, written.Text);

        Assert.Equal(1, lines.Count(l => l == "quest maud_1 wren"));
        Assert.Equal(1, lines.Count(l => l == "quest maud_2 wren"));
        Assert.Contains(client.Record.Find("maud")!.Inventory.Items, stack => stack.ItemId == "maud_psalter");
    }

    private static string ArtScriptPath() => Path.Combine(ClientParityTests.Root(), "tests", "parity", "campaign", $"psalter-art-{Seed}.script");

    private const string ArtQuests = Quest + ",maud_1,maud_2";

    // Written by the casting heuristic since the seize step (issue 1409); before it, the Shrine was lost and the art never reached.
    private static CampaignScript.Result WriteArt(IReadOnlyCollection<string>? deploy) =>
        CampaignScript.Write(Content, Fixture.RealContentDirectory(), Seed, Ironwake.Sim.Program.HandPlays(Fixture.RealContentDirectory()), Difficulty, permadeath: false, Variant, ArtQuests, deploy: deploy, stopAfter: new[] { "art" });

    [Fact]
    public void ThePsalterArtScriptMatchesTheConsoleByteForByteThroughTheClickPath()
    {
        var log = Path.Combine(Path.GetTempPath(), $"ironwake-psalter-art-{Guid.NewGuid():N}.log");
        try
        {
            var output = ConsoleCapture.Run(() => CampaignSession.Run(new[]
            {
                "--seed", Seed.ToString(), "--difficulty", Difficulty, "--permadeath", "off", "--script", ArtScriptPath(), "--strict",
                "--content", Fixture.RealContentDirectory(), "--log", log,
            }));
            var console = File.ReadAllText(log);
            var client = Script.PlayCampaign(new CampaignClient(Content, Fixture.RealContentDirectory(), Start()), File.ReadAllText(ArtScriptPath()));

            Assert.DoesNotContain("ERROR", output);
            Assert.DoesNotContain("Rejected", output);
            Assert.Contains("Maud declares Unasked with Maud's Psalter", console);
            Assert.Null(Parity.FirstDifference(console, client));
        }
        finally
        {
            File.Delete(log);
        }
    }

    [Fact]
    public void CommittedPsalterArtScriptIsWhatTheSimWrites()
    {
        var written = WriteArt(new[] { "maud" });

        Assert.Null(written.LostOn);
        Assert.Contains("art", written.Touched);
        Assert.Contains("\nitem maud maud_psalter teodor form unasked\n", written.Text);
        Assert.EndsWith("# stopped at the camp before sallow_grange: art taken\n", written.Text);
        Assert.Equal(File.ReadAllText(ArtScriptPath()), written.Text);
    }

    [Fact]
    public void DeploySeatsTheArtBearerOnlyAfterTheNamedQuestsByBenchingFromTheBack()
    {
        var seatedRun = WriteArt(new[] { "maud" });
        var seated = seatedRun.Text.Split('\n');
        var unseated = WriteArt(null);

        // Since the seize step (issue 1409) the unseated company reaches the art too, three maps later.
        Assert.True(!unseated.Touched.Contains("art") || unseated.Maps > seatedRun.Maps);
        Assert.Equal("bench dunstan", seated[Array.IndexOf(seated, "pick rook") - 1]);
        Assert.True(Array.IndexOf(seated, "bench dunstan") > Array.IndexOf(seated, "quest maud_2 wren"));
    }

    [Fact]
    public void ASideMapsHandPlayIsFoughtOnlyWhenTheQuestIsNamed()
    {
        var written = CampaignScript.Write(Content, Fixture.RealContentDirectory(), Seed, Ironwake.Sim.Program.HandPlays(Fixture.RealContentDirectory()), Difficulty, permadeath: false, Variant, Quest);

        Assert.Contains("quest maud_1 brannock", written.Text.Split('\n'));
        Assert.DoesNotContain("quest maud_1 wren", written.Text.Split('\n'));
    }
}
