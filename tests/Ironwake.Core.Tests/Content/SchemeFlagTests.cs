using Ironwake.Sim;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The CLI's <c>--scheme</c> and the Sim's <c>--trace</c> read one word through
/// <see cref="RollSchemes"/>, so a trace under one roll replays in the CLI under the same
/// flag as the same game; issue 158's <c>--formula</c> is retired with the keep
/// (DECISIONS/0028); and the terrain column of the hit-band tally files every strike it counts.
/// </summary>
[Collection("console")]
public class SchemeFlagTests
{
    [Fact]
    public void EverySchemeWordParsesAndNothingElseDoes()
    {
        Assert.Equal(RollScheme.OneRoll, RollSchemes.Parse("one"));
        Assert.Equal(RollScheme.TwoRollAverage, RollSchemes.Parse("two"));
        Assert.Null(RollSchemes.Parse("three"));
    }

    [Fact]
    public void ThePlayHeaderNamesTheScheme()
    {
        var two = Play(out _, "");
        Assert.StartsWith("Old Mill Road, seed 7, scheme TwoRollAverage\n", two);

        var one = Play(out _, "", "7", "--scheme", "one");
        Assert.StartsWith("Old Mill Road, seed 7, scheme OneRoll\n", one);
    }

    [Theory]
    [InlineData("--scheme", "three")]
    [InlineData("--formula", "arm4")]
    [InlineData("--formula", "standard")]
    public void AnUnknownSchemeOrTheRetiredFormulaFlagIsAUsageError(string flag, string value)
    {
        var output = Run(out var exit, "play", "old_mill_road", flag, value, "--content", Fixture.RealContentDirectory());

        Assert.Equal(2, exit);
        Assert.Contains($"ERROR: unexpected argument '{flag}'", output);
        Assert.Contains("[--scheme one|two]", output);
        Assert.DoesNotContain("--formula", PlaySessionUsage());
    }

    /// <summary>
    /// A trace under one roll replays in the CLI given the same scheme: every line applies
    /// under --strict and the battle ends where the Sim said. One game, two front ends.
    /// </summary>
    [Fact]
    public void AOneRollTraceReplaysInTheCliUnderTheSameScheme()
    {
        var trace = Capture(() => Program.Trace("saltmarsh_ford", 5, RollScheme.OneRoll));
        Assert.StartsWith("# saltmarsh_ford seed 5, heuristic player, one roll\n", trace);
        var outcome = System.Text.RegularExpressions.Regex.Match(trace, "# (Won|Lost) on turn (\\d+): (.*)\n$");
        Assert.True(outcome.Success, trace);

        var path = Path.Combine(Path.GetTempPath(), "ironwake-scheme-trace-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, trace);
        try
        {
            var exit = 0;
            var output = Capture(() => exit = Ironwake.Cli.Program.Main(new[] { "play", "saltmarsh_ford", "--seed", "5", "--script", path, "--strict", "--scheme", "one", "--content", Fixture.RealContentDirectory() }));

            Assert.DoesNotContain("strict: stopped", output);
            Assert.EndsWith($"battle {outcome.Groups[1].Value.ToLowerInvariant()}: {outcome.Groups[3].Value}\n", output);
            Assert.Equal(outcome.Groups[1].Value == "Won" ? 0 : 1, exit);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheTraceUsageNamesTheSchemeFlagAndNotTheFormula()
    {
        Assert.Contains("--trace <map> <seed> [--scheme one|two] |", Program.Usage);
        Assert.DoesNotContain("--formula", Program.Usage);
    }

    [Fact]
    public void TheTerrainRowsFileEveryStrikeTheSideRowsCount()
    {
        var hits = new HitTally();
        var dir = Fixture.RealContentDirectory();
        var content = Ironwake.Content.ContentLoader.Load(dir);
        var map = Ironwake.Content.MapFiles.LoadAll(dir, content).Single(m => m.Id == "saltmarsh_ford").Map;
        Runner.Play(content, map, 7, new HeuristicPlayer(), hits: hits);

        var sides = hits.Lines("t").Split('\n');
        var terrain = hits.TerrainLines("t").Split('\n');
        foreach (var (side, row) in new[] { ("player", sides[0]), ("enemy", sides[1]) })
        {
            var total = Strikes(row);
            var filed = terrain.Where(l => l.StartsWith($"t {side} into ", StringComparison.Ordinal)).Sum(Strikes);
            Assert.True(total > 0, row);
            Assert.Equal(total, filed);
        }

        Assert.Contains(terrain, l => !l.Contains(" into plain:", StringComparison.Ordinal));
    }

    private static string PlaySessionUsage() => Ironwake.Cli.PlaySession.Usage;

    private static int Strikes(string line) =>
        int.Parse(line.Split("strikes ")[1].Split(',')[0], System.Globalization.CultureInfo.InvariantCulture);

    private static string Play(out int exit, string script, string seed = "7", params string[] flags)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-scheme-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, script);
        try
        {
            return Run(out exit, new[] { "play", "old_mill_road", "--seed", seed, "--script", path, "--content", Fixture.RealContentDirectory() }.Concat(flags).ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string Run(out int exit, params string[] args)
    {
        var code = 0;
        var output = ConsoleCapture.Run(() => code = Ironwake.Cli.Program.Main(args));
        exit = code;
        return output;
    }

    private static string Capture(Action run) =>
        ConsoleCapture.Run(run, Path.GetDirectoryName(Fixture.RealContentDirectory())!);
}
