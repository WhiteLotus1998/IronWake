using System.Text;
using System.Text.Json;
using Ironwake.Core;

namespace Ironwake.Content.Protocol;

/// <summary>
/// The presentation protocol's JSON (issue 25; the shapes are documented field by field in
/// docs/PROTOCOL.md): every <see cref="GameEvent"/>, every <see cref="Command"/>, a forecast,
/// a reach, and a <see cref="BattleState"/>. Field names are written by hand and never
/// reflected from the records, so renaming a record parameter cannot change the protocol;
/// a golden test per event type holds each shape. Output is compact and canonical: every
/// field is written, in a fixed order, with null where a value is absent. Enum values are
/// the enum's name in camel case (<c>player</c>, <c>twoRollAverage</c>). Consumers ignore
/// fields they do not know (<see cref="ProtocolVersion"/>), and so do the readers here.
/// </summary>
public static class ProtocolJson
{
    /// <summary>One event as a JSON object: a <c>type</c> field naming the record in camel case, its fields, then <paramref name="text"/> when given (the console's own line for the event).</summary>
    public static string Event(GameEvent e, string? text = null) => Write(w => WriteEvent(w, e, text));

    /// <summary>One command as a JSON object, the shape <see cref="ReadCommand(string)"/> reads back.</summary>
    public static string Command(Command command) => Write(w => WriteCommand(w, command));

    /// <summary>A forecast as a JSON object, the shape <see cref="ReadForecast(string)"/> reads back.</summary>
    public static string Forecast(CombatForecast forecast) => Write(w => WriteForecast(w, forecast));

    /// <summary>
    /// The whole state: the map as its canonical map text, every unit in full, the history
    /// with each prior state in the same shape (their own history empty), and the outcome.
    /// <see cref="ReadState(string, GameContent)"/> reads it back to an equal state.
    /// </summary>
    public static string State(BattleState state, GameContent content) => Write(w => WriteState(w, state, content, full: true));

    /// <summary>
    /// The state a command's answer carries: the full shape without <c>map</c> and
    /// <c>history</c>, which would make every answer grow with the battle. <c>historyCount</c>
    /// and <c>mapName</c> are written either way; a renderer asks for the full state once and
    /// follows the map's changes through <see cref="TerrainChanged"/> events.
    /// </summary>
    public static string BoardState(BattleState state, GameContent content) => Write(w => WriteState(w, state, content, full: false));

    public static void WriteEvent(Utf8JsonWriter w, GameEvent e, string? text = null)
    {
        w.WriteStartObject();
        w.WriteString("type", CamelCase(e.GetType().Name));
        switch (e)
        {
            case UnitMoved m:
                w.WriteString("unit", m.UnitId);
                WriteCoord(w, "from", m.From);
                WriteCoord(w, "to", m.To);
                WriteCoords(w, "path", m.Path);
                break;
            case CombatFought f:
                w.WriteString("attacker", f.AttackerId);
                w.WriteString("target", f.TargetId);
                w.WriteNumber("turn", f.Turn);
                w.WriteString("phase", Name(f.Phase));
                w.WriteStartArray("strikes");
                foreach (var s in f.Strikes)
                {
                    w.WriteStartObject();
                    w.WriteNumber("index", s.Index);
                    w.WriteString("attacker", s.AttackerId);
                    w.WriteString("target", s.TargetId);
                    w.WriteBoolean("hit", s.Hit);
                    w.WriteBoolean("crit", s.Crit);
                    w.WriteNumber("damage", s.Damage);
                    w.WriteNumber("targetHpAfter", s.TargetHpAfter);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WriteNumber("attackerHpAfter", f.AttackerHpAfter);
                w.WriteNumber("targetHpAfter", f.TargetHpAfter);
                break;
            case UnitDied d:
                w.WriteString("unit", d.UnitId);
                w.WriteString("side", Name(d.Side));
                WriteCoord(w, "at", d.At);
                break;
            case ExpGained x:
                w.WriteString("unit", x.UnitId);
                w.WriteNumber("amount", x.Amount);
                w.WriteNumber("expAfter", x.ExpAfter);
                break;
            case LeveledUp l:
                w.WriteString("unit", l.UnitId);
                w.WriteNumber("newLevel", l.NewLevel);
                WriteStats(w, "gains", l.Gains);
                break;
            case RankRaised k:
                w.WriteString("unit", k.UnitId);
                w.WriteString("weaponType", Name(k.Type));
                w.WriteString("rank", Name(k.Rank));
                break;
            case MasteryEarned m:
                w.WriteString("unit", m.UnitId);
                w.WriteString("class", m.ClassId);
                w.WriteString("ability", m.AbilityId);
                break;
            case UnitWaited u:
                w.WriteString("unit", u.UnitId);
                break;
            case Cantoed c:
                w.WriteString("unit", c.UnitId);
                WriteCoord(w, "from", c.From);
                WriteCoord(w, "to", c.To);
                WriteCoords(w, "path", c.Path);
                break;
            case UnitRetreated r:
                w.WriteString("unit", r.UnitId);
                WriteCoord(w, "from", r.From);
                WriteCoord(w, "to", r.To);
                break;
            case RapportGained g:
                w.WriteString("a", g.A);
                w.WriteString("b", g.B);
                w.WriteNumber("amount", g.Amount);
                w.WriteNumber("total", g.Total);
                WriteNullableNumber(w, "outOf", g.OutOf);
                break;
            case RivalryEnded r:
                w.WriteString("a", r.A);
                w.WriteString("b", r.B);
                break;
            case PhaseEnded p:
                w.WriteString("side", Name(p.Side));
                w.WriteNumber("turn", p.Turn);
                break;
            case PhaseBegan p:
                w.WriteString("side", Name(p.Side));
                w.WriteNumber("turn", p.Turn);
                break;
            case UnitHealed h:
                w.WriteString("unit", h.UnitId);
                w.WriteNumber("amount", h.Amount);
                w.WriteNumber("hpAfter", h.HpAfter);
                break;
            case Recalled r:
                w.WriteNumber("toIndex", r.ToIndex);
                w.WriteNumber("chargesLeft", r.ChargesLeft);
                break;
            case ItemUsed i:
                w.WriteString("unit", i.UnitId);
                w.WriteString("item", i.ItemId);
                w.WriteString("target", i.TargetId);
                w.WriteNumber("usesLeft", i.UsesLeft);
                break;
            case WeaponEquipped q:
                w.WriteString("unit", q.UnitId);
                w.WriteString("item", q.ItemId);
                break;
            case ArtDeclared a:
                w.WriteString("unit", a.UnitId);
                w.WriteString("art", a.ArtId);
                w.WriteString("item", a.ItemId);
                w.WriteNumber("cost", a.Cost);
                break;
            case WeaponBroke b:
                w.WriteString("unit", b.UnitId);
                w.WriteString("item", b.ItemId);
                break;
            case SpellSpent s:
                w.WriteString("unit", s.UnitId);
                w.WriteString("item", s.ItemId);
                break;
            case GroupWoke g:
                w.WriteString("group", g.Group);
                w.WriteString("cause", Name(g.Cause));
                break;
            case MapEventFired m:
                w.WriteString("name", m.Name);
                w.WriteBoolean("blocked", m.Blocked);
                break;
            case TerrainChanged t:
                WriteCoord(w, "at", t.At);
                w.WriteString("terrain", t.TerrainId);
                break;
            case UnitSpawned u:
                w.WriteString("unit", u.UnitId);
                WriteCoord(w, "at", u.At);
                w.WriteString("group", u.Group);
                w.WriteString("behavior", Name(u.Behavior));
                break;
            case FlagSet f:
                w.WriteString("flag", f.Flag);
                break;
            default:
                throw new ArgumentException($"the protocol has no shape for event {e.GetType().Name}; add one to ProtocolJson and docs/PROTOCOL.md", nameof(e));
        }

        if (text is not null)
        {
            w.WriteString("text", text);
        }

        w.WriteEndObject();
    }

    public static void WriteCommand(Utf8JsonWriter w, Command command)
    {
        w.WriteStartObject();
        switch (command)
        {
            case Move m:
                w.WriteString("type", "move");
                w.WriteString("unit", m.UnitId);
                WriteCoord(w, "to", m.To);
                break;
            case Attack a:
                w.WriteString("type", "attack");
                w.WriteString("unit", a.UnitId);
                w.WriteString("target", a.TargetId);
                WriteNullableNumber(w, "slot", a.Slot);
                if (a.Art is not null)
                {
                    w.WriteString("art", a.Art);
                }

                break;
            case UseItem i:
                w.WriteString("type", "item");
                w.WriteString("unit", i.UnitId);
                w.WriteNumber("slot", i.Slot);
                w.WriteString("target", i.TargetId);
                break;
            case Retreat r:
                w.WriteString("type", "retreat");
                w.WriteString("unit", r.UnitId);
                WriteCoord(w, "to", r.To);
                break;
            case Wait wait:
                w.WriteString("type", "wait");
                w.WriteString("unit", wait.UnitId);
                break;
            case Canto canto:
                w.WriteString("type", "canto");
                w.WriteString("unit", canto.UnitId);
                WriteCoord(w, "to", canto.To);
                break;
            case EndPhase:
                w.WriteString("type", "end");
                break;
            case Recall r:
                w.WriteString("type", "recall");
                w.WriteNumber("toIndex", r.ToIndex);
                break;
            default:
                throw new ArgumentException($"the protocol has no shape for command {command.GetType().Name}", nameof(command));
        }

        w.WriteEndObject();
    }

    /// <summary>Reads a command object. Throws <see cref="ProtocolException"/> naming the field when the object is not one.</summary>
    public static Command ReadCommand(string json)
    {
        using var doc = Parse(json);
        return ReadCommand(doc.RootElement);
    }

    public static Command ReadCommand(JsonElement e)
    {
        var type = RequiredString(e, "type");
        return type switch
        {
            "move" => new Move(RequiredString(e, "unit"), ReadCoord(e, "to")),
            "attack" => new Attack(RequiredString(e, "unit"), RequiredString(e, "target"), OptionalInt(e, "slot"), OptionalString(e, "art")),
            "item" => new UseItem(RequiredString(e, "unit"), RequiredInt(e, "slot"), OptionalString(e, "target")),
            "retreat" => new Retreat(RequiredString(e, "unit"), ReadCoord(e, "to")),
            "wait" => new Wait(RequiredString(e, "unit")),
            "canto" => new Canto(RequiredString(e, "unit"), ReadCoord(e, "to")),
            "end" => new EndPhase(),
            "recall" => new Recall(RequiredInt(e, "toIndex")),
            _ => throw new ProtocolException($"type '{type}' is not a command; expected move, attack, item, retreat, wait, canto, end, or recall"),
        };
    }

    public static void WriteForecast(Utf8JsonWriter w, CombatForecast forecast)
    {
        w.WriteStartObject();
        WriteSide(w, "attacker", forecast.Attacker);
        WriteSide(w, "defender", forecast.Defender);
        w.WriteString("scheme", Name(forecast.Scheme));
        if (forecast.ArtCost != 0)
        {
            w.WriteNumber("artCost", forecast.ArtCost);
        }

        w.WriteEndObject();
    }

    public static CombatForecast ReadForecast(string json)
    {
        using var doc = Parse(json);
        var e = doc.RootElement;
        return new CombatForecast(ReadSide(Required(e, "attacker")), ReadSide(Required(e, "defender")), ParseEnum<RollScheme>(RequiredString(e, "scheme"), "scheme"), OptionalInt(e, "artCost") ?? 0);
    }

    /// <summary>Every tile of a reach in the order the core settled them (DECISIONS/0012), each with its cost, its path from the origin, and whether the unit may end its move there.</summary>
    public static void WriteReach(Utf8JsonWriter w, Reach reach)
    {
        w.WriteStartObject();
        WriteCoord(w, "origin", reach.Origin);
        w.WriteString("movement", Name(reach.Movement));
        w.WriteNumber("mov", reach.Mov);
        w.WriteStartArray("tiles");
        foreach (var entry in reach.Entries)
        {
            w.WriteStartObject();
            w.WriteNumber("x", entry.At.X);
            w.WriteNumber("y", entry.At.Y);
            w.WriteNumber("cost", entry.Cost);
            w.WriteBoolean("canEnd", entry.CanEnd);
            WriteCoords(w, "path", entry.Path);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    public static void WriteState(Utf8JsonWriter w, BattleState state, GameContent content, bool full)
    {
        w.WriteStartObject();
        w.WriteNumber("protocolVersion", ProtocolVersion.Current);
        w.WriteNumber("rulesVersion", RulesVersion.Current);
        w.WriteString("mapName", state.Map.Name);
        if (full)
        {
            w.WriteString("map", MapFormat.Write(state.Map, content));
        }

        w.WriteNumber("turn", state.Turn);
        w.WriteString("phase", Name(state.Phase));
        w.WriteString("seed", state.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        w.WriteString("scheme", Name(state.Scheme));
        w.WriteNumber("recallCharges", state.RecallCharges);
        w.WriteStartArray("units");
        foreach (var unit in state.Units)
        {
            WriteUnit(w, unit, content);
        }

        w.WriteEndArray();
        WriteStrings(w, "awakeGroups", state.AwakeGroups);
        WriteStrings(w, "fired", state.Fired);
        WriteStrings(w, "flags", state.Flags);
        w.WriteStartArray("rapport");
        foreach (var r in state.Rapport)
        {
            w.WriteStartObject();
            w.WriteString("a", r.A);
            w.WriteString("b", r.B);
            w.WriteNumber("points", r.Points);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        var outcome = state.Outcome;
        w.WriteStartObject("outcome");
        w.WriteString("result", Name(outcome.Result));
        w.WriteString("reason", outcome.Reason);
        w.WriteString("cause", Name(outcome.Cause));
        w.WriteEndObject();
        w.WriteNumber("historyCount", state.History.Count);
        if (full)
        {
            w.WriteStartArray("history");
            foreach (var prior in state.History)
            {
                WriteState(w, prior, content, full: true);
            }

            w.WriteEndArray();
        }

        w.WriteEndObject();
    }

    /// <summary>
    /// Reads a full state (<see cref="State"/>) back. The map text is parsed against
    /// <paramref name="content"/>, so a state names content the reader must have. A state
    /// written by another protocol version is refused rather than read into a different game.
    /// Derived fields (<c>outcome</c>, <c>maxHp</c>, <c>historyCount</c>) are not read.
    /// </summary>
    public static BattleState ReadState(string json, GameContent content)
    {
        using var doc = Parse(json);
        return ReadState(doc.RootElement, content);
    }

    public static BattleState ReadState(JsonElement e, GameContent content)
    {
        var version = RequiredInt(e, "protocolVersion");
        if (version != ProtocolVersion.Current)
        {
            throw new ProtocolException($"protocolVersion {version} is not this build's {ProtocolVersion.Current}");
        }

        if (!e.TryGetProperty("map", out var mapText) || mapText.ValueKind != JsonValueKind.String)
        {
            throw new ProtocolException("field 'map' is missing: only a full state (the state query's answer) can be read back");
        }

        MapDefinition map;
        try
        {
            map = MapFormat.Parse("protocol state", mapText.GetString()!, content);
        }
        catch (MapException ex)
        {
            throw new ProtocolException("field 'map': " + ex.Message);
        }

        if (!ulong.TryParse(RequiredString(e, "seed"), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seed))
        {
            throw new ProtocolException("field 'seed' is not an unsigned integer");
        }

        var history = new List<BattleState>();
        if (e.TryGetProperty("history", out var priors))
        {
            foreach (var prior in Array(priors, "history"))
            {
                history.Add(ReadState(prior, content));
            }
        }

        return new BattleState(
            map,
            ValueList<BattleUnit>.From(Array(Required(e, "units"), "units").Select(u => ReadUnit(u, content))),
            RequiredInt(e, "turn"),
            ParseEnum<Side>(RequiredString(e, "phase"), "phase"),
            seed,
            ParseEnum<RollScheme>(RequiredString(e, "scheme"), "scheme"),
            RequiredInt(e, "recallCharges"),
            ValueList<BattleState>.From(history),
            ReadStrings(e, "awakeGroups"),
            ReadStrings(e, "fired"),
            ReadStrings(e, "flags"),
            ValueList<Rapport>.From(Array(Required(e, "rapport"), "rapport").Select(r => new Rapport(RequiredString(r, "a"), RequiredString(r, "b"), RequiredInt(r, "points")))));
    }

    private static void WriteUnit(Utf8JsonWriter w, BattleUnit unit, GameContent content)
    {
        var u = unit.Unit;
        w.WriteStartObject();
        w.WriteString("id", u.Id);
        w.WriteString("name", u.Name);
        w.WriteString("side", Name(unit.Side));
        WriteCoord(w, "at", unit.At);
        w.WriteNumber("hp", unit.Hp);
        w.WriteNumber("maxHp", unit.MaxHp(content));
        w.WriteBoolean("moved", unit.Moved);
        w.WriteBoolean("acted", unit.Acted);
        w.WriteString("group", unit.Group);
        if (unit.Behavior is { } behavior)
        {
            w.WriteString("behavior", Name(behavior));
        }
        else
        {
            w.WriteNull("behavior");
        }

        w.WriteBoolean("isBoss", unit.IsBoss);
        w.WriteBoolean("isCaptain", unit.IsCaptain);
        w.WriteNumber("placementIndex", unit.PlacementIndex);
        w.WriteBoolean("retreated", unit.Retreated);
        WriteNullableNumber(w, "canto", unit.Canto);
        w.WriteString("class", u.ClassId);
        w.WriteNumber("level", u.Level);
        w.WriteNumber("exp", u.Exp);
        WriteStats(w, "stats", u.Stats);
        WriteStats(w, "growths", u.Growths);
        w.WriteStartArray("inventory");
        foreach (var stack in u.Inventory.Items)
        {
            w.WriteStartObject();
            w.WriteString("item", stack.ItemId);
            w.WriteNumber("uses", stack.Uses);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        WriteStrings(w, "abilities", u.Abilities);
        w.WriteString("region", u.Region);
        w.WriteString("personality", u.Personality);
        WriteStrings(w, "hooks", u.Hooks);
        w.WriteStartObject("weaponPoints");
        foreach (var (type, points) in u.Skill.All)
        {
            w.WriteNumber(Name(type), points);
        }

        w.WriteEndObject();
        w.WriteStartObject("masteryPoints");
        foreach (var (classId, points) in u.Mastery.All)
        {
            w.WriteNumber(classId, points);
        }

        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static BattleUnit ReadUnit(JsonElement e, GameContent content)
    {
        var id = RequiredString(e, "id");
        var classId = RequiredString(e, "class");
        if (!content.Classes.ContainsKey(classId))
        {
            throw new ProtocolException($"unit '{id}': class '{classId}' is not in the content");
        }

        Unit unit;
        try
        {
            unit = new Unit(
                id,
                RequiredString(e, "name"),
                classId,
                RequiredInt(e, "level"),
                RequiredInt(e, "exp"),
                ReadStats(Required(e, "stats")),
                ReadStats(Required(e, "growths")),
                new Inventory(ValueList<ItemStack>.From(Array(Required(e, "inventory"), "inventory").Select(s => new ItemStack(RequiredString(s, "item"), RequiredInt(s, "uses"))))),
                ReadStrings(e, "abilities"),
                OptionalString(e, "region"),
                OptionalString(e, "personality"))
            {
                Hooks = ReadStrings(e, "hooks"),
                Skill = ReadWeaponPoints(e),
                Mastery = ReadMasteryPoints(e),
            };
        }
        catch (ArgumentException ex)
        {
            throw new ProtocolException($"unit '{id}': {ex.Message}");
        }

        var behavior = OptionalString(e, "behavior");
        return new BattleUnit(
            unit,
            ParseEnum<Side>(RequiredString(e, "side"), "side"),
            ReadCoord(e, "at"),
            RequiredInt(e, "hp"),
            RequiredBool(e, "moved"),
            RequiredBool(e, "acted"),
            OptionalString(e, "group"),
            behavior is null ? null : ParseEnum<Behavior>(behavior, "behavior"),
            RequiredBool(e, "isBoss"),
            RequiredBool(e, "isCaptain"),
            RequiredInt(e, "placementIndex"),
            RequiredBool(e, "retreated"),
            OptionalInt(e, "canto"));
    }

    private static void WriteSide(Utf8JsonWriter w, string name, SideForecast side)
    {
        w.WriteStartObject(name);
        w.WriteBoolean("strikes", side.Strikes);
        w.WriteNumber("damage", side.Damage);
        w.WriteNumber("hitChance", side.HitChance);
        w.WriteNumber("displayedHit", side.DisplayedHit);
        w.WriteNumber("critChance", side.CritChance);
        w.WriteBoolean("doubles", side.Doubles);
        w.WriteNumber("strikesPerRound", side.StrikesPerRound);
        w.WriteEndObject();
    }

    private static SideForecast ReadSide(JsonElement e) => new(
        RequiredBool(e, "strikes"), RequiredInt(e, "damage"), RequiredInt(e, "hitChance"), RequiredInt(e, "displayedHit"), RequiredInt(e, "critChance"), RequiredBool(e, "doubles"), OptionalInt(e, "strikesPerRound") ?? 1);

    private static readonly string[] StatKeys = { "hp", "str", "mag", "dex", "spd", "lck", "def", "res", "cha" };

    private static void WriteStats(Utf8JsonWriter w, string name, Stats stats)
    {
        w.WriteStartObject(name);
        for (var i = 0; i < StatKeys.Length; i++)
        {
            w.WriteNumber(StatKeys[i], stats.Get(Stats.All[i]));
        }

        w.WriteEndObject();
    }

    private static Stats ReadStats(JsonElement e)
    {
        var stats = Stats.Zero;
        for (var i = 0; i < StatKeys.Length; i++)
        {
            stats = stats.With(Stats.All[i], RequiredInt(e, StatKeys[i]));
        }

        return stats;
    }

    private static void WriteCoord(Utf8JsonWriter w, string name, Coord at)
    {
        w.WriteStartObject(name);
        w.WriteNumber("x", at.X);
        w.WriteNumber("y", at.Y);
        w.WriteEndObject();
    }

    private static void WriteCoords(Utf8JsonWriter w, string name, IEnumerable<Coord> coords)
    {
        w.WriteStartArray(name);
        foreach (var at in coords)
        {
            w.WriteStartObject();
            w.WriteNumber("x", at.X);
            w.WriteNumber("y", at.Y);
            w.WriteEndObject();
        }

        w.WriteEndArray();
    }

    private static void WriteStrings(Utf8JsonWriter w, string name, IEnumerable<string> values)
    {
        w.WriteStartArray(name);
        foreach (var value in values)
        {
            w.WriteStringValue(value);
        }

        w.WriteEndArray();
    }

    private static void WriteNullableNumber(Utf8JsonWriter w, string name, int? value)
    {
        if (value is { } v)
        {
            w.WriteNumber(name, v);
        }
        else
        {
            w.WriteNull(name);
        }
    }

    /// <summary>An enum value's protocol name: its C# name with the first letter lower-cased.</summary>
    public static string Name<T>(T value)
        where T : struct, Enum => CamelCase(value.ToString());

    private static string CamelCase(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private static T ParseEnum<T>(string text, string field)
        where T : struct, Enum
    {
        foreach (var value in Enum.GetValues<T>())
        {
            if (Name(value) == text)
            {
                return value;
            }
        }

        throw new ProtocolException($"field '{field}': '{text}' is not one of: {string.Join(", ", Enum.GetValues<T>().Select(v => Name(v)))}");
    }

    public static string Write(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static JsonDocument Parse(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ProtocolException("not JSON: " + ex.Message);
        }
    }

    private static JsonElement Required(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object)
        {
            throw new ProtocolException($"expected an object holding '{name}', got {e.ValueKind.ToString().ToLowerInvariant()}");
        }

        if (!e.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            throw new ProtocolException($"field '{name}' is missing");
        }

        return value;
    }

    public static string RequiredString(JsonElement e, string name)
    {
        var value = Required(e, name);
        return value.ValueKind == JsonValueKind.String ? value.GetString()! : throw new ProtocolException($"field '{name}' is not a string");
    }

    public static string? OptionalString(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : throw new ProtocolException($"field '{name}' is not a string");
    }

    public static int RequiredInt(JsonElement e, string name)
    {
        var value = Required(e, name);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) ? n : throw new ProtocolException($"field '{name}' is not an integer");
    }

    public static int? OptionalInt(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) ? n : throw new ProtocolException($"field '{name}' is not an integer");
    }

    private static bool RequiredBool(JsonElement e, string name)
    {
        var value = Required(e, name);
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new ProtocolException($"field '{name}' is not true or false"),
        };
    }

    public static Coord ReadCoord(JsonElement e, string name)
    {
        var value = Required(e, name);
        return new Coord(RequiredInt(value, "x"), RequiredInt(value, "y"));
    }

    public static Coord? OptionalCoord(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? ReadCoord(e, name) : null;

    private static IEnumerable<JsonElement> Array(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Array ? e.EnumerateArray().ToList() : throw new ProtocolException($"field '{name}' is not an array");

    /// <summary>A unit's <c>weaponPoints</c> (issue 67); a state written before the field existed reads as rank E in everything.</summary>
    private static WeaponSkill ReadWeaponPoints(JsonElement e)
    {
        if (!e.TryGetProperty("weaponPoints", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return WeaponSkill.Zero;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ProtocolException("field 'weaponPoints' is not an object");
        }

        var skill = WeaponSkill.Zero;
        foreach (var property in value.EnumerateObject())
        {
            var points = RequiredInt(value, property.Name);
            if (points < 0)
            {
                throw new ProtocolException($"field 'weaponPoints.{property.Name}' is below 0");
            }

            skill = skill.With(ParseEnum<WeaponType>(property.Name, "weaponPoints"), points);
        }

        return skill;
    }

    /// <summary>A unit's <c>masteryPoints</c> (issue 69), class id to points; a state written before the field existed reads as none.</summary>
    private static MasteryProgress ReadMasteryPoints(JsonElement e)
    {
        if (!e.TryGetProperty("masteryPoints", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return MasteryProgress.Empty;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ProtocolException("field 'masteryPoints' is not an object");
        }

        var progress = MasteryProgress.Empty;
        foreach (var property in value.EnumerateObject())
        {
            var points = RequiredInt(value, property.Name);
            if (points < 0)
            {
                throw new ProtocolException($"field 'masteryPoints.{property.Name}' is below 0");
            }

            progress = progress.With(property.Name, points);
        }

        return progress;
    }

    private static ValueList<string> ReadStrings(JsonElement e, string name) =>
        ValueList<string>.From(Array(Required(e, name), name).Select(v => v.ValueKind == JsonValueKind.String ? v.GetString()! : throw new ProtocolException($"field '{name}' holds a value that is not a string")));
}

/// <summary>A protocol message that could not be read: bad JSON, or a field missing or of the wrong kind, named in the message.</summary>
public sealed class ProtocolException : Exception
{
    public ProtocolException(string message)
        : base(message)
    {
    }
}
