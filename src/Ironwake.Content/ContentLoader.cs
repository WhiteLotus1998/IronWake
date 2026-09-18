using System.Collections.Immutable;
using System.Text.Json;
using Ironwake.Core;

namespace Ironwake.Content;

/// <summary>
/// Reads the content directory into a validated <see cref="GameContent"/>. Every failure
/// is a <see cref="ContentException"/> naming file, entry, and field. Validation order
/// is fixed: terrain, classes, weapons, units, then cross-references, so the first error
/// reported is deterministic.
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
            ReadFile(contentRoot, ContentFiles.ItemsName));

        return Parse(files);
    }

    /// <summary>Parses already-read file text. This is the whole loader; <see cref="Load"/> only adds disk access.</summary>
    public static GameContent Parse(ContentFiles files)
    {
        var terrain = ParseTerrain(files.Terrain);
        var classes = ParseClasses(files.Classes);
        var weapons = ParseWeapons(files.Weapons);
        var items = ParseItems(files.Items, weapons);
        var units = ParseUnits(files.Units, classes, weapons, items);
        var wakeRadius = ParseRules(files.Rules);
        return new GameContent(classes, weapons, terrain, units, items, wakeRadius);
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

            builder.Add(node.Entry!, new Item(node.Entry!, node.String("name"), heals, uses));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// The global rule constants of DESIGN.md: today only the Guard wake radius (section 8),
    /// which lives in content so it is identical on every map and never in a map file.
    /// </summary>
    private static int ParseRules(ContentFile file)
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

        return wakeRadius;
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

    private static Stats ParseStats(EntryNode node, bool allRequired)
    {
        var values = new int[StatKeys.Length];
        for (var i = 0; i < StatKeys.Length; i++)
        {
            values[i] = allRequired ? node.Int(StatKeys[i]) : node.IntOr(StatKeys[i], 0);
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

    private static ImmutableSortedDictionary<string, UnitClass> ParseClasses(ContentFile file)
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

            builder.Add(node.Entry!, new UnitClass(
                node.Entry!,
                node.String("name"),
                node.Enum<MovementType>("movement"),
                mov,
                modifiers,
                ValueList<WeaponType>.From(weapons),
                growthModifiers));
        }

        if (builder.Count == 0)
        {
            throw new ContentException(file.Name, null, "classes", "must contain at least one class");
        }

        return builder.ToImmutable();
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
                healBase));
        }

        if (builder.Count == 0)
        {
            throw new ContentException(file.Name, null, "weapons", "must contain at least one weapon");
        }

        return builder.ToImmutable();
    }

    private static ImmutableSortedDictionary<string, Unit> ParseUnits(
        IReadOnlyList<ContentFile> files,
        ImmutableSortedDictionary<string, UnitClass> classes,
        ImmutableSortedDictionary<string, Weapon> weapons,
        ImmutableSortedDictionary<string, Item> items)
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<string, Unit>(StringComparer.Ordinal);
        var origin = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var entries = Entries(file, "units");
            RequireUnique(file, entries);
            foreach (var node in entries)
            {
                if (origin.TryGetValue(node.Entry!, out var otherFile))
                {
                    throw node.Error("id", "duplicate id, also defined in " + otherFile);
                }

                origin[node.Entry!] = file.Name;
                builder.Add(node.Entry!, ParseUnit(node, classes, weapons, items));
            }
        }

        return builder.ToImmutable();
    }

    private static Unit ParseUnit(
        EntryNode node,
        ImmutableSortedDictionary<string, UnitClass> classes,
        ImmutableSortedDictionary<string, Weapon> weapons,
        ImmutableSortedDictionary<string, Item> knownItems)
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
            ValueList<string>.From(node.StringArrayOrEmpty("abilities")),
            node.OptionalString("region"),
            node.OptionalString("personality"));
    }
}
