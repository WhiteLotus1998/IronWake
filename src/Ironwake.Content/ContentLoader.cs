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
        var (units, cast) = ParseUnits(files.Units, classes, weapons, items, abilities);
        var (wakeRadius, rivalry, difficulties) = ParseRules(files.Rules);
        var campaign = files.Campaign is { } campaignFile ? ParseCampaign(campaignFile, weapons, items, classes, terrain) : CampaignRules.None;
        return new GameContent(classes, weapons, terrain, units, items, wakeRadius) { Cast = cast, Rivalry = rivalry, Abilities = abilities, Difficulties = difficulties, Campaign = campaign };
    }

    /// <summary>
    /// <c>campaign.json</c> (issue 74): <c>startingPurse</c> and <c>certificationPrice</c>, each at
    /// least 0, and <c>maps</c>, at least one, each a <c>map</c> id (unique), a <c>reward</c> of at
    /// least 0 and a <c>stock</c> of weapon or item ids, each carrying a <c>price</c> and none
    /// listed twice. The map ids are checked against <c>content/maps</c> by whoever loads maps.
    /// The optional <c>trials</c> object (issue 252) maps a class id to a trial map id under
    /// <c>content/trials</c>; an unknown class or an empty map id is refused, and whoever loads the
    /// trial checks that its <c>certification:</c> header names the same class.
    /// </summary>
    private static CampaignRules ParseCampaign(
        ContentFile file, ImmutableSortedDictionary<string, Weapon> weapons, ImmutableSortedDictionary<string, Item> items,
        ImmutableSortedDictionary<string, UnitClass> classes, ImmutableSortedDictionary<string, Terrain> terrain)
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

            maps.Add(new CampaignMap(mapId, reward, ValueList<string>.From(stock)));
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

        return new CampaignRules(purse, seal, ValueList<CampaignMap>.From(maps))
        {
            Trials = ValueList<CampaignTrial>.From(trials.OrderBy(t => t.ClassId, StringComparer.Ordinal)),
            Keep = root.OptionalObject("keep") is { } keepNode ? ParseKeep(keepNode, terrain) : KeepMenu.None,
        };
    }

    /// <summary>
    /// The optional <c>keep</c> object (issue 82): the keep's <c>map</c> id under <c>content/keep</c>
    /// and its <c>edits</c>, each an <c>id</c>, <c>name</c>, <c>terrain</c> id, <c>price</c> of at least 1
    /// and a non-empty <c>at</c> list of <c>x,y</c> tiles. Whoever loads the map checks the tiles against it.
    /// </summary>
    private static KeepMenu ParseKeep(EntryNode node, ImmutableSortedDictionary<string, Terrain> terrain)
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

        return new KeepMenu(mapId, ValueList<KeepEdit>.From(edits));
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
                    RequireOnly(entry, condition, "effect.against", "weapon", "movement");
                    WeaponType? weapon = condition.Has("weapon") ? entry.ParseEnum<WeaponType>("effect.against.weapon", condition.String("weapon")) : null;
                    MovementType? movement = condition.Has("movement") ? entry.ParseEnum<MovementType>("effect.against.movement", condition.String("movement")) : null;
                    if (weapon is null && movement is null)
                    {
                        throw entry.Error("effect.against", "must name a weapon, a movement, or both; leave it out to match every opponent");
                    }

                    against = new OpponentCondition(weapon, movement);
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
                RequireOnly(entry, effect, "effect", "kind", "weapon", "rank", "cost", "mt", "hit", "crit", "wt", "range");
                var art = new CombatArtEffect(
                    entry.ParseEnum<WeaponType>("effect.weapon", effect.String("weapon")),
                    entry.ParseEnum<WeaponRank>("effect.rank", effect.String("rank")),
                    effect.Int("cost"),
                    effect.IntOr("mt", 0),
                    effect.IntOr("hit", 0),
                    effect.IntOr("crit", 0),
                    effect.IntOr("wt", 0),
                    effect.IntOr("range", 0));
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
            default:
                throw entry.Error("effect.kind", $"unknown kind '{kind}'; expected stats, combat, art or canto");
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

            builder.Add(node.Entry!, new Item(node.Entry!, node.String("name"), heals, uses, Price(node)));
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
    /// 99; omitted keeps each map's). The block must declare <c>normal</c>, and <c>normal</c>
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
                if (field.Name is not ("statPercent" or "enemyLevelOffset" or "recall"))
                {
                    throw entry.Error(field.Name, "is not a difficulty field; expected statPercent, enemyLevelOffset or recall");
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

            builder.Add(property.Name, new Difficulty(property.Name, percent, offset, recall));
        }

        if (!builder.TryGetValue(Difficulty.NormalId, out var normal))
        {
            throw node.Error("difficulties", $"must declare '{Difficulty.NormalId}'");
        }

        if (!normal.IsIdentity)
        {
            throw new ContentException(node.File, "difficulties." + Difficulty.NormalId, null, "must be the identity: every percent 100, offset 0, no recall");
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
                node.BoolOr("appliesToFlyers", false)));
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
            });
        }

        if (builder.Count == 0)
        {
            throw new ContentException(file.Name, null, "classes", "must contain at least one class");
        }

        return builder.ToImmutable();
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

            var effective = node.StringArrayOrEmpty("effective")
                .Select(e => node.ParseEnum<MovementType>("effective", e)).ToList();
            if (effective.Distinct().Count() != effective.Count)
            {
                throw node.Error("effective", "must not repeat a movement type");
            }

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
                Price(node)));
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
    /// (issue 113, ninth round).
    /// </summary>
    private static (ImmutableSortedDictionary<string, Unit> Units, ValueList<Unit> Cast) ParseUnits(
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

        return (builder.ToImmutable(), ValueList<Unit>.From(cast));
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
                throw node.Error("inventory", "a Reason or Faith unit carries at least two castable spells; a spent spell does not equip");
            }

            if (castable.All(w => w!.Heals))
            {
                throw node.Error("inventory", "a Reason or Faith unit carries at least one attacking spell, since a healing spell is legal only when an ally in range is hurt");
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
