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

    private static readonly string[] HeaderKeys = { "name", "size", "win", "turn_limit", "recall", "enemy_level", "exit", "protect", "cheap_shots", "retreat", "rivalry", "supplies", "announce", "keepsakes", "dusk", "grudges", "shove", "pincer", "brace", "wildfire", "windup", "overwatch", "cover", "signatures", "break", "kinsbane", "woken", "messenger", "orders", "exit_after_move", "effective_bows", "difficulty", "certification", "wake_links", "oathbound", "deploy", "fronts", "hunter", "hunt_waits", "pair_rule", "freed", "keziah_warning", "carry", "breath", "route_drift", "region", "dash", "wind", "one_answer", "seen_far", "holds", "goes_home", "seize_name", "seize_hold", "shard_race", "kin_shard", "swallowed", "shard_breaks", "drops", "arrivals", "wake_on_death" };

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
        if (map.SeizeName is { } seizeName)
        {
            sb.Append("seize_name: ").Append(seizeName).Append('\n');
        }

        if (map.SeizeHold)
        {
            sb.Append("seize_hold: 1\n");
        }

        if (map.KinShard is { } kinShard)
        {
            sb.Append("kin_shard: ").Append(kinShard).Append('\n');
        }

        if (map.Swallowed is { } swallowed)
        {
            sb.Append("swallowed: ").Append(swallowed).Append('\n');
        }

        if (map.ShardBreaks != ShardBreak.None)
        {
            sb.Append("shard_breaks: ").Append(map.ShardBreaks == ShardBreak.Stills ? "stills" : "turns").Append('\n');
        }

        if ((map.ShardRace ?? map.SecretRace?.Race) is { } race)
        {
            var word = map.SecretRace is not null ? " secret" : race.Secret ? " armed" : "";
            sb.Append("shard_race: ").Append(race.Inner).Append(' ').Append(race.Phases).Append(word).Append('\n');
        }

        if (map.Drops.Count > 0)
        {
            sb.Append("drops: ").Append(string.Join(' ', map.Drops)).Append('\n');
        }

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

        if (map.DashEnabled)
        {
            sb.Append("dash: on\n");
        }

        if (map.OneAnswerEnabled)
        {
            sb.Append("one_answer: on\n");
        }

        if (map.PincerEnabled)
        {
            sb.Append("pincer: on\n");
        }

        if (map.ArrivalsWait)
        {
            sb.Append("arrivals: wait\n");
        }

        if (map.DeathWakes.Count > 0)
        {
            sb.Append("wake_on_death: ").Append(string.Join(", ", map.DeathWakes)).Append('\n');
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

        if (map.WokenBearer is { } woken)
        {
            sb.Append("woken: ").Append(woken).Append('\n');
        }

        if (map.CarryRider is { } carry)
        {
            sb.Append("carry: ").Append(carry).Append('\n');
        }

        if (map.BreathRider is { } breath)
        {
            sb.Append("breath: ").Append(breath).Append('\n');
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

        if (map.EffectiveBows)
        {
            sb.Append("effective_bows: on\n");
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

        if (map.PairRuleGroups.Count > 0)
        {
            sb.Append("pair_rule: ").Append(string.Join(", ", map.PairRuleGroups)).Append('\n');
        }

        if (map.Fronts.Count > 0)
        {
            sb.Append("fronts: ").Append(string.Join("; ", map.Fronts.Select(f => f.Name + " " + string.Join(' ', f.Tiles)))).Append('\n');
        }

        if (map.Hunter is { } hunter)
        {
            sb.Append("hunter: ").Append(hunter).Append('\n');
        }

        if (map.HuntWaits)
        {
            sb.Append("hunt_waits: on\n");
        }

        if (map.Bond is { } bond)
        {
            sb.Append("freed: ").Append(bond.Bound).Append(" by ").Append(bond.BossGroup).Append('\n');
        }

        if (map.RouteDrift is { } routeDrift)
        {
            sb.Append("route_drift: ").Append(routeDrift).Append('\n');
        }

        if (map.Wind is { } wind)
        {
            sb.Append("wind: ").Append(wind).Append('\n');
        }

        if (map.SeenFar is { } seenFar)
        {
            sb.Append("seen_far: ").Append(seenFar).Append('\n');
        }

        if (map.Holds is { } holds)
        {
            sb.Append("holds: ").Append(holds).Append('\n');
        }

        if (map.Homing is { } homing)
        {
            sb.Append("goes_home: ").Append(homing).Append('\n');
        }

        if (map.Region != MapRegion.Seam)
        {
            sb.Append("region: ").Append(MapRegions.Word(map.Region)).Append('\n');
        }

        if (map.DifficultyId is { } difficulty)
        {
            sb.Append("difficulty: ").Append(difficulty).Append('\n');
        }

        if (map.Deploy != MapDefinition.DefaultDeploy)
        {
            sb.Append("deploy: ").Append(map.DeploysAll ? "all" : map.Deploy.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        }

        if (map.KeziahWarning)
        {
            sb.Append("keziah_warning: on\n");
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

        var events = map.SecretRace is { } held ? map.Events.Concat(held.Events).ToList() : map.Events.ToList();
        if (events.Count > 0)
        {
            sb.Append('\n');
            sb.Append("events:\n");
            foreach (var mapEvent in events)
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
            WakesTrigger w => "wakes " + w.Group + (w.Late ? " late" : ""),
            MessengerTrigger => "messenger",
            FallsTrigger f => "falls " + f.Front,
            DropTrigger d => "drop " + d.Ledge,
            RaceTrigger r => "race " + r.Phases,
            _ => throw new ArgumentOutOfRangeException(nameof(mapEvent), mapEvent.Trigger, "unknown map event trigger"),
        };
        var action = mapEvent.Action switch
        {
            ChangeTerrain c => "terrain " + c.At + " " + content.TerrainById(c.TerrainId).Glyph + (c.Held ? " held" : ""),
            SpawnEnemy s => "spawn " + (s.Placement.IsBoss ? "boss " : "") + s.Placement.TemplateId + " " + s.Placement.At + " group:" + s.Placement.Group + " behavior:" + s.Placement.Behavior.ToString().ToLowerInvariant(),
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
            var dash = ParseOn(header, "dash");
            var oneAnswer = ParseOn(header, "one_answer");
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
            var woken = header.TryGetValue("woken", out var wokenEntry) ? wokenEntry.Value : null;
            var orders = ParseOn(header, "orders");
            var effectiveBows = ParseOn(header, "effective_bows");
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
            var events = ParseEvents(width, height, terrain, turnLimit, placements);
            if (announce && events.Count == 0)
            {
                throw ErrorAt(header["announce"].Line, "announce: on needs an events: block to announce");
            }

            var map = new MapDefinition(name, width, height, win, turnLimit, recall, enemyLevel, cheapShots, terrain, placements, exits, protect, events, retreat, rivalry, supplies, difficulty, certification, announce, keepsakes, dusk, grudges, shove, exitAfterMove);
            map = map with { WakeLinks = ParseWakeLinks(header, map), PincerEnabled = pincer, BraceEnabled = brace, WildfireEnabled = wildfire, WindupEnabled = windup, OverwatchEnabled = overwatch, OverwatchHold = overwatchHold, CoverEnabled = cover, SignaturesEnabled = signatures, BreakEnabled = breaks, KinsbaneBearer = kinsbane, WokenBearer = woken, Chests = chests, Messenger = ParseMessenger(header, width, height), OrdersEnabled = orders, EffectiveBows = effectiveBows, DashEnabled = dash, OneAnswerEnabled = oneAnswer };
            ValidateMessenger(map, header);
            Validate(map);
            map = map with { CarryRider = ParseCarry(header, map), BreathRider = ParseBreath(header, map) };
            map = map with { Deploy = ParseDeploy(header, map), Oathbound = ParseGroups(header, map, "oathbound"), PairRuleGroups = ParseGroups(header, map, "pair_rule") };
            map = map with { Fronts = ParseFronts(header, map) };
            map = map with { Hunter = ParseHunter(header, map) };
            map = map with { HuntWaits = ParseHuntWaits(header, map) };
            map = map with { Bond = ParseFreed(header, map), KeziahWarning = ParseOn(header, "keziah_warning") };
            map = map with { RouteDrift = ParseRouteDrift(header, map), Region = ParseRegion(header), Wind = ParseWind(header, map), SeenFar = ParseSeenFar(header), Holds = ParseHolds(header, map), Homing = ParseHoming(header, map), SeizeName = ParseSeizeName(header, win), SeizeHold = ParseSeizeHold(header, win) };
            var (shardRace, held) = ParseShardRace(header, win, map);
            map = map with { ShardRace = shardRace };
            if (held && shardRace is { } secretRace)
            {
                map = map with
                {
                    ShardRace = null,
                    SecretRace = new SecretRace(secretRace, ValueList<MapEvent>.From(map.Events.Where(e => e.Trigger is RaceTrigger))),
                    Events = ValueList<MapEvent>.From(map.Events.Where(e => e.Trigger is not RaceTrigger)),
                };
            }
            map = map with { KinShard = ParseKinShard(header, map) };
            map = map with { Swallowed = ParseSwallowed(header, map) };
            map = map with { ShardBreaks = ParseShardBreaks(header, map) };
            map = map with { Drops = ParseDrops(header, map), ArrivalsWait = ParseArrivals(header, map), DeathWakes = ParseDeathWakes(header, map) };
            if (Kinsbane.WarningRefusal(map, _content) is { } warning)
            {
                throw ErrorAt(header["keziah_warning"].Line, warning);
            }

            return map;
        }

        /// <summary>
        /// The <c>wake_on_death:</c> header (issue 1264): comma-separated <c>group by other</c> entries.
        /// Both groups must be on the map; the first must have a Guard member, since only a sleeping
        /// group can wake; a group never names itself, and is named once.
        /// </summary>
        private ValueList<DeathWake> ParseDeathWakes(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            if (!header.TryGetValue("wake_on_death", out var entry))
            {
                return ValueList<DeathWake>.Empty;
            }

            var enemies = map.Placements.OfType<EnemyPlacement>().Concat(map.Spawns()).ToList();
            var wakes = new List<DeathWake>();
            foreach (var part in entry.Value.Split(',', StringSplitOptions.TrimEntries))
            {
                var sides = part.Split(" by ", StringSplitOptions.TrimEntries);
                if (sides.Length != 2 || sides[0].Length == 0 || sides[1].Length == 0)
                {
                    throw ErrorAt(entry.Line, $"wake_on_death: '{part}' is not 'group by other'");
                }

                var wake = new DeathWake(sides[0], sides[1]);
                foreach (var group in sides)
                {
                    if (enemies.All(e => e.Group != group))
                    {
                        throw ErrorAt(entry.Line, $"wake_on_death: no enemy is in group '{group}'");
                    }
                }

                if (wake.Group == wake.By)
                {
                    throw ErrorAt(entry.Line, $"wake_on_death: group '{wake.Group}' names itself");
                }

                if (enemies.All(e => e.Group != wake.Group || e.Behavior != Behavior.Guard))
                {
                    throw ErrorAt(entry.Line, $"wake_on_death: group '{wake.Group}' has no guard member, so nothing in it sleeps");
                }

                if (wakes.Any(w => w.Group == wake.Group))
                {
                    throw ErrorAt(entry.Line, $"wake_on_death: group '{wake.Group}' is named twice");
                }

                wakes.Add(wake);
            }

            return ValueList<DeathWake>.From(wakes);
        }

        /// <summary>
        /// The <c>arrivals:</c> header (issue 1259, experiment): only <c>wait</c>, and only on a map with a spawn
        /// event, or absent, when a blocked spawn is spent.
        /// </summary>
        private bool ParseArrivals(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            if (!header.TryGetValue("arrivals", out var entry))
            {
                return false;
            }

            if (entry.Value != "wait")
            {
                throw ErrorAt(entry.Line, $"arrivals may only be 'wait' (or absent), got '{entry.Value}'");
            }

            if (!map.Events.Any(e => e.Action is SpawnEnemy { Placement.IsBoss: false }))
            {
                throw ErrorAt(entry.Line, "arrivals: wait needs a spawn event to hold back");
            }

            return true;
        }

        /// <summary>
        /// The <c>freed:</c> header (issue 750): <c>x,y by group</c>, the tile an enemy placement's
        /// that is neither a boss nor the messenger, the group one with a boss, placed or spawned.
        /// </summary>
        private FreedBond? ParseFreed(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            if (!header.TryGetValue("freed", out var entry))
            {
                return null;
            }

            var parts = entry.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var tile = parts.Length == 3 && parts[1] == "by" ? parts[0].Split(',') : Array.Empty<string>();
            if (tile.Length != 2 || !int.TryParse(tile[0], out var x) || !int.TryParse(tile[1], out var y))
            {
                throw ErrorAt(entry.Line, $"freed: needs the bound enemy's tile and the boss's group: 'freed: 7,6 by lord', got '{entry.Value}'");
            }

            var at = new Coord(x, y);
            var group = parts[2];
            if (map.Placements.FirstOrDefault(p => p.At == at) is not EnemyPlacement enemy)
            {
                throw ErrorAt(entry.Line, $"freed names {at} but no E line places an enemy there");
            }

            if (enemy.IsBoss)
            {
                throw ErrorAt(entry.Line, $"freed: the enemy at {at} is a boss; a boss is never bound");
            }

            if (map.Messenger is { } route && route.From == at)
            {
                throw ErrorAt(entry.Line, $"freed: the enemy at {at} is the messenger");
            }

            if (!map.Placements.OfType<EnemyPlacement>().Concat(map.Spawns()).Any(e => e.IsBoss && e.Group == group))
            {
                throw ErrorAt(entry.Line, $"freed: group '{group}' has no boss, placed or spawned");
            }

            return new FreedBond(at, group);
        }

        /// <summary>
        /// The <c>kin_shard:</c> header (issue 1386 slice 3a): <c>x,y</c>, the tile of the enemy placement that carries
        /// the shard under the hill, neither a boss nor the messenger.
        /// </summary>
        private Coord? ParseKinShard(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            if (!header.TryGetValue("kin_shard", out var entry))
            {
                return null;
            }

            var tile = entry.Value.Trim().Split(',');
            if (tile.Length != 2 || !int.TryParse(tile[0], out var x) || !int.TryParse(tile[1], out var y))
            {
                throw ErrorAt(entry.Line, $"kin_shard: needs the bearer's tile: 'kin_shard: 7,6', got '{entry.Value}'");
            }

            var at = new Coord(x, y);
            if (map.Placements.FirstOrDefault(p => p.At == at) is not EnemyPlacement enemy)
            {
                throw ErrorAt(entry.Line, $"kin_shard names {at} but no E line places an enemy there");
            }

            if (enemy.IsBoss)
            {
                throw ErrorAt(entry.Line, $"kin_shard: the enemy at {at} is a boss; a sworn carries the shard");
            }

            if (map.Messenger is { } route && route.From == at)
            {
                throw ErrorAt(entry.Line, $"kin_shard: the enemy at {at} is the messenger");
            }

            return at;
        }

        /// <summary>
        /// The <c>swallowed:</c> header (issue 1386 slice 3b', under the hill): the tile of the boss placement that
        /// begins the map already swallowed. It must place a boss whose template carries a <c>swallow</c> block, and not
        /// on a <c>shard_race:</c> map, whose boss runs before he swallows.
        /// </summary>
        private Coord? ParseSwallowed(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            if (!header.TryGetValue("swallowed", out var entry))
            {
                return null;
            }

            var tile = entry.Value.Trim().Split(',');
            if (tile.Length != 2 || !int.TryParse(tile[0], out var x) || !int.TryParse(tile[1], out var y))
            {
                throw ErrorAt(entry.Line, $"swallowed: needs the boss's tile: 'swallowed: 1,4', got '{entry.Value}'");
            }

            var at = new Coord(x, y);
            if (map.Placements.FirstOrDefault(p => p.At == at) is not EnemyPlacement enemy)
            {
                throw ErrorAt(entry.Line, $"swallowed names {at} but no B line places a boss there");
            }

            if (!enemy.IsBoss)
            {
                throw ErrorAt(entry.Line, $"swallowed: the enemy at {at} is not a boss; only a boss begins swallowed");
            }

            if (!_content.Units.TryGetValue(enemy.TemplateId, out var template) || template.Swallow is null)
            {
                throw ErrorAt(entry.Line, $"swallowed: the boss at {at} ('{enemy.TemplateId}') has no swallow block, so it has no second stage to begin in");
            }

            if (map.ShardRace is not null || map.SecretRace is not null)
            {
                throw ErrorAt(entry.Line, "swallowed: a shard_race: map's boss runs before he swallows; a map has one or the other");
            }

            return at;
        }

        /// <summary>
        /// The <c>shard_breaks:</c> header (issue 1386 slice 3e): <c>stills</c> or <c>turns</c>, only on a map that carries
        /// both a Kin shard (<c>kin_shard:</c>) and a boss who begins swallowed (<c>swallowed:</c>), since it changes that
        /// boss's frost when that shard breaks.
        /// </summary>
        private ShardBreak ParseShardBreaks(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            if (!header.TryGetValue("shard_breaks", out var entry))
            {
                return ShardBreak.None;
            }

            var rule = entry.Value.Trim() switch
            {
                "stills" => ShardBreak.Stills,
                "turns" => ShardBreak.Turns,
                _ => throw ErrorAt(entry.Line, $"shard_breaks names 'stills' or 'turns', got '{entry.Value}'"),
            };
            if (map.KinShard is null || map.Swallowed is null)
            {
                throw ErrorAt(entry.Line, "shard_breaks: needs both kin_shard: and swallowed:; it changes a swallowed boss's frost when the Kin's shard breaks");
            }

            return rule;
        }

        /// <summary>
        /// The <c>drops:</c> header (issue 1246): one or more <c>x,y</c> tiles, each placing an enemy
        /// that carries a Lore tome, none named twice. Empty when absent.
        /// </summary>
        private ValueList<Coord> ParseDrops(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            if (!header.TryGetValue("drops", out var entry))
            {
                return ValueList<Coord>.Empty;
            }

            var tiles = new List<Coord>();
            foreach (var part in entry.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var xy = part.Split(',');
                if (xy.Length != 2 || !int.TryParse(xy[0], out var x) || !int.TryParse(xy[1], out var y))
                {
                    throw ErrorAt(entry.Line, $"drops: needs the tiles of enemies that drop their tomes, 'drops: 7,6 9,4', got '{part}'");
                }

                var at = new Coord(x, y);
                if (map.Placements.FirstOrDefault(p => p.At == at) is not EnemyPlacement enemy)
                {
                    throw ErrorAt(entry.Line, $"drops names {at} but no E line places an enemy there");
                }

                if (!_content.Units[enemy.TemplateId].Inventory.Items.Any(i => _content.Weapons.TryGetValue(i.ItemId, out var w) && w.Type == WeaponType.Reason))
                {
                    throw ErrorAt(entry.Line, $"drops: the enemy at {at} ({enemy.TemplateId}) carries no Lore tome");
                }

                if (tiles.Contains(at))
                {
                    throw ErrorAt(entry.Line, $"drops names {at} twice");
                }

                tiles.Add(at);
            }

            if (tiles.Count == 0)
            {
                throw ErrorAt(entry.Line, "drops: needs at least one tile");
            }

            return ValueList<Coord>.From(tiles);
        }

        /// <summary>
        /// The <c>route_drift:</c> header (issue 81): <c>group x,y; group x,y; turn N</c>, two distinct
        /// groups each with a Guard member, placed or spawned, each with its route's crossing, a tile on
        /// the map, and the turn the untaken route's group moves, 1 to the turn limit.
        /// </summary>
        private RouteDrift? ParseRouteDrift(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            if (!header.TryGetValue("route_drift", out var entry))
            {
                return null;
            }

            var parts = entry.Value.Split(';', StringSplitOptions.TrimEntries);
            var turn = parts.Length == 3 ? parts[2].Split(' ', StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
            if (parts.Length != 3 || turn.Length != 2 || turn[0] != "turn" || !int.TryParse(turn[1], out var when))
            {
                throw ErrorAt(entry.Line, $"route_drift: needs two route groups with their crossings and a turn: 'route_drift: line 12,7; south 12,13; turn 6', got '{entry.Value}'");
            }

            var enemies = map.Placements.OfType<EnemyPlacement>().Concat(map.Spawns()).ToList();
            var routes = new List<(string Group, Coord Crossing)>();
            foreach (var part in parts.Take(2))
            {
                var pieces = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var tile = pieces.Length == 2 ? pieces[1].Split(',') : Array.Empty<string>();
                if (tile.Length != 2 || !int.TryParse(tile[0], out var x) || !int.TryParse(tile[1], out var y))
                {
                    throw ErrorAt(entry.Line, $"route_drift: '{part}' is not a group and its crossing, 'line 12,7'");
                }

                var crossing = new Coord(x, y);
                if (!map.Contains(crossing))
                {
                    throw ErrorAt(entry.Line, $"route_drift: crossing {crossing} is outside the map");
                }

                if (enemies.All(e => e.Group != pieces[0] || e.Behavior != Behavior.Guard))
                {
                    throw ErrorAt(entry.Line, $"route_drift: group '{pieces[0]}' has no guard member, so nothing in it sleeps to drift");
                }

                routes.Add((pieces[0], crossing));
            }

            if (routes[0].Group == routes[1].Group)
            {
                throw ErrorAt(entry.Line, $"route_drift: group '{routes[0].Group}' is named twice; the two routes need two groups");
            }

            if (when < 1 || when > map.TurnLimit)
            {
                throw ErrorAt(entry.Line, $"route_drift: turn {when} is outside 1 to the turn limit {map.TurnLimit}");
            }

            return new RouteDrift(routes[0].Group, routes[0].Crossing, routes[1].Group, routes[1].Crossing, when);
        }

        /// <summary>
        /// The <c>hunt_waits: on</c> header (issue 1204): the hunt before DECISIONS/0284, kept for the
        /// samples whose journaled plays were made under it. It needs a <c>hunter:</c> header.
        /// </summary>
        private bool ParseHuntWaits(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            var on = ParseOn(header, "hunt_waits");
            if (on && map.Hunter is null)
            {
                throw ErrorAt(header["hunt_waits"].Line, "hunt_waits: needs a hunter: header; it keeps the old hunt");
            }

            return on;
        }

        /// <summary>
        /// The <c>hunter:</c> header (issue 692): one tile, an enemy placement's, whose unit hunts the
        /// weakest front (<see cref="Hunt"/>). The map needs fronts; a boss never hunts, nor does the messenger.
        /// </summary>
        private Coord? ParseHunter(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            if (!header.TryGetValue("hunter", out var entry))
            {
                return null;
            }

            var parts = entry.Value.Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
            {
                throw ErrorAt(entry.Line, $"hunter: needs the hunter's tile as x,y: 'hunter: 3,6', got '{entry.Value}'");
            }

            var at = new Coord(x, y);
            if (map.Fronts.Count == 0)
            {
                throw ErrorAt(entry.Line, "hunter: needs a fronts: header; the hunter hunts a front");
            }

            if (map.Placements.FirstOrDefault(p => p.At == at) is not EnemyPlacement enemy)
            {
                throw ErrorAt(entry.Line, $"hunter names {at} but no E line places an enemy there");
            }

            if (enemy.IsBoss)
            {
                throw ErrorAt(entry.Line, $"hunter at {at} is a boss; a boss never hunts");
            }

            if (map.Messenger is { } route && route.From == at)
            {
                throw ErrorAt(entry.Line, $"hunter at {at} is the messenger, which never strikes");
            }

            return at;
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
        /// The <c>fronts:</c> header (issue 692): fronts separated by <c>;</c>, each a one-word name
        /// (lower case, digits and underscores) and its tiles, in file order. A name is listed once, a
        /// tile belongs to one front, and no enemy may start on a front's tile, or it would fall before
        /// the first command. Every <c>falls</c> trigger must name a front here, and a map with the
        /// trigger needs the header.
        /// </summary>
        private ValueList<Front> ParseFronts(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            if (!header.TryGetValue("fronts", out var entry))
            {
                if (map.Events.FirstOrDefault(e => e.Trigger is FallsTrigger) is { } orphan)
                {
                    throw new MapException(_file, 0, $"event '{orphan.Name}' uses the falls trigger but the map has no fronts: header");
                }

                return ValueList<Front>.Empty;
            }

            var fronts = new List<Front>();
            foreach (var part in entry.Value.Split(';', StringSplitOptions.TrimEntries))
            {
                var tokens = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2)
                {
                    throw ErrorAt(entry.Line, $"fronts: each front needs a name and at least one tile: 'fronts: west 2,4 2,5; gate 7,0', got '{part}'");
                }

                var name = tokens[0];
                if (!name.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_') || name[0] is < 'a' or > 'z')
                {
                    throw ErrorAt(entry.Line, $"fronts: a front's name is one word in lower case (letters, digits, underscores), got '{name}'");
                }

                if (fronts.Any(f => f.Name == name))
                {
                    throw ErrorAt(entry.Line, $"fronts: front '{name}' is listed twice");
                }

                var tiles = new List<Coord>();
                foreach (var token in tokens[1..])
                {
                    var parts = token.Split(',');
                    if (parts.Length != 2 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
                    {
                        throw ErrorAt(entry.Line, $"fronts: front '{name}' tile must be x,y, got '{token}'");
                    }

                    var tile = new Coord(x, y);
                    if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
                    {
                        throw ErrorAt(entry.Line, $"fronts: front '{name}' tile {tile} is outside the {map.Width}x{map.Height} grid");
                    }

                    if (tiles.Contains(tile) || fronts.Any(f => f.Tiles.Contains(tile)))
                    {
                        throw ErrorAt(entry.Line, $"fronts: tile {tile} is listed twice; a tile belongs to one front");
                    }

                    if (map.Placements.OfType<EnemyPlacement>().Any(e => e.At == tile))
                    {
                        throw ErrorAt(entry.Line, $"fronts: an enemy starts on {tile}, front '{name}''s tile, so it would fall before the first command");
                    }

                    tiles.Add(tile);
                }

                fronts.Add(new Front(name, ValueList<Coord>.From(tiles)));
            }

            if (map.Events.FirstOrDefault(e => e.Trigger is FallsTrigger falls && fronts.All(f => f.Name != falls.Front)) is { } stray)
            {
                throw ErrorAt(entry.Line, $"event '{stray.Name}' uses 'falls {((FallsTrigger)stray.Trigger).Front}' but fronts: names no such front");
            }

            return ValueList<Front>.From(fronts);
        }

        /// <summary>
        /// The <c>carry:</c> header (issue 805, samples): <c>carry: &lt;rider&gt;</c>, the rider placed by a
        /// <c>P recruit:</c> line. The settings <c>waited</c> and <c>brace</c> were dropped and <c>free</c>
        /// is the only one (issue 1094), so a setting word is refused.
        /// </summary>
        private string? ParseCarry(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            if (!header.TryGetValue("carry", out var entry))
            {
                return null;
            }

            var words = entry.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 2 && words[0] is "waited" or "free" or "brace")
            {
                throw ErrorAt(entry.Line, $"carry takes only '<rider>' now; the setting '{words[0]}' was dropped and every carry lands free (issue 1094), got '{entry.Value}'");
            }

            if (words.Length != 1)
            {
                throw ErrorAt(entry.Line, $"carry needs '<rider>', got '{entry.Value}'");
            }

            if (!map.Placements.Any(p => p is PlayerPlacement { Slot: PlayerSlot.NamedRecruit } n && n.RecruitId == words[0]))
            {
                throw ErrorAt(entry.Line, $"carry names '{words[0]}' but no 'P recruit:{words[0]}' line places them");
            }

            return words[0];
        }

        /// <summary>
        /// The <c>breath:</c> header (issue 805, samples): <c>breath: &lt;rider&gt;</c>, the rider placed by a
        /// <c>P recruit:</c> line.
        /// </summary>
        private string? ParseBreath(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            if (!header.TryGetValue("breath", out var entry))
            {
                return null;
            }

            var words = entry.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length != 1)
            {
                throw ErrorAt(entry.Line, $"breath needs '<rider>', got '{entry.Value}'");
            }

            if (!map.Placements.Any(p => p is PlayerPlacement { Slot: PlayerSlot.NamedRecruit } n && n.RecruitId == words[0]))
            {
                throw ErrorAt(entry.Line, $"breath names '{words[0]}' but no 'P recruit:{words[0]}' line places them");
            }

            return words[0];
        }

        /// <summary>
        /// A header of comma-separated enemy group names, each on the map as a placement's or a spawn's
        /// group, each listed once: <c>oathbound:</c> (issue 691), every enemy in one oath-bound, and
        /// <c>pair_rule:</c> (issue 692), every enemy in one bound by the pair rule.
        /// </summary>
        private ValueList<string> ParseGroups(Dictionary<string, (string Value, int Line)> header, MapDefinition map, string key)
        {
            if (!header.TryGetValue(key, out var entry))
            {
                return ValueList<string>.Empty;
            }

            var enemies = map.Placements.OfType<EnemyPlacement>().Concat(map.Spawns()).ToList();
            var groups = new List<string>();
            foreach (var group in entry.Value.Split(',', StringSplitOptions.TrimEntries))
            {
                if (group.Length == 0 || enemies.All(e => e.Group != group))
                {
                    throw ErrorAt(entry.Line, $"{key}: no enemy is in group '{group}'");
                }

                if (groups.Contains(group))
                {
                    throw ErrorAt(entry.Line, $"{key}: group '{group}' is listed twice");
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

        /// <summary>
        /// The <c>wind:</c> header (DESIGN.md 13.28): <c>east</c>, or <c>east; turn 4 north</c>, a way
        /// and then each turn it turns, in rising order, every turn from 2 to the turn limit, each a change.
        /// Refused beside <c>dusk:</c> (issue 957).
        /// </summary>
        private WindRule? ParseWind(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            if (!header.TryGetValue("wind", out var entry))
            {
                return null;
            }

            if (map.Dusk is not null)
            {
                throw ErrorAt(entry.Line, "wind: cannot ride with dusk: (issue 957); a dusk board prints the hearing radius unbent, so a windy dusk needs its own decision");
            }

            var parts = entry.Value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var known = "north, east, south, west";
            if (parts.Length == 0 || Wind.Parse(parts[0]) is not { } start)
            {
                throw ErrorAt(entry.Line, $"wind: needs the way it blows first, one of {known}: 'wind: east; turn 4 north', got '{entry.Value}'");
            }

            var shifts = new List<WindShift>();
            var way = start;
            foreach (var part in parts.Skip(1))
            {
                var pieces = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (pieces.Length != 3 || pieces[0] != "turn" || !int.TryParse(pieces[1], out var turn) || Wind.Parse(pieces[2]) is not { } to)
                {
                    throw ErrorAt(entry.Line, $"wind: '{part}' is not a turn and a way, 'turn 4 north' ({known})");
                }

                if (turn < 2 || turn > map.TurnLimit || (shifts.Count > 0 && turn <= shifts[^1].Turn))
                {
                    throw ErrorAt(entry.Line, $"wind: turn {turn} must be after the last, from 2 to the turn limit {map.TurnLimit}");
                }

                if (to == way)
                {
                    throw ErrorAt(entry.Line, $"wind: on turn {turn} it already blows {pieces[2]}; a turn must change it");
                }

                shifts.Add(new WindShift(turn, to));
                way = to;
            }

            return new WindRule(start, ValueList<WindShift>.From(shifts));
        }

        /// <summary>
        /// The <c>seen_far:</c> header (issue 973): a unit id in the content and the tiles it adds,
        /// <c>rook 2</c>, from <see cref="SeenFar.MinExtra"/> to <see cref="SeenFar.MaxExtra"/>. The unit
        /// needs no <c>P</c> line: a campaign seats it from the roster.
        /// </summary>
        private SeenFar? ParseSeenFar(Dictionary<string, (string Value, int Line)> header)
        {
            if (!header.TryGetValue("seen_far", out var entry))
            {
                return null;
            }

            var parts = entry.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[1], out var extra))
            {
                throw ErrorAt(entry.Line, $"seen_far: needs a unit and the tiles it adds, 'seen_far: rook 2', got '{entry.Value}'");
            }

            if (!_content.Units.ContainsKey(parts[0]))
            {
                throw ErrorAt(entry.Line, $"seen_far: '{parts[0]}' is not a unit in the content");
            }

            if (extra < SeenFar.MinExtra || extra > SeenFar.MaxExtra)
            {
                throw ErrorAt(entry.Line, $"seen_far: {extra} tiles is out of range; from {SeenFar.MinExtra} to {SeenFar.MaxExtra}");
            }

            return new SeenFar(parts[0], extra);
        }

        /// <summary>
        /// The <c>goes_home:</c> header (issue 1372): one enemy group placed on the map with a Guard or
        /// Aggressive member, the only behaviors that ever leave a post.
        /// </summary>
        private Homing? ParseHoming(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            if (!header.TryGetValue("goes_home", out var entry))
            {
                return null;
            }

            var parts = entry.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 1)
            {
                throw ErrorAt(entry.Line, $"goes_home: needs one group, 'goes_home: hall', got '{entry.Value}'");
            }

            var members = map.Placements.OfType<EnemyPlacement>().Where(p => p.Group == parts[0]).ToList();
            if (members.Count == 0)
            {
                throw ErrorAt(entry.Line, $"goes_home: no enemy is placed in group '{parts[0]}'");
            }

            if (members.All(m => m.Behavior is not (Behavior.Guard or Behavior.Aggressive)))
            {
                throw ErrorAt(entry.Line, $"goes_home: group '{parts[0]}' has no guard or aggressive member, so nothing in it leaves its post");
            }

            return new Homing(parts[0]);
        }

        /// <summary>
        /// The <c>holds:</c> header (issue 1189): <c>group x,y x,y</c>, an enemy group on the map with a
        /// Guard or Aggressive member and the corners of a rectangle inside the grid in which every member
        /// of the group is placed or spawned (a spawned boss on a one-tile post, issue 1204 lever 3).
        /// </summary>
        private HeldGround? ParseHolds(Dictionary<string, (string Value, int Line)> header, MapDefinition map)
        {
            if (!header.TryGetValue("holds", out var entry))
            {
                return null;
            }

            var parts = entry.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                throw ErrorAt(entry.Line, $"holds: needs a group and two corners, 'holds: mill 0,0 11,2', got '{entry.Value}'");
            }

            var corners = new Coord[2];
            for (var i = 0; i < 2; i++)
            {
                var xy = parts[i + 1].Split(',');
                if (xy.Length != 2 || !int.TryParse(xy[0], out var x) || !int.TryParse(xy[1], out var y))
                {
                    throw ErrorAt(entry.Line, $"holds: corner must be x,y, got '{parts[i + 1]}'");
                }

                if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
                {
                    throw ErrorAt(entry.Line, $"holds: corner {x},{y} is outside the {map.Width}x{map.Height} grid");
                }

                corners[i] = new Coord(x, y);
            }

            var held = new HeldGround(parts[0], corners[0], corners[1]);
            var members = map.Placements.OfType<EnemyPlacement>()
                .Concat(map.Events.Select(e => e.Action).OfType<SpawnEnemy>().Select(s => s.Placement))
                .Where(p => p.Group == held.Group).ToList();
            if (members.Count == 0)
            {
                throw ErrorAt(entry.Line, $"holds: no enemy is in group '{held.Group}'");
            }

            if (members.All(m => m.Behavior is not (Behavior.Guard or Behavior.Aggressive)))
            {
                throw ErrorAt(entry.Line, $"holds: group '{held.Group}' has no guard or aggressive member, so nothing in it moves to hold its ground");
            }

            if (members.FirstOrDefault(m => !held.Contains(m.At)) is { } outside)
            {
                throw ErrorAt(entry.Line, $"holds: {outside.TemplateId} at {outside.At.X},{outside.At.Y} is placed outside the ground its group holds");
            }

            return held;
        }

        /// <summary>
        /// The <c>seize_name:</c> header (issue 1208): the throne tile's name on this map, lowercase
        /// words separated by single spaces; only on <c>win: seize</c>. Absent means the content's name.
        /// </summary>
        private string? ParseSeizeName(Dictionary<string, (string Value, int Line)> header, WinCondition win)
        {
            if (!header.TryGetValue("seize_name", out var entry))
            {
                return null;
            }

            if (win != WinCondition.Seize)
            {
                throw ErrorAt(entry.Line, "seize_name: needs win: seize");
            }

            var words = entry.Value.Split(' ');
            if (words.Any(w => w.Length == 0 || !w.All(c => c is >= 'a' and <= 'z')))
            {
                throw ErrorAt(entry.Line, $"seize_name: '{entry.Value}' must be lowercase words (a to z) separated by single spaces");
            }

            return entry.Value;
        }

        /// <summary>
        /// The <c>seize_hold:</c> header (issue 1274): <c>1</c>, the one enemy phase the captain must
        /// hold the seize tile through; only on <c>win: seize</c>. Absent means the seize is won on the step.
        /// </summary>
        private bool ParseSeizeHold(Dictionary<string, (string Value, int Line)> header, WinCondition win)
        {
            if (!header.TryGetValue("seize_hold", out var entry))
            {
                return false;
            }

            if (win != WinCondition.Seize)
            {
                throw ErrorAt(entry.Line, "seize_hold: needs win: seize");
            }

            if (entry.Value != "1")
            {
                throw ErrorAt(entry.Line, $"seize_hold: '{entry.Value}' must be 1, the one enemy phase the seize tile is held through");
            }

            return true;
        }

        /// <summary>
        /// The <c>shard_race:</c> header (issue 1386, the secret path): the inner tile a beaten boss runs back to
        /// and the phases of his side he holds the shard before he swallows it, <c>shard_race: 0,6 5</c>; only on
        /// <c>win: defeat_boss</c>, the tile inside the map and passable, the phases 1 to 9. Absent means no race. A
        /// third word, <c>secret</c>, makes it the campaign's secret path (issue 1386 slice 2b): held apart with its
        /// <c>race</c> events (<see cref="MapDefinition.SecretRace"/>) until a campaign arms it; <c>armed</c> is that race
        /// once armed, on the board, as a battle's own map text writes it.
        /// </summary>
        private (ShardRace? Race, bool Held) ParseShardRace(Dictionary<string, (string Value, int Line)> header, WinCondition win, MapDefinition map)
        {
            if (!header.TryGetValue("shard_race", out var entry))
            {
                if (map.Events.FirstOrDefault(e => e.Trigger is RaceTrigger) is { } orphan)
                {
                    throw new MapException(_file, 0, $"event '{orphan.Name}' uses the race trigger but the map has no shard_race: header");
                }

                return (null, false);
            }

            if (win != WinCondition.DefeatBoss)
            {
                throw ErrorAt(entry.Line, "shard_race: needs win: defeat_boss");
            }

            var parts = entry.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var word = parts.Length == 3 ? parts[2] : null;
            var tile = parts.Length == 2 || word is "secret" or "armed" ? parts[0].Split(',') : Array.Empty<string>();
            if (tile.Length != 2 || !int.TryParse(tile[0], out var x) || !int.TryParse(tile[1], out var y) || !int.TryParse(parts[1], out var phases))
            {
                throw ErrorAt(entry.Line, $"shard_race: needs the inner tile and the phases, and 'secret' for the campaign's secret path, 'shard_race: 0,6 5', 'shard_race: 0,6 5 secret' or 'armed', got '{entry.Value}'");
            }

            var inner = new Coord(x, y);
            if (!map.Contains(inner))
            {
                throw ErrorAt(entry.Line, $"shard_race: inner tile {inner} is outside the map");
            }

            if (!map.TerrainAt(inner, _content).IsPassable(MovementType.Infantry))
            {
                throw ErrorAt(entry.Line, $"shard_race: inner tile {inner} is impassable");
            }

            if (phases < 1 || phases > ShardRace.MaxPhases)
            {
                throw ErrorAt(entry.Line, $"shard_race: phases {phases} must be 1 to {ShardRace.MaxPhases}");
            }

            if (map.Events.FirstOrDefault(e => e.Trigger is RaceTrigger r && r.Phases >= phases) is { } late)
            {
                throw ErrorAt(entry.Line, $"event '{late.Name}' fires {((RaceTrigger)late.Trigger).Phases} phases into the race, but he swallows after {phases}; a race trigger is 0 to {phases - 1}");
            }

            return (new ShardRace(inner, phases, word is not null), word == "secret");
        }

        /// <summary>The <c>region:</c> header (issue 916): one of the region words; absent means the seam.</summary>
        private MapRegion ParseRegion(Dictionary<string, (string Value, int Line)> header)
        {
            if (!header.TryGetValue("region", out var entry))
            {
                return MapRegion.Seam;
            }

            return MapRegions.Parse(entry.Value)
                ?? throw ErrorAt(entry.Line, $"region names '{entry.Value}'; known: {string.Join(", ", Enum.GetValues<MapRegion>().Select(MapRegions.Word))}");
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

        private Placement ParseUnitLine(string line, int width, int height, ValueList<string> terrain, bool arriving = false, IReadOnlySet<Coord>? retiled = null)
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
            if (!tile.IsPassable(movement) && retiled?.Contains(at) != true)
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
                        "aggressive" when arriving => Behavior.Aggressive,
                        _ => throw Error(arriving
                            ? $"a boss spawn's behavior is boss, guard or aggressive, got '{given}'"
                            : $"a B line's behavior is boss or guard, got '{given}'"),
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
        /// <c>name trigger action</c>. Triggers are <c>turn N player|enemy</c>,
        /// <c>enter x,y</c> and <c>wakes group [late]</c> (issue 1365), among others; actions are <c>terrain x,y glyph</c>, <c>spawn template x,y
        /// group:g behavior:b</c> on an edge tile, and <c>flag name</c>. Names are unique.
        /// </summary>
        private ValueList<MapEvent> ParseEvents(int width, int height, ValueList<string> terrain, int turnLimit, IReadOnlyList<Placement> placements)
        {
            if (AtEnd)
            {
                return ValueList<MapEvent>.Empty;
            }

            _index++;
            var retiled = RetiledTiles(width, height, terrain, turnLimit);
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
                var action = ParseAction(rest, width, height, terrain, edgeOnly: trigger is not (FallsTrigger or RaceTrigger), retiled);
                if (action is SpawnEnemy { Placement.IsBoss: true } && trigger is not TurnTrigger)
                {
                    throw Error("a boss spawn needs a turn trigger, so the boss's arrival is a turn the board can name: 'assault turn 9 enemy spawn boss bandit_leader 0,5 group:assault'");
                }

                if (action is ChangeTerrain { Held: true } && trigger is not EnterTrigger { Tiles.Count: 1 })
                {
                    throw Error("a held terrain change needs an enter trigger on one tile, the tile that holds it: 'north_bar enter 8,1 terrain 8,0 # held'");
                }

                if (trigger is WakesTrigger wakes && !SleepsAtStart(placements, wakes.Group))
                {
                    throw Error($"wakes trigger names group '{wakes.Group}', but no E line places a guard in it, so it never wakes: 'ford wakes fort spawn brigand 0,9 group:ford behavior:aggressive'");
                }

                events.Add(new MapEvent(name, trigger, action));
            }

            return ValueList<MapEvent>.From(events);
        }

        /// <summary>Whether <paramref name="group"/> has a guard among the placements: a group that can wake (issue 1365).</summary>
        private static bool SleepsAtStart(IReadOnlyList<Placement> placements, string group) =>
            placements.OfType<EnemyPlacement>().Any(e => e.Group == group && e.Behavior == Behavior.Guard);

        /// <summary>
        /// The tiles the events block's terrain changes name, read ahead of the block without moving past it
        /// (issue 1259). A spawn on one of them may stand on terrain its template cannot enter, since a
        /// battle's map written back (the protocol state) carries what the change made: the shipped Lazar
        /// House's wall over its north lane. A line that does not parse is left for the block's own pass to refuse.
        /// </summary>
        private HashSet<Coord> RetiledTiles(int width, int height, ValueList<string> terrain, int turnLimit)
        {
            var retiled = new HashSet<Coord>();
            var first = _index;
            for (; !AtEnd; _index++)
            {
                var tokens = Current.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                try
                {
                    var (_, rest) = ParseTrigger(tokens, width, height, turnLimit);
                    if (rest.Length > 0 && rest[0] == "terrain" && ParseAction(rest, width, height, terrain, edgeOnly: false, retiled) is ChangeTerrain change)
                    {
                        retiled.Add(change.At);
                    }
                }
                catch (MapException)
                {
                }
            }

            _index = first;
            return retiled;
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
                case "wakes":
                    if (tokens.Length < 3 || tokens[2].Contains(','))
                    {
                        throw Error("wakes trigger needs a group's name: 'wakes fort'");
                    }

                    return tokens.Length > 3 && tokens[3] == "late"
                        ? (new WakesTrigger(tokens[2], Late: true), tokens[4..])
                        : (new WakesTrigger(tokens[2]), tokens[3..]);
                case "messenger":
                    return (new MessengerTrigger(), tokens[2..]);
                case "falls":
                    if (tokens.Length < 3)
                    {
                        throw Error("falls trigger needs a front's name: 'falls west'");
                    }

                    return (new FallsTrigger(tokens[2]), tokens[3..]);
                case "drop":
                    if (tokens.Length < 3 || !tokens[2].Contains(','))
                    {
                        throw Error("drop trigger needs the ledge's tile: 'drop 5,2'");
                    }

                    return (new DropTrigger(ParseCoord(tokens[2], width, height)), tokens[3..]);
                case "race":
                    if (tokens.Length < 3 || !int.TryParse(tokens[2], out var into) || into < 0 || into >= ShardRace.MaxPhases)
                    {
                        throw Error($"race trigger needs the phases into the shard race, 0 to {ShardRace.MaxPhases - 1}: 'race 2'");
                    }

                    return (new RaceTrigger(into), tokens[3..]);
                default:
                    throw Error($"unknown event trigger '{tokens[1]}'; expected turn, enter, wakes, messenger, falls, drop or race");
            }
        }

        private MapEventAction ParseAction(string[] tokens, int width, int height, ValueList<string> terrain, bool edgeOnly, IReadOnlySet<Coord> retiled)
        {
            if (tokens.Length == 0)
            {
                throw Error("event line needs an action: terrain, spawn, or flag");
            }

            switch (tokens[0])
            {
                case "terrain":
                    var held = tokens.Length == 4 && tokens[3] == "held";
                    if ((tokens.Length != 3 && !held) || tokens[2].Length != 1)
                    {
                        throw Error("terrain action needs a tile and one glyph, and may end in 'held': 'terrain 6,1 =', 'terrain 8,0 # held'");
                    }

                    var at = ParseCoord(tokens[1], width, height);
                    var glyph = tokens[2][0];
                    var tile = _content.TerrainByGlyph(glyph) ?? throw Error($"unknown terrain glyph '{glyph}' in terrain action");
                    return new ChangeTerrain(at, tile.Id, held);
                case "spawn":
                    if (tokens.Length < 3)
                    {
                        throw Error("spawn action needs a template and an edge tile: 'spawn brigand 0,5 group:west behavior:aggressive'");
                    }

                    var boss = tokens[1] == "boss";
                    if (boss && tokens.Length < 4)
                    {
                        throw Error("a boss spawn needs a template and an edge tile: 'spawn boss bandit_leader 0,5 group:assault'");
                    }

                    var placement = ParseUnitLine((boss ? "B " : "E ") + string.Join(' ', tokens[(boss ? 2 : 1)..]), width, height, terrain, arriving: boss, retiled: retiled);
                    if (placement is not EnemyPlacement enemy)
                    {
                        throw Error("spawn action places an enemy");
                    }

                    if (edgeOnly && enemy.At.X != 0 && enemy.At.Y != 0 && enemy.At.X != width - 1 && enemy.At.Y != height - 1)
                    {
                        throw Error($"spawn tile {enemy.At} is not on the edge of the {width}x{height} grid; reinforcements arrive from an edge, unless a front's fall lets them in (falls <front>)");
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

            if (map.Win == WinCondition.DefeatBoss && !map.Placements.Any(p => p is EnemyPlacement { IsBoss: true }) && !map.BossSpawns().Any())
            {
                throw new MapException(_file, 0, "win is defeat_boss but there is no B line and no boss spawn");
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

            if (map.WokenBearer is { } wokenBearer && !map.Placements.Any(p => p is PlayerPlacement { Slot: PlayerSlot.NamedRecruit } n && n.RecruitId == wokenBearer))
            {
                throw new MapException(_file, 0, $"woken names '{wokenBearer}' but no 'P recruit:{wokenBearer}' line places them");
            }

            if (map.ProtectId is { } protect && !map.Placements.Any(p => p is PlayerPlacement { Slot: PlayerSlot.NamedRecruit } n && n.RecruitId == protect))
            {
                throw new MapException(_file, 0, $"protect names '{protect}' but no 'P recruit:{protect}' line places them");
            }
        }
    }
}
