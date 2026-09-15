using Ironwake.Content;

namespace Ironwake.Cli;

/// <summary>
/// Console front end for Ironwake. <c>validate</c> loads every content file and every map
/// and reports OK or the first error. <c>show</c> prints a map's console view. <c>play</c>
/// arrives with issue 11; until then it prints usage and exits non-zero so a script cannot
/// mistake it for a working game.
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
            case "show":
                if (args.Length < 2)
                {
                    Console.WriteLine("usage: ironwake show <map-file> [content-dir]");
                    return 2;
                }

                return Show(args[1], args.Length > 2 ? args[2] : "content");
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
        Console.WriteLine("       ironwake show <map-file> [content-dir]");
        Console.WriteLine("       ironwake play <map> [--seed N] [--script file]   (not implemented yet)");
    }

    private static int Validate(string contentDir)
    {
        try
        {
            var content = ContentLoader.Load(contentDir);
            var maps = MapFiles.LoadAll(contentDir, content);
            Console.WriteLine(
                $"OK: {content.Terrain.Count} terrain, {content.Classes.Count} classes, " +
                $"{content.Weapons.Count} weapons, {content.Units.Count} units, {maps.Count} maps from {contentDir}");
            return 0;
        }
        catch (ContentException e)
        {
            Console.WriteLine("ERROR: " + e.Message);
            return 1;
        }
        catch (MapException e)
        {
            Console.WriteLine("ERROR: " + e.Message);
            return 1;
        }
    }

    private static int Show(string mapPath, string contentDir)
    {
        try
        {
            var content = ContentLoader.Load(contentDir);
            var map = MapFiles.Load(mapPath, content);
            Console.Write(Core.MapRenderer.Render(map, content));
            return 0;
        }
        catch (ContentException e)
        {
            Console.WriteLine("ERROR: " + e.Message);
            return 1;
        }
        catch (MapException e)
        {
            Console.WriteLine("ERROR: " + e.Message);
            return 1;
        }
    }
}
