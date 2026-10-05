using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The thin renderer, slice 2 (issue 353): the Recall browser is the console's
/// <c>recall list</c>, row for row, with a click on a row recalling that state. The threat
/// panel this slice also built is cut from the client (issue 625); the console's <c>threat</c> stays.
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
        var start = Array.FindLastIndex(printed, l => l.StartsWith("Recall: ", StringComparison.Ordinal));
        var rows = Sallow(script).RecallRows;

        Assert.Contains(rows, r => r.Text.Contains("after move", StringComparison.Ordinal));
        Assert.Equal(printed.Skip(start).Take(rows.Count), rows.Select(r => r.Text));
        Assert.True(printed.Length == start + rows.Count || !printed[start + rows.Count].StartsWith("  State ", StringComparison.Ordinal));
    }

    [Fact]
    public void ClickingARecallRowRewindsToItsStateAndSaysWhatItGaveBack()
    {
        var client = Sallow(Prefix());
        var row = client.RecallRows.Last(r => r.State is not null);
        var charges = client.State.RecallCharges;

        Assert.True(client.Recall(row.State!.Value));
        Assert.Equal(charges - 1, client.State.RecallCharges);
        Assert.Equal($"Recalled to state {row.State}; {PlaySession.ChargesLeft(charges - 1)}", client.Log[^1]);
        Assert.StartsWith("Undone: ", client.Status);
        Assert.EndsWith(PlaySession.SameRolls, client.Status);
    }

    /// <summary>Issue 629: once a rewind is chosen, the browser closes itself.</summary>
    [Fact]
    public void ARecallClosesTheRecallBrowser()
    {
        var client = Sallow(Prefix());
        client.RecallOpen = true;

        Assert.True(client.Recall(client.RecallRows.Last(r => r.State is not null).State!.Value));
        Assert.False(client.RecallOpen);
    }

    [Fact]
    public void ARefusedRecallLeavesTheRecallBrowserOpen()
    {
        var client = Sallow(Prefix());
        client.RecallOpen = true;

        Assert.False(client.Recall(9999));
        Assert.True(client.RecallOpen);
    }

    [Fact]
    public void ARecallToAStateThatDoesNotExistIsRefused()
    {
        var client = Sallow(Prefix());
        var logged = client.Log.Count;

        Assert.False(client.Recall(9999));
        Assert.StartsWith("History holds ", client.Status);
        Assert.Equal(logged, client.Log.Count);
    }

    private static string WriteScript(IEnumerable<string> lines)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ironwake-recall-{Guid.NewGuid():N}.script");
        File.WriteAllLines(path, lines);
        return path;
    }
}
