using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core;

namespace Ironwake.Cli;

/// <summary>
/// <c>ironwake campaign</c> (issue 74, DESIGN section 9): the maps of <c>campaign.json</c> in
/// order, each preceded by the between-map screen, text only. The screen reads the roster, the
/// shop and the next map's deployment, and takes the actions of <see cref="CampaignRecord"/>:
/// buy, repair, certify, trial, quest (a side map, issue 635), build (the keep's menu, after the raid on it; issue 288), bench and unbench; <c>march</c> starts the battle, which plays as
/// <c>play</c> does until <c>leave</c> after it is decided. A won battle returns to the screen
/// with the reward paid and the fallen gone; a lost one ends this campaign and offers the fail
/// menu: load a save, a new game, or quit (issue 663). Every camp autosaves, <c>save</c> names a
/// save, and <c>quit</c> in a battle writes a suspend that <c>--resume</c> replays once (<see cref="SaveStore"/>).
/// The same script grammar and <c>--strict</c> as <c>play</c>, one script for the whole campaign.
/// </summary>
public sealed class CampaignSession
{
    public const string Usage = "usage: ironwake campaign [--seed N] [--script file] [--strict] [--content dir] [--difficulty id] [--permadeath on|off] [--origin id] [--captain he|she] [--scheme one|two] [--from map [--pick claimant [--fed N]] [--level N]] [--log file] [--saves dir] [--load save | --resume] [--reseed N]";

    /// <summary>Where the keyboard's saves go when <c>--saves</c> is not given; a scripted run keeps none unless it is.</summary>
    public const string DefaultSavesDirectory = "saves";

    private const string Help = """
        Between maps:
          camp                     The camp as one view: roster, keep, quests, shop, then the march line
          roster                   Every unit: class, level, EXP, items with uses, mastery
          show <unit>              One unit's numbers, ranks and mastery
          shop                     What the shop sells before this map, and each price
          buy <item> <unit>        Buy an item at full uses into the unit's next free slot
          repair <unit> <slot>     Restore a weapon's uses, at its price per use
          refine <unit> <slot> mt|hit  Raise a weapon one step at the keep's forge, for one material and the smith's fee
          drop <unit> <slot>       Throw away an item, no refund; a signature item is never dropped
          take <unit> <n>          Move entry n of the wagon (what chests sent past a full pack) into the unit's pack
          classes [unit]           What each class asks for promotion into it, and what the unit still lacks
          certify <unit> <class>   Promote into a class, paying a seal from the purse
          trial <unit> <class>     Try the class's certification trial instead of a seal; one attempt per camp
          quest <id> <ally>...     Fight a member's side map with the allies its board takes; who falls there is gone for good
          keep                     The keep's rooms and beds; once the raid is fought, each wall placement, its price and what it does
          build <room>             Buy a room for the keep from the purse; each adds beds, and no bed free means a recruit will not join
          hire [<id>]              List the barracks' hires, or hire one into the company from the purse (once the barracks is built)
          pick <unit>              Fill the last seat with one of the two claimants; the other rides home (final)
          meet <unit>              Take on the side character met at this camp, if a bed is free (final; one a map)
          build <edit> <x,y>       Buy one edit of the keep's menu at one of its placements
          bench <unit>             Keep a unit off the next map; the next in roster order fills its slot
          unbench <unit>           Return a benched unit to the deployment order
          record                   The campaign record as one JSON line (the protocol's campaign shape)
          difficulty <name>        Lower the campaign's difficulty for the rest of it; never raised, never mid-battle
          save <name>              Save the campaign as it stands at this camp; every camp also autosaves, keeping the last three
          saves                    Every save: the autosaves, newest first, then the named ones
          march                    Start the next map
          help                     This list
        In battle, every play command; leave ends a battle once it is won or lost; quit suspends it, and --resume picks it up once
        Slots count from 1
        """;

    private readonly GameContent _content;
    private readonly string _contentDir;
    private readonly TextWriter _out;
    private readonly bool _scripted;
    private readonly RollScheme _scheme;
    private readonly List<(int Line, string Command, string Reason)> _rejections = new();
    private readonly SaveStore? _saves;
    private CampaignRecord _record;
    private IReadOnlyList<string>? _resume;

    /// <summary>The seed the loaded save pinned, when <c>--reseed</c> replaced it (issue 963); null otherwise.</summary>
    private ulong? _reseededFrom;
    private bool _strictStopped;
    private int _line;
    private int _commands;

    /// <summary>
    /// The campaign's event log (issue 360): what changed and nothing else, the lines the thin
    /// renderer's campaign presenter logs. Each map's opening line, each accepted between-map
    /// action's line, a trial's opening lines, every battle's event lines as <c>play --log</c>
    /// writes them, and the won or lost lines; never a listing, a refusal, an echo or a board.
    /// <c>--log</c> writes it to a file.
    /// </summary>
    private readonly StringWriter _log = new() { NewLine = "\n" };

    /// <summary>The event lines printed so far, each ending in <c>\n</c>.</summary>
    internal string EventLog => _log.ToString();

    /// <summary>
    /// The first card's line for a save replayed on a new seed (issue 963, DECISIONS/0238): every
    /// roll after it is the new seed's, so a read taken from it is not the save's battle.
    /// </summary>
    internal static string ReseededLine(ulong from, ulong to) => $"reseeded from {from} to {to}: not the save's battle";

    /// <summary>Prints one event line to the console and to the event log.</summary>
    private void WriteEvent(string line)
    {
        _out.WriteLine(line);
        _log.WriteLine(line);
    }

    private CampaignSession(GameContent content, string contentDir, CampaignRecord record, TextWriter output, bool scripted, RollScheme scheme, SaveStore? saves, IReadOnlyList<string>? resume)
    {
        _saves = saves;
        _resume = resume;
        _content = content;
        _contentDir = contentDir;
        _record = record;
        _out = output;
        _scripted = scripted;
        _scheme = scheme;
    }

    /// <summary>
    /// <paramref name="record"/> with the hungering weapon <paramref name="pick"/> carries fed
    /// <paramref name="fed"/> times and not starved (issue 865, round 286): <c>--fed N</c>, so a chair
    /// opening on a later map with <c>--from</c> plays the scythe at the feed the campaign would have
    /// grown, its teeth and Mt as <see cref="Kinsbane"/> derives them from the count. Fed 0 is the
    /// stack as issued. Null when the pick is not on the roster or carries no hungering weapon.
    /// </summary>
    public static CampaignRecord? Fed(CampaignRecord record, GameContent content, string pick, int fed)
    {
        if (record.Roster.FirstOrDefault(u => u.Id == pick) is not { } unit
            || !unit.Inventory.Items.Any(s => content.Weapons.TryGetValue(s.ItemId, out var w) && w.Hungers))
        {
            return null;
        }

        var items = unit.Inventory.Items.Select(s => content.Weapons.TryGetValue(s.ItemId, out var w) && w.Hungers ? s with { Fed = fed, Starved = false } : s);
        var fedUnit = unit with { Inventory = new Inventory(ValueList<ItemStack>.From(items)) };
        return record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == pick ? fedUnit : u)) };
    }

    /// <summary>
    /// A line for each drake a won map grew (issue 805), <c>Rook's drake is grown.</c>, in roster
    /// order: a rider in both records whose stage moved.
    /// </summary>
    public static IEnumerable<string> DrakesGrown(CampaignRecord before, CampaignRecord after) =>
        after.Roster
            .Where(u => u.Drake is { } now && before.Find(u.Id)?.Drake is { } then && now.Stage != then.Stage)
            .Select(u => $"{u.Name}'s drake is {Drake.Word(u.Drake!.Stage)}.");

    /// <summary>Parses the arguments after <c>campaign</c> and runs it: 0 when every map is won, 1 on a loss or an unfinished script, 2 for a usage error, 3 for a strict stop.</summary>
    public static int Run(string[] args)
    {
        ulong seed = 1;
        var seedGiven = false;
        string? script = null;
        var strict = false;
        var contentDir = "content";
        var difficulty = CampaignRecord.NormalDifficulty;
        var scheme = RollScheme.TwoRollAverage;
        string? from = null;
        string? pick = null;
        int? level = null;
        int? fed = null;
        string? log = null;
        string? savesDir = null;
        string? load = null;
        var resume = false;
        ulong? reseed = null;
        var permadeath = true;
        string? origin = null;
        Pronoun? captain = null;
        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "--seed" when value is not null && ulong.TryParse(value, out seed):
                    seedGiven = true;
                    i++;
                    break;
                case "--script" when value is not null:
                    script = value;
                    i++;
                    break;
                case "--strict":
                    strict = true;
                    break;
                case "--content" when value is not null:
                    contentDir = value;
                    i++;
                    break;
                case "--difficulty" when value is not null:
                    difficulty = value;
                    i++;
                    break;
                case "--scheme" when value is not null && RollSchemes.Parse(value) is { } parsedScheme:
                    scheme = parsedScheme;
                    i++;
                    break;
                case "--from" when value is not null:
                    from = value;
                    i++;
                    break;
                case "--pick" when value is not null:
                    pick = value;
                    i++;
                    break;
                case "--level" when value is not null && int.TryParse(value, out var parsedLevel) && parsedLevel >= Unit.MinLevel && parsedLevel <= Unit.MaxLevel:
                    level = parsedLevel;
                    i++;
                    break;
                case "--fed" when value is not null && int.TryParse(value, out var parsedFed):
                    fed = parsedFed;
                    i++;
                    break;
                case "--log" when value is not null:
                    log = value;
                    i++;
                    break;
                case "--saves" when value is not null:
                    savesDir = value;
                    i++;
                    break;
                case "--load" when value is not null:
                    load = value;
                    i++;
                    break;
                case "--resume":
                    resume = true;
                    break;
                case "--reseed" when value is not null && ulong.TryParse(value, out var parsedReseed):
                    reseed = parsedReseed;
                    i++;
                    break;
                case "--permadeath" when value is "on" or "off":
                    permadeath = value == "on";
                    i++;
                    break;
                case "--origin" when value is not null:
                    origin = value;
                    i++;
                    break;
                case "--captain" when value is "he" or "she":
                    captain = value == "he" ? Pronoun.He : Pronoun.She;
                    i++;
                    break;
                default:
                    Console.WriteLine($"ERROR: unexpected argument '{args[i]}'");
                    Console.WriteLine(Usage);
                    return 2;
            }
        }

        if (strict && script is null)
        {
            Console.WriteLine("ERROR: --strict applies to a scripted run; give --script");
            Console.WriteLine(Usage);
            return 2;
        }

        if (pick is not null && from is null)
        {
            Console.WriteLine("ERROR: --pick makes the branch's pick for a campaign opening after it; give --from");
            Console.WriteLine(Usage);
            return 2;
        }

        if (level is not null && from is null)
        {
            Console.WriteLine("ERROR: --level raises the company a campaign opens with on a later map; give --from");
            Console.WriteLine(Usage);
            return 2;
        }

        if (fed is not null && from is null)
        {
            Console.WriteLine("ERROR: --fed feeds the pick's hungering weapon for a campaign opening on a later map; give --from");
            Console.WriteLine(Usage);
            return 2;
        }

        if (fed is not null && pick is null)
        {
            Console.WriteLine("ERROR: --fed feeds the pick's hungering weapon; give --pick");
            Console.WriteLine(Usage);
            return 2;
        }

        if (fed is { } count && (count < 0 || count > Kinsbane.WakeKills))
        {
            Console.WriteLine($"ERROR: --fed is 0 to {Kinsbane.WakeKills}, the count at which the weapon wakes; got {count}");
            Console.WriteLine(Usage);
            return 2;
        }

        if ((load is not null || resume) && (from is not null || (load is not null && resume)))
        {
            Console.WriteLine("ERROR: --load, --resume and --from each pick where the campaign starts; give one");
            Console.WriteLine(Usage);
            return 2;
        }

        if ((load is not null || resume) && seedGiven)
        {
            Console.WriteLine($"ERROR: --seed {seed} starts a new campaign; a loaded or resumed campaign keeps the seed its save pins; --reseed N replays it on a new seed");
            Console.WriteLine(Usage);
            return 2;
        }

        if (reseed is { } unloaded && load is null && !resume)
        {
            Console.WriteLine($"ERROR: --reseed {unloaded} replays a save on a new seed; give --load or --resume");
            Console.WriteLine(Usage);
            return 2;
        }

        if ((load is not null || resume) && (origin is not null || captain is not null))
        {
            Console.WriteLine("ERROR: --origin and --captain choose a new campaign's captain; a loaded campaign keeps its own");
            Console.WriteLine(Usage);
            return 2;
        }

        if ((load is not null || resume) && script is not null && savesDir is null)
        {
            Console.WriteLine("ERROR: a scripted run keeps no saves unless --saves names a directory");
            Console.WriteLine(Usage);
            return 2;
        }

        GameContent content;
        try
        {
            content = ContentLoader.Load(contentDir);
        }
        catch (ContentException e)
        {
            Console.WriteLine("ERROR: " + e.Message);
            return 1;
        }

        if (content.Campaign.Maps.Count == 0)
        {
            Console.WriteLine($"ERROR: content has no campaign: {ContentFiles.CampaignName} is missing under {contentDir}");
            return 2;
        }

        if (content.Cast.Count == 0)
        {
            Console.Error.WriteLine(PlaySession.NoCast);
            return 2;
        }

        if (content.Difficulties.Count > 0)
        {
            if (DifficultyNamed(content, difficulty) is not { } chosen)
            {
                Console.WriteLine($"ERROR: no difficulty '{difficulty}'; the content declares {string.Join(", ", content.Difficulties.Values.Select(DifficultyChoice))}");
                return 2;
            }

            difficulty = chosen.Id;
        }

        if (origin is not null && content.Campaign.Origin(origin) is null)
        {
            Console.WriteLine(content.Campaign.Origins.Count == 0
                ? $"ERROR: the campaign offers no origins, so '{origin}' cannot be chosen"
                : $"ERROR: the campaign has no origin '{origin}'; it offers {string.Join(", ", content.Campaign.Origins.Select(o => o.Id))}");
            return 2;
        }

        if (from is not null && content.Campaign.Maps.All(m => m.MapId != from))
        {
            Console.WriteLine($"ERROR: the campaign has no map '{from}'; it lists {string.Join(", ", content.Campaign.Maps.Select(m => m.MapId))}");
            return 2;
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

        var saves = savesDir is not null ? new SaveStore(savesDir) : script is null ? new SaveStore(DefaultSavesDirectory) : null;
        if (load is null && !resume && saves is not null && content.Difficulties.TryGetValue(difficulty, out var picked) && !picked.IsUnlocked(saves.Won()))
        {
            Console.WriteLine($"ERROR: {picked.DisplayName} unlocks when a campaign is won on {content.Difficulty(picked.UnlockedBy!).DisplayName}");
            return 2;
        }

        CampaignRecord record;
        try
        {
            record = from is null ? CampaignRecord.Start(content, seed, difficulty, permadeath, origin, captain) : CampaignRecord.StartAt(content, seed, from, difficulty, permadeath, origin, captain, pick);
        }
        catch (ArgumentException e)
        {
            Console.WriteLine("ERROR: " + e.Message);
            return 2;
        }

        if (level is { } floor)
        {
            record = record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.ScaledTo(floor, content.Class(u.ClassId)))) };
        }

        if (fed is { } feed)
        {
            if (Fed(record, content, pick!, feed) is not { } fedRecord)
            {
                Console.WriteLine($"ERROR: {pick} carries no hungering weapon, so --fed has nothing to feed");
                return 2;
            }

            record = fedRecord;
        }

        IReadOnlyList<string>? battleLines = null;
        if (load is not null)
        {
            var (loaded, refusal) = saves!.Load(load, content);
            if (loaded is null)
            {
                Console.WriteLine("ERROR: " + refusal);
                return 2;
            }

            record = loaded;
        }
        else if (resume)
        {
            var (suspended, lines, refusal) = saves!.TakeSuspend(content);
            if (suspended is null)
            {
                Console.WriteLine("ERROR: " + refusal);
                return 2;
            }

            record = suspended;
            battleLines = lines;
        }

        ulong? reseededFrom = null;
        if (reseed is { } fresh)
        {
            var refusal = fresh == record.Seed
                ? $"--reseed {fresh} is the seed the save already pins; give another"
                : battleLines is { Count: > 0 }
                    ? $"--reseed {fresh} would replay the suspended battle on other rolls; --load the camp's autosave and reseed that"
                    : null;
            if (refusal is not null)
            {
                if (resume)
                {
                    // The suspend is put back, so the refusal costs the player nothing.
                    saves!.Suspend(record, battleLines ?? Array.Empty<string>());
                }

                Console.WriteLine("ERROR: " + refusal);
                return 2;
            }

            reseededFrom = record.Seed;
            record = record with { Seed = fresh };
        }

        var session = new CampaignSession(content, contentDir, record, Console.Out, script is not null, scheme, saves, battleLines) { _reseededFrom = reseededFrom };
        var code = session.Play(input, strict);
        if (log is not null)
        {
            File.WriteAllText(log, session.EventLog);
        }

        return code;
    }

    private int Play(TextReader input, bool strict)
    {
        _out.WriteLine($"Campaign, seed {_record.Seed}, {RulesLine(_record, _content)}, scheme {_scheme}, {_content.Campaign.Maps.Count} maps");
        if (_reseededFrom is { } pinned)
        {
            WriteEvent(ReseededLine(pinned, _record.Seed));
        }
        if (CaptainLine(_record, _content) is { } captainLine)
        {
            _out.WriteLine(captainLine);
        }
        var code = 1;
        var stopped = false;
        while (!_record.IsFinished(_content))
        {
            MapDefinition map;
            try
            {
                map = LoadMap(_record.NextMap(_content).MapId);
            }
            catch (MapException e)
            {
                _out.WriteLine("ERROR: " + e.Message);
                return 1;
            }

            var resume = _resume;
            _resume = null;
            if (resume is null && Screen(input, map, strict) != true)
            {
                stopped = strict && _rejections.Count > 0;
                break;
            }

            if (_record.NextMap(_content).MapId == _content.Campaign.Keep.MapId)
            {
                // The keep is rebuilt after the screen, so an edit bought on it stands in this battle.
                map = LoadMap(_content.Campaign.Keep.MapId);
            }

            WriteEvent(MapLine(_record, _content, map));
            if (resume is null)
            {
                Lines(SceneLines(_record, _content, ScenePoint.Before, _record.NextMap(_content).MapId, map.Name));
            }

            var screen = new QuietWriter(_out);
            var battle = new PlaySession(_content, _record.Begin(map, _content, _scheme), screen, _scripted, _line);
            var replayed = 0;
            if (resume is not null)
            {
                screen.Quiet = true;
                var replayCommands = 0;
                battle.RunCommands(new StringReader(string.Concat(resume.Select(l => l + "\n"))), false, ref replayCommands);
                screen.Quiet = false;
                replayed = battle.Rejections.Count;
                _out.WriteLine($"Resumed {map.Name} at turn {battle.State.Turn} from the suspend, {resume.Count} lines replayed; the suspend is deleted");
            }

            _out.WriteLine("Objective: " + Objective.Line(battle.State, _content));
            _out.Write(MapRenderer.Render(battle.State, _content));
            var typed = new RecordingReader(input, resume);
            stopped = battle.RunCommands(typed, strict, ref _commands);
            _line = battle.Line;
            _rejections.AddRange(battle.Rejections.Skip(replayed));
            _log.Write(battle.EventLog);
            if (!stopped && !battle.Left && _saves is not null)
            {
                _saves.Suspend(_record, typed.Lines.Where(l => l.Trim() != "quit"));
                _out.WriteLine($"Suspended in {map.Name} at turn {battle.State.Turn}: ironwake campaign --resume picks it up once, from {_saves.Directory}");
                break;
            }

            if (stopped || !battle.Left)
            {
                _out.WriteLine($"Campaign stopped in {map.Name} at turn {battle.State.Turn}, {(battle.State.Outcome.IsOver ? "decided and not left" : "undecided")}");
                break;
            }

            if (battle.State.Outcome.Result != BattleResult.Won)
            {
                WriteEvent(LostLine(battle.State, _content));
                if (FailMenu(input, strict) is not { } next)
                {
                    stopped = _strictStopped;
                    break;
                }

                _record = next;
                continue;
            }

            var before = _record;
            _record = _record.AfterBattle(battle.State, _content);
            WriteEvent(WonLine(before, _record, battle.State, _content));
            foreach (var grown in DrakesGrown(before, _record))
            {
                WriteEvent(grown);
            }
            Lines(AfterCard(before, _content, map));
            Lines(SceneLines(_record, _content, ScenePoint.After, before.NextMap(_content).MapId, map.Name));
        }

        if (_record.IsFinished(_content))
        {
            WriteEvent(CampaignWonLine(_record, _content));
            Lines(EndingLines(_record, _content));
            code = 0;
            _saves?.WriteEnding(_record, _content);
            if (_saves is not null && _saves.RecordWin(_record.Difficulty))
            {
                foreach (var opened in _content.Difficulties.Values.Where(d => d.UnlockedBy == _record.Difficulty))
                {
                    _out.WriteLine($"Unlocked: {opened.DisplayName} (--difficulty {opened.Id})");
                }
            }
        }

        if (_scripted && _rejections.Count > 0)
        {
            _out.WriteLine($"Rejected {_rejections.Count} of {_commands} commands:");
            foreach (var (at, command, reason) in _rejections)
            {
                _out.WriteLine($"  line {at}: {command}: {reason}");
            }
        }

        return stopped ? PlaySession.StrictStop : code;
    }

    /// <summary>
    /// The campaign map <paramref name="mapId"/>: the keep and its raid from <c>content/keep</c>, the
    /// keep with every edit the record holds made (issue 288), every other map from <c>content/maps</c>.
    /// </summary>
    private MapDefinition LoadMap(string mapId) => MapFor(_contentDir, _content, _record, mapId);

    private MapDefinition LoadBareKeep() => BareKeep(_contentDir, _content);

    /// <summary>
    /// The campaign map <paramref name="mapId"/> as <paramref name="record"/> would fight it: the keep
    /// with every edit the record holds made (issue 288), every other map as its file stands.
    /// </summary>
    public static MapDefinition MapFor(string contentDir, GameContent content, CampaignRecord record, string mapId)
    {
        var map = MapFiles.Load(MapFiles.CampaignPath(contentDir, content, mapId), content);
        return mapId == content.Campaign.Keep.MapId ? record.KeepMap(map, content) : map;
    }

    /// <summary>The keep as its file stands, before any edit; the placements of the keep's menu are read on it.</summary>
    public static MapDefinition BareKeep(string contentDir, GameContent content) =>
        MapFiles.Load(MapFiles.CampaignPath(contentDir, content, content.Campaign.Keep.MapId), content);

    /// <summary>
    /// The certification trial for <paramref name="classId"/> from <c>content/trials</c>, or the refusal
    /// the screen prints: the record's own refusal, a trial map that will not load, or one that
    /// certifies another class.
    /// </summary>
    public static (MapDefinition? Trial, string? Refusal) TrialFor(string contentDir, GameContent content, CampaignRecord record, string unitId, string classId)
    {
        if (record.TrialRefusal(unitId, classId, content) is { } refusal)
        {
            return (null, refusal);
        }

        MapDefinition trial;
        try
        {
            trial = MapFiles.Load(Path.Combine(contentDir, "trials", content.Campaign.TrialFor(classId)!.MapId + ".map"), content);
        }
        catch (MapException e)
        {
            return (null, e.Message);
        }

        return CampaignRecord.TrialMapRefusal(trial, classId) is { } mismatch ? (null, mismatch) : (trial, null);
    }

    /// <summary>The line that opens a campaign battle: which map of how many, and the seed it plays on.</summary>
    public static string MapLine(CampaignRecord record, GameContent content, MapDefinition map) =>
        $"Map {record.MapIndex + 1} of {content.Campaign.Maps.Count}: {map.Name}, seed {record.BattleSeed}";

    /// <summary>The lines that open a certification trial: the map and its seed, then who plays it as what.</summary>
    public static IReadOnlyList<string> TrialLines(CampaignRecord record, GameContent content, MapDefinition trial, string unitId, string classId) => new[]
    {
        $"Trial: {trial.Name}, seed {record.TrialSeed(content)}",
        $"Promotion trial: {UnitNames.Of(record, content)[unitId]} plays as {content.Class(classId).Name} with {string.Join(", ", trial.Certification!.Loadout)}",
    };

    /// <summary>The line after a won battle: the reason, the reward, the purse, and who fell on it, by name.</summary>
    public static string WonLine(CampaignRecord before, CampaignRecord after, BattleState end, GameContent content)
    {
        var names = UnitNames.Of(after, content);
        var fallen = after.Fallen.Skip(before.Fallen.Count).Select(id => names[id]).ToList();
        return $"{end.Map.Name} won: {end.Outcome.Reason}; reward {after.Purse - before.Purse}, the purse holds {after.Purse}"
            + (fallen.Count > 0 ? $"; fallen: {string.Join(", ", fallen)}" : "; nobody fell");
    }

    /// <summary>The line a lost battle ends the campaign on, its reason naming units as the battle does.</summary>
    public static string LostLine(BattleState end, GameContent content) =>
        $"Campaign lost on {end.Map.Name}: {UnitNames.Of(end, content).Named(Objective.Reason(end, content))}";

    /// <summary>The line once every map is won.</summary>
    public static string CampaignWonLine(CampaignRecord record, GameContent content) =>
        $"Campaign won: all {content.Campaign.Maps.Count} maps, the purse holds {record.Purse}";

    /// <summary>
    /// The lines after the campaign is won for those without an epilogue card (issue 690): one
    /// <c>&lt;name&gt; served at the keep.</c> per hire still in the company, in roster order, or the
    /// line a won quest of theirs gives in its place (issue 691).
    /// </summary>
    public static IReadOnlyList<string> EndingLines(CampaignRecord record, GameContent content) =>
        record.Roster.Where(u => Barracks.IsHire(u, content)).Select(u => Barracks.EndingLine(record, u, content)).ToList();

    /// <summary>
    /// A between-map line as a reader sees it (issue 615): a refusal or an accepted action's
    /// line from <see cref="CampaignRecord"/>, each unit id on the roster or among the fallen
    /// read as its name, in sentence case: <c>Wren buys Vulnerary for 300; ...</c>.
    /// </summary>
    public static string Text(CampaignRecord record, GameContent content, string text) =>
        UnitNames.Of(record, content).Message(text);

    /// <summary>
    /// <c>trial &lt;unit&gt; &lt;class&gt;</c> (issue 252): refused as <see cref="CampaignRecord.TrialRefusal"/>
    /// says, or for a trial map that will not load or certifies another class; otherwise the trial
    /// plays on the screen with every <c>play</c> command until <c>leave</c>, and its result is
    /// applied through <see cref="CampaignRecord.AfterTrial"/>. False on a strict stop or when the
    /// input ends inside the trial.
    /// </summary>
    private bool RunTrial(TextReader input, string text, string unitId, string classId, bool strict)
    {
        var (trial, refusal) = TrialFor(_contentDir, _content, _record, unitId, classId);
        if (trial is null)
        {
            Error(text, refusal!);
            return true;
        }

        foreach (var opening in TrialLines(_record, _content, trial, unitId, classId))
        {
            WriteEvent(opening);
        }
        var battle = new PlaySession(_content, _record.BeginTrial(trial, unitId, _content, _scheme), _out, _scripted, _line);
        battle.WritePendingEvents();
        _out.WriteLine("Objective: " + Objective.Line(battle.State, _content));
        _out.Write(MapRenderer.Render(battle.State, _content));
        var stopped = battle.RunCommands(input, strict, ref _commands);
        _line = battle.Line;
        _rejections.AddRange(battle.Rejections);
        _log.Write(battle.EventLog);
        if (stopped || !battle.Left)
        {
            _out.WriteLine($"Campaign stopped in {trial.Name} at turn {battle.State.Turn}, {(battle.State.Outcome.IsOver ? "decided and not left" : "undecided")}");
            return false;
        }

        Take(_record.AfterTrial(battle.State, unitId, _content), text);
        return true;
    }

    /// <summary>
    /// The side map <paramref name="questId"/> from <c>content/quests</c>, or the refusal the screen
    /// prints (issue 635): the record's own refusal, a map that will not load, or one whose slots
    /// are not a side map's.
    /// </summary>
    public static (MapDefinition? Map, string? Refusal) QuestFor(string contentDir, GameContent content, CampaignRecord record, string questId, string allyId) =>
        QuestFor(contentDir, content, record, questId, new[] { allyId });

    /// <summary>
    /// <see cref="QuestFor(string, GameContent, CampaignRecord, string, string)"/> for a side map
    /// that takes several allies (issue 691), refused too when <paramref name="allyIds"/> do not
    /// fill its bare slots one each (<see cref="CampaignRecord.QuestAlliesRefusal"/>).
    /// </summary>
    public static (MapDefinition? Map, string? Refusal) QuestFor(string contentDir, GameContent content, CampaignRecord record, string questId, IReadOnlyList<string> allyIds)
    {
        if (record.QuestRefusal(questId, allyIds, content) is { } refusal)
        {
            return (null, refusal);
        }

        MapDefinition map;
        try
        {
            map = MapFiles.Load(QuestPath(contentDir, content.Campaign.Quest(questId)!), content);
        }
        catch (MapException e)
        {
            return (null, e.Message);
        }

        return (CampaignRecord.QuestMapRefusal(map) ?? Kinsbane.WarningRefusal(map, content, content.Campaign.Quest(questId)!.MemberId)) is { } shape ? (null, shape)
            : CampaignRecord.QuestAlliesRefusal(map, allyIds) is { } count ? (null, count)
            : (map, null);
    }

    private static string QuestPath(string contentDir, CampaignQuest quest) =>
        Path.Combine(contentDir, MapFiles.QuestsDirectory, quest.MapId + MapFiles.Extension);

    /// <summary>
    /// The side maps this interlude offers, as the screen prints them (issue 635): a heading with
    /// the price, then one line per quest naming its member, its part, its board and the command.
    /// Empty when none is offered.
    /// </summary>
    public static IReadOnlyList<string> QuestLines(string contentDir, GameContent content, CampaignRecord record)
    {
        var offered = record.QuestsOffered(content);
        if (offered.Count == 0)
        {
            return Array.Empty<string>();
        }

        var names = UnitNames.Of(record, content);
        var price = record.Permadeath ? "permadeath applies" : "permadeath is off: who falls comes back wounded";
        var boards = offered.Select(quest => QuestBoard(contentDir, content, quest)).ToList();
        var who = boards.All(b => b.Allies == 1) ? "one ally you pick" : "the allies you pick";
        var lines = new List<string> { $"Side maps (the member and {who}, not the captain; a lost side map never ends the campaign):" };
        foreach (var (quest, (board, allies)) in offered.Zip(boards))
        {
            var closed = record.QuestsTried.Contains(quest.Id) ? "; fought since the last map, open again after the next" : "";
            var command = $"quest {quest.Id} {string.Join(" ", Enumerable.Repeat("<ally>", allies))}";
            var what = content.Cast.Any(u => u.Id == quest.MemberId) ? $"quest {quest.Part}" : "request";
            lines.Add($"  {quest.Id}: {names[quest.MemberId]}'s {what}, {board} ({command}); {price}{closed}");
        }

        return lines;
    }

    /// <summary>A side map's board by its display name and the allies it takes (issue 691), or its id and one when the board will not load.</summary>
    private static (string Board, int Allies) QuestBoard(string contentDir, GameContent content, CampaignQuest quest)
    {
        try
        {
            var map = MapFiles.Load(QuestPath(contentDir, quest), content);
            return (map.Name, Math.Max(1, CampaignRecord.QuestAllies(map)));
        }
        catch (MapException)
        {
            return (quest.MapId, 1);
        }
    }

    /// <summary>The camp's panel headings (issue 678), in the order the camp prints them.</summary>
    public static readonly IReadOnlyList<string> CampPanels = new[] { "== Roster ==", "== Keep ==", "== Quests ==", "== Shop ==" };

    /// <summary>
    /// The camp as one view (issue 678), what the screen prints on arriving and <c>camp</c> prints
    /// again: the heading, then four panels in order. Roster: each member's row, the fallen with the
    /// board each fell on, who deploys, and the leave warning. Keep: the purse first, then the rooms
    /// and, once the raid is fought, the walls and ditches, else why they are closed. Quests: the
    /// side maps open, with their price, then those won and what each paid. Shop: the stock and the
    /// seal. Last, the <c>march</c> line naming the next map and its objective; <c>march</c> is the
    /// one command that leaves.
    /// </summary>
    public static IReadOnlyList<string> CampLines(string contentDir, GameContent content, CampaignRecord record, MapDefinition map, bool typed = false)
    {
        var lines = new List<string> { ScreenHeading(record, content, map), CampPanels[0] };
        lines.AddRange(RosterPanelLines(record, content, map, typed));
        lines.Add(CampPanels[1]);
        lines.AddRange(KeepPanelLines(contentDir, content, record));
        lines.Add(CampPanels[2]);
        lines.AddRange(QuestPanelLines(contentDir, content, record));
        lines.Add(CampPanels[3]);
        lines.AddRange(ShopLines(record, content));
        lines.Add(MarchLine(record, content, map));
        return lines;
    }

    /// <summary>The camp's Roster panel (issue 678): each member's row, the fallen with their boards, who deploys to <paramref name="map"/>, and the leave warning.</summary>
    public static IReadOnlyList<string> RosterPanelLines(CampaignRecord record, GameContent content, MapDefinition map, bool typed = false)
    {
        var lines = RosterLines(record, content, typed).Skip(1).ToList();
        try
        {
            lines.Add(DeploymentLine(record, content, map));
        }
        catch (ArgumentException e)
        {
            lines.Add("ERROR: " + e.Message);
        }

        lines.AddRange(WarningLines(record, content, map));
        lines.AddRange(LowLines(record, content));
        lines.AddRange(BranchLines(record, content));
        lines.AddRange(MeetingLines(record, content));
        lines.AddRange(ContestLines(record, content, map));
        return lines;
    }

    /// <summary>
    /// The Roster panel's word on a contested place (issue 844): when the pick and a side character
    /// compete for the next map's open places (<see cref="CampaignRecord.ContestedPlaces"/>),
    /// <c>The Field Before the Keep has one open place: Ansgar or Rook</c>. Empty otherwise.
    /// </summary>
    public static IReadOnlyList<string> ContestLines(CampaignRecord record, GameContent content, MapDefinition map)
    {
        if (record.ContestedPlaces(map, content) is not { } contest)
        {
            return Array.Empty<string>();
        }

        var places = contest.Open == 0 ? "no open place" : "one open place";
        var names = contest.Contenders.Select(id => content.Unit(id).Name).ToList();
        return new[] { $"{map.Name} has {places}: {string.Join(", ", names.SkipLast(1))} or {names[^1]}" };
    }

    /// <summary>
    /// The Roster panel's word on a <c>keziah_warning</c> map (issue 871), before seats are chosen:
    /// with the hungering weapon's bearer on the living roster, that the map is not ideal for them and
    /// that <c>march</c> asks first if they deploy. Empty on any other map, or once asked and answered.
    /// </summary>
    public static IReadOnlyList<string> WarningLines(CampaignRecord record, GameContent content, MapDefinition map)
    {
        if (!map.KeziahWarning || record.WarningConfirmed == record.MapIndex || Kinsbane.Bearer(content) is not { } bearer
            || record.Present(content).FirstOrDefault(u => u.Id == bearer) is not { } unit)
        {
            return Array.Empty<string>();
        }

        return new[] { $"This map is not ideal for {unit.Name}; march asks first if {unit.Name} deploys (bench {bearer} to leave her)." };
    }

    /// <summary>
    /// The branch as the Roster panel prints it at its camp (issue 633, DESIGN section 14): before the
    /// pick, the two claimants with their classes, the command, each claimant's own line (issue 804,
    /// the choice screen) in the same shape, and the bed rule the return is set up under; after it, who took the seat and who rode home. Empty at every other camp.
    /// </summary>
    public static IReadOnlyList<string> BranchLines(CampaignRecord record, GameContent content)
    {
        if (ReturnLines(record, content) is { Count: > 0 } returnLines)
        {
            return returnLines;
        }

        if (record.IsFinished(content) || record.NextMap(content).Branch is not { Count: 2 } branch)
        {
            return Array.Empty<string>();
        }

        string Claimant(string id) => $"{content.Unit(id).Name} ({content.Class(content.Unit(id).ClassId).Name})";
        if (record.Pick is { } pick)
        {
            return new[] { $"The seat: {content.Unit(pick).Name}. {content.Unit(record.Passed(content)!).Name} rode home." };
        }

        var pitch = record.NextMap(content).Pitch;
        return new[] { $"The last seat: {Claimant(branch[0])} or {Claimant(branch[1])} (pick <unit>). The one passed on rides home." }
            .Concat(pitch.Select((text, i) => $"{content.Unit(branch[i]).Name}: \"{text}\""))
            .Append("They come back before the keep. Turned, they join only if a bed is free, and a death never frees one.")
            .ToList();
    }

    /// <summary>
    /// The meeting as the Roster panel prints it at its camp (issue 633 slice 3, DESIGN section 14):
    /// before it, who may be met, at what level, the command, and the bed rule with the beds as they
    /// stand, or <c>no bed free: &lt;name&gt; will not join</c> when none is; after it, who joined.
    /// Empty at every other camp.
    /// </summary>
    public static IReadOnlyList<string> MeetingLines(CampaignRecord record, GameContent content)
    {
        if (record.IsFinished(content) || record.NextMap(content).Meets is not { Count: > 0 } meets)
        {
            return Array.Empty<string>();
        }

        if (meets.FirstOrDefault(record.Met.Contains) is { } made)
        {
            return new[] { $"Met: {content.Unit(made).Name}, with the company." };
        }

        var level = record.JoinLevel(content);
        string Side(string id) => $"{content.Unit(id).Name} ({content.Class(content.Unit(id).ClassId).Name}, level {Math.Max(content.Unit(id).Level, level)})";
        if (meets.Select(id => record.Meet(id, content)).Where(r => !r.Accepted).Select(r => r.Text).ToList() is { } refused && refused.Count == meets.Count)
        {
            return meets.Select((id, i) => $"Met on the road: {Side(id)}. {char.ToUpperInvariant(refused[i][0])}{refused[i][1..]}.").ToList();
        }

        var beds = record.Beds(content) is { } total ? $" (beds: {record.BedsTaken + (record.Present(content).Count - record.Roster.Count)}/{total})" : "";
        var them = Referent.For(content, meets[0], content.Unit(meets[0]).Name);
        return new[]
        {
            $"Met on the road: {string.Join(" or ", meets.Select(Side))} (meet <unit>; one, final).",
            meets.Count == 1
                ? $"{content.Unit(meets[0]).Name} {them.Verb("joins", "join")} only if a bed is free{beds}, and a death never frees one. Not met, {them.Subject} {them.Verb("goes", "go")} on alone."
                : $"The one met joins only if a bed is free{beds}, and a death never frees one.",
        };
    }

    /// <summary>
    /// The return as the Roster panel prints it (issue 633 slice 2): at the camp before the map that
    /// brings the passed claimant back, who comes, at what level, whose talk does what, and the bed
    /// rule with the beds as they stand; at every camp after it, what became of them. Empty otherwise.
    /// </summary>
    public static IReadOnlyList<string> ReturnLines(CampaignRecord record, GameContent content)
    {
        if (record.Passed(content) is not { } passed)
        {
            return Array.Empty<string>();
        }

        var card = content.Unit(passed);
        var them = Referent.For(content, passed, card.Name);
        if (record.Returned is { } fate)
        {
            var words = fate switch
            {
                ClaimantFate.Turned => $"turned by {content.Unit(record.Pick!).Name}, and with the company",
                ClaimantFate.TurnedAway => $"turned by {content.Unit(record.Pick!).Name}, and gone: no bed was free",
                ClaimantFate.Spared => "spared, and gone",
                ClaimantFate.Fell => "fell on the field",
                _ => "stood against the company to the end, and rode off",
            };
            return new[] { $"{card.Name} came back with the enemy: {words}." };
        }

        if (record.ReturnLevel(content) is not { } level)
        {
            return Array.Empty<string>();
        }

        var beds = record.Beds(content) is { } total ? $" (beds: {record.BedsTaken}/{total})" : "";
        return new[]
        {
            $"{card.Name} ({content.Class(card.ClassId).Name}, level {level}) rides with the enemy on this map.",
            $"{content.Unit(record.Pick!).Name}'s talk turns {them.Object}; the captain's spares {them.Object} (talk <unit> {passed}). Turned, {them.Subject} {them.Verb("joins", "join")} only if a bed is free{beds}, and a death never frees one.",
        };
    }

    /// <summary>
    /// The wagon as the Keep panel prints it (issue 679): <c>Wagon: 1 Iron Bow, 2 Field Dressing (take &lt;unit&gt; &lt;n&gt;)</c>,
    /// each entry numbered for <c>take</c>; null while the wagon is empty.
    /// </summary>
    public static string? WagonLine(CampaignRecord record, GameContent content) =>
        record.Wagon.Count == 0 ? null : $"Wagon: {string.Join(", ", record.Wagon.Select((id, i) => $"{i + 1} {content.ItemName(id)}"))} (take <unit> <n>)";

    /// <summary>The camp's Keep panel (issue 678): the purse, the rooms, then the walls and ditches once the raid is fought, else why they are closed.</summary>
    public static IReadOnlyList<string> KeepPanelLines(string contentDir, GameContent content, CampaignRecord record)
    {
        var lines = new List<string> { $"Purse: {record.Purse}" };
        if (WagonLine(record, content) is { } wagon)
        {
            lines.Add(wagon);
        }

        lines.AddRange(RoomLines(record, content));
        if (record.KeepMenuRefusal(content) is { } closed)
        {
            lines.Add("Walls and ditches: " + UnitNames.Sentence(closed));
            return lines;
        }

        try
        {
            lines.AddRange(KeepLines(contentDir, content, record));
        }
        catch (MapException e)
        {
            lines.Add("ERROR: " + e.Message);
        }

        return lines;
    }

    /// <summary>The camp's Quests panel (issue 678): the side maps open with their price, then those won with their pay, or that none is open.</summary>
    public static IReadOnlyList<string> QuestPanelLines(string contentDir, GameContent content, CampaignRecord record)
    {
        var lines = QuestLines(contentDir, content, record).Concat(WonQuestLines(contentDir, content, record)).ToList();
        return lines.Count == 0 ? new[] { "No side map is open." } : lines;
    }

    /// <summary>
    /// The side maps won (issue 678), one line each under a heading, with what each paid: the
    /// signature item and the stores' material, or nothing. Empty when none is won.
    /// </summary>
    public static IReadOnlyList<string> WonQuestLines(string contentDir, GameContent content, CampaignRecord record)
    {
        if (record.QuestsWon.Count == 0)
        {
            return Array.Empty<string>();
        }

        var names = UnitNames.Of(record, content);
        var lines = new List<string> { "Side maps won:" };
        foreach (var won in record.QuestsWon)
        {
            if (content.Campaign.Quest(won.QuestId) is not { } quest)
            {
                continue;
            }

            var paid = new List<string>();
            if (quest.Pays is { } item)
            {
                paid.Add(content.ItemName(item));
            }

            if (quest.Common > 0)
            {
                paid.Add($"{quest.Common} common material");
            }

            if (quest.Rare > 0)
            {
                paid.Add($"{quest.Rare} {Forge.Label(Material.Rare)}");
            }

            if (quest.Promotes is { } classId)
            {
                paid.Add($"the {content.Class(classId).Name} class");
            }

            var what = content.Cast.Any(u => u.Id == quest.MemberId) ? $"quest {quest.Part}" : "request";
            lines.Add($"  {quest.Id}: {names[quest.MemberId]}'s {what}, {QuestBoard(contentDir, content, quest).Board}; paid {(paid.Count == 0 ? "nothing" : string.Join(" and ", paid))}");
        }

        return lines;
    }

    /// <summary>
    /// The camp's last line (issue 678): <c>march</c> and the map it starts, by its place in the
    /// campaign and its name, then the objective line the battle opens with.
    /// </summary>
    public static string MarchLine(CampaignRecord record, GameContent content, MapDefinition map)
    {
        var line = $"March (march): map {record.MapIndex + 1}, {map.Name}.";
        try
        {
            return line + " " + Objective.Line(record.Begin(map, content), content);
        }
        catch (ArgumentException)
        {
            return line;
        }
    }

    /// <summary>The lines that open a side map: the board and its seed, then who goes.</summary>
    public static IReadOnlyList<string> QuestOpening(CampaignRecord record, GameContent content, MapDefinition map, string questId, string allyId) =>
        QuestOpening(record, content, map, questId, new[] { allyId });

    /// <summary>The lines that open a side map with several allies (issue 691), named in the order given.</summary>
    public static IReadOnlyList<string> QuestOpening(CampaignRecord record, GameContent content, MapDefinition map, string questId, IReadOnlyList<string> allyIds)
    {
        var names = UnitNames.Of(record, content);
        var quest = content.Campaign.Quest(questId)!;
        var allies = allyIds.Select(id => names[id]).ToList();
        var with = allies.Count <= 2 ? string.Join(" and ", allies) : string.Join(", ", allies.Take(allies.Count - 1)) + " and " + allies[^1];
        return new[]
        {
            $"Side map: {map.Name}, seed {record.QuestSeed(questId, content)}",
            $"{names[quest.MemberId]} goes with {with}. This map is lost if {names[quest.MemberId]} falls; who falls here is gone for good.",
        };
    }

    /// <summary>
    /// <c>quest &lt;id&gt; &lt;ally&gt;...</c> (issue 635; several allies, issue 691): refused as
    /// <see cref="QuestFor(string, GameContent, CampaignRecord, string, IReadOnlyList{string})"/> says;
    /// otherwise the quest's card, then the side map plays on the screen with every <c>play</c>
    /// command until <c>leave</c>, its result applied through <see cref="CampaignRecord.AfterQuest"/>,
    /// and on a win its after card. False on a strict stop or when the input ends inside it.
    /// </summary>
    private bool RunQuest(TextReader input, string text, string questId, IReadOnlyList<string> allyId, bool strict)
    {
        var (map, refusal) = QuestFor(_contentDir, _content, _record, questId, allyId);
        if (map is null)
        {
            Error(text, refusal!);
            return true;
        }

        Lines(QuestBeforeCard(_content, map, questId));
        foreach (var opening in QuestOpening(_record, _content, map, questId, allyId))
        {
            WriteEvent(opening);
        }

        var battle = new PlaySession(_content, _record.BeginQuest(map, questId, allyId, _content, _scheme), _out, _scripted, _line);
        battle.WritePendingEvents();
        _out.WriteLine("Objective: " + Objective.Line(battle.State, _content));
        _out.Write(MapRenderer.Render(battle.State, _content));
        var stopped = battle.RunCommands(input, strict, ref _commands);
        _line = battle.Line;
        _rejections.AddRange(battle.Rejections);
        _log.Write(battle.EventLog);
        if (stopped || !battle.Left)
        {
            _out.WriteLine($"Campaign stopped in {map.Name} at turn {battle.State.Turn}, {(battle.State.Outcome.IsOver ? "decided and not left" : "undecided")}");
            return false;
        }

        if (Take(_record.AfterQuest(battle.State, questId, _content), text) && battle.State.Outcome.Result == BattleResult.Won)
        {
            Lines(QuestAfterCard(_content, map, questId));
        }

        return true;
    }

    /// <summary>
    /// The between-map screen before <paramref name="map"/>: prints it, then applies commands until
    /// <c>march</c> (true) or the input ends or a strict stop (null).
    /// </summary>
    private bool? Screen(TextReader input, MapDefinition map, bool strict)
    {
        if (_saves is not null)
        {
            _saves.Autosave(_record);
            _out.WriteLine($"Autosaved as {SaveStore.AutoPrefix}1; the last {SaveStore.AutosavesKept} camps are kept");
        }

        Lines(BeforeCard(_record, _content, map));
        Lines(SceneLines(_record, _content, ScenePoint.Camp, _record.NextMap(_content).MapId, map.Name));
        Lines(TurnedAwayLines(_record, _content));
        Lines(JoinLines(_record, _content));
        Lines(CampLines(_contentDir, _content, _record, map, typed: true));

        while (input.ReadLine() is { } line)
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

            _commands++;
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words is ["march"] or ["march", "sure"])
            {
                if (_record.MarchRefusal(map, _content) is { } refusal)
                {
                    Error(text, refusal);
                    return false;
                }

                if (_record.MarchWarning(map, _content) is { } warning)
                {
                    if (words is ["march"])
                    {
                        // Asked, not refused: the screen stays open for "march sure" or a bench,
                        // and a strict script stops on this line.
                        Error(text, warning + " (march sure, or bench the unit)");
                        if (strict)
                        {
                            _out.WriteLine($"Strict: stopped at line {_line} ({text}); no later command applied");
                            return null;
                        }

                        continue;
                    }

                    _record = _record.ConfirmWarning();
                }

                Lines(LowLines(_record, _content));
                return true;
            }

            if (words is ["trial", var trialUnit, var trialClass])
            {
                if (!RunTrial(input, text, trialUnit, trialClass, strict))
                {
                    return null;
                }
            }
            else if (words is ["quest", var questId, _, ..])
            {
                if (!RunQuest(input, text, questId, words[2..], strict))
                {
                    return null;
                }
            }
            else
            {
                Execute(words, text, map);
            }

            if (strict && _rejections.Count > 0)
            {
                _out.WriteLine($"Strict: stopped at line {_line} ({text}); no later command applied");
                return null;
            }
        }

        _out.WriteLine($"Campaign stopped before {map.Name}: the script ended on the screen");
        return null;
    }

    private void Execute(string[] words, string text, MapDefinition map)
    {
        switch (words)
        {
            case ["camp"]:
                Lines(CampLines(_contentDir, _content, _record, map, typed: true));
                break;
            case ["roster"]:
                PrintRoster();
                break;
            case ["show", var unitId]:
                if (_record.Find(unitId) is { } unit)
                {
                    PrintUnit(unit, detail: true);
                }
                else
                {
                    Error(text, $"no unit '{unitId}' on the roster");
                }

                break;
            case ["shop"]:
                PrintShop();
                break;
            case ["buy", var itemId, var unitId]:
                Take(_record.Buy(itemId, unitId, _content), text);
                break;
            case ["repair", var unitId, var slotText] when int.TryParse(slotText, out var slot):
                Take(_record.Repair(unitId, slot - 1, _content), text);
                break;
            case ["drop", var unitId, var slotText] when int.TryParse(slotText, out var slot):
                Take(_record.Drop(unitId, slot - 1, _content), text);
                break;
            case ["take", var unitId, var indexText] when int.TryParse(indexText, out var index):
                Take(_record.TakeFromWagon(unitId, index - 1, _content), text);
                break;
            case ["refine", var unitId, var slotText, var stat] when int.TryParse(slotText, out var slot):
                Take(_record.Refine(unitId, slot - 1, stat, _content), text);
                break;
            case ["difficulty", var difficultyName]:
                Take(_record.LowerDifficulty(DifficultyNamed(_content, difficultyName)?.Id ?? difficultyName, _content), text);
                break;
            case ["difficulty", ..]:
                Error(text, "usage: difficulty <name>; it may be lowered at a camp, never raised");
                break;
            case ["classes"]:
                PrintClasses(null);
                break;
            case ["classes", var unitId]:
                if (_record.Find(unitId) is { } candidate)
                {
                    PrintClasses(candidate);
                }
                else
                {
                    Error(text, $"no unit '{unitId}' on the roster");
                }

                break;
            case ["certify", var unitId, var classId]:
                Take(_record.Certify(unitId, classId, _content), text);
                break;
            case ["keep"]:
                if (_record.KeepMenuRefusal(_content) is { } closed)
                {
                    if (_content.Campaign.Keep.Rooms.Count > 0 && !_record.IsFinished(_content))
                    {
                        Lines(RoomLines(_record, _content));
                        _out.WriteLine("Walls and ditches: " + UnitNames.Sentence(closed));
                    }
                    else
                    {
                        Error(text, closed);
                    }
                }
                else
                {
                    Lines(RoomLines(_record, _content));
                    PrintKeep();
                }

                break;
            case ["hire"]:
                Lines(HireLines(_record, _content, always: true));
                break;
            case ["hire", var hireId]:
                Take(_record.Hire(hireId, _content), text);
                break;
            case ["pick", var claimant]:
                Take(_record.PickClaimant(claimant, _content), text);
                break;
            case ["meet", var side]:
                Take(_record.Meet(side, _content), text);
                break;
            case ["build", var roomId] when _content.Campaign.Keep.Edit(roomId) is null:
                Take(_record.BuildRoom(roomId, _content), text);
                break;
            case ["build", var editId, var atText] when TryParseCoord(atText, out var at):
                try
                {
                    Take(_record.Build(editId, at, LoadBareKeep(), _content), text);
                }
                catch (MapException e)
                {
                    Error(text, e.Message);
                }

                break;
            case ["build", ..]:
                Error(text, "usage: build <room>, or build <edit> <x,y>");
                break;
            case ["bench", var unitId]:
                if (Take(_record.Bench(unitId, map, _content), text))
                {
                    PrintDeployment(map);
                }

                break;
            case ["unbench", var unitId]:
                if (Take(_record.Unbench(unitId), text))
                {
                    PrintDeployment(map);
                }

                break;
            case ["record"]:
                _out.WriteLine(ProtocolJson.Campaign(_record));
                break;
            case ["save", var name]:
                if (_saves is null)
                {
                    Error(text, SavesOff);
                }
                else if (_saves.Save(name, _record) is { } refused)
                {
                    Error(text, refused);
                }
                else
                {
                    _out.WriteLine($"Saved as {name}: {SaveLine(_record, _content)}");
                }

                break;
            case ["save", ..]:
                Error(text, "usage: save <name>");
                break;
            case ["saves"]:
                PrintSaves();
                break;
            case ["help"]:
                _out.WriteLine(Help);
                break;
            case ["buy", ..]:
                Error(text, "usage: buy <item> <unit>");
                break;
            case ["repair", ..]:
                Error(text, "usage: repair <unit> <slot>");
                break;
            case ["drop", ..]:
                Error(text, "usage: drop <unit> <slot>");
                break;
            case ["take", ..]:
                Error(text, "usage: take <unit> <n>");
                break;
            case ["refine", ..]:
                Error(text, "usage: refine <unit> <slot> mt|hit");
                break;
            case ["certify", ..]:
                Error(text, "usage: certify <unit> <class>");
                break;
            case ["trial", ..]:
                Error(text, "usage: trial <unit> <class>");
                break;
            case ["quest", ..]:
                Error(text, "usage: quest <id> <ally> [<ally>...]");
                break;
            case ["bench" or "unbench" or "show" or "classes", ..]:
                Error(text, $"usage: {words[0]} <unit>");
                break;
            default:
                Error(text, $"unknown command '{words[0]}' between maps; type help");
                break;
        }
    }

    private const string SavesOff = "saves are off: a scripted run keeps none unless --saves names a directory";

    /// <summary>
    /// The card a lost map plays before the fail menu (issue 663): a placeholder, marked as one, until
    /// the storyline's bad ending is written (issue 656).
    /// </summary>
    public static readonly IReadOnlyList<string> LostCard = Card("-- The company is lost (placeholder card) --", ValueList<string>.From(new[]
    {
        "The levy breaks on the field. The ones who can walk go home by the back roads, and nobody writes down which of them were yours.",
    }));

    /// <summary>The fail menu's prompt: the three ways on from a lost map.</summary>
    public const string FailMenuLine = "Load from save (load <name>; saves lists them), New game (new), or Quit (quit)";

    /// <summary>
    /// The difficulty and the permadeath toggle as the record carries them (issue 664):
    /// <c>difficulty Captain, permadeath on</c>, the difficulty by its display name.
    /// </summary>
    public static string RulesLine(CampaignRecord record, GameContent content)
    {
        string Name(string id) => content.Difficulties.TryGetValue(id, out var difficulty) ? difficulty.DisplayName : id;
        var lowered = record.LoweredFrom.Count > 0 ? $" (lowered from {Name(record.LoweredFrom[0])})" : "";
        return $"difficulty {Name(record.Difficulty)}{lowered}, permadeath {(record.Permadeath ? "on" : "off")}";
    }

    /// <summary>
    /// The captain as the start chose them (issue 681): <c>Captain: Alder Fenn, from Sallow
    /// (she/her)</c>, the origin by its name; null when neither an origin nor a pronoun was chosen,
    /// so a campaign on the cast file's captain prints as it always has.
    /// </summary>
    public static string? CaptainLine(CampaignRecord record, GameContent content)
    {
        var captain = record.Captain(content);
        var origin = record.Origin is { } id ? content.Campaign.Origin(id) : null;
        if (captain is null || (origin is null && captain.Pronoun is null))
        {
            return null;
        }

        var from = origin is null ? "" : $", from {origin.Name}";
        var words = Referent.For(content, captain);
        var pronoun = words.Subject == captain.Id ? "" : $" ({words.Subject}/{words.Object})";
        return $"Captain: {captain.Name}{from}{pronoun}";
    }

    /// <summary>The difficulty a <c>--difficulty</c> value names: its id, or its display name in any case; null when none.</summary>
    public static Difficulty? DifficultyNamed(GameContent content, string value) =>
        content.Difficulties.TryGetValue(value, out var byId)
            ? byId
            : content.Difficulties.Values.FirstOrDefault(d => string.Equals(d.DisplayName, value, StringComparison.OrdinalIgnoreCase));

    private static string DifficultyChoice(Difficulty d) => d.Name is null ? d.Id : $"{d.Id} ({d.Name})";

    /// <summary>A save as the screen lists it: which map is next, of how many, and the purse.</summary>
    public static string SaveLine(CampaignRecord record, GameContent content) =>
        record.IsFinished(content)
            ? $"every map won, the purse holds {record.Purse}"
            : $"before map {record.MapIndex + 1} of {content.Campaign.Maps.Count}, {record.NextMap(content).MapId}, the purse holds {record.Purse}";

    private void PrintSaves()
    {
        if (_saves is null)
        {
            _out.WriteLine("Saves: off; " + SavesOff);
            return;
        }

        var names = _saves.Names();
        _out.WriteLine(names.Count == 0 ? $"Saves in {_saves.Directory}: none" : $"Saves in {_saves.Directory}:");
        foreach (var name in names)
        {
            var (record, refusal) = _saves.Load(name, _content);
            _out.WriteLine($"  {name}: {(record is null ? refusal : SaveLine(record, _content))}");
        }
    }

    /// <summary>
    /// After a lost map (issue 663): the placeholder card, then the fail menu until a save is loaded
    /// or a new game is started (the record to go on with), or quit, the input ending or a strict
    /// stop (null; <see cref="_strictStopped"/> says which).
    /// </summary>
    private CampaignRecord? FailMenu(TextReader input, bool strict)
    {
        Lines(LostCard);
        _out.WriteLine(FailMenuLine);
        while (input.ReadLine() is { } line)
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

            _commands++;
            switch (text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                case ["quit"]:
                    return null;
                case ["new"]:
                    var fresh = CampaignRecord.Start(_content, _record.Seed, _record.Difficulty, _record.Permadeath, _record.Origin, _record.Captain(_content)?.Pronoun);
                    WriteEvent($"New game, seed {fresh.Seed}, {RulesLine(fresh, _content)}");
                    return fresh;
                case ["saves"]:
                    PrintSaves();
                    break;
                case ["load", var name] when _saves is null:
                    Error(text, SavesOff);
                    break;
                case ["load", var name]:
                    var (loaded, refusal) = _saves.Load(name, _content);
                    if (loaded is not null)
                    {
                        WriteEvent($"Loaded {name}: {SaveLine(loaded, _content)}");
                        return loaded;
                    }

                    Error(text, refusal!);
                    break;
                case ["load", ..]:
                    Error(text, "usage: load <name>");
                    break;
                default:
                    Error(text, "the campaign is lost; " + FailMenuLine);
                    break;
            }

            if (strict && _rejections.Count > 0)
            {
                _out.WriteLine($"Strict: stopped at line {_line} ({text}); no later command applied");
                _strictStopped = true;
                return null;
            }
        }

        return null;
    }

    private bool Take(ScreenResult result, string text)
    {
        if (!result.Accepted)
        {
            Error(text, result.Text);
            return false;
        }

        _record = result.Record;
        WriteEvent(Text(_record, _content, result.Text));
        return true;
    }

    private void Error(string command, string message)
    {
        message = Text(_record, _content, message);
        _out.WriteLine("ERROR: " + message);
        if (_scripted)
        {
            _rejections.Add((_line, command, message));
        }
    }

    /// <summary>
    /// What an advanced form adds over <paramref name="basis"/> (issue 704), in a fixed order: the weapon
    /// types its base lacks (a strike-only one says so), the Mov it gains, then the class abilities its base
    /// does not hold, by name; <c>numbers only</c> when it adds none of them.
    /// </summary>
    private string Adds(UnitClass form, UnitClass basis)
    {
        var parts = form.Weapons.Where(w => !basis.CanUse(w))
            .Select(w => form.StrikeOnly.Contains(w) ? $"{w.Label()} (strike spells only)" : w.Label())
            .ToList();
        if (form.Movement != basis.Movement)
        {
            parts.Insert(0, form.Movement.ToString().ToLowerInvariant());
        }

        if (form.Mov > basis.Mov)
        {
            parts.Add($"+{form.Mov - basis.Mov} Mov");
        }

        parts.AddRange(form.Abilities.Where(a => !basis.Abilities.Contains(a)).Select(a => _content.Ability(a).Name));
        return parts.Count == 0 ? "numbers only" : string.Join(", ", parts);
    }

    /// <summary>
    /// Every class with what it asks (issue 72) and whether a trial stands in for its seal, and
    /// for <paramref name="unit"/>, what it still lacks for each, read against its own stats.
    /// </summary>
    private void PrintClasses(Unit? unit)
    {
        var advanced = _content.Classes.Values.Any(c => c.Advances is not null) ? $", {_content.Campaign.AdvancedCertificationPrice} for an advanced form" : "";
        _out.WriteLine($"Classes: what each asks, read against a unit's own stats without its class's; a seal costs {_content.Campaign.CertificationPrice}{advanced}");
        var names = UnitNames.Of(_record, _content);
        foreach (var target in _content.Classes.Values.Where(c => (!c.Hidden || c.Id == unit?.ClassId) && (c.Unique is null || unit is null || c.Unique == unit.Id)))
        {
            var from = target.Advances is { } basis ? $" (from {basis.Name}; adds {Adds(target, basis)})" : "";
            if (target.Unique is { } owner)
            {
                from += $" [{names[owner]}'s other door; loses {target.Loses?.ToString().ToLowerInvariant()}]";
            }

            var line = $"  {target.Name}{from}: {target.Certification.Describe()}";
            if (_content.Campaign.TrialFor(target.Id) is not null)
            {
                line += "; or its trial in place of the seal";
            }

            if (unit is not null && unit.ClassId == target.Id)
            {
                line += $" -- {names[unit.Id]}'s class";
            }
            else if (unit is not null)
            {
                var refusals = Certifications.Check(unit, target, _content.Class(unit.ClassId), CampaignRecord.IsCaptain(unit, _content), _record.WonQuestIds);
                line += refusals.Count == 0 ? $" -- {names[unit.Id]} may be promoted" : $" -- {names.Named(string.Join("; ", refusals.Select(r => r.Text)))}";
            }

            _out.WriteLine(line);
            if (Drake.ClassLine(target, _content, unit) is { } drakeLine)
            {
                _out.WriteLine("    " + drakeLine);
            }
        }
    }

    /// <summary>
    /// The keep's menu (issue 288): what is built, then every edit with its price and, for each of
    /// its placements, what the edit does there in rules terms on the keep the record holds, or that
    /// it is built or refused.
    /// </summary>
    private void PrintKeep()
    {
        try
        {
            Lines(KeepLines(_contentDir, _content, _record));
        }
        catch (MapException e)
        {
            _out.WriteLine("ERROR: " + e.Message);
        }
    }

    /// <summary>The keep's menu as the screen prints it (see <see cref="PrintKeep"/>); throws <see cref="MapException"/> when the keep will not load.</summary>
    public static IReadOnlyList<string> KeepLines(string contentDir, GameContent content, CampaignRecord record)
    {
        var keep = record.KeepMap(BareKeep(contentDir, content), content);
        var lines = new List<string> { $"Keep: {keep.Name}; built: {(record.Keep.Count == 0 ? "nothing" : string.Join(", ", record.Keep))}; the purse holds {record.Purse}" };
        foreach (var edit in content.Campaign.Keep.Edits)
        {
            lines.Add($"  {edit.Id}: {edit.Name}, {edit.Price}");
            foreach (var at in edit.At)
            {
                lines.Add(record.Keep.Contains(new KeepWork(edit.Id, at))
                    ? $"    {edit.Id} {at}: built"
                    : Keep.Refusal(keep, edit, at) is { } refusal
                        ? $"    {edit.Id} {at}: {UnitNames.Sentence(refusal)}"
                        : "    " + UnitNames.Sentence(Keep.Describe(keep, edit, at, content)));
            }
        }

        return lines;
    }

    /// <summary>
    /// The keep's rooms as the camp screen prints them (issue 687, DESIGN section 13.20): the
    /// beds held and the beds the keep has, then each room with its price, the beds it adds and
    /// how many of it are built; empty when the keep sells no rooms or the campaign is finished.
    /// </summary>
    public static IReadOnlyList<string> RoomLines(CampaignRecord record, GameContent content)
    {
        var keep = content.Campaign.Keep;
        if (keep.Rooms.Count == 0 || record.IsFinished(content) || record.Beds(content) is not { } beds)
        {
            return Array.Empty<string>();
        }

        var lines = new List<string> { $"Rooms: beds: {record.BedsTaken}/{beds}; a fallen member keeps their bed" };
        foreach (var room in keep.Rooms)
        {
            var does = room.Forge ? $"Refine +{content.Campaign.Forge.Hit} acc or +{content.Campaign.Forge.Mt} power a step" : $"+{room.Beds} {(room.Beds == 1 ? "bed" : "beds")}";
            if (room.Hires.Count > 0)
            {
                does += $" and {room.Hires.Count} {(room.Hires.Count == 1 ? "hire" : "hires")} at {keep.HirePrice}";
            }

            var opens = room.After.Length > 0 && content.Campaign.Maps.ToList().FindIndex(m => m.MapId == room.After) >= record.MapIndex ? $"; opens once {room.After} is won" : "";
            if (room.Requires.Length > 0 && !record.Rooms.Contains(room.Requires))
            {
                opens += $"; needs the {keep.Room(room.Requires)?.Name ?? room.Requires} first";
            }

            lines.Add($"  {room.Id}: {room.Name}, {room.Price}, {does}, built {record.Rooms.Count(id => id == room.Id)} of {room.Max}{opens}");
        }

        if (keep.Rooms.Any(r => r.Forge))
        {
            var forge = content.Campaign.Forge;
            lines.Add($"  Stores: common {record.CommonMaterial}, frozen iron {record.RareMaterial}; a step costs one and {forge.Price}, shop weapons to +{forge.CommonSteps} on common, the main line's signatures to +{forge.RareSteps} on frozen iron");
        }

        lines.AddRange(HireLines(record, content));

        return lines;
    }

    /// <summary>
    /// The barracks' list as the Keep panel prints it (issue 690): a heading with the price and the
    /// level a hire joins at now, then two lines per hire on offer, the card that would join
    /// (name, pronoun, class, level, items, and the stats and growths with the class's modifiers, as <c>show</c> and a level-up read them, printed whole, pillar 2) and the
    /// hire's one line. Empty while no room that hires is built, unless <paramref name="always"/>,
    /// when the heading says why the list is empty.
    /// </summary>
    public static IReadOnlyList<string> HireLines(CampaignRecord record, GameContent content, bool always = false)
    {
        var keep = content.Campaign.Keep;
        var hiring = keep.Rooms.Where(r => r.Hires.Count > 0).ToList();
        var built = hiring.Any(r => record.Rooms.Contains(r.Id));
        if (!built || record.IsFinished(content))
        {
            if (!always)
            {
                return Array.Empty<string>();
            }

            return new[] { hiring.Count == 0 || record.IsFinished(content) ? "Hires: " + (record.HireRefusal("", content) ?? "none") : $"Hires: none until the {hiring[0].Name} is built (build {hiring[0].Id})" };
        }

        var level = Barracks.JoinLevel(record);
        var offered = record.HiresOffered(content);
        var lines = new List<string> { $"Hires: {keep.HirePrice} each, joining at L{level}, the company's average less {Barracks.LevelsBelow}; hire <id>" + (offered.Count == 0 ? "; nobody is left to hire" : "") };
        foreach (var hire in offered)
        {
            var unit = Barracks.Recruit(hire, level, content);
            var items = string.Join(", ", unit.Inventory.Items.Select(i => content.ItemName(i.ItemId)));
            lines.Add($"  {hire.Id}: {hire.Name} ({hire.Pronoun.ToString().ToLowerInvariant()}), {content.Class(hire.ClassId).Name} L{unit.Level}, {items}; {StatText(unit.EffectiveStats(content.Class(hire.ClassId)))}; growths {StatText(unit.EffectiveGrowths(content.Class(hire.ClassId)))}");
            lines.Add($"    {hire.Line}");
        }

        return lines;
    }

    private static string StatText(Stats stats) =>
        string.Join(" ", Stats.All.Select(s => $"{s.ToString().ToLowerInvariant()} {stats.Get(s)}"));

    /// <summary>
    /// The warning on leaving the camp (issue 647): <c>low: &lt;name&gt;'s &lt;weapon&gt; has &lt;n&gt; uses</c>
    /// for each unit going to the next map whose equipped weapon has fewer than
    /// <see cref="CampaignRecord.LowUses"/>; then, for a hungering weapon in its starved form
    /// (issue 856), <c>starved: &lt;name&gt;'s &lt;weapon&gt; at half power until it lands a hit</c>
    /// in its place. It warns and never refuses.
    /// </summary>
    public static IReadOnlyList<string> LowLines(CampaignRecord record, GameContent content) =>
        record.LowWeapons(content).Select(l => $"low: {l.Unit.Name}'s {l.Weapon.Name} has {l.Uses} {(l.Uses == 1 ? "use" : "uses")}")
            .Concat(record.StarvedWeapons(content).Select(s => $"starved: {s.Unit.Name}'s {s.Weapon.Name} at half power until it lands a hit"))
            .ToList();

    /// <summary>
    /// One line per arrival of the next map who will not join, printed where they are met: when the
    /// company is at its cap (issue 689), <c>company full (12): &lt;name&gt; will not join</c>; else,
    /// for want of a bed (issue 687), <c>no bed free: &lt;name&gt; will not join</c>.
    /// </summary>
    public static IReadOnlyList<string> TurnedAwayLines(CampaignRecord record, GameContent content)
    {
        var names = UnitNames.Of(record, content);
        var why = record.CompanyFull(content) ? $"company full ({CampaignRecord.CompanyCap})" : "no bed free";
        return record.TurnedAway(content).Select(id => $"{why}: {names[id]} will not join").ToList();
    }

    /// <summary>
    /// One line per recruit joining at the next map below the living company's median (issue 763),
    /// printed where they are met: <c>&lt;name&gt; joins at level N (the company's median)</c>.
    /// </summary>
    public static IReadOnlyList<string> JoinLines(CampaignRecord record, GameContent content)
    {
        var names = UnitNames.Of(record, content);
        return record.RaisedOnJoining(content).Select(j => $"{names[j.Id]} joins at level {j.Level} (the company's median)").ToList();
    }

    private void Lines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            _out.WriteLine(line);
        }
    }

    private static bool TryParseCoord(string text, out Coord at)
    {
        var parts = text.Split(',');
        if (parts.Length == 2 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y))
        {
            at = new Coord(x, y);
            return true;
        }

        at = default;
        return false;
    }

    private void PrintRoster() => Lines(RosterLines(_record, _content, typed: true));

    private void PrintUnit(Unit unit, bool detail) => Lines(UnitLines(_record, _content, unit, detail, typed: true));

    private void PrintShop() => Lines(ShopLines(_record, _content));

    private void PrintDeployment(MapDefinition map)
    {
        try
        {
            _out.WriteLine(DeploymentLine(_record, _content, map));
        }
        catch (ArgumentException e)
        {
            _out.WriteLine("ERROR: " + e.Message);
        }
    }

    /// <summary>
    /// The roster as the screen prints it: a heading with the living count against the company's
    /// cap (issue 689, <c>Roster: company 7/12</c>), one row per unit, then the fallen if any,
    /// everyone <see cref="CampaignRecord.Present"/> for the next map counted and listed, the members
    /// first and those joining at this camp after them, so a joiner, the pick or a side character
    /// met here is on it before the march (issue 842),
    /// each by name (issue 615); with <paramref name="typed"/>, as the console prints it, a name a
    /// command types differently is followed by that id: <c>Alder Fenn (captain)</c>.
    /// </summary>
    public static IReadOnlyList<string> RosterLines(CampaignRecord record, GameContent content, bool typed = false)
    {
        var joining = record.Present(content).Where(u => record.Find(u.Id) is null).ToList();
        var lines = new List<string> { $"Roster: company {record.Living + joining.Count}/{CampaignRecord.CompanyCap}" };
        foreach (var unit in record.Roster.Concat(joining))
        {
            lines.AddRange(UnitLines(record, content, unit, detail: false, typed));
        }

        if (SupportsLine(record, content) is { } supports)
        {
            lines.Add(supports);
        }

        if (record.Fallen.Count > 0)
        {
            var names = UnitNames.Of(record, content);
            lines.Add($"  Fallen: {string.Join(", ", record.Fallen.Select(id => FallenEntry(record, content, names, id)))}");
        }

        return lines;
    }

    /// <summary>
    /// One of the camp's fallen (issues 678 and 805): the name, then in brackets the board they fell on
    /// and, for the campaign's drake rider once <see cref="CampaignRecord.DrakeFlew"/> is set, that the
    /// drake flew: <c>Rook (fell on The Rookery; the drake flew, grown)</c>.
    /// </summary>
    private static string FallenEntry(CampaignRecord record, GameContent content, UnitNames names, string id)
    {
        var notes = new List<string>();
        if (record.FellOnMap(id) is { } board)
        {
            notes.Add($"fell on {board}");
        }

        if (record.DrakeFlew is { } flew && content.Campaign.Drake?.Member == id)
        {
            notes.Add($"the drake flew, {Drake.Word(flew)}");
        }

        return notes.Count == 0 ? names[id] : $"{names[id]} ({string.Join("; ", notes)})";
    }

    /// <summary>
    /// The roster's supports line (issue 77): each support pair of two living members whose rapport
    /// on the record reaches a tier, in the campaign file's order, by name with the tier, as
    /// <c>  Supports: Alder Fenn and Pell C, Maud and Pell B</c>; null when no pair stands at C.
    /// </summary>
    public static string? SupportsLine(CampaignRecord record, GameContent content)
    {
        var names = UnitNames.Of(record, content);
        var reached = content.Campaign.Supports
            .Where(p => record.Find(p.A) is not null && record.Find(p.B) is not null)
            .Select(p => (Pair: p, Tier: Supports.TierOf(content, p.A, p.B, record.RapportOf(p.A, p.B))))
            .Where(t => t.Tier is not null)
            .Select(t => $"{names[t.Pair.A]} and {names[t.Pair.B]} {t.Tier!.Name}")
            .ToList();
        return reached.Count == 0 ? null : "  Supports: " + string.Join(", ", reached);
    }

    /// <summary>
    /// One unit's roster row: its name, class, level, EXP, whether it is benched, and each item
    /// with its uses; with <paramref name="detail"/>, its stats, ranks and mastery below, as
    /// <c>show</c> prints them; with <paramref name="typed"/>, the id a command types after the
    /// name where the two differ.
    /// </summary>
    public static IReadOnlyList<string> UnitLines(CampaignRecord record, GameContent content, Unit unit, bool detail, bool typed = false)
    {
        var name = typed && !string.Equals(unit.Name, unit.Id, StringComparison.OrdinalIgnoreCase) ? $"{unit.Name} ({unit.Id})" : unit.Name;
        var unitClass = content.Class(unit.ClassId);
        var slots = unit.Inventory.Items.Select((item, slot) => $"{slot + 1}: {ItemText(content, item)}");
        var bench = (unit.Wound is { } wound ? ", " + wound.Label : "") + (record.Benched.Contains(unit.Id) ? ", benched" : "");
        var ladder = CampaignRecord.IsCaptain(unit, content) && LadderText(content, unitClass) is { } text ? "; " + text : "";
        var lines = new List<string> { $"  {name}: {unitClass.Name} L{unit.Level}, EXP {unit.Exp}{bench}; {(unit.Inventory.Count == 0 ? "no items" : string.Join(", ", slots))}{ladder}" };
        if (!detail)
        {
            return lines;
        }

        var stats = content.StatsOf(unit);
        if (unit.Wound is { } open)
        {
            lines.Add("    " + PlaySession.WoundLine(open));
        }

        lines.Add($"    HP {stats.Hp}  Str {stats.Str} Mag {stats.Mag} Dex {stats.Dex} Spd {stats.Spd} Lck {stats.Lck} Def {stats.Def} Res {stats.Res} Cha {stats.Cha}");
        var ranks = unitClass.Weapons.Select(type => $"{type.ToString().ToLowerInvariant()} {unit.Skill.Rank(type)} ({unit.Skill.Points(type)})");
        lines.Add($"    Ranks: {string.Join(", ", ranks)}");
        if (PlaySession.MasteryLine(new BattleUnit(unit, Side.Player, default, stats.Hp, false, false), content) is { } mastery)
        {
            lines.Add("  " + UnitNames.Sentence(mastery));
        }

        return lines;
    }

    /// <summary>
    /// The captain's ladder as the camp row ends it (issue 705): before the captain takes a class on it,
    /// the level and the choices, <c>promotes at 3: Marshal, Ranger or Vanguard</c>; after, the class
    /// held and the form above it, <c>ladder: Vanguard, then Champion at 10</c>. Null when the content has no ladder.
    /// </summary>
    public static string? LadderText(GameContent content, UnitClass held)
    {
        var bases = content.Classes.Values.Where(c => c.Captain && c.Advances is null).ToList();
        if (bases.Count == 0)
        {
            return null;
        }

        if (!held.Captain)
        {
            var names = bases.Select(c => c.Name).ToList();
            var choices = names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} or {names[^1]}";
            return $"promotes at {bases.Min(c => c.Certification.Level)}: {choices}";
        }

        var basis = held.Advances ?? held;
        var form = content.Classes.Values.FirstOrDefault(c => c.Advances?.Id == basis.Id && c.Unique is null);
        return form is null ? $"ladder: {basis.Name}" : $"ladder: {basis.Name}, then {form.Name} at {form.Certification.Level}";
    }

    /// <summary>An inventory entry with its uses out of its full uses, and what repairing it costs where it can be repaired and is short.</summary>
    private static string ItemText(GameContent content, ItemStack stack)
    {
        if (!content.Weapons.TryGetValue(stack.ItemId, out var weapon))
        {
            var item = content.Item(stack.ItemId);
            return $"{item.Name} {stack.Uses}/{item.Uses}";
        }

        var text = $"{Forge.Name(weapon.Name, stack)}{Keepsake.Suffix(stack, content)} {stack.Uses}/{weapon.Durability}";
        if (stack.Uses < weapon.Durability && CampaignRules.RepairPricePerUse(weapon) is { } perUse)
        {
            text += $" (repair {(weapon.Durability - stack.Uses) * perUse})";
        }

        return text;
    }

    /// <summary>The ids the shop sells before the next map, in its stock order; each is what <c>buy</c> takes.</summary>
    public static IReadOnlyList<string> Stock(CampaignRecord record, GameContent content) => record.NextMap(content).Stock.ToList();

    /// <summary>One ware as the shop line names it: the id <c>buy</c> takes, then its price.</summary>
    public static string WareText(GameContent content, string id) =>
        content.Weapons.TryGetValue(id, out var weapon) ? $"{id} {weapon.Price}" : $"{id} {content.Item(id).Price}";

    /// <summary>The shop as the screen prints it: each ware and its price and the seal's, then the trials if any.</summary>
    public static IReadOnlyList<string> ShopLines(CampaignRecord record, GameContent content)
    {
        var wares = Stock(record, content).Select(id => WareText(content, id));
        var lines = new List<string> { $"Shop: {string.Join(", ", wares)}; a seal for promotion costs {content.Campaign.CertificationPrice}" };
        if (content.Campaign.Trials.Count > 0)
        {
            var trials = content.Campaign.Trials.Select(t => content.Class(t.ClassId).Name);
            lines.Add($"Trials in place of a seal (one attempt per unit and class before each map): {string.Join(", ", trials)}");
        }

        return lines;
    }

    /// <summary>
    /// Who deploys to <paramref name="map"/>, in slot order, by name, then how many of the living
    /// members present field (issue 689): <c>(deploy 2 of 3)</c>, or on <c>deploy: all</c>
    /// <c>the whole company fights: 11;</c> before the names. A map fought short-handed (issue 795)
    /// adds how many of its slots stand empty: <c>(deploy 2 of 2; 4 slots stand empty)</c>. Throws
    /// <see cref="ArgumentException"/> as <see cref="CampaignRecord.Deployment"/> does.
    /// </summary>
    public static string DeploymentLine(CampaignRecord record, GameContent content, MapDefinition map)
    {
        var names = UnitNames.Of(record, content);
        var deployed = record.Deployment(map, content);
        var who = string.Join(", ", deployed.Select(id => names[id]));
        var empty = map.Placements.OfType<PlayerPlacement>().Count() - deployed.Count;
        var standEmpty = empty > 0 ? $"; {empty} {(empty == 1 ? "slot stands" : "slots stand")} empty" : "";
        return map.DeploysAll
            ? $"Deploys to {map.Name}: the whole company fights: {deployed.Count}; {who}"
            : $"Deploys to {map.Name}: {who} (deploy {deployed.Count} of {record.Present(content).Count}{standEmpty})";
    }

    /// <summary>The width a text card's paragraphs are wrapped to (issue 631).</summary>
    public const int CardWidth = 72;

    /// <summary>
    /// The text card printed before the screen of <paramref name="record"/>'s next map (issue 631,
    /// DESIGN section 14): a heading naming the map, then each paragraph wrapped to
    /// <see cref="CardWidth"/>, a blank line between paragraphs and after the last. Empty for a map
    /// whose <c>campaign.json</c> entry has no <c>before</c>.
    /// </summary>
    public static IReadOnlyList<string> BeforeCard(CampaignRecord record, GameContent content, MapDefinition map) =>
        Card($"-- {map.Name} --", record.NextMap(content).Before);

    /// <summary>
    /// The text card printed after <paramref name="map"/> is won, <paramref name="before"/> being the
    /// record the battle began from (issue 631); shaped as <see cref="BeforeCard"/>, empty for a map
    /// with no <c>after</c>. It is screen text, not an event, so the event log leaves it out.
    /// </summary>
    public static IReadOnlyList<string> AfterCard(CampaignRecord before, GameContent content, MapDefinition map) =>
        Card($"-- After {map.Name} --", before.NextMap(content).After);

    /// <summary>The card printed before side map <paramref name="questId"/> plays on <paramref name="map"/> (issue 635), shaped as <see cref="BeforeCard"/>.</summary>
    public static IReadOnlyList<string> QuestBeforeCard(GameContent content, MapDefinition map, string questId) =>
        Card($"-- {map.Name} --", content.Campaign.Quest(questId)!.Before);

    /// <summary>The card printed once side map <paramref name="questId"/> is won on <paramref name="map"/> (issue 635), shaped as <see cref="BeforeCard"/>.</summary>
    public static IReadOnlyList<string> QuestAfterCard(GameContent content, MapDefinition map, string questId) =>
        Card($"-- After {map.Name} --", content.Campaign.Quest(questId)!.After);

    /// <summary>
    /// The scenes that play at <paramref name="point"/> of campaign map <paramref name="mapId"/>, named <paramref name="mapName"/> (issue 1001), shown
    /// against <paramref name="record"/>: per scene with a line to show, the heading a card at that
    /// point carries, then each shown line wrapped to <see cref="CardWidth"/>, a spoken one after its
    /// speaker's name, and a blank line after the last. Empty when nothing plays there. Screen text,
    /// as a card is, so the event log leaves it out.
    /// </summary>
    public static IReadOnlyList<string> SceneLines(CampaignRecord record, GameContent content, ScenePoint point, string mapId, string mapName)
    {
        var names = UnitNames.Of(record, content);
        var lines = new List<string>();
        foreach (var scene in SceneScripts.At(content, point, mapId))
        {
            var shown = SceneScripts.Shown(scene, record, content);
            if (shown.Count == 0)
            {
                continue;
            }

            lines.Add(point == ScenePoint.After ? $"-- After {mapName} --" : $"-- {mapName} --");
            foreach (var line in shown)
            {
                lines.AddRange(Wrap(line.Speaker == SceneScripts.Narration ? line.Text : $"{SpeakerName(names, content, line.Speaker)}: {line.Text}", CardWidth));
            }

            lines.Add("");
        }

        return lines;
    }

    /// <summary>A scene speaker's name: the record's name for a cast member or hire, else the unit template's.</summary>
    private static string SpeakerName(UnitNames names, GameContent content, string speaker) =>
        names[speaker] is var name && name != speaker ? name : content.Units.TryGetValue(speaker, out var unit) ? unit.Name : speaker;

    private static IReadOnlyList<string> Card(string heading, ValueList<string> paragraphs)
    {
        if (paragraphs.Count == 0)
        {
            return Array.Empty<string>();
        }

        var lines = new List<string> { heading };
        foreach (var paragraph in paragraphs)
        {
            lines.AddRange(Wrap(paragraph, CardWidth));
            lines.Add("");
        }

        return lines;
    }

    /// <summary><paramref name="text"/> broken at spaces into lines of at most <paramref name="width"/> characters; a longer word stands on its own line.</summary>
    public static IReadOnlyList<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                lines.Add(line);
                line = "";
            }

            line = line.Length == 0 ? word : line + " " + word;
        }

        if (line.Length > 0)
        {
            lines.Add(line);
        }

        return lines;
    }

    /// <summary>The heading the screen opens with before <paramref name="map"/>.</summary>
    public static string ScreenHeading(CampaignRecord record, GameContent content, MapDefinition map) =>
        $"-- Before map {record.MapIndex + 1} of {content.Campaign.Maps.Count}: {map.Name}; the purse holds {record.Purse} --";
}

/// <summary>A writer that drops what it is given while <see cref="Quiet"/>, so a resumed battle replays unseen (issue 663).</summary>
internal sealed class QuietWriter : TextWriter
{
    private readonly TextWriter _inner;

    public QuietWriter(TextWriter inner)
    {
        _inner = inner;
        NewLine = inner.NewLine;
    }

    public bool Quiet { get; set; }

    public override System.Text.Encoding Encoding => _inner.Encoding;

    public override void Write(char value)
    {
        if (!Quiet)
        {
            _inner.Write(value);
        }
    }

    public override void Write(string? value)
    {
        if (!Quiet)
        {
            _inner.Write(value);
        }
    }
}

/// <summary>A reader that keeps every line it hands on, after the lines a resumed battle replayed, so a quit can write them as the suspend (issue 663).</summary>
internal sealed class RecordingReader : TextReader
{
    private readonly TextReader _inner;
    private readonly List<string> _lines;

    public RecordingReader(TextReader inner, IEnumerable<string>? replayed)
    {
        _inner = inner;
        _lines = replayed?.ToList() ?? new List<string>();
    }

    /// <summary>Every line read, the replayed ones first.</summary>
    public IReadOnlyList<string> Lines => _lines;

    public override string? ReadLine()
    {
        var line = _inner.ReadLine();
        if (line is not null)
        {
            _lines.Add(line);
        }

        return line;
    }
}
