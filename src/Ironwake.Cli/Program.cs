using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Cli;

/// <summary>
/// Console front end for Ironwake. <c>validate</c> loads every content file and every map
/// and reports OK or the first error. <c>show</c> prints a map's console view. <c>reach</c>
/// prints the view with one unit's reachable tiles marked, for checking a map by eye. <c>play</c>
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
            case "reach":
                return Reach(args.Skip(1).ToArray());
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
        Console.WriteLine("ironwake cli (rules version " + RulesVersion.Current + ")");
        Console.WriteLine("usage: ironwake validate [content-dir]");
        Console.WriteLine("       ironwake show <map-file> [content-dir]");
        Console.WriteLine("       ironwake reach <map-file> <x,y> [<movement>:<mov>] [content-dir]");
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

    private const string ReachUsage = "usage: ironwake reach <map-file> <x,y> [<movement>:<mov>] [content-dir]";

    /// <summary>
    /// Marks the tiles the unit at <c>x,y</c> can end on. An enemy's movement and Mov come
    /// from its template's class. A player slot has no class until the roster exists
    /// (issue 13), so it is treated as infantry with Mov 4 unless <c>movement:mov</c> is
    /// given, which also lets an empty tile be probed while authoring a map.
    /// </summary>
    private static int Reach(string[] args)
    {
        if (args.Length < 2 || !TryParseCoord(args[1], out var at))
        {
            Console.WriteLine(ReachUsage);
            return 2;
        }

        MovementType? movement = null;
        var mov = 0;
        var contentDir = "content";
        foreach (var arg in args.Skip(2))
        {
            if (TryParseMovement(arg, out var parsedMovement, out var parsedMov))
            {
                movement = parsedMovement;
                mov = parsedMov;
            }
            else
            {
                contentDir = arg;
            }
        }

        try
        {
            var content = ContentLoader.Load(contentDir);
            var map = MapFiles.Load(args[0], content);
            if (!map.Contains(at))
            {
                Console.WriteLine($"ERROR: {at} is outside the {map.Width}x{map.Height} map");
                return 1;
            }

            var placement = map.PlacementAt(at);
            var side = placement?.Side ?? Side.Player;
            if (movement is null)
            {
                switch (placement)
                {
                    case EnemyPlacement enemy:
                        var unitClass = content.Class(content.Unit(enemy.TemplateId).ClassId);
                        movement = unitClass.Movement;
                        mov = unitClass.Mov;
                        break;
                    case PlayerPlacement:
                        movement = MovementType.Infantry;
                        mov = 4;
                        break;
                    default:
                        Console.WriteLine($"ERROR: no unit at {at}; give <movement>:<mov> to probe an empty tile");
                        return 1;
                }
            }

            var reach = Movement.Reach(map, content, at, movement.Value, mov, tile => map.OccupantAt(tile, side));
            Console.Write(MapRenderer.Render(map, content, reach));
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

    private static bool TryParseCoord(string text, out Coord at)
    {
        at = default;
        var parts = text.Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
        {
            return false;
        }

        at = new Coord(x, y);
        return true;
    }

    private static bool TryParseMovement(string text, out MovementType movement, out int mov)
    {
        movement = default;
        mov = 0;
        var parts = text.Split(':');
        return parts.Length == 2
            && Enum.TryParse(parts[0], ignoreCase: true, out movement)
            && Enum.IsDefined(movement)
            && int.TryParse(parts[1], out mov)
            && mov >= 0;
    }

    private static int Show(string mapPath, string contentDir)
    {
        try
        {
            var content = ContentLoader.Load(contentDir);
            var map = MapFiles.Load(mapPath, content);
            Console.Write(MapRenderer.Render(map, content));
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
