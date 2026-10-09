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
        content.Campaign == CampaignRules.None ? null : new ContentFile(ContentFiles.CampaignName, WriteCampaign(content.Campaign)),
        content.Scenes.Select(s => new ContentFile(SceneFormat.Directory + "/" + s.Id + ".txt", SceneFormat.Write(s))).ToList());

    /// <summary>A map's text card (issue 631) as a string array under <paramref name="name"/>; nothing for an empty card.</summary>
    private static void WriteCard(Utf8JsonWriter writer, string name, ValueList<string> card)
    {
        if (card.Count == 0)
        {
            return;
        }

        writer.WriteStartArray(name);
        foreach (var paragraph in card)
        {
            writer.WriteStringValue(paragraph);
        }

        writer.WriteEndArray();
    }

    /// <summary>campaign.json (issue 74) in the shape <see cref="ContentLoader"/> reads.</summary>
    private static string WriteCampaign(CampaignRules campaign)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("startingPurse", campaign.StartingPurse);
            writer.WriteNumber("certificationPrice", campaign.CertificationPrice);
            writer.WriteNumber("advancedCertificationPrice", campaign.AdvancedCertificationPrice);
            if (campaign.LevyFloor > 0)
            {
                writer.WriteNumber("levyFloor", campaign.LevyFloor);
            }

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
                WriteCard(writer, "before", map.Before);
                WriteCard(writer, "after", map.After);
                if (map.Arrives.Count > 0)
                {
                    writer.WriteStartArray("arrives");
                    foreach (var id in map.Arrives)
                    {
                        writer.WriteStringValue(id);
                    }

                    writer.WriteEndArray();
                }

                if (map.Joins.Count > 0)
                {
                    writer.WriteStartArray("joins");
                    foreach (var id in map.Joins)
                    {
                        writer.WriteStringValue(id);
                    }

                    writer.WriteEndArray();
                }

                if (map.Branch.Count > 0)
                {
                    writer.WriteStartArray("branch");
                    foreach (var id in map.Branch)
                    {
                        writer.WriteStringValue(id);
                    }

                    writer.WriteEndArray();
                }

                if (map.Pitch.Count > 0)
                {
                    writer.WriteStartObject("pitch");
                    for (var i = 0; i < map.Branch.Count; i++)
                    {
                        writer.WriteString(map.Branch[i], map.Pitch[i]);
                    }

                    writer.WriteEndObject();
                }

                if (map.SeatLevel is { } seatLevel)
                {
                    writer.WriteNumber("seatLevel", seatLevel);
                }

                if (map.SeatRank > 0)
                {
                    writer.WriteNumber("seatRank", map.SeatRank);
                }

                if (map.Meets.Count > 0)
                {
                    writer.WriteStartArray("meets");
                    foreach (var id in map.Meets)
                    {
                        writer.WriteStringValue(id);
                    }

                    writer.WriteEndArray();
                }

                if (map.Return is { } back)
                {
                    writer.WriteStartObject("return");
                    writer.WriteString("at", $"{back.At.X},{back.At.Y}");
                    writer.WriteString("group", back.Group);
                    writer.WriteString("behavior", back.Behavior.ToString().ToLowerInvariant());
                    writer.WriteEndObject();
                }

                if (map.EnemyLevel is { } enemyLevel)
                {
                    writer.WriteNumber("enemyLevel", enemyLevel);
                }

                if (map.Swaps.Count > 0)
                {
                    writer.WriteStartObject("swap");
                    foreach (var swap in map.Swaps)
                    {
                        writer.WriteString($"{swap.At.X},{swap.At.Y}", swap.TemplateId);
                    }

                    writer.WriteEndObject();
                }

                if (map.Secret is { } secret)
                {
                    writer.WriteStartObject("secret");
                    writer.WriteString("bearer", secret.Bearer);
                    writer.WriteString("quest", secret.Quest);
                    if (secret.Hill is { } hill)
                    {
                        writer.WriteStartObject("hill");
                        writer.WriteString("map", hill.MapId);
                        writer.WriteNumber("enemyLevel", hill.EnemyLevel ?? 1);
                        writer.WriteStartArray("keepers");
                        foreach (var keeper in secret.Keepers)
                        {
                            writer.WriteStringValue(keeper);
                        }

                        writer.WriteEndArray();
                        WriteCard(writer, "before", hill.Before);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (campaign.Issues.Count > 0)
            {
                writer.WriteStartObject("issues");
                foreach (var issue in campaign.Issues)
                {
                    writer.WriteString(issue.UnitId, issue.WeaponId);
                }

                writer.WriteEndObject();
            }

            if (campaign.Trials.Count > 0)
            {
                writer.WriteStartObject("trials");
                foreach (var trial in campaign.Trials)
                {
                    writer.WriteString(trial.ClassId, trial.MapId);
                }

                writer.WriteEndObject();
            }

            if (campaign.Quests.Count > 0)
            {
                writer.WriteStartArray("quests");
                foreach (var quest in campaign.Quests)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", quest.Id);
                    writer.WriteString("member", quest.MemberId);
                    writer.WriteNumber("part", quest.Part);
                    writer.WriteString("map", quest.MapId);
                    if (quest.Pays is { } pays)
                    {
                        writer.WriteString("pays", pays);
                    }

                    if (quest.Wakes is { } wakes)
                    {
                        writer.WriteString("wakes", wakes);
                    }

                    if (quest.Names is { } names)
                    {
                        writer.WriteString("names", names);
                    }

                    if (quest.Common > 0)
                    {
                        writer.WriteNumber("common", quest.Common);
                    }

                    if (quest.Rare > 0)
                    {
                        writer.WriteNumber("rare", quest.Rare);
                    }

                    if (quest.OpensAfter is { } opensAfter)
                    {
                        writer.WriteString("opensAfter", opensAfter);
                    }

                    if (quest.Promotes is { } promotes)
                    {
                        writer.WriteString("promotes", promotes);
                    }

                    if (quest.Ending is { } ending)
                    {
                        writer.WriteString("ending", ending);
                    }

                    WriteCard(writer, "before", quest.Before);
                    WriteCard(writer, "after", quest.After);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            if (campaign.Drake is { } drake)
            {
                writer.WriteStartObject("drake");
                writer.WriteString("member", drake.Member);
                writer.WriteString("grownAfter", drake.GrownAfter);
                writer.WriteNumber("grownFlown", drake.GrownFlown);
                writer.WriteString("unbrokenAfter", drake.UnbrokenAfter);
                writer.WriteEndObject();
            }

            if (campaign.Forge != ForgeRules.None)
            {
                writer.WriteStartObject("forge");
                writer.WriteNumber("mt", campaign.Forge.Mt);
                writer.WriteNumber("hit", campaign.Forge.Hit);
                writer.WriteNumber("price", campaign.Forge.Price);
                writer.WriteNumber("common", campaign.Forge.CommonSteps);
                writer.WriteNumber("rare", campaign.Forge.RareSteps);
                writer.WriteEndObject();
            }

            if (campaign.Origins.Count > 0)
            {
                writer.WriteStartArray("origins");
                foreach (var origin in campaign.Origins)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", origin.Id);
                    writer.WriteString("name", origin.Name);
                    WriteStats(writer, "stats", origin.Stats);
                    WriteStats(writer, "growths", origin.Growths);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            if (campaign.Supports.Count > 0)
            {
                writer.WriteStartArray("supports");
                foreach (var pair in campaign.Supports)
                {
                    writer.WriteStartObject();
                    writer.WriteString("a", pair.A);
                    writer.WriteString("b", pair.B);
                    writer.WriteString("kind", pair.Kind.ToString().ToLowerInvariant());
                    if (pair.Romance.Count > 0)
                    {
                        writer.WriteStartArray("romance");
                        foreach (var pronoun in pair.Romance)
                        {
                            writer.WriteStringValue(pronoun.ToString().ToLowerInvariant());
                        }

                        writer.WriteEndArray();
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            if (campaign.Keep != KeepMenu.None)
            {
                writer.WriteStartObject("keep");
                writer.WriteString("map", campaign.Keep.MapId);
                if (campaign.Keep.RaidId.Length > 0)
                {
                    writer.WriteString("raid", campaign.Keep.RaidId);
                }

                if (campaign.Keep.Beds > 0)
                {
                    writer.WriteNumber("beds", campaign.Keep.Beds);
                }

                if (campaign.Keep.Rooms.Count > 0)
                {
                    writer.WriteStartArray("rooms");
                    foreach (var room in campaign.Keep.Rooms)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("id", room.Id);
                        writer.WriteString("name", room.Name);
                        writer.WriteNumber("price", room.Price);
                        writer.WriteNumber("beds", room.Beds);
                        writer.WriteNumber("max", room.Max);
                        if (room.Forge)
                        {
                            writer.WriteBoolean("forge", true);
                        }

                        if (room.After.Length > 0)
                        {
                            writer.WriteString("after", room.After);
                        }

                        if (room.Requires.Length > 0)
                        {
                            writer.WriteString("requires", room.Requires);
                        }

                        if (room.Hires.Count > 0)
                        {
                            writer.WriteStartArray("hires");
                            foreach (var hireId in room.Hires)
                            {
                                writer.WriteStringValue(hireId);
                            }

                            writer.WriteEndArray();
                        }

                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }

                if (campaign.Keep.Hires.Count > 0)
                {
                    writer.WriteNumber("hirePrice", campaign.Keep.HirePrice);
                    writer.WriteStartArray("hires");
                    foreach (var hire in campaign.Keep.Hires)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("id", hire.Id);
                        writer.WriteString("name", hire.Name);
                        writer.WriteString("pronoun", hire.Pronoun.ToString().ToLowerInvariant());
                        writer.WriteString("class", hire.ClassId);
                        writer.WriteStartArray("items");
                        foreach (var itemId in hire.Items)
                        {
                            writer.WriteStringValue(itemId);
                        }

                        writer.WriteEndArray();
                        writer.WriteString("line", hire.Line);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }

                if (campaign.Keep.FinaleRanks.Count > 0)
                {
                    writer.WriteStartObject("finaleRanks");
                    foreach (var (id, skill) in campaign.Keep.FinaleRanks)
                    {
                        writer.WriteStartObject(id);
                        foreach (var (type, points) in skill.All.Where(t => t.Points > 0))
                        {
                            writer.WriteNumber(type.ToString().ToLowerInvariant(), points);
                        }

                        writer.WriteEndObject();
                    }

                    writer.WriteEndObject();
                }

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

                    if (modifier.Against.Oathbound)
                    {
                        writer.WriteBoolean("oathbound", true);
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
                writer.WriteNumber("grit", art.Grit);
                if (art.PerMap is { } perMap)
                {
                    writer.WriteNumber("perMap", perMap);
                }

                if (art.CostsNextPhase)
                {
                    writer.WriteBoolean("costsNextPhase", true);
                }

                if (art.Single)
                {
                    writer.WriteBoolean("single", true);
                }

                if (art.Woken)
                {
                    writer.WriteBoolean("woken", true);
                }

                if (art.Locks)
                {
                    writer.WriteBoolean("locks", true);
                }

                if (art.Item is { } item)
                {
                    writer.WriteString("item", item);
                }

                break;
            case HealArtEffect healArt:
                writer.WriteString("kind", "heal_art");
                writer.WriteString("weapon", healArt.Weapon.ToString().ToLowerInvariant());
                writer.WriteString("rank", healArt.Rank.ToString());
                writer.WriteNumber("factor", healArt.Factor);
                if (healArt.Item is { } spell)
                {
                    writer.WriteString("item", spell);
                }

                break;
            case MoveAgainEffect moveAgain:
                writer.WriteString("kind", "move_again");
                if (moveAgain.AfterHeal)
                {
                    writer.WriteString("after", "heal");
                }

                break;
            case MendingEffect mending:
                writer.WriteString("kind", "mending");
                writer.WriteNumber("factor", mending.Factor);
                writer.WriteNumber("reach", mending.Reach);
                break;
            case BraceEffect:
                writer.WriteString("kind", "brace");
                break;
            case RangeEffect reach:
                writer.WriteString("kind", "range");
                if (reach.Heals)
                {
                    writer.WriteBoolean("heals", true);
                }
                else
                {
                    writer.WriteString("weapon", reach.Weapon!.Value.ToString().ToLowerInvariant());
                }

                writer.WriteNumber("range", reach.Range);
                break;
            case KillHealEffect killHeal:
                writer.WriteString("kind", "killheal");
                writer.WriteNumber("heal", killHeal.Heal);
                if (killHeal.Wielding is { } killWielding)
                {
                    writer.WriteString("wielding", killWielding.ToString().ToLowerInvariant());
                }

                break;
            case BesideStatsEffect beside:
                writer.WriteString("kind", "beside");
                WriteStats(writer, "stats", beside.Delta);
                break;
            case AuraEffect aura:
                writer.WriteString("kind", "aura");
                writer.WriteNumber("radius", aura.Radius);
                writer.WriteNumber("hit", aura.Hit);
                writer.WriteNumber("avoid", aura.Avoid);
                break;
            case FootingEffect footing:
                writer.WriteString("kind", "footing");
                writer.WriteStartArray("terrain");
                foreach (var id in footing.Terrain)
                {
                    writer.WriteStringValue(id);
                }

                writer.WriteEndArray();
                writer.WriteNumber("cost", footing.Cost);
                break;
            case OpeningEffect opening:
                writer.WriteString("kind", "opening");
                writer.WriteNumber("def", opening.Def);
                writer.WriteNumber("res", opening.Res);
                break;
            case BiteEffect bite:
                writer.WriteString("kind", "bite");
                writer.WriteNumber("halfGrown", bite.HalfGrown);
                writer.WriteNumber("grown", bite.Grown);
                break;
            case LongCarryEffect:
                writer.WriteString("kind", "long_carry");
                break;
            case DeepRimeEffect deep:
                writer.WriteString("kind", "deep_rime");
                writer.WriteNumber("rounds", deep.Rounds);
                break;
            case DrakeFrostEffect frost:
                writer.WriteString("kind", "drake_frost");
                writer.WriteNumber("damage", frost.Damage);
                writer.WriteNumber("rest", frost.Rest);
                break;
            case StoopEffect stoop:
                writer.WriteString("kind", "stoop");
                writer.WriteNumber("flight", stoop.Flight);
                writer.WriteNumber("damage", stoop.Damage);
                break;
            case RodEffect rod:
                writer.WriteString("kind", "rod");
                writer.WriteString("school", rod.School.Label());
                writer.WriteNumber("radius", rod.Radius);
                break;
            case LineStrikeEffect line:
                writer.WriteString("kind", "line_strike");
                writer.WriteNumber("reach", line.Reach);
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
            files.Add(new ContentFile(ContentFiles.CastName, WriteArray("units", content.Cast, (writer, unit) => WriteUnit(writer, unit, content.Signatures.TryGetValue(unit.Id, out var kind) ? kind : null, content.Pronouns.TryGetValue(unit.Id, out var pronoun) ? pronoun : null))));
        }

        return files;
    }

    private static void WriteItem(Utf8JsonWriter writer, Item item)
    {
        writer.WriteStartObject();
        writer.WriteString("id", item.Id);
        writer.WriteString("name", item.Name);
        if (item.Teaches is { } teaches)
        {
            writer.WriteString("teaches", teaches.Label());
        }
        else
        {
            writer.WriteNumber("heals", item.Heals);
        }

        writer.WriteNumber("uses", item.Uses);
        if (item.Price is { } price)
        {
            writer.WriteNumber("price", price);
        }

        writer.WriteString("description", item.Description);
        writer.WriteEndObject();
    }

    /// <summary>rules.json: the wake radius, and the rivalry (issue 16), difficulties (issue 76) and schools (issue 1243) blocks when the content has them.</summary>
    private static string WriteRules(GameContent content)
    {
        if (content.Rivalry == RivalryRules.None && content.Difficulties.Count == 0 && content.Riders.Count == 0)
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
                if (content.Rivalry.SupportTiers.Count > 0)
                {
                    writer.WriteStartArray("supportTiers");
                    foreach (var tier in content.Rivalry.SupportTiers)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("tier", tier.Name);
                        writer.WriteNumber("at", tier.At);
                        writer.WriteNumber("hit", tier.Hit);
                        writer.WriteNumber("avoid", tier.Avoid);
                        writer.WriteNumber("crit", tier.Crit);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }

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

                    if (difficulty.RecallOffset != 0)
                    {
                        writer.WriteNumber("recallOffset", difficulty.RecallOffset);
                    }

                    if (difficulty.Name is { } name)
                    {
                        writer.WriteString("name", name);
                    }

                    if (difficulty.UnlockedBy is { } needed)
                    {
                        writer.WriteString("unlockedBy", needed);
                    }

                    if (difficulty.Tier != 0)
                    {
                        writer.WriteNumber("tier", difficulty.Tier);
                    }

                    if (!difficulty.LethalConfirm)
                    {
                        writer.WriteBoolean("lethalConfirm", false);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            if (content.Riders.Count > 0)
            {
                writer.WriteStartObject("schools");
                foreach (var (school, rider) in content.Riders)
                {
                    writer.WriteStartObject(school.Label());
                    writer.WriteStartObject("rider");
                    writer.WriteString("kind", SchoolRider.Label(rider.Kind));
                    if (rider.Gate is not 0)
                    {
                        if (rider.Gate is { } gate)
                        {
                            writer.WriteNumber("gate", gate);
                        }
                        else
                        {
                            writer.WriteNull("gate");
                        }
                    }

                    if (rider.Kind == RiderKind.Burn)
                    {
                        writer.WriteNumber("amount", rider.Amount);
                        writer.WriteNumber("phases", rider.Phases);
                        writer.WriteNumber("cap", rider.Cap);
                    }

                    if (rider.Kind == RiderKind.Curse)
                    {
                        writer.WriteNumber("amount", rider.Amount);
                        writer.WriteNumber("phases", rider.Phases);
                        writer.WriteNumber("blind", rider.Blind);
                    }

                    if (rider.Kind == RiderKind.Stun)
                    {
                        writer.WriteStartArray("classes");
                        foreach (var classId in rider.Classes)
                        {
                            writer.WriteStringValue(classId);
                        }

                        writer.WriteEndArray();
                    }

                    if (rider.Terrain is { } laid)
                    {
                        writer.WriteString("terrain", laid);
                    }

                    writer.WriteEndObject();
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
        if (unitClass.Advances is { } basis)
        {
            writer.WriteString("advances", basis.Id);
            if (unitClass.Unique is not null && unitClass.GrowthModifiers != basis.GrowthModifiers)
            {
                WriteStats(writer, "growthModifiers", unitClass.GrowthModifiers - basis.GrowthModifiers);
            }
        }
        else
        {
            WriteStats(writer, "growthModifiers", unitClass.GrowthModifiers);
        }
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

        if (unitClass.Hidden)
        {
            writer.WriteBoolean("hidden", true);
        }

        if (unitClass.Captain)
        {
            writer.WriteBoolean("captain", true);
        }

        if (unitClass.StrikeOnly.Count > 0)
        {
            writer.WriteStartArray("strikeOnly");
            foreach (var type in unitClass.StrikeOnly)
            {
                writer.WriteStringValue(type.ToString().ToLowerInvariant());
            }

            writer.WriteEndArray();
        }

        if (unitClass.HealOnly.Count > 0)
        {
            writer.WriteStartArray("healOnly");
            foreach (var type in unitClass.HealOnly)
            {
                writer.WriteStringValue(type.ToString().ToLowerInvariant());
            }

            writer.WriteEndArray();
        }

        if (unitClass.Unique is { } unique)
        {
            writer.WriteString("unique", unique);
        }

        if (unitClass.UnlockedBy is { } unlockedBy)
        {
            writer.WriteString("unlockedBy", unlockedBy);
        }

        if (unitClass.Loses is { } loses)
        {
            writer.WriteString("loses", loses.ToString().ToLowerInvariant());
        }

        if (unitClass.SingleStrike)
        {
            writer.WriteBoolean("singleStrike", true);
        }

        if (unitClass.Enemy)
        {
            writer.WriteBoolean("enemy", true);
        }

        if (unitClass.Description is { } description)
        {
            writer.WriteString("description", description);
        }

        if (unitClass.Schools.Count > 0)
        {
            writer.WriteStartArray("schools");
            foreach (var school in unitClass.Schools)
            {
                writer.WriteStringValue(school.Label());
            }

            writer.WriteEndArray();
        }

        if (unitClass.Grants.Count > 0)
        {
            writer.WriteStartObject("grants");
            foreach (var (type, rank) in unitClass.Grants)
            {
                writer.WriteString(type.ToString().ToLowerInvariant(), rank.ToString());
            }

            writer.WriteEndObject();
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
            if (certification.Points.Count > 0)
            {
                writer.WriteStartObject("points");
                foreach (var (type, points) in certification.Points)
                {
                    writer.WriteNumber(type.ToString().ToLowerInvariant(), points);
                }

                writer.WriteEndObject();
            }

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

        if (weapon.EffectiveAgainstHollows)
        {
            writer.WriteStringValue("hollow");
        }

        writer.WriteEndArray();
        writer.WriteBoolean("heals", weapon.Heals);
        if (weapon.Cleanses)
        {
            writer.WriteBoolean("cleanses", true);
        }
        else if (weapon.Heals)
        {
            writer.WriteNumber("healBase", weapon.HealBase);
        }

        if (weapon.AreaHeal > 0)
        {
            writer.WriteNumber("areaHeal", weapon.AreaHeal);
        }

        if (weapon.Area > 0)
        {
            writer.WriteNumber("area", weapon.Area);
        }

        if (weapon.Marks)
        {
            writer.WriteBoolean("marks", true);
        }

        if (weapon.Ignites)
        {
            writer.WriteBoolean("ignites", true);
        }

        if (weapon.Windup)
        {
            writer.WriteBoolean("windup", true);
        }

        if (weapon.Hungers)
        {
            writer.WriteBoolean("hungers", true);
        }

        if (weapon.Glass)
        {
            writer.WriteBoolean("glass", true);
        }

        if (weapon.FrozenIron)
        {
            writer.WriteBoolean("frozenIron", true);
        }

        if (weapon.School is { } school)
        {
            writer.WriteString("school", school.Label());
        }

        if (weapon.Rider is { } rider)
        {
            writer.WriteString("rider", SchoolRider.Label(rider));
        }

        if (weapon.Armor is { } armor)
        {
            writer.WriteStartObject("armor");
            writer.WriteNumber("def", armor.Def);
            writer.WriteNumber("mov", armor.Mov);
            writer.WriteNumber("phases", armor.Phases);
            if (armor.Shell)
            {
                writer.WriteBoolean("shell", true);
            }

            if (armor.Range > 0)
            {
                writer.WriteNumber("range", armor.Range);
            }

            writer.WriteEndObject();
        }

        if (weapon.MinMag is { } minMag)
        {
            writer.WriteNumber("minMag", minMag);
        }

        if (weapon.BurnStacks != 1)
        {
            writer.WriteNumber("burnStacks", weapon.BurnStacks);
        }

        if (weapon.CritAgainst.Count > 0)
        {
            writer.WriteStartArray("critAgainst");
            foreach (var movement in weapon.CritAgainst)
            {
                writer.WriteStringValue(movement.ToString().ToLowerInvariant());
            }

            writer.WriteEndArray();
            writer.WriteNumber("critBonus", weapon.CritBonus);
        }

        if (weapon.BoundTo is { } owner)
        {
            writer.WriteString("boundTo", owner);
        }

        if (weapon.Heirloom is { } ladder)
        {
            writer.WriteStartObject("heirloom");
            writer.WriteNumber("fromMap", ladder.FromMap);
            writer.WriteString("first", ladder.First);
            if (ladder.Held is { } held)
            {
                writer.WriteString("held", held);
            }

            if (ladder.Named is { } named)
            {
                writer.WriteString("named", named);
            }

            writer.WriteStartArray("stages");
            foreach (var stage in ladder.Turns)
            {
                writer.WriteStartObject();
                writer.WriteString("id", stage.Id);
                writer.WriteNumber("at", stage.At);
                writer.WriteNumber("mt", stage.Mt);
                writer.WriteNumber("hit", stage.Hit);
                writer.WriteNumber("crit", stage.Crit);
                writer.WriteNumber("wt", stage.Wt);
                writer.WriteString("description", stage.Description);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        if (weapon.Voice is { } voice)
        {
            writer.WriteStartObject("voice");
            foreach (var (name, lines) in new[] { ("starved", voice.Starved), ("tooth", voice.Tooth), ("woken", voice.Woken) })
            {
                writer.WriteStartArray(name);
                foreach (var line in lines)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", line.Id);
                    writer.WriteString("text", line.Text);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        writer.WriteString("description", weapon.Description);

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
        if (terrain.BurnPercent > 0)
        {
            writer.WriteNumber("burn", terrain.BurnPercent);
        }

        if (terrain.WearsTo is { } wearsTo)
        {
            writer.WriteString("wearsTo", wearsTo);
        }

        if (terrain.ThawsTo is { } thawsTo)
        {
            writer.WriteString("thawsTo", thawsTo);
        }

        writer.WriteEndObject();
    }

    private static void WriteUnit(Utf8JsonWriter writer, Unit unit) => WriteUnit(writer, unit, null, null);

    /// <summary>A unit entry; a cast member's <paramref name="signature"/> (DESIGN.md 13.18) and <paramref name="pronoun"/> (issue 615) are written when it has them.</summary>
    private static void WriteUnit(Utf8JsonWriter writer, Unit unit, SignatureKind? signature, Pronoun? pronoun)
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

        if (unit.Description is not null)
        {
            writer.WriteString("description", unit.Description);
        }

        if (unit.Named)
        {
            writer.WriteBoolean("named", true);
        }

        if (unit.Swallow is { } swallow)
        {
            writer.WriteStartObject("swallow");
            writer.WriteNumber("hp", swallow.Hp);
            writer.WriteNumber("def", swallow.Def);
            writer.WriteNumber("res", swallow.Res);
            writer.WriteNumber("heal", swallow.Heal);
            if (swallow.Description is not null)
            {
                writer.WriteString("description", swallow.Description);
            }

            if (swallow.Rooted)
            {
                writer.WriteBoolean("rooted", true);
            }

            if (swallow.Late)
            {
                writer.WriteBoolean("late", true);
            }

            if (swallow.Race)
            {
                writer.WriteBoolean("race", true);
            }

            if (swallow.Dose != Core.Swallow.FirstDose)
            {
                writer.WriteNumber("dose", swallow.Dose);
            }

            if (swallow.Step != Core.Swallow.DoseStep)
            {
                writer.WriteNumber("step", swallow.Step);
            }

            writer.WriteEndObject();
        }

        if (signature is { } kind)
        {
            writer.WriteString("signature", kind.ToString().ToLowerInvariant());
        }

        if (pronoun is { } said)
        {
            writer.WriteString("pronoun", said.ToString().ToLowerInvariant());
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
