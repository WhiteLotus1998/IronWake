using System.Text;
using Ironwake.Core;

namespace Ironwake.Content;

/// <summary>
/// Reads and writes the <c>.map</c> text format from DESIGN.md section 10: a header of
/// <c>key: value</c> lines, a blank line, the terrain grid, a blank line, then a
/// <c>units:</c> block. <see cref="Write"/> is canonical (fixed header order, every
/// default written out), so <c>Write(Parse(text))</c> is a fixed point and a hand-edited
/// file can be checked against it. Nothing here touches the disk; <see cref="MapFiles"/> does.
/// </summary>
public static class MapFormat
{
    private static readonly string[] HeaderKeys = { "name", "size", "win", "turn_limit", "recall", "enemy_level", "cheap_shots" };

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
        if (map.CheapShotsAllowed)
        {
            sb.Append("cheap_shots: allowed\n");
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

        return sb.ToString();
    }

    private static string WriteUnitLine(Placement placement) => placement switch
    {
        PlayerPlacement { Slot: PlayerSlot.Captain } p => "P captain " + p.At,
        PlayerPlacement { Slot: PlayerSlot.NamedRecruit } p => "P recruit:" + p.RecruitId + " " + p.At,
        PlayerPlacement p => "P recruit " + p.At,
        EnemyPlacement { IsBoss: true } e => "B " + e.TemplateId + " " + e.At + " group:" + e.Group + " behavior:boss",
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

            SkipBlankLines();
            var terrain = ParseGrid(width, height);
            SkipBlankLines();
            var placements = ParseUnits(width, height, terrain);

            var map = new MapDefinition(name, width, height, win, turnLimit, recall, enemyLevel, cheapShots, terrain, placements);
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
            for (; !AtEnd; _index++)
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
                if (attributes.TryGetValue("behavior", out var given) && given != "boss")
                {
                    throw Error($"a B line's behavior is always boss, got '{given}'");
                }

                behavior = Behavior.Boss;
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
        }
    }
}
