namespace Ironwake.Core.Tests.Content;

/// <summary>CLI tests redirect the process-wide console, so they share one collection and never run in parallel with each other.</summary>
[Collection("console")]
public class CliValidateTests
{
    [Fact]
    public void ValidateReportsOkOnTheStarterContent()
    {
        var output = Run(out var exit, "validate", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.StartsWith("OK:", output);
    }

    [Fact]
    public void ValidatePrintsTheFirstErrorAndExitsOne()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ironwake-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "classes.json"), Fixture.Classes);
            File.WriteAllText(Path.Combine(dir, "weapons.json"), Fixture.Weapons);
            File.WriteAllText(Path.Combine(dir, "rules.json"), Fixture.Rules);
            File.WriteAllText(Path.Combine(dir, "items.json"), Fixture.Items);
            File.WriteAllText(Path.Combine(dir, "abilities.json"), Fixture.Abilities);
            File.WriteAllText(Path.Combine(dir, "terrain.json"), "{ \"terrain\": [ { \"id\": \"plain\", \"name\": \"Plain\", \"glyph\": \"..\", \"cost\": { \"infantry\": 1, \"cavalry\": 1, \"flying\": 1, \"armored\": 1 } } ] }");

            var output = Run(out var exit, "validate", dir);

            Assert.Equal(1, exit);
            Assert.StartsWith("ERROR: terrain.json > entry 'plain' > field 'glyph'", output);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ValidateRefusesACampaignMapWithNoFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ironwake-cli-campaign-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "maps"));
        try
        {
            foreach (var name in new[] { "classes.json", "weapons.json", "terrain.json", "rules.json", "items.json", "abilities.json" })
            {
                File.Copy(Path.Combine(Fixture.RealContentDirectory(), name), Path.Combine(dir, name));
            }

            Directory.CreateDirectory(Path.Combine(dir, "units"));
            File.Copy(Path.Combine(Fixture.RealContentDirectory(), "units", "enemies.json"), Path.Combine(dir, "units", "enemies.json"));
            File.Copy(Path.Combine(Fixture.RealContentDirectory(), "units", "cast.json"), Path.Combine(dir, "units", "cast.json"));
            File.WriteAllText(Path.Combine(dir, "campaign.json"), """{ "startingPurse": 0, "certificationPrice": 0, "maps": [ { "map": "lost_road", "reward": 0, "stock": [] } ] }""");

            var output = Run(out var exit, "validate", dir);

            Assert.Equal(1, exit);
            Assert.StartsWith("ERROR: campaign.json", output);
            Assert.Contains("lost_road", output);
            Assert.Contains("no file maps/lost_road.map", output);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ValidateCountsMapsAndReportsTheFirstMapError()
    {
        var ok = Run(out var okExit, "validate", Fixture.RealContentDirectory());
        Assert.Equal(0, okExit);
        Assert.Contains("8 maps", ok);

        var dir = Path.Combine(Path.GetTempPath(), "ironwake-cli-maps-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "maps"));
        try
        {
            foreach (var name in new[] { "classes.json", "weapons.json", "terrain.json", "rules.json", "items.json", "abilities.json" })
            {
                File.Copy(Path.Combine(Fixture.RealContentDirectory(), name), Path.Combine(dir, name));
            }

            Directory.CreateDirectory(Path.Combine(dir, "units"));
            File.Copy(Path.Combine(Fixture.RealContentDirectory(), "units", "enemies.json"), Path.Combine(dir, "units", "enemies.json"));
            File.Copy(Path.Combine(Fixture.RealContentDirectory(), "units", "cast.json"), Path.Combine(dir, "units", "cast.json"));
            File.WriteAllText(Path.Combine(dir, "maps", "broken.map"), "name: Broken\nsize: 2x1\nwin: rout\nturn_limit: 5\n\n.?\n\nunits:\nP captain 0,0\n");

            var output = Run(out var exit, "validate", dir);

            Assert.Equal(1, exit);
            Assert.StartsWith("ERROR: " + Path.Combine(dir, "maps", "broken.map") + ", line 6: unknown terrain glyph '?'", output);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Issue 397: validate parses the trial and keep maps too, and counts each kind apart.</summary>
    [Fact]
    public void ValidateCountsTrialAndKeepMaps()
    {
        var output = Run(out var exit, "validate", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.Contains("8 maps, 2 trials, 2 side maps, 2 keep maps from", output);
    }

    [Theory]
    [InlineData("trials", "outrider_trial.map")]
    [InlineData("keep", "ironwake_raid.map")]
    public void ValidateReportsABrokenTrialOrKeepMapByFileAndLine(string subdirectory, string file)
    {
        var dir = CopyOfRealContent();
        try
        {
            var path = Path.Combine(dir, subdirectory, file);
            var lines = File.ReadAllLines(path);
            var line = Array.FindIndex(lines, l => l.StartsWith("win:", StringComparison.Ordinal));
            lines[line] = "win: bogus";
            File.WriteAllLines(path, lines);

            var output = Run(out var exit, "validate", dir);

            Assert.Equal(1, exit);
            Assert.StartsWith($"ERROR: {path}, line {line + 1}:", output);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ValidateRefusesACampaignTrialWithNoFile()
    {
        var dir = CopyOfRealContent();
        try
        {
            File.Delete(Path.Combine(dir, "trials", "bulwark_trial.map"));

            var output = Run(out var exit, "validate", dir);

            Assert.Equal(1, exit);
            Assert.StartsWith("ERROR: campaign.json", output);
            Assert.Contains("no file trials/bulwark_trial.map", output);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string CopyOfRealContent()
    {
        var source = Fixture.RealContentDirectory();
        var dir = Path.Combine(Path.GetTempPath(), "ironwake-cli-copy-" + Guid.NewGuid().ToString("N"));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(dir, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        return dir;
    }

    [Fact]
    public void ShowPrintsTheMapView()
    {
        var output = Run(out var exit, "show", Path.Combine(Fixture.RealContentDirectory(), "maps", "old_mill_road.map"), Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.StartsWith("Old Mill Road  12x10", output);
        Assert.Contains(" 8 .AB.........", output);
        Assert.Contains("d  Mill Bandit L2         10,1   Plain  group mill, guard", output);
        Assert.All(output, c => Assert.True(c < 128, "non-ASCII character in CLI output"));
    }

    [Fact]
    public void ShowWithoutAMapOrWithAMissingMapFails()
    {
        Run(out var usage, "show");
        var output = Run(out var missing, "show", "/no/such.map", Fixture.RealContentDirectory());

        Assert.Equal(2, usage);
        Assert.Equal(1, missing);
        Assert.StartsWith("ERROR: /no/such.map: file not found", output);
    }

    [Fact]
    public void UnknownCommandExitsTwo()
    {
        Run(out var exit, "dance");

        Assert.Equal(2, exit);
    }

    [Fact]
    public void OutputIsPlainAscii()
    {
        var output = Run(out _, "validate", Fixture.RealContentDirectory());

        Assert.All(output, c => Assert.True(c < 128, "non-ASCII character in CLI output"));
    }

    private static string Run(out int exit, params string[] args)
    {
        var code = 0;
        var output = ConsoleCapture.Run(() => code = Ironwake.Cli.Program.Main(args));
        exit = code;
        return output;
    }
}
