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
          { "id": "iron_sword", "name": "Iron Sword", "type": "sword", "mt": 5, "hit": 90, "crit": 0, "wt": 5, "minRange": 1, "maxRange": 1, "durability": 40, "description": "A test line.", "rank": "E" }
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
          { "id": "field_dressing", "name": "Field Dressing", "heals": 10, "uses": 3, "description": "A test line." }
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
    /// tests of what certifying does rather than of the shipped ladder (issue 72), its campaign
    /// opening on Old Mill Road as every script written for it was (<see cref="WithoutStartingAlone"/>).
    /// Made once per test run under the temp directory.
    /// </summary>
    public static string LadderFreeContentDirectory() => LadderFree.Value;

    private static readonly Lazy<string> CurveFree = new(() => WithoutCurve(CopyContentAsIs("ironwake-curve-free-")));

    /// <summary>
    /// A copy of the real content directory without the campaign's enemy-level curve and template
    /// swaps (issue 704) and nothing else changed, for a campaign transcript journaled before them. Made once per test run.
    /// </summary>
    public static string CurveFreeContentDirectory() => CurveFree.Value;

    private static readonly Lazy<string> DoublingStrike = new(() => WithoutSingleStrike(CopyContentAsIs("ironwake-doubling-strike-")));

    /// <summary>
    /// A copy of the real content directory whose arts all may double (<see cref="WithoutSingleStrike"/>)
    /// and nothing else changed, for a play journaled while Full Measure still doubled (issue 739). Made once per test run.
    /// </summary>
    public static string DoublingStrikeContentDirectory() => DoublingStrike.Value;

    /// <summary>
    /// Takes <c>single</c> off every art in <c>abilities.json</c> (issue 739): a play journaled before
    /// an art struck once doubled with it, and a transcript is a record of the build it was played on.
    /// </summary>
    public static string WithoutSingleStrike(string target)
    {
        var abilitiesPath = Path.Combine(target, "abilities.json");
        var abilities = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(abilitiesPath))!;
        foreach (var entry in abilities["abilities"]!.AsArray())
        {
            if (entry!["effect"] is System.Text.Json.Nodes.JsonObject effect)
            {
                effect.Remove("single");
            }
        }

        File.WriteAllText(abilitiesPath, abilities.ToJsonString());
        return target;
    }

    private static readonly Lazy<string> KeepCampaign = new(CopyWithKeepCampaign);

    /// <summary>
    /// A copy of the real content directory whose campaign is only the raid and the keep (issue
    /// 288), in that order, so a scripted campaign reaches the keep's menu and the finale in two
    /// battles, with no side map on offer (issue 635), as when its script was journaled. Made once
    /// per test run under the temp directory.
    /// </summary>
    public static string KeepCampaignContentDirectory() => KeepCampaign.Value;

    private static string CopyWithKeepCampaign()
    {
        var target = WithoutCurve(CopyRealContent("ironwake-keep-campaign-"));
        var campaignPath = Path.Combine(target, ContentFiles.CampaignName);
        var campaign = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(campaignPath))!;
        var keep = campaign["keep"]!;
        var ids = new[] { (string)keep["raid"]!, (string)keep["map"]! };
        var maps = campaign["maps"]!.AsArray().Where(m => ids.Contains((string)m!["map"]!)).Select(m => m!.DeepClone()).ToArray();
        campaign.AsObject()["maps"] = new System.Text.Json.Nodes.JsonArray(maps);
        campaign.AsObject().Remove("quests");
        campaign.AsObject().Remove("issues");
        File.WriteAllText(campaignPath, campaign.ToJsonString());
        return target;
    }

    private static readonly Lazy<string> Saltmarsh0030 = new(() => WithSaltmarsh0030(WithoutStartingAlone(CopyRealContent("ironwake-saltmarsh-0030-"))));

    private static readonly Lazy<string> LadderFreeSaltmarsh0030 = new(() => WithSaltmarsh0030(CopyWithoutLadder()));

    /// <summary>
    /// A copy of the real content directory with Saltmarsh Ford as it stood under DECISIONS/0030,
    /// before issue 131's timing arm, kept as <c>docs/samples/saltmarsh_ford_0030.map</c>, so a
    /// campaign journaled on that file replays on it, the cast knowing no art as then (issue 611). Made once per test run under the temp directory.
    /// </summary>
    public static string Saltmarsh0030ContentDirectory() => Saltmarsh0030.Value;

    /// <summary>
    /// <see cref="LadderFreeContentDirectory"/> with Saltmarsh Ford as it stood under DECISIONS/0030,
    /// as <see cref="Saltmarsh0030ContentDirectory"/> says.
    /// </summary>
    public static string LadderFreeSaltmarsh0030ContentDirectory() => LadderFreeSaltmarsh0030.Value;

    private static string WithSaltmarsh0030(string target)
    {
        var repo = Directory.GetParent(RealContentDirectory())!.FullName;
        File.Copy(
            Path.Combine(repo, "docs", "samples", "saltmarsh_ford_0030.map"),
            Path.Combine(target, "maps", "saltmarsh_ford.map"),
            overwrite: true);
        WithoutCastArts(target);
        WithoutAdvancedForms(target);
        return target;
    }

    /// <summary>
    /// Takes every advanced form (issue 704) and the captain's ladder (issue 705) out of <c>classes.json</c>,
    /// and every enemy template in a form out of <c>units/enemies.json</c>: the class list a campaign journaled
    /// before the second tier prints has none, its captain certified like anyone, its dark reached as far
    /// as the first tier's classes do, and its maps were fought without the curve (<see cref="WithoutCurve"/>).
    /// </summary>
    public static string WithoutAdvancedForms(string target)
    {
        var classesPath = Path.Combine(target, ContentFiles.ClassesName);
        var classes = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(classesPath))!;
        var ladder = classes["classes"]!.AsArray();
        var forms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var advanced in ladder.Where(e => e!.AsObject().ContainsKey("advances") || e!.AsObject().ContainsKey("captain")).ToList())
        {
            forms.Add((string)advanced!["id"]!);
            ladder.Remove(advanced);
        }

        File.WriteAllText(classesPath, classes.ToJsonString());

        var enemiesPath = Path.Combine(target, "units", "enemies.json");
        var enemies = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(enemiesPath))!;
        var templates = enemies["units"]!.AsArray();
        foreach (var promoted in templates.Where(e => forms.Contains((string)e!["class"]!)).ToList())
        {
            templates.Remove(promoted);
        }

        File.WriteAllText(enemiesPath, enemies.ToJsonString());
        return WithoutCurve(target);
    }

    /// <summary>
    /// Takes the campaign's enemy-level curve and template swaps (issue 704) out of <c>campaign.json</c>:
    /// a campaign journaled before them fought every map at its file's level and placements.
    /// </summary>
    public static string WithoutCurve(string target)
    {
        var campaignPath = Path.Combine(target, ContentFiles.CampaignName);
        var campaign = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(campaignPath))!;
        foreach (var map in campaign["maps"]!.AsArray())
        {
            map!.AsObject().Remove("enemyLevel");
            map.AsObject().Remove("swap");
        }

        File.WriteAllText(campaignPath, campaign.ToJsonString());
        return target;
    }

    /// <summary>
    /// Takes every combat art out of the cast's <c>abilities</c> (issue 611): a campaign journaled
    /// before the cast knew any art writes each unit's abilities empty, and a transcript is a
    /// record of the build it was played on.
    /// </summary>
    public static string WithoutCastArts(string target)
    {
        var castPath = Path.Combine(target, "units", "cast.json");
        var cast = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(castPath))!;
        foreach (var entry in cast["units"]!.AsArray())
        {
            entry!.AsObject().Remove("abilities");
        }

        File.WriteAllText(castPath, cast.ToJsonString());
        return target;
    }

    /// <summary>
    /// Takes Starting Alone (issue 631) out of the campaign's maps and puts Old Mill Road back in
    /// The Mill's place with nobody arriving (issue 632): a campaign journaled before the story
    /// order opens on Old Mill Road as map 1 with the whole cast, and its battle seeds count from there.
    /// The side maps (issue 635) go too, since no such script was journaled with one on offer, and so
    /// does the scythe the campaign issues Keziah (issue 804), which no such build issued.
    /// </summary>
    public static string WithoutStartingAlone(string target)
    {
        var campaignPath = Path.Combine(target, ContentFiles.CampaignName);
        var campaign = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(campaignPath))!;
        var maps = campaign["maps"]!.AsArray().Where(m => (string)m!["map"]! != "starting_alone").Select(m => m!.DeepClone()).ToArray();
        foreach (var map in maps.Select(m => m!.AsObject()))
        {
            map.Remove("arrives");
            if ((string)map["map"]! == "the_mill")
            {
                map["map"] = "old_mill_road";
                map.Remove("before");
                map.Remove("after");
            }
        }

        campaign.AsObject()["maps"] = new System.Text.Json.Nodes.JsonArray(maps);
        campaign.AsObject().Remove("quests");
        campaign.AsObject().Remove("issues");
        File.WriteAllText(campaignPath, campaign.ToJsonString());
        return target;
    }

    private static readonly Lazy<string> BeforeSecondTier = new(() => WithoutAdvancedForms(CopyRealContent("ironwake-before-second-tier-")));

    /// <summary>
    /// A copy of the real content directory without the second tier (issue 704): no advanced form
    /// and no enemy template in one, so the dark reaches 8 as it did when Brackwater Cut's dusk plays
    /// were journaled. Made once per test run under the temp directory.
    /// </summary>
    public static string BeforeSecondTierContentDirectory() => BeforeSecondTier.Value;

    private static readonly Lazy<string> Roomless = new(() => CopyRealContent("ironwake-roomless-"));

    /// <summary>
    /// A copy of the real content directory with the keep's beds and rooms taken out (issue 687):
    /// a campaign journaled before the keep sold rooms prints no rooms line, and a transcript is a
    /// record of the build it was played on. Made once per test run under the temp directory.
    /// </summary>
    public static string RoomlessContentDirectory() => Roomless.Value;

    /// <summary>
    /// A copy of the real content directory under the temp directory. Every copy stands for a build
    /// some transcript was journaled on, all of them before the keep sold rooms, so the keep's
    /// <c>beds</c> and <c>rooms</c> are taken out (issue 687), and before the forge, so the
    /// <c>forge</c> and the quests' material payouts are taken out too (issue 647), and before the
    /// barracks, so a hire's quest goes with the hires (issue 691).
    /// </summary>
    private static string CopyContentAsIs(string prefix) => WithoutTheField(CopyFiles(prefix));

    /// <summary>A file-for-file copy of the real content directory under the temp directory, nothing taken out.</summary>
    private static string CopyFiles(string prefix)
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

    /// <summary>
    /// Takes The Field Before the Keep (issue 81) out of the campaign's maps, and puts the branch
    /// (issue 633) back as Rook joining at the raid's camp. Every copy stands for a
    /// build journaled before it was map 9, and a trial's and a side map's seed count the campaign's
    /// maps (<see cref="CampaignRecord.TrialSeed"/>,
    /// <see cref="CampaignRecord.QuestSeed"/>), so a script journaled on nine
    /// maps replays only on nine. Pell's quests 1 and 2 (issue 635 slices 6 and 7) go too, and
    /// with them her art Read Ahead, and Ottilie's art Paid in Full (slice 10): every copy stands
    /// for a build journaled before them, when no camp listed them and no card printed the art.
    /// </summary>
    public static string WithoutTheField(string target)
    {
        var campaignPath = Path.Combine(target, ContentFiles.CampaignName);
        var campaign = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(campaignPath))!;
        var maps = campaign["maps"]!.AsArray().Where(m => (string)m!["map"]! != "the_field").Select(m => m!.DeepClone()).ToArray();
        foreach (var map in maps.OfType<System.Text.Json.Nodes.JsonObject>().Where(m => m.ContainsKey("branch")))
        {
            // Issue 633: the same builds had no branch; Rook joined at the raid's camp and Keziah was on the roster from map 1.
            map.Remove("branch");
            map.Remove("pitch");
            map["joins"] = new System.Text.Json.Nodes.JsonArray("rook");
        }

        campaign.AsObject()["maps"] = new System.Text.Json.Nodes.JsonArray(maps);
        // Issue 804: the same builds issued Keziah no scythe; she fought with her iron axe.
        campaign.AsObject().Remove("issues");
        if (campaign["quests"] is System.Text.Json.Nodes.JsonArray quests)
        {
            campaign.AsObject()["quests"] = new System.Text.Json.Nodes.JsonArray(quests.Where(q => (string)q!["id"]! is not ("pell_1" or "pell_2")).Select(q => q!.DeepClone()).ToArray());
        }

        File.WriteAllText(campaignPath, campaign.ToJsonString());

        var castPath = Path.Combine(target, "units", "cast.json");
        var cast = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(castPath))!;
        foreach (var unit in cast["units"]!.AsArray().OfType<System.Text.Json.Nodes.JsonObject>().Where(u => (string)u["id"]! is "pell" or "ottilie" or "teodor"))
        {
            unit["abilities"] = new System.Text.Json.Nodes.JsonArray(unit["abilities"]!.AsArray().Where(a => (string)a! is not ("read_ahead" or "paid_in_full" or "turn_the_key")).Select(a => a!.DeepClone()).ToArray());
        }

        File.WriteAllText(castPath, cast.ToJsonString());
        return target;
    }

    private static string CopyRealContent(string prefix)
    {
        var target = CopyContentAsIs(prefix);

        var campaignPath = Path.Combine(target, ContentFiles.CampaignName);
        var campaign = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(campaignPath))!;
        if (campaign["keep"] is System.Text.Json.Nodes.JsonObject keep)
        {
            keep.Remove("beds");
            keep.Remove("rooms");
            keep.Remove("hires");
            keep.Remove("hirePrice");
        }

        campaign.AsObject().Remove("forge");
        if (campaign["quests"] is System.Text.Json.Nodes.JsonArray quests)
        {
            foreach (var quest in quests.OfType<System.Text.Json.Nodes.JsonObject>())
            {
                quest.Remove("common");
                quest.Remove("rare");
            }

            foreach (var hired in quests.OfType<System.Text.Json.Nodes.JsonObject>().Where(q => q.ContainsKey("opensAfter")).ToList())
            {
                quests.Remove(hired);
            }

            // A part 2 whose part 1 went with the held slots goes too (Ottilie's, issue 635 slice 10).
            var firsts = quests.OfType<System.Text.Json.Nodes.JsonObject>().Where(q => (int)q["part"]! == 1).Select(q => (string)q["member"]!).ToHashSet();
            foreach (var orphan in quests.OfType<System.Text.Json.Nodes.JsonObject>().Where(q => (int)q["part"]! == 2 && !firsts.Contains((string)q["member"]!)).ToList())
            {
                quests.Remove(orphan);
            }
        }

        File.WriteAllText(campaignPath, campaign.ToJsonString());

        return target;
    }

    private static string CopyWithoutLadder()
    {
        var target = WithoutAdvancedForms(WithoutStartingAlone(CopyRealContent("ironwake-ladder-free-")));

        var classesPath = Path.Combine(target, ContentFiles.ClassesName);
        var classes = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(classesPath))!;
        foreach (var entry in classes["classes"]!.AsArray())
        {
            entry!.AsObject().Remove("certification");
        }

        File.WriteAllText(classesPath, classes.ToJsonString());
        return target;
    }

    private static readonly Lazy<string> BeforeWardensGate = new(() => WithoutQuest(CopyFiles("ironwake-before-wardens-gate-"), "teodor_2"));

    /// <summary>
    /// A copy of the real content directory without Teodor's quest 2 (issue 635 slice 12). A camp
    /// that has won Teodor's quest 1 offers it ahead of a quest opened later, so a side map journaled
    /// at such a camp before it existed replays only without it. Made once per test run under the
    /// temp directory.
    /// </summary>
    public static string BeforeWardensGateContentDirectory() => BeforeWardensGate.Value;

    /// <summary>Takes the side map <paramref name="questId"/> out of the campaign's quests in the copy at <paramref name="target"/>.</summary>
    public static string WithoutQuest(string target, string questId)
    {
        var campaignPath = Path.Combine(target, ContentFiles.CampaignName);
        var campaign = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(campaignPath))!;
        var quests = campaign["quests"]!.AsArray().Where(q => (string)q!["id"]! != questId).Select(q => q!.DeepClone()).ToArray();
        campaign.AsObject()["quests"] = new System.Text.Json.Nodes.JsonArray(quests);
        File.WriteAllText(campaignPath, campaign.ToJsonString());
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
