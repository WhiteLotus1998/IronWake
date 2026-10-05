using Ironwake.Cli;
using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Issue 1093 (Chat's round on #805, agreed): <c>end</c> with a unit lethal if all land prints the
/// lethal lines and ends nothing, in the attack guard's shape (issue 975); <c>end !</c> prints the
/// same lines and ends the phase, so bait stays legal. Only the lethal refuses: the escape count and
/// the wind line still print and the phase still ends. The protocol's <c>end</c> asks the same, and
/// <c>"anyway": true</c> answers it.
/// </summary>
[Collection("console")]
public sealed class EndAsksTests
{
    /// <summary>Alder Fenn at 0,1 and Wren at 0,3 with three of a waking group beside them: both are lethal if all land.</summary>
    private const string Ringed = """
        name: Field
        size: 8x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ........
        ........
        ........
        ........

        units:
        P captain 0,1
        P recruit:wren 0,3
        E soldier 2,3 group:y behavior:aggressive
        E soldier 1,2 group:y behavior:aggressive
        E brigand 3,3 group:y behavior:aggressive
        E soldier 7,0 group:z behavior:aggressive

        """;

    /// <summary>The same two with only the far soldier: nobody is lethal.</summary>
    private const string Open = """
        name: Field
        size: 8x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ........
        ........
        ........
        ........

        units:
        P captain 0,1
        P recruit:wren 0,3
        E soldier 7,0 group:z behavior:hold

        """;

    /// <summary>An Escape corridor at limit 4: the captain needs three phases, so ending turn 1 passes the last start.</summary>
    private const string Corridor = """
        name: Corridor
        size: 12x3
        win: escape
        turn_limit: 4
        recall: 0
        enemy_level: 1
        exit: 11,1

        ............
        ............
        ............

        units:
        P captain 0,1

        """;

    private static string Play(string mapText, string scriptText, params string[] extra)
    {
        var map = Path.Combine(Path.GetTempPath(), "ironwake-endasks-" + Guid.NewGuid().ToString("N") + ".map");
        var script = Path.ChangeExtension(map, ".script");
        File.WriteAllText(map, mapText);
        File.WriteAllText(script, scriptText);
        try
        {
            return ConsoleCapture.Run(() => Ironwake.Cli.Program.Main(new[] { "play", map, "--seed", "1", "--script", script, "--content", Fixture.RealContentDirectory() }.Concat(extra).ToArray()));
        }
        finally
        {
            File.Delete(map);
            File.Delete(script);
        }
    }

    [Fact]
    public void EndRefusesWhileAUnitIsLethalIfAllLand()
    {
        var output = Play(Ringed, "end\n");

        Assert.Contains("> end\nLethal if all land: Alder Fenn (", output);
        Assert.Contains("\nLethal if all land: Wren (", output);
        Assert.Contains("ERROR: Lethal if all land: Alder Fenn, Wren; add ! to end anyway: end !\n", output);
        Assert.DoesNotContain("-- Player phase ends", output);
    }

    [Fact]
    public void EndBangPrintsTheLethalLinesAndEndsThePhase()
    {
        var output = Play(Ringed, "end !\n");

        Assert.Contains("> end !\nLethal if all land: Alder Fenn (", output);
        Assert.Contains("\n-- Player phase ends, turn 1 --\n", output);
        Assert.DoesNotContain("ERROR:", output);
    }

    [Fact]
    public void EndWithNoLethalEnds()
    {
        var output = Play(Open, "end\n");

        Assert.Contains("> end\n-- Player phase ends, turn 1 --\n", output);
        Assert.DoesNotContain("Lethal if all land", output);
        Assert.DoesNotContain("ERROR:", output);
    }

    [Fact]
    public void TheEscapeCountNeverRefuses()
    {
        var output = Play(Corridor, "end\n");

        var end = output.IndexOf("> end\nCount: after this phase", StringComparison.Ordinal);
        Assert.True(end >= 0, output);
        Assert.Contains("-- Player phase ends, turn 1 --", output[end..]);
        Assert.DoesNotContain("ERROR:", output);
    }

    /// <summary>
    /// The wind line is an <c>end</c> warning too (issue 957): Code's 1280 play of the wind sample to
    /// turn 4, where nobody steps clear and the line prints, ends the phase on a bare <c>end</c>.
    /// </summary>
    [Fact]
    public void TheWindLineNeverRefuses()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var played = File.ReadAllLines(Path.Combine(repo, "docs", "transcripts", "2026-10-04-sallow_grange_wind-1280.script")).Take(35);
        var path = Path.Combine(Path.GetTempPath(), "ironwake-endasks-wind-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllLines(path, played.Append("end"));
        try
        {
            var output = ConsoleCapture.Run(() => Ironwake.Cli.Program.Main(new[] { "play", Path.Combine(repo, "docs", "samples", "sallow_grange_wind.map"), "--seed", "1280", "--script", path, "--content", Fixture.RealContentDirectory() }));

            var end = output.LastIndexOf("> end", StringComparison.Ordinal);
            Assert.Contains("The wind turns east at turn 5", output[end..]);
            Assert.Contains("-- Player phase ends, turn 4 --", output[end..]);
            Assert.DoesNotContain("ERROR:", output[end..]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AStrictScriptStopsAtAnEndThatAsks()
    {
        var output = Play(Ringed, "end\nwait captain\n", "--strict");

        Assert.Contains("ERROR: Lethal if all land: Alder Fenn, Wren; add ! to end anyway: end !\n", output);
        Assert.DoesNotContain("> wait captain", output);
    }

    [Fact]
    public void TheProtocolsEndRefusesWhileAUnitIsLethalAndAnywayEnds()
    {
        var map = Path.Combine(Path.GetTempPath(), "ironwake-endasks-" + Guid.NewGuid().ToString("N") + ".map");
        File.WriteAllText(map, Ringed);
        try
        {
            var content = ContentLoader.Load(Fixture.RealContentDirectory());
            var state = BattleState.From(MapFiles.Load(map, content), content, content.Cast, 1);
            var session = new ProtocolSession(content, state, new StringWriter());

            var refused = session.Answer("""{"type":"end"}""");
            var ended = session.Answer("""{"type":"end","anyway":true}""");

            Assert.StartsWith("{\"ok\":false,\"error\":{\"reason\":\"lethalUnconfirmed\",\"message\":\"Lethal if all land: Alder Fenn (", refused);
            Assert.Contains("\"lethal\":[{\"unit\":\"captain\",", refused);
            Assert.Contains("lethal if all land: Alder Fenn, Wren; send ", refused);
            Assert.StartsWith("{\"ok\":true,", ended);
            Assert.Contains("\"lethal\":[{\"unit\":\"captain\",", ended);
        }
        finally
        {
            File.Delete(map);
        }
    }
}
