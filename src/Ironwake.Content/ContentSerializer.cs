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
        new ContentFile(ContentFiles.RulesName, WriteRules(content)),
        new ContentFile(ContentFiles.ItemsName, WriteArray("items", content.Items.Values, WriteItem)),
        new ContentFile(ContentFiles.AbilitiesName, WriteArray("abilities", content.Abilities.Values, WriteAbility)),
        content.Campaign == CampaignRules.None ? null : new ContentFile(ContentFiles.CampaignName, WriteCampaign(content.Campaign)));

    /// <summary>campaign.json (issue 74) in the shape <see cref="ContentLoader"/> reads.</summary>
    private static string WriteCampaign(CampaignRules campaign)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("startingPurse", campaign.StartingPurse);
            writer.WriteNumber("certificationPrice", campaign.CertificationPrice);
            writer.WriteStartArray("maps");
            foreach (var map in campaign.Maps)
            {
                writer.WriteStartObject();
                writer.WriteString("map", map.MapId);
                writer.WriteNumber("reward", map.Reward);
                writer.WriteStartArray("stock");
                foreach (var id in map.Stock)
                {
                    writer.WriteStringValue(id);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (campaign.Trials.Count > 0)
            {
                writer.WriteStartObject("trials");
                foreach (var trial in campaign.Trials)
                {
                    writer.WriteString(trial.ClassId, trial.MapId);
                }

                writer.WriteEndObject();
            }

            if (campaign.Keep != KeepMenu.None)
            {
                writer.WriteStartObject("keep");
                writer.WriteString("map", campaign.Keep.MapId);
                writer.WriteStartArray("edits");
                foreach (var edit in campaign.Keep.Edits)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", edit.Id);
                    writer.WriteString("name", edit.Name);
                    writer.WriteString("terrain", edit.TerrainId);
                    writer.WriteNumber("price", edit.Price);
                    writer.WriteStartArray("at");
                    foreach (var at in edit.At)
                    {
                        writer.WriteStringValue(at.ToString());
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary>An ability and its effect in the shape <see cref="ContentLoader"/> reads (issue 66); a combat effect writes all four modifiers, an art all five deltas.</summary>
    private static void WriteAbility(Utf8JsonWriter writer, Ability ability)
    {
        writer.WriteStartObject();
        writer.WriteString("id", ability.Id);
        writer.WriteString("name", ability.Name);
        writer.WriteString("text", ability.Text);
        writer.WriteStartObject("effect");
        switch (ability.Effect)
        {
            case StatDeltaEffect delta:
                writer.WriteString("kind", "stats");
                WriteStats(writer, "stats", delta.Delta);
                break;
            case CombatModifierEffect modifier:
                writer.WriteString("kind", "combat");
                if (modifier.Against != OpponentCondition.Any)
                {
                    writer.WriteStartObject("against");
                    if (modifier.Against.Weapon is { } weapon)
                    {
                        writer.WriteString("weapon", weapon.ToString().ToLowerInvariant());
                    }

                    if (modifier.Against.Movement is { } movement)
                    {
                        writer.WriteString("movement", movement.ToString().ToLowerInvariant());
                    }

                    writer.WriteEndObject();
                }

                if (modifier.Wielding is { } wielding)
                {
                    writer.WriteString("wielding", wielding.ToString().ToLowerInvariant());
                }

                writer.WriteNumber("hit", modifier.Hit);
                writer.WriteNumber("avoid", modifier.Avoid);
                writer.WriteNumber("crit", modifier.Crit);
                writer.WriteNumber("critAvoid", modifier.CritAvoid);
                break;
            case CombatArtEffect art:
                writer.WriteString("kind", "art");
                writer.WriteString("weapon", art.Weapon.ToString().ToLowerInvariant());
                writer.WriteString("rank", art.Rank.ToString());
                writer.WriteNumber("cost", art.Cost);
                writer.WriteNumber("mt", art.Mt);
                writer.WriteNumber("hit", art.Hit);
                writer.WriteNumber("crit", art.Crit);
                writer.WriteNumber("wt", art.Wt);
                writer.WriteNumber("range", art.Range);
                break;
            case CantoEffect:
                writer.WriteString("kind", "canto");
                break;
            default:
                throw new ArgumentException($"no serializer for the effect of {ability.Id}", nameof(ability));
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

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
        if (item.Price is { } price)
        {
            writer.WriteNumber("price", price);
        }

        writer.WriteEndObject();
    }

    /// <summary>rules.json: the wake radius, and the rivalry (issue 16) and difficulties (issue 76) blocks when the content has them.</summary>
    private static string WriteRules(GameContent content)
    {
        if (content.Rivalry == RivalryRules.None && content.Difficulties.Count == 0)
        {
            return "{\n  \"wakeRadius\": " + content.WakeRadius + "\n}\n";
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("wakeRadius", content.WakeRadius);
            if (content.Rivalry != RivalryRules.None)
            {
                writer.WriteStartObject("rivalry");
                writer.WriteStartObject("arms");
                foreach (var arm in content.Rivalry.Arms)
                {
                    writer.WriteStartObject(arm.Id);
                    writer.WriteNumber("hit", arm.Hit);
                    writer.WriteNumber("crit", arm.Crit);
                    writer.WriteNumber("critAvoid", arm.CritAvoid);
                    writer.WriteBoolean("countersOnly", arm.CountersOnly);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
                writer.WriteStartArray("rapportRate");
                foreach (var step in content.Rivalry.RapportRates)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("cha", step.Cha);
                    writer.WriteNumber("rate", step.Rate);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteNumber("overwriteAt", content.Rivalry.OverwriteAt);
                writer.WriteEndObject();
            }

            if (content.Difficulties.Count > 0)
            {
                writer.WriteStartObject("difficulties");
                foreach (var difficulty in content.Difficulties.Values)
                {
                    writer.WriteStartObject(difficulty.Id);
                    WriteStats(writer, "statPercent", difficulty.StatPercent);
                    writer.WriteNumber("enemyLevelOffset", difficulty.EnemyLevelOffset);
                    if (difficulty.RecallCharges is { } recall)
                    {
                        writer.WriteNumber("recall", recall);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
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
        if (unitClass.Mastery is not null)
        {
            writer.WriteString("mastery", unitClass.Mastery);
            writer.WriteNumber("masteryPoints", unitClass.MasteryPoints);
        }

        if (unitClass.Abilities.Count > 0)
        {
            writer.WriteStartArray("abilities");
            foreach (var ability in unitClass.Abilities)
            {
                writer.WriteStringValue(ability);
            }

            writer.WriteEndArray();
        }

        if (unitClass.Certification != CertificationRequirements.None)
        {
            var certification = unitClass.Certification;
            writer.WriteStartObject("certification");
            writer.WriteNumber("level", certification.Level);
            writer.WriteStartObject("ranks");
            foreach (var (type, rank) in certification.Ranks)
            {
                writer.WriteString(type.ToString().ToLowerInvariant(), rank.ToString());
            }

            writer.WriteEndObject();
            WriteStats(writer, "stats", certification.Stats);
            writer.WriteEndObject();
        }

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
        writer.WriteString("rank", weapon.Rank.ToString());
        if (weapon.Price is { } price)
        {
            writer.WriteNumber("price", price);
        }

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

        if (unit.Skill != WeaponSkill.Zero)
        {
            writer.WriteStartObject("ranks");
            foreach (var (type, points) in unit.Skill.All.Where(t => t.Points > 0))
            {
                writer.WriteString(type.ToString().ToLowerInvariant(), WeaponRanks.RankAt(points).ToString());
            }

            writer.WriteEndObject();
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
