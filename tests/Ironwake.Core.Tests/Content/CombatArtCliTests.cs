using System.Text.Json;
using System.Text.Json.Nodes;
using Ironwake.Cli;
using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Issue 68 at the console and over the protocol: <c>attack</c> and <c>forecast</c> take
/// <c>art &lt;id&gt;</c>, the forecast prints the art line, <c>show</c> lists the arts a
/// unit knows, and the journaled play replays. The content is the shipped set with
/// <c>docs/samples/arts/abilities.json</c> in place of <c>abilities.json</c> and the captain
/// knowing its one art, since no shipped unit knows an art yet.
/// </summary>
[Collection("console")]
public class CombatArtCliTests : IDisposable
{
    private readonly string _content = SampleContent();

    public void Dispose()
    {
        Directory.Delete(_content, recursive: true);
    }

    /// <summary>A copy of the real content with the sample arts and the captain knowing <c>cleave</c>.</summary>
    private static string SampleContent()
    {
        var real = Fixture.RealContentDirectory();
        var copy = Path.Combine(Path.GetTempPath(), "ironwake-arts-" + Guid.NewGuid().ToString("N"));
        foreach (var file in Directory.GetFiles(real, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(copy, Path.GetRelativePath(real, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        var repo = Directory.GetParent(real)!.FullName;
        File.Copy(Path.Combine(repo, "docs", "samples", "arts", "abilities.json"), Path.Combine(copy, ContentFiles.AbilitiesName), overwrite: true);
        var castPath = Path.Combine(copy, "units", "cast.json");
        var cast = JsonNode.Parse(File.ReadAllText(castPath))!;
        cast["units"]![0]!["abilities"] = new JsonArray("cleave");
        File.WriteAllText(castPath, cast.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return copy;
    }

    private string Play(out int exit, string script, string seed = "11", params string[] extra)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-arts-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, script);
        try
        {
            return Run(out exit, new[] { "play", "old_mill_road", "--seed", seed, "--script", path, "--content", _content }.Concat(extra).ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ShowListsTheArtsAUnitKnows()
    {
        var output = Play(out _, "show captain\nshow wren\n");

        Assert.Contains("  ranks: sword E (0), lance E (0), axe E (0)\n  arts: cleave (sword E, cost 2): +5 Mt and +5 Wt; two extra uses, hit or miss.\n", output);
        Assert.DoesNotContain("  arts: ", output[output.IndexOf("> show wren", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void TheJournaledCleavePlayWinsAndShowsTheArtCostingTheDouble()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-25-old_mill_road-11-cleave.script");

        var output = Run(out var exit, "play", "old_mill_road", "--seed", "11", "--script", script, "--strict", "--content", _content);

        Assert.Equal(0, exit);
        Assert.Contains(
            "> forecast captain brigand-1\nforecast captain -> brigand-1: dmg 11 x2 hit 90% crit 5%; counter: dmg 10 hit 49% crit 0%\n"
            + "> attack captain brigand-1 art cleave\nforecast captain -> brigand-1: dmg 16 hit 90% crit 5%; counter: dmg 10 hit 56% crit 0%\n"
            + "  art Cleave: Iron Sword at mt 10 hit 75 crit 0 wt 10 range 1-1; spends up to 3 of 40 uses, 2 of them hit or miss\n"
            + "captain declares cleave with iron_sword, spending 2 extra uses\n",
            output);
        Assert.EndsWith("battle won: rout\n", output);
        Assert.DoesNotContain("rejected ", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    [Fact]
    public void AnArtTheCaptainCannotDeclareIsRefusedWithItsReason()
    {
        var output = Play(out _, "move captain 2,6\nmove wren 3,7\nend\nforecast wren brigand-1 art cleave\nattack captain brigand-1 art sunder\nattack captain brigand-1 art\n");

        Assert.Contains("> forecast wren brigand-1 art cleave\nERROR: wren knows no art 'cleave'\n", output);
        Assert.Contains("> attack captain brigand-1 art sunder\nERROR: captain knows no art 'sunder'\n", output);
        Assert.Contains("> attack captain brigand-1 art\nERROR: usage: attack <unit> <target> [slot] [art <id>]\n", output);
    }

    [Fact]
    public void TheProtocolForecastsAndDeclaresAnArt()
    {
        var script = string.Join("\n",
            """{"type":"move","unit":"captain","to":{"x":2,"y":6}}""",
            """{"type":"move","unit":"wren","to":{"x":3,"y":7}}""",
            """{"type":"end"}""",
            """{"query":"forecast","unit":"captain","target":"brigand-1","art":"cleave"}""",
            """{"type":"attack","unit":"captain","target":"brigand-1","slot":null,"art":"cleave"}""") + "\n";
        var path = Path.Combine(Path.GetTempPath(), "ironwake-arts-" + Guid.NewGuid().ToString("N") + ".jsonl");
        File.WriteAllText(path, script);
        try
        {
            var output = Run(out _, "play", "old_mill_road", "--seed", "11", "--protocol", "--script", path, "--content", _content);

            Assert.Contains("\"scheme\":\"twoRollAverage\",\"artCost\":2}", output);
            Assert.Contains("\\n  art Cleave: Iron Sword at mt 10", output);
            Assert.Contains("{\"type\":\"artDeclared\",\"unit\":\"captain\",\"art\":\"cleave\",\"item\":\"iron_sword\",\"cost\":2,\"text\":\"captain declares cleave with iron_sword, spending 2 extra uses\"}", output);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string Run(out int exit, params string[] args)
    {
        var code = 0;
        var output = ConsoleCapture.Run(() => code = Program.Main(args));
        exit = code;
        return output;
    }
}
