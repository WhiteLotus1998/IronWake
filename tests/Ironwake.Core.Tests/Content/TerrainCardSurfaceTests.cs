using System.Text.Json;
using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The terrain card on every surface (issue 610): the console's <c>terrain</c> command, the
/// protocol's <c>terrain</c> query and the client's legend hover print one text, the core's.
/// </summary>
[Collection("console")]
public class TerrainCardSurfaceTests
{
    private static string Ebb => Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "ebb_ford_tide.map");

    private static (GameContent Content, BattleState State) Load(string path)
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        return (content, BattleState.From(MapFiles.Load(path, content), content, content.Cast, 1));
    }

    [Fact]
    public void TheConsoleTerrainCommandPrintsEveryCardOnTheBoardAfterItsGlyph()
    {
        var (content, state) = Load(Ebb);
        var script = Path.Combine(Path.GetTempPath(), "ironwake-terrain-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(script, "terrain\nterrain ~\nterrain swamp\n");
        try
        {
            var output = ConsoleCapture.Run(() => Ironwake.Cli.Program.Main(new[] { "play", Ebb, "--seed", "1", "--script", script, "--content", Fixture.RealContentDirectory() }));
            var expected = TerrainCard.OnBoard(state.Map).Select(id => $"{content.TerrainById(id).Glyph}  {TerrainCard.Text(state, content, id)}").ToList();

            Assert.Contains("> terrain\n" + string.Join("\n", expected) + "\n> terrain ~\n" + expected.Single(l => l.StartsWith('~')) + "\n", output, StringComparison.Ordinal);
            Assert.Contains("holds its tile", expected.Single(l => l.StartsWith('~')), StringComparison.Ordinal);
            Assert.Contains("ERROR: no terrain 'swamp'; name it by its glyph, id or name", output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Fact]
    public void TheProtocolAndTheClientCarryTheConsolesCard()
    {
        var (content, state) = Load(Ebb);
        var session = new ProtocolSession(content, state, new StringWriter());
        var client = new ClientSession(content, state);
        foreach (var id in TerrainCard.OnBoard(state.Map))
        {
            var glyph = content.TerrainById(id).Glyph;
            using var answer = JsonDocument.Parse(session.Answer($$"""{"query":"terrain","terrain":"{{glyph}}"}"""));

            Assert.Equal(id, answer.RootElement.GetProperty("terrain").GetString());
            Assert.Equal(TerrainCard.Text(state, content, id), answer.RootElement.GetProperty("text").GetString());
            Assert.Equal(TerrainCard.Text(state, content, id), client.TerrainText(id));
        }
    }

    [Fact]
    public void TheProtocolRefusesAnUnknownTerrainAsABadRequest()
    {
        var (content, state) = Load(Ebb);
        var session = new ProtocolSession(content, state, new StringWriter());

        using var answer = JsonDocument.Parse(session.Answer("""{"query":"terrain","terrain":"swamp"}"""));

        Assert.Equal("badRequest", answer.RootElement.GetProperty("error").GetProperty("reason").GetString());
        Assert.Equal("no terrain 'swamp'; name it by its glyph, id or name", answer.RootElement.GetProperty("error").GetProperty("message").GetString());
    }
}
