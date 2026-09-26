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
          { "id": "iron_sword", "name": "Iron Sword", "type": "sword", "mt": 5, "hit": 90, "crit": 0, "wt": 5, "minRange": 1, "maxRange": 1, "durability": 40, "rank": "E" }
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

    public const string Items = """
        { "items": [
          { "id": "field_dressing", "name": "Field Dressing", "heals": 10, "uses": 3 }
        ] }
        """;

    public const string Abilities = """
        { "abilities": [
          { "id": "vigilance", "name": "Vigilance", "text": "Def +2.", "effect": { "kind": "stats", "stats": { "def": 2 } } }
        ] }
        """;

    public static ContentFiles Files(
        string? terrain = null,
        string? classes = null,
        string? weapons = null,
        string? units = null,
        string? secondUnitsFile = null,
        string? rules = null,
        string? items = null,
        string? cast = null,
        string? abilities = null)
    {
        var unitFiles = new List<ContentFile> { new("units/units.json", units ?? Units) };
        if (cast is not null)
        {
            unitFiles.Add(new ContentFile(ContentFiles.CastName, cast));
        }

        if (secondUnitsFile is not null)
        {
            unitFiles.Add(new ContentFile("units/more.json", secondUnitsFile));
        }

        return new ContentFiles(
            new ContentFile(ContentFiles.ClassesName, classes ?? Classes),
            new ContentFile(ContentFiles.WeaponsName, weapons ?? Weapons),
            new ContentFile(ContentFiles.TerrainName, terrain ?? Terrain),
            unitFiles,
            new ContentFile(ContentFiles.RulesName, rules ?? Rules),
            new ContentFile(ContentFiles.ItemsName, items ?? Items),
            new ContentFile(ContentFiles.AbilitiesName, abilities ?? Abilities));
    }

    private static readonly Lazy<string> LadderFree = new(CopyWithoutLadder);

    /// <summary>
    /// A copy of the real content directory whose classes ask nothing to certify into, for CLI
    /// tests of what certifying does rather than of the shipped ladder (issue 72). Made once per
    /// test run under the temp directory.
    /// </summary>
    public static string LadderFreeContentDirectory() => LadderFree.Value;

    private static readonly Lazy<string> KeepCampaign = new(CopyWithKeepCampaign);

    /// <summary>
    /// A copy of the real content directory whose campaign is only the raid and the keep (issue
    /// 288), in that order, so a scripted campaign reaches the keep's menu and the finale in two
    /// battles. Made once per test run under the temp directory.
    /// </summary>
    public static string KeepCampaignContentDirectory() => KeepCampaign.Value;

    private static string CopyWithKeepCampaign()
    {
        var target = CopyRealContent("ironwake-keep-campaign-");
        var campaignPath = Path.Combine(target, ContentFiles.CampaignName);
        var campaign = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(campaignPath))!;
        var keep = campaign["keep"]!;
        var ids = new[] { (string)keep["raid"]!, (string)keep["map"]! };
        var maps = campaign["maps"]!.AsArray().Where(m => ids.Contains((string)m!["map"]!)).Select(m => m!.DeepClone()).ToArray();
        campaign.AsObject()["maps"] = new System.Text.Json.Nodes.JsonArray(maps);
        File.WriteAllText(campaignPath, campaign.ToJsonString());
        return target;
    }

    private static string CopyRealContent(string prefix)
    {
        var source = RealContentDirectory();
        var target = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var copy = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.Copy(file, copy);
        }

        return target;
    }

    private static string CopyWithoutLadder()
    {
        var target = CopyRealContent("ironwake-ladder-free-");

        var classesPath = Path.Combine(target, ContentFiles.ClassesName);
        var classes = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(classesPath))!;
        foreach (var entry in classes["classes"]!.AsArray())
        {
            entry!.AsObject().Remove("certification");
        }

        File.WriteAllText(classesPath, classes.ToJsonString());
        return target;
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
