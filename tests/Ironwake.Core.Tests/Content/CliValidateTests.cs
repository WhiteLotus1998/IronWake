namespace Ironwake.Core.Tests.Content;

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
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            exit = Ironwake.Cli.Program.Main(args);
        }
        finally
        {
            Console.SetOut(original);
        }

        return writer.ToString();
    }
}
