using System.Text;
using Ironwake.Core;

namespace Ironwake.Content;

/// <summary>
/// Reads and writes the <c>.map</c> text format from DESIGN.md section 10: a header of
/// <c>key: value</c> lines, a blank line, the terrain grid, a blank line, then a
/// <c>units:</c> block, then an optional <c>chests:</c> block (issue 649), then an optional <c>events:</c> block (issue 32). <see cref="Write"/> is canonical (fixed header order, every
/// default written out), so <c>Write(Parse(text))</c> is a fixed point and a hand-edited
/// file can be checked against it. Nothing here touches the disk; <see cref="MapFiles"/> does.
/// </summary>
public static class MapFormat
{
    /// <summary>The largest <c>supplies:</c> cap; above every consumable's uses, so a cap this high never binds.</summary>
    private const int MaxSupplies = 99;

    private static readonly string[] HeaderKeys = { "name", "size", "win", "turn_limit", "recall", "enemy_level", "exit", "protect", "cheap_shots", "retreat", "rivalry", "supplies", "announce", "keepsakes", "dusk", "grudges", "shove", "pincer", "brace", "wildfire", "windup", "overwatch", "cover", "signatures", "break", "kinsbane", "messenger", "orders", "exit_after_move", "difficulty", "certification", "wake_links", "oathbound", "deploy" };

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

        if (map.GrudgesEnabled)
        {
            sb.Append("grudges: on\n");
        }

        if (map.ShoveEnabled)
        {
            sb.Append("shove: on\n");
        }

        if (map.PincerEnabled)
        {
            sb.Append("pincer: on\n");
        }

        if (map.BraceEnabled)
        {
            sb.Append("brace: on\n");
        }

        if (map.WildfireEnabled)
        {
            sb.Append("wildfire: on\n");
        }

        if (map.WindupEnabled)
        {
            sb.Append("windup: on\n");
        }

        if (map.OverwatchEnabled)
        {
            sb.Append(map.OverwatchHold ? "overwatch: hold\n" : "overwatch: on\n");
        }

        if (map.CoverEnabled)
        {
            sb.Append("cover: on\n");
        }

        if (map.SignaturesEnabled)
        {
            sb.Append("signatures: on\n");
        }

        if (map.BreakEnabled)
        {
            sb.Append("break: on\n");
        }

        if (map.KinsbaneBearer is { } bearer)
        {
            sb.Append("kinsbane: ").Append(bearer).Append('\n');
        }

        if (map.Messenger is { } messenger)
        {
            sb.Append("messenger: ").Append(messenger.From).Append(' ').Append(messenger.Road).Append('\n');
        }

        if (map.OrdersEnabled)
        {
            sb.Append("orders: on\n");
        }

        if (map.ExitAfterMove)
        {
            sb.Append("exit_after_move: on\n");
        }

        if (map.Dusk is { } dusk)
        {
            sb.Append("dusk: ").Append(dusk).Append('\n');
        }

        if (map.WakeLinks.Count > 0)
        {
            sb.Append("wake_links: ").Append(string.Join(", ", map.WakeLinks)).Append('\n');
        }

        if (map.Oathbound.Count > 0)
        {
            sb.Append("oathbound: ").Append(string.Join(", ", map.Oathbound)).Append('\n');
        }

        if (map.DifficultyId is { } difficulty)
        {
            sb.Append("difficulty: ").Append(difficulty).Append('\n');
        }

        if (map.Deploy != MapDefinition.DefaultDeploy)
        {
            sb.Append("deploy: ").Append(map.DeploysAll ? "all" : map.Deploy.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
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

        if (map.Chests.Count > 0)
        {
            sb.Append('\n');
            sb.Append("chests:\n");
            foreach (var chest in map.Chests)
            {
                sb.Append(chest.At).Append(' ').Append(string.Join(' ', chest.Items)).Append('\n');
            }
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
            EnterTrigger e => "enter " + string.Join(' ', e.Tiles),
            MessengerTrigger => "messenger",
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
            var grudges = ParseOn(header, "grudges");
            var shove = ParseOn(header, "shove");
            var pincer = ParseOn(header, "pincer");
            var brace = ParseOn(header, "brace");
            var wildfire = ParseOn(header, "wildfire");
            var windup = ParseOn(header, "windup");
            if (header.TryGetValue("overwatch", out var overwatchEntry) && overwatchEntry.Value is not ("on" or "hold"))
            {
                throw ErrorAt(overwatchEntry.Line, $"overwatch may only be 'on' or 'hold' (or absent), got '{overwatchEntry.Value}'");
            }

            var overwatchHold = overwatchEntry.Value == "hold";
            var overwatch = header.ContainsKey("overwatch");
            if (overwatch && brace)
            {
                throw ErrorAt(header["overwatch"].Line, "overwatch: on and brace: on are exclusive (DESIGN 13.17)");
            }
            var cover = ParseOn(header, "cover");
            var signatures = ParseOn(header, "signatures");
            var breaks = ParseOn(header, "break");
            var kinsbane = header.TryGetValue("kinsbane", out var kinsbaneEntry) ? kinsbaneEntry.Value : null;
            var orders = ParseOn(header, "orders");
            var exitAfterMove = ParseOn(header, "exit_after_move");
            if (exitAfterMove && win != WinCondition.Escape)
            {
                throw ErrorAt(header["exit_after_move"].Line, "exit_after_move: on needs win: escape");
            }
            int? dusk = header.ContainsKey("dusk") ? ParseInt(header, "dusk", 1, MapDefinition.MaxSide * 2, required: true, fallback: 0) : null;
            var exits = ParseExits(header, width, height);
            var protect = header.TryGetValue("protect", out var protectEntry) ? protectEntry.Value : null;
            var difficulty = ParseDifficulty(header);
            var certification = ParseCertification(header);

            SkipBlankLines();
            var terrain = ParseGrid(width, height);
            SkipBlankLines();
            var placements = ParseUnits(width, height, terrain);
            var chests = ParseChests(width, height);
            var events = ParseEvents(width, height, terrain, turnLimit);
            if (announce && events.Count == 0)
            {
                throw ErrorAt(header["announce"].Line, "announce: on needs an events: block to announce");
            }

            var map = new MapDefinition(name, width, height, win, turnLimit, recall, enemyLevel, cheapShots, terrain, placements, exits, protect, events, retreat, rivalry, supplies, difficulty, certification, announce, keepsakes, dusk, grudges, shove, exitAfterMove);
            map = map with { WakeLinks = ParseWakeLinks(header, map), PincerEnabled = pincer, BraceEnabled = brace, WildfireEnabled = wildfire, WindupEnabled = windup, OverwatchEnabled = overwatch, OverwatchHold = overwatchHold, CoverEnabled = cover, SignaturesEnabled = signatures, BreakEnabled = breaks, KinsbaneBearer = kinsbane, Chests = chests, Messenger = ParseMessenger(header, width, height), OrdersEnabled = orders };
            ValidateMessenger(map, header);
            Validate(map);
            return map with { Deploy = ParseDeploy(header, map), Oathbound = ParseOathbound(header, map) };
        }

        /// <summary>
        /// The <c>deploy:</c> header (issue 689): a count from 1, or <c>all</c> for the whole living
        /// company; absent, <see cref="MapDefinition.DefaultDeploy"/>. A count must cover the map's
        /// player placements, and <c>all</c> needs at least <see cref="CampaignRecord.CompanyCap"/> of them.
        /// </summary>
        private int ParseDeploy(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            var slots = map.Placements.Count(p => p is PlayerPlacement);
            if (!header.TryGetValue("deploy", out var entry))
            {
                if (slots > MapDefinition.DefaultDeploy)
                {
                    throw new MapException(_file, 0, $"deploy: the map has {slots} player placements and fields {MapDefinition.DefaultDeploy} without a deploy: header; set 'deploy: {slots}' or 'deploy: all'");
                }

                return MapDefinition.DefaultDeploy;
            }

            if (entry.Value == "all")
            {
                if (slots < CampaignRecord.CompanyCap)
                {
                    throw ErrorAt(entry.Line, $"deploy: all needs at least {CampaignRecord.CompanyCap} player placements, one per member of a full company, got {slots}");
                }

                return MapDefinition.DeployAll;
            }

            var count = ParseInt(header, "deploy", 1, CampaignRecord.CompanyCap, required: true, fallback: 0);
            if (slots > count)
            {
                throw ErrorAt(entry.Line, $"deploy: {count} is fewer than the map's {slots} player placements");
            }

            return count;
        }

        /// <summary>The <c>messenger:</c> header (DESIGN.md 13.24): two tiles, the messenger's placement and its road on the grid's edge.</summary>
        private MessengerRoute? ParseMessenger(Dictionary<string, (string Value, int Line)> header, int width, int height)
        {
            if (!header.TryGetValue("messenger", out var entry))
            {
                return null;
            }

            var tokens = entry.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length != 2)
            {
                throw ErrorAt(entry.Line, $"messenger needs two tiles, the messenger's and its road's: 'messenger: 3,2 12,0', got '{entry.Value}'");
            }

            Coord Tile(string token)
            {
                var parts = token.Split(',');
                if (parts.Length != 2 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
                {
                    throw ErrorAt(entry.Line, $"messenger position must be x,y, got '{token}'");
                }

                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    throw ErrorAt(entry.Line, $"messenger position {x},{y} is outside the {width}x{height} grid");
                }

                return new Coord(x, y);
            }

            var from = Tile(tokens[0]);
            var road = Tile(tokens[1]);
            if (road.X != 0 && road.Y != 0 && road.X != width - 1 && road.Y != height - 1)
            {
                throw ErrorAt(entry.Line, $"messenger's road {road} must be on the grid's edge");
            }

            return new MessengerRoute(from, road);
        }

        private void ValidateMessenger(MapDefinition map, Dictionary<string, (string Value, int Line)> header)
        {
            if (map.Messenger is not { } route)
            {
                if (map.Events.Any(e => e.Trigger is MessengerTrigger))
                {
                    throw new MapException(_file, 0, "an event uses the messenger trigger but the map has no messenger: header");
                }

                return;
            }

            var line = header["messenger"].Line;
            if (map.Placements.FirstOrDefault(p => p.At == route.From) is not EnemyPlacement enemy)
            {
                throw ErrorAt(line, $"messenger names {route.From} but no E line places an enemy there");
            }

            if (enemy.IsBoss)
            {
                throw ErrorAt(line, $"messenger at {route.From} is a boss; a boss never runs");
            }

            if (route.From == route.Road)
            {
                throw ErrorAt(line, "messenger's road is the tile it starts on");
            }

            if (!map.Events.Any(e => e.Trigger is MessengerTrigger))
            {
                throw ErrorAt(line, "messenger: needs at least one event with the messenger trigger, or reaching the road does nothing");
            }
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

        /// <summary>
        /// The <c>oathbound:</c> header (issue 691): comma-separated enemy group names, each on the
        /// map as a placement's or a spawn's group, each listed once. Every enemy in one is oath-bound.
        /// </summary>
        private ValueList<string> ParseOathbound(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            if (!header.TryGetValue("oathbound", out var entry))
            {
                return ValueList<string>.Empty;
            }

            var enemies = map.Placements.OfType<EnemyPlacement>().Concat(map.Spawns()).ToList();
            var groups = new List<string>();
            foreach (var group in entry.Value.Split(',', StringSplitOptions.TrimEntries))
            {
                if (group.Length == 0 || enemies.All(e => e.Group != group))
                {
                    throw ErrorAt(entry.Line, $"oathbound: no enemy is in group '{group}'");
                }

                if (groups.Contains(group))
                {
                    throw ErrorAt(entry.Line, $"oathbound: group '{group}' is listed twice");
                }

                groups.Add(group);
            }

            return ValueList<string>.From(groups);
        }

        /// <summary>
        /// The <c>wake_links:</c> header (issue 393): comma-separated <c>from&gt;to</c> pairs of
        /// group names. Each group must be on the map, as a placement's or a spawn's group; the
        /// second must have a Guard member, since only a sleeping group can be called; a group
        /// never links to itself, and a pair is listed once.
        /// </summary>
        private ValueList<WakeLink> ParseWakeLinks(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            if (!header.TryGetValue("wake_links", out var entry))
            {
                return ValueList<WakeLink>.Empty;
            }

            var enemies = map.Placements.OfType<EnemyPlacement>().Concat(map.Spawns()).ToList();
            var links = new List<WakeLink>();
            foreach (var part in entry.Value.Split(',', StringSplitOptions.TrimEntries))
            {
                var sides = part.Split('>', StringSplitOptions.TrimEntries);
                if (sides.Length != 2 || sides[0].Length == 0 || sides[1].Length == 0)
                {
                    throw ErrorAt(entry.Line, $"wake_links: '{part}' is not a pair 'from>to'");
                }

                var link = new WakeLink(sides[0], sides[1]);
                foreach (var group in sides)
                {
                    if (enemies.All(e => e.Group != group))
                    {
                        throw ErrorAt(entry.Line, $"wake_links: no enemy is in group '{group}'");
                    }
                }

                if (link.From == link.To)
                {
                    throw ErrorAt(entry.Line, $"wake_links: group '{link.From}' links to itself");
                }

                if (enemies.All(e => e.Group != link.To || e.Behavior != Behavior.Guard))
                {
                    throw ErrorAt(entry.Line, $"wake_links: group '{link.To}' has no guard member, so nothing in it sleeps to be called");
                }

                if (links.Contains(link))
                {
                    throw ErrorAt(entry.Line, $"wake_links: '{link}' is listed twice");
                }

                links.Add(link);
            }

            return ValueList<WakeLink>.From(links);
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
            for (; !AtEnd && Current.Trim() is not ("events:" or "chests:"); _index++)
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
        /// The optional <c>chests:</c> block after the units (issue 649): one line per chest,
        /// <c>x,y item [item ...]</c>, one to <see cref="Chest.MaxItems"/> ids from weapons.json or
        /// items.json, one chest per tile. A signature item (<c>boundTo</c>) is never in a chest:
        /// it is lost with its owner, never found.
        /// </summary>
        private ValueList<Chest> ParseChests(int width, int height)
        {
            if (AtEnd || Current.Trim() != "chests:")
            {
                return ValueList<Chest>.Empty;
            }

            _index++;
            var chests = new List<Chest>();
            var lines = new Dictionary<Coord, int>();
            for (; !AtEnd && Current.Trim() != "events:"; _index++)
            {
                if (string.IsNullOrWhiteSpace(Current))
                {
                    continue;
                }

                var tokens = Current.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var at = ParseCoord(tokens[0], width, height);
                if (lines.TryGetValue(at, out var otherLine))
                {
                    throw Error($"tile {at} already has the chest on line {otherLine}");
                }

                var items = tokens[1..];
                if (items.Length is 0 or > Chest.MaxItems)
                {
                    throw Error($"a chest holds 1 to {Chest.MaxItems} items, got {items.Length}");
                }

                foreach (var item in items)
                {
                    if (_content.Weapons.TryGetValue(item, out var weapon))
                    {
                        if (weapon.BoundTo is { } owner)
                        {
                            throw Error($"chest at {at} holds '{item}', a signature item bound to {owner}; it is lost with its owner, never found");
                        }
                    }
                    else if (!_content.Items.ContainsKey(item))
                    {
                        throw Error($"chest at {at} holds '{item}', which is not a weapon in weapons.json or an item in items.json");
                    }
                }

                lines[at] = LineNumber;
                chests.Add(new Chest(at, ValueList<string>.From(items)));
            }

            return ValueList<Chest>.From(chests);
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
                    var count = tokens.Skip(2).TakeWhile(t => t.Contains(',')).Count();
                    if (count == 0)
                    {
                        throw Error("enter trigger needs a tile: 'enter 6,2', or several: 'enter 6,4 6,3'");
                    }

                    var tiles = new List<Coord>();
                    foreach (var token in tokens[2..(2 + count)])
                    {
                        var tile = ParseCoord(token, width, height);
                        if (tiles.Contains(tile))
                        {
                            throw Error($"enter trigger lists {tile} twice");
                        }

                        tiles.Add(tile);
                    }

                    return (new EnterTrigger(ValueList<Coord>.From(tiles)), tokens[(2 + count)..]);
                case "messenger":
                    return (new MessengerTrigger(), tokens[2..]);
                default:
                    throw Error($"unknown event trigger '{tokens[1]}'; expected turn, enter or messenger");
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

            if (map.KinsbaneBearer is { } bearer && !map.Placements.Any(p => p is PlayerPlacement { Slot: PlayerSlot.NamedRecruit } n && n.RecruitId == bearer))
            {
                throw new MapException(_file, 0, $"kinsbane names '{bearer}' but no 'P recruit:{bearer}' line places them");
            }

            if (map.ProtectId is { } protect && !map.Placements.Any(p => p is PlayerPlacement { Slot: PlayerSlot.NamedRecruit } n && n.RecruitId == protect))
            {
                throw new MapException(_file, 0, $"protect names '{protect}' but no 'P recruit:{protect}' line places them");
            }
        }
    }
}
