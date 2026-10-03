using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The camp actions the client reaches without the console (issue 786): a committed script that
/// takes every one of them, played by the console from a saved record and by the presenter from the
/// same record, byte for byte; and the rows the camp screen draws, each one the command it says.
/// </summary>
[Collection("console")]
public class CampActionsTests
{
    private const string Map = "sallow_grange";
    private const ulong Seed = 41;

    private static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    private static string ScriptPath() => Path.Combine(ClientParityTests.Root(), "tests", "parity", "campaign", "camp-actions-41.script");

    /// <summary>A camp before Sallow Grange with the purse, the wagon, the forge's stores, the levels and a worn sword to take every action once.</summary>
    internal static CampaignRecord Stocked()
    {
        var start = CampaignRecord.StartAt(Content, Seed, Map);
        var roster = start.Roster.Select(u => u.Id switch
        {
            "captain" => u with { Level = 3 },
            "teodor" => u with { Level = 4 },
            "wren" => u with { Inventory = u.Inventory.Replace(0, u.Inventory.Items[0] with { Uses = 20 }) },
            _ => u,
        });
        return start with
        {
            Roster = ValueList<Unit>.From(roster),
            Purse = 5000,
            Wagon = ValueList<string>.From(new[] { "iron_sword", "salve" }),
            CommonMaterial = 4,
        };
    }

    /// <summary>The console's output and event log for <paramref name="script"/> from <see cref="Stocked"/>, loaded from a save.</summary>
    internal static (string Output, string Log) Console(string script, bool strict = true)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ironwake-camp-actions-{Guid.NewGuid():N}");
        var log = Path.Combine(dir, "campaign.log");
        var scriptFile = Path.Combine(dir, "camp.script");
        try
        {
            var store = new SaveStore(dir);
            Assert.Null(store.Save("stocked", Stocked()));
            File.WriteAllText(scriptFile, script);
            var args = new List<string> { "--saves", dir, "--load", "stocked", "--script", scriptFile, "--content", Fixture.RealContentDirectory(), "--log", log };
            if (strict)
            {
                args.Add("--strict");
            }

            var output = ConsoleCapture.Run(() => CampaignSession.Run(args.ToArray()));
            return (output, File.Exists(log) ? File.ReadAllText(log) : "");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static CampaignClient Client() => new(Content, Fixture.RealContentDirectory(), Stocked());

    private static string ClientLog() => Script.PlayCampaign(Client(), File.ReadAllText(ScriptPath()));

    [Fact]
    public void EveryCampActionMatchesTheConsoleByteForByte()
    {
        var (output, console) = Console(File.ReadAllText(ScriptPath()));

        Assert.DoesNotContain("ERROR", output);
        foreach (var line in new[]
        {
            "Forge built for 600", "Bunk room built for 400", "Barracks built for 500", "Corin Ashby joins as a Cadet",
            "Refine Iron Sword: Iron Sword +1", "Refine Iron Sword: Iron Sword +2", "Wren's Iron Sword repaired from 20 to 40", "Wren takes Salve from the wagon",
            "Wren drops Salve", "Alder Fenn certifies from Cadet to Vanguard", "Built for 400", "Side map: The Lazar House",
            "Maud falls on maud_1", "Map 7 of 10: Sallow Grange",
        })
        {
            Assert.Contains(line, console);
        }

        Assert.Null(Parity.FirstDifference(console, ClientLog()));
    }

    [Fact]
    public void CampParityFiresOnALogMissingTheRefine()
    {
        var (_, console) = Console(File.ReadAllText(ScriptPath()));
        var client = ClientLog();
        var refine = client.IndexOf("Refine Iron Sword: Iron Sword +1", StringComparison.Ordinal);
        var without = client[..refine] + client[(client.IndexOf('\n', refine) + 1)..];

        Assert.StartsWith("event log differs at line 5", Parity.FirstDifference(console, without));
    }

    [Fact]
    public void EachRowIsTheConsoleCommandItSays()
    {
        var rows = CampActions.For(Client(), "wren", Array.Empty<string>());

        foreach (var row in rows)
        {
            var client = Client();
            var fresh = CampActions.For(client, "wren", Array.Empty<string>()).Single(r => r.Command == row.Command);
            var accepted = fresh.Run();
            var (output, console) = Console(row.Command + "\n", strict: false);

            Assert.Equal(!output.Contains("ERROR", StringComparison.Ordinal), accepted);
            if (client.Battle is null)
            {
                Assert.Null(Parity.FirstDifference(console, client.LogText));
            }
            else
            {
                Assert.StartsWith(client.LogText.Split('\n')[0], console);
            }
        }
    }

    [Fact]
    public void TheSelectedUnitsRowsCoverItsSlotsTheWagonAndTheClassesItMeets()
    {
        var commands = CampActions.For(Client(), "captain", Array.Empty<string>()).Select(r => r.Command).ToList();

        Assert.Contains("repair captain 1", commands);
        Assert.Contains("drop captain 2", commands);
        Assert.Contains("take captain 1", commands);
        Assert.Contains("take captain 2", commands);
        Assert.Contains("certify captain vanguard", commands);
        Assert.DoesNotContain("certify captain adept", commands);
        Assert.Contains("build forge", commands);
        Assert.Contains("build wall 10,3", commands);
        Assert.Contains("quest maud_1 captain", commands);
        Assert.DoesNotContain(commands, c => c.StartsWith("refine", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, c => c.StartsWith("hire", StringComparison.Ordinal));
    }

    [Fact]
    public void RefineAndHireRowsAppearOnceTheirRoomsAreBuilt()
    {
        var client = Client();
        Assert.True(client.BuildRoom("forge"));
        Assert.True(client.BuildRoom("barracks"));

        var commands = CampActions.For(client, "captain", Array.Empty<string>()).Select(r => r.Command).ToList();

        Assert.Contains("refine captain 1 mt", commands);
        Assert.Contains("refine captain 1 hit", commands);
        Assert.DoesNotContain("refine captain 2 mt", commands);
        Assert.Contains("hire corin", commands);
        Assert.DoesNotContain("build forge", commands);
        Assert.Contains("build bunk", commands);
    }

    [Fact]
    public void AQuestRowTakesThePartyWhenOneIsPicked()
    {
        var row = CampActions.For(Client(), "captain", new[] { "wren", "teodor" }).Single(r => r.Command.StartsWith("quest", StringComparison.Ordinal));

        Assert.Equal("quest maud_1 wren teodor", row.Command);
        Assert.Equal("fight Maud's side map maud_1 with Wren, Teodor", row.Text);
    }

    [Fact]
    public void WithNoUnitSelectedOnlyTheKeepsRowsAreOffered()
    {
        var commands = CampActions.For(Client(), null, Array.Empty<string>()).Select(r => r.Command).ToList();

        Assert.All(commands, c => Assert.StartsWith("build", c));
        Assert.NotEmpty(commands);
    }

    [Fact]
    public void ARefusedRowShowsTheConsolesRefusal()
    {
        var client = Client();
        var repair = CampActions.For(client, "captain", Array.Empty<string>()).Single(r => r.Command == "repair captain 1");

        Assert.False(repair.Run());
        Assert.Equal("Iron Sword is at full uses (40)", client.Status);
        Assert.Equal("", client.LogText);
    }

    [Fact]
    public void LeavingASideMapAppliesItsResultAndReturnsToTheCamp()
    {
        var client = Client();
        Assert.True(client.Quest("maud_1", new[] { "wren" }));
        Assert.True(client.InQuest);
        Assert.False(client.Leave());

        Script.PlayCampaign(client, "end\nend\nend\nend\nleave\n");

        Assert.Null(client.Battle);
        Assert.False(client.InQuest);
        Assert.False(client.Over);
        Assert.Contains("maud", client.Record.Fallen);
        Assert.NotNull(client.NextMap);
    }

    [Fact]
    public void ARefusedSideMapShowsTheConsolesRefusalAndOpensNoBattle()
    {
        var client = Client();

        Assert.False(client.Quest("maud_1", new[] { "captain" }));

        var (output, _) = Console("quest maud_1 captain\n", strict: false);
        Assert.Contains("ERROR: " + client.Status, output);
        Assert.Null(client.Battle);
        Assert.Equal("", client.LogText);
    }
}
