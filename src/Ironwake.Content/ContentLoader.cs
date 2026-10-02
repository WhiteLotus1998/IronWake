using System.Collections.Immutable;
using System.Text.Json;
using Ironwake.Core;

namespace Ironwake.Content;

/// <summary>
/// Reads the content directory into a validated <see cref="GameContent"/>. Every failure
/// is a <see cref="ContentException"/> naming file, entry, and field. Validation order
/// is fixed: terrain, abilities, classes, weapons, units, then cross-references, so the
/// first error reported is deterministic.
/// </summary>
public static class ContentLoader
{
    private static readonly string[] StatKeys = { "hp", "str", "mag", "dex", "spd", "lck", "def", "res", "cha" };

    private static readonly string[] MovementKeys = { "infantry", "cavalry", "flying", "armored" };

    /// <summary>Loads from a directory laid out as described in CLAUDE.md under content/.</summary>
    public static GameContent Load(string contentRoot)
    {
        if (!Directory.Exists(contentRoot))
        {
            throw new ContentException(contentRoot, null, null, "content directory does not exist");
        }

        var unitsDir = Path.Combine(contentRoot, ContentFiles.UnitsDirectory);
        var unitFiles = Directory.Exists(unitsDir)
            ? Directory.GetFiles(unitsDir, "*.json").OrderBy(p => p, StringComparer.Ordinal).ToList()
            : new List<string>();

        var files = new ContentFiles(
            ReadFile(contentRoot, ContentFiles.ClassesName),
            ReadFile(contentRoot, ContentFiles.WeaponsName),
            ReadFile(contentRoot, ContentFiles.TerrainName),
            unitFiles.Select(p => new ContentFile(
                ContentFiles.UnitsDirectory + "/" + Path.GetFileName(p), File.ReadAllText(p))).ToList(),
            ReadFile(contentRoot, ContentFiles.RulesName),
            ReadFile(contentRoot, ContentFiles.ItemsName),
            ReadFile(contentRoot, ContentFiles.AbilitiesName),
            File.Exists(Path.Combine(contentRoot, ContentFiles.CampaignName)) ? ReadFile(contentRoot, ContentFiles.CampaignName) : null);

        return Parse(files);
    }

    /// <summary>Parses already-read file text. This is the whole loader; <see cref="Load"/> only adds disk access.</summary>
    public static GameContent Parse(ContentFiles files)
    {
        var terrain = ParseTerrain(files.Terrain);
        var abilities = ParseAbilities(files.Abilities);
        var classes = ParseClasses(files.Classes, abilities);
        var weapons = ParseWeapons(files.Weapons);
        var items = ParseItems(files.Items, weapons);
        var (units, cast, signatures, pronouns) = ParseUnits(files.Units, classes, weapons, items, abilities);
        ValidateSignatureItems(files, weapons, abilities, cast);
        var (wakeRadius, rivalry, difficulties) = ParseRules(files.Rules);
        var campaign = files.Campaign is { } campaignFile ? ParseCampaign(campaignFile, weapons, items, classes, terrain, cast) : CampaignRules.None;
        var content = new GameContent(classes, weapons, terrain, units, items, wakeRadius) { Cast = cast, Rivalry = rivalry, Abilities = abilities, Difficulties = difficulties, Campaign = campaign, Signatures = signatures, Pronouns = pronouns };
        if (files.Campaign is { } campaignText && Forge.RareRefusal(content) is { } rare)
        {
            throw new ContentException(campaignText.Name, "quests", "rare", rare);
        }

        foreach (var map in campaign.Maps)
        {
            foreach (var swap in map.Swaps)
            {
                if (!units.TryGetValue(swap.TemplateId, out var template))
                {
                    throw new ContentException(files.Campaign!.Name, map.MapId, "swap", $"'{swap.TemplateId}' is not a unit template");
                }

                if (cast.Any(u => u.Id == template.Id))
                {
                    throw new ContentException(files.Campaign!.Name, map.MapId, "swap", $"'{swap.TemplateId}' is in the cast, not an enemy template");
                }
            }
        }

        return content;
    }

    /// <summary>
    /// Signature items and arts (issue 635): a weapon's <c>boundTo</c> names a cast member and the
    /// weapon carries no <c>price</c>, since a bound item is never sold or repaired; an art's
    /// <c>item</c> names a weapon of the art's own type.
    /// </summary>
    private static void ValidateSignatureItems(
        ContentFiles files, ImmutableSortedDictionary<string, Weapon> weapons, ImmutableSortedDictionary<string, Ability> abilities, IReadOnlyList<Unit> cast)
    {
        foreach (var weapon in weapons.Values.Where(w => w.BoundTo is not null))
        {
            if (!cast.Any(u => u.Id == weapon.BoundTo))
            {
                throw new ContentException(files.Weapons.Name, weapon.Id, "boundTo", $"'{weapon.BoundTo}' is not in the cast");
            }

            if (weapon.Price is not null)
            {
                throw new ContentException(files.Weapons.Name, weapon.Id, "price", "a signature item is bound to its owner and is never sold");
            }
        }

        foreach (var ability in abilities.Values)
        {
            if (ability.Effect is CombatArtEffect { Item: { } item } art
                && !(weapons.TryGetValue(item, out var weapon) && weapon.Type == art.Weapon))
            {
                throw new ContentException(files.Abilities.Name, ability.Id, "effect.item", $"'{item}' must be a {art.Weapon.ToString().ToLowerInvariant()} in weapons.json");
            }
        }
    }

    /// <summary>
    /// <c>campaign.json</c> (issue 74): <c>startingPurse</c> and <c>certificationPrice</c>, each at
    /// least 0, and <c>maps</c>, at least one, each a <c>map</c> id (unique), a <c>reward</c> of at
    /// least 0 and a <c>stock</c> of weapon or item ids, each carrying a <c>price</c> and none
    /// listed twice. The map ids are checked against <c>content/maps</c> by whoever loads maps.
    /// The optional <c>trials</c> object (issue 252) maps a class id to a trial map id under
    /// <c>content/trials</c>; an unknown class or an empty map id is refused, and whoever loads the
    /// trial checks that its <c>certification:</c> header names the same class. A map's optional
    /// <c>before</c> and <c>after</c> are its text cards (issue 631), read by <see cref="Card"/>.
    /// A map's optional <c>arrives</c> (issue 632) lists the cast ids who join on it; a unit not in
    /// the cast, the captain (the cast's first, there from the start) and a unit arriving twice are
    /// refused. Whoever loads the map checks that it places each arrival by name.
    /// A map's optional <c>enemyLevel</c> (issue 704) is the level the campaign fights it at, 1 to the
    /// level cap, in place of the file's; its optional <c>swap</c> object maps an <c>x,y</c> tile to an
    /// enemy template id the campaign fields there instead (a cast member is refused); whoever loads the
    /// map checks that the tile holds an enemy placement (<see cref="CampaignMap.Prepare"/>).
    /// The optional <c>quests</c> array (issue 635) lists the side maps: each an <c>id</c> (unique),
    /// a <c>member</c> in the cast and not the captain, a <c>part</c> of 1 or 2 (one of each per
    /// member at most, part 2 only after that member's part 1 in the file), a <c>map</c> id under
    /// <c>content/quests</c>, optional <c>before</c> and <c>after</c> cards, and on a part 2 an optional
    /// <c>pays</c>, a weapon bound to the member (<see cref="Weapon.BoundTo"/>). Whoever loads the
    /// map checks its slots (<see cref="CampaignRecord.QuestMapRefusal"/>). A quest's optional
    /// <c>common</c> and <c>rare</c> (issue 647) are the material a win pays, each at least 0; the rare
    /// in all quests is held to exactly what the issued signatures need (<see cref="Forge.RareRefusal"/>).
    /// A quest's optional <c>opensAfter</c> (issue 691) names the main map after which it opens, and
    /// lets its <c>member</c> be a <c>keep.hires</c> id; <c>promotes</c> names the hidden class a win
    /// puts the member in; <c>ending</c>, a hire's only, replaces their served line.
    /// The optional <c>forge</c> object (issue 647) is <see cref="ForgeRules"/>: <c>mt</c>, <c>hit</c>,
    /// <c>price</c>, <c>common</c> and <c>rare</c> steps, each at least 1; a forge room needs it.
    /// </summary>
    private static CampaignRules ParseCampaign(
        ContentFile file, ImmutableSortedDictionary<string, Weapon> weapons, ImmutableSortedDictionary<string, Item> items,
        ImmutableSortedDictionary<string, UnitClass> classes, ImmutableSortedDictionary<string, Terrain> terrain, IReadOnlyList<Unit> castUnits)
    {
        var cast = castUnits.Select(u => u.Id).ToList();
        var captain = castUnits.Count > 0 ? castUnits[0] : null;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(file.Text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException e)
        {
            throw new ContentException(file.Name, null, null, "invalid JSON: " + e.Message);
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ContentException(file.Name, null, null, "root must be an object with startingPurse, certificationPrice and maps");
        }

        var root = new EntryNode(file.Name, null, document.RootElement);
        var purse = root.Int("startingPurse");
        if (purse < 0)
        {
            throw root.Error("startingPurse", "must be at least 0");
        }

        var seal = root.Int("certificationPrice");
        if (seal < 0)
        {
            throw root.Error("certificationPrice", "must be at least 0");
        }

        var advancedSeal = root.IntOr("advancedCertificationPrice", seal);
        if (advancedSeal < 0)
        {
            throw root.Error("advancedCertificationPrice", "must be at least 0");
        }

        var maps = new List<CampaignMap>();
        var index = 0;
        foreach (var element in root.Array("maps"))
        {
            var node = new EntryNode(file.Name, "maps[" + index + "]", element);
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw node.Error(null, "must be an object");
            }

            var mapId = node.String("map");
            node = node.WithEntry(mapId);
            if (maps.Any(m => m.MapId == mapId))
            {
                throw node.Error("map", "is listed twice");
            }

            var reward = node.Int("reward");
            if (reward < 0)
            {
                throw node.Error("reward", "must be at least 0");
            }

            var stock = node.StringArray("stock");
            foreach (var id in stock)
            {
                int? price = weapons.TryGetValue(id, out var weapon) ? weapon.Price
                    : items.TryGetValue(id, out var item) ? item.Price
                    : throw node.Error("stock", $"'{id}' is neither a weapon nor an item");
                if (price is null)
                {
                    throw node.Error("stock", $"'{id}' has no price, so no shop can sell it");
                }
            }

            if (stock.Distinct().Count() != stock.Count)
            {
                throw node.Error("stock", "must not list an id twice");
            }

            var arrives = node.StringArrayOrEmpty("arrives");
            foreach (var id in arrives)
            {
                if (!cast.Contains(id))
                {
                    throw node.Error("arrives", $"'{id}' is not in the cast");
                }

                if (cast.Count > 0 && cast[0] == id)
                {
                    throw node.Error("arrives", $"'{id}' is the captain, who leads from the first map");
                }

                if (arrives.Count(a => a == id) > 1 || maps.Any(m => m.Arrives.Contains(id)))
                {
                    throw node.Error("arrives", $"'{id}' arrives on more than one map");
                }
            }

            int? enemyLevel = null;
            if (node.Has("enemyLevel"))
            {
                enemyLevel = node.Int("enemyLevel");
                if (enemyLevel < Unit.MinLevel || enemyLevel > Unit.MaxLevel)
                {
                    throw node.Error("enemyLevel", $"must be between {Unit.MinLevel} and {Unit.MaxLevel}");
                }
            }

            var swaps = new List<TemplateSwap>();
            if (node.OptionalObject("swap") is { } swapNode)
            {
                foreach (var property in swapNode.Element.EnumerateObject())
                {
                    var parts = property.Name.Split(',');
                    if (parts.Length != 2 || !int.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var x)
                        || !int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var y))
                    {
                        throw node.Error("swap", $"'{property.Name}' is not a tile; expected x,y");
                    }

                    if (property.Value.ValueKind != JsonValueKind.String || property.Value.GetString() is not { Length: > 0 } template)
                    {
                        throw node.Error("swap", $"'{property.Name}' must name a template id");
                    }

                    swaps.Add(new TemplateSwap(new Coord(x, y), template));
                }
            }

            maps.Add(new CampaignMap(mapId, reward, ValueList<string>.From(stock))
            {
                Before = Card(node, "before"),
                After = Card(node, "after"),
                Arrives = ValueList<string>.From(arrives),
                EnemyLevel = enemyLevel,
                Swaps = ValueList<TemplateSwap>.From(swaps),
            });
            index++;
        }

        if (maps.Count == 0)
        {
            throw root.Error("maps", "must list at least one map");
        }

        var trials = new List<CampaignTrial>();
        if (root.OptionalObject("trials") is { } trialNode)
        {
            foreach (var property in trialNode.Element.EnumerateObject())
            {
                if (!classes.ContainsKey(property.Name))
                {
                    throw root.Error("trials." + property.Name, "is not a class");
                }

                if (property.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.Value.GetString()))
                {
                    throw root.Error("trials." + property.Name, "must be a trial map id");
                }

                trials.Add(new CampaignTrial(property.Name, property.Value.GetString()!));
            }
        }

        var hireIds = root.OptionalObject("keep") is { } hiresNode
            ? hiresNode.ArrayOrEmpty("hires").Where(h => h.ValueKind == JsonValueKind.Object && h.TryGetProperty("id", out var hid) && hid.ValueKind == JsonValueKind.String).Select(h => h.GetProperty("id").GetString()!).ToList()
            : new List<string>();
        var quests = new List<CampaignQuest>();
        var questIndex = 0;
        foreach (var element in root.ArrayOrEmpty("quests"))
        {
            var node = new EntryNode(file.Name, "quests[" + questIndex + "]", element);
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw node.Error(null, "must be an object");
            }

            var id = node.String("id");
            node = node.WithEntry(id);
            if (quests.Any(q => q.Id == id))
            {
                throw node.Error("id", "is listed twice");
            }

            var member = node.String("member");
            var opensAfter = node.OptionalString("opensAfter");
            if (opensAfter is not null && !maps.Any(m => m.MapId == opensAfter))
            {
                throw node.Error("opensAfter", $"'{opensAfter}' is not a map of the campaign");
            }

            if (!cast.Contains(member) && !(opensAfter is not null && hireIds.Contains(member)))
            {
                throw node.Error("member", hireIds.Contains(member)
                    ? $"'{member}' is a hire, whose quest names the map it opensAfter"
                    : $"'{member}' is not in the cast");
            }

            if (cast[0] == member)
            {
                throw node.Error("member", $"'{member}' is the captain, whose quest is not a side map of this shape");
            }

            var part = node.Int("part");
            if (part is not (1 or 2))
            {
                throw node.Error("part", "must be 1 or 2");
            }

            if (quests.Any(q => q.MemberId == member && q.Part == part))
            {
                throw node.Error("part", $"'{member}' has a quest {part} already");
            }

            if (part == 2 && !quests.Any(q => q.MemberId == member && q.Part == 1))
            {
                throw node.Error("part", $"'{member}' has no quest 1 listed before this quest 2");
            }

            var map = node.String("map");
            if (string.IsNullOrWhiteSpace(map))
            {
                throw node.Error("map", "must be a side map id");
            }

            string? pays = null;
            if (node.Has("pays"))
            {
                pays = node.String("pays");
                if (part != 2)
                {
                    throw node.Error("pays", "only a quest 2 pays the signature item");
                }

                if (!weapons.TryGetValue(pays, out var item) || item.BoundTo != member)
                {
                    throw node.Error("pays", $"'{pays}' must be a weapon bound to '{member}'");
                }
            }

            var common = node.IntOr("common", 0);
            var rare = node.IntOr("rare", 0);
            if (common < 0 || rare < 0)
            {
                throw node.Error(common < 0 ? "common" : "rare", "must be at least 0");
            }

            var promotes = node.OptionalString("promotes");
            if (promotes is not null && !(classes.TryGetValue(promotes, out var promoted) && promoted.Hidden))
            {
                throw node.Error("promotes", $"'{promotes}' must be a hidden class, the only kind a side map puts a member in");
            }

            var ending = node.OptionalString("ending");
            if (ending is not null && !hireIds.Contains(member))
            {
                throw node.Error("ending", $"'{member}' is not a hire; only a hire's ending is one line");
            }

            if (ending is not null && string.IsNullOrWhiteSpace(ending))
            {
                throw node.Error("ending", "must be a line, or left out");
            }

            quests.Add(new CampaignQuest(id, member, part, map)
            {
                Before = Card(node, "before"),
                After = Card(node, "after"),
                Pays = pays,
                Common = common,
                Rare = rare,
                OpensAfter = opensAfter,
                Promotes = promotes,
                Ending = ending,
            });
            questIndex++;
        }

        var keep = root.OptionalObject("keep") is { } keepNode ? ParseKeep(keepNode, terrain, classes, weapons, items, castUnits) : KeepMenu.None;
        var ids = maps.Select(m => m.MapId).ToList();
        if (keep.RaidId.Length > 0 && ids.Contains(keep.MapId) && !(ids.IndexOf(keep.RaidId) is var raidAt && raidAt >= 0 && raidAt < ids.IndexOf(keep.MapId)))
        {
            throw root.Error("keep.raid", $"'{keep.RaidId}' must be listed in maps before the keep '{keep.MapId}', since the keep's menu opens after the raid");
        }

        if (keep.Beds > 0 && keep.Beds < cast.Count)
        {
            throw root.Error("keep.beds", $"must be at least {cast.Count}, the starting roster and every arrival, so a map's own arrival always has a bed");
        }

        var forge = ForgeRules.None;
        if (root.OptionalObject("forge") is { } forgeNode)
        {
            forge = new ForgeRules(forgeNode.Int("mt"), forgeNode.Int("hit"), forgeNode.Int("price"), forgeNode.Int("common"), forgeNode.Int("rare"));
            foreach (var (field, value) in new[] { ("mt", forge.Mt), ("hit", forge.Hit), ("price", forge.Price), ("common", forge.CommonSteps), ("rare", forge.RareSteps) })
            {
                if (value < 1)
                {
                    throw forgeNode.Error(field, "must be at least 1");
                }
            }
        }

        foreach (var room in keep.Rooms)
        {
            if (room.Forge && forge == ForgeRules.None)
            {
                throw root.Error("keep.rooms." + room.Id + ".forge", "needs the campaign's forge object, which holds Refine's numbers");
            }

            if (room.After.Length > 0 && !ids.Contains(room.After))
            {
                throw root.Error("keep.rooms." + room.Id + ".after", $"'{room.After}' is not a campaign map");
            }
        }

        return new CampaignRules(purse, seal, ValueList<CampaignMap>.From(maps))
        {
            AdvancedCertificationPrice = advancedSeal,
            Trials = ValueList<CampaignTrial>.From(trials.OrderBy(t => t.ClassId, StringComparer.Ordinal)),
            Quests = ValueList<CampaignQuest>.From(quests),
            Keep = keep,
            Forge = forge,
            Origins = ParseOrigins(root, captain),
        };
    }

    /// <summary>
    /// The optional <c>origins</c> array (issue 681): the captain's origins, each an <c>id</c>
    /// (unique), a <c>name</c>, and <c>stats</c> and <c>growths</c> as deltas on the cast's captain,
    /// keyed as a cast entry's are. Each delta set sums to 0, no stat of the captain's card may fall
    /// below 0, and no growth may leave 0 to 100.
    /// </summary>
    private static ValueList<CaptainOrigin> ParseOrigins(EntryNode root, Unit? captain)
    {
        var origins = new List<CaptainOrigin>();
        var index = 0;
        foreach (var element in root.ArrayOrEmpty("origins"))
        {
            var node = new EntryNode(root.File, "origins[" + index + "]", element);
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw node.Error(null, "must be an object");
            }

            var id = node.String("id");
            node = node.WithEntry(id);
            if (origins.Any(o => o.Id == id))
            {
                throw node.Error("id", "is listed twice");
            }

            if (captain is null)
            {
                throw node.Error("id", "an origin needs a cast, whose first entry is the captain it changes");
            }

            var name = node.String("name");
            var stats = ParseStats(node.Object("stats"), allRequired: false);
            var growths = ParseStats(node.Object("growths"), allRequired: false);
            foreach (var (field, deltas) in new[] { ("stats", stats), ("growths", growths) })
            {
                if (CaptainOrigin.Sum(deltas) != 0)
                {
                    throw node.Error(field, $"sums to {CaptainOrigin.Sum(deltas)}; an origin moves the captain's card, it does not grow it, so each set sums to 0");
                }
            }

            foreach (var stat in Stats.All)
            {
                var key = StatKeys[(int)stat];
                if (captain.Stats.Get(stat) + stats.Get(stat) < 0)
                {
                    throw node.Error("stats." + key, $"takes the captain's {key} to {captain.Stats.Get(stat) + stats.Get(stat)}; a stat is at least 0");
                }

                if (captain.Growths.Get(stat) + growths.Get(stat) is var growth && (growth < 0 || growth > 100))
                {
                    throw node.Error("growths." + key, $"takes the captain's {key} growth to {growth}; a growth is 0 to 100");
                }
            }

            origins.Add(new CaptainOrigin(id, name, stats, growths));
            index++;
        }

        return ValueList<CaptainOrigin>.From(origins);
    }

    /// <summary>
    /// The optional <c>keep</c> object (issue 82): the keep's <c>map</c> id under <c>content/keep</c>
    /// and its <c>edits</c>, each an <c>id</c>, <c>name</c>, <c>terrain</c> id, <c>price</c> of at least 1
    /// and a non-empty <c>at</c> list of <c>x,y</c> tiles; and an optional <c>raid</c>, the raid's map id
    /// under <c>content/keep</c> (issue 288). Whoever loads the map checks the tiles against it.
    /// Optional <c>beds</c>, at least 1 and at least the cast the campaign seats without a choice,
    /// and <c>rooms</c>, each an <c>id</c>, <c>name</c>, <c>price</c>, <c>beds</c> and <c>max</c> of
    /// at least 1 (issue 687); rooms need beds. A room's optional <c>forge</c> (issue 647) marks the forge, one at most, whose
    /// <c>beds</c> may be 0 or absent; its optional <c>after</c> names the campaign map after whose win it may be built.
    /// </summary>
    /// <summary>
    /// A campaign map's optional text card (issue 631): an array of paragraphs, each a non-blank
    /// line of printable ASCII (the console is the screen), at most <see cref="CardParagraphLength"/>
    /// characters, and at most <see cref="CardParagraphs"/> of them. An absent field is an empty card.
    /// </summary>
    private static ValueList<string> Card(EntryNode node, string field)
    {
        var paragraphs = node.StringArrayOrEmpty(field);
        if (node.Has(field) && paragraphs.Count == 0)
        {
            throw node.Error(field, "must hold at least one paragraph, or be left out");
        }

        if (paragraphs.Count > CardParagraphs)
        {
            throw node.Error(field, $"holds {paragraphs.Count} paragraphs; a card holds at most {CardParagraphs}");
        }

        for (var i = 0; i < paragraphs.Count; i++)
        {
            var text = paragraphs[i];
            if (string.IsNullOrWhiteSpace(text) || text != text.Trim())
            {
                throw node.Error($"{field}[{i}]", "must be non-blank text with no leading or trailing space");
            }

            if (text.Any(c => c < ' ' || c > '~'))
            {
                throw node.Error($"{field}[{i}]", "must be printable ASCII on one line");
            }

            if (text.Length > CardParagraphLength)
            {
                throw node.Error($"{field}[{i}]", $"is {text.Length} characters; a paragraph holds at most {CardParagraphLength}");
            }
        }

        return ValueList<string>.From(paragraphs);
    }

    /// <summary>The most paragraphs a text card holds (issue 631; the console is the screen).</summary>
    public const int CardParagraphs = 6;

    /// <summary>The longest paragraph a text card holds, in characters (issue 631).</summary>
    public const int CardParagraphLength = 400;

    private static KeepMenu ParseKeep(
        EntryNode node, ImmutableSortedDictionary<string, Terrain> terrain, ImmutableSortedDictionary<string, UnitClass> classes,
        ImmutableSortedDictionary<string, Weapon> weapons, ImmutableSortedDictionary<string, Item> items, IReadOnlyList<Unit> cast)
    {
        var mapId = node.String("map");
        var edits = new List<KeepEdit>();
        var index = 0;
        foreach (var element in node.Array("edits"))
        {
            var entry = new EntryNode(node.File, "keep.edits[" + index++ + "]", element);
            var id = entry.String("id");
            entry = entry.WithEntry("keep." + id);
            if (edits.Any(e => e.Id == id))
            {
                throw entry.Error("id", "is listed twice");
            }

            var terrainId = entry.String("terrain");
            if (!terrain.ContainsKey(terrainId))
            {
                throw entry.Error("terrain", $"'{terrainId}' is not a terrain");
            }

            var price = entry.Int("price");
            if (price < 1)
            {
                throw entry.Error("price", "must be at least 1");
            }

            var tiles = new List<Coord>();
            foreach (var text in entry.StringArray("at"))
            {
                var parts = text.Split(',');
                if (parts.Length != 2 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y) || x < 0 || y < 0)
                {
                    throw entry.Error("at", $"'{text}' is not an x,y tile");
                }

                tiles.Add(new Coord(x, y));
            }

            if (tiles.Count == 0)
            {
                throw entry.Error("at", "must name at least one tile");
            }

            edits.Add(new KeepEdit(id, entry.String("name"), terrainId, price, ValueList<Coord>.From(tiles)));
        }

        var raid = node.OptionalString("raid") ?? "";
        if (raid == mapId)
        {
            throw node.Error("raid", "must name a map other than the keep");
        }

        var beds = node.IntOr("beds", 0);
        if (node.Has("beds") && beds < 1)
        {
            throw node.Error("beds", "must be at least 1");
        }

        var rooms = new List<KeepRoom>();
        index = 0;
        foreach (var element in node.ArrayOrEmpty("rooms"))
        {
            var entry = new EntryNode(node.File, "keep.rooms[" + index++ + "]", element);
            var id = entry.String("id");
            entry = entry.WithEntry("keep.rooms." + id);
            if (rooms.Any(r => r.Id == id) || edits.Any(e => e.Id == id))
            {
                throw entry.Error("id", "is listed twice");
            }

            var isForge = entry.BoolOr("forge", false);
            var room = new KeepRoom(id, entry.String("name"), entry.Int("price"), isForge ? entry.IntOr("beds", 0) : entry.Int("beds"), entry.Int("max"))
            {
                Forge = isForge,
                After = entry.Has("after") ? entry.String("after") : "",
                Requires = entry.Has("requires") ? entry.String("requires") : "",
                Hires = ValueList<string>.From(entry.StringArrayOrEmpty("hires")),
            };
            foreach (var (field, value) in new[] { ("price", room.Price), ("beds", room.Beds), ("max", room.Max) })
            {
                if (value < (field == "beds" && isForge ? 0 : 1))
                {
                    throw entry.Error(field, isForge && field == "beds" ? "must be at least 0" : "must be at least 1");
                }
            }

            if (isForge && rooms.Any(r => r.Forge))
            {
                throw entry.Error("forge", "the keep has one forge");
            }

            rooms.Add(room);
        }

        if (rooms.Count > 0 && beds == 0)
        {
            throw node.Error("rooms", "needs the keep's beds, since a room adds beds");
        }

        foreach (var room in rooms.Where(r => r.Requires.Length > 0 && (r.Requires == r.Id || rooms.All(o => o.Id != r.Requires))))
        {
            throw node.Error("rooms." + room.Id + ".requires", $"'{room.Requires}' is not another room of the keep");
        }

        var hires = ParseHires(node, classes, weapons, items, cast);
        foreach (var room in rooms)
        {
            foreach (var hireId in room.Hires)
            {
                if (hires.All(h => h.Id != hireId))
                {
                    throw node.Error("rooms." + room.Id + ".hires", $"'{hireId}' is not in the keep's hires");
                }

                if (rooms.Count(r => r.Hires.Contains(hireId)) > 1 || room.Hires.Count(id => id == hireId) > 1)
                {
                    throw node.Error("rooms." + room.Id + ".hires", $"'{hireId}' is listed by more than one room or twice");
                }
            }
        }

        foreach (var hire in hires.Where(h => rooms.All(r => !r.Hires.Contains(h.Id))))
        {
            throw node.Error("hires." + hire.Id, "no room lists this hire, so nobody could hire them");
        }

        var hirePrice = node.IntOr("hirePrice", 0);
        if (hires.Count > 0 && hirePrice < 1)
        {
            throw node.Error("hirePrice", "must be at least 1 when the keep has hires");
        }

        return new KeepMenu(mapId, ValueList<KeepEdit>.From(edits))
        {
            RaidId = raid,
            Beds = beds,
            Rooms = ValueList<KeepRoom>.From(rooms),
            Hires = ValueList<KeepHire>.From(hires),
            HirePrice = hirePrice,
        };
    }

    /// <summary>
    /// The keep's optional <c>hires</c> (issue 690): each an <c>id</c> (unique, not a cast member's),
    /// a <c>name</c>, a <c>pronoun</c>, a base <c>class</c> that some cast member other than the
    /// captain holds (a hire's card is read from theirs), one to four <c>items</c>, each a rank E
    /// weapon the class uses or an item, and one <c>line</c> for the hire menu.
    /// </summary>
    private static List<KeepHire> ParseHires(
        EntryNode node, ImmutableSortedDictionary<string, UnitClass> classes, ImmutableSortedDictionary<string, Weapon> weapons,
        ImmutableSortedDictionary<string, Item> items, IReadOnlyList<Unit> cast)
    {
        var hires = new List<KeepHire>();
        var index = 0;
        foreach (var element in node.ArrayOrEmpty("hires"))
        {
            var entry = new EntryNode(node.File, "keep.hires[" + index++ + "]", element);
            var id = entry.String("id");
            entry = entry.WithEntry("keep.hires." + id);
            if (hires.Any(h => h.Id == id) || cast.Any(u => u.Id == id))
            {
                throw entry.Error("id", "is listed twice or is a cast member's id");
            }

            var pronoun = entry.Enum<Pronoun>("pronoun");
            var classId = entry.String("class");
            if (!classes.TryGetValue(classId, out var unitClass))
            {
                throw entry.Error("class", $"unknown class '{classId}'");
            }

            if (!cast.Skip(1).Any(u => u.ClassId == classId))
            {
                throw entry.Error("class", $"no cast member but the captain is a {classId}, and a hire's card is read from theirs");
            }

            var held = entry.StringArray("items");
            if (held.Count is < 1 or > Inventory.Capacity)
            {
                throw entry.Error("items", $"must name 1 to {Inventory.Capacity} items");
            }

            foreach (var itemId in held)
            {
                if (weapons.TryGetValue(itemId, out var weapon))
                {
                    if (!unitClass.CanUse(weapon.Type) || weapon.Rank != WeaponRank.E)
                    {
                        throw entry.Error("items", $"'{itemId}' must be a rank E weapon a {classId} uses");
                    }
                }
                else if (!items.ContainsKey(itemId))
                {
                    throw entry.Error("items", $"unknown item '{itemId}': not a weapon in weapons.json or an item in items.json");
                }
            }

            var line = entry.String("line");
            if (line.Trim().Length == 0)
            {
                throw entry.Error("line", "must not be empty");
            }

            hires.Add(new KeepHire(id, entry.String("name"), pronoun, classId, ValueList<string>.From(held), line));
        }

        return hires;
    }

    /// <summary>An optional <c>price</c> (issue 74): at least 1 when present, null when absent.</summary>
    private static int? Price(EntryNode node)
    {
        if (!node.Has("price"))
        {
            return null;
        }

        var price = node.Int("price");
        return price >= 1 ? price : throw node.Error("price", "must be at least 1");
    }

    /// <summary>
    /// The abilities of issue 66: an id, a name, one line of <c>text</c>, and an <c>effect</c>
    /// whose <c>kind</c> picks one of the closed set. <c>stats</c> is a passive flat delta,
    /// its <c>stats</c> object a partial stat block with at least one non-zero value;
    /// <c>combat</c> is an on-combat modifier of <c>hit</c>, <c>avoid</c>, <c>crit</c> and
    /// <c>critAvoid</c> (each optional, at least one non-zero), applied against opponents
    /// matching the optional <c>against</c> object's <c>weapon</c> and <c>movement</c> and
    /// only while the holder fights with the optional <c>wielding</c> weapon type (issue 245);
    /// <c>art</c> is a combat art (issue 68): a <c>weapon</c> type, the <c>rank</c> it needs,
    /// its extra <c>cost</c> in uses (at least 1), and optional <c>mt</c>, <c>hit</c>,
    /// <c>crit</c>, <c>wt</c> and <c>range</c> deltas, at least one non-zero and
    /// <c>range</c> never negative; <c>canto</c> (issue 71) reads nothing but its kind.
    /// </summary>
    private static ImmutableSortedDictionary<string, Ability> ParseAbilities(ContentFile file)
    {
        var entries = Entries(file, "abilities");
        RequireUnique(file, entries);
        var builder = ImmutableSortedDictionary.CreateBuilder<string, Ability>(StringComparer.Ordinal);
        foreach (var node in entries)
        {
            var text = node.String("text");
            if (string.IsNullOrWhiteSpace(text))
            {
                throw node.Error("text", "must be one line of text");
            }

            builder.Add(node.Entry!, new Ability(node.Entry!, node.String("name"), text, ParseEffect(node, node.Object("effect"))));
        }

        return builder.ToImmutable();
    }

    private static AbilityEffect ParseEffect(EntryNode entry, EntryNode effect)
    {
        var kind = effect.String("kind");
        switch (kind)
        {
            case "stats":
                RequireOnly(entry, effect, "effect", "kind", "stats");
                var delta = ParseStats(effect.Object("stats"), allRequired: false);
                if (delta == Stats.Zero)
                {
                    throw entry.Error("effect.stats", "must change at least one stat");
                }

                return new StatDeltaEffect(delta);
            case "combat":
                RequireOnly(entry, effect, "effect", "kind", "against", "wielding", "hit", "avoid", "crit", "critAvoid");
                var against = OpponentCondition.Any;
                if (effect.OptionalObject("against") is { } condition)
                {
                    RequireOnly(entry, condition, "effect.against", "weapon", "movement", "oathbound");
                    WeaponType? weapon = condition.Has("weapon") ? entry.ParseEnum<WeaponType>("effect.against.weapon", condition.String("weapon")) : null;
                    MovementType? movement = condition.Has("movement") ? entry.ParseEnum<MovementType>("effect.against.movement", condition.String("movement")) : null;
                    var oathbound = condition.BoolOr("oathbound", false);
                    if (weapon is null && movement is null && !oathbound)
                    {
                        throw entry.Error("effect.against", "must name a weapon, a movement or oathbound: true; leave it out to match every opponent");
                    }

                    against = new OpponentCondition(weapon, movement) { Oathbound = oathbound };
                }

                WeaponType? wielding = effect.Has("wielding") ? entry.ParseEnum<WeaponType>("effect.wielding", effect.String("wielding")) : null;
                var modifier = new CombatModifierEffect(against, effect.IntOr("hit", 0), effect.IntOr("avoid", 0), effect.IntOr("crit", 0), effect.IntOr("critAvoid", 0))
                {
                    Wielding = wielding,
                };
                if (modifier.Hit == 0 && modifier.Avoid == 0 && modifier.Crit == 0 && modifier.CritAvoid == 0)
                {
                    throw entry.Error("effect", "a combat effect must change hit, avoid, crit or critAvoid");
                }

                return modifier;
            case "art":
                RequireOnly(entry, effect, "effect", "kind", "weapon", "rank", "cost", "mt", "hit", "crit", "wt", "range", "perMap", "costsNextPhase", "item");
                var art = new CombatArtEffect(
                    entry.ParseEnum<WeaponType>("effect.weapon", effect.String("weapon")),
                    entry.ParseEnum<WeaponRank>("effect.rank", effect.String("rank")),
                    effect.Int("cost"),
                    effect.IntOr("mt", 0),
                    effect.IntOr("hit", 0),
                    effect.IntOr("crit", 0),
                    effect.IntOr("wt", 0),
                    effect.IntOr("range", 0))
                {
                    PerMap = effect.Has("perMap") ? effect.Int("perMap") : null,
                    CostsNextPhase = effect.BoolOr("costsNextPhase", false),
                    Item = effect.Has("item") ? effect.String("item") : null,
                };
                if (art.PerMap is < 1)
                {
                    throw entry.Error("effect.perMap", "must be at least 1, or left out for no cap");
                }

                if (art.Cost < 1)
                {
                    throw entry.Error("effect.cost", "must be at least 1: an art free on a miss is the plain attack with better numbers");
                }

                if (art.Range < 0)
                {
                    throw entry.Error("effect.range", "must not be negative");
                }

                if (art.Mt == 0 && art.Hit == 0 && art.Crit == 0 && art.Wt == 0 && art.Range == 0)
                {
                    throw entry.Error("effect", "an art must change mt, hit, crit, wt or range");
                }

                return art;
            case "canto":
                RequireOnly(entry, effect, "effect", "kind");
                return new CantoEffect();
            case "brace":
                RequireOnly(entry, effect, "effect", "kind");
                return new BraceEffect();
            case "range":
                RequireOnly(entry, effect, "effect", "kind", "weapon", "heals", "range");
                var heals = effect.BoolOr("heals", false);
                if (heals == effect.Has("weapon"))
                {
                    throw entry.Error("effect", "a range effect names a weapon type or heals: true, not both");
                }

                var reach = new RangeEffect(heals ? null : entry.ParseEnum<WeaponType>("effect.weapon", effect.String("weapon")), heals, effect.Int("range"));
                if (reach.Range < 1)
                {
                    throw entry.Error("effect.range", "must be at least 1");
                }

                return reach;
            case "killheal":
                RequireOnly(entry, effect, "effect", "kind", "heal", "wielding");
                var killHeal = new KillHealEffect(effect.Int("heal"), effect.Has("wielding") ? entry.ParseEnum<WeaponType>("effect.wielding", effect.String("wielding")) : null);
                if (killHeal.Heal < 1)
                {
                    throw entry.Error("effect.heal", "must be at least 1");
                }

                return killHeal;
            default:
                throw entry.Error("effect.kind", $"unknown kind '{kind}'; expected stats, combat, art, canto, brace, range or killheal");
        }
    }

    /// <summary>Refuses a key the effect's kind does not read, so a misspelt modifier fails instead of doing nothing.</summary>
    private static void RequireOnly(EntryNode entry, EntryNode node, string prefix, params string[] keys)
    {
        foreach (var property in node.Element.EnumerateObject())
        {
            if (System.Array.IndexOf(keys, property.Name) < 0)
            {
                throw entry.Error(prefix + "." + property.Name, "is not read here; expected one of " + string.Join(", ", keys));
            }
        }
    }

    /// <summary>A list of ability ids: each must be in abilities.json, none twice.</summary>
    private static ValueList<string> AbilityIds(EntryNode node, string field, ImmutableSortedDictionary<string, Ability> abilities)
    {
        var ids = node.StringArrayOrEmpty(field);
        for (var i = 0; i < ids.Count; i++)
        {
            if (!abilities.ContainsKey(ids[i]))
            {
                throw node.Error($"{field}[{i}]", $"unknown ability '{ids[i]}': not in {ContentFiles.AbilitiesName}");
            }

            if (ids.Take(i).Contains(ids[i], StringComparer.Ordinal))
            {
                throw node.Error($"{field}[{i}]", $"'{ids[i]}' is listed twice");
            }
        }

        return ValueList<string>.From(ids);
    }

    /// <summary>The consumables of DESIGN.md section 5 (issue 9): each heals its user and has a number of uses. An id shared with a weapon is refused, since an inventory entry names either.</summary>
    /// <summary>
    /// An item's <c>description</c> (issue 650): required on every weapon, spell and consumable, one
    /// line of at most <see cref="DescriptionMax"/> characters with no line break, read after the
    /// entry's other fields so an earlier field's error is the one named.
    /// </summary>
    /// <summary>
    /// A weapon's <c>heirloom</c> ladder (issue 646): <c>fromMap</c> (at least 1), <c>first</c> (the
    /// first stage's id), and <c>stages</c>, each with an id, the combats it turns at (rising, from
    /// 1), Mt, hit, crit, weight and a description. Only a physical weapon with no price and an
    /// owner (<c>boundTo</c>) carries one; every id is distinct.
    /// </summary>
    private static HeirloomLadder ReadHeirloom(EntryNode weapon, bool magicOrHeals)
    {
        if (magicOrHeals || weapon.Has("price") || !weapon.Has("boundTo") || weapon.BoolOr("hungers", false))
        {
            throw weapon.Error("heirloom", "an heirloom is a physical weapon bound to its owner, with no price and no hunger");
        }

        var node = weapon.Object("heirloom");
        var fromMap = node.Int("fromMap");
        if (fromMap < 1)
        {
            throw weapon.Error("heirloom.fromMap", "must be at least 1");
        }

        var first = node.String("first");
        var ids = new HashSet<string>(StringComparer.Ordinal) { first };
        var turns = new List<WeaponStage>();
        var stages = node.Array("stages");
        if (stages.Count == 0)
        {
            throw weapon.Error("heirloom.stages", "must name at least one stage");
        }

        for (var i = 0; i < stages.Count; i++)
        {
            var field = $"heirloom.stages[{i}]";
            var stage = new EntryNode(weapon.File, $"{weapon.Entry}.{field}", stages[i]);
            var id = stage.String("id");
            if (!ids.Add(id))
            {
                throw weapon.Error(field + ".id", $"'{id}' names a stage twice");
            }

            var at = stage.Int("at");
            if (at < 1 || (turns.Count > 0 && at <= turns[^1].At))
            {
                throw weapon.Error(field + ".at", "must be at least 1 and above the stage before it");
            }

            var mt = stage.Int("mt");
            var hit = stage.Int("hit");
            var crit = stage.Int("crit");
            var wt = stage.Int("wt");
            if (mt < 0 || hit < 0 || hit > 200 || crit < 0 || crit > 100 || wt < 0)
            {
                throw weapon.Error(field, "mt and wt must be at least 0, hit 0..200, crit 0..100");
            }

            turns.Add(new WeaponStage(id, mt, hit, crit, wt, at, Description(stage)));
        }

        return new HeirloomLadder(fromMap, first, ValueList<WeaponStage>.From(turns));
    }

    private static string Description(EntryNode node)
    {
        var text = node.String("description");
        if (text.Contains('\n') || text.Contains('\r'))
        {
            throw node.Error("description", "must be one line");
        }

        if (text.Length > DescriptionMax)
        {
            throw node.Error("description", $"must be at most {DescriptionMax} characters, not {text.Length}");
        }

        return text;
    }

    /// <summary>The longest <c>description</c> the validator accepts: one console line beside the card's label.</summary>
    public const int DescriptionMax = 72;

    private static ImmutableSortedDictionary<string, Item> ParseItems(ContentFile file, ImmutableSortedDictionary<string, Weapon> weapons)
    {
        var entries = Entries(file, "items");
        var builder = ImmutableSortedDictionary.CreateBuilder<string, Item>(StringComparer.Ordinal);
        foreach (var node in entries)
        {
            if (weapons.ContainsKey(node.Entry!))
            {
                throw node.Error("id", $"'{node.Entry}' is already a weapon");
            }

            var heals = node.Int("heals");
            if (heals < 1)
            {
                throw node.Error("heals", "must be at least 1");
            }

            var uses = node.Int("uses");
            if (uses < 1)
            {
                throw node.Error("uses", "must be at least 1");
            }

            builder.Add(node.Entry!, new Item(node.Entry!, node.String("name"), heals, uses, Price(node)) { Description = Description(node) });
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// The global rule constants of DESIGN.md: today only the Guard wake radius (section 8),
    /// which lives in content so it is identical on every map and never in a map file.
    /// </summary>
    private static (int WakeRadius, RivalryRules Rivalry, ImmutableSortedDictionary<string, Difficulty> Difficulties) ParseRules(ContentFile file)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(file.Text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException e)
        {
            throw new ContentException(file.Name, null, null, "invalid JSON: " + e.Message);
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ContentException(file.Name, null, null, "root must be an object");
        }

        var root = new EntryNode(file.Name, null, document.RootElement);
        var wakeRadius = root.Int("wakeRadius");
        if (wakeRadius < 0)
        {
            throw root.Error("wakeRadius", "must be at least 0");
        }

        var rivalryRules = root.OptionalObject("rivalry") is { } rivalry ? ParseRivalry(rivalry) : RivalryRules.None;
        var difficulties = root.OptionalObject("difficulties") is { } block
            ? ParseDifficulties(block)
            : ImmutableSortedDictionary<string, Difficulty>.Empty.WithComparers(StringComparer.Ordinal);
        return (wakeRadius, rivalryRules, difficulties);
    }

    /// <summary>
    /// The difficulties block of rules.json (issue 76): an object of difficulty id to an
    /// optional <c>statPercent</c> (stat keys, each at least 0, an omitted stat 100), an optional
    /// <c>enemyLevelOffset</c> (0 when omitted), and an optional <c>recall</c> charge count (0 to
    /// 99; omitted keeps each map's) or <c>recallOffset</c> (added to each map's, issue 664), an
    /// optional display <c>name</c> and an optional <c>unlockedBy</c> naming another difficulty. The block must declare <c>normal</c>, and <c>normal</c>
    /// must be the identity, so Normal is data and never a code path of its own.
    /// </summary>
    private static ImmutableSortedDictionary<string, Difficulty> ParseDifficulties(EntryNode node)
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<string, Difficulty>(StringComparer.Ordinal);
        foreach (var property in node.Element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                throw new ContentException(node.File, "difficulties", property.Name, "must be an object");
            }

            var entry = new EntryNode(node.File, "difficulties." + property.Name, property.Value);
            foreach (var field in entry.Element.EnumerateObject())
            {
                if (field.Name is not ("statPercent" or "enemyLevelOffset" or "recall" or "recallOffset" or "name" or "unlockedBy" or "tier"))
                {
                    throw entry.Error(field.Name, "is not a difficulty field; expected statPercent, enemyLevelOffset, recall, recallOffset, name, unlockedBy or tier");
                }
            }

            var percent = entry.OptionalObject("statPercent") is { } p ? ParseStats(p, allRequired: false, fallback: 100) : Difficulty.FullPercent;
            foreach (var stat in Stats.All)
            {
                if (percent.Get(stat) < 0)
                {
                    throw entry.Error("statPercent." + StatKeys[(int)stat], "must be at least 0");
                }
            }

            var offset = entry.IntOr("enemyLevelOffset", 0);
            if (Math.Abs(offset) >= Unit.MaxLevel)
            {
                throw entry.Error("enemyLevelOffset", $"must be between {1 - Unit.MaxLevel} and {Unit.MaxLevel - 1}");
            }

            int? recall = entry.Has("recall") ? entry.Int("recall") : null;
            if (recall is < 0 or > 99)
            {
                throw entry.Error("recall", "must be 0 to 99");
            }

            var recallOffset = entry.IntOr("recallOffset", 0);
            if (recallOffset is < -99 or > 99)
            {
                throw entry.Error("recallOffset", "must be -99 to 99");
            }

            if (recall is not null && entry.Has("recallOffset"))
            {
                throw entry.Error("recallOffset", "cannot stand beside recall; a difficulty names a count or an offset");
            }

            var name = entry.OptionalString("name");
            if (name is not null && (name.Length is 0 or > 24 || name.Any(c => c is < ' ' or > '~')))
            {
                throw entry.Error("name", "must be 1 to 24 printable ASCII characters");
            }

            var tier = entry.IntOr("tier", 0);
            if (tier is < -9 or > 9)
            {
                throw entry.Error("tier", "must be -9 to 9");
            }

            builder.Add(property.Name, new Difficulty(property.Name, percent, offset, recall) { RecallOffset = recallOffset, Name = name, UnlockedBy = entry.OptionalString("unlockedBy"), Tier = tier });
        }

        foreach (var difficulty in builder.Values)
        {
            if (difficulty.UnlockedBy is { } needed && (!builder.ContainsKey(needed) || needed == difficulty.Id))
            {
                throw new ContentException(node.File, "difficulties." + difficulty.Id, "unlockedBy", $"names '{needed}', which is not another difficulty");
            }
        }

        if (!builder.TryGetValue(Difficulty.NormalId, out var normal))
        {
            throw node.Error("difficulties", $"must declare '{Difficulty.NormalId}'");
        }

        if (!normal.IsIdentity)
        {
            throw new ContentException(node.File, "difficulties." + Difficulty.NormalId, null, "must be the identity: every percent 100, offset 0, no recall or recallOffset");
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// The rivalry block of rules.json (issue 16): <c>arms</c>, an object of arm id to
    /// <c>hit</c>, <c>crit</c>, <c>critAvoid</c> and optional <c>countersOnly</c>; <c>rapportRate</c>,
    /// steps of <c>cha</c> and <c>rate</c> with the first at Cha 0 and Cha strictly rising;
    /// and <c>overwriteAt</c>, at least 1.
    /// </summary>
    private static RivalryRules ParseRivalry(EntryNode node)
    {
        var armsNode = node.Object("arms");
        var arms = new List<RivalryArm>();
        foreach (var property in armsNode.Element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                throw new ContentException(node.File, "rivalry.arms", property.Name, "must be an object");
            }

            var arm = new EntryNode(node.File, "rivalry.arms." + property.Name, property.Value);
            arms.Add(new RivalryArm(property.Name, arm.Int("hit"), arm.Int("crit"), arm.Int("critAvoid"), arm.BoolOr("countersOnly", false)));
        }

        if (arms.Count == 0)
        {
            throw node.Error("rivalry.arms", "must name at least one arm");
        }

        var steps = new List<RapportStep>();
        var rates = node.Array("rapportRate");
        for (var i = 0; i < rates.Count; i++)
        {
            if (rates[i].ValueKind != JsonValueKind.Object)
            {
                throw node.Error($"rivalry.rapportRate[{i}]", "must be an object");
            }

            var step = new EntryNode(node.File, $"rivalry.rapportRate[{i}]", rates[i]);
            var cha = step.Int("cha");
            var rate = step.Int("rate");
            if (i == 0 && cha != 0)
            {
                throw step.Error("cha", "the first step must be at Cha 0");
            }

            if (i > 0 && cha <= steps[i - 1].Cha)
            {
                throw step.Error("cha", $"must rise; {cha} follows {steps[i - 1].Cha}");
            }

            if (rate < 0)
            {
                throw step.Error("rate", "must be at least 0");
            }

            steps.Add(new RapportStep(cha, rate));
        }

        if (steps.Count == 0)
        {
            throw node.Error("rivalry.rapportRate", "must have at least one step");
        }

        var overwriteAt = node.Int("overwriteAt");
        if (overwriteAt < 1)
        {
            throw node.Error("rivalry.overwriteAt", "must be at least 1");
        }

        return new RivalryRules(ValueList<RivalryArm>.From(arms), ValueList<RapportStep>.From(steps), overwriteAt);
    }

    private static ContentFile ReadFile(string root, string name)
    {
        var path = Path.Combine(root, name);
        if (!File.Exists(path))
        {
            throw new ContentException(name, null, null, "file not found under " + root);
        }

        return new ContentFile(name, File.ReadAllText(path));
    }

    private static List<EntryNode> Entries(ContentFile file, string key)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(file.Text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException e)
        {
            throw new ContentException(file.Name, null, null, "invalid JSON: " + e.Message);
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ContentException(file.Name, null, null, "root must be an object with a '" + key + "' array");
        }

        var root = new EntryNode(file.Name, null, document.RootElement);
        var entries = new List<EntryNode>();
        var index = 0;
        foreach (var element in root.Array(key))
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new ContentException(file.Name, key + "[" + index + "]", null, "must be an object");
            }

            var node = new EntryNode(file.Name, key + "[" + index + "]", element);
            entries.Add(node.WithEntry(node.String("id")));
            index++;
        }

        return entries;
    }

    private static void RequireUnique(ContentFile file, IEnumerable<EntryNode> entries)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in entries)
        {
            if (!seen.Add(node.Entry!))
            {
                throw new ContentException(file.Name, node.Entry, "id", "duplicate id");
            }
        }
    }

    private static Stats ParseStats(EntryNode node, bool allRequired, int fallback = 0)
    {
        var values = new int[StatKeys.Length];
        for (var i = 0; i < StatKeys.Length; i++)
        {
            values[i] = allRequired ? node.Int(StatKeys[i]) : node.IntOr(StatKeys[i], fallback);
        }

        foreach (var property in node.Element.EnumerateObject())
        {
            if (System.Array.IndexOf(StatKeys, property.Name) < 0)
            {
                throw node.Error(property.Name, "is not a stat; expected one of " + string.Join(", ", StatKeys));
            }
        }

        return new Stats(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8]);
    }

    private static ImmutableSortedDictionary<string, Terrain> ParseTerrain(ContentFile file)
    {
        var entries = Entries(file, "terrain");
        RequireUnique(file, entries);
        var builder = ImmutableSortedDictionary.CreateBuilder<string, Terrain>(StringComparer.Ordinal);
        var glyphs = new Dictionary<char, string>();
        foreach (var node in entries)
        {
            var glyphText = node.String("glyph");
            if (glyphText.Length != 1)
            {
                throw node.Error("glyph", "must be exactly one character");
            }

            var glyph = glyphText[0];
            if (glyphs.TryGetValue(glyph, out var other))
            {
                throw node.Error("glyph", $"'{glyph}' is already used by terrain '{other}'");
            }

            glyphs[glyph] = node.Entry!;

            var costNode = node.Object("cost");
            var costs = new int?[MovementKeys.Length];
            for (var i = 0; i < MovementKeys.Length; i++)
            {
                var cost = costNode.NullableInt(MovementKeys[i]);
                if (cost is < 1)
                {
                    throw costNode.Error("cost." + MovementKeys[i], "must be at least 1, or null for impassable");
                }

                costs[i] = cost;
            }

            var heal = node.IntOr("heal", 0);
            if (heal < 0 || heal > 100)
            {
                throw node.Error("heal", "must be 0..100 (percent of max HP)");
            }

            var burn = node.IntOr("burn", 0);
            if (burn < 0 || burn > 100)
            {
                throw node.Error("burn", "must be 0..100 (percent of max HP)");
            }

            if (burn > 0 && heal > 0)
            {
                throw node.Error("burn", "a tile that heals cannot also burn");
            }

            var avoid = node.IntOr("avoid", 0);
            if (avoid < 0)
            {
                throw node.Error("avoid", "must be at least 0");
            }

            builder.Add(node.Entry!, new Terrain(
                node.Entry!,
                node.String("name"),
                glyph,
                ValueList<int?>.From(costs),
                avoid,
                node.IntOr("def", 0),
                node.IntOr("res", 0),
                heal,
                node.BoolOr("appliesToFlyers", false),
                burn));
        }

        if (builder.Count == 0)
        {
            throw new ContentException(file.Name, null, "terrain", "must contain at least one terrain type");
        }

        return builder.ToImmutable();
    }

    private static ImmutableSortedDictionary<string, UnitClass> ParseClasses(ContentFile file, ImmutableSortedDictionary<string, Ability> abilities)
    {
        var entries = Entries(file, "classes");
        RequireUnique(file, entries);
        var builder = ImmutableSortedDictionary.CreateBuilder<string, UnitClass>(StringComparer.Ordinal);
        foreach (var node in entries)
        {
            var mov = node.Int("mov");
            if (mov < 1)
            {
                throw node.Error("mov", "must be at least 1");
            }

            var weaponNames = node.StringArray("weapons");
            if (weaponNames.Count == 0)
            {
                throw node.Error("weapons", "must list at least one weapon type");
            }

            var weapons = weaponNames.Select(w => node.ParseEnum<WeaponType>("weapons", w)).ToList();
            if (weapons.Distinct().Count() != weapons.Count)
            {
                throw node.Error("weapons", "must not repeat a weapon type");
            }

            var modifiers = node.OptionalObject("modifiers") is { } m ? ParseStats(m, allRequired: false) : Stats.Zero;
            var growthModifiers = node.OptionalObject("growthModifiers") is { } g ? ParseStats(g, allRequired: false) : Stats.Zero;
            var mastery = node.OptionalString("mastery");
            if (mastery is not null && !abilities.ContainsKey(mastery))
            {
                throw node.Error("mastery", $"unknown ability '{mastery}': not in {ContentFiles.AbilitiesName}");
            }

            var masteryPoints = 0;
            if (mastery is null && node.Has("masteryPoints"))
            {
                throw node.Error("masteryPoints", "needs a 'mastery' to earn");
            }

            if (mastery is not null)
            {
                if (!node.Has("masteryPoints"))
                {
                    throw node.Error("masteryPoints", $"missing: a class that names a mastery names the points that earn it");
                }

                masteryPoints = node.Int("masteryPoints");
                if (masteryPoints < 1)
                {
                    throw node.Error("masteryPoints", "must be at least 1");
                }
            }

            builder.Add(node.Entry!, new UnitClass(
                node.Entry!,
                node.String("name"),
                node.Enum<MovementType>("movement"),
                mov,
                modifiers,
                ValueList<WeaponType>.From(weapons),
                growthModifiers)
            {
                Mastery = mastery,
                MasteryPoints = masteryPoints,
                Abilities = AbilityIds(node, "abilities", abilities),
                Certification = ParseCertification(node),
                Hidden = node.BoolOr("hidden", false),
                Captain = node.BoolOr("captain", false),
                StrikeOnly = ParseStrikeOnly(node, weapons),
                Grants = ParseGrants(node, weapons),
            });
        }

        if (builder.Count == 0)
        {
            throw new ContentException(file.Name, null, "classes", "must contain at least one class");
        }

        foreach (var node in entries.Where(n => n.Has("advances")))
        {
            var basisId = node.String("advances");
            if (!builder.TryGetValue(basisId, out var basis))
            {
                throw node.Error("advances", $"unknown class '{basisId}'");
            }

            if (basis.Id == node.Entry || basis.Hidden)
            {
                throw node.Error("advances", $"'{basisId}' must be another class that is not hidden");
            }

            if (entries.Any(n => n.Entry == basisId && n.Has("advances")))
            {
                throw node.Error("advances", $"'{basisId}' is itself an advanced form; a class has one step above it");
            }

            if (node.Has("growthModifiers"))
            {
                throw node.Error("growthModifiers", $"an advanced form grows as its base, '{basisId}', does; name none");
            }

            var form = builder[node.Entry!];
            if (form.Captain != basis.Captain)
            {
                throw node.Error("captain", $"must match its base, '{basisId}': an advanced form is on the captain's ladder exactly when its base is");
            }

            foreach (var weapon in basis.Weapons.Where(w => !form.CanUse(w)))
            {
                throw node.Error("weapons", $"must keep every weapon type of '{basisId}'; missing {weapon.ToString().ToLowerInvariant()}");
            }

            builder[node.Entry!] = form with { GrowthModifiers = basis.GrowthModifiers, Advances = basis };
        }

        return builder.ToImmutable();
    }

    /// <summary>A class's optional <c>strikeOnly</c> (issue 704): weapon types it uses, each once, that it never heals with.</summary>
    private static ValueList<WeaponType> ParseStrikeOnly(EntryNode node, IReadOnlyList<WeaponType> weapons)
    {
        var types = node.StringArrayOrEmpty("strikeOnly").Select(w => node.ParseEnum<WeaponType>("strikeOnly", w)).ToList();
        if (types.Distinct().Count() != types.Count)
        {
            throw node.Error("strikeOnly", "must not repeat a weapon type");
        }

        foreach (var type in types.Where(t => !weapons.Contains(t)))
        {
            throw node.Error("strikeOnly", $"{type.ToString().ToLowerInvariant()} is not in the class's weapons");
        }

        return ValueList<WeaponType>.From(types);
    }

    /// <summary>A class's optional <c>grants</c> (issue 704): weapon type to the rank letter a unit holds at least on entering it; each type is one the class uses.</summary>
    private static ValueList<(WeaponType, WeaponRank)> ParseGrants(EntryNode node, IReadOnlyList<WeaponType> weapons)
    {
        if (node.OptionalObject("grants") is not { } grants)
        {
            return ValueList<(WeaponType, WeaponRank)>.Empty;
        }

        var list = new List<(WeaponType, WeaponRank)>();
        foreach (var property in grants.Element.EnumerateObject())
        {
            var type = node.ParseEnum<WeaponType>("grants", property.Name);
            if (!weapons.Contains(type))
            {
                throw node.Error("grants." + property.Name, "is not in the class's weapons");
            }

            list.Add((type, node.ParseEnum<WeaponRank>("grants." + property.Name, property.Value.GetString() ?? "")));
        }

        return ValueList<(WeaponType, WeaponRank)>.From(list);
    }

    /// <summary>
    /// A class's optional <c>certification</c> (issue 72): <c>level</c>, <c>ranks</c> (weapon
    /// type to rank letter) and <c>stats</c> (minimums, 0 asking nothing), each optional; absent, the class asks nothing.
    /// </summary>
    private static CertificationRequirements ParseCertification(EntryNode node)
    {
        if (node.OptionalObject("certification") is not { } certification)
        {
            return CertificationRequirements.None;
        }

        RequireOnly(node, certification, "certification", "level", "ranks", "stats");
        var level = Unit.MinLevel;
        if (certification.Has("level"))
        {
            if (certification.Element.GetProperty("level") is not { ValueKind: JsonValueKind.Number } element
                || !element.TryGetInt32(out level) || level < Unit.MinLevel || level > Unit.MaxLevel)
            {
                throw node.Error("certification.level", $"must be an integer {Unit.MinLevel}..{Unit.MaxLevel}");
            }
        }

        var ranks = new List<(WeaponType, WeaponRank)>();
        if (certification.OptionalObject("ranks") is { } rankNode)
        {
            foreach (var property in rankNode.Element.EnumerateObject())
            {
                var field = "certification.ranks." + property.Name;
                var type = node.ParseEnum<WeaponType>(field, property.Name);
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    throw node.Error(field, "must be a rank letter");
                }

                ranks.Add((type, node.ParseEnum<WeaponRank>(field, property.Value.GetString()!)));
            }
        }

        var stats = Stats.Zero;
        if (certification.OptionalObject("stats") is { } statNode)
        {
            foreach (var property in statNode.Element.EnumerateObject())
            {
                if (System.Array.IndexOf(StatKeys, property.Name) < 0)
                {
                    throw node.Error("certification.stats." + property.Name, "is not a stat; expected one of " + string.Join(", ", StatKeys));
                }

                if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out var minimum) || minimum < 0)
                {
                    throw node.Error("certification.stats." + property.Name, "must be an integer at least 0");
                }

                stats = stats.With((Stat)System.Array.IndexOf(StatKeys, property.Name), minimum);
            }
        }

        return new CertificationRequirements(level, ValueList<(WeaponType, WeaponRank)>.From(ranks), stats);
    }

    private static ImmutableSortedDictionary<string, Weapon> ParseWeapons(ContentFile file)
    {
        var entries = Entries(file, "weapons");
        RequireUnique(file, entries);
        var builder = ImmutableSortedDictionary.CreateBuilder<string, Weapon>(StringComparer.Ordinal);
        foreach (var node in entries)
        {
            var type = node.Enum<WeaponType>("type");
            var heals = node.BoolOr("heals", false);
            if (heals && type != WeaponType.Faith)
            {
                throw node.Error("heals", "only Faith spells can heal");
            }

            var mt = node.IntOr("mt", 0);
            if (mt < 0)
            {
                throw node.Error("mt", "must be at least 0");
            }

            var hit = node.IntOr("hit", 0);
            if (hit < 0 || hit > 200)
            {
                throw node.Error("hit", "must be 0..200");
            }

            var crit = node.IntOr("crit", 0);
            if (crit < 0 || crit > 100)
            {
                throw node.Error("crit", "must be 0..100");
            }

            var wt = node.Int("wt");
            if (wt < 0)
            {
                throw node.Error("wt", "must be at least 0");
            }

            var minRange = node.Int("minRange");
            if (minRange < 1)
            {
                throw node.Error("minRange", "must be at least 1");
            }

            var maxRange = node.Int("maxRange");
            if (maxRange < minRange)
            {
                throw node.Error("maxRange", "must be at least minRange");
            }

            if (type == WeaponType.Gauntlet && maxRange != 1)
            {
                throw node.Error("maxRange", "gauntlets strike at range 1 only");
            }

            var durability = node.Int("durability");
            if (durability < 1)
            {
                throw node.Error("durability", "must be at least 1");
            }

            var healBase = node.IntOr("healBase", 0);
            if (healBase < 0)
            {
                throw node.Error("healBase", "must be at least 0");
            }

            if (!heals && node.Has("healBase"))
            {
                throw node.Error("healBase", "is only valid when heals is true");
            }

            if (node.BoolOr("hungers", false) && (node.Has("price") || heals || type.IsMagic()))
            {
                throw node.Error("hungers", "a hungering weapon is a physical weapon with no price: never sold, never repaired");
            }

            if (node.BoolOr("glass", false) && (!node.Has("price") || heals || type.IsMagic() || node.Has("boundTo")))
            {
                throw node.Error("glass", "a glass weapon is a physical shop weapon with a price, bound to no one");
            }

            if (node.BoolOr("frozenIron", false) && node.BoolOr("hungers", false))
            {
                throw node.Error("frozenIron", "a hungering weapon is never frozen iron; it is what the iron holds");
            }

            if (node.BoolOr("frozenIron", false) && (heals || type.IsMagic()))
            {
                throw node.Error("frozenIron", "frozen iron is a physical weapon, never a spell");
            }

            var effective = node.StringArrayOrEmpty("effective")
                .Select(e => node.ParseEnum<MovementType>("effective", e)).ToList();
            if (effective.Distinct().Count() != effective.Count)
            {
                throw node.Error("effective", "must not repeat a movement type");
            }

            var critAgainst = node.StringArrayOrEmpty("critAgainst")
                .Select(e => node.ParseEnum<MovementType>("critAgainst", e)).ToList();
            if (critAgainst.Distinct().Count() != critAgainst.Count)
            {
                throw node.Error("critAgainst", "must not repeat a movement type");
            }

            var critBonus = node.IntOr("critBonus", 0);
            if (critAgainst.Count > 0 && (critBonus < 1 || critBonus > 100))
            {
                throw node.Error("critBonus", "must be 1..100 when critAgainst names a movement type");
            }

            if (critAgainst.Count == 0 && node.Has("critBonus"))
            {
                throw node.Error("critBonus", "is only valid when critAgainst names a movement type");
            }

            var heirloom = node.Has("heirloom") ? ReadHeirloom(node, heals || type.IsMagic()) : null;

            builder.Add(node.Entry!, new Weapon(
                node.Entry!,
                node.String("name"),
                type,
                mt,
                hit,
                crit,
                wt,
                minRange,
                maxRange,
                durability,
                ValueList<MovementType>.From(effective),
                heals,
                healBase,
                node.Enum<WeaponRank>("rank"),
                Price(node),
                node.BoolOr("ignites", false),
                node.BoolOr("windup", false))
            {
                BoundTo = node.Has("boundTo") ? node.String("boundTo") : null,
                Description = Description(node),
                Hungers = node.BoolOr("hungers", false),
                Heirloom = heirloom,
                Glass = node.BoolOr("glass", false),
                FrozenIron = node.BoolOr("frozenIron", false),
                CritAgainst = ValueList<MovementType>.From(critAgainst),
                CritBonus = critBonus,
            });
        }

        if (builder.Count == 0)
        {
            throw new ContentException(file.Name, null, "weapons", "must contain at least one weapon");
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Every unit file, plus the cast: the units of <see cref="ContentFiles.CastName"/> in
    /// file order, the first the captain. A cast entry must carry a region and a personality
    /// (section 9), its hooks must name other cast members, and a Reason or Faith unit must
    /// carry at least two castable spells, since a spent spell does not equip and the only
    /// sidearm a caster can hold is a second book (Design Table, seventh round), at least
    /// one of them an attacking spell, since a healing spell is legal only when an ally in
    /// range is hurt and two of them leave the unit with Move and Wait on a healthy turn
    /// (issue 113, ninth round). A cast entry may name a <c>signature</c> (DESIGN.md 13.18, issue
    /// 486), one of <see cref="SignatureKind"/>, and a <c>pronoun</c> (issue 615), one of
    /// <see cref="Pronoun"/>; a template may carry neither.
    /// </summary>
    private static (ImmutableSortedDictionary<string, Unit> Units, ValueList<Unit> Cast, ImmutableSortedDictionary<string, SignatureKind> Signatures, ImmutableSortedDictionary<string, Pronoun> Pronouns) ParseUnits(
        IReadOnlyList<ContentFile> files,
        ImmutableSortedDictionary<string, UnitClass> classes,
        ImmutableSortedDictionary<string, Weapon> weapons,
        ImmutableSortedDictionary<string, Item> items,
        ImmutableSortedDictionary<string, Ability> abilities)
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<string, Unit>(StringComparer.Ordinal);
        var origin = new Dictionary<string, string>(StringComparer.Ordinal);
        var cast = new List<Unit>();
        var castNodes = new List<EntryNode>();
        var signatures = ImmutableSortedDictionary.CreateBuilder<string, SignatureKind>(StringComparer.Ordinal);
        var pronouns = ImmutableSortedDictionary.CreateBuilder<string, Pronoun>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var entries = Entries(file, "units");
            RequireUnique(file, entries);
            var isCast = file.Name == ContentFiles.CastName;
            foreach (var node in entries)
            {
                if (origin.TryGetValue(node.Entry!, out var otherFile))
                {
                    throw node.Error("id", "duplicate id, also defined in " + otherFile);
                }

                origin[node.Entry!] = file.Name;
                var unit = ParseUnit(node, classes, weapons, items, abilities);
                if (!isCast)
                {
                    ValidateTemplateRanks(node, unit, classes, weapons);
                    if (node.OptionalString("signature") is not null)
                    {
                        throw node.Error("signature", "only a cast member carries a signature");
                    }

                    if (node.OptionalString("pronoun") is not null)
                    {
                        throw node.Error("pronoun", "only a cast member carries a pronoun");
                    }
                }
                else
                {
                    if (node.OptionalString("signature") is { } signature)
                    {
                        signatures.Add(node.Entry!, node.ParseEnum<SignatureKind>("signature", signature));
                    }

                    if (node.OptionalString("pronoun") is { } pronoun)
                    {
                        pronouns.Add(node.Entry!, node.ParseEnum<Pronoun>("pronoun", pronoun));
                    }
                }

                builder.Add(node.Entry!, unit);
                if (isCast)
                {
                    cast.Add(unit);
                    castNodes.Add(node);
                }
            }
        }

        var castIds = new HashSet<string>(cast.Select(u => u.Id), StringComparer.Ordinal);
        for (var i = 0; i < cast.Count; i++)
        {
            ValidateCastEntry(castNodes[i], cast[i], castIds, classes, weapons);
        }

        foreach (var (id, unit) in builder)
        {
            if (classes[unit.ClassId].Captain && !(cast.Count > 0 && cast[0].Id == id))
            {
                throw new ContentException(origin[id], id, "class", $"'{unit.ClassId}' is on the captain's ladder, and only the captain, the cast's first, stands in it");
            }
        }

        return (builder.ToImmutable(), ValueList<Unit>.From(cast), signatures.ToImmutable(), pronouns.ToImmutable());
    }

    /// <summary>
    /// An enemy template declares its ranks in content and nothing raises them (issue 67),
    /// so a weapon its class uses above its declared rank could never be equipped: that is
    /// a content error, named by the inventory slot. A cast member may carry such a weapon,
    /// since a player unit's rank grows by use.
    /// </summary>
    private static void ValidateTemplateRanks(
        EntryNode node,
        Unit unit,
        ImmutableSortedDictionary<string, UnitClass> classes,
        ImmutableSortedDictionary<string, Weapon> weapons)
    {
        var unitClass = classes[unit.ClassId];
        for (var i = 0; i < unit.Inventory.Count; i++)
        {
            if (weapons.TryGetValue(unit.Inventory.Items[i].ItemId, out var weapon) && unitClass.CanUse(weapon.Type) && !unit.CanWield(weapon, unitClass))
            {
                throw node.Error(
                    "inventory[" + i + "].item",
                    $"{unit.Id} is {Resolver.RankShort(unit, weapon)}; declare the rank under 'ranks'");
            }
        }
    }

    /// <summary>A unit's optional <c>ranks</c>: an object from weapon type to rank letter, each type at its rank's threshold, every type not named at E.</summary>
    private static WeaponSkill ParseRanks(EntryNode node)
    {
        if (node.OptionalObject("ranks") is not { } ranks)
        {
            return WeaponSkill.Zero;
        }

        var skill = WeaponSkill.Zero;
        foreach (var property in ranks.Element.EnumerateObject())
        {
            var type = node.ParseEnum<WeaponType>("ranks", property.Name);
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw node.Error("ranks." + property.Name, "must be a rank letter");
            }

            var rank = node.ParseEnum<WeaponRank>("ranks." + property.Name, property.Value.GetString()!);
            skill = skill.With(type, WeaponRanks.Threshold(rank));
        }

        return skill;
    }

    private static void ValidateCastEntry(
        EntryNode node,
        Unit unit,
        HashSet<string> castIds,
        ImmutableSortedDictionary<string, UnitClass> classes,
        ImmutableSortedDictionary<string, Weapon> weapons)
    {
        if (string.IsNullOrWhiteSpace(unit.Region))
        {
            throw node.Error("region", "a cast member names a home region");
        }

        if (string.IsNullOrWhiteSpace(unit.Personality))
        {
            throw node.Error("personality", "a cast member carries a one-line personality");
        }

        foreach (var hook in unit.Hooks)
        {
            if (hook == unit.Id || !castIds.Contains(hook))
            {
                throw node.Error("hooks", $"'{hook}' is not another cast member");
            }
        }

        var unitClass = classes[unit.ClassId];
        var casterOnly = unitClass.Weapons.Count > 0 && unitClass.Weapons.All(t => t.IsMagic());
        if (casterOnly)
        {
            var castable = unit.Inventory.Items
                .Select(s => weapons.TryGetValue(s.ItemId, out var w) && unitClass.CanUse(w.Type) ? w : null)
                .Where(w => w is not null)
                .ToList();
            if (castable.Count < 2)
            {
                throw node.Error("inventory", "a Lore or Faith unit carries at least two castable spells; a spent spell does not equip");
            }

            if (castable.All(w => w!.Heals))
            {
                throw node.Error("inventory", "a Lore or Faith unit carries at least one attacking spell, since a healing spell is legal only when an ally in range is hurt");
            }
        }
    }

    private static Unit ParseUnit(
        EntryNode node,
        ImmutableSortedDictionary<string, UnitClass> classes,
        ImmutableSortedDictionary<string, Weapon> weapons,
        ImmutableSortedDictionary<string, Item> knownItems,
        ImmutableSortedDictionary<string, Ability> abilities)
    {
        var classId = node.String("class");
        if (!classes.ContainsKey(classId))
        {
            throw node.Error("class", $"unknown class '{classId}'");
        }

        var level = node.IntOr("level", 1);
        if (level < Unit.MinLevel || level > Unit.MaxLevel)
        {
            throw node.Error("level", $"must be {Unit.MinLevel}..{Unit.MaxLevel}");
        }

        var exp = node.IntOr("exp", 0);
        if (exp < 0 || exp > Unit.MaxExp)
        {
            throw node.Error("exp", $"must be 0..{Unit.MaxExp}");
        }

        var stats = ParseStats(node.Object("stats"), allRequired: true);
        if (stats.Hp < 1)
        {
            throw node.Error("stats.hp", "must be at least 1");
        }

        foreach (var stat in Stats.All)
        {
            if (stats.Get(stat) < 0)
            {
                throw node.Error("stats." + stat.ToString().ToLowerInvariant(), "must be at least 0");
            }
        }

        var growths = ParseStats(node.Object("growths"), allRequired: true);
        foreach (var stat in Stats.All)
        {
            if (growths.Get(stat) < 0)
            {
                throw node.Error("growths." + stat.ToString().ToLowerInvariant(), "must be at least 0");
            }
        }

        var items = new List<ItemStack>();
        var index = 0;
        foreach (var element in node.ArrayOrEmpty("inventory"))
        {
            var field = "inventory[" + index + "]";
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw node.Error(field, "must be an object with 'item' and optional 'uses'");
            }

            var itemNode = new EntryNode(node.File, node.Entry, element);
            var itemId = itemNode.String("item");
            int maxUses;
            if (weapons.TryGetValue(itemId, out var weapon))
            {
                maxUses = weapon.Durability;
            }
            else if (knownItems.TryGetValue(itemId, out var known))
            {
                maxUses = known.Uses;
            }
            else
            {
                throw node.Error(field + ".item", $"unknown item '{itemId}': not a weapon in weapons.json or an item in items.json");
            }

            var uses = itemNode.IntOr("uses", maxUses);
            if (uses < 1 || uses > maxUses)
            {
                throw node.Error(field + ".uses", $"must be 1..{maxUses} for '{itemId}'");
            }

            items.Add(new ItemStack(itemId, uses));
            index++;
        }

        if (items.Count > Inventory.Capacity)
        {
            throw node.Error("inventory", $"holds at most {Inventory.Capacity} items, got {items.Count}");
        }

        return new Unit(
            node.Entry!,
            node.String("name"),
            classId,
            level,
            exp,
            stats,
            growths,
            new Inventory(ValueList<ItemStack>.From(items)),
            AbilityIds(node, "abilities", abilities),
            node.OptionalString("region"),
            node.OptionalString("personality"))
        {
            Hooks = ValueList<string>.From(node.StringArrayOrEmpty("hooks")),
            Skill = ParseRanks(node),
        };
    }
}
