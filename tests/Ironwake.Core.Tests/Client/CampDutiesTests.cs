using Ironwake.Client;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The duty and yard rows the client reaches without the console (issue 1331): a committed script
/// that takes a forge duty, a rest and a yard drill, played by the console and by the presenter from
/// the same record, byte for byte; and the rows the camp screen draws for them.
/// </summary>
[Collection("console")]
public class CampDutiesTests
{
    private static string ScriptPath() => Path.Combine(ClientParityTests.Root(), "tests", "parity", "campaign", "camp-duties-41.script");

    private static CampaignClient Client() => new(CampActionsTests.Content, Fixture.RealContentDirectory(), CampActionsTests.Stocked());

    private static List<string> Commands(CampaignClient client, string unitId) =>
        CampActions.For(client, unitId, Array.Empty<string>()).Select(r => r.Command).ToList();

    [Fact]
    public void TheDutyAndYardRowsMatchTheConsoleByteForByte()
    {
        var (output, console) = CampActionsTests.Console(File.ReadAllText(ScriptPath()));

        Assert.DoesNotContain("ERROR", output);
        foreach (var line in new[]
        {
            "Teodor works the forge: Iron Lance: Iron Lance +1", "Maud takes the rest duty at this camp",
            "Yard: The Yard (placeholder), seed 191", "Wren drills under Alder Fenn in the sword.",
            "Wren trained under Alder Fenn; the drill is lost", "Map 7 of 10: Sallow Grange",
        })
        {
            Assert.Contains(line, console);
        }

        Assert.Null(Parity.FirstDifference(console, Script.PlayCampaign(Client(), File.ReadAllText(ScriptPath()))));
    }

    [Fact]
    public void DutyParityFiresOnALogMissingTheForgeDuty()
    {
        var (_, console) = CampActionsTests.Console(File.ReadAllText(ScriptPath()));
        var client = Script.PlayCampaign(Client(), File.ReadAllText(ScriptPath()));
        var forge = client.IndexOf("Teodor works the forge", StringComparison.Ordinal);
        var without = client[..forge] + client[(client.IndexOf('\n', forge) + 1)..];

        Assert.StartsWith("event log differs at line 2", Parity.FirstDifference(console, without));
    }

    [Fact]
    public void AUnitWithNoDutyIsOfferedRestAndTheYardsItCouldDrill()
    {
        var commands = Commands(Client(), "wren");

        Assert.Contains("duty wren rest", commands);
        Assert.Contains("yard captain wren sword", commands);
        Assert.DoesNotContain(commands, c => c.StartsWith("duty wren forge", StringComparison.Ordinal));
    }

    [Fact]
    public void ForgeDutyRowsAppearOnceTheForgeIsBuilt()
    {
        var client = Client();
        Assert.True(client.BuildRoom("forge"));

        var rows = CampActions.For(client, "teodor", Array.Empty<string>());

        Assert.Contains(rows, r => r.Command == "duty teodor forge 1 mt" && r.Text == "work the forge, slot 1: Iron Lance, Mt (no gold)");
        Assert.Contains(rows, r => r.Command == "duty teodor forge 1 hit");
    }

    [Fact]
    public void AUnitThatTookADutyIsOfferedNoOther()
    {
        var client = Client();
        Assert.True(client.Duty("wren", "rest"));

        var commands = Commands(client, "wren");

        Assert.DoesNotContain(commands, c => c.StartsWith("duty ", StringComparison.Ordinal) || c.StartsWith("yard ", StringComparison.Ordinal));
        Assert.DoesNotContain("yard wren captain sword", Commands(client, "captain"));
    }

    [Fact]
    public void LeavingTheYardAppliesItsResultAndReturnsToTheCamp()
    {
        var client = Client();
        Assert.True(client.Yard("captain", "wren", "sword"));
        Assert.True(client.InYard);
        Assert.False(client.Leave());

        Script.PlayCampaign(client, "end !\nmove captain 3,5\nattack captain soldier-1 1\nattack wren soldier-1 1\nend !\nleave\n");

        Assert.Null(client.Battle);
        Assert.False(client.InYard);
        Assert.Contains("wren", client.Record.Fallen);
        Assert.Equal(Duty.Yard, client.Record.DutyOf("captain"));
        Assert.NotNull(client.NextMap);
    }

    [Fact]
    public void ARefusedYardShowsTheConsolesRefusalAndOpensNoBattle()
    {
        var client = Client();

        Assert.False(client.Yard("wren", "wren", "sword"));

        var (output, _) = CampActionsTests.Console("yard wren wren sword\n", strict: false);
        Assert.Contains("ERROR: " + client.Status, output);
        Assert.Null(client.Battle);
        Assert.Equal("", client.LogText);
    }
}
