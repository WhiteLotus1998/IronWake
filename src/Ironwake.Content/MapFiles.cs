using Ironwake.Core;

namespace Ironwake.Content;

/// <summary>Disk access for map files. Parsing is <see cref="MapFormat"/>; this only reads and lists.</summary>
public static class MapFiles
{
    public const string MapsDirectory = "maps";
    public const string Extension = ".map";

    /// <summary>Loads one map file. Errors name the file as given.</summary>
    public static MapDefinition Load(string path, GameContent content)
    {
        if (!File.Exists(path))
        {
            throw new MapException(path, 0, "file not found");
        }

        return MapFormat.Parse(path, File.ReadAllText(path), content);
    }

    /// <summary>
    /// Loads every <c>.map</c> under <c>content/maps</c>, in file name order, keyed by
    /// file name without extension. A missing directory is an empty result, not an error.
    /// </summary>
    public static IReadOnlyList<(string Id, MapDefinition Map)> LoadAll(string contentRoot, GameContent content)
    {
        var dir = Path.Combine(contentRoot, MapsDirectory);
        if (!Directory.Exists(dir))
        {
            return Array.Empty<(string, MapDefinition)>();
        }

        return Directory.GetFiles(dir, "*" + Extension)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => (Path.GetFileNameWithoutExtension(p), Load(p, content)))
            .ToList();
    }
}
