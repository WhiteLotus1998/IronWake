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

    private static readonly Lazy<string> SallowHome = new(() => WithSallowHome(CopyFiles("ironwake-sallow-home-")));

    /// <summary>
    /// A copy of the real content directory whose Sallow Grange is the <c>goes_home: hall</c> sample
    /// (<c>docs/samples/sallow_grange_home.map</c>, issue 1372) and nothing else changed, so a campaign
    /// replay reads the lever where it would ship. Made once per test run.
    /// </summary>
    public static string SallowHomeContentDirectory() => SallowHome.Value;

    private static string WithSallowHome(string target)
    {
        var root = Directory.GetParent(RealContentDirectory())!.FullName;
        File.Copy(Path.Combine(root, "docs", "samples", "sallow_grange_home.map"), Path.Combine(target, MapFiles.MapsDirectory, "sallow_grange.map"), overwrite: true);
        return target;
    }

    private static readonly Lazy<string> CountingHouseDefSeven = new(() => WithCountingHouseDefSeven(CopyFiles("ironwake-counting-def-seven-")));

    /// <summary>
    /// A copy of the real content directory whose Counting House is the map before issue 1375
    /// (<c>docs/samples/the_counting_house_1375.map</c>: the shared Sworn Captain, Def 7 on the board),
    /// its card without the armour line, and nothing else changed, for a play journaled before the
    /// house captain's Def came down to 5. Made once per test run.
    /// </summary>
    public static string CountingHouseDefSevenContentDirectory() => CountingHouseDefSeven.Value;

    private static string WithCountingHouseDefSeven(string target)
    {
        var root = Directory.GetParent(RealContentDirectory())!.FullName;
        File.Copy(Path.Combine(root, "docs", "samples", "the_counting_house_1375.map"), Path.Combine(target, MapFiles.QuestsDirectory, "the_counting_house.map"), overwrite: true);

        // The card names the armour since issue 1375, and a transcript prints the card.
        var campaignPath = Path.Combine(target, ContentFiles.CampaignName);
        const string card = " The Sworn Captain on the house fort is armoured: leave time for him.";
        var campaign = File.ReadAllText(campaignPath);
        if (!campaign.Contains(card, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{campaignPath}: no '{card.Trim()}' to take back out");
        }

        File.WriteAllText(campaignPath, campaign.Replace(card, string.Empty, StringComparison.Ordinal));
        return target;
    }

    private static readonly Lazy<string> OathRiderOnTurnThree = new(() => WithOathRiderOnTurnThree(CopyFiles("ironwake-oath-rider-three-")));

    /// <summary>
    /// A copy of the real content directory whose Oath Stone sends its rear rider on turn 3, its
    /// card saying so, and nothing else changed, for a play journaled before the rider came with the brigand on turn 5
    /// (issue 940). Made once per test run.
    /// </summary>
    public static string OathRiderOnTurnThreeContentDirectory() => OathRiderOnTurnThree.Value;

    private static string WithOathRiderOnTurnThree(string target)
    {
        var path = Path.Combine(target, MapFiles.QuestsDirectory, "the_oath_stone.map");
        var text = File.ReadAllText(path);
        const string now = "rear1 turn 5 enemy spawn rider";
        if (!text.Contains(now, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{path}: no '{now}' line to put back on turn 3");
        }

        File.WriteAllText(path, text.Replace(now, "rear1 turn 3 enemy spawn rider", StringComparison.Ordinal));

        // The quest card named the turn the pursuers come, and a transcript prints the card.
        var campaignPath = Path.Combine(target, ContentFiles.CampaignName);
        const string card = "More come up behind you from turn 5, announced.";
        var campaign = File.ReadAllText(campaignPath);
        if (!campaign.Contains(card, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{campaignPath}: no '{card}' to put back on turn 3");
        }

        File.WriteAllText(campaignPath, campaign.Replace(card, "More come up behind you from turn 3, announced.", StringComparison.Ordinal));
        return target;
    }

    private static readonly Lazy<string> YardPlaceholder = new(() => WithYardPlaceholder(CopyFiles("ironwake-yard-placeholder-")));

    /// <summary>The yard's one placeholder board, as it shipped before the ring and the post replaced it (issue 1332).</summary>
    public const string YardPlaceholderBoard = """
        name: The Yard (placeholder)
        size: 8x8
        win: rout
        turn_limit: 6
        recall: 0
        enemy_level: 1

        ########
        #......#
        #..^...#
        #......#
        #...^..#
        #......#
        #......#
        ########

        units:
        P captain 2,6
        P recruit 3,6
        E brigand 5,2 group:hands behavior:aggressive
        E soldier 2,1 group:hands behavior:aggressive
        E brigand 6,4 group:post behavior:hold

        """;

    /// <summary>
    /// A copy of the real content directory whose yard pool is the one placeholder board and nothing
    /// else changed, for the drills journaled before the ring and the post replaced it (issue 1332).
    /// Made once per test run.
    /// </summary>
    public static string YardPlaceholderContentDirectory() => YardPlaceholder.Value;

    private static string WithYardPlaceholder(string target)
    {
        var yard = Path.Combine(target, MapFiles.YardDirectory);
        foreach (var board in Directory.GetFiles(yard, "*" + MapFiles.Extension))
        {
            File.Delete(board);
        }

        File.WriteAllText(Path.Combine(yard, "yard_placeholder" + MapFiles.Extension), YardPlaceholderBoard);
        return target;
    }

    private static readonly Lazy<string> ShrineNorthStart = new(() => WithShrineNorthStart(WithShrineArcherHeld(WithShrineSeizedOnTheStep(CopyFiles("ironwake-shrine-north-")))));

    private static readonly Lazy<string> ShrineArcherHeld = new(() => WithShrineArcherHeld(WithShrineSeizedOnTheStep(CopyFiles("ironwake-shrine-held-"))));

    private static readonly Lazy<string> ShrineSeizedOnTheStep = new(() => WithShrineSeizedOnTheStep(CopyFiles("ironwake-shrine-step-")));

    /// <summary>The First Shrine quest card's rules line as shipped with the held altar (issue 1274).</summary>
    private const string ShrineHeldRules = "(Get Maud onto the altar at 7,0 and hold it through one enemy phase by the end of turn 10.";

    /// <summary>
    /// A copy of the real content directory whose First Shrine is won on the step onto the altar, with
    /// no <c>seize_hold:</c> header and the quest card saying so, and nothing else changed, for a play
    /// journaled before the altar was held through an enemy phase (issue 1274). Made once per test run.
    /// </summary>
    public static string ShrineSeizedOnTheStepContentDirectory() => ShrineSeizedOnTheStep.Value;

    private static string WithShrineSeizedOnTheStep(string target)
    {
        var path = Path.Combine(target, MapFiles.QuestsDirectory, "the_first_shrine.map");
        var text = File.ReadAllText(path);
        const string header = "seize_hold: 1\n";
        if (!text.Contains(header, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{path}: no held altar to put back to a seize on the step");
        }

        File.WriteAllText(path, text.Replace(header, "", StringComparison.Ordinal));
        var campaignPath = Path.Combine(target, ContentFiles.CampaignName);
        var campaign = File.ReadAllText(campaignPath);
        if (!campaign.Contains(ShrineHeldRules, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{campaignPath}: no held-altar rules line on the First Shrine card");
        }

        File.WriteAllText(campaignPath, campaign.Replace(ShrineHeldRules, "(Get Maud onto the altar at 7,0 by the end of turn 10.", StringComparison.Ordinal));
        return target;
    }

    /// <summary>
    /// A copy of the real content directory whose First Shrine keeps the sanctum archer on Hold in
    /// the sanctum group, with no <c>wake_on_death:</c> header, and nothing else changed, for a play
    /// journaled before the archer woke on the door soldier's death (issue 1264). Made once per test run.
    /// </summary>
    public static string ShrineArcherHeldContentDirectory() => ShrineArcherHeld.Value;

    private static string WithShrineArcherHeld(string target)
    {
        var path = Path.Combine(target, MapFiles.QuestsDirectory, "the_first_shrine.map");
        var text = File.ReadAllText(path);
        const string header = "wake_on_death: loft by sanctum\n";
        const string now = "E archer 5,1 group:loft behavior:guard\n";
        if (!text.Contains(header, StringComparison.Ordinal) || !text.Contains(now, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{path}: no woken sanctum archer to put back on Hold");
        }

        File.WriteAllText(path, text.Replace(header, "", StringComparison.Ordinal).Replace(now, "E archer 5,1 group:sanctum behavior:hold\n", StringComparison.Ordinal));
        return target;
    }

    /// <summary>
    /// A copy of the real content directory whose First Shrine starts Maud and her ally north of
    /// the water, at 6,5 and 8,5, with the sanctum archer on Hold (<see cref="ShrineArcherHeldContentDirectory"/>),
    /// and nothing else changed, for a play journaled before the start moved south (issue 1198). Made once per test run.
    /// </summary>
    public static string ShrineNorthStartContentDirectory() => ShrineNorthStart.Value;

    private static string WithShrineNorthStart(string target)
    {
        var path = Path.Combine(target, MapFiles.QuestsDirectory, "the_first_shrine.map");
        var text = File.ReadAllText(path);
        const string now = "P captain 4,8\nP recruit 10,8\n";
        if (!text.Contains(now, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{path}: no south start to put back north of the water");
        }

        File.WriteAllText(path, text.Replace(now, "P captain 6,5\nP recruit 8,5\n", StringComparison.Ordinal));
        return target;
    }

    /// <summary>The Lazar House quest card's rules line as shipped with held bars at five turns (issue 1266).</summary>
    private const string LazarHouseHeldRules = "(Hold out five turns. The lanes in are announced. A unit that ends its move on the bar beside a lane, 8,1 north or 10,4 east, closes that lane while it stays there; what the bar blocks waits behind it and comes through, one a turn, once the bar is left. The ford south cannot be barred.)";

    /// <summary>The Lazar House quest card's rules line as it read before held bars (issue 1266).</summary>
    private const string LazarHousePermanentRules = "(Hold out six turns. The lanes in are announced. A unit that ends its move on the bar beside a lane, 8,1 north or 10,4 east, closes that lane for the rest of the map. The ford south cannot be barred.)";

    /// <summary>
    /// Puts the Lazar House in the copy at <paramref name="target"/> back to its permanent bars at six turns,
    /// with no waiting arrivals and no turn-5 archer, its card saying so: the map every play before held bars
    /// shipped was journaled on (issue 1266).
    /// </summary>
    private static string WithLazarHousePermanentBars(string target)
    {
        var path = Path.Combine(target, MapFiles.QuestsDirectory, "the_lazar_house.map");
        var text = File.ReadAllText(path);
        string[] now = { "turn_limit: 5\n", "arrivals: wait\n", "8,0 # held\n", "11,4 # held\n", "east3 turn 5 enemy spawn archer 11,2 group:east behavior:aggressive\n" };
        if (now.Any(line => !text.Contains(line, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"{path}: no held bars to put back to permanent ones");
        }

        File.WriteAllText(path, text
            .Replace(now[0], "turn_limit: 6\n", StringComparison.Ordinal)
            .Replace(now[1], "", StringComparison.Ordinal)
            .Replace(now[2], "8,0 #\n", StringComparison.Ordinal)
            .Replace(now[3], "11,4 #\n", StringComparison.Ordinal)
            .Replace(now[4], "", StringComparison.Ordinal));
        return WithLazarHouseRules(target, LazarHousePermanentRules);
    }

    private static string WithLazarHouseRules(string target, string rules)
    {
        var campaignPath = Path.Combine(target, ContentFiles.CampaignName);
        var campaign = File.ReadAllText(campaignPath);
        if (!campaign.Contains(LazarHouseHeldRules, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{campaignPath}: no held-bar rules line on the Lazar House card");
        }

        File.WriteAllText(campaignPath, campaign.Replace(LazarHouseHeldRules, rules, StringComparison.Ordinal));
        return target;
    }

    private static readonly Lazy<string> LazarHouseHeldSample = new(() => WithLazarHouseHeldSample(CopyFiles("ironwake-lazar-held-sample-")));

    /// <summary>
    /// A copy of the real content directory whose Lazar House is issue 1259's sample, held bars at six turns,
    /// <c>docs/samples/the_lazar_house_held.map</c>, under the card that sample was played under, and nothing
    /// else changed, for the plays journaled on the sample before it shipped at five turns (issue 1266). Made once per test run.
    /// </summary>
    public static string LazarHouseHeldSampleContentDirectory() => LazarHouseHeldSample.Value;

    private static string WithLazarHouseHeldSample(string target)
    {
        var sample = Path.Combine(RealContentDirectory(), "..", "docs", "samples", "the_lazar_house_held.map");
        File.Copy(sample, Path.Combine(target, MapFiles.QuestsDirectory, "the_lazar_house.map"), overwrite: true);
        return WithLazarHouseRules(target, LazarHousePermanentRules);
    }

    private static readonly Lazy<string> FieldUnseen = new(() => WithFieldUnseen(CopyFiles("ironwake-field-unseen-")));

    /// <summary>
    /// A copy of the real content directory whose field carries no <c>seen_far:</c> header, so Rook's
    /// pick is not seated there and nothing else changes, for a play journaled before the drake was seen
    /// (issue 973). Made once per test run.
    /// </summary>
    public static string FieldUnseenContentDirectory() => FieldUnseen.Value;

    private static string WithFieldUnseen(string target)
    {
        var path = Path.Combine(target, "maps", "the_field.map");
        var text = File.ReadAllText(path);
        const string header = "seen_far: rook 2\n";
        if (!text.Contains(header, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{path}: no '{header.TrimEnd()}' line to take off");
        }

        File.WriteAllText(path, text.Replace(header, string.Empty, StringComparison.Ordinal));
        return target;
    }

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
        WithKeep1139(target, campaign);
        File.WriteAllText(campaignPath, campaign.ToJsonString());
        WithoutScenesOffTheCampaign(target);
        return target;
    }

    /// <summary>
    /// The keep as it stood before issue 1149 seated the finale on it, kept as
    /// <c>docs/samples/ironwake_keep_1139.map</c> (survive, two breaches at column 10), with the menu
    /// placed for that wall (walls at 10,3 and 10,8, ditches at 9,5 and 9,6), so a campaign journaled
    /// on the old keep replays on it.
    /// </summary>
    private static void WithKeep1139(string target, System.Text.Json.Nodes.JsonNode campaign)
    {
        var repo = Directory.GetParent(RealContentDirectory())!.FullName;
        File.Copy(
            Path.Combine(repo, "docs", "samples", "ironwake_keep_1139.map"),
            Path.Combine(target, "keep", (string)campaign["keep"]!["map"]! + ".map"),
            overwrite: true);
        foreach (var (edit, tiles) in new[] { ("wall", new[] { "10,3", "10,8" }), ("ditch", new[] { "9,5", "9,6" }) })
        {
            var entry = campaign["keep"]!["edits"]!.AsArray().First(e => (string)e!["id"]! == edit)!;
            entry["at"] = new System.Text.Json.Nodes.JsonArray(tiles.Select(t => (System.Text.Json.Nodes.JsonNode?)t).ToArray());
        }
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

    private static readonly Lazy<string> SaltmarshWakesLate = new(() => WithSaltmarshSample(CopyFiles("ironwake-saltmarsh-wakes-late-"), "saltmarsh_ford_wakes_late.map"));

    /// <summary>
    /// A file-for-file copy of the real content directory with Saltmarsh Ford replaced by
    /// <c>docs/samples/saltmarsh_ford_wakes_late.map</c> (issue 1365, DECISIONS/0339: the pair on the
    /// fort's wake, a phase late), so a campaign journaled on the sample at the floor replays on it.
    /// Made once per test run under the temp directory.
    /// </summary>
    public static string SaltmarshWakesLateContentDirectory() => SaltmarshWakesLate.Value;

    private static readonly Lazy<string> SaltmarshEastPair = new(() => WithSaltmarshSample(CopyFiles("ironwake-saltmarsh-east-pair-"), "saltmarsh_ford_east_pair.map"));

    /// <summary>
    /// A file-for-file copy of the real content directory with Saltmarsh Ford replaced by
    /// <c>docs/samples/saltmarsh_ford_east_pair.map</c> (issue 1370: the pair on the shipped enter line,
    /// spawned on the east edge of the south bank, arrivals waiting), so a campaign journaled on the
    /// sample at the floor replays on it. Made once per test run under the temp directory.
    /// </summary>
    public static string SaltmarshEastPairContentDirectory() => SaltmarshEastPair.Value;

    private static readonly Lazy<string> KeziahQuestOnly = new(() => WithOnlyQuest(CopyFiles("ironwake-keziah-quest-only-"), "keziah_1"));

    /// <summary>
    /// The shipped content with every side map but Keziah's quest 1 (issue 635) cut from <c>campaign.json</c>,
    /// so a campaign opened with <c>--from brackwater_cut</c> offers the Burned Shrine at the camp before map 8
    /// instead of the earlier quests that hold the interlude's two seats (DECISIONS/0278's floor play of it).
    /// </summary>
    public static string KeziahQuestOnlyContentDirectory() => KeziahQuestOnly.Value;

    /// <summary>Cuts every quest but <paramref name="questId"/> from the campaign's side maps.</summary>
    public static string WithOnlyQuest(string target, string questId)
    {
        var campaignPath = Path.Combine(target, ContentFiles.CampaignName);
        var campaign = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(campaignPath))!;
        var kept = campaign["quests"]!.AsArray().Where(q => (string)q!["id"]! == questId).Select(q => q!.DeepClone()).ToArray();
        campaign.AsObject()["quests"] = new System.Text.Json.Nodes.JsonArray(kept);
        File.WriteAllText(campaignPath, campaign.ToJsonString());
        return target;
    }

    private static string WithSaltmarshSample(string target, string sample)
    {
        var repo = Directory.GetParent(RealContentDirectory())!.FullName;
        File.Copy(Path.Combine(repo, "docs", "samples", sample), Path.Combine(target, "maps", "saltmarsh_ford.map"), overwrite: true);
        return target;
    }

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
    /// does the scythe the campaign issues Keziah (issue 804), which no such build issued, and so do
    /// the scenes on either map (issue 1005).
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

        WithoutScenesOffTheCampaign(target);
        return target;
    }

    /// <summary>
    /// Deletes each scene script in the copy at <paramref name="target"/> whose <c>before</c>,
    /// <c>camp</c> or <c>after</c> point names a map its <c>campaign.json</c> no longer lists, so a
    /// copy that cuts the campaign's maps still loads (issue 1005).
    /// </summary>
    private static void WithoutScenesOffTheCampaign(string target)
    {
        var scenes = Path.Combine(target, SceneFormat.Directory);
        if (!Directory.Exists(scenes))
        {
            return;
        }

        var campaign = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(target, ContentFiles.CampaignName)))!;
        var maps = campaign["maps"]!.AsArray().Select(m => (string)m!["map"]!).ToHashSet();
        foreach (var scene in Directory.GetFiles(scenes, "*.txt"))
        {
            var plays = (File.ReadLines(scene).FirstOrDefault(l => l.StartsWith("plays:", StringComparison.Ordinal)) ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (plays.Length == 3 && plays[1] is "before" or "camp" or "after" && !maps.Contains(plays[2]))
            {
                File.Delete(scene);
            }
        }
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
    /// barracks, so a hire's quest goes with the hires (issue 691), and before held bars, so the Lazar House
    /// keeps its permanent bars (issue 1266).
    /// </summary>
    private static string CopyContentAsIs(string prefix) => WithoutTheField(WithLazarHousePermanentBars(CopyFiles(prefix)));

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
            // Issue 1130: nor a seat; Rook joined at the median like any joiner.
            map.Remove("seatLevel");
            map.Remove("seatRank");
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
