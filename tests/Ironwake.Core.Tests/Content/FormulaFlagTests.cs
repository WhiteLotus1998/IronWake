using Ironwake.Sim;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Issue 158's flags: the CLI's <c>--formula</c> and <c>--scheme</c> and the Sim's
/// <c>--trace</c> take one arm through <see cref="FormulaArms"/>, so a trace under an arm
/// replays in the CLI under the same flags as the same game, and the terrain column of the
/// hit-band tally files every strike it counts.
/// </summary>
[Collection("console")]
public class FormulaFlagTests
{
    [Fact]
    public void EveryArmKeyParsesBackToItsArmAndNothingElseParses()
    {
        foreach (var arm in Enum.GetValues<FormulaArm>())
        {
            Assert.Equal(arm, FormulaArms.Parse(FormulaArms.Key(arm)));
        }

        Assert.Equal(FormulaArm.FullStrSpeedTwiceIronHit15, FormulaArms.Parse("arm4"));
        Assert.Null(FormulaArms.Parse("arm5"));
        Assert.Null(FormulaArms.Parse("main"));
        Assert.Equal(RollScheme.OneRoll, FormulaArms.ParseScheme("one"));
        Assert.Equal(RollScheme.TwoRollAverage, FormulaArms.ParseScheme("two"));
        Assert.Null(FormulaArms.ParseScheme("three"));
    }

    [Fact]
    public void ThePlayHeaderNamesTheSchemeAndTheFormula()
    {
        var standard = Play(out _, "");
        Assert.StartsWith("Old Mill Road, seed 7, scheme TwoRollAverage, formula standard\n", standard);

        var arm = Play(out _, "", "7", "--scheme", "one", "--formula", "arm4");
        Assert.StartsWith("Old Mill Road, seed 7, scheme OneRoll, formula arm4\n", arm);
    }

    [Theory]
    [InlineData("--formula", "arm9")]
    [InlineData("--formula", "main")]
    [InlineData("--scheme", "three")]
    public void AnUnknownFormulaOrSchemeIsAUsageError(string flag, string value)
    {
        var output = Run(out var exit, "play", "old_mill_road", flag, value, "--content", Fixture.RealContentDirectory());

        Assert.Equal(2, exit);
        Assert.Contains($"ERROR: unexpected argument '{flag}'", output);
        Assert.Contains("[--scheme one|two] [--formula standard|arm1|arm2|arm3|arm4]", output);
    }

    /// <summary>
    /// The iron cut and the formula both reach the forecast the CLI prints. Seed 29 on Old
    /// Mill Road, opened as Code's cold entry opened it: the brigand strikes Wren in the
    /// first enemy phase, and Wren's Iron Sword counter reads lower under arm 3 (content)
    /// and lower again under arm 4 (content and formula) than on main.
    /// </summary>
    [Fact]
    public void TheArmChangesTheForecastTheCliPrints()
    {
        const string script = "move wren 4,7\nwait wren\nmove captain 2,7\nwait captain\nend\n";
        var main = CounterHit(Play(out _, script, "29"));
        var arm3 = CounterHit(Play(out _, script, "29", "--formula", "arm3"));
        var arm4 = CounterHit(Play(out _, script, "29", "--formula", "arm4"));

        Assert.True(arm3 < main, $"{arm3} against {main}");
        Assert.True(arm4 < arm3, $"{arm4} against {arm3}");
    }

    /// <summary>
    /// A trace under arm 4 and one roll names the flags that replay it, and the CLI given
    /// those flags applies every line under --strict and ends where the Sim said: one game.
    /// </summary>
    [Fact]
    public void AnArmTraceReplaysInTheCliUnderTheSameFlags()
    {
        var trace = Capture(() => Program.Trace("saltmarsh_ford", 5, RollScheme.OneRoll, FormulaArm.FullStrSpeedTwiceIronHit15));
        Assert.StartsWith("# saltmarsh_ford seed 5, heuristic player, one roll, arm 4 (arm 2 + iron hit -15) (replay with --scheme one --formula arm4)\n", trace);
        var outcome = System.Text.RegularExpressions.Regex.Match(trace, "# (Won|Lost) on turn (\\d+): (.*)\n$");
        Assert.True(outcome.Success, trace);

        var path = Path.Combine(Path.GetTempPath(), "ironwake-arm-trace-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, trace);
        try
        {
            var exit = 0;
            var output = Capture(() => exit = Ironwake.Cli.Program.Main(new[] { "play", "saltmarsh_ford", "--seed", "5", "--script", path, "--strict", "--scheme", "one", "--formula", "arm4", "--content", Fixture.RealContentDirectory() }));

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
    public void TheTraceUsageNamesTheFormulaFlag()
    {
        Assert.Contains("--trace <map> <seed> [--scheme one|two] [--formula standard|arm1|arm2|arm3|arm4]", Program.Usage);
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

    private static int Strikes(string line) =>
        int.Parse(line.Split("strikes ")[1].Split(',')[0], System.Globalization.CultureInfo.InvariantCulture);

    private static int CounterHit(string output)
    {
        var line = output.Split('\n').First(l => l.StartsWith("forecast brigand-1 -> wren", StringComparison.Ordinal));
        var hit = System.Text.RegularExpressions.Regex.Match(line, "counter: .*hit (\\d+)%");
        Assert.True(hit.Success, line);
        return int.Parse(hit.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Play(out int exit, string script, string seed = "7", params string[] flags)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-formula-" + Guid.NewGuid().ToString("N") + ".script");
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
