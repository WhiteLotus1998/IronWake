using System.Text.Json;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The seize tile's name is one value (issue 569): the throne terrain's display name, lowercased,
/// read by the objective, the protocol's <c>seizeName</c>, and the client's end card, legend and
/// how-to-play, so the console and the client never call one tile two things.
/// </summary>
public class SeizeNameTests
{
    private static string Hall(string win, string? header = null) => $"""
        name: Hall
        size: 16x3
        win: {win}{(header is null ? "" : "\n" + header)}
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ................
        ............T...
        ................

        units:
        P captain 1,1
        E brigand 15,0 group:far behavior:hold

        """;

    private static GameContent Renamed(GameContent content, string name) =>
        content with { Terrain = content.Terrain.SetItem(MapDefinition.ThroneTerrainId, content.Terrain[MapDefinition.ThroneTerrainId] with { Name = name }) };

    [Fact]
    public void TheSeizeTileIsNamedByTheThroneTerrainsDisplayNameLowercased()
    {
        Assert.Equal("Gate", Starter.Terrain[MapDefinition.ThroneTerrainId].Name);
        Assert.Equal("gate", Objective.SeizeName(Starter));
        Assert.Equal("bridge", Objective.SeizeName(Renamed(Starter, "Bridge")));
    }

    /// <summary>The name is read from the content, not a literal: renaming the terrain renames every line.</summary>
    [Fact]
    public void TheObjectiveLinePrintsWhateverTheContentCallsTheSeizeTile()
    {
        var content = Renamed(Starter, "Bridge");
        var state = Start(map: Hall("seize"));

        Assert.Equal("Get the captain to the bridge by the end of turn 10. Captain hale must survive.", Objective.Line(state, content));
        Assert.Contains("on the bridge at 12,1", Assert.Single(Objective.Rules(state, content)), StringComparison.Ordinal);
        Assert.DoesNotContain("gate", Objective.Line(state, content), StringComparison.Ordinal);
    }

    /// <summary>The fallback's guard: content without a throne terrain names the tile by its id.</summary>
    [Fact]
    public void ContentWithoutAThroneTerrainNamesTheTileByItsId()
    {
        var content = Starter with { Terrain = Starter.Terrain.Remove(MapDefinition.ThroneTerrainId) };

        Assert.Equal("throne", Objective.SeizeName(content));
    }

    [Fact]
    public void TheProtocolStateCarriesTheSeizeNameOnASeizeMapOnly()
    {
        using var seize = JsonDocument.Parse(ProtocolJson.BoardState(Start(map: Hall("seize")), Starter));
        using var rout = JsonDocument.Parse(ProtocolJson.State(Start(map: Hall("rout")), Starter));

        Assert.Equal("gate", seize.RootElement.GetProperty("seizeName").GetString());
        Assert.False(rout.RootElement.TryGetProperty("seizeName", out _));
    }

    /// <summary>
    /// The parity the issue is for: on the Tollgate the console's objective and verdict, the
    /// protocol, the client's end card, its legend (the terrain's own name) and the how-to-play's
    /// goal line all say the same word, and none says throne.
    /// </summary>
    [Fact]
    public void OnTheTollgateTheConsoleTheProtocolAndTheClientCallTheSeizeTileOneName()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), content);
        var client = new ClientSession(content, BattleState.From(map, content, content.Cast, 113));
        var name = Objective.SeizeName(content);
        Assert.Equal("gate", name);

        using var json = JsonDocument.Parse(ProtocolJson.State(client.State, content));
        Assert.Equal(name, json.RootElement.GetProperty("seizeName").GetString());
        Assert.Equal($"Get the captain to the {name} by the end of turn 10. Captain Fenn must survive.", client.Objective);
        Assert.Contains($"must stand on the {name} at 7,1", Assert.Single(Objective.Rules(client.State, content)), StringComparison.Ordinal);
        Assert.Equal(name, content.TerrainById(MapDefinition.ThroneTerrainId).Name.ToLowerInvariant());
        Assert.Contains(Screens.HowTo, s => s.Heading == "The goal" && s.Lines[0].Contains($"onto the {name}.", StringComparison.Ordinal));

        for (var turn = 0; turn < 12 && !client.State.Outcome.IsOver; turn++)
        {
            client.Submit(new EndPhase());
            client.Continue();
        }

        Assert.EndsWith($"short of the {name}.", client.Ending!.Line, StringComparison.Ordinal);
        Assert.Contains($"not on the {name} at 7,1", client.Verdict!, StringComparison.Ordinal);
        foreach (var line in new[] { client.Objective, client.Verdict!, client.Ending.Line })
        {
            Assert.DoesNotContain("throne", line, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Issue 1208: a map's <c>seize_name:</c> names its throne tile in every line the content's
    /// name would print, the tile's own terrain name included; the map file writes it back.
    /// </summary>
    [Fact]
    public void ASeizeMapWithASeizeNameHeaderPrintsItsNameInTheObjectiveTheLossAndTheTile()
    {
        var client = new ClientSession(Starter, Start(map: Hall("seize", "seize_name: altar")));
        var map = client.State.Map;
        var throne = new Coord(12, 1);

        Assert.Equal("altar", map.SeizeName);
        Assert.Equal("altar", Objective.SeizeName(map, Starter));
        Assert.Equal("Get the captain to the altar by the end of turn 10. Captain hale must survive.", client.Objective);
        Assert.Contains("on the altar at 12,1", Assert.Single(Objective.Rules(client.State, Starter)), StringComparison.Ordinal);
        Assert.Equal("Altar", map.TerrainAt(throne, Starter).Name);
        Assert.StartsWith("Altar.", TerrainCard.Text(client.State, Starter, MapDefinition.ThroneTerrainId), StringComparison.Ordinal);
        Assert.Equal("Gate", Starter.TerrainById(MapDefinition.ThroneTerrainId).Name);
        using (var json = JsonDocument.Parse(ProtocolJson.State(client.State, Starter)))
        {
            Assert.Equal("altar", json.RootElement.GetProperty("seizeName").GetString());
        }

        Assert.Contains("seize_name: altar\n", MapFormat.Write(map, Starter), StringComparison.Ordinal);

        for (var turn = 0; turn < 12 && !client.State.Outcome.IsOver; turn++)
        {
            client.Submit(new EndPhase());
            client.Continue();
        }

        Assert.Contains("on the altar at 12,1", client.Verdict!, StringComparison.Ordinal);
        Assert.EndsWith("short of the altar.", client.Ending!.Line, StringComparison.Ordinal);
    }

    [Fact]
    public void ASeizeMapWithoutTheHeaderStillCallsTheTileAGate()
    {
        var state = Start(map: Hall("seize"));

        Assert.Null(state.Map.SeizeName);
        Assert.Equal("gate", Objective.SeizeName(state.Map, Starter));
        Assert.Equal("Gate", state.Map.TerrainAt(new Coord(12, 1), Starter).Name);
        Assert.DoesNotContain("seize_name", MapFormat.Write(state.Map, Starter), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("seize", "seize_name: Altar", "must be lowercase words")]
    [InlineData("seize", "seize_name: old  altar", "must be lowercase words")]
    [InlineData("seize", "seize_name: altar2", "must be lowercase words")]
    [InlineData("rout", "seize_name: altar", "seize_name: needs win: seize")]
    public void ABadSeizeNameHeaderIsRefusedNamingTheFileAndLine(string win, string header, string problem)
    {
        var ex = Assert.Throws<MapException>(() => MapFixture.Parse(Hall(win, header), "hall.map"));

        Assert.StartsWith("hall.map, line 4: ", ex.Message, StringComparison.Ordinal);
        Assert.Contains(problem, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>The First Shrine is the one map that names its tile (issue 1208): Maud goes to the altar.</summary>
    [Fact]
    public void TheFirstShrinesSeizeTileIsTheAltar()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "quests", "the_first_shrine.map"), content);

        Assert.Equal("altar", map.SeizeName);
        Assert.Equal("altar", Objective.SeizeName(map, content));
    }
}
