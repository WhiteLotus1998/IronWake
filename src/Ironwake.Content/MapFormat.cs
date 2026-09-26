using System.Text;
using Ironwake.Core;

namespace Ironwake.Content;

/// <summary>
/// Reads and writes the <c>.map</c> text format from DESIGN.md section 10: a header of
/// <c>key: value</c> lines, a blank line, the terrain grid, a blank line, then a
/// <c>units:</c> block, then an optional <c>events:</c> block (issue 32). <see cref="Write"/> is canonical (fixed header order, every
/// default written out), so <c>Write(Parse(text))</c> is a fixed point and a hand-edited
/// file can be checked against it. Nothing here touches the disk; <see cref="MapFiles"/> does.
/// </summary>
public static class MapFormat
{
    /// <summary>The largest <c>supplies:</c> cap; above every consumable's uses, so a cap this high never binds.</summary>
    private const int MaxSupplies = 99;

    private static readonly string[] HeaderKeys = { "name", "size", "win", "turn_limit", "recall", "enemy_level", "exit", "protect", "cheap_shots", "retreat", "rivalry", "supplies", "announce", "keepsakes", "difficulty", "certification" };

    /// <summary>Parses map text. <paramref name="file"/> is only used in error messages.</summary>
    public static MapDefinition Parse(string file, string text, GameContent content)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var parser = new Parser(file, lines, content);
        return parser.Parse();
    }

    /// <summary>Writes a map in canonical form. Parsing the result gives back an equal map.</summary>
    public static string Write(MapDefinition map, GameContent content)
    {
        var sb = new StringBuilder();
        sb.Append("name: ").Append(map.Name).Append('\n');
        sb.Append("size: ").Append(map.Width).Append('x').Append(map.Height).Append('\n');
        sb.Append("win: ").Append(MapRenderer.WinName(map.Win)).Append('\n');
        sb.Append("turn_limit: ").Append(map.TurnLimit).Append('\n');
        sb.Append("recall: ").Append(map.RecallCharges).Append('\n');
        sb.Append("enemy_level: ").Append(map.EnemyLevel).Append('\n');
        if (map.Exits.Count > 0)
        {
            sb.Append("exit: ").Append(string.Join(' ', map.Exits)).Append('\n');
        }

        if (map.ProtectId is not null)
        {
            sb.Append("protect: ").Append(map.ProtectId).Append('\n');
        }

        if (map.CheapShotsAllowed)
        {
            sb.Append("cheap_shots: allowed\n");
        }

        if (map.RetreatEnabled)
        {
            sb.Append("retreat: on\n");
        }

        if (map.RivalryArm is { } arm)
        {
            sb.Append("rivalry: ").Append(arm).Append('\n');
        }

        if (map.Supplies is { } supplies)
        {
            sb.Append("supplies: ").Append(supplies).Append('\n');
        }

        if (map.Announced)
        {
            sb.Append("announce: on\n");
        }

        if (map.KeepsakesEnabled)
        {
            sb.Append("keepsakes: on\n");
        }

        if (map.DifficultyId is { } difficulty)
        {
            sb.Append("difficulty: ").Append(difficulty).Append('\n');
        }

        if (map.Certification is { } trial)
        {
            sb.Append("certification: ").Append(trial.ClassId);
            foreach (var item in trial.Loadout)
            {
                sb.Append(' ').Append(item);
            }

            sb.Append('\n');
        }

        sb.Append('\n');
        for (var y = 0; y < map.Height; y++)
        {
            sb.Append(map.GlyphRow(y, content)).Append('\n');
        }

        sb.Append('\n');
        sb.Append("units:\n");
        foreach (var placement in map.Placements)
        {
            sb.Append(WriteUnitLine(placement)).Append('\n');
        }

        if (map.Events.Count > 0)
        {
            sb.Append('\n');
            sb.Append("events:\n");
            foreach (var mapEvent in map.Events)
            {
                sb.Append(WriteEventLine(mapEvent, content)).Append('\n');
            }
        }

        return sb.ToString();
    }

    private static string WriteEventLine(MapEvent mapEvent, GameContent content)
    {
        var trigger = mapEvent.Trigger switch
        {
            TurnTrigger t => "turn " + t.Turn + " " + t.Phase.ToString().ToLowerInvariant(),
            EnterTrigger e => "enter " + e.At,
            _ => throw new ArgumentOutOfRangeException(nameof(mapEvent), mapEvent.Trigger, "unknown map event trigger"),
        };
        var action = mapEvent.Action switch
        {
            ChangeTerrain c => "terrain " + c.At + " " + content.TerrainById(c.TerrainId).Glyph,
            SpawnEnemy s => "spawn " + s.Placement.TemplateId + " " + s.Placement.At + " group:" + s.Placement.Group + " behavior:" + s.Placement.Behavior.ToString().ToLowerInvariant(),
            SetFlag f => "flag " + f.Flag,
            _ => throw new ArgumentOutOfRangeException(nameof(mapEvent), mapEvent.Action, "unknown map event action"),
        };
        return mapEvent.Name + " " + trigger + " " + action;
    }

    private static string WriteUnitLine(Placement placement) => placement switch
    {
        PlayerPlacement { Slot: PlayerSlot.Captain } p => "P captain " + p.At,
        PlayerPlacement { Slot: PlayerSlot.NamedRecruit } p => "P recruit:" + p.RecruitId + " " + p.At,
        PlayerPlacement p => "P recruit " + p.At,
        EnemyPlacement { IsBoss: true } e => "B " + e.TemplateId + " " + e.At + " group:" + e.Group + " behavior:" + (e.Behavior == Behavior.Guard ? "guard" : "boss"),
        EnemyPlacement e => "E " + e.TemplateId + " " + e.At + " group:" + e.Group + " behavior:" + e.Behavior.ToString().ToLowerInvariant(),
        _ => throw new ArgumentOutOfRangeException(nameof(placement), placement, "unknown placement kind"),
    };

    private sealed class Parser
    {
        private readonly string _file;
        private readonly string[] _lines;
        private readonly GameContent _content;
        private int _index;

        public Parser(string file, string[] lines, GameContent content)
        {
            _file = file;
            _lines = lines;
            _content = content;
        }

        private int LineNumber => _index + 1;

        private MapException Error(string problem) => new(_file, LineNumber, problem);

        private MapException ErrorAt(int line, string problem) => new(_file, line, problem);

        private bool AtEnd => _index >= _lines.Length;

        private string Current => _lines[_index];

        public MapDefinition Parse()
        {
            if (_lines.All(string.IsNullOrWhiteSpace))
            {
                throw new MapException(_file, 0, "file is empty");
            }

            var header = ParseHeader();
            var name = Require(header, "name");
            var (width, height) = ParseSize(header);
            var win = ParseWin(header);
            var turnLimit = ParseInt(header, "turn_limit", 1, 999, required: true, fallback: 0);
            var recall = ParseInt(header, "recall", 0, 99, required: false, fallback: MapDefinition.DefaultRecallCharges);
            var enemyLevel = ParseInt(header, "enemy_level", Unit.MinLevel, Unit.MaxLevel, required: false, fallback: MapDefinition.DefaultEnemyLevel);
            var cheapShots = ParseCheapShots(header);
            var retreat = ParseOn(header, "retreat");
            var rivalry = ParseRivalry(header);
            int? supplies = header.ContainsKey("supplies") ? ParseInt(header, "supplies", 1, MaxSupplies, required: true, fallback: 0) : null;
            var announce = ParseOn(header, "announce");
            var keepsakes = ParseOn(header, "keepsakes");
            var exits = ParseExits(header, width, height);
            var protect = header.TryGetValue("protect", out var protectEntry) ? protectEntry.Value : null;
            var difficulty = ParseDifficulty(header);
            var certification = ParseCertification(header);

            SkipBlankLines();
            var terrain = ParseGrid(width, height);
            SkipBlankLines();
            var placements = ParseUnits(width, height, terrain);
            var events = ParseEvents(width, height, terrain, turnLimit);
            if (announce && events.Count == 0)
            {
                throw ErrorAt(header["announce"].Line, "announce: on needs an events: block to announce");
            }

            var map = new MapDefinition(name, width, height, win, turnLimit, recall, enemyLevel, cheapShots, terrain, placements, exits, protect, events, retreat, rivalry, supplies, difficulty, certification, announce, keepsakes);
            Validate(map);
            return map;
        }

        private Dictionary<string, (string Value, int Line)> ParseHeader()
        {
            var header = new Dictionary<string, (string, int)>(StringComparer.Ordinal);
            while (!AtEnd && !string.IsNullOrWhiteSpace(Current))
            {
                var colon = Current.IndexOf(':');
                if (colon < 0)
                {
                    throw Error("expected 'key: value' in the header, or a blank line before the grid");
                }

                var key = Current[..colon].Trim();
                var value = Current[(colon + 1)..].Trim();
                if (Array.IndexOf(HeaderKeys, key) < 0)
                {
                    throw Error($"unknown header key '{key}'; expected one of {string.Join(", ", HeaderKeys)}");
                }

                if (header.ContainsKey(key))
                {
                    throw Error($"header key '{key}' appears twice");
                }

                if (value.Length == 0)
                {
                    throw Error($"header key '{key}' has no value");
                }

                header[key] = (value, LineNumber);
                _index++;
            }

            return header;
        }

        private string Require(Dictionary<string, (string Value, int Line)> header, string key)
        {
            if (!header.TryGetValue(key, out var entry))
            {
                throw new MapException(_file, 0, $"header is missing '{key}'");
            }

            return entry.Value;
        }

        private (int, int) ParseSize(Dictionary<string, (string Value, int Line)> header)
        {
            var (value, line) = header.TryGetValue("size", out var e) ? e : throw new MapException(_file, 0, "header is missing 'size'");
            var parts = value.Split('x');
            if (parts.Length != 2
                || !int.TryParse(parts[0], out var width)
                || !int.TryParse(parts[1], out var height))
            {
                throw ErrorAt(line, $"size must be WIDTHxHEIGHT, got '{value}'");
            }

            if (width < 1 || height < 1 || width > MapDefinition.MaxSide || height > MapDefinition.MaxSide)
            {
                throw ErrorAt(line, $"size must be 1..{MapDefinition.MaxSide} on each side, got {width}x{height}");
            }

            return (width, height);
        }

        private WinCondition ParseWin(Dictionary<string, (string Value, int Line)> header)
        {
            var (value, line) = header.TryGetValue("win", out var e) ? e : throw new MapException(_file, 0, "header is missing 'win'");
            foreach (var win in Enum.GetValues<WinCondition>())
            {
                if (MapRenderer.WinName(win) == value)
                {
                    return win;
                }
            }

            var allowed = string.Join(", ", Enum.GetValues<WinCondition>().Select(MapRenderer.WinName));
            throw ErrorAt(line, $"win must be one of {allowed}, got '{value}'");
        }

        private int ParseInt(Dictionary<string, (string Value, int Line)> header, string key, int min, int max, bool required, int fallback)
        {
            if (!header.TryGetValue(key, out var entry))
            {
                return required ? throw new MapException(_file, 0, $"header is missing '{key}'") : fallback;
            }

            if (!int.TryParse(entry.Value, out var value) || value < min || value > max)
            {
                throw ErrorAt(entry.Line, $"{key} must be an integer {min}..{max}, got '{entry.Value}'");
            }

            return value;
        }

        /// <summary>The <c>exit:</c> header: tiles separated by spaces, each inside the grid, none twice.</summary>
        private ValueList<Coord> ParseExits(Dictionary<string, (string Value, int Line)> header, int width, int height)
        {
            if (!header.TryGetValue("exit", out var entry))
            {
                return ValueList<Coord>.Empty;
            }

            var exits = new List<Coord>();
            foreach (var token in entry.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.Split(',');
                if (parts.Length != 2 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
                {
                    throw ErrorAt(entry.Line, $"exit must list tiles as x,y separated by spaces, got '{token}'");
                }

                var at = new Coord(x, y);
                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    throw ErrorAt(entry.Line, $"exit {at} is outside the {width}x{height} grid");
                }

                if (exits.Contains(at))
                {
                    throw ErrorAt(entry.Line, $"exit {at} is listed twice");
                }

                exits.Add(at);
            }

            return ValueList<Coord>.From(exits);
        }

        private bool ParseCheapShots(Dictionary<string, (string Value, int Line)> header)
        {
            if (!header.TryGetValue("cheap_shots", out var entry))
            {
                return false;
            }

            if (entry.Value != "allowed")
            {
                throw ErrorAt(entry.Line, $"cheap_shots may only be 'allowed' (or absent), got '{entry.Value}'");
            }

            return true;
        }

        private bool ParseOn(Dictionary<string, (string Value, int Line)> header, string key)
        {
            if (!header.TryGetValue(key, out var entry))
            {
                return false;
            }

            if (entry.Value != "on")
            {
                throw ErrorAt(entry.Line, $"{key} may only be 'on' (or absent), got '{entry.Value}'");
            }

            return true;
        }

        private string? ParseRivalry(Dictionary<string, (string Value, int Line)> header)
        {
            if (!header.TryGetValue("rivalry", out var entry))
            {
                return null;
            }

            if (_content.Rivalry.Arm(entry.Value) is null)
            {
                var arms = _content.Rivalry.Arms.Count == 0
                    ? "rules.json has no rivalry arms"
                    : "the arms are " + string.Join(", ", _content.Rivalry.Arms.Select(a => a.Id));
                throw ErrorAt(entry.Line, $"rivalry names arm '{entry.Value}'; {arms}");
            }

            return entry.Value;
        }

        /// <summary>
        /// The <c>difficulty:</c> header (issue 76): a difficulty in rules.json that the header's
        /// enemy level and Recall charges already include. Written only for a map played under one,
        /// so a battle state reads back under it; <see cref="MapFiles.LoadAll"/> refuses it in content.
        /// </summary>
        private string? ParseDifficulty(Dictionary<string, (string Value, int Line)> header)
        {
            if (!header.TryGetValue("difficulty", out var entry))
            {
                return null;
            }

            if (!_content.Difficulties.ContainsKey(entry.Value))
            {
                var known = _content.Difficulties.Count == 0
                    ? "rules.json declares no difficulties"
                    : "the difficulties are " + string.Join(", ", _content.Difficulties.Keys);
                throw ErrorAt(entry.Line, $"difficulty names '{entry.Value}'; {known}");
            }

            return entry.Value;
        }

        /// <summary>
        /// The <c>certification:</c> header (issue 73): a class in classes.json, then one to
        /// <see cref="Inventory.Capacity"/> weapon or item ids, the trial's fixed loadout.
        /// </summary>
        private CertificationTrial? ParseCertification(Dictionary<string, (string Value, int Line)> header)
        {
            if (!header.TryGetValue("certification", out var entry))
            {
                return null;
            }

            var words = entry.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2)
            {
                throw ErrorAt(entry.Line, "certification must be '<class> <item> [<item> ...]': the class it grants and the loadout the trial is played with");
            }

            if (!_content.Classes.TryGetValue(words[0], out var trialClass))
            {
                throw ErrorAt(entry.Line, $"certification names class '{words[0]}', which is not in classes.json");
            }

            var loadout = words[1..];
            if (loadout.Length > Inventory.Capacity)
            {
                throw ErrorAt(entry.Line, $"certification loadout holds at most {Inventory.Capacity} items, got {loadout.Length}");
            }

            foreach (var item in loadout)
            {
                if (!_content.Weapons.ContainsKey(item) && !_content.Items.ContainsKey(item))
                {
                    throw ErrorAt(entry.Line, $"certification loadout names '{item}', which is not a weapon in weapons.json or an item in items.json");
                }

                if (_content.Weapons.TryGetValue(item, out var weapon) && !trialClass.CanUse(weapon.Type))
                {
                    throw ErrorAt(entry.Line, $"certification loadout names '{item}', a {weapon.Type.ToString().ToLowerInvariant()}, which a {trialClass.Name} cannot wield");
                }
            }

            return new CertificationTrial(words[0], ValueList<string>.From(loadout));
        }

        private void SkipBlankLines()
        {
            while (!AtEnd && string.IsNullOrWhiteSpace(Current))
            {
                _index++;
            }
        }

        private ValueList<string> ParseGrid(int width, int height)
        {
            var ids = new string[width * height];
            for (var y = 0; y < height; y++)
            {
                if (AtEnd || string.IsNullOrWhiteSpace(Current))
                {
                    throw Error($"grid has {y} rows, size says {height}");
                }

                var row = Current;
                if (row.Length != width)
                {
                    throw Error($"grid row is {row.Length} wide, size says {width}");
                }

                for (var x = 0; x < width; x++)
                {
                    var terrain = _content.TerrainByGlyph(row[x])
                        ?? throw Error($"unknown terrain glyph '{row[x]}' at column {x}");
                    ids[y * width + x] = terrain.Id;
                }

                _index++;
            }

            if (!AtEnd && !string.IsNullOrWhiteSpace(Current) && Current != "units:")
            {
                throw Error($"grid has more than {height} rows, size says {height}");
            }

            return ValueList<string>.From(ids);
        }

        private ValueList<Placement> ParseUnits(int width, int height, ValueList<string> terrain)
        {
            if (AtEnd)
            {
                throw new MapException(_file, 0, "missing 'units:' block after the grid");
            }

            if (Current.Trim() != "units:")
            {
                throw Error("expected 'units:' after the grid");
            }

            _index++;
            var placements = new List<Placement>();
            var occupied = new Dictionary<Coord, int>();
            for (; !AtEnd && Current.Trim() != "events:"; _index++)
            {
                if (string.IsNullOrWhiteSpace(Current))
                {
                    continue;
                }

                var placement = ParseUnitLine(Current.Trim(), width, height, terrain);
                if (occupied.TryGetValue(placement.At, out var otherLine))
                {
                    throw Error($"tile {placement.At} is already occupied by the unit on line {otherLine}");
                }

                occupied[placement.At] = LineNumber;
                placements.Add(placement);
            }

            return ValueList<Placement>.From(placements);
        }

        private Placement ParseUnitLine(string line, int width, int height, ValueList<string> terrain)
        {
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens[0] is not ("P" or "E" or "B"))
            {
                throw Error($"unit line must start with P, E, or B, got '{tokens[0]}'");
            }

            if (tokens.Length < 3)
            {
                throw Error("unit line needs a prefix, a unit, and a position: 'P captain 1,8'");
            }

            var at = ParseCoord(tokens[2], width, height);
            var attributes = ParseAttributes(tokens.Skip(3));
            var tile = _content.TerrainById(terrain[at.Y * width + at.X]);

            if (tokens[0] == "P")
            {
                if (attributes.Count > 0)
                {
                    throw Error($"player units take no attributes, got '{attributes.Keys.First()}:'");
                }

                if (!tile.IsPassable(MovementType.Infantry))
                {
                    throw Error($"player unit cannot start on {tile.Name} at {at}");
                }

                return ParsePlayer(tokens[1], at);
            }

            var isBoss = tokens[0] == "B";
            if (!_content.Units.TryGetValue(tokens[1], out var template))
            {
                throw Error($"unknown enemy template '{tokens[1]}'");
            }

            var movement = _content.Class(template.ClassId).Movement;
            if (!tile.IsPassable(movement))
            {
                throw Error($"{template.Name} ({movement.ToString().ToLowerInvariant()}) cannot start on {tile.Name} at {at}");
            }

            if (!attributes.TryGetValue("group", out var group))
            {
                throw Error("enemy line needs 'group:<name>'");
            }

            Behavior behavior;
            if (isBoss)
            {
                behavior = attributes.TryGetValue("behavior", out var given)
                    ? given switch
                    {
                        "boss" => Behavior.Boss,
                        "guard" => Behavior.Guard,
                        _ => throw Error($"a B line's behavior is boss or guard, got '{given}'"),
                    }
                    : Behavior.Boss;
            }
            else
            {
                if (!attributes.TryGetValue("behavior", out var given))
                {
                    throw Error("enemy line needs 'behavior:aggressive', 'behavior:hold', or 'behavior:guard'");
                }

                behavior = given switch
                {
                    "aggressive" => Behavior.Aggressive,
                    "hold" => Behavior.Hold,
                    "guard" => Behavior.Guard,
                    "boss" => throw Error("behavior boss belongs on a B line, not an E line"),
                    _ => throw Error($"unknown behavior '{given}'; expected aggressive, hold, or guard"),
                };
            }

            return new EnemyPlacement(at, tokens[1], group, behavior, isBoss);
        }

        /// <summary>
        /// The optional <c>events:</c> block after the units (issue 32): one line per event,
        /// <c>name trigger action</c>. Triggers are <c>turn N player|enemy</c> and
        /// <c>enter x,y</c>; actions are <c>terrain x,y glyph</c>, <c>spawn template x,y
        /// group:g behavior:b</c> on an edge tile, and <c>flag name</c>. Names are unique.
        /// </summary>
        private ValueList<MapEvent> ParseEvents(int width, int height, ValueList<string> terrain, int turnLimit)
        {
            if (AtEnd)
            {
                return ValueList<MapEvent>.Empty;
            }

            _index++;
            var events = new List<MapEvent>();
            var names = new Dictionary<string, int>(StringComparer.Ordinal);
            for (; !AtEnd; _index++)
            {
                if (string.IsNullOrWhiteSpace(Current))
                {
                    continue;
                }

                var tokens = Current.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var name = tokens[0];
                if (name.Contains(':'))
                {
                    throw Error($"an event line starts with its name, got '{name}'");
                }

                if (names.TryGetValue(name, out var otherLine))
                {
                    throw Error($"event name '{name}' is already used on line {otherLine}");
                }

                names[name] = LineNumber;
                var (trigger, rest) = ParseTrigger(tokens, width, height, turnLimit);
                var action = ParseAction(rest, width, height, terrain);
                events.Add(new MapEvent(name, trigger, action));
            }

            return ValueList<MapEvent>.From(events);
        }

        private (MapEventTrigger, string[]) ParseTrigger(string[] tokens, int width, int height, int turnLimit)
        {
            if (tokens.Length < 2)
            {
                throw Error("event line needs a name, a trigger, and an action: 'reinforce turn 3 enemy spawn brigand 0,5 group:west behavior:aggressive'");
            }

            switch (tokens[1])
            {
                case "turn":
                    if (tokens.Length < 4)
                    {
                        throw Error("turn trigger needs a turn and a phase: 'turn 3 enemy'");
                    }

                    if (!int.TryParse(tokens[2], out var turn) || turn < 1 || turn > turnLimit)
                    {
                        throw Error($"turn trigger's turn must be an integer 1..{turnLimit} (the turn limit), got '{tokens[2]}'");
                    }

                    var phase = tokens[3] switch
                    {
                        "player" => Side.Player,
                        "enemy" => Side.Enemy,
                        _ => throw Error($"turn trigger's phase must be player or enemy, got '{tokens[3]}'"),
                    };
                    if (turn == 1 && phase == Side.Player)
                    {
                        throw Error("turn 1 player never begins, since the battle opens in it; put the change in the grid or the units block, or use turn 1 enemy");
                    }

                    return (new TurnTrigger(turn, phase), tokens[4..]);
                case "enter":
                    if (tokens.Length < 3)
                    {
                        throw Error("enter trigger needs a tile: 'enter 6,2'");
                    }

                    return (new EnterTrigger(ParseCoord(tokens[2], width, height)), tokens[3..]);
                default:
                    throw Error($"unknown event trigger '{tokens[1]}'; expected turn or enter");
            }
        }

        private MapEventAction ParseAction(string[] tokens, int width, int height, ValueList<string> terrain)
        {
            if (tokens.Length == 0)
            {
                throw Error("event line needs an action: terrain, spawn, or flag");
            }

            switch (tokens[0])
            {
                case "terrain":
                    if (tokens.Length != 3 || tokens[2].Length != 1)
                    {
                        throw Error("terrain action needs a tile and one glyph: 'terrain 6,1 ='");
                    }

                    var at = ParseCoord(tokens[1], width, height);
                    var glyph = tokens[2][0];
                    var tile = _content.TerrainByGlyph(glyph) ?? throw Error($"unknown terrain glyph '{glyph}' in terrain action");
                    return new ChangeTerrain(at, tile.Id);
                case "spawn":
                    if (tokens.Length < 3)
                    {
                        throw Error("spawn action needs a template and an edge tile: 'spawn brigand 0,5 group:west behavior:aggressive'");
                    }

                    var placement = ParseUnitLine("E " + string.Join(' ', tokens[1..]), width, height, terrain);
                    if (placement is not EnemyPlacement enemy)
                    {
                        throw Error("spawn action places an enemy");
                    }

                    if (enemy.At.X != 0 && enemy.At.Y != 0 && enemy.At.X != width - 1 && enemy.At.Y != height - 1)
                    {
                        throw Error($"spawn tile {enemy.At} is not on the edge of the {width}x{height} grid; reinforcements arrive from an edge");
                    }

                    return new SpawnEnemy(enemy);
                case "flag":
                    if (tokens.Length != 2)
                    {
                        throw Error("flag action needs one name: 'flag alarm'");
                    }

                    return new SetFlag(tokens[1]);
                default:
                    throw Error($"unknown event action '{tokens[0]}'; expected terrain, spawn, or flag");
            }
        }

        private PlayerPlacement ParsePlayer(string token, Coord at)
        {
            if (token == "captain")
            {
                return new PlayerPlacement(at, PlayerSlot.Captain);
            }

            if (token == "recruit")
            {
                return new PlayerPlacement(at, PlayerSlot.AnyRecruit);
            }

            if (token.StartsWith("recruit:", StringComparison.Ordinal) && token.Length > "recruit:".Length)
            {
                return new PlayerPlacement(at, PlayerSlot.NamedRecruit, token["recruit:".Length..]);
            }

            throw Error($"player unit must be 'captain', 'recruit', or 'recruit:<id>', got '{token}'");
        }

        private Coord ParseCoord(string token, int width, int height)
        {
            var parts = token.Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
            {
                throw Error($"position must be x,y, got '{token}'");
            }

            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                throw Error($"position {x},{y} is outside the {width}x{height} grid");
            }

            return new Coord(x, y);
        }

        private Dictionary<string, string> ParseAttributes(IEnumerable<string> tokens)
        {
            var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var token in tokens)
            {
                var colon = token.IndexOf(':');
                if (colon <= 0 || colon == token.Length - 1)
                {
                    throw Error($"attribute must be key:value, got '{token}'");
                }

                var key = token[..colon];
                if (key is not ("group" or "behavior"))
                {
                    throw Error($"unknown unit attribute '{key}'; expected group or behavior");
                }

                if (!attributes.TryAdd(key, token[(colon + 1)..]))
                {
                    throw Error($"attribute '{key}' appears twice");
                }
            }

            return attributes;
        }

        private void Validate(MapDefinition map)
        {
            var captains = map.Placements.Count(p => p is PlayerPlacement { Slot: PlayerSlot.Captain });
            if (captains != 1)
            {
                throw new MapException(_file, 0, $"a map needs exactly one 'P captain', got {captains}");
            }

            if (map.Win == WinCondition.Seize && !map.TilesOf(MapDefinition.ThroneTerrainId).Any())
            {
                throw new MapException(_file, 0, "win is seize but the grid has no throne tile");
            }

            if (map.Win == WinCondition.DefeatBoss && !map.Placements.Any(p => p is EnemyPlacement { IsBoss: true }))
            {
                throw new MapException(_file, 0, "win is defeat_boss but there is no B line");
            }

            var slots = map.Placements.Count(p => p is PlayerPlacement);
            if (map.Win == WinCondition.Escape && map.Exits.Count < slots)
            {
                throw new MapException(_file, 0, $"win is escape but there are {map.Exits.Count} exit tiles for {slots} player slots; every deployed unit needs one to stand on");
            }

            if (map.Win != WinCondition.Escape && map.Exits.Count > 0)
            {
                throw new MapException(_file, 0, $"exit tiles are only for win: escape, and this map's win is {MapRenderer.WinName(map.Win)}");
            }

            if (map.Certification is not null && slots != 1)
            {
                throw new MapException(_file, 0, $"a certification map has exactly one player slot, the candidate's 'P captain', got {slots}");
            }

            if (map.ProtectId is { } protect && !map.Placements.Any(p => p is PlayerPlacement { Slot: PlayerSlot.NamedRecruit } n && n.RecruitId == protect))
            {
                throw new MapException(_file, 0, $"protect names '{protect}' but no 'P recruit:{protect}' line places them");
            }
        }
    }
}
