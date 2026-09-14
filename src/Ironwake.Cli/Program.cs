using Ironwake.Content;

namespace Ironwake.Cli;

/// <summary>
/// Console front end for Ironwake. <c>validate</c> loads every content file and reports
/// OK or the first error. <c>play</c> arrives with issue 11; until then it prints usage
/// and exits non-zero so a script cannot mistake it for a working game.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        switch (args[0])
        {
            case "validate":
                return Validate(args.Length > 1 ? args[1] : "content");
            case "play":
                Console.WriteLine("play is not implemented yet (issue 11)");
                return 2;
            default:
                PrintUsage();
                return 2;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("ironwake cli (rules version " + Core.RulesVersion.Current + ")");
        Console.WriteLine("usage: ironwake validate [content-dir]");
        Console.WriteLine("       ironwake play <map> [--seed N] [--script file]   (not implemented yet)");
    }

    private static int Validate(string contentDir)
    {
        try
        {
            var content = ContentLoader.Load(contentDir);
            Console.WriteLine(
                $"OK: {content.Terrain.Count} terrain, {content.Classes.Count} classes, " +
                $"{content.Weapons.Count} weapons, {content.Units.Count} units from {contentDir}");
            return 0;
        }
        catch (ContentException e)
        {
            Console.WriteLine("ERROR: " + e.Message);
            return 1;
        }
    }
}
