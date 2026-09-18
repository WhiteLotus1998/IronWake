using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// A minimal valid content set for validation tests, plus a way to find the real
/// content directory from the test output folder.
/// </summary>
internal static class Fixture
{
    public const string Terrain = """
        { "terrain": [
          { "id": "plain", "name": "Plain", "glyph": ".", "cost": { "infantry": 1, "cavalry": 1, "flying": 1, "armored": 1 } }
        ] }
        """;

    public const string Classes = """
        { "classes": [
          { "id": "cadet", "name": "Cadet", "movement": "infantry", "mov": 4, "weapons": ["sword"] }
        ] }
        """;

    public const string Weapons = """
        { "weapons": [
          { "id": "iron_sword", "name": "Iron Sword", "type": "sword", "mt": 5, "hit": 90, "crit": 0, "wt": 5, "minRange": 1, "maxRange": 1, "durability": 40 }
        ] }
        """;

    public const string Units = """
        { "units": [
          { "id": "recruit", "name": "Recruit", "class": "cadet", "level": 1,
            "stats": { "hp": 20, "str": 6, "mag": 1, "dex": 5, "spd": 6, "lck": 3, "def": 4, "res": 2, "cha": 5 },
            "growths": { "hp": 50, "str": 40, "mag": 10, "dex": 40, "spd": 45, "lck": 30, "def": 30, "res": 20, "cha": 35 },
            "inventory": [ { "item": "iron_sword" } ] }
        ] }
        """;

    public const string Rules = """
        { "wakeRadius": 4 }
        """;

    public static ContentFiles Files(
        string? terrain = null,
        string? classes = null,
        string? weapons = null,
        string? units = null,
        string? secondUnitsFile = null,
        string? rules = null)
    {
        var unitFiles = new List<ContentFile> { new("units/units.json", units ?? Units) };
        if (secondUnitsFile is not null)
        {
            unitFiles.Add(new ContentFile("units/more.json", secondUnitsFile));
        }

        return new ContentFiles(
            new ContentFile(ContentFiles.ClassesName, classes ?? Classes),
            new ContentFile(ContentFiles.WeaponsName, weapons ?? Weapons),
            new ContentFile(ContentFiles.TerrainName, terrain ?? Terrain),
            unitFiles,
            new ContentFile(ContentFiles.RulesName, rules ?? Rules));
    }

    /// <summary>Walks up from the test binaries to the repository's content directory.</summary>
    public static string RealContentDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "content");
            if (File.Exists(Path.Combine(candidate, ContentFiles.TerrainName)))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("content directory not found above " + AppContext.BaseDirectory);
    }
}
