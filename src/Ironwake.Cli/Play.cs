using System.Text;
using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Cli;

/// <summary>One row of <c>recall list</c> (issue 75): the history state a click on it recalls, or null for a heading, and the console's text.</summary>
public sealed record RecallRow(int? State, string Text);

/// <summary>
/// The <c>play</c> command (issue 11): a battle on one map, commands from a script file or
/// standard input, every event printed as a line and the board after every command. The
/// session renders only from events and asks the core for every number through
/// <see cref="Queries"/>, <see cref="Resolver"/>, and <see cref="EnemyAi"/>; it computes
/// nothing about the rules itself (DESIGN.md section 2). Output is plain ASCII. Every
/// attack the session resolves prints its forecast line first, the enemy phase's included
/// (issue 152): the odds behind a hit are on the transcript whichever side threw it, so a
/// reader can tell a misplay from bad luck. Slots count from one at this boundary (issue 101): <c>show</c> lists them from 1 and
/// <c>attack</c>, <c>item</c>, and <c>forecast</c> read them that way; the core counts
/// from zero and is not told. A scripted run is a claim about a game, so it ends with a
/// summary of every rejected line, and <c>--strict</c> stops at the first one.
/// <c>--scheme</c> picks the roll scheme through <see cref="RollSchemes"/>, the same word the
/// Sim's trace takes, and the header line names it, so a transcript says which game it is.
/// <c>--protocol</c> hands the battle to <see cref="ProtocolSession"/> instead: JSON lines in and out (issue 25).
/// </summary>
public sealed class PlaySession
{
    public const string Usage = "usage: ironwake play <map-file|map-name> [--seed N] [--script file] [--strict] [--content dir] [--scheme one|two] [--protocol [--omniscient]] [--confirm-lethal on|off] [--candidate id] [--level N [--company full|depleted|floor]] [--log file]";

    /// <summary>The exit code of a <c>--strict</c> run stopped by a rejection: not a loss (1) and not a usage error (2).</summary>
    public const int StrictStop = 3;

    /// <summary>The refusal when the content directory has no cast file: the roster is content (issue 13), so nothing stands in for it.</summary>
    public const string NoCast = "content has no cast: " + ContentFiles.CastName + " is missing or empty";

    private const string Help = """
        Commands:
          move <unit> <x,y>        Move a unit to a tile in its reach
          move <unit> <x,y> via <x,y>  Move by way of a tile: the cheapest route to it, then on, within the unit's Mov
          move <unit> <x,y> [via <x,y>] preview  The route the move would walk and the planks it would wear, without moving
          attack <unit> <target> [slot|weapon] [art <id>] [!]  Attack an enemy in range, with the weapon in a slot or named, declaring a technique by its id (the forecast prints first); a swing whose counter is lethal to the attacker is refused unless the line ends in !
          item <unit> <slot|item> [ally] Use the item in a slot or named by id; a healing spell names the ally, and so does a tome that raises ground (earthwork under the ally until the caster's next phase ends); a tome that raises the dead names a fallen foe or its tile; an area heal names no one, and `item <unit> <slot> preview` lists whom it would heal; an area tome names a unit or a tile, and `item <unit> <slot> <unit|x,y> preview` lists every strike it would make
          wait <unit>              End the unit's action
          undo <unit>              Take back a unit's move before it acts, if the move was the last command and changed nothing but its tile (no charge)
          canto <unit> <x,y|stay>  After acting, a unit with Move Again moves on what its move left, or stays
          exit <unit>              On an Escape map, leave the board from an exit as the unit's action; the captain's exit ends the battle
          recover <unit>           On a keepsakes map, take the weapon a fallen ally left on the unit's tile, as its action
          open <unit> <x,y>        Open the chest on or beside the unit, as its action; what fits goes to its pack, the rest to the wagon
          drop <unit>              On a ledge, bring the rock down as the action: each tile below takes 10 to anyone on it, and closes unless someone stands there
          talk <unit> <target>     Beside the claimant who came back as a foe: the pick's talk turns them, the captain's spares them
          dash <unit> <x,y>        On a dash map, a unit not yet moved or acted moves with Move +2 (terrain costs the extra), as its whole turn; struck at +15 Acc until its next phase
          shove <unit> <target>    On a shove map, push an adjacent ally one tile away, as the action
          carry <unit> <ally> <x,y> <x,y>  In the campaign, a grown drake lifts an adjacent ally, flies to the first tile and sets it down on the second, as the rider's whole turn
          breathe <unit> <x,y>     In the campaign, once a map, an unbroken drake breathes 3 tiles out through the adjacent tile, as the action: everyone on the line is chilled, Water freezes to Rime ice
          order <press|rally|fall back>  Commander's Word, once a map, as the captain's action: allies within 2 + Cha / 4 of the captain; press +1 Mov to those not moved, rally heals 15 percent, fall back lets those who acted move 2
          order <kind> preview [from <x,y>]  Who the order would reach from where the captain stands, or from a tile
          fallback <unit> <x,y|stay>  After a fall back order, an ally who had acted moves up to 2, if it wakes no one
          cover <unit> <ally>      On a cover map, take the first strike aimed at the ally beside it, as the action
          watch <unit>             On an overwatch map, strike the first foe to end a move in the unit's ring, as the action
          end [!]                  End the player phase; the enemy phase plays out, each enemy attack printing its forecast first. With --confirm-lethal on (the default; never on Tactician), refused while a unit is lethal if all land; end ! ends anyway
          recall <n>               Rewind to history state n, a player-phase state (spends a charge), printing what it undoes
          recall list              Every state recall can return to, the command that made it, and what a rewind there gives back
          recall                   List the state each player turn started at, and the charges left
          forecast <unit> <target> [slot|weapon] [art <id>] [from <x,y>]  Show the forecast without attacking, from any tile the unit can reach
          threat <unit> [from <x,y>]  What each enemy would strike it with next enemy phase, from where it stands or a tile it can reach, on the board as it stands now (a foe freed by a kill mid-phase is not counted, except a tile the unit's own counter-kill frees, one wave deep)
          reach <unit>             Show the board with the unit's reachable tiles marked
          show <unit>              Show a unit's numbers
          terrain [glyph|name]     What the ground does for a unit on it; with no name, every terrain on the board
          about <item>             What a weapon, spell or item is: its numbers and one line on it
          map                      Show the board
          help                     This list
        At dusk no side strikes what it cannot see, and an enemy knows a unit its side sees or that stands within its hearing, the radius the dusk line prints
        Slots count from 1, as show lists them; a weapon may be named instead, by id or name (gust, iron bow)
        A swing moves its weapon to slot 1; a number that names another item than the last listing showed is refused
        A scripted run ends with a summary of every rejected line; --strict stops at the first
        """;

    private readonly GameContent _content;
    private readonly TextWriter _out;
    private readonly bool _scripted;
    private readonly List<(int Line, string Command, string Reason)> _rejections = new();

    /// <summary>
    /// Rivalry's exposure log (issue 16): every recruit that ended a player phase beside a
    /// rival on a tile an awake enemy could strike (<see cref="Rivalry.Exposed"/>, issue 209), with the history length when that phase ended, and whether an enemy attacked
    /// it in the enemy phase that followed. A Recall drops the entries it undoes.
    /// </summary>
    private readonly List<ExposureEntry> _exposure = new();

    /// <summary>
    /// The command applied from each history state, by index (issue 75): entry i is what
    /// turned history state i into the next one, so <c>recall list</c> can name the command
    /// that made each state. A Recall drops the entries it undoes.
    /// </summary>
    private readonly List<string> _made = new();
    private BattleState _state;
    private int _line;

    /// <summary>
    /// The event log (issue 347): every event line the console prints, in order and nothing
    /// else, the same lines a renderer's event log prints. <c>--log</c> writes it to a file,
    /// which is what the thin renderer's parity gate compares against byte for byte.
    /// </summary>
    private readonly StringWriter _log = new() { NewLine = "\n" };

    /// <summary>The event lines printed so far, each ending in <c>\n</c>.</summary>
    internal string EventLog => _log.ToString();

    /// <summary>Prints one event line to the console and to the event log.</summary>
    private void WriteEvent(string line)
    {
        _out.WriteLine(line);
        _log.WriteLine(line);
    }
    private string _command = "";

    /// <summary>
    /// Each unit's pack as it was last listed (issue 1114): item ids by slot, taken for every
    /// unit when the battle opens, on <c>show</c>, and when a refused slot prints the unit's
    /// slots. A typed slot number is checked against it, since a swing moves the weapon it
    /// used to the front and a number read before that swing would name another weapon.
    /// </summary>
    private readonly Dictionary<string, IReadOnlyList<string>> _listed = new();

    private PlaySession(GameContent content, BattleState state, TextWriter output, bool scripted)
    {
        _content = content;
        _state = state;
        _out = output;
        _scripted = scripted;
        foreach (var unit in state.Units)
        {
            Listed(unit);
        }
    }

    /// <summary>Records <paramref name="unit"/>'s pack as listed now (issue 1114).</summary>
    private void Listed(BattleUnit unit) =>
        _listed[unit.Id] = unit.Unit.Inventory.Items.Select(item => item.ItemId).ToList();

    /// <summary>
    /// The refusal for a slot number that no longer holds what the unit's last listing showed
    /// there (issue 1114), or null when the number still holds it or the unit was never listed.
    /// Names both items and the two ways to fix the command, ending before the <c>show</c>
    /// command that the caller appends as typed.
    /// </summary>
    internal static string? StaleSlot(GameContent content, BattleUnit unit, string name, IReadOnlyList<string>? listed, int slot)
    {
        if (listed is null)
        {
            return null;
        }

        var now = unit.Unit.Inventory.Items[slot];
        var was = slot < listed.Count ? listed[slot] : null;
        if (was == now.ItemId)
        {
            return null;
        }

        var wasText = was is null ? "empty" : content.ItemName(was);
        return $"slot {slot + 1} is {Heirloom.Name(now, content)} now; it was {wasText} when {name}'s pack was last listed; name the weapon, or ";
    }

    /// <summary>
    /// A battle inside a campaign (issue 74): the same commands, plus <c>leave</c>, which ends a
    /// decided battle and hands the board back to <see cref="CampaignSession"/>. Script lines count
    /// on from <paramref name="line"/>, so a rejection names its line in the whole campaign script.
    /// </summary>
    internal PlaySession(GameContent content, BattleState state, TextWriter output, bool scripted, int line)
        : this(content, state, output, scripted)
    {
        _campaign = true;
        _line = line;
    }

    private readonly bool _campaign;

    /// <summary>
    /// Whether <c>end</c> is refused while a unit is lethal if all land (issue 1120, DECISIONS/0260):
    /// the player's setting under the difficulty in play (<see cref="Difficulty.AsksOnLethal"/>).
    /// When false, <c>end</c> prints the lethal lines and ends the phase.
    /// </summary>
    internal bool ConfirmLethal { get; init; } = true;

    /// <summary>The fielded finale company's header line under <c>--company</c> (issue 1217), printed after the map line; null otherwise.</summary>
    internal string? CompanyLine { get; init; }

    /// <summary>The board as it stands.</summary>
    internal BattleState State => _state;

    /// <summary>The last script line read.</summary>
    internal int Line => _line;

    /// <summary>Every rejected line, with its line number and reason.</summary>
    internal IReadOnlyList<(int Line, string Command, string Reason)> Rejections => _rejections;

    /// <summary>Whether a campaign battle has been left with <c>leave</c>.</summary>
    internal bool Left { get; private set; }

    /// <summary>Whether a campaign battle has been quit with <c>quit</c> before it was decided (issue 663).</summary>
    internal bool Quit { get; private set; }

    /// <summary>Parses the arguments after <c>play</c>, runs the session, and returns the exit code: 0 on a win, 1 otherwise, 2 for a usage error.</summary>
    public static int Run(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine(Usage);
            return 2;
        }

        ulong seed = 1;
        string? script = null;
        var strict = false;
        var protocol = false;
        var omniscient = false;
        var confirmLethal = true;
        var contentDir = "content";
        var scheme = RollScheme.TwoRollAverage;
        string? candidate = null;
        int? level = null;
        FinaleCompany? company = null;
        string? log = null;
        for (var i = 1; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "--seed" when value is not null && ulong.TryParse(value, out seed):
                    i++;
                    break;
                case "--script" when value is not null:
                    script = value;
                    i++;
                    break;
                case "--strict":
                    strict = true;
                    break;
                case "--protocol":
                    protocol = true;
                    break;
                case "--omniscient":
                    omniscient = true;
                    break;
                case "--confirm-lethal" when OnOff(value) is { } lethalSetting:
                    confirmLethal = lethalSetting;
                    i++;
                    break;
                case "--content" when value is not null:
                    contentDir = value;
                    i++;
                    break;
                case "--candidate" when value is not null:
                    candidate = value;
                    i++;
                    break;
                case "--level" when value is not null && int.TryParse(value, out var parsedLevel) && parsedLevel >= Unit.MinLevel && parsedLevel <= Unit.MaxLevel:
                    level = parsedLevel;
                    i++;
                    break;
                case "--company" when value is not null && FinaleCompanies.Parse(value) is { } parsedCompany:
                    company = parsedCompany;
                    i++;
                    break;
                case "--log" when value is not null:
                    log = value;
                    i++;
                    break;
                case "--scheme" when value is not null && RollSchemes.Parse(value) is { } parsedScheme:
                    scheme = parsedScheme;
                    i++;
                    break;
                default:
                    Console.WriteLine($"ERROR: unexpected argument '{args[i]}'");
                    Console.WriteLine(Usage);
                    return 2;
            }
        }

        if (omniscient && !protocol)
        {
            Console.WriteLine("ERROR: --omniscient applies to a protocol run; the console always shows the player's view");
            Console.WriteLine(Usage);
            return 2;
        }

        if (strict && protocol)
        {
            Console.WriteLine("ERROR: --strict applies to a text script; a protocol run answers every line with ok true or false");
            Console.WriteLine(Usage);
            return 2;
        }

        if (log is not null && protocol)
        {
            Console.WriteLine("ERROR: --log applies to a text run; a protocol run carries each event's text in its answer");
            Console.WriteLine(Usage);
            return 2;
        }

        if (company is not null && level is null)
        {
            Console.WriteLine("ERROR: --company fields a finale company at a level; give --level");
            Console.WriteLine(Usage);
            return 2;
        }

        if (company is not null && candidate is not null)
        {
            Console.WriteLine("ERROR: --company fields a whole company and --candidate a single trial candidate; give one");
            Console.WriteLine(Usage);
            return 2;
        }

        if (strict && script is null)
        {
            Console.WriteLine("ERROR: --strict applies to a scripted run; give --script");
            Console.WriteLine(Usage);
            return 2;
        }

        GameContent content;
        MapDefinition map;
        try
        {
            content = ContentLoader.Load(contentDir);
            map = MapFiles.Load(ResolveMap(args[0], contentDir), content);
        }
        catch (Exception e) when (e is ContentException or MapException)
        {
            Console.WriteLine("ERROR: " + e.Message);
            return 1;
        }

        TextReader input;
        if (script is not null)
        {
            if (!File.Exists(script))
            {
                Console.WriteLine($"ERROR: script file '{script}' not found");
                return 2;
            }

            input = new StringReader(File.ReadAllText(script));
        }
        else
        {
            input = Console.In;
        }

        if (content.Cast.Count == 0)
        {
            Console.Error.WriteLine(NoCast);
            return 2;
        }

        var roster = content.Cast;
        if (candidate is not null)
        {
            if (map.Certification is null)
            {
                Console.WriteLine($"ERROR: --candidate is for a certification map, and '{map.Name}' has no certification header");
                return 2;
            }

            if (content.Cast.FirstOrDefault(u => u.Id == candidate) is not { } unit)
            {
                Console.WriteLine($"ERROR: --candidate names '{candidate}', who is not in the cast");
                return 2;
            }

            roster = ValueList<Unit>.From(new[] { unit });
        }

        // --level raises each member below N to N on the average growth enemies scale by (issue 692),
        // so a board written for a late-campaign company can be played without the campaign before it.
        // --company fields the Sim's finale roster for that company instead (issue 1217), built by the
        // same function the Sim calls, so a hand chair and the measurement cannot drift apart.
        string? companyLine = null;
        if (company is { } fielded && level is { } companyLevel)
        {
            if (!map.DeploysAll)
            {
                Console.WriteLine($"ERROR: --company needs a 'deploy: all' map to seat the whole company, and '{map.Name}' deploys {map.Deploy}");
                return 2;
            }

            roster = FinaleCompanies.Roster(content, fielded, companyLevel);
            var members = roster.Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
            var missing = map.Placements.OfType<PlayerPlacement>().Where(p => p.Slot == PlayerSlot.NamedRecruit && p.RecruitId is { } id && !members.Contains(id)).Select(p => content.Cast.FirstOrDefault(u => u.Id == p.RecruitId)?.Name ?? p.RecruitId).ToList();
            if (missing.Count > 0)
            {
                Console.WriteLine($"ERROR: '{map.Name}' places {string.Join(", ", missing)} by name, and the {FinaleCompanies.Name(fielded)} company has no {string.Join(" or ", missing)}");
                return 2;
            }

            companyLine = FinaleCompanies.Line(fielded, roster, content);
        }
        else if (level is { } floor)
        {
            roster = ValueList<Unit>.From(roster.Select(u => u.ScaledTo(floor, content.Class(u.ClassId))));
        }

        if (protocol)
        {
            return new ProtocolSession(content, BattleState.From(map, content, roster, seed, scheme), Console.Out, omniscient) { ConfirmLethal = confirmLethal }.Run(input);
        }

        var session = new PlaySession(content, BattleState.From(map, content, roster, seed, scheme), Console.Out, scripted: script is not null) { ConfirmLethal = confirmLethal, CompanyLine = companyLine };
        var code = session.Play(input, strict, seed);
        if (log is not null)
        {
            File.WriteAllText(log, session.EventLog);
        }

        return code;
    }

    /// <summary>
    /// A bare map name resolves to <c>&lt;content&gt;/maps/&lt;name&gt;.map</c> when no file
    /// exists at the path given (issue 101), so <c>play the_tollgate</c> works the way the
    /// journals name maps. A path that exists is used as given.
    /// </summary>
    public static string ResolveMap(string mapArg, string contentDir)
    {
        if (File.Exists(mapArg))
        {
            return mapArg;
        }

        var named = Path.Combine(contentDir, "maps", mapArg + ".map");
        return File.Exists(named) ? named : mapArg;
    }

    /// <summary>
    /// Lists the map's events that have not fired, one line each in player words, on a
    /// certification trial and on a map with <c>announce: on</c> (issues 78, 256): when it
    /// fires, what it does, and for a spawn the held-tile rule that stops it. Terrain changes
    /// that share a turn, a phase and a target terrain print as one line (issue 600).
    /// </summary>
    internal void WritePendingEvents()
    {
        foreach (var line in PendingEventLines(_state.Map.Events.Where(mapEvent => !_state.HasFired(mapEvent.Name)), _content, _state.Map.ArrivalsWait))
        {
            _out.WriteLine("  " + line);
        }
    }

    /// <summary>
    /// The announce lines for <paramref name="events"/>, in map order: one line per event,
    /// except that turn-triggered terrain changes to the same terrain on the same turn and
    /// phase are one decision and print as one line at the first one's place, the tiles in map
    /// order and the held-tile rule once (issue 600, the tide sample). Presentation only.
    /// </summary>
    internal static IReadOnlyList<string> PendingEventLines(IEnumerable<MapEvent> events, GameContent content, bool arrivalsWait = false)
    {
        var lines = new List<string>();
        var groups = new Dictionary<(int Turn, Side Phase, string TerrainId), int>();
        var tiles = new List<List<Coord>>();
        var heads = new List<MapEvent?>();
        foreach (var mapEvent in events)
        {
            if (mapEvent is { Trigger: TurnTrigger turn, Action: ChangeTerrain change })
            {
                var key = (turn.Turn, turn.Phase, change.TerrainId);
                if (groups.TryGetValue(key, out var at))
                {
                    tiles[at].Add(change.At);
                    continue;
                }

                groups[key] = lines.Count;
                lines.Add("");
                tiles.Add(new List<Coord> { change.At });
                heads.Add(mapEvent);
                continue;
            }

            lines.Add(DescribeEvent(mapEvent, content, arrivalsWait));
            tiles.Add(new List<Coord>());
            heads.Add(null);
        }

        for (var i = 0; i < lines.Count; i++)
        {
            if (heads[i] is { Trigger: TurnTrigger turn, Action: ChangeTerrain change })
            {
                lines[i] = WhenWords(turn) + ": " + TerrainWords(tiles[i], change.TerrainId, content);
            }
        }

        return lines;
    }

    /// <summary>
    /// A map event in player words, for example <c>turn 3, enemy phase: a rider arrives at
    /// 7,0 (aggressive). A unit standing on 7,0 stops it.</c> The held-tile rule is the one
    /// <see cref="MapEvents"/> applies: a spawn tile with any unit on it blocks the spawn, or, under
    /// <c>arrivals: wait</c> (<paramref name="arrivalsWait"/>, issue 1259), holds it back until the tile is
    /// open. A held bar says it lasts only while its tile is held.
    /// </summary>
    public static string DescribeEvent(MapEvent mapEvent, GameContent content, bool arrivalsWait = false)
    {
        var when = mapEvent.Trigger switch
        {
            TurnTrigger turn => WhenWords(turn),
            EnterTrigger enter => $"when one of yours stops on {string.Join(" or ", enter.Tiles)}",
            WakesTrigger { Late: true } wakes => $"at the start of your next phase after {wakes.Group.Replace('_', ' ')} wakes",
            WakesTrigger wakes => $"when {wakes.Group.Replace('_', ' ')} wakes",
            MessengerTrigger => "if the messenger reaches the road",
            FallsTrigger falls => $"if {falls.Front.Replace('_', ' ')} falls",
            DropTrigger drop => $"when one of yours drops the rock from {drop.Ledge}",
            _ => throw new InvalidOperationException("unknown trigger " + mapEvent.Trigger.GetType().Name),
        };
        var what = mapEvent.Action switch
        {
            SpawnEnemy spawn => SpawnWords(spawn.Placement, content, arrivalsWait),
            ChangeTerrain change when mapEvent.Trigger is DropTrigger => $"{change.At} becomes {content.TerrainById(change.TerrainId).Name.ToLowerInvariant()}, unless anyone stands on it; the rock strikes them for {Rockfall.Damage}, never below 1.",
            ChangeTerrain { Held: true } change when mapEvent.Trigger is EnterTrigger { Tiles: [var holder] } => $"{change.At} becomes {content.TerrainById(change.TerrainId).Name.ToLowerInvariant()} while one of yours stays on {holder}; it gives way when {holder} is left.",
            ChangeTerrain change => TerrainWords(new[] { change.At }, change.TerrainId, content),
            SetFlag flag => $"{flag.Flag} is set.",
            _ => throw new InvalidOperationException("unknown action " + mapEvent.Action.GetType().Name),
        };
        return when + ": " + what;
    }

    private static string WhenWords(TurnTrigger turn) =>
        $"turn {turn.Turn}, {(turn.Phase == Side.Enemy ? "enemy" : "player")} phase";

    /// <summary>
    /// A terrain change to one or more tiles in player words, with the held-tile rule
    /// <see cref="MapEvents"/> applies when the new terrain is impassable to some movement type:
    /// the change does not happen under a unit that could not stand on it (the tide sample,
    /// DESIGN.md 13.21). The rule is printed once however many tiles change.
    /// </summary>
    private static string TerrainWords(IReadOnlyList<Coord> tiles, string terrainId, GameContent content)
    {
        var terrain = content.TerrainById(terrainId);
        var name = terrain.Name.ToLowerInvariant();
        var impassable = Enum.GetValues<MovementType>().Any(movement => !terrain.IsPassable(movement));
        var verb = tiles.Count == 1 ? "becomes" : "become";
        var change = $"{string.Join(" ", tiles)} {verb} {name}";
        return impassable
            ? $"{change}, unless one who cannot enter {name} stands on it."
            : $"{change}.";
    }

    private static string SpawnWords(EnemyPlacement placement, GameContent content, bool arrivalsWait)
    {
        var template = content.Unit(placement.TemplateId);
        var kind = template.Name.ToLowerInvariant();
        var who = template.Named ? template.Name : ("aeiou".Contains(kind[0]) ? "an " : "a ") + kind;
        var behavior = placement.Behavior.ToString().ToLowerInvariant();
        if (placement.IsBoss)
        {
            return $"the boss, {who}, arrives at {placement.At}. A unit standing on {placement.At} does not stop the boss, who takes the nearest free tile.";
        }

        var it = template.Named ? template.Name : "it";
        return arrivalsWait
            ? $"{who} arrives at {placement.At} ({behavior}). A unit or a wall on {placement.At} holds {it} back, and {it} lands at the first enemy phase that starts with {placement.At} open."
            : $"{who} arrives at {placement.At} ({behavior}). A unit standing on {placement.At} stops {it}.";
    }

    private int Play(TextReader input, bool strict, ulong seed)
    {
        _out.WriteLine($"{_state.Map.Name}, seed {seed}, scheme {_state.Scheme}");
        if (CompanyLine is not null)
        {
            _out.WriteLine(CompanyLine);
        }

        if (_state.Map.Certification is { } trialHeader)
        {
            var candidate = _state.UnitsOf(Side.Player).Single();
            _out.WriteLine($"Promotion trial: {candidate.Unit.Name} plays as {_content.Class(trialHeader.ClassId).Name} with {string.Join(", ", trialHeader.Loadout)}");
        }

        _out.WriteLine("Objective: " + Objective.Line(_state, _content));
        if (_state.Map.Certification is not null || _state.Map.Announced)
        {
            WritePendingEvents();
        }
        _out.Write(MapRenderer.Render(_state, _content));
        var commands = 0;
        var stopped = RunCommands(input, strict, ref commands);

        if (_scripted && _rejections.Count > 0)
        {
            _out.WriteLine($"Rejected {_rejections.Count} of {commands} commands:");
            foreach (var (at, command, reason) in _rejections)
            {
                _out.WriteLine($"  line {at}: {command}: {reason}");
            }
        }

        var outcome = _state.Outcome;
        _out.WriteLine(outcome.IsOver
            ? $"Battle {(outcome.Result == BattleResult.Won ? "won" : "lost")}: {UnitNames.Of(_state, _content).Named(Objective.Reason(_state, _content))}"
            : $"Battle ongoing at turn {_state.Turn}, {_state.Phase.ToString().ToLowerInvariant()} phase");
        if (EscapeSummary(_state) is { } escape && outcome.IsOver)
        {
            _out.WriteLine(UnitNames.Of(_state, _content).Message(escape));
        }

        if (_state.Map.Certification is { } trial && outcome.IsOver)
        {
            var className = _content.Class(trial.ClassId).Name;
            _out.WriteLine(outcome.Result == BattleResult.Won
                ? $"Promotion: {UnitNames.Of(_state, _content)[_state.UnitsOf(Side.Player).Single().Id]} earned {className}"
                : $"Promotion: {className} not earned");
        }
        if (_state.Map.RivalryArm is { } arm)
        {
            var attacked = _exposure.Count(entry => entry.Attacked);
            _out.WriteLine($"Rivalry ({arm}): {_exposure.Count} threatened player phases ended beside a rival, {attacked} of them attacked in the enemy phase after");
        }

        return stopped ? StrictStop : outcome.Result == BattleResult.Won ? 0 : 1;
    }

    /// <summary>
    /// Reads and applies commands until the input ends, a campaign battle is left, or, under
    /// <paramref name="strict"/>, a line is rejected; true for the strict stop. Blank lines and
    /// <c>#</c> comments are skipped and not counted.
    /// </summary>
    internal bool RunCommands(TextReader input, bool strict, ref int commands)
    {
        while (!Left && !Quit && input.ReadLine() is { } line)
        {
            _line++;
            var text = line.Trim();
            if (text.Length == 0 || text.StartsWith('#'))
            {
                continue;
            }

            if (_scripted)
            {
                _out.WriteLine("> " + text);
            }

            _command = text;
            commands++;
            Execute(text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (strict && _rejections.Count > 0)
            {
                _out.WriteLine($"Strict: stopped at line {_line} ({text}); no later command applied");
                return true;
            }
        }

        return false;
    }

    private void Execute(string[] words)
    {
        var swingAnyway = words[0] is "attack" or "end" && words.Length > 1 && words[^1] == "!";
        if (swingAnyway)
        {
            words = words[..^1];
        }

        var art = words[0] is "attack" or "forecast" or "item" ? TakeArt(ref words) : null;
        switch (words[0])
        {
            case "move" when ParseMove(words) is { } parsed:
                if (parsed.Preview)
                {
                    PreviewMove(parsed.Move);
                }
                else
                {
                    Apply(parsed.Move);
                }

                break;
            case "move":
                Error("usage: move <unit> <x,y> [via <x,y>] [preview]");
                break;
            case "undo" when words.Length == 2:
                if (Apply(new Undo(words[1])))
                {
                    _exposure.RemoveAll(entry => entry.HistoryAt >= _state.History.Count);
                }

                break;
            case "undo":
                Error("usage: undo <unit>");
                break;
            case "attack" when words.Length >= 3 && IsSlotText(words[3..]):
                if (TrySlot(words[1], SlotText(words[3..]), out var attackSlot) && PrintForecast(words[1], words[2], attackSlot, art, out var lethalCounter))
                {
                    if (lethalCounter is not null && !swingAnyway)
                    {
                        Error(LethalSwingRefusal(lethalCounter), $"{_command} !");
                    }
                    else
                    {
                        Apply(new Attack(words[1], words[2], attackSlot, art));
                    }
                }

                break;
            case "attack":
                Error("usage: attack <unit> <target> [slot|weapon] [art <id>] [!]");
                break;
            case "wait" when words.Length == 2:
                Apply(new Wait(words[1]));
                break;
            case "wait":
                Error("usage: wait <unit>");
                break;
            case "watch" when words.Length == 2:
                Apply(new Watch(words[1]));
                break;
            case "watch":
                Error("usage: watch <unit>");
                break;
            case "cover" when words.Length == 3:
                Apply(new Cover(words[1], words[2]));
                break;
            case "cover":
                Error("usage: cover <unit> <ally>");
                break;
            case "canto" when words.Length == 3 && TryCoord(words[2], out var cantoTo):
                Apply(new Canto(words[1], cantoTo));
                break;
            case "canto" when words.Length == 3 && words[2] == "stay":
                if (Find(words[1]) is { } stayer)
                {
                    Apply(new Canto(stayer.Id, stayer.At));
                }

                break;
            case "canto":
                Error("usage: canto <unit> <x,y|stay>");
                break;
            case "exit" when words.Length == 2:
                Apply(new Exit(words[1]), first: ExitLine(words[1]));
                break;
            case "exit":
                Error("usage: exit <unit>");
                break;
            case "recover" when words.Length == 2:
                Apply(new Recover(words[1]));
                break;
            case "recover":
                Error("usage: recover <unit>");
                break;
            case "open" when words.Length == 3 && TryCoord(words[2], out var chestAt):
                Apply(new Open(words[1], chestAt));
                break;
            case "open":
                Error("usage: open <unit> <x,y>");
                break;
            case "drop" when words.Length == 2:
                Apply(new Drop(words[1]));
                break;
            case "drop":
                Error("usage: drop <unit>");
                break;
            case "talk" when words.Length == 3:
                Apply(new Talk(words[1], words[2]));
                break;
            case "talk":
                Error("usage: talk <unit> <target>");
                break;
            case "order":
                OrderWords(words);
                break;
            case "fallback" when words.Length == 3 && TryCoord(words[2], out var fallTo):
                Apply(new FallBack(words[1], fallTo));
                break;
            case "fallback" when words.Length == 3 && words[2] == "stay":
                if (Find(words[1]) is { } holder)
                {
                    Apply(new FallBack(holder.Id, holder.At));
                }

                break;
            case "fallback":
                Error("usage: fallback <unit> <x,y|stay>");
                break;
            case "dash" when words.Length == 3 && TryCoord(words[2], out var dashTo):
                Apply(new Dash(words[1], dashTo));
                break;
            case "dash":
                Error("usage: dash <unit> <x,y>");
                break;
            case "shove" when words.Length == 3:
                Apply(new Shove(words[1], words[2]));
                break;
            case "shove":
                Error("usage: shove <unit> <target>");
                break;
            case "carry" when words.Length == 5 && TryCoord(words[3], out var carryTo) && TryCoord(words[4], out var setDown):
                Apply(new Carry(words[1], words[2], carryTo, setDown));
                break;
            case "carry":
                Error("usage: carry <unit> <ally> <x,y> <set down x,y>");
                break;
            case "breathe" when words.Length == 3 && TryCoord(words[2], out var toward):
                Apply(new Breathe(words[1], toward));
                break;
            case "breathe":
                Error("usage: breathe <unit> <x,y beside it>");
                break;
            case "end" when words.Length == 1:
                var exposed = _state.Map.RivalryArm is not null && _state.Phase == Side.Player && !_state.Outcome.IsOver
                    ? Rivalry.Exposed(_state, _content).Select(u => new ExposureEntry(_state.History.Count, _state.Turn, u.Id)).ToList()
                    : new List<ExposureEntry>();
                var endNames = UnitNames.Of(_state, _content);
                var lethalNow = Queries.Lethal(_state, _content);
                if (lethalNow.Count > 0 && ConfirmLethal && !swingAnyway && _state.Phase == Side.Player && !_state.Outcome.IsOver)
                {
                    foreach (var lethal in lethalNow)
                    {
                        _out.WriteLine(LethalLine(lethal, endNames));
                    }

                    Error(LethalEndRefusal(lethalNow.Select(l => endNames[l.Unit.Id])), "end !");
                    break;
                }

                var warnings = lethalNow.Select(l => LethalLine(l, endNames))
                    .Concat(EscapeCount.PassedByEnding(_state, _content).Select(c => EscapeCount.Warning(c, _state.Map.TurnLimit, endNames)))
                    .Concat(Wind.ComingLine(_state, _content, endNames, quiet: false) is { } windTurn ? new[] { UnitNames.Sentence(windTurn) } : Array.Empty<string>())
                    .ToList();
                if (Apply(new EndPhase(), first: warnings.Count == 0 ? null : string.Join("\n", warnings)))
                {
                    _exposure.AddRange(exposed);
                    EnemyPhase();
                }

                break;
            case "end":
                Error("usage: end [!]");
                break;
            case "recall" when words.Length == 2 && int.TryParse(words[1], out var index):
                var undone = index >= 0 && index < _state.History.Count ? RecallCost.Of(_state, index) : null;
                if (Apply(new Recall(index), undone is null ? null : "Undone: " + UndoText(undone, UnitNames.Of(_state, _content)) + "\n" + SameRolls))
                {
                    _exposure.RemoveAll(entry => entry.HistoryAt >= index);
                }

                break;
            case "recall" when words.Length == 2 && words[1] == "list":
                ListRecallHistory();
                break;
            case "recall" when words.Length == 1:
                ListRecallTargets();
                break;
            case "recall":
                Error("usage: recall <n> | recall list  (history holds " + _state.History.Count + " states)");
                break;
            case "item" when words.Length == 4 && words[3] == "preview":
                if (TrySlot(words[1], words[2], out var previewSlot))
                {
                    PrintAreaHealPreview(words[1], previewSlot!.Value);
                }

                break;
            case "item" when words.Length == 5 && words[4] == "preview":
                if (TrySlot(words[1], words[2], out var castSlot))
                {
                    PrintAreaCastPreview(words[1], castSlot!.Value, words[3]);
                }

                break;
            case "item" when words.Length is 3 or 4:
                if (TrySlot(words[1], words[2], out var itemSlot))
                {
                    var ally = words.Length == 4 ? words[3] : null;
                    Apply(new UseItem(words[1], itemSlot!.Value, ally, art), first: art is null ? null : HealArtLine(art, ally));
                }

                break;
            case "item":
                Error("usage: item <unit> <slot|item id> [ally] [art <id>]");
                break;
            case "forecast" when TryForecastWords(words, out var slotText, out var from):
                if (TrySlot(words[1], slotText, out var forecastSlot))
                {
                    PrintForecast(words[1], words[2], forecastSlot, art, out _, from);
                }

                break;
            case "forecast":
                Error("usage: forecast <unit> <target> [slot|weapon] [art <id>] [from <x,y>]");
                break;
            case "threat" when words.Length == 2:
                PrintThreat(words[1], null);
                break;
            case "threat" when words.Length == 4 && words[2] == "from" && TryCoord(words[3], out var threatFrom):
                PrintThreat(words[1], threatFrom);
                break;
            case "threat":
                Error("usage: threat <unit> [from <x,y>]");
                break;
            case "reach" when words.Length == 2:
                if (Find(words[1]) is { } mover)
                {
                    _out.Write(MapRenderer.Render(_state, _content, Queries.Reachable(_state, _content, mover)));
                }

                break;
            case "reach":
                Error("usage: reach <unit>");
                break;
            case "show" when words.Length == 2:
                if (Find(words[1]) is { } unit)
                {
                    Show(unit);
                    Listed(unit);
                }

                break;
            case "show":
                Error("usage: show <unit>");
                break;
            case "terrain" when words.Length == 1:
                foreach (var id in TerrainCard.OnBoard(_state.Map))
                {
                    PrintTerrainCard(_content.TerrainById(id));
                }

                break;
            case "terrain" when words.Length == 2:
                if (TerrainCard.Find(_content, words[1]) is { } named)
                {
                    PrintTerrainCard(named);
                }
                else
                {
                    Error($"no terrain '{words[1]}'; name it by its glyph, id or name");
                }

                break;
            case "terrain":
                Error("usage: terrain [glyph|name]");
                break;
            case "about" when words.Length >= 2:
                if (ItemCard.Find(_content, string.Join(' ', words[1..])) is { } itemId)
                {
                    _out.WriteLine(ItemCard.Text(_content, itemId));
                }
                else
                {
                    Error($"no item '{string.Join(' ', words[1..])}'; name it by its id or name");
                }

                break;
            case "about":
                Error("usage: about <item>");
                break;
            case "map":
                _out.Write(MapRenderer.Render(_state, _content));
                if (_state.Map.Announced)
                {
                    WritePendingEvents();
                }

                break;
            case "leave" when _campaign && words.Length == 1:
                if (_state.Outcome.IsOver)
                {
                    Left = true;
                }
                else
                {
                    Error("the battle is not decided; leave comes after it is won or lost");
                }

                break;
            case "difficulty" when _campaign:
                Error("the difficulty is lowered only at a camp, never mid-battle");
                break;
            case "quit" when _campaign && words.Length == 1:
                if (_state.Outcome.IsOver)
                {
                    Error("the battle is decided; leave it instead");
                }
                else
                {
                    Quit = true;
                }

                break;
            case "help":
                _out.WriteLine(Help);
                foreach (var rule in Objective.Rules(_state, _content))
                {
                    _out.WriteLine(rule);
                }

                break;
            default:
                Error($"unknown command '{words[0]}'; type help");
                break;
        }
    }

    /// <summary>
    /// <c>item &lt;unit&gt; &lt;slot&gt; preview</c> (issue 1321 slice 3): an area heal's forecast, every unit it would heal and
    /// by how much, with nothing applied. Any other item is refused: a heal on one ally shows its number when cast.
    /// </summary>
    private void PrintAreaHealPreview(string unitId, int slot)
    {
        if (Find(unitId) is not { } caster)
        {
            return;
        }

        var stack = caster.Unit.Inventory.Items[slot];
        if (!_content.Weapons.TryGetValue(stack.ItemId, out var weapon) || weapon.AreaHeal == 0)
        {
            Error($"only an area heal is previewed; {stack.ItemId} is not one");
            return;
        }

        _out.WriteLine(AreaHeal.Preview(_state, _content, caster, _content.WeaponOf(caster.Unit, weapon)));
    }

    /// <summary>
    /// <c>item &lt;unit&gt; &lt;slot&gt; &lt;unit|x,y&gt; preview</c> (issue 1329): an area tome's forecast, every enemy it would
    /// strike with its hit and damage, with nothing applied. Any other item is refused.
    /// </summary>
    private void PrintAreaCastPreview(string unitId, int slot, string target)
    {
        if (Find(unitId) is not { } caster)
        {
            return;
        }

        var stack = caster.Unit.Inventory.Items[slot];
        if (!_content.Weapons.TryGetValue(stack.ItemId, out var weapon) || weapon.Area == 0)
        {
            Error($"only an area tome is previewed at a target; {stack.ItemId} is not one");
            return;
        }

        if (AreaCast.TileOf(_state, target) is not { } at)
        {
            Error($"no living unit or tile '{target}' to cast at");
            return;
        }

        _out.WriteLine(AreaCast.Preview(_state, _content, caster, _content.WeaponOf(caster.Unit, weapon), at));
    }

    /// <summary>The terrain's card (issue 610) after its glyph: <c>^  Forest. -20 to hit a unit here, ...</c>.</summary>
    private void PrintTerrainCard(Terrain terrain) =>
        _out.WriteLine($"{terrain.Glyph}  {TerrainCard.Text(_state, _content, terrain.Id)}");

    /// <summary>
    /// <c>move &lt;unit&gt; &lt;x,y&gt; [via &lt;x,y&gt;] [preview]</c> (issue 782): the move, with a waypoint
    /// when one is named, and whether only to preview it. Null for any other shape.
    /// </summary>
    private static (Move Move, bool Preview)? ParseMove(string[] words)
    {
        var preview = words.Length > 3 && words[^1] == "preview";
        var rest = preview ? words[..^1] : words;
        if (rest.Length == 3 && TryCoord(rest[2], out var to))
        {
            return (new Move(rest[1], to), preview);
        }

        if (rest.Length == 5 && rest[3] == "via" && TryCoord(rest[2], out var end) && TryCoord(rest[4], out var via))
        {
            return (new Move(rest[1], end, via), preview);
        }

        return null;
    }

    /// <summary>
    /// The move preview (issue 782): <c>preview: Dunstan would move 6,8 -&gt; 7,5 via 6,7 7,7 7,6; wears nothing</c>,
    /// or <c>; would wear 6,5 (Water)</c>, each worn tile with what it would become, then the drake's frost the landing
    /// would fire (issue 1127, <see cref="DrakeFrost.Preview"/>). Nothing moves.
    /// </summary>
    private void PreviewMove(Move move)
    {
        if (Queries.PreviewMove(_state, _content, move, out var rejection) is not { } preview)
        {
            Error(rejection!.Message);
            return;
        }

        var walk = preview.Walk;
        var names = UnitNames.Of(_state, _content);
        var route = walk.Path.Count > 1 ? " via " + string.Join(" ", walk.Path.Take(walk.Path.Count - 1)) : "";
        var wear = preview.Worn.Count == 0
            ? "wears nothing"
            : "would wear " + string.Join(", ", preview.Worn.Select(w => $"{w.At} ({(_content.Terrain.TryGetValue(w.TerrainId, out var terrain) ? terrain.Name : w.TerrainId)})"));
        _out.WriteLine($"preview: {names[walk.UnitId]} would move {walk.From} -> {walk.To}{route}; {wear}");
        if (Find(walk.UnitId) is { } rider && DrakeFrost.Preview(_state, _content, rider, walk.To, names) is { } frost)
        {
            _out.WriteLine(frost);
        }
    }

    /// <summary>
    /// Applies a player command and, if it was accepted, prints <paramref name="first"/>, its
    /// events, then <paramref name="after"/>, then the board. False, with the rejection printed, if it was refused.
    /// </summary>
    private bool Apply(Command command, string? after = null, string? first = null)
    {
        var result = Resolver.Apply(_state, _content, command);
        if (!result.Accepted)
        {
            Error(result.Rejection!.Message);
            return false;
        }

        if (first is not null)
        {
            _out.WriteLine(first);
        }

        Record(command);
        var before = _state;
        _state = result.Next;
        foreach (var e in result.Events)
        {
            WriteEvent(Describe(e, _content, UnitNames.Of(_state, _content)));
        }

        foreach (var notice in Objective.Notices(before, _state, _content, command))
        {
            WriteEvent(notice);
        }

        if (after is not null)
        {
            _out.WriteLine(after);
        }

        if (CantoOwed(command) is { } owed)
        {
            _out.WriteLine($"{UnitNames.Of(_state, _content)[owed.Id]} may move again up to {owed.Canto} movement: canto {owed.Id} <x,y|stay>");
        }

        if (command is not EndPhase)
        {
            _out.Write(MapRenderer.Render(_state, _content));
            AnnounceOutcome();
        }

        return true;
    }

    /// <summary>
    /// Who came out of an Escape map and who did not (issue 269): the units that left
    /// through an exit in the order they left, then the ones left behind and the ones that
    /// fell, the deployed player units missing from both. Null on any other map.
    /// </summary>
    internal static string? EscapeSummary(BattleState state)
    {
        if (state.Map.Win != WinCondition.Escape)
        {
            return null;
        }

        var opening = state.History.Count > 0 ? state.History[0] : state;
        var escaped = state.Escaped.Select(u => u.Id).ToList();
        var behind = state.LeftBehind().Select(u => u.Id).ToList();
        var fell = opening.UnitsOf(Side.Player).Select(u => u.Id)
            .Where(id => !state.HasEscaped(id) && state.Find(id) is null).ToList();
        static string List(List<string> ids) => ids.Count == 0 ? "none" : string.Join(", ", ids);
        return $"escaped: {List(escaped)}; left behind: {List(behind)}; fell: {List(fell)}";
    }

    /// <summary>The player unit an Attack, Item or Wait just left owed a Canto (issue 71), or null.</summary>
    private BattleUnit? CantoOwed(Command command)
    {
        var id = command switch
        {
            Attack a => a.UnitId,
            UseItem i => i.UnitId,
            Wait w => w.UnitId,
            _ => null,
        };
        return id is not null && _state.Find(id) is { Side: Side.Player } unit && _state.CantoReachOf(unit, _content) is not null ? unit : null;
    }

    /// <summary>
    /// Plays the enemy phase from <see cref="EnemyAi.Plan"/>, one command at a time through
    /// the same resolver, and prints the board once it is over. An enemy attack prints the
    /// same forecast line a player's attack does, asked of the core on the board the
    /// resolver is about to read, so the transcript carries the odds of every combat and
    /// not only the player's half (issue 152). The planner only emits attacks the core
    /// forecasts, so a null forecast here is a harness fault, like a rejected command.
    /// A grudge strike prints the planner's best alternative under its forecast (issue 331).
    /// A run of consecutive dark commands prints as one counted line (issue 415), while the
    /// event log keeps one line per command, the protocol's and the client's granularity.
    /// </summary>
    private void EnemyPhase()
    {
        var grudges = new Dictionary<string, EnemyAi.GrudgeStrike?>();
        var darkRun = 0;
        foreach (var command in EnemyAi.Plan(_state, _content))
        {
            var grudge = EnemyAi.GrudgeLog(_state, _content, command, grudges, UnitNames.Of(_state, _content));
            var passed = CoverRule.PassedLine(_state, _content, command);
            if (command is Move or Wait && InTheDark(command))
            {
                var hidden = Resolve(command);
                _state = hidden.Next;
                _log.WriteLine(ProtocolSession.DarkLine);
                darkRun++;
                foreach (var e in hidden.Events.Where(e => e is not UnitMoved and not UnitWaited))
                {
                    darkRun = FlushDarkRun(darkRun);
                    WriteEvent(Describe(e, _content, UnitNames.Of(_state, _content)));
                }

                continue;
            }

            darkRun = FlushDarkRun(darkRun);
            if (passed is not null)
            {
                _out.WriteLine(passed);
            }

            _out.WriteLine("enemy: " + CommandText(command));
            if (command is Attack attack)
            {
                var attacker = _state.Find(attack.UnitId);
                var aimed = _state.Find(attack.TargetId);
                var covered = aimed is null ? null : CoverRule.Swapped(_state, aimed);
                var target = covered?.Struck ?? aimed;
                var board = covered?.Board ?? _state;
                var names = UnitNames.Of(_state, _content);
                if (covered is { } swap)
                {
                    _out.WriteLine($"  Cover: {names[swap.Struck.Id]} takes the strike aimed at {names[aimed!.Id]}");
                }

                var forecast = attacker is null || target is null ? null : Queries.Forecast(board, _content, attacker, target, attacker.At, attack.Slot);
                if (forecast is null)
                {
                    throw new InvalidOperationException($"the enemy AI's {command} has no forecast");
                }

                var rodNote = "";
                var named = target!;
                if (RodHolder(board, _content, attacker!, forecast, attack.Slot) is var (holder, rod))
                {
                    rodNote = LightningRod.ForecastText(_content, holder, names[holder.Id], rod);
                    target = holder;
                }

                var (with, counterWith) = Arms(_content, attacker!, target!, attack.Slot, attacker!.At);
                var (riders, counterRiders) = Riders(_content, attacker!, target!, attack.Slot, forecast);
                _out.WriteLine(ForecastLine(attacker!, named, forecast, rodNote, with, counterWith, RaisesWith(_state, _content, attacker!, attack.Slot), names, riders, counterRiders, CounterUses(_content, target!, forecast.Defender)));
                PrintRivalry(target!, countering: true);
                if (SwornLine(attacker!, target!, names) is { } sworn)
                {
                    _out.WriteLine(UnitNames.Sentence(sworn));
                }

                foreach (var pincer in PincerLines(_state, attacker!, target!, names))
                {
                    _out.WriteLine(UnitNames.Sentence(pincer));
                }

                foreach (var brace in BraceLines(attacker!, target!, names))
                {
                    _out.WriteLine(UnitNames.Sentence(brace));
                }

                foreach (var line in BreakLines(_state, _content, attacker!, target!, names))
                {
                    _out.WriteLine(UnitNames.Sentence(line));
                }

                foreach (var line in HungerLines(_content, attacker!, target!, attack.Slot, forecast.Defender.Strikes, names, _state, forecast))
                {
                    _out.WriteLine(UnitNames.Sentence(line));
                }

                foreach (var ignite in IgniteLines(_state, _content, attacker!, target!, attacker!.At, attack.Slot, forecast.Defender.Strikes, names))
                {
                    _out.WriteLine(UnitNames.Sentence(ignite));
                }

                foreach (var windup in WindupLines(_state, _content, attacker!, target!, attack.Slot, names))
                {
                    _out.WriteLine(UnitNames.Sentence(windup));
                }

                if (grudge is not null)
                {
                    _out.WriteLine(UnitNames.Sentence("  " + grudge));
                }
            }

            var before = _state;
            var result = Resolve(command);
            _state = result.Next;
            foreach (var e in result.Events)
            {
                WriteEvent(Describe(e, _content, UnitNames.Of(_state, _content)));
                if (e is CombatFought fought)
                {
                    foreach (var entry in _exposure.Where(x => x.UnitId == fought.TargetId && x.Turn == fought.Turn))
                    {
                        entry.Attacked = true;
                    }
                }
            }

            foreach (var notice in Objective.Notices(before, _state, _content, command))
            {
                WriteEvent(notice);
            }
        }

        FlushDarkRun(darkRun);
        _out.Write(MapRenderer.Render(_state, _content));
        AnnounceOutcome();
    }

    /// <summary>
    /// Prints a pending run of dark commands to the console as one line, counted when the run
    /// is longer than one (issue 415); the count never tells a move from a wait (issue 301).
    /// Returns the emptied run.
    /// </summary>
    private int FlushDarkRun(int run)
    {
        if (run > 0)
        {
            _out.WriteLine(DarkRunLine(run));
        }

        return 0;
    }

    /// <summary>The console line for <paramref name="run"/> consecutive dark commands: the bare dark line for one, counted for more.</summary>
    public static string DarkRunLine(int run) => run == 1 ? ProtocolSession.DarkLine : $"{ProtocolSession.DarkLine} (x{run})";

    /// <summary>
    /// Applies one of the enemy planner's commands and records it; the planner only emits
    /// commands the resolver accepts, so a refusal is a harness fault.
    /// </summary>
    private ApplyResult Resolve(Command command)
    {
        var result = Resolver.Apply(_state, _content, command);
        if (!result.Accepted)
        {
            throw new InvalidOperationException($"the enemy AI's {command} was rejected: {result.Rejection!.Message}");
        }

        Record(command);
        return result;
    }

    /// <summary>
    /// Whether an enemy's Move or Wait happens where no player unit sees it, before and after
    /// (DESIGN.md 13.7): the console then prints only that something moved, since naming the
    /// unit or its tiles would light the dark. A Move that ends in sight prints in full.
    /// </summary>
    private bool InTheDark(Command command) => Dusk.InTheDark(_state, _content, command);

    /// <summary>
    /// Prints the history index at which each player phase in the history began, so a
    /// player can find the n for <c>recall n</c> (issue 190). Spends nothing.
    /// </summary>
    private void ListRecallTargets()
    {
        var starts = _state.RecallTargets()
            .Where(i => i == 0 || _state.History[i - 1].Phase != Side.Player)
            .Select(i => $"turn {_state.History[i].Turn} state {i}")
            .ToList();
        var list = starts.Count == 0 ? "none yet" : string.Join(", ", starts);
        _out.WriteLine($"Player turns start at: {list}; history holds {_state.History.Count} states; {ChargesLeft(_state.RecallCharges)}");
        _out.WriteLine(RollsFixed);
    }

    /// <summary>
    /// The line under the bare <c>recall</c> listing (issue 1352): rolls are keyed by the turn and
    /// the strike (section 7), so a reordered turn after a Recall draws the same dice; said before
    /// a charge is spent, not after.
    /// </summary>
    public const string RollsFixed = "Rolls are fixed by the turn: a recall changes the plan, not the dice.";

    /// <summary>How many Recall charges are left, as printed: "1 charge left", "N charges left" (issue 1066).</summary>
    public static string ChargesLeft(int charges) => charges == 1 ? "1 charge left" : $"{charges} charges left";

    /// <summary>The line a rewind prints under what it undoes: rolls are keyed (section 7), so a Recall is a choice and never a reroll.</summary>
    public const string SameRolls = "The rolls do not change: the same attack will roll the same";

    /// <summary>
    /// Keeps <see cref="_made"/> in step with the history for an accepted command, before the
    /// state moves: a Recall truncates it to the state it returns to, an Undo drops the move
    /// it takes back (issue 676), anything else names the command applied from the state it leaves.
    /// </summary>
    private void Record(Command command)
    {
        if (command is Recall recall)
        {
            _made.RemoveRange(recall.ToIndex, _made.Count - recall.ToIndex);
            return;
        }

        if (command is Undo)
        {
            var kept = _state.History.Count - 1;
            if (_made.Count > kept)
            {
                _made.RemoveRange(kept, _made.Count - kept);
            }

            return;
        }

        if (_made.Count == _state.History.Count)
        {
            _made.Add(CommandText(command));
        }
    }

    /// <summary>
    /// The Recall browser (issue 75): every history state a Recall may return to, with the
    /// turn, the command that made it, and what a rewind there gives back, from
    /// <see cref="RecallCost.Of"/>, which the rewind itself prints too. Spends nothing.
    /// </summary>
    private void ListRecallHistory()
    {
        foreach (var row in RecallRows(_state, _content, _made))
        {
            _out.WriteLine(row.Text);
        }
    }

    /// <summary>
    /// What <c>recall list</c> prints (issue 75), one row per line: the charges, then each
    /// history state a Recall may return to with its index, or why there is none.
    /// <paramref name="made"/> is the command applied from each history state, by index, as
    /// <see cref="CommandText"/> names it. The thin renderer's Recall browser shows the same
    /// rows and recalls the row clicked (issue 353). Units are named as a reader sees them,
    /// in sentence case (issue 615); the command a state came after keeps its ids, as typed.
    /// </summary>
    public static IReadOnlyList<RecallRow> RecallRows(BattleState state, GameContent content, IReadOnlyList<string> made)
    {
        var names = UnitNames.Of(state, content);
        var total = state.Map.RecallCharges;
        var spent = total - state.RecallCharges;
        var rows = new List<RecallRow>
        {
            new(null, $"Recall: {state.RecallCharges} of {total} {(total == 1 ? "charge" : "charges")} left, {spent} spent; a spent charge does not come back, and the same attack will roll the same"),
        };
        if (state.RecallCharges < 1)
        {
            rows.Add(new(null, "  No charges left: nothing more can be recalled on this map"));
            return rows;
        }

        var targets = state.RecallTargets().ToList();
        if (targets.Count == 0)
        {
            rows.Add(new(null, "  No state to return to yet"));
            return rows;
        }

        foreach (var i in targets)
        {
            var from = i == 0
                ? "the start"
                : state.History[i - 1].Phase != Side.Player
                    ? "turn start"
                    : i - 1 < made.Count ? "after " + made[i - 1] : "after ?";
            rows.Add(new(i, $"  State {i}  turn {state.History[i].Turn}  {from}  undoes: {UndoText(RecallCost.Of(state, i), names)}"));
        }

        return rows;
    }

    /// <summary>
    /// A rewind's cost in the console's words, the player's gains given back first, then what
    /// comes back to the player. A number printed next to a unit's name is that unit's own
    /// number (issue 552): a returned unit is named with the HP it comes back at, and each other
    /// unit with the HP it gets back, read from <see cref="RecallCost.HpByUnit"/>. Units read by
    /// <paramref name="names"/> (issue 615): <c>returns Teodor alive at 17 hp, 14 hp to Wren</c>.
    /// A talk is named apart from the kills (issue 826): <c>gives back Keziah's talk (spared)</c>;
    /// so are a messenger's escape and a freeing (issue 829): <c>gives back Rider's escape</c>, <c>gives back Brigand freed</c>.
    /// </summary>
    public static string UndoText(RecallCost cost, UnitNames names)
    {
        if (cost.IsEmpty)
        {
            return "moves only";
        }

        var back = new List<string>();
        if (cost.KillsGivenBack.Count > 0)
        {
            back.Add($"{cost.KillsGivenBack.Count} {(cost.KillsGivenBack.Count == 1 ? "kill" : "kills")} ({string.Join(", ", cost.KillsGivenBack.Select(id => names[id]))})");
        }

        if (cost.TalkGivenBack is { } talk)
        {
            back.Add($"{names[talk.Id]}'s talk ({talk.Fate.ToString().ToLowerInvariant()})");
        }

        if (cost.EscapeGivenBack is { } escape)
        {
            back.Add($"{names[escape]}'s escape");
        }

        if (cost.FreedGivenBack is { } freed)
        {
            back.Add($"{names[freed]} freed");
        }

        if (cost.ExpGivenBack > 0)
        {
            back.Add($"{cost.ExpGivenBack} exp");
        }

        if (cost.LevelsGivenBack > 0)
        {
            back.Add($"{cost.LevelsGivenBack} {(cost.LevelsGivenBack == 1 ? "level" : "levels")}");
        }

        if (cost.EnemyHpBack > 0)
        {
            back.Add($"{cost.EnemyHpBack} enemy hp");
        }

        var returned = new List<string>();
        returned.AddRange(cost.UnitsReturned.Select(id => $"{names[id]} alive at {cost.HpFor(id)} hp"));
        returned.AddRange(cost.HpByUnit.Where(entry => !cost.UnitsReturned.Contains(entry.Id)).Select(entry => $"{entry.Hp} hp to {names[entry.Id]}"));

        returned.AddRange(cost.ArrivalsUndone.Select(id => names[id] + " not yet arrived"));
        var parts = new List<string>();
        if (back.Count > 0)
        {
            parts.Add("gives back " + string.Join(", ", back));
        }

        if (returned.Count > 0)
        {
            parts.Add("returns " + string.Join(", ", returned));
        }

        return string.Join("; ", parts);
    }

    private void AnnounceOutcome()
    {
        var outcome = _state.Outcome;
        if (outcome.IsOver)
        {
            var after = _state.RecallCharges > 0
                ? "only recall is left" + (_campaign ? ", or leave" : "")
                : "no recall is left" + (_campaign ? ", so leave" : "");
            _out.WriteLine($"Battle {(outcome.Result == BattleResult.Won ? "won" : "lost")}: {UnitNames.Of(_state, _content).Named(Objective.Reason(_state, _content))}; {after}");
            if (Objective.Verdict(_state, _content) is { } verdict)
            {
                _out.WriteLine(verdict);
            }
        }
    }

    /// <summary>
    /// The line a heal art prints before its events (issue 635, round 261): the art, then the ally
    /// held on its tile, whose phase the art ends.
    /// </summary>
    private string HealArtLine(string art, string? ally) =>
        _state.Find(ally ?? "") is { } held
            ? $"{AbilityName(art, _content)}: {UnitNames.Of(_state, _content)[held.Id]} holds {held.At}; phase ends"
            : AbilityName(art, _content);

    /// <summary>
    /// Takes <c>art &lt;id&gt;</c> out of an <c>attack</c>, <c>forecast</c> or <c>item</c> line (issues 68, 635),
    /// after the unit and the target, and returns the id; the remaining words parse as
    /// before. A trailing <c>art</c> with no id is left for the usage error.
    /// </summary>
    private static string? TakeArt(ref string[] words)
    {
        for (var i = 3; i + 1 < words.Length; i++)
        {
            if (words[i] == "art")
            {
                var id = words[i + 1];
                words = words.Take(i).Concat(words.Skip(i + 2)).ToArray();
                return id;
            }
        }

        return null;
    }

    /// <summary>
    /// True when the words after the target can be an <c>attack</c> or <c>forecast</c> slot
    /// (issue 477): none, one number, or a weapon's id or name, whose words are letters,
    /// <c>_</c>, <c>-</c> and <c>'</c> only, so a coordinate or a stray keyword stays a usage error.
    /// </summary>
    private static bool IsSlotText(string[] words) =>
        words.Length == 0
        || (words.Length == 1 && int.TryParse(words[0], out _))
        || words.All(word => word is not ("from" or "art") && word.All(c => char.IsAsciiLetter(c) || c is '_' or '-' or '\''));

    /// <summary>The slot words joined with one space, or null when there are none.</summary>
    private static string? SlotText(string[] words) => words.Length == 0 ? null : string.Join(' ', words);

    /// <summary>
    /// Reads the words after <c>forecast &lt;unit&gt; &lt;target&gt;</c>: an optional slot or
    /// weapon name, then an optional <c>from &lt;x,y&gt;</c> (issues 151, 477). False when they
    /// are anything else.
    /// </summary>
    private static bool TryForecastWords(string[] words, out string? slotText, out Coord? from)
    {
        slotText = null;
        from = null;
        if (words.Length < 3)
        {
            return false;
        }

        var next = 3;
        var fromAt = Array.IndexOf(words, "from", next);
        var slotWords = words[next..(fromAt < 0 ? words.Length : fromAt)];
        if (!IsSlotText(slotWords))
        {
            return false;
        }

        slotText = SlotText(slotWords);
        next += slotWords.Length;

        if (next < words.Length)
        {
            if (words[next] != "from" || next + 1 != words.Length - 1 || !TryCoord(words[next + 1], out var at))
            {
                return false;
            }

            from = at;
            next += 2;
        }

        return next == words.Length;
    }

    /// <summary>
    /// Prints the forecast line, from the unit's own tile or from <paramref name="from"/>,
    /// a tile it could still move to (issue 151). The from-tile line names the tile and
    /// its terrain, since the terrain is what the player is choosing between.
    /// </summary>
    private bool PrintForecast(string unitId, string targetId, int? slot, string? art, out string? lethalCounter, Coord? from = null)
    {
        lethalCounter = null;
        if (Find(unitId) is not { } unit)
        {
            return false;
        }

        var tile = from ?? unit.At;
        if (tile != unit.At && !Queries.CanStandOn(_state, _content, unit, tile))
        {
            Error(unit.Moved ? $"{unit.Id} has already moved this phase; forecast from {unit.At}" : $"{unit.Id} cannot move to {tile}");
            return false;
        }

        if (Find(targetId, unit.Id, tile) is not { } target)
        {
            return false;
        }

        var forecast = Queries.Forecast(_state, _content, unit, target, tile, slot, art);
        if (forecast is null)
        {
            Error(Queries.WeaponRefusal(_content, unit, slot, art)?.Message ?? $"{unit.Id} cannot attack {target.Id} from {tile}");
            return false;
        }

        _out.WriteLine(ForecastText(_state, _content, unit, target, forecast, tile, from is not null, slot, art));
        lethalCounter = LethalCounterLine(unit, target, forecast, RaisesWith(_state, _content, unit, slot), UnitNames.Of(_state, _content));
        return true;
    }

    /// <summary>
    /// Everything the console prints for a forecast, one line per row: the forecast line,
    /// then the art line, the rivalry line and the pending-retreat lines when they apply. Shared with the
    /// protocol's forecast query (issue 25), whose <c>text</c> is exactly this.
    /// <paramref name="fromTile"/> is true for a forecast asked from a tile the unit has not
    /// moved to, whose line names the tile and its terrain.
    /// Every line, the experiment sub-lines included, names units as a reader sees them (issue 615);
    /// the sub-line builders keep ids when called without names.
    /// </summary>
    public static string ForecastText(BattleState state, GameContent content, BattleUnit unit, BattleUnit target, CombatForecast forecast, Coord tile, bool fromTile, int? slot = null, string? art = null)
    {
        var where = fromTile ? $" from {tile} ({state.Map.TerrainAt(tile, content).Name})" : "";
        var names = UnitNames.Of(state, content);
        var aimed = target;
        if (RodHolder(state, content, unit, forecast, slot) is var (holder, rod))
        {
            where += LightningRod.ForecastText(content, holder, names[holder.Id], rod);
            target = holder;
        }

        var (with, counterWith) = Arms(content, unit, target, slot, tile);
        var raises = RaisesWith(state, content, unit, slot);
        var (riders, counterRiders) = Riders(content, unit, target, slot, forecast, state);
        var lines = new List<string> { ForecastLine(unit, aimed, forecast, where, with, counterWith, raises, names, riders, counterRiders, CounterUses(content, target, forecast.Defender)) };
        if (SeenRolls.Line(state, state.Turn, state.Phase, unit.Id, names[unit.Id], forecast.Attacker.HitChance, target.Id, names[target.Id], forecast.Defender.Strikes ? forecast.Defender.HitChance : null) is { } seen)
        {
            lines.Add("  " + seen);
        }

        var hunger = HungerLines(content, unit, target, slot, forecast.Defender.Strikes, names, state, forecast).ToList();
        if (ClaimantLine(state, unit, target, forecast, raises, names, hunger) is { } claimant)
        {
            lines.Add(claimant);
        }

        if (PullLine(unit, target, forecast, names) is { } pull)
        {
            lines.Add(pull);
        }

        if (LethalCounterLine(unit, target, forecast, raises, names) is { } lethal)
        {
            if (FirstRoundKillLine(unit, target, forecast, raises) is { } kills)
            {
                lines.Add(kills);
            }

            lines.Add(lethal);
        }

        if (art is not null)
        {
            lines.Add(ArtLine(content, unit, forecast, slot, art));
        }

        if (RivalryLine(state, content, unit with { At = tile }, countering: false, names) is { } rivalry)
        {
            lines.Add(rivalry);
        }

        if (SwornLine(unit, target, names) is { } sworn)
        {
            lines.Add(sworn);
        }

        lines.AddRange(PincerLines(state, unit with { At = tile }, target, names));
        lines.AddRange(BraceLines(unit, target, names));
        lines.AddRange(OpenLines(unit, target, names));
        lines.AddRange(BreakLines(state, content, unit, target, names));
        lines.AddRange(hunger);
        lines.AddRange(SignatureLines(state, content, unit with { At = tile }, target, forecast, names));
        lines.AddRange(IgniteLines(state, content, unit, target, tile, slot, forecast.Defender.Strikes, names));
        lines.AddRange(WindupLines(state, content, unit with { At = tile }, target, slot, names));
        lines.AddRange(PendingRetreatLines(state, content, unit, tile, target, forecast, names));
        if (FightWakesLine(state, content, unit, target, tile) is { } wakes)
        {
            lines.Add(wakes);
        }

        return UnitNames.Sentence(string.Join("\n", lines));
    }

    /// <summary>
    /// The row under a yard forecast whose teacher's blows would reach a kill (issue 1331, Table round 448):
    /// <c>  pulls: Alder Fenn stops at 1 HP on Brigand; the kill is the student's</c> when the teacher strikes,
    /// <c>  counter pulls: ...</c> when the teacher answers. The teacher's blows never take a drill hand
    /// below 1 HP (<see cref="Combatant.Pulls"/>). Null anywhere else and when no blow would reach.
    /// </summary>
    public static string? PullLine(BattleUnit unit, BattleUnit target, CombatForecast forecast, UnitNames names)
    {
        if (unit.Yard is { Teaches: true } && forecast.Attacker.Strikes && forecast.AttackerDamageLivedFor(unit.Hp) >= target.Hp)
        {
            return $"  pulls: {names[unit.Id]} stops at 1 HP on {names[target.Id]}; the kill is the student's";
        }

        return target.Yard is { Teaches: true } && forecast.CounterIfAllLand >= unit.Hp
            ? $"  counter pulls: {names[target.Id]} stops at 1 HP on {names[unit.Id]}; the kill is the student's"
            : null;
    }

    /// <summary>
    /// The row under a forecast whose fight would wake a sleeping group by its noise (issue 1106), in
    /// <c>threat</c>'s words: <c>  fighting here wakes: the field group (noise, heard from 10,2)</c>, naming the
    /// fight's tiles that reach the group, and <c>(called by ...)</c> for a group a <c>wake_links:</c> call brings.
    /// Read from <see cref="Queries.FightWakes"/>, so a group the stop alone wakes is <c>threat</c>'s line, not
    /// this one. Null when the fight wakes nothing.
    /// </summary>
    public static string? FightWakesLine(BattleState state, GameContent content, BattleUnit unit, BattleUnit target, Coord tile)
    {
        var wakes = Queries.FightWakes(state, content, unit, target, tile);
        return wakes.Count == 0
            ? null
            : $"  fighting here wakes: {string.Join(", ", wakes.Select(w => $"{UnitNames.Group(w.Group)} ({(w.CalledBy is { } by ? "called by " + UnitNames.Group(by) : "noise, heard from " + string.Join(" and ", w.HeardFrom))})"))}";
    }

    /// <summary>
    /// The row under a <c>threat</c> line whose strike would wake a sleeping group by its noise (issue 1290),
    /// in the forecast's words (<see cref="FightWakesLine"/>):
    /// <c>    its strike here wakes: the yard group (noise, heard from 7,5); not in the total</c>, unpriced. At dusk a group
    /// with no member the player sees is left out. Null when the strike wakes nothing.
    /// </summary>
    private static string? StrikeWakesRow(BattleState state, ThreatLine line)
    {
        var wakes = line.Wakes.Where(w => state.Units.Any(u => u.Group == w.Group && Dusk.Seen(state, u))).ToList();
        return wakes.Count == 0
            ? null
            : $"    its strike here wakes: {string.Join(", ", wakes.Select(w => $"{UnitNames.Group(w.Group)} ({(w.CalledBy is { } by ? "called by " + UnitNames.Group(by) : "noise, heard from " + string.Join(" and ", w.HeardFrom))})"))}; not in the total";
    }

    /// <summary>
    /// The first row under a forecast that would kill the returned claimant (issue 1068, Design Table round 372),
    /// from any attacker: <c>  kills on hit: Rook falls for good (the claimant)</c> when one plain strike reaches
    /// her HP, <c>  kills if all land: ...</c> when the strikes the attacker lives for do
    /// (<see cref="CombatForecast.AttackerDamageLivedFor"/>); and with her as the striker, <c>  counter kills on hit: ...</c>
    /// when the counter is lethal (<see cref="CombatForecast.CounterIsLethal"/>). A Kinsbane feed row in
    /// <paramref name="hunger"/> that already carries the clause is taken out of it and printed here instead, so the
    /// loss leads and never prints twice. Null for any other unit, a windup and a forecast that cannot kill her.
    /// </summary>
    private static string? ClaimantLine(BattleState state, BattleUnit unit, BattleUnit target, CombatForecast forecast, bool raises, UnitNames names, List<string> hunger)
    {
        var (falls, label) = raises ? (null, "")
            : Returned.Falls(state, target, names) is { } struck && forecast.Attacker.Strikes && forecast.AttackerDamageLivedFor(unit.Hp) >= target.Hp
                ? (struck, forecast.Attacker.Damage >= target.Hp ? "kills on hit:" : "kills if all land:")
            : Returned.Falls(state, unit, names) is { } striking && forecast.CounterIsLethal(unit.Hp, target.Hp)
                ? (striking, Kinsbane.CounterKillLabel(forecast, unit))
            : ((string?)null, "");
        if (falls is null)
        {
            return null;
        }

        if (hunger.FirstOrDefault(h => h.Contains(falls, StringComparison.Ordinal)) is { } merged)
        {
            hunger.Remove(merged);
            return merged;
        }

        return $"  {label} {falls}";
    }

    /// <summary>
    /// What the captain's <c>exit</c> costs, printed before it resolves (issue 928): <c>Exit: leaves Teodor behind</c>,
    /// every player unit still on the board by name, and in a campaign that being left behind counts as
    /// falling (<see cref="CampaignRecord"/> scores it so on every map). Null for anyone but the captain,
    /// and when nobody would be left.
    /// </summary>
    private string? ExitLine(string unitId) => ExitLine(_state, _content, unitId, _campaign);

    /// <summary>
    /// What <c>exit</c> prints before the captain leaves an Escape map with others still on it:
    /// <c>Exit: leaves Wren, Pell behind</c>, with <c>(left behind counts as fallen)</c> in a
    /// campaign; null for anyone else or with no one left. The client's exit row reads the same
    /// line (issue 1308).
    /// </summary>
    public static string? ExitLine(BattleState state, GameContent content, string unitId, bool campaign)
    {
        if (state.Find(unitId) is not { IsCaptain: true } captain || state.Map.Win != WinCondition.Escape)
        {
            return null;
        }

        var names = UnitNames.Of(state, content);
        var left = state.UnitsOf(Side.Player).Where(u => u.Id != captain.Id).Select(u => names[u.Id]).ToList();
        return left.Count == 0
            ? null
            : $"Exit: leaves {string.Join(", ", left)} behind" + (campaign ? " (left behind counts as fallen)" : "");
    }

    /// <summary>
    /// What <c>end</c> prints before a player phase ends for a unit the coming enemy phase kills
    /// if every strike <c>threat</c> prices lands (issue 558, <see cref="Queries.Lethal"/>):
    /// <c>Lethal if all land: Wren (Soldier 2 for 9, Archer for 6, against 15 hp)</c>, each unit by
    /// the name a reader sees (issue 615), the <c>for</c> keeping a numbered name off its damage.
    /// A strike a tile freed by the unit's own counter-kill lets in (<see cref="LethalThreat.Freed"/>, issue 1191)
    /// follows the others with its tile and the counter's chance:
    /// <c>Lethal if all land: Teodor (Brigand 1 for 9, Brigand 2 for 9 on 4,0 if Teodor's counter kills Brigand 1 (81 hit), against 13 hp)</c>.
    /// </summary>
    public static string LethalLine(LethalThreat lethal, UnitNames names) =>
        $"Lethal if all land: {names[lethal.Unit.Id]} ({string.Join(", ", lethal.Strikers.Select(s => $"{names[s.Enemy.Id]} for {s.Damage}").Concat(lethal.Freed.Select(f => $"{names[f.Follower.Id]} for {f.Damage} on {f.Tile} if {names[lethal.Unit.Id]}'s counter kills {names[f.Freer.Id]} {FreedChance(f)}")))}, against {lethal.Unit.Hp} hp)";

    /// <summary>
    /// The chance a freeing counter (issue 1191) is read at: its displayed hit, <c>(81 hit)</c> when one
    /// counter strike kills, else <c>(81 hit, all counters landing)</c>, the split <see cref="Kinsbane.CounterKillLabel"/> draws.
    /// </summary>
    public static string FreedChance(FreedStrike freed) =>
        freed.Counter.Defender.Damage >= freed.Freer.Hp ? $"({freed.Counter.Defender.DisplayedHit} hit)" : $"({freed.Counter.Defender.DisplayedHit} hit, all counters landing)";

    /// <summary>The value of an <c>on|off</c> flag (issue 1120), or null when it is neither.</summary>
    internal static bool? OnOff(string? value) => value switch
    {
        "on" => true,
        "off" => false,
        _ => null,
    };

    /// <summary>
    /// The refusal of an <c>end</c> while a unit is lethal if all land (issue 1093): the warned death is
    /// read before the phase ends, not after, so <c>end</c> prints the lethal lines and ends nothing.
    /// The refusal reads <c>lethal if all land: Wren, Maud; add ! to end anyway: </c>, and the caller
    /// follows it with <c>end !</c>, which prints the same lines and ends the phase. Bait stays legal.
    /// </summary>
    public static string LethalEndRefusal(IEnumerable<string> names) =>
        $"lethal if all land: {string.Join(", ", names)}; add ! to end anyway: ";

    /// <summary>
    /// The refusal of an <c>attack</c> whose forecast carries <paramref name="lethalCounter"/> (issue 975):
    /// the attacker's own death is the one printed consequence a typed line does not execute unasked,
    /// so the line is refused and nothing is spent. The refusal reads
    /// <c>counter is lethal to Keziah (14 against 12 hp); add ! to swing anyway: </c>, and the caller
    /// follows it with the line as typed and <c>!</c>, its ids kept as typed.
    /// </summary>
    public static string LethalSwingRefusal(string lethalCounter) =>
        $"{lethalCounter.Trim().Replace("Counter: lethal to", "counter is lethal to")}; add ! to swing anyway: ";

    /// <summary>
    /// Under a forecast whose counter kills the attacker if every counter strike lands (issue 539):
    /// <c>  Counter: lethal to Wren (17 against 17 hp)</c>, read by
    /// <see cref="CombatForecast.CounterIsLethal"/>; null otherwise, and for a raise, which draws no counter.
    /// The unit is named as a reader sees it when <paramref name="names"/> is given (issue 615).
    /// When one plain strike of the first round kills the target, the counter comes only if that round misses,
    /// and the line says so with the chance (issue 991, <see cref="CombatForecast.FirstRoundMissChance"/>):
    /// <c>  Counter: lethal to Keziah only if the first round misses (4 in 100) (11 against 10 hp)</c>.
    /// </summary>
    public static string? LethalCounterLine(BattleUnit unit, BattleUnit target, CombatForecast forecast, bool raises, UnitNames? names = null)
    {
        if (raises || !forecast.CounterIsLethal(unit.Hp, target.Hp))
        {
            return null;
        }

        var condition = forecast.FirstRoundMissChance(target.Hp) is { } miss ? $" only if the first round misses ({miss} in 100)" : "";
        return $"  Counter: lethal to {(names ?? UnitNames.None)[unit.Id]}{condition} ({forecast.CounterIfAllLand} against {unit.Hp} hp)";
    }

    /// <summary>
    /// Above a conditioned lethal-counter line (issue 991), the other side of that chance: what the first round
    /// kills with, <c>  Kills if the first strike lands (79)</c> for one strike and
    /// <c>  Kills if any strike of the first round lands (96)</c> for a gauntlet, its figure <c>100</c> less the
    /// miss chance so the two lines sum to 100. Null whenever <see cref="LethalCounterLine"/> carries no condition.
    /// </summary>
    public static string? FirstRoundKillLine(BattleUnit unit, BattleUnit target, CombatForecast forecast, bool raises) =>
        !raises && forecast.CounterIsLethal(unit.Hp, target.Hp) && forecast.FirstRoundMissChance(target.Hp) is { } miss
            ? forecast.Attacker.StrikesPerRound > 1
                ? $"  Kills if any strike of the first round lands ({100 - miss})"
                : $"  Kills if the first strike lands ({100 - miss})"
            : null;

    /// <summary>
    /// Under a forecast of a combat art (issue 68): the art, the weapon as the art makes it,
    /// and the most uses the attack spends against the uses the weapon has, the art's cost
    /// included, since that is paid whether the strike lands or not. An art with a per-map cap
    /// says how many uses of it are left this map, and one that costs the next phase says so
    /// (issue 636), since a rule the player pays for is printed where it is chosen. One that
    /// strikes once says <c>x1, never doubles</c> (issue 739).
    /// </summary>
    private static string ArtLine(GameContent content, BattleUnit unit, CombatForecast forecast, int? slot, string art)
    {
        var (armed, weapon, _) = Resolver.ChooseWeapon(unit, content, slot);
        var ability = content.Ability(art);
        var struck = ((CombatArtEffect)ability.Effect).Apply(weapon!);
        var uses = armed.Unit.Inventory.Items[armed.EquippedSlot(content)].Uses;
        return $"  technique {ability.Name}: {struck.Name} at acc {struck.Hit} power {struck.Mt} crit {struck.Crit} wt {struck.Wt} range {struck.MinRange}-{struck.MaxRange}; spends up to {forecast.AttackerSpendsAtMost} of {uses} uses, {forecast.ArtCost} of them hit or miss"
            + (((CombatArtEffect)ability.Effect).PerMap is { } cap ? $"; {cap - unit.TimesDeclared(art)} of {cap} left this map" : "")
            + (((CombatArtEffect)ability.Effect).Single ? "; x1, never doubles" : "")
            + (((CombatArtEffect)ability.Effect).CostsNextPhase ? "; costs the next phase: no move, no act" : "")
            + (((CombatArtEffect)ability.Effect).Locks ? $"; locks: a hit it survives holds it at Mov 0 while {armed.Unit.Name} stands beside" : "");
    }

    /// <summary>
    /// On a retreat map, the line under a forecast that says where the target would fall
    /// back to if the strike leaves it alive and below the threshold (issue 215), through
    /// <see cref="RetreatRule.Pending"/>: once per distinct HP the attacker's landed strikes
    /// can leave, from one hit up to every strike the attack can make, crits aside. Silent when no outcome
    /// sends it anywhere.
    /// </summary>
    private static IEnumerable<string> PendingRetreatLines(BattleState state, GameContent content, BattleUnit unit, Coord tile, BattleUnit target, CombatForecast forecast, UnitNames names)
    {
        if (!state.Map.RetreatEnabled || !forecast.Attacker.Strikes)
        {
            yield break;
        }

        var hits = Enumerable.Range(1, forecast.Attacker.StrikeCount);
        foreach (var hpAfter in hits.Select(n => target.Hp - n * forecast.Attacker.Damage).Distinct())
        {
            if (RetreatRule.Pending(state, content, unit, tile, target, hpAfter) is { } refuge)
            {
                yield return $"  {names[target.Id]} would fall back to {refuge} at {hpAfter} hp";
            }
        }
    }

    /// <summary>
    /// Prints what the coming enemy phase could do to a player unit standing where it is
    /// or on <paramref name="from"/> (issue 217), through <see cref="Queries.Threats"/>:
    /// one line per enemy with the weapon the planner would swing, its slot counted from
    /// one, the tile it strikes from, and the forecast line the enemy phase would print,
    /// then the total if every strike lands against the unit's HP, one enemy per strike
    /// tile (<see cref="Queries.IfAllLand(IReadOnlyList{ThreatLine})"/>, issue 253). Spends nothing.
    /// A move there that would fire a drake's frost (issue 1127) prints its line first and is priced on the frosted board.
    /// </summary>
    private void PrintThreat(string unitId, Coord? from)
    {
        if (Find(unitId) is not { } unit)
        {
            return;
        }

        if (unit.Side != Ironwake.Core.Side.Player)
        {
            Error($"{unit.Id} is an enemy; threat answers for a player unit");
            return;
        }

        var tile = from ?? unit.At;
        var board = _state;
        var frost = Queries.CanStandOn(_state, _content, unit, tile) ? DrakeFrost.Preview(_state, _content, unit, tile, UnitNames.Of(_state, _content)) : null;
        if (frost is not null)
        {
            board = DrakeFrost.Strike(_state, _content, unit, tile, new List<GameEvent>());
        }

        if (Queries.Threats(board, _content, unit, tile) is not { } lines)
        {
            Error(unit.Canto is not null && unit.Acted ? $"{unit.Id} cannot canto to {tile}" : unit.Moved ? $"{unit.Id} has already moved this phase; threat from {unit.At}" : $"{unit.Id} cannot move to {tile}");
            return;
        }

        if (!Queries.CanStandOn(_state, _content, unit, tile) && Winded.CanDashTo(_state, _content, unit, tile))
        {
            _out.WriteLine($"{tile} is a dash away: priced winded, struck at +{Winded.Hit} Acc");
        }

        if (frost is not null)
        {
            _out.WriteLine(frost + "; priced held");
        }

        _out.WriteLine(ThreatText(board, _content, unit, tile, lines, Queries.SleepingThreats(board, _content, unit, tile)!, Queries.Unseeing(board, _content, unit, tile), Queries.MoveWins(board, _content, unit, tile), Queries.Anvils(board, _content, unit, tile), Queries.StopWakes(board, _content, unit, tile), Queries.Refusals(board, _content, unit, tile)));
        if (LineStrikeThreat(board, _content, unit, tile) is { } line)
        {
            _out.WriteLine(line);
        }

        if (Queries.Stunned(board, _content, unit, tile) is { Count: > 0 } stunned)
        {
            var names = UnitNames.Of(board, _content);
            _out.WriteLine($"Stunned, no line: {string.Join(", ", stunned.Select(u => $"{names[u.Id]} ({u.At})"))} skips the enemy phase");
        }

        if (BracedThreat(board, _content, unit, tile) is { } braced)
        {
            _out.WriteLine(braced);
        }
    }

    /// <summary>
    /// The <c>threat</c> line for a line strike through <paramref name="unit"/> on <paramref name="tile"/> (issue 1384,
    /// <see cref="LineStrike.Through"/>): <c>Hask's line strike from 3,6 east catches Captain, Pell: acc 71% dmg 12
    /// (hp 23), no counter; not in the total</c>. Null when the planner strikes no line through it.
    /// </summary>
    public static string? LineStrikeThreat(BattleState state, GameContent content, BattleUnit unit, Coord tile)
    {
        if (LineStrike.Through(state, content, unit, tile) is not { } through)
        {
            return null;
        }

        var board = tile == unit.At ? state : state.WithUnit(unit with { At = tile });
        var names = UnitNames.Of(board, content);
        var target = board.Find(unit.Id)!;
        var side = LineStrike.Forecast(board.WithUnit(through.Striker), content, through.Striker, target).Attacker;
        return $"{names[through.Striker.Id]}'s line strike from {through.Line.From} {through.Line.Direction} catches {string.Join(", ", through.Line.Caught.Select(u => names[u.Id]))}: "
            + $"{names[unit.Id]} acc {side.DisplayedHit}% dmg {side.Damage} (hp {target.Hp}), no counter; not in the total";
    }

    /// <summary>
    /// The note after a <c>threat</c> line's tile that says where the total counts it (issue
    /// 1237, round 415): <c> (counted from 9,4)</c> when the seating puts it on another tile than
    /// the one printed, <c> (not counted: 8,3 taken)</c> when the seating drops it. Empty for a
    /// line counted where it reads, a raise or a line that deals nothing; a line whose only tile a
    /// side-mate keeps reads <c> (not counted: 5,1 held by Brigand 2)</c> (issue 1256); the covered case,
    /// whose total is not the seated sum, does not ask.
    /// </summary>
    private static string CountedNote(ThreatLine line, Coord? counted, UnitNames names) =>
        line.HeldBy is { } holder ? $" (not counted: {line.From} held by {names[holder.Id]})"
        : line.Raises || line.IfAllLand <= 0 ? ""
        : counted is { } seat ? (seat == line.From ? "" : $" (counted from {seat})")
        : $" (not counted: {line.From} taken)";

    /// <summary>
    /// On a <c>brace: on</c> map (DESIGN.md 13.14), for a unit asked about on its own tile that
    /// would brace if it waited there and something can strike it: the same threat priced braced,
    /// under a line saying so, so the choice between striking and bracing reads as two numbers.
    /// Null otherwise.
    /// </summary>
    public static string? BracedThreat(BattleState state, GameContent content, BattleUnit unit, Coord tile)
    {
        if (tile != unit.At || unit.Acted || unit.Braced || !Brace.BracesOnWait(state, content, unit))
        {
            return null;
        }

        var braced = unit with { Braced = true };
        var after = state.WithUnit(braced);
        if (Queries.Threats(after, content, braced, tile) is not { Count: > 0 } lines)
        {
            return null;
        }

        return $"If {UnitNames.Of(state, content)[unit.Id]} waits here and braces (acc -{Brace.Hit}):\n"
            + ThreatText(after, content, braced, tile, lines, Queries.SleepingThreats(after, content, braced, tile)!, Queries.Unseeing(after, content, braced, tile), anvils: Queries.Anvils(after, content, braced, tile), refusals: Queries.Refusals(after, content, braced, tile));
    }

    /// <summary>
    /// What <c>threat</c> prints for <see cref="Queries.Threats"/>' lines, one row per line,
    /// an enemy an announced event brings marked with where it arrives, then one row per
    /// group <see cref="Queries.SleepingThreats"/> names, members and tiles and no numbers,
    /// and the wake rule under them (issue 248); the protocol's threat query carries it as
    /// its <c>text</c> (issue 25). On a dusk map (DESIGN.md 13.7) an enemy no player unit sees
    /// is left out of the rows, the total and the sleeping groups, and one line says the dark is
    /// unpriced, whether or not anything in it could strike; while anything is in the dark, the
    /// empty case says <c>no enemy in sight can strike it</c>, never a claim about the whole board (issue 399). The dark
    /// line lists the unseen tiles within <see cref="GameContent.LongestReach"/> of the tile, nearest first with
    /// the distance, unpriced and unnamed (<see cref="Dusk.UnseenNear"/>, issue 403); with none that near it
    /// keeps the bare <c>and whatever is in the dark</c> line. An enemy the player sees that
    /// would strike the unit in daylight but does not know where it is, or whose side cannot
    /// see it from where it would strike (<see cref="Queries.Unseeing"/>, issue 302), is priced
    /// at 0 with the reason: <c>archer-1: cannot see you (dark)</c>; one that strikes once a
    /// side-mate acting before it lights the unit (<see cref="ThreatLine.LitBy"/>, issue 987) is a
    /// priced row instead, in the total, marked <c>(once shieldbearer-1 lights you)</c>, or
    /// <c>(once a side-mate in the dark lights you)</c> when the player does not see that side-mate. A move that wins the map
    /// (<see cref="Queries.MoveWins"/>, issue 356) is one line saying so, since no enemy phase follows.
    /// On a <c>windup: on</c> map (DESIGN.md 13.16, issue 444) a blow already raised over the tile
    /// is a row before the strikes and is in the total, since it lands for certain at the enemy
    /// phase start; a maul that would raise this phase says so under its row and adds nothing.
    /// On a <c>pincer: on</c> map (DESIGN.md 13.13, issue 457) each of the planner's anvil plans
    /// against the unit (<see cref="Queries.Anvils"/>) is one row after the total, phrased as could
    /// and unpriced: <c>anvil: shieldbearer-1 could step to 17,0 so soldier-1 strikes you pinned
    /// from 19,0</c>, or <c>could hold 17,0</c> when the tile is the anvil's own; a plan with an
    /// enemy the player does not see is left out.
    /// A stop that wakes a sleeping group (<see cref="Queries.StopWakes"/>, issue 458) is one row
    /// before the sleeping groups, naming each group and why, unpriced:
    /// <c>stopping here wakes: ford (proximity), weir (called by ford)</c>.
    /// On a <c>wind:</c> map the turn before the wind turns (issue 957), a group the unit on the tile
    /// would wake once it turns, and does not wake now, is one row after it (<see cref="Wind.StopWakesOnTurn"/>):
    /// <c>quiet now; once the wind turns east on turn 5, stopping here wakes: the field group</c>.
    /// On a <c>cover: on</c> map (DESIGN.md 13.19, round 142) a strike a cover would swap is
    /// priced against the coverer on the tile, <c>covered by teodor, strikes teodor on 7,5</c>,
    /// and the total says the first strike swaps them and where the unit lands, the strikes after
    /// the swap unpriced.
    /// A boss under the veto that could strike the unit but refuses every tile it would strike
    /// from (<see cref="Queries.Refusals"/>, issue 565) is one row after the strikes, naming the
    /// nearest refused tile and where its plan ends, unpriced:
    /// <c>weir_foreman-1 could reach 12,4 but refuses it: too exposed there; holds 14,6</c>, or
    /// <c>ends on 13,6</c> when the plan moves it; a boss the player does not see is left out.
    /// On a map with fronts (issue 692) one row names the front the tile defends (<see cref="Hunt.DefendedFrom"/>),
    /// and on a map with a hunter one more names the front it hunts (<see cref="Hunt.Line"/>), read
    /// with the unit moved to the tile.
    /// A line the total seats on another tile than the one it prints says so after the tile,
    /// <c>from 8,3 (counted from 9,4)</c>, and a priced line the seating drops says
    /// <c>(not counted: 8,3 taken)</c> (<see cref="Queries.CountedFrom"/>, issue 1237); the
    /// printed tile stays the enemy's own, and the covered case prints no seat.
    /// </summary>
    public static string ThreatText(BattleState state, GameContent content, BattleUnit unit, Coord tile, IReadOnlyList<ThreatLine> lines, IReadOnlyList<SleepingThreat> asleep, IReadOnlyList<BattleUnit>? unseeing = null, bool wins = false, IReadOnlyList<AnvilLine>? anvils = null, IReadOnlyList<GroupWoke>? wakes = null, IReadOnlyList<RefusalLine>? refusals = null)
    {
        var where = $"{tile} ({state.Map.TerrainAt(tile, content).Name})";
        var names = UnitNames.Of(state, content);
        var name = names[unit.Id];
        if (wins)
        {
            return $"Threat on {name} at {where}: this move wins the map";
        }

        var rows = new List<string>();
        lines = lines.Where(line => line.Arrives is not null || Dusk.Seen(state, line.Enemy)).ToList();
        asleep = asleep.Select(g => g with { Members = ValueList<BattleUnit>.From(g.Members.Where(m => Dusk.Seen(state, m))) }).Where(g => g.Members.Count > 0).ToList();
        var dark = Dusk.Sight(state) is not null && state.UnitsOf(Side.Enemy).Any(e => !Dusk.Seen(state, e));
        var blow = Queries.RaisedBlowOn(state, content, unit, tile);
        if (lines.Count == 0 && blow is null)
        {
            rows.Add($"threat on {name} at {where}: no enemy {(dark ? "in sight " : "")}can strike {names.Refer(unit.Id).Object} next phase");
        }
        else
        {
            rows.Add($"threat on {name} at {where}:");
            if (blow is not null)
            {
                rows.Add($"  {names[blow.Wielder.Id]}'s raised blow lands here at the enemy phase start: {blow.Damage}, sure, unless a hit from within its reach breaks it");
            }

            var counted = lines.Any(l => l.CoveredBy is not null) ? null : Queries.CountedFrom(lines);
            foreach (var (line, index) in lines.Select((l, i) => (l, i)))
            {
                var arrives = line.Arrives is { } at ? $" (arrives this enemy phase at {at})" : "";
                if (line.LitBy is { } lighter)
                {
                    arrives += Dusk.Seen(state, state.Find(lighter.Id) ?? lighter) ? $" (once {names[lighter.Id]} lights you)" : " (once a side-mate in the dark lights you)";
                }

                if (line.FreedBy is { } stepper)
                {
                    arrives += $" (once {names[stepper.Id]} steps off {line.From})";
                }

                var caught = line.Forecast.CaughtBy is { } rodId ? state.Find(rodId) : null;
                var covered = line.CoveredBy is { } by ? $"covered by {names[by.Id]}, strikes {names[by.Id]} on {tile}, " : "";
                if (caught is not null && line.Weapon.School is { } school)
                {
                    covered = $"{LightningRod.RodName(content, caught, school)} catches it, strikes {names[caught.Id]} on {caught.At} (not in the total), ";
                }

                var answers = caught ?? line.CoveredBy ?? unit;
                var answerDistance = caught is null ? line.From.DistanceTo(tile) : line.From.DistanceTo(caught.At);
                rows.Add($"  {names[line.Enemy.Id]}{arrives} from {line.From}{(counted is null ? "" : CountedNote(line, counted[index], names))} with {line.Weapon.Name}{Keepsake.Suffix(line.Enemy.Unit.Inventory.Items[line.Slot], content)} (slot {line.Slot + 1}): {covered}{(line.Raises ? RaiseText(line.Forecast.Attacker) + "; counter: none" : StrikeText(line.Forecast.Attacker) + "; counter" + (line.Forecast.Defender.Strikes ? CounterWith(content, answers, answerDistance) + ": " + StrikeText(line.Forecast.Defender) + CounterUses(content, answers, line.Forecast.Defender) : ": none"))}");
                if (!line.Raises && line.Arrives is null && line.Forecast.CaughtBy is null
                    && SeenRolls.Line(state, state.Turn, Side.Enemy, line.Enemy.Id, names[line.Enemy.Id], line.Forecast.Attacker.HitChance, answers.Id, names[answers.Id], line.Forecast.Defender.Strikes ? line.Forecast.Defender.HitChance : null) is { } seen)
                {
                    rows.Add($"    {seen}");
                }

                var falls = !line.Raises && line.HeldBy is null && line.Forecast.CounterIsLethal(line.Enemy.Hp, answers.Hp) ? Returned.Falls(state, line.Enemy, names) : null;
                if (!line.Raises && line.HeldBy is null && line.Forecast.Defender.Strikes && Kinsbane.CounterFeedLine(answers, line.Enemy, line.Forecast, content, names[answers.Id], falls) is { } feed)
                {
                    rows.Add($"    {feed}");
                }
                else if (falls is not null)
                {
                    rows.Add($"    {Kinsbane.CounterKillLabel(line.Forecast, line.Enemy)} {falls}");
                }

                if (line.Raises)
                {
                    rows.Add($"    windup: no strike; {names[line.Enemy.Id]} raises over {tile}, lands next enemy phase for {line.Forecast.Attacker.Damage}, sure, unless a hit from within its reach breaks it (not in the total)");
                }

                if (StrikeWakesRow(state, line) is { } heard)
                {
                    rows.Add(heard);
                }

                if (line.Casts is { } cast)
                {
                    rows.Add($"    may {(cast == CastKind.Raise ? "raise the dead" : "lay Rampart")} instead: it casts in place of any strike that does not kill (still in the total)");
                }
            }

            if (lines.FirstOrDefault(l => l.CoveredBy is not null)?.CoveredBy is { } coverer)
            {
                var landing = state.Find(coverer.Id)?.At ?? coverer.At;
                var worst = lines.Where(l => l.CoveredBy is not null).Max(l => l.IfAllLand);
                rows.Add($"  if all land: the first strike swaps them; {names[coverer.Id]} takes up to {worst} against {coverer.Hp} hp on {tile}, {name} lands on {landing}, and any strike after it is unpriced");
            }
            else
            {
                rows.Add($"  if all land: {Queries.IfAllLand(lines, blow)} against {unit.Hp} hp");
                var freed = Queries.FreedStrikes(lines, unit);
                if (freed.Count > 0)
                {
                    var clauses = freed.Select((f, i) => $"{(i == 0 ? $"{name}'s counter kills" : "and if it kills")} {names[f.Freer.Id]} {FreedChance(f)}, {names[f.Follower.Id]} takes {f.Tile}");
                    rows.Add($"  if {string.Join(", ", clauses)}: {Queries.IfAllLand(lines, blow) + freed.Sum(f => f.Damage)} against {unit.Hp} hp");
                }

                if (LastUseRow(state, unit, lines, content) is { } lastUse)
                {
                    rows.Add(lastUse);
                }
            }

            if (state.Map.OneAnswerEnabled && lines.Count(l => !l.Raises && l.HeldBy is null && l.Forecast.Defender.Strikes) > 1)
            {
                rows.Add($"  one answer: {name} counters only the first of these to strike; the rest go unanswered");
            }
        }

        foreach (var refusal in (refusals ?? Array.Empty<RefusalLine>()).Where(r => Dusk.Seen(state, r.Boss)))
        {
            var ends = refusal.Ends == refusal.Boss.At ? $"holds {refusal.Ends}" : $"ends on {refusal.Ends}";
            rows.Add($"  {names[refusal.Boss.Id]} could reach {refusal.Refused} but refuses it: too exposed there; {ends}");
        }

        foreach (var anvil in (anvils ?? Array.Empty<AnvilLine>()).Where(a => Dusk.Seen(state, a.Anvil) && Dusk.Seen(state, a.Follower)))
        {
            var step = anvil.Tile == anvil.Anvil.At ? $"hold {anvil.Tile}" : $"step to {anvil.Tile}";
            rows.Add($"  anvil: {names[anvil.Anvil.Id]} could {step} so {names[anvil.Follower.Id]} strikes you pinned from {anvil.From}");
        }

        foreach (var blind in (unseeing ?? Array.Empty<BattleUnit>()).Where(e => Dusk.Seen(state, e)))
        {
            rows.Add($"  {names[blind.Id]}: cannot see you (dark)");
        }

        if (dark)
        {
            var near = Dusk.UnseenNear(state, tile, content.LongestReach);
            rows.Add(near.Count == 0
                ? $"  and whatever is in the dark ({Dusk.Unseen}), unpriced"
                : $"  in the dark, unpriced: {string.Join(", ", near.Select(n => $"{Dusk.Unseen} at {n.At} ({n.Distance})"))}");
        }

        if (state.Map.Fronts.Count > 0)
        {
            var there = tile == unit.At ? state : state.WithUnit(unit with { At = tile });
            rows.Add(Hunt.DefendedFrom(state.Map, tile) is { } front
                ? $"  front: defends the {front.Words}{(Fronts.HasFallen(state, front) ? " (fallen)" : "")}"
                : $"  front: defends none (more than {Hunt.DefendRadius} tiles from every front)");
            if (Hunt.Line(there, names) is { } hunt)
            {
                rows.Add($"  {hunt}");
            }
        }

        if (Freed.Line(state, names) is { } bondRow)
        {
            rows.Add($"  {bondRow}");
        }

        if (Returned.Line(state, content, names) is { } returnRow)
        {
            rows.Add($"  {returnRow}");
        }

        if (wakes is { Count: > 0 })
        {
            rows.Add($"  stopping here wakes: {string.Join(", ", wakes.Select(w => $"{UnitNames.Group(w.Group)} ({(w.CalledBy is { } by ? "called by " + UnitNames.Group(by) : WakeCauseText(w))})"))}");
        }

        if (Wind.StopWakesOnTurn(state, content, unit, tile) is { Groups.Count: > 0 } windTurn)
        {
            rows.Add($"  quiet now; once the wind turns {Wind.Word(windTurn.Shift.To)} on turn {windTurn.Shift.Turn}, stopping here wakes: {string.Join(", ", windTurn.Groups.Select(UnitNames.Group))}");
        }

        foreach (var group in asleep)
        {
            rows.Add($"  {UnitNames.Group(group.Group)} is asleep, could strike here if woken: {string.Join(", ", group.Members.Select(m => $"{names[m.Id]} at {m.At}"))}");
        }

        if (MapRenderer.DeathWakeLegend(state.Map, g => asleep.Any(a => a.Group == g)) is { } deaf)
        {
            rows.Add($"  {deaf}");
        }

        if (asleep.Any(a => !state.Map.IsDeaf(a.Group)))
        {
            rows.Add($"  {MapRenderer.WakeLegend(content)}");
            if (Wind.Line(state.Map, content, state.Turn) is { } wind)
            {
                rows.Add($"  {wind}");
            }

            if (state.Map.SeenFar is { } far && far.For(unit) > 0)
            {
                rows.Add($"  {far.Line(unit.Unit.Name, content)}");
            }
        }

        return UnitNames.Sentence(string.Join("\n", rows));
    }

    /// <summary>Why a group wakes, as the wake event prints it: <c>called by ford</c> for a linked call, else the cause in lower case.</summary>
    public static string WakeCauseText(GroupWoke woke) => woke.CalledBy is { } by ? (woke.Cause == WakeCause.Death ? $"a death in {by}" : $"called by {by}") : woke.Cause.ToString().ToLowerInvariant();

    /// <summary>The holder whose Lightning Rod catches <paramref name="forecast"/>'s attack (issue 1280) and the school it catches, or null when none does.</summary>
    private static (BattleUnit Holder, MagicSchool School)? RodHolder(BattleState state, GameContent content, BattleUnit unit, CombatForecast forecast, int? slot) =>
        forecast.CaughtBy is { } id && state.Find(id) is { } holder && Resolver.ChooseWeapon(unit, content, slot).Weapon?.School is { } school ? (holder, school) : null;

    /// <summary>
    /// The one forecast line, printed before an attack from either side and by the
    /// <c>forecast</c> command: the attacker's strike, then the counter or <c>none</c>.
    /// <paramref name="where"/> is the tile suffix of a forecast asked from a tile the
    /// unit has not moved to (issue 151), empty for a forecast on the standing board; a Lightning Rod's note follows it (issue 1280).
    /// </summary>
    public static string ForecastLine(BattleUnit unit, BattleUnit target, CombatForecast forecast, string where = "", string with = "", string counterWith = "", bool raises = false, UnitNames? names = null, string riders = "", string counterRiders = "", string counterUses = "")
    {
        names ??= UnitNames.None;
        if (raises)
        {
            return $"Forecast {names[unit.Id]} -> {names[target.Id]}{where}{with}: {RaiseText(forecast.Attacker)}; counter: none";
        }

        return $"Forecast {names[unit.Id]} -> {names[target.Id]}{where}{with}: {StrikeText(forecast.Attacker)}{riders}; counter{(forecast.Defender.Strikes ? counterWith + ": " + StrikeText(forecast.Defender) + counterRiders + counterUses : ": none" + (forecast.CounterAnswered ? " (answered this phase)" : ""))}";
    }

    /// <summary>
    /// What each side of a forecast leaves on a hit, printed after that side's strike columns: the
    /// attacker with <paramref name="slot"/> (else its equipped weapon), the target with the weapon it
    /// holds in front. <c> chills</c> for frozen iron (issue 702, <see cref="Frost"/>), then
    /// <c> burn 4 (2 stacks, 2 phases)</c>, the burn a hit leaves, for a school's burn, or <c> cashes burn 8</c>
    /// for an ember (issues 1243, 1279, <see cref="Burning"/>). <c> chills</c>
    /// also for an ice tome's chill rider, and <c> stuns</c> (or <c> stun: bosses spared</c>, <c> stun spent</c>)
    /// for a stun rider in a gated caster's hands (issue 1244, <see cref="Stun"/>), and <c> drains up to 12</c> for a drain rider when <paramref name="forecast"/> is given (issue 1283, <see cref="Drain"/>). A learned school's
    /// rider prints its gate (issue 1246, <see cref="LearnedGate"/>): <c> burn 2 (1 stack, 2 phases): Mag 6
    /// over Res 4</c> when it fires, <c> no burn: Mag 4, Res 4</c> when it does not.
    /// </summary>
    public static (string Riders, string CounterRiders) Riders(GameContent content, BattleUnit unit, BattleUnit target, int? slot, CombatForecast? forecast = null, BattleState? state = null) =>
        (RiderText(content, unit, Resolver.ChooseWeapon(unit, content, slot).Weapon, target, forecast?.Attacker, state), RiderText(content, target, target.EquippedWeapon(content), unit, forecast?.Defender, state));

    private static string RiderText(GameContent content, BattleUnit striker, Weapon? weapon, BattleUnit struck, SideForecast? side, BattleState? state)
    {
        if (LearnedGate.Read(content, striker, weapon, struck) is { Passes: false } held)
        {
            return LearnedGate.Refused(Frost.Chills(content, weapon) ? "chill" : Burning.Embers(content, weapon) ? "ember" : Drain.Drains(content, weapon) ? "drain" : Curse.Curses(content, weapon) ? "curse" : Freeze.Freezes(content, weapon) ? "freeze" : "burn", held);
        }

        var gate = LearnedGate.Suffix(LearnedGate.Read(content, striker, weapon, struck));
        return (Frost.Chills(content, weapon) ? " chills" : "") + Burning.ForecastText(content, weapon, struck) + Curse.ForecastText(content, weapon) + (state is null ? "" : Freeze.ForecastText(state, content, weapon, struck)) + (side is null ? "" : Drain.ForecastText(content, weapon, side, struck)) + gate + Stun.ForecastText(content, striker, weapon, struck) + Sunder.ForecastText(content, weapon, struck) + (side is null ? "" : Mark.ForecastText(side) + Armor.ForecastText(side) + LightningRod.ForecastText(side));
    }

    /// <summary>
    /// The weapon suffixes of a forecast line (DESIGN.md 13.11, issue 313): <c> with Toll Axe</c>
    /// after the target for the attacker's <paramref name="slot"/> (else its equipped weapon), and
    /// after <c>counter</c> for the weapon the defender holds in front, which is the one it last
    /// swung. A side is named only when it could strike from that range with more than one
    /// weapon (<see cref="BattleUnit.WeaponChoicesAt"/>), the attacker counted at
    /// <paramref name="from"/>; an attacker's keepsake is named whatever the count
    /// (<see cref="KeepsakeWith"/>).
    /// </summary>
    public static (string With, string CounterWith) Arms(GameContent content, BattleUnit unit, BattleUnit target, int? slot, Coord from)
    {
        var distance = from.DistanceTo(target.At);
        var with = unit.WeaponChoicesAt(content, distance) > 1
            ? WeaponWith(unit, content, slot ?? unit.EquippedSlot(content))
            : KeepsakeWith(unit, content, slot);
        return (with, CounterWith(content, target, distance));
    }

    /// <summary>
    /// The counter's suffix, <c> with Steel Axe</c>, for a defender that could strike from
    /// <paramref name="distance"/> with more than one weapon; empty otherwise (issue 313).
    /// </summary>
    public static string CounterWith(GameContent content, BattleUnit defender, int distance) =>
        defender.WeaponChoicesAt(content, distance) > 1 ? WeaponWith(defender, content, defender.EquippedSlot(content)) : "";

    /// <summary>
    /// The uses a counter would spend from, after its strike columns (issue 1200): <c> (Radiance 2 of 5 left)</c>
    /// when <paramref name="defender"/> counters with a spell, whose uses are a battle's (<see cref="BattleState"/>
    /// refills them at the start); empty for a physical weapon or no counter. Information only: no ask follows from it.
    /// </summary>
    public static string CounterUses(GameContent content, BattleUnit defender, SideForecast counter) =>
        counter.Strikes && SpellInFront(content, defender) is { } spell ? $" ({spell.Weapon.Name} {spell.Uses} of {spell.Weapon.Durability} left)" : "";

    /// <summary>
    /// The row <c>threat</c> adds when the counters it lists could spend <paramref name="unit"/>'s last use of the
    /// spell it holds in front (issue 1200): each answered strike spends one, a double two, and on a
    /// <c>one_answer: on</c> map only the first line is answered. <c>threat</c> asks it only when no line is
    /// covered, since a coverer's counters are not the unit's.
    /// </summary>
    public static string? LastUseRow(BattleState state, BattleUnit unit, IReadOnlyList<ThreatLine> lines, GameContent content)
    {
        if (SpellInFront(content, unit) is not { } spell)
        {
            return null;
        }

        var counters = lines.Where(l => !l.Raises && l.HeldBy is null && l.Forecast.Defender.Strikes).Select(l => l.Forecast.Defender.StrikeCount).ToList();
        var spends = state.Map.OneAnswerEnabled ? counters.Take(1).Sum() : counters.Sum();
        return counters.Count > 0 && spends >= spell.Uses ? $"  counters could spend {spell.Weapon.Name}'s last use" : null;
    }

    private static (Weapon Weapon, int Uses)? SpellInFront(GameContent content, BattleUnit unit)
    {
        var slot = unit.EquippedSlot(content);
        if (slot < 0 || slot >= unit.Unit.Inventory.Count)
        {
            return null;
        }

        var stack = unit.Unit.Inventory.Items[slot];
        return content.Weapons.TryGetValue(stack.ItemId, out var weapon) && weapon.IsMagic ? (weapon, stack.Uses) : null;
    }

    private static string WeaponWith(BattleUnit unit, GameContent content, int slot)
    {
        if (slot < 0 || slot >= unit.Unit.Inventory.Count)
        {
            return "";
        }

        var stack = unit.Unit.Inventory.Items[slot];
        return " with " + Heirloom.Name(stack, content) + Keepsake.Suffix(stack, content);
    }

    /// <summary>
    /// What a forecast line adds when the weapon struck with is a keepsake (DESIGN.md 13.8,
    /// issue 295): <c> with Iron Lance (Teodor's)</c>, the slot named or else the equipped
    /// one; empty for an ordinary weapon, so the line reads as it always has.
    /// </summary>
    public static string KeepsakeWith(BattleUnit unit, GameContent content, int? slot)
    {
        var at = slot ?? unit.EquippedSlot(content);
        if (at < 0 || at >= unit.Unit.Inventory.Count || unit.Unit.Inventory.Items[at] is not { Keepsake: { } fallen } stack)
        {
            return "";
        }

        return " with " + Keepsake.Name(stack.ItemId, fallen, content);
    }

    /// <summary>
    /// One side of a forecast as the console prints it: displayed Acc first, then damage, doubles and crit (issue 701);
    /// <c>grounds N%</c> in place of the crit when the side's crit grounds a flier instead of tripling (issue 723);
    /// a single-strike class says <c>x1, never doubles</c>, a dive <c>; Stoop +2 on the first strike</c> (issue 1127), and a drake's bite <c>; drake bites 5, no roll</c> (issue 872).
    /// </summary>
    private static string StrikeText(SideForecast side) =>
        $"acc {side.DisplayedHit}% dmg {side.Damage}{(side.StrikeCount > 1 ? $" x{side.StrikeCount}" : side.NeverDoubles ? " x1, never doubles," : "")} {(side.CritGrounds ? "grounds" : "crit")} {side.CritChance}%{(side.Stoop > 0 ? $"; Stoop +{side.Stoop} on the first strike" : "")}{(side.Bite > 0 ? $"; drake bites {side.Bite} if a strike hits and both stand, no roll" : "")}";

    /// <summary>
    /// The strike columns of an attack that raises a blow (DESIGN.md 13.16, issue 447): the
    /// landing's damage and no percentage, since the raise has no roll and the landing is sure.
    /// </summary>
    private static string RaiseText(SideForecast side) => $"acc -- dmg {side.Damage} crit --";

    /// <summary>True when <paramref name="unit"/>'s attack with <paramref name="slot"/> raises a blow instead of fighting.</summary>
    internal static bool RaisesWith(BattleState state, GameContent content, BattleUnit unit, int? slot) =>
        Windup.Raises(state, Resolver.ChooseWeapon(unit, content, slot).Weapon);

    /// <summary>
    /// Reads a one-based slot typed by the player into the core's zero-based one. Null text
    /// is no slot. A slot outside 1..count is refused here, naming the range the player
    /// sees, so the core's zero-based message never reaches the screen. Text that is not a
    /// number names a carried item by id or display name, ignoring case, a space standing for
    /// the id's <c>_</c> (issue 477); a name that matches no slot, or more than one, is refused
    /// with the unit's slots listed by number and name. A number whose slot holds another item
    /// than the unit's last listing showed there is refused naming both (issue 1114); a name
    /// is never checked against the listing.
    /// </summary>
    private bool TrySlot(string unitId, string? text, out int? slot)
    {
        slot = null;
        if (text is null)
        {
            return true;
        }

        if (Find(unitId) is not { } unit)
        {
            return false;
        }

        var count = unit.Unit.Inventory.Count;
        if (!int.TryParse(text, out var typed))
        {
            var named = SlotsNamed(unit.Unit.Inventory, _content, text);
            if (named.Count == 1)
            {
                slot = named[0];
                return true;
            }

            var slots = count == 0
                ? "carries nothing"
                : "slots: " + string.Join(", ", unit.Unit.Inventory.Items.Select((item, at) => $"{at + 1} {Heirloom.Name(item, _content)}"));
            Listed(unit);
            Error(named.Count == 0
                ? $"{unit.Id} carries no '{text}'; {slots}"
                : $"{unit.Id} carries '{text}' in slots {string.Join(" and ", named.Select(at => at + 1))}; give the slot number; {slots}");
            return false;
        }

        if (typed < 1 || typed > count)
        {
            Error(count == 0 ? $"{unit.Id} carries nothing" : $"{unit.Id} has nothing in slot {typed}; slots run 1-{count}");
            return false;
        }

        if (StaleSlot(_content, unit, UnitNames.Of(_state, _content)[unit.Id], _listed.GetValueOrDefault(unit.Id), typed - 1) is { } stale)
        {
            Error(stale, $"show {unit.Id}");
            return false;
        }

        slot = typed - 1;
        return true;
    }

    /// <summary>
    /// The zero-based slots whose item is named by <paramref name="text"/> (issue 477): its id
    /// or its display name, ignoring case, a space standing for the id's <c>_</c>.
    /// </summary>
    public static IReadOnlyList<int> SlotsNamed(Inventory inventory, GameContent content, string text) =>
        inventory.Items
            .Select((item, at) => (item, at))
            .Where(entry => string.Equals(entry.item.ItemId, text.Replace(' ', '_'), StringComparison.OrdinalIgnoreCase)
                || string.Equals(content.ItemName(entry.item.ItemId), text, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.at)
            .ToList();

    /// <summary>
    /// The <c>show</c> weapon line: the equipped weapon's numbers, or <c>unarmed</c> for a
    /// unit with nothing usable, with <c>(spell spent)</c> when a spell its class could
    /// cast sits at zero uses (issue 101; DECISIONS/0018 item 3).
    /// </summary>
    public static string WeaponLine(BattleUnit unit, GameContent content)
    {
        var weapon = unit.EquippedWeapon(content);
        if (weapon is not null)
        {
            return $"{weapon.Name} (acc {weapon.Hit} power {weapon.Mt} crit {weapon.Crit} wt {weapon.Wt} range {weapon.MinRange}-{weapon.MaxRange}){(unit.WeaponBroken(content) ? " broken: -10 acc -5 power" : "")}";
        }

        var unitClass = content.Class(unit.Unit.ClassId);
        var spent = unit.Unit.Inventory.Items.Any(item =>
            item.Uses == 0 && content.Weapons.TryGetValue(item.ItemId, out var w) && w.IsMagic && !w.Heals && unitClass.CanUse(w.Type));
        return spent ? "unarmed (spell spent)" : "unarmed";
    }

    private void Show(BattleUnit unit)
    {
        foreach (var line in ShowLines(_state, _content, unit, typed: true))
        {
            _out.WriteLine(line);
        }
    }

    /// <summary>
    /// A wound as a card prints it (issue 664): <c>Wounded (2): Str -2, Dex -2 for 2 more main maps</c>,
    /// the stats it took in stat order; the numbers above it are already the wounded ones.
    /// </summary>
    public static string WoundLine(Wound wound)
    {
        var taken = Stats.All.Where(s => wound.Penalty.Get(s) > 0).Select(s => $"{s} -{wound.Penalty.Get(s)}");
        var maps = wound.MapsLeft == 1 ? "the next main map" : $"{wound.MapsLeft} more main maps";
        return $"{wound.Label}: {string.Join(", ", taken)} for {maps}";
    }

    /// <summary>
    /// The tag a signature art carries on the unit's card (issue 969): <c>with &lt;item&gt;</c> for an
    /// art declared only with its own item, and <c>with &lt;item&gt;, not carried</c> while that item is
    /// not in the unit's inventory, so the card says what <see cref="Resolver.ChooseArt"/> will refuse.
    /// Null for an art any weapon of its type declares.
    /// </summary>
    public static string? ArtItemTag(string? item, BattleUnit unit, GameContent content) =>
        item is null ? null
        : unit.Unit.Inventory.Items.Any(stack => stack.ItemId == item) ? $"with {content.ItemName(item)}"
        : $"with {content.ItemName(item)}, not carried";

    /// <summary>
    /// The lines <c>show &lt;unit&gt;</c> prints: who and where, the unit's description if it has one (issue 806), stats, weapon, items, ranks,
    /// arts, abilities, mastery, Canto, targets and rivalry. The Godot client's unit panel
    /// shows the same lines (issue 349). Units and arts read by the names a reader sees, every
    /// line in sentence case (issue 615); with <paramref name="typed"/>, as the console prints
    /// them, each name a command types differently is followed by that id in parentheses:
    /// <c>Alder Fenn (captain)</c>, <c>Brigand 1 (brigand-1)</c>.
    /// </summary>
    public static IReadOnlyList<string> ShowLines(BattleState state, GameContent content, BattleUnit unit, bool typed = false)
    {
        var names = UnitNames.Of(state, content);
        string Named(string name, string id) =>
            typed && !string.Equals(name, id, StringComparison.OrdinalIgnoreCase) ? $"{name} ({id})" : name;
        var lines = new List<string>();
        var stats = content.StatsOf(unit.Unit);
        lines.Add($"{Named(names[unit.Id], unit.Id)}, {content.Class(unit.Unit.ClassId).Name} L{unit.Unit.Level}, at {unit.At} on {state.Map.TerrainAt(unit.At, content).Label(stats.Hp)}");
        if (unit.Unit.Description is { } description)
        {
            lines.Add("  " + description);
        }

        if (unit.Kin is { } kin)
        {
            lines.Add(unit.Swallowed
                ? $"  Swallowed: the Kin heals {kin.Heal} at his side's phase start; Frozen Iron lands for {state.FrozenIron} on every unit at the next phase start"
                : $"  At 0 HP he swallows the shard: stage 2 on a fresh bar (hp {kin.Hp}, Def +{kin.Def}, Res +{kin.Res}), the Kin heals {kin.Heal} a phase, and Frozen Iron falls on every unit");
        }

        var unitClass = content.Class(unit.Unit.ClassId);
        lines.Add($"  HP {unit.Hp}/{stats.Hp}  Str {stats.Str} Mag {stats.Mag} Dex {stats.Dex} Spd {stats.Spd} Lck {stats.Lck} Def {stats.Def} Res {stats.Res} Cha {stats.Cha}  Mov {unitClass.Mov} ({unitClass.Movement.ToString().ToLowerInvariant()})");
        if (unit.Unit.Wound is { } wound)
        {
            lines.Add("  " + WoundLine(wound));
        }

        lines.Add($"  Weapon: {WeaponLine(unit, content)}");
        if (unit.EquippedWeapon(content) is { FrozenIron: true })
        {
            lines.Add($"  Frozen iron: a hit chills, Mov -{Frost.MovLost} until the end of the target's side's next phase");
        }

        var slots = unit.Unit.Inventory.Items.Select((item, slot) => $"{slot + 1}: {Heirloom.Name(item, content)}{Keepsake.Suffix(item, content)} x{item.Uses}");
        lines.Add($"  Items: {(unit.Unit.Inventory.Count == 0 ? "none" : string.Join(", ", slots))}");
        var ranks = content.Class(unit.Unit.ClassId).Weapons
            .Select(type => $"{type.Label()} {unit.Unit.Skill.Rank(type)} ({unit.Unit.Skill.Points(type)})");
        lines.Add($"  Ranks: {string.Join(", ", ranks)}");
        var arts = content.ArtsOf(unit.Unit).Select(a =>
            $"{Named(a.Ability.Name, a.Ability.Id)} ({a.Art.Weapon.Label()} {a.Art.Rank}, cost {a.Art.Cost}{(ArtItemTag(a.Art.Item, unit, content) is { } tag ? ", " + tag : "")}): {a.Ability.Text}").ToList();
        if (arts.Count > 0)
        {
            lines.Add($"  Techniques: {string.Join(", ", arts)}");
        }

        var held = content.AbilitiesOf(unit.Unit).Where(a => a.Effect is not CombatArtEffect && !(a.Effect is DrakeFrostEffect && unit.Unit.Drake is null) && !(a.Effect is StoopEffect && unit.Unit.Drake is not null))
            .Select(a => $"{a.Name} ({a.Text.TrimEnd('.')}{(ArtItemTag((a.Effect as HealArtEffect)?.Item, unit, content) is { } tag ? "; " + tag : "")})").ToList();
        if (held.Count > 0)
        {
            lines.Add($"  Abilities: {string.Join(", ", held)}");
        }

        if (MasteryLine(unit, content) is { } mastery)
        {
            lines.Add(UnitNames.Sentence(mastery));
        }

        if (Signatures.Of(state, content, unit) is { } signature)
        {
            lines.Add($"  Signature: {Signatures.Describe(signature)}");
        }

        if (Kinsbane.Card(unit, content) is { } hunger)
        {
            lines.Add($"  {hunger}");
        }

        if (Heirloom.Card(unit, content) is { } heirloom)
        {
            lines.Add($"  {heirloom}");
        }

        if (Drake.Card(unit.Unit, content) is { } drake)
        {
            lines.Add($"  {drake}");
        }

        if (state.CantoReachOf(unit, content) is not null)
        {
            lines.Add($"  Move again: {unit.Canto} movement left this phase");
        }

        if (unit.IsCaptain && state.OrdersOpen)
        {
            var spent = state.OrderCalled is { } called ? $"spent ({Orders.Word(called)})" : "unspent";
            lines.Add($"  Commander's Word: order press, rally or fall back, once a map, as the captain's action; reaches allies within {Orders.Radius(unit, content)} (2 + Cha / 4); {spent}");
        }

        if (unit.Pressed)
        {
            lines.Add("  Pressed: +1 Mov this phase");
        }

        if (unit.LockedBy is { } locker && Lock.CardLine(state, unit, UnitNames.Of(state, content)[locker]) is { } locked)
        {
            lines.Add("  " + locked);
        }
        else if (Frost.CardLine(state, unit) is { } chilled)
        {
            lines.Add("  " + chilled);
        }

        if (Burning.CardLine(content, unit) is { } burning)
        {
            lines.Add("  " + burning);
        }

        if (Curse.CardLine(content, unit, UnitNames.Of(state, content)) is { } cursed)
        {
            lines.Add("  " + cursed);
        }

        if (Freeze.CardLine(unit) is { } frozen)
        {
            lines.Add("  " + frozen);
        }

        if (Mark.CardLine(unit) is { } marked)
        {
            lines.Add("  " + marked);
        }

        if (LightningRod.CardLine(unit) is { } charged)
        {
            lines.Add("  " + charged);
        }

        if (Armor.CardLine(content, unit) is { } armored)
        {
            lines.Add("  " + armored);
        }

        if (Stun.CardLine(unit) is { } stunned)
        {
            lines.Add("  " + stunned);
        }

        if (Hollow.CardLine(unit, UnitNames.Of(state, content)) is { } hollow)
        {
            lines.Add("  " + hollow);
        }

        if (DrakeFrost.HeldLine(state, unit) is { } frosted)
        {
            lines.Add("  " + frosted);
        }

        if (DrakeFrost.CardLine(state, content, unit) is { } frost)
        {
            lines.Add("  " + frost);
        }

        if (Opening.CardLine(state, unit, UnitNames.Of(state, content)) is { } open)
        {
            lines.Add("  " + open);
        }

        if (Grounding.CardLine(state, unit) is { } grounded)
        {
            lines.Add("  " + grounded + (Grounding.Stranded(state, unit, content) ? ", stranded: no move, may still act" : ", moves on foot"));
        }

        if (unit.FallingBack)
        {
            lines.Add($"  Fall back: up to {Orders.FallBackMov} movement owed this phase, if it wakes no one");
        }

        var targets = string.Join(", ", Queries.Targets(state, content, unit).Select(t => Named(names[t.Id], t.Id)));
        lines.Add($"  Targets from here: {(targets.Length == 0 ? "none" : targets)}");
        if (state.Map.RivalryArm is not null && Rivalry.IsRecruit(unit))
        {
            var rivals = state.UnitsOf(Side.Player)
                .Where(other => Rivalry.AreRivals(state, content, unit, other))
                .Select(other => $"{names[other.Id]} {Rivalry.PointsOf(state, unit.Id, other.Id)}/{content.Rivalry.OverwriteAt}");
            var list = string.Join(", ", rivals);
            lines.Add(UnitNames.Sentence($"  {unit.Unit.Region}; rapport {Rivalry.RateOf(unit, content)} per phase beside a recruit; rivals: {(list.Length == 0 ? "none" : list)}"));
        }

        return lines;
    }

    /// <summary>
    /// The mastery line of <c>show</c> (issue 69): the class's mastery ability and the unit's
    /// points against the requirement, or that it is mastered. Silent for a class with none.
    /// </summary>
    public static string? MasteryLine(BattleUnit unit, GameContent content)
    {
        var unitClass = content.Class(unit.Unit.ClassId);
        if (unitClass.Mastery is not { } id)
        {
            return null;
        }

        var name = content.Ability(id).Name;
        return unit.Unit.Abilities.Contains(id)
            ? $"  mastery: {name}, mastered"
            : $"  mastery: {name} ({unit.Unit.Mastery.Points(unitClass.Id)} of {unitClass.MasteryPoints} {(content.Weapons.Values.Any(w => w.Heals && unitClass.CanUse(w.Type)) ? "combats or heals" : "combats")})";
    }

    /// <summary>
    /// On a rivalry map, the line under a forecast that names what rivalry changed on the
    /// player's side: the rivals beside the unit and the modifiers the forecast included.
    /// Silent when no rival is adjacent.
    /// </summary>
    private void PrintRivalry(BattleUnit unit, bool countering)
    {
        if (RivalryLine(_state, _content, unit, countering, UnitNames.Of(_state, _content)) is { } line)
        {
            _out.WriteLine(UnitNames.Sentence(line));
        }
    }

    private static string? RivalryLine(BattleState state, GameContent content, BattleUnit unit, bool countering, UnitNames names)
    {
        if (Rivalry.ArmOf(state, content) is null || Rivalry.AdjacentRivals(state, content, unit) is not { Count: > 0 } rivals)
        {
            return null;
        }

        var (hit, crit, critAvoid) = Rivalry.Modifiers(state, content, unit, countering);
        return $"  rivalry: {names[unit.Id]} beside {string.Join(", ", rivals.Select(r => names[r.Id]))}: acc {hit:+0;-0;0} crit {crit:+0;-0;0} crit evade {critAvoid:+0;-0;0}";
    }

    /// <summary>
    /// Under a forecast between a sworn player unit and the enemy sworn on it (issue 331): the
    /// crit avoid the forecast already took off, so the reader sees why the crit is what it is.
    /// Silent otherwise.
    /// </summary>
    public static string? SwornLine(BattleUnit a, BattleUnit b, UnitNames? names = null)
    {
        names ??= UnitNames.None;
        var (enemy, player) = a.Side == Side.Enemy ? (a, b) : (b, a);
        return enemy.Grudge == player.Id && enemy.Side != player.Side
            ? $"  sworn: {names[enemy.Id]} on {names[player.Id]}: {names[player.Id]} crit evade {Grudges.SwornCritAvoid:+0;-0;0}"
            : null;
    }

    /// <summary>
    /// Under a forecast on a <c>pincer: on</c> map (DESIGN.md 13.13): one line for each side
    /// that is pinned, naming the unit behind it and the hit the forecast already added, the
    /// strike first and the counter second. Silent when neither is pinned.
    /// </summary>
    public static IEnumerable<string> PincerLines(BattleState state, BattleUnit attacker, BattleUnit target, UnitNames? names = null)
    {
        names ??= UnitNames.None;
        if (Pincer.PinnedBy(state, attacker, target) is { } behindTarget)
        {
            yield return $"  pincer: {names[target.Id]} pinned by {names[behindTarget.Id]}: {names[attacker.Id]} acc +{Pincer.Hit}";
        }

        if (Pincer.PinnedBy(state, target, attacker) is { } behindAttacker)
        {
            yield return $"  pincer: {names[attacker.Id]} pinned by {names[behindAttacker.Id]}: {names[target.Id]} acc +{Pincer.Hit}";
        }
    }

    /// <summary>
    /// Under a forecast on a <c>break: on</c> map (DESIGN.md 13.22): for the target, then the
    /// striker, when it is a boss whose fall would break someone, one line naming each member at
    /// or below half with its HP. Silent otherwise.
    /// </summary>
    public static IEnumerable<string> BreakLines(BattleState state, GameContent content, BattleUnit attacker, BattleUnit target, UnitNames? names = null)
    {
        names ??= UnitNames.None;
        foreach (var boss in new[] { target, attacker })
        {
            var members = Break.WouldBreak(state, content, boss);
            if (members.Count > 0)
            {
                yield return $"  break if {names[boss.Id]} falls: " + string.Join(", ", members.Select(m => $"{names[m.Id]} ({m.Hp}/{m.MaxHp(content)})"));
            }

            var sworn = Break.Sworn(state, boss);
            if (sworn.Count > 0)
            {
                yield return $"  sworn: will not break: " + string.Join(", ", sworn.Select(m => names[m.Id]));
            }
        }

        if (Freed.ForecastLine(state, attacker, target, names) is { } bond)
        {
            yield return "  " + bond;
        }
    }

    /// <summary>
    /// Under a forecast (DESIGN.md 13.23, experiment): for the striker with the weapon it strikes
    /// with, then the target when it counters, the heal a kill would feed a hungering weapon, and in
    /// the starved form the hit that eases it (<see cref="Kinsbane.ForecastLines"/>), and for the striker
    /// on its own phase of <paramref name="state"/> the hunt running on (issue 804). Given the
    /// <paramref name="forecast"/>, a kill row reads <c>kills on hit:</c> when one plain hit by that
    /// unit reaches the other's HP, else <c>on a kill:</c> (issue 939). Silent for any other weapon.
    /// </summary>
    public static IEnumerable<string> HungerLines(GameContent content, BattleUnit attacker, BattleUnit target, int? slot, bool targetCounters, UnitNames? names = null, BattleState? state = null, CombatForecast? forecast = null)
    {
        names ??= UnitNames.None;
        var armed = slot is { } chosen && chosen >= 0 && chosen < attacker.Unit.Inventory.Count ? attacker.WithSlotInFront(chosen) : attacker;
        foreach (var unit in targetCounters ? new[] { armed, target } : new[] { armed })
        {
            var equipped = unit.EquippedSlot(content);
            if (equipped < 0)
            {
                continue;
            }

            int? huntMov = state is not null && unit == armed && unit.Side == state.Phase ? Kinsbane.HuntMov(state, content, unit) : null;
            var side = forecast is null ? null : unit == armed ? forecast.Attacker : forecast.Defender;
            var foe = unit == armed ? target : armed;
            var killsOnHit = side is { Strikes: true } && side.Damage >= foe.Hp;
            var falls = killsOnHit && state is not null ? Returned.Falls(state, foe, names) : null;
            foreach (var line in Kinsbane.ForecastLines(unit, content, unit.EquippedWeapon(content), unit.Unit.Inventory.Items[equipped], names[unit.Id], huntMov, killsOnHit, falls))
            {
                yield return line;
            }
        }
    }

    /// <summary>
    /// Under a forecast on a <c>brace: on</c> map (DESIGN.md 13.14): one line when the target is
    /// braced, naming the hit the forecast already took off. Silent otherwise; the striker is never
    /// braced, since its brace ends before it can strike.
    /// </summary>
    public static IEnumerable<string> BraceLines(BattleUnit attacker, BattleUnit target, UnitNames? names = null)
    {
        names ??= UnitNames.None;
        if (target.Braced)
        {
            yield return $"  brace: {names[target.Id]} braced: {names[attacker.Id]} acc -{Brace.Hit}";
        }
    }

    /// <summary>
    /// Under a forecast (issue 772): one line when the target is open to the striker, naming the Def and Res the
    /// forecast already took off, or one when the striker is the target's own opener, who never reads it. Silent otherwise.
    /// </summary>
    public static IEnumerable<string> OpenLines(BattleUnit attacker, BattleUnit target, UnitNames? names = null)
    {
        names ??= UnitNames.None;
        if (target.Open is not { } open)
        {
            yield break;
        }

        yield return Opening.Reads(target, attacker)
            ? $"  open: {names[target.Id]} opened by {names[open.By]}: Def -{open.Def}, Res -{open.Res} in this forecast"
            : $"  open: {names[target.Id]} is open to {names[open.By]}'s allies, not to {names[attacker.Id]}";
    }

    /// <summary>
    /// Under a forecast on a <c>signatures: on</c> map (DESIGN.md 13.18): Teodor's orders on the
    /// striker, Teodor's own watched penalty, each naming the hit the forecast already holds, and
    /// Ottilie's refusal with the displayed hit it refuses. The forecast still answers. Silent otherwise.
    /// </summary>
    public static IEnumerable<string> SignatureLines(BattleState state, GameContent content, BattleUnit attacker, BattleUnit target, CombatForecast forecast, UnitNames? names = null)
    {
        names ??= UnitNames.None;
        if (Signatures.OrderedBy(state, content, attacker) is { } teodor)
        {
            yield return $"  signature: {names[teodor.Id]}'s orders: {names[attacker.Id]} acc +{Signatures.OrdersHit}";
        }

        if (Signatures.Watched(state, content, attacker))
        {
            yield return $"  signature: {names[attacker.Id]} acc -{Signatures.WatchedHit} (ally within {Signatures.OrdersRadius})";
        }

        if (Signatures.Refuses(state, content, attacker, forecast.Attacker.DisplayedHit))
        {
            yield return $"  signature: {names[attacker.Id]} refuses this strike: {forecast.Attacker.DisplayedHit} is under {Signatures.LedgerFloor}";
        }
    }

    /// <summary>
    /// Under a forecast on a <c>wildfire: on</c> map (DESIGN.md 13.15): one line when the strike's
    /// weapon ignites and the target stands on forest, and one when the target counters with a
    /// weapon that does and the striker's tile is forest, naming the tile a hit sets alight. Silent otherwise.
    /// </summary>
    public static IEnumerable<string> IgniteLines(BattleState state, GameContent content, BattleUnit attacker, BattleUnit target, Coord tile, int? slot, bool counters, UnitNames? names = null)
    {
        names ??= UnitNames.None;
        if (!state.Map.WildfireEnabled)
        {
            yield break;
        }

        if (Resolver.ChooseWeapon(attacker, content, slot).Weapon is { Ignites: true } && state.Map.TerrainIdAt(target.At) == Wildfire.ForestTerrainId)
        {
            yield return $"  wildfire: {names[attacker.Id]} ignites {target.At} on a hit";
        }

        if (counters && target.EquippedWeapon(content) is { Ignites: true } && state.Map.TerrainIdAt(tile) == Wildfire.ForestTerrainId)
        {
            yield return $"  wildfire: {names[target.Id]} ignites {tile} on a hit";
        }
    }

    /// <summary>
    /// Under a forecast on a <c>windup: on</c> map (DESIGN.md 13.16): one line when the attack
    /// raises a blow instead of fighting, with what the blow would deal the target where it stands;
    /// one when the target has a raised blow, saying whether a hit from the attacker's tile breaks
    /// it; and one when the attacker's tile is under another unit's blow, with the certain damage
    /// it lands on the attacker. Silent otherwise.
    /// </summary>
    public static IEnumerable<string> WindupLines(BattleState state, GameContent content, BattleUnit attacker, BattleUnit target, int? slot, UnitNames? names = null)
    {
        names ??= UnitNames.None;
        if (!state.Map.WindupEnabled)
        {
            yield break;
        }

        var (armed, weapon, _) = Resolver.ChooseWeapon(attacker, content, slot);
        if (Windup.Raises(state, weapon))
        {
            var damage = Windup.Damage(state, content, armed, target);
            yield return $"  windup: no combat now; {names[attacker.Id]} raises a blow over {target.At}, landing at {names.Refer(attacker.Id).Possessive} next phase start on whoever stands there ({names[target.Id]}: {damage}, sure) unless a hit from within its reach breaks it";
        }

        if (target.WindupAt is { } at)
        {
            yield return Windup.Breaks(content, target, attacker.At)
                ? $"  windup: a hit on {names[target.Id]} breaks {names.Refer(target.Id).Possessive} blow over {at}"
                : $"  windup: a hit from {attacker.At} does not break {names[target.Id]}'s blow over {at} (outside {names.Refer(target.Id).Possessive} reach)";
        }

        if (Windup.Over(state, attacker.At) is { } wielder && wielder.Id != attacker.Id)
        {
            yield return $"  windup: {names[wielder.Id]}'s blow lands on {attacker.At} at {names.Refer(wielder.Id).Possessive} next phase start: {Windup.Damage(state, content, wielder, attacker)} to {names[attacker.Id]}, sure";
        }
    }

    private string Named(string itemId) => _content.ItemName(itemId);

    /// <summary>
    /// The living unit named <paramref name="id"/>, or null with the console's error. At dusk
    /// an enemy the player does not see is refused as out of sight; given a mover and the
    /// tile a forecast is asked from, an enemy the mover would see from that tile is found
    /// (<see cref="Dusk.Seen(BattleState, BattleUnit, string, Coord)"/>, issue 309).
    /// </summary>
    private BattleUnit? Find(string id, string? moverId = null, Coord? moverAt = null)
    {
        var unit = _state.Find(id);
        if (unit is null)
        {
            Error($"no living unit '{id}'");
        }
        else if (moverId is not null && moverAt is { } tile ? !Dusk.Seen(_state, unit, moverId, tile) : !Dusk.Seen(_state, unit))
        {
            Error($"no unit '{id}' in sight at dusk");
            return null;
        }

        return unit;
    }

    /// <summary>
    /// <c>order &lt;kind&gt;</c> calls Commander's Word (issue 85); <c>order &lt;kind&gt; preview [from &lt;x,y&gt;]</c>
    /// prints who it would reach and changes nothing. The kind <c>fall back</c> may be typed as two words.
    /// </summary>
    private void OrderWords(string[] words)
    {
        const string usage = "usage: order <press|rally|fall back> [preview [from <x,y>]]";
        var rest = words.Skip(1).ToList();
        if (rest.Count >= 2 && rest[0] == "fall" && rest[1] == "back")
        {
            rest = new[] { "fall back" }.Concat(rest.Skip(2)).ToList();
        }

        if (rest.Count == 0 || Orders.Parse(rest[0]) is not { } kind)
        {
            Error(usage);
            return;
        }

        if (rest.Count == 1)
        {
            Apply(new Order(kind));
            return;
        }

        Coord? from = null;
        if (rest[1] != "preview" || rest.Count is not (2 or 4)
            || (rest.Count == 4 && (rest[2] != "from" || !TryCoord(rest[3], out var tile) || (from = tile) is null)))
        {
            Error(usage);
            return;
        }

        _out.WriteLine(OrderPreview(_state, _content, kind, from));
    }

    /// <summary>
    /// The order preview (issue 85): <c>Press would reach Wren, Teodor (2 of 4; radius 4 from 3,4)</c>,
    /// the allies <see cref="Orders.Reached"/> lists from the captain's tile or <paramref name="from"/>,
    /// over the living allies. A spent or closed order says why after the count.
    /// </summary>
    public static string OrderPreview(BattleState state, GameContent content, OrderKind kind, Coord? from = null)
    {
        if (!state.OrdersOpen)
        {
            return "There are no orders on this map";
        }

        if (state.UnitsOf(Side.Player).FirstOrDefault(u => u.IsCaptain) is not { } captain)
        {
            return "There is no captain on the board to call an order";
        }

        var names = UnitNames.Of(state, content);
        var at = from ?? captain.At;
        var reached = Orders.Reached(state, content, captain, at, kind);
        var who = reached.Count == 0 ? "no one" : string.Join(", ", reached.Select(u => names[u.Id]));
        var line = $"{Orders.Word(kind)} would reach {who} ({reached.Count} of {Orders.Allies(state).Count}; radius {Orders.Radius(captain, content)} from {at})";
        if (Orders.Refusal(state) is { } refusal)
        {
            line += $"; not now: {refusal}";
        }

        return UnitNames.Sentence(names.Message(line));
    }

    /// <summary>
    /// Prints <paramref name="message"/> after <c>ERROR:</c> as a reader sees it (issue 615):
    /// unit ids as names, quoted ids as typed, sentence case; the scripted summary keeps the same text.
    /// </summary>
    private void Error(string message, string asTyped = "")
    {
        message = UnitNames.Of(_state, _content).Message(message) + asTyped;
        _out.WriteLine("ERROR: " + message);
        if (_scripted)
        {
            _rejections.Add((_line, _command, message));
        }
    }

    private static bool TryCoord(string text, out Coord at)
    {
        at = default;
        var parts = text.Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
        {
            return false;
        }

        at = new Coord(x, y);
        return true;
    }

    /// <summary>A command as a script line types it, the words <c>recall list</c> names a state by.</summary>
    public static string CommandText(Command command) => command switch
    {
        Move m => m.Via is { } via ? $"move {m.UnitId} {m.To} via {via}" : $"move {m.UnitId} {m.To}",
        Attack a => $"attack {a.UnitId} {a.TargetId}" + (a.Slot is null ? "" : " " + (a.Slot + 1)) + (a.Art is null ? "" : " art " + a.Art),
        Canto c => $"canto {c.UnitId} {c.To}",
        Wait w => $"wait {w.UnitId}",
        Watch w => $"watch {w.UnitId}",
        Cover c => $"cover {c.UnitId} {c.AllyId}",
        Exit x => $"exit {x.UnitId}",
        Recover r => $"recover {r.UnitId}",
        Open o => $"open {o.UnitId} {o.At}",
        Drop d => $"drop {d.UnitId}",
        Talk t => $"talk {t.UnitId} {t.TargetId}",
        Order o => $"order {Orders.Word(o.Kind)}",
        FallBack f => $"fallback {f.UnitId} {f.To}",
        Shove s => $"shove {s.UnitId} {s.TargetId}",
        Dash d => $"dash {d.UnitId} {d.To}",
        Carry c => $"carry {c.UnitId} {c.AllyId} {c.To} {c.SetDown}",
        Breathe b => $"breathe {b.UnitId} {b.Toward}",
        StrikeLine l => $"strikeline {l.UnitId} {l.Toward}",
        Retreat r => $"retreat {r.UnitId} {r.To}",
        EndPhase => "end",
        Recall r => $"recall {r.ToIndex}",
        Undo u => $"undo {u.UnitId}",
        UseItem i => $"item {i.UnitId} {i.Slot + 1}" + (i.TargetId is null ? "" : " " + i.TargetId),
        _ => command.ToString() ?? "?",
    };

    /// <summary>
    /// One line per event, the words a transcript reader sees. Items, weapons, abilities, classes
    /// and terrain print their display names from <paramref name="content"/>; units print the names in
    /// <paramref name="names"/>, never their ids (issue 609), and every line is in sentence case.
    /// A command still types the id; only the narration reads as names.
    /// </summary>
    public static string Describe(GameEvent e, GameContent content, UnitNames names) =>
        UnitNames.Sentence(DescribeLine(e, content, names));

    private static string DescribeLine(GameEvent e, GameContent content, UnitNames names)
    {
        switch (e)
        {
            case MoveUndone u:
                return $"{names[u.UnitId]} takes back the move to {u.From} and stands at {u.To} again, unmoved";
            case UnitMoved m:
                return $"{names[m.UnitId]} moves {m.From} -> {m.To}" + (m.Path.Count > 1 ? " via " + string.Join(" ", m.Path.Take(m.Path.Count - 1)) : "");
            case CombatFought f:
                var sb = new StringBuilder();
                sb.Append($"{names[f.AttackerId]} attacks {names[f.TargetId]}");
                foreach (var strike in f.Strikes)
                {
                    sb.Append('\n').Append("  ").Append(names[strike.AttackerId]).Append(' ')
                        .Append(!strike.Hit ? "misses " + names[strike.TargetId] : $"{(strike.Crit ? "crits" : "hits")} {names[strike.TargetId]} for {strike.Damage} (hp {strike.TargetHpAfter})");
                }

                if (f.Bite is { } bite)
                {
                    sb.Append('\n').Append($"  the drake bites: {bite.Damage} ({names[bite.TargetId]} hp {bite.TargetHpAfter})");
                }

                sb.Append('\n').Append($"  {names[f.AttackerId]} hp {f.AttackerHpAfter}, {names[f.TargetId]} hp {f.TargetHpAfter}");
                return sb.ToString();
            case UnitDied d:
                return $"{names[d.UnitId]} falls at {d.At}" + (names.Rides(d.UnitId) ? $"; {names.Refer(d.UnitId).Possessive} drake leaves the field" : "");
            case ExpGained x:
                return $"{names[x.UnitId]} gains {x.Amount} exp ({x.ExpAfter})";
            case LeveledUp l:
                var rose = string.Join(" ", Stats.All.Where(stat => l.Gains.Get(stat) > 0).Select(stat => stat.ToString().ToLowerInvariant() + " +1"));
                return $"{names[l.UnitId]} reaches level {l.NewLevel}: {(rose.Length == 0 ? "nothing rose" : rose)}";
            case RankRaised k:
                return $"{names[k.UnitId]} reaches rank {k.Rank} in {k.Type.Label()}";
            case MasteryEarned m:
                return $"{names[m.UnitId]} masters the {ClassName(m.ClassId, content)} class and keeps {AbilityName(m.AbilityId, content)}";
            case UnitWaited w:
                return w.Braced ? $"{names[w.UnitId]} waits and braces" : $"{names[w.UnitId]} waits";
            case UnitExited x:
                return $"{names[x.UnitId]} leaves through the exit at {x.At}";
            case UnitLeftBehind b:
                return $"{names[b.UnitId]} is left behind at {b.At}";
            case KeepsakeLeft k:
                return $"{Keepsake.Name(k.ItemId, k.FallenId, content)} lies at {k.At}";
            case TomeDropped t:
                return $"{names[t.UnitId]} drops {string.Join(", ", t.ItemIds.Select(content.ItemName))}: to the wagon, kept only if the map is won";
            case KeepsakeRecovered k:
                return $"{names[k.UnitId]} recovers {Keepsake.Name(k.ItemId, k.FallenId, content)}";
            case ChestOpened c:
                return ChestLine(c, names, content);
            case KeepsakeTaken k:
                return $"{names[k.UnitId]} takes {Keepsake.Name(k.ItemId, k.FallenId, content)}";
            case KeepsakeLost k:
                return k.CarrierId is { } carrier
                    ? $"{Keepsake.Name(k.ItemId, k.FallenId, content)} went with {names[carrier]}"
                    : $"{Keepsake.Name(k.ItemId, k.FallenId, content)} was left at {k.At}";
            case Cantoed c:
                return c.From == c.To
                    ? $"{names[c.UnitId]} stays at {c.To} (move again)"
                    : $"{names[c.UnitId]} moves again {c.From} -> {c.To}" + (c.Path.Count > 1 ? " via " + string.Join(" ", c.Path.Take(c.Path.Count - 1)) : "");
            case OrderCalled o:
                return $"{names[o.CaptainId]} calls {Orders.Word(o.Kind)} (radius {o.Radius}): " + (o.Reached.Count == 0 ? "it reaches no one" : string.Join(", ", o.Reached.Select(id => names[id]))) + $" ({o.Reached.Count} of {o.Alive})";
            case FellBack f:
                return f.From == f.To
                    ? $"{names[f.UnitId]} holds at {f.To} (fall back)"
                    : $"{names[f.UnitId]} falls back {f.From} -> {f.To}" + (f.Path.Count > 1 ? " via " + string.Join(" ", f.Path.Take(f.Path.Count - 1)) : "");
            case UnitWinded wd:
                return $"{names[wd.UnitId]} dashed and is winded: struck at +{Winded.Hit} Acc until the player phase";
            case Shoved s:
                return $"{names[s.UnitId]} shoves {names[s.TargetId]} {s.From} -> {s.To}";
            case Breathed b:
                return $"{names[b.UnitId]}'s drake breathes rime from {b.From} over {string.Join(" ", b.Line)}" + (b.Frozen.Count > 0 ? $"; the water freezes at {string.Join(" ", b.Frozen)}" : "") + (b.Chilled.Count == 0 ? "; no one is caught" : "");
            case Carried c:
                return $"{names[c.UnitId]}'s drake carries {names[c.AllyId]} {c.AllyFrom} -> {c.SetDown}; lands free to move and act";
            case UnitRetreated r:
                return $"{names[r.UnitId]} falls back to {r.To} and will not fight this phase";
            case UnitBroke b:
                return $"{names[b.UnitId]} breaks and flees ({b.Hp} hp)";
            case UnitFreed f:
                return $"{names[f.UnitId]} lays down the weapon: freed ({f.Hp} hp)";
            case RouteDrifted d:
                return $"{UnitNames.Group(d.Group)} wakes and makes for {d.To}, the crossing you took: {string.Join(", ", d.Units.Select(id => names[id]))}";
            case UnitTalked t:
                return t.Fate == ReturnFate.Turned
                    ? $"{names[t.UnitId]} talks {names[t.TargetId]} round: turned, off the field ({t.Hp} hp)"
                    : $"{names[t.UnitId]} talks {names[t.TargetId]} round: spared, off the field ({t.Hp} hp)";
            case MessengerEscaped m:
                return $"{names[m.UnitId]} reaches the road at {m.At} and is gone: the word is out";
            case FrontFell f:
                return Fronts.FallLine(new Front(f.Front, ValueList<Coord>.Empty));
            case GrudgeSworn g:
                return $"{names[g.UnitId]} swears a grudge against {names[g.AgainstId]}";
            case UnitHealed h:
                return $"{names[h.UnitId]} heals {h.Amount} (hp {h.HpAfter})";
            case UnitBurned b:
                return $"{names[b.UnitId]} burns {b.Amount} (hp {b.HpAfter})";
            case RockfallStruck r:
                return $"  the rock strikes {names[r.UnitId]} at {r.At} for {r.Amount} (hp {r.HpAfter})";
            case UnitRested r:
                return $"{names[r.UnitId]} is spent from the strike and cannot move or act this phase";
            case HungerDrained h:
                return $"{content.ItemName(h.ItemId)} drains {names[h.UnitId]} {h.Amount} (hp {h.HpAfter})" + (h.Starved ? "; it starves: half power, uses 1" : "");
            case HungerFed h:
                return $"{content.ItemName(h.ItemId)} feeds: fed {h.Fed}, power +{h.MtBonus}" + (Kinsbane.ToothGrew(h.Fed) ? $", a tooth grows (teeth {Kinsbane.Teeth(h.Fed)}/{Kinsbane.MtCap})" : "") + (h.Healed > 0 ? $"; {names[h.UnitId]} heals {h.Healed} (hp {h.HpAfter})" : "") + (h.Woke ? "; it wakes and hungers no more" : "");
            case KinsbaneSpoke k:
                return $"{content.ItemName(k.ItemId)}, to {names[k.UnitId]}: \"{k.Text}\"";
            case HuntRanOn h:
                return $"the hunt runs on: {names[h.UnitId]} may move again, {h.Mov} movement";
            case HungerEased h:
                return $"{content.ItemName(h.ItemId)} is eased by the hit" + (h.Healed > 0 ? $"; {names[h.UnitId]} heals {h.Healed} (hp {h.HpAfter})" : "");
            case UnitOpened o:
                return $"{names[o.UnitId]} is open: allies of {names[o.ByUnitId]} strike it at Def -{o.Def}, Res -{o.Res} until the phase ends";
            case UnitIgnited i:
                return $"{names[i.UnitId]} " + (i.Stacks > 1 ? "burns hotter" : "catches fire") + $": {i.Amount} hp at the start of each of its side's next {SchoolRider.PhasesText(i.Phases)} ({i.Stacks} {(i.Stacks == 1 ? "stack" : "stacks")}" + (i.Laid > 1 ? $", {i.Laid} laid by the hit)" : ")");
            case UnitDrained dr:
                return $"{names[dr.UnitId]} drains {dr.Amount} from {names[dr.FromUnitId]} (hp {dr.HpAfter})";
            case UnitCursed cu:
                return $"{names[cu.UnitId]} is cursed by {names[cu.ByUnitId]}: Hit -{cu.Blind}, and {cu.Amount} hp to {names[cu.ByUnitId]} at the start of each of its side's next {SchoolRider.PhasesText(cu.Phases)}";
            case UnitMarked um:
                return $"{names[um.UnitId]} is marked by {names[um.ByUnitId]}: the next {um.School.Label()} hit on it deals {Mark.Times}";
            case MarkCashed mc:
                return $"{names[mc.ByUnitId]} cashes the mark on {names[mc.UnitId]}";
            case LineStruck ls:
                return $"{names[ls.UnitId]} strikes a line {LineStrike.Directions.Single(d => ls.Line.Count > 0 && ls.From.X + d.Dx == ls.Line[0].X && ls.From.Y + d.Dy == ls.Line[0].Y).Name} from {ls.From} over {string.Join(" ", ls.Line)}, striking {string.Join(", ", ls.Struck.Select(id => names[id]))}";
            case AreaCastAt ac:
                return $"{names[ac.CasterId]} casts {(content.Weapons.TryGetValue(ac.SpellId, out var storm) ? storm.Name : ac.SpellId)} at {ac.At}, striking {string.Join(", ", ac.Struck.Select(id => names[id]))} ({ac.UsesLeft} {(ac.UsesLeft == 1 ? "use" : "uses")} left)";
            case CurseTicked ct:
                return $"the curse takes {ct.Amount} from {names[ct.UnitId]} (hp {ct.HpAfter})" + (ct.CasterId is { } caster ? $"; {names[caster]} heals {ct.Healed} (hp {ct.CasterHpAfter})" : "; its caster is gone, and it heals no one");
            case BurnCashed c:
                return $"{names[c.ByUnitId]} cashes the burn on {names[c.UnitId]}: {c.Amount} at once (hp {c.HpAfter}), and it burns no more";
            case GroundRaised g:
                return $"{names[g.UnitId]} raises {(content.Terrain.TryGetValue(g.TerrainId, out var raised) ? raised.Name.ToLowerInvariant() : g.TerrainId)} under {names[g.TargetId]} at {g.At}: held by whoever stands on it until the caster's next phase ends";
            case ArmorDonned { Shell: true } shell:
                return (shell.WearerId is { } shelled ? $"{names[shell.UnitId]} lays {(content.Weapons.TryGetValue(shell.SpellId, out var laid) ? laid.Name : shell.SpellId)} on {names[shelled]}" : $"{names[shell.UnitId]} wears {(content.Weapons.TryGetValue(shell.SpellId, out var own) ? own.Name : shell.SpellId)}")
                    + $": a shell, Def +{shell.Def} against the first hit{(shell.Mov > 0 ? $", Mov -{shell.Mov}" : "")}, through its side's next {SchoolRider.PhasesText(shell.Phases)}";
            case ArmorDonned ad:
                return (ad.WearerId is { } on ? $"{names[ad.UnitId]} lays {(content.Weapons.TryGetValue(ad.SpellId, out var given) ? given.Name : ad.SpellId)} on {names[on]}" : $"{names[ad.UnitId]} wears {(content.Weapons.TryGetValue(ad.SpellId, out var worn) ? worn.Name : ad.SpellId)}")
                    + $": Def +{ad.Def}, Mov -{ad.Mov} through its side's next {SchoolRider.PhasesText(ad.Phases)}";
            case ShardSwallowed ss:
                return $"{names[ss.UnitId]} pulls the shard from the lance's pommel and swallows it: stage 2 on a fresh bar (hp {ss.Hp}); Frozen Iron falls on every unit from the next phase";
            case FrozenIronFell fi:
                return $"Frozen Iron falls on every unit for {fi.Amount}, past Def and Res: " + string.Join(", ", fi.Struck.Select((id, i) => $"{names[id]} (hp {fi.HpAfter[i]})"));
            case KinHealed kh:
                return $"The Kin heals {names[kh.UnitId]} {kh.Amount} (hp {kh.HpAfter})";
            case ColdDrained cd:
                return $"The cold drains out of {names[cd.UnitId]}; for a breath he is himself (his last line waits on Lotus). The shard lies on {cd.At}";
            case ArmorShattered ash:
                return $"{names[ash.ByUnitId]}'s hit shatters the {(content.Weapons.TryGetValue(ash.SpellId, out var glass) ? glass.Name : ash.SpellId)} on {names[ash.UnitId]}";
            case ArmorFell af:
                return $"{names[af.UnitId]}'s {(content.Weapons.TryGetValue(af.SpellId, out var shed) ? shed.Name : af.SpellId)} falls away";
            case HollowRaised hr:
                return $"{names[hr.UnitId]} raises {names[hr.FallenId]} at {hr.At}: {names[hr.HollowId]} stands on {names[hr.UnitId]}'s side (hp {hr.Hp}), acts from its side's next phase, and crumbles after {SchoolRider.PhasesText(hr.Phases)} or when {names[hr.UnitId]} falls";
            case HollowCrumbled hc:
                return hc.RaiserFell ? $"{names[hc.UnitId]} crumbles: its raiser is gone" : $"{names[hc.UnitId]} crumbles: its last phase is over";
            case GroundSundered gs:
                return $"{names[gs.UnitId]} sunders the {(content.Terrain.TryGetValue(gs.TerrainId, out var sundered) ? sundered.Name.ToLowerInvariant() : gs.TerrainId)} {names[gs.OwnerId]} raised at {gs.At}: the ground falls back";
            case RodCaught rc:
                return $"{names[rc.UnitId]}'s rod catches {names[rc.ByUnitId]}'s spell aimed at {names[rc.AimedId]}: it strikes {names[rc.UnitId]}";
            case RodCharged rch:
                return $"{names[rch.UnitId]}'s rod is charged: the next {rch.School.Label()} cast deals {LightningRod.BoostText}";
            case RodChargeSpent rcs:
                return $"{names[rcs.UnitId]} spends the rod's charge";
            case UnitStunned st:
                return $"{names[st.UnitId]} is stunned: it skips {(st.Next ? "its side's phase after this one" : "its side's next phase")}, and still counters";
            case StunSkipped sk:
                return $"{names[sk.UnitId]} is stunned and skips this phase";
            case UnitCleansed uc:
                return $"{names[uc.UnitId]} is cleansed by {names[uc.ByUnitId]}: " + string.Join(", ", new[] { uc.Burn ? "the burn" : null, uc.Chill ? "the chill" : null, uc.Stun ? "the stun" : null, uc.Curse ? "the curse" : null, uc.Frozen ? "the freeze" : null }.Where(p => p is not null)) + " cleared" + (uc.Freed ? "; it may move and act this phase" : "");
            case UnitFrozen fz:
                return $"{names[fz.UnitId]} is frozen: {Freeze.What(fz.Boss)} until {Frost.Until(fz.Side, fz.Next)}";
            case UnitChilled c:
                return $"{names[c.UnitId]} is chilled: Mov -{Frost.MovLost} until {Frost.Until(c.Side, c.Next)}";
            case UnitFrosted f:
                return $"the drake's frost strikes {names[f.UnitId]} for {f.Damage} (hp {f.HpAfter})"
                    + (f.Held ? $"; held to {DrakeFrost.HoldMov} tile, no Canto, until {Frost.Until(f.Side, f.Next)}" : f.Boss ? "; a boss: no hold" : "; already held");
            case UnitLocked l:
                return $"{names[l.UnitId]} is locked by {names[l.ByUnitId]}: Mov 0 until {Frost.Until(l.Side, l.Next)} while {names[l.ByUnitId]} stands beside";
            case LockDropped d:
                return $"{names[d.UnitId]}'s lock drops: {names[d.ByUnitId]} no longer stands beside it; chilled still";
            case UnitGrounded g:
                return $"{names[g.UnitId]} is grounded: moves on foot until {Frost.Until(g.Side, g.Next)}";
            case HeirloomTurned t:
                return $"{content.ItemName(t.ItemId)} turns in {names[t.UnitId]}'s hands: {t.StageId}. {Heirloom.Shape(content.Weapon(t.ItemId), new ItemStack(t.ItemId, 0) { Stage = t.Stage }).Description}";
            case WatchTaken w:
                return $"{names[w.UnitId]} watches from {w.At}"
                    + (!w.Holds ? "" : w.HoldsInsteadOf is { } instead ? $"; holds instead of {w.At} -> {instead}" : "; holds (no move closer)")
                    + (w.PassedUpTargetId is { } passed ? $"; passes up {names[passed]} at {w.PassedUpHit}" : "; no strike passed up");
            case WatchFired w:
                return $"{names[w.UnitId]}'s watch fires on {names[w.TargetId]} at {w.At}: " + (w.Strike.Hit ? (w.Strike.Crit ? "crit " : "hit ") + w.Strike.Damage : "miss") + $" ({names[w.TargetId]} hp {w.Strike.TargetHpAfter})";
            case WatchHeld w:
                return $"{names[w.UnitId]}'s watch holds on {names[w.TargetId]} at {w.At}: {w.Hit} is under {Signatures.LedgerFloor}; {names.Refer(w.UnitId).Subject} still {names.Refer(w.UnitId).Verb("watches", "watch")}";
            case WatchEnded w:
                return $"{names[w.UnitId]} is struck and stops watching";
            case CoverTaken c:
                return $"{names[c.UnitId]} covers {names[c.AllyId]}; if struck, {names[c.AllyId]} lands on {c.AllyLandsOn}" + (c.PassedUpTargetId is { } passedUp ? $"; passes up {names[passedUp]} at {c.PassedUpHit}" : "; no strike passed up");
            case CoverFired c:
                return $"{names[c.UnitId]} covers {names[c.AllyId]}: steps onto {c.At}, {names[c.AllyId]} to {c.AllyTo}; {names[c.AttackerId]}'s strike "
                    + (c.WouldHaveKilled ? "would have killed" : "would not have killed") + $" {names[c.AllyId]}" + (c.Counters ? "" : $"; {names[c.UnitId]} cannot counter");
            case BlowRaised b:
                return $"{names[b.UnitId]} raises a blow over {b.At} ({names[b.TargetId]}); it lands at {names[b.UnitId]}'s next phase start";
            case BlowLanded b:
                return $"{names[b.UnitId]}'s blow lands on {names[b.TargetId]} at {b.At} for {b.Damage} (hp {b.TargetHpAfter})";
            case BlowFell b:
                return $"{names[b.UnitId]}'s blow falls on empty ground at {b.At}";
            case BlowBroken b:
                return $"{names[b.UnitId]}'s blow over {b.At} is broken";
            case PhaseEnded p:
                return $"-- {p.Side.ToString().ToLowerInvariant()} phase ends, turn {p.Turn} --";
            case PhaseBegan p:
                return $"-- {p.Side.ToString().ToLowerInvariant()} phase, turn {p.Turn} --";
            case GroupWoke g:
                return $"{UnitNames.Group(g.Group)} wakes ({(g.CalledBy is { } by ? (g.Cause == WakeCause.Death ? "a death in " : "called by ") + UnitNames.Group(by) : WakeCauseText(g))})"
                    + (g.Lamps.Count > 0 ? $"; their lamps are lit ({string.Join(", ", g.Lamps.Select(l => $"{names[l.UnitId]} {l.At}"))})" : "");
            case MapEventFired m:
                return names.Event(m.Name, m.Blocked, m.Terrain is null ? null : content.Terrain.TryGetValue(m.Terrain, out var barring) ? barring.Name : m.Terrain);
            case TerrainChanged t:
                return $"  {t.At} becomes {(content.Terrain.TryGetValue(t.TerrainId, out var terrain) ? terrain.Name : t.TerrainId)}";
            case UnitSpawned u:
                return $"  {names[u.UnitId]} arrives at {u.At} with {UnitNames.Group(u.Group)}, {u.Behavior.ToString().ToLowerInvariant()}";
            case FlagSet f:
                return $"  flag {f.Flag} is set";
            case BarReleased b:
                return $"{b.Event.Replace('_', ' ')} gives way: nobody holds {b.Holder}";
            case ArrivalWaits a:
                return $"{content.Unit(a.Template).Name.ToLowerInvariant()} waits at {a.At} ({(a.Terrain is null ? "a unit holds it" : (content.Terrain.TryGetValue(a.Terrain, out var walled) ? walled.Name : a.Terrain).ToLowerInvariant())}); it lands at the first enemy phase that starts with {a.At} open";
            case RapportGained g:
                return $"rapport {names[g.A]} and {names[g.B]} +{g.Amount} ({g.Total}{(g.OutOf is { } outOf ? $" of {outOf}" : "")})";
            case RivalryEnded r:
                return $"{names[r.A]} and {names[r.B]} are rivals no longer";
            case SupportReached s:
                return $"{names[s.A]} and {names[s.B]} reach support {s.Tier}";
            case Recalled r:
                return $"recalled to state {r.ToIndex}; {ChargesLeft(r.ChargesLeft)}";
            case ItemUsed i:
                return $"{names[i.UnitId]} uses {content.ItemName(i.ItemId)}" + (i.TargetId == i.UnitId ? "" : " on " + i.TargetId) + $" ({i.UsesLeft} left)";
            case WeaponEquipped w:
                return $"{names[w.UnitId]} equips {content.ItemName(w.ItemId)}";
            case ArtDeclared a:
                return $"{names[a.UnitId]} declares {AbilityName(a.ArtId, content)} with {content.ItemName(a.ItemId)}, spending {a.Cost} extra {(a.Cost == 1 ? "use" : "uses")}";
            case WeaponBroke b:
                return $"{names[b.UnitId]}'s {content.ItemName(b.ItemId)} breaks";
            case SpellSpent s:
                return $"{names[s.UnitId]}'s {content.ItemName(s.ItemId)} is spent for this battle";
            default:
                return e.ToString() ?? "?";
        }
    }

    /// <summary>
    /// The console's lines for an opened chest (issue 679): what went to the pack, then, when
    /// anything did not fit, what went to the wagon, collected only if the map is won. A tome or
    /// grimoire the opener cannot wield is still taken and says why (issue 1285), so a chest is
    /// never a dead click: <c>Cinder (cannot wield: fire school)</c>.
    /// </summary>
    internal static string ChestLine(ChestOpened c, UnitNames names, GameContent content)
    {
        string Name(string id) =>
            c.CannotWield.FirstOrDefault(s => s.ItemId == id) is { } shortOf ? $"{content.ItemName(id)} (cannot wield: {shortOf.Why})" : content.ItemName(id);

        var pack = c.ItemIds.Count == 0 ? "nothing fits in the pack" : string.Join(", ", c.ItemIds.Select(Name));
        var line = $"{names[c.UnitId]} opens the chest at {c.At}: {pack}";
        return c.Wagon.Count == 0 ? line : line + $"\n  To the wagon, kept if the map is won: {string.Join(", ", c.Wagon.Select(Name))}";
    }

    /// <summary>An ability's name as the console prints it, its id when content names none.</summary>
    public static string AbilityName(string id, GameContent content) =>
        content.Abilities.TryGetValue(id, out var ability) ? ability.Name : id;

    private static string ClassName(string id, GameContent content) =>
        content.Classes.TryGetValue(id, out var unitClass) ? unitClass.Name : id;
}

/// <summary>One recruit that ended a threatened player phase beside a rival (issues 16 and 209), and whether the enemy phase after struck it.</summary>
internal sealed class ExposureEntry
{
    public ExposureEntry(int historyAt, int turn, string unitId)
    {
        HistoryAt = historyAt;
        Turn = turn;
        UnitId = unitId;
    }

    public int HistoryAt { get; }

    public int Turn { get; }

    public string UnitId { get; }

    public bool Attacked { get; set; }
}
