using System.Text;
using System.Text.Json;
using Ironwake.Core;

namespace Ironwake.Content;

/// <summary>
/// Writes a <see cref="GameContent"/> back to the file shapes <see cref="ContentLoader"/>
/// reads. Output is canonical: sorted by id, every field present, so two equal contents
/// serialize to identical text. The cast goes into units/cast.json and every other unit into units/all.json.
/// </summary>
public static class ContentSerializer
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    public static ContentFiles Write(GameContent content) => new(
        new ContentFile(ContentFiles.ClassesName, WriteArray("classes", content.Classes.Values, WriteClass)),
        new ContentFile(ContentFiles.WeaponsName, WriteArray("weapons", content.Weapons.Values, WriteWeapon)),
        new ContentFile(ContentFiles.TerrainName, WriteArray("terrain", content.Terrain.Values, WriteTerrain)),
        UnitFiles(content),
        new ContentFile(ContentFiles.RulesName, "{\n  \"wakeRadius\": " + content.WakeRadius + "\n}\n"),
        new ContentFile(ContentFiles.ItemsName, WriteArray("items", content.Items.Values, WriteItem)));

    /// <summary>
    /// The cast goes to <see cref="ContentFiles.CastName"/> in roster order, since the order is
    /// content (issue 13); every other unit goes to units/all.json. No cast file is written for
    /// content without a cast, so a round trip stays equal.
    /// </summary>
    private static IReadOnlyList<ContentFile> UnitFiles(GameContent content)
    {
        var castIds = new HashSet<string>(content.Cast.Select(u => u.Id), StringComparer.Ordinal);
        var files = new List<ContentFile>
        {
            new(ContentFiles.UnitsDirectory + "/all.json", WriteArray("units", content.Units.Values.Where(u => !castIds.Contains(u.Id)), WriteUnit)),
        };
        if (content.Cast.Count > 0)
        {
            files.Add(new ContentFile(ContentFiles.CastName, WriteArray("units", content.Cast, WriteUnit)));
        }

        return files;
    }

    private static void WriteItem(Utf8JsonWriter writer, Item item)
    {
        writer.WriteStartObject();
        writer.WriteString("id", item.Id);
        writer.WriteString("name", item.Name);
        writer.WriteNumber("heals", item.Heals);
        writer.WriteNumber("uses", item.Uses);
        writer.WriteEndObject();
    }

    private static string WriteArray<T>(string key, IEnumerable<T> items, Action<Utf8JsonWriter, T> writeItem)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteStartArray(key);
            foreach (var item in items)
            {
                writeItem(writer, item);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static void WriteStats(Utf8JsonWriter writer, string key, Stats stats)
    {
        writer.WriteStartObject(key);
        foreach (var stat in Stats.All)
        {
            writer.WriteNumber(stat.ToString().ToLowerInvariant(), stats.Get(stat));
        }

        writer.WriteEndObject();
    }

    private static void WriteClass(Utf8JsonWriter writer, UnitClass unitClass)
    {
        writer.WriteStartObject();
        writer.WriteString("id", unitClass.Id);
        writer.WriteString("name", unitClass.Name);
        writer.WriteString("movement", unitClass.Movement.ToString().ToLowerInvariant());
        writer.WriteNumber("mov", unitClass.Mov);
        writer.WriteStartArray("weapons");
        foreach (var weapon in unitClass.Weapons)
        {
            writer.WriteStringValue(weapon.ToString().ToLowerInvariant());
        }

        writer.WriteEndArray();
        WriteStats(writer, "modifiers", unitClass.Modifiers);
        WriteStats(writer, "growthModifiers", unitClass.GrowthModifiers);
        writer.WriteEndObject();
    }

    private static void WriteWeapon(Utf8JsonWriter writer, Weapon weapon)
    {
        writer.WriteStartObject();
        writer.WriteString("id", weapon.Id);
        writer.WriteString("name", weapon.Name);
        writer.WriteString("type", weapon.Type.ToString().ToLowerInvariant());
        writer.WriteNumber("mt", weapon.Mt);
        writer.WriteNumber("hit", weapon.Hit);
        writer.WriteNumber("crit", weapon.Crit);
        writer.WriteNumber("wt", weapon.Wt);
        writer.WriteNumber("minRange", weapon.MinRange);
        writer.WriteNumber("maxRange", weapon.MaxRange);
        writer.WriteNumber("durability", weapon.Durability);
        writer.WriteStartArray("effective");
        foreach (var movement in weapon.EffectiveAgainst)
        {
            writer.WriteStringValue(movement.ToString().ToLowerInvariant());
        }

        writer.WriteEndArray();
        writer.WriteBoolean("heals", weapon.Heals);
        if (weapon.Heals)
        {
            writer.WriteNumber("healBase", weapon.HealBase);
        }

        writer.WriteEndObject();
    }

    private static void WriteTerrain(Utf8JsonWriter writer, Terrain terrain)
    {
        writer.WriteStartObject();
        writer.WriteString("id", terrain.Id);
        writer.WriteString("name", terrain.Name);
        writer.WriteString("glyph", terrain.Glyph.ToString());
        writer.WriteStartObject("cost");
        foreach (var movement in Enum.GetValues<MovementType>())
        {
            var key = movement.ToString().ToLowerInvariant();
            var cost = terrain.MoveCost(movement);
            if (cost is null)
            {
                writer.WriteNull(key);
            }
            else
            {
                writer.WriteNumber(key, cost.Value);
            }
        }

        writer.WriteEndObject();
        writer.WriteNumber("avoid", terrain.Avoid);
        writer.WriteNumber("def", terrain.Def);
        writer.WriteNumber("res", terrain.Res);
        writer.WriteNumber("heal", terrain.HealPercent);
        writer.WriteBoolean("appliesToFlyers", terrain.AppliesToFlyers);
        writer.WriteEndObject();
    }

    private static void WriteUnit(Utf8JsonWriter writer, Unit unit)
    {
        writer.WriteStartObject();
        writer.WriteString("id", unit.Id);
        writer.WriteString("name", unit.Name);
        writer.WriteString("class", unit.ClassId);
        writer.WriteNumber("level", unit.Level);
        writer.WriteNumber("exp", unit.Exp);
        WriteStats(writer, "stats", unit.Stats);
        WriteStats(writer, "growths", unit.Growths);
        writer.WriteStartArray("inventory");
        foreach (var item in unit.Inventory.Items)
        {
            writer.WriteStartObject();
            writer.WriteString("item", item.ItemId);
            writer.WriteNumber("uses", item.Uses);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("abilities");
        foreach (var ability in unit.Abilities)
        {
            writer.WriteStringValue(ability);
        }

        writer.WriteEndArray();
        if (unit.Region is not null)
        {
            writer.WriteString("region", unit.Region);
        }

        if (unit.Personality is not null)
        {
            writer.WriteString("personality", unit.Personality);
        }

        if (unit.Hooks.Count > 0)
        {
            writer.WriteStartArray("hooks");
            foreach (var hook in unit.Hooks)
            {
                writer.WriteStringValue(hook);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}
