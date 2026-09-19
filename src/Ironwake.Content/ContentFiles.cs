namespace Ironwake.Content;

/// <summary>One content file's name (relative to the content root) and text.</summary>
public sealed record ContentFile(string Name, string Text);

/// <summary>
/// The raw text of every content file. <see cref="ContentLoader"/> parses this, and
/// <see cref="ContentSerializer"/> produces it, so round trips never touch the disk.
/// </summary>
public sealed record ContentFiles(
    ContentFile Classes,
    ContentFile Weapons,
    ContentFile Terrain,
    IReadOnlyList<ContentFile> Units,
    ContentFile Rules,
    ContentFile Items)
{
    public const string ItemsName = "items.json";
    public const string ClassesName = "classes.json";
    public const string RulesName = "rules.json";
    public const string WeaponsName = "weapons.json";
    public const string TerrainName = "terrain.json";
    public const string UnitsDirectory = "units";

    /// <summary>The unit file that holds the player's cast, in roster order (issue 13).</summary>
    public const string CastName = UnitsDirectory + "/cast.json";
}
