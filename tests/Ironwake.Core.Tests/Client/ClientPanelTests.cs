using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The thin renderer, slice 2 (issue 353): the threat panel is the console's <c>threat</c>
/// text for the selected unit on the hovered tile, and the Recall browser is the console's
/// <c>recall list</c>, row for row, with a click on a row recalling that state.
/// </summary>
[Collection("console")]
public class ClientPanelTests
{
    private static readonly string Map = Path.Combine(Fixture.RealContentDirectory(), "maps", "sallow_grange.map");

    /// <summary>The Sallow seed 61 parity script up to its first Recall and two commands past it, with a charge left.</summary>
    private static string[] Prefix()
    {
        var lines = File.ReadAllLines(ClientParityTests.ScriptPath("sallow_grange-61"));
        var recall = Array.FindIndex(lines, l => l.StartsWith("recall ", StringComparison.Ordinal));
        return lines.Take(recall + 3).ToArray();
    }

    private static ClientSession Sallow(IEnumerable<string> script)
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var client = new ClientSession(content, BattleState.From(MapFiles.Load(Map, content), content, content.Cast, 61));
        Script.Apply(client, string.Join("\n", script));
        return client;
    }

    [Fact]
    public void ThreatPanelIsTheConsolesThreatTextForTheHoveredTile()
    {
        var client = Sallow(Array.Empty<string>());
        var captain = client.State.Find("captain")!;
        var tile = new Coord(3, 4);
        client.Select(captain.At);

        var lines = Queries.Threats(client.State, client.Content, captain, tile)!;
        var expected = PlaySession.ThreatText(client.State, client.Content, captain, tile, lines,
            Queries.SleepingThreats(client.State, client.Content, captain, tile)!, Queries.Unseeing(client.State, client.Content, captain, tile), Queries.MoveWins(client.State, client.Content, captain, tile));

        Assert.Equal(expected, client.Threat(tile));
        Assert.StartsWith("threat on captain at 3,4", client.Threat(tile));
    }

    [Fact]
    public void ThreatPanelIsEmptyFromATileOutOfReachOrWithNoSelection()
    {
        var client = Sallow(Array.Empty<string>());
        Assert.Null(client.Threat(new Coord(3, 4)));

        client.Select(client.State.Find("captain")!.At);
        Assert.Null(client.Threat(new Coord(16, 6)));
    }

    [Fact]
    public void RecallBrowserRowsAreTheConsolesRecallList()
    {
        var script = Prefix();
        var path = WriteScript(script.Append("recall list"));
        string console;
        try
        {
            console = ConsoleCapture.Run(() => PlaySession.Run(new[] { Map, "--seed", "61", "--script", path, "--strict", "--content", Fixture.RealContentDirectory() }));
        }
        finally
        {
            File.Delete(path);
        }

        var printed = console.Split('\n');
        var start = Array.FindLastIndex(printed, l => l.StartsWith("recall: ", StringComparison.Ordinal));
        var rows = Sallow(script).RecallRows;

        Assert.Contains(rows, r => r.Text.Contains("after move", StringComparison.Ordinal));
        Assert.Equal(printed.Skip(start).Take(rows.Count), rows.Select(r => r.Text));
        Assert.True(printed.Length == start + rows.Count || !printed[start + rows.Count].StartsWith("  state ", StringComparison.Ordinal));
    }

    [Fact]
    public void ClickingARecallRowRewindsToItsStateAndSaysWhatItGaveBack()
    {
        var client = Sallow(Prefix());
        var row = client.RecallRows.Last(r => r.State is not null);
        var charges = client.State.RecallCharges;

        Assert.True(client.Recall(row.State!.Value));
        Assert.Equal(charges - 1, client.State.RecallCharges);
        Assert.Equal($"Recalled to state {row.State}; {charges - 1} charges left", client.Log[^1]);
        Assert.StartsWith("undone: ", client.Status);
        Assert.EndsWith(PlaySession.SameRolls, client.Status);
    }

    [Fact]
    public void ARecallToAStateThatDoesNotExistIsRefused()
    {
        var client = Sallow(Prefix());
        var logged = client.Log.Count;

        Assert.False(client.Recall(9999));
        Assert.StartsWith("history holds ", client.Status);
        Assert.Equal(logged, client.Log.Count);
    }

    private static string WriteScript(IEnumerable<string> lines)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ironwake-recall-{Guid.NewGuid():N}.script");
        File.WriteAllLines(path, lines);
        return path;
    }
}
