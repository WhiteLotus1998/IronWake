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
    public const string Usage = "usage: ironwake campaign [--seed N] [--script file] [--strict] [--content dir] [--difficulty id] [--permadeath on|off] [--origin id] [--captain he|she] [--scheme one|two] [--from map] [--log file] [--saves dir] [--load save | --resume]";

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
          quest <id> <ally>        Fight a member's side map with one ally beside them; who falls there is gone for good
          keep                     The keep's rooms and beds; once the raid is fought, each wall placement, its price and what it does
          build <room>             Buy a room for the keep from the purse; each adds beds, and no bed free means a recruit will not join
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

    /// <summary>Parses the arguments after <c>campaign</c> and runs it: 0 when every map is won, 1 on a loss or an unfinished script, 2 for a usage error, 3 for a strict stop.</summary>
    public static int Run(string[] args)
    {
        ulong seed = 1;
        string? script = null;
        var strict = false;
        var contentDir = "content";
        var difficulty = CampaignRecord.NormalDifficulty;
        var scheme = RollScheme.TwoRollAverage;
        string? from = null;
        string? log = null;
        string? savesDir = null;
        string? load = null;
        var resume = false;
        var permadeath = true;
        string? origin = null;
        Pronoun? captain = null;
        for (var i = 0; i < args.Length; i++)
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

        if ((load is not null || resume) && (from is not null || (load is not null && resume)))
        {
            Console.WriteLine("ERROR: --load, --resume and --from each pick where the campaign starts; give one");
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

        var record = from is null ? CampaignRecord.Start(content, seed, difficulty, permadeath, origin, captain) : CampaignRecord.StartAt(content, seed, from, difficulty, permadeath, origin, captain);
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

        var session = new CampaignSession(content, contentDir, record, Console.Out, script is not null, scheme, saves, battleLines);
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
            Lines(AfterCard(before, _content, map));
        }

        if (_record.IsFinished(_content))
        {
            WriteEvent(CampaignWonLine(_record, _content));
            code = 0;
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
        $"Campaign lost on {end.Map.Name}: {UnitNames.Of(end, content).Named(end.Outcome.Reason)}";

    /// <summary>The line once every map is won.</summary>
    public static string CampaignWonLine(CampaignRecord record, GameContent content) =>
        $"Campaign won: all {content.Campaign.Maps.Count} maps, the purse holds {record.Purse}";

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
    public static (MapDefinition? Map, string? Refusal) QuestFor(string contentDir, GameContent content, CampaignRecord record, string questId, string allyId)
    {
        if (record.QuestRefusal(questId, allyId, content) is { } refusal)
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

        return CampaignRecord.QuestMapRefusal(map) is { } shape ? (null, shape) : (map, null);
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
        var lines = new List<string> { "Side maps (the member and one ally you pick, not the captain; a lost side map never ends the campaign):" };
        foreach (var quest in offered)
        {
            var closed = record.QuestsTried.Contains(quest.Id) ? "; fought since the last map, open again after the next" : "";
            lines.Add($"  {quest.Id}: {names[quest.MemberId]}'s quest {quest.Part}, {QuestBoard(contentDir, content, quest)} (quest {quest.Id} <ally>); {price}{closed}");
        }

        return lines;
    }

    /// <summary>A side map's board by its display name, or its id when the board will not load.</summary>
    private static string QuestBoard(string contentDir, GameContent content, CampaignQuest quest)
    {
        try
        {
            return MapFiles.Load(QuestPath(contentDir, quest), content).Name;
        }
        catch (MapException)
        {
            return quest.MapId;
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

        lines.AddRange(LowLines(record, content));
        return lines;
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
                paid.Add($"{quest.Rare} rare material");
            }

            lines.Add($"  {quest.Id}: {names[quest.MemberId]}'s quest {quest.Part}, {QuestBoard(contentDir, content, quest)}; paid {(paid.Count == 0 ? "nothing" : string.Join(" and ", paid))}");
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
    public static IReadOnlyList<string> QuestOpening(CampaignRecord record, GameContent content, MapDefinition map, string questId, string allyId)
    {
        var names = UnitNames.Of(record, content);
        var quest = content.Campaign.Quest(questId)!;
        return new[]
        {
            $"Side map: {map.Name}, seed {record.QuestSeed(questId, content)}",
            $"{names[quest.MemberId]} goes with {names[allyId]}. This map is lost if {names[quest.MemberId]} falls; who falls here is gone for good.",
        };
    }

    /// <summary>
    /// <c>quest &lt;id&gt; &lt;ally&gt;</c> (issue 635): refused as <see cref="QuestFor"/> says;
    /// otherwise the quest's card, then the side map plays on the screen with every <c>play</c>
    /// command until <c>leave</c>, its result applied through <see cref="CampaignRecord.AfterQuest"/>,
    /// and on a win its after card. False on a strict stop or when the input ends inside it.
    /// </summary>
    private bool RunQuest(TextReader input, string text, string questId, string allyId, bool strict)
    {
        var (map, refusal) = QuestFor(_contentDir, _content, _record, questId, allyId);
        if (map is null)
        {
            Error(text, refusal!);
            return true;
        }

        var quest = _content.Campaign.Quest(questId)!;
        Lines(Card($"-- {map.Name} --", quest.Before));
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
            Lines(Card($"-- After {map.Name} --", quest.After));
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
        Lines(TurnedAwayLines(_record, _content));
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
            if (words is ["march"])
            {
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
            else if (words is ["quest", var questId, var ally])
            {
                if (!RunQuest(input, text, questId, ally, strict))
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
                if (Take(_record.Bench(unitId, map), text))
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
                Error(text, "usage: quest <id> <ally>");
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
    /// Every class with what it asks (issue 72) and whether a trial stands in for its seal, and
    /// for <paramref name="unit"/>, what it still lacks for each, read against its own stats.
    /// </summary>
    private void PrintClasses(Unit? unit)
    {
        _out.WriteLine($"Classes: what each asks, read against a unit's own stats without its class's; a seal costs {_content.Campaign.CertificationPrice}");
        var names = UnitNames.Of(_record, _content);
        foreach (var target in _content.Classes.Values)
        {
            var line = $"  {target.Name}: {target.Certification.Describe()}";
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
                var refusals = Certifications.Check(unit, target);
                line += refusals.Count == 0 ? $" -- {names[unit.Id]} may be promoted" : $" -- {names.Named(string.Join("; ", refusals.Select(r => r.Text)))}";
            }

            _out.WriteLine(line);
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
            var does = room.Forge ? $"Refine +{content.Campaign.Forge.Mt} Mt or +{content.Campaign.Forge.Hit} hit a step" : $"+{room.Beds} {(room.Beds == 1 ? "bed" : "beds")}";
            var opens = room.After.Length > 0 && content.Campaign.Maps.ToList().FindIndex(m => m.MapId == room.After) >= record.MapIndex ? $"; opens once {room.After} is won" : "";
            lines.Add($"  {room.Id}: {room.Name}, {room.Price}, {does}, built {record.Rooms.Count(id => id == room.Id)} of {room.Max}{opens}");
        }

        if (keep.Rooms.Any(r => r.Forge))
        {
            var forge = content.Campaign.Forge;
            lines.Add($"  Stores: common {record.CommonMaterial}, rare {record.RareMaterial}; a step costs one and {forge.Price}, shop weapons to +{forge.CommonSteps} on common, the main line's signatures to +{forge.RareSteps} on rare");
        }

        return lines;
    }

    /// <summary>
    /// The warning on leaving the camp (issue 647): <c>low: &lt;name&gt;'s &lt;weapon&gt; has &lt;n&gt; uses</c>
    /// for each unit going to the next map whose equipped weapon has fewer than
    /// <see cref="CampaignRecord.LowUses"/>. It warns and never refuses.
    /// </summary>
    public static IReadOnlyList<string> LowLines(CampaignRecord record, GameContent content) =>
        record.LowWeapons(content).Select(l => $"low: {l.Unit.Name}'s {l.Weapon.Name} has {l.Uses} {(l.Uses == 1 ? "use" : "uses")}").ToList();

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
    /// each by name (issue 615); with <paramref name="typed"/>, as the console prints it, a name a
    /// command types differently is followed by that id: <c>Alder Fenn (captain)</c>.
    /// </summary>
    public static IReadOnlyList<string> RosterLines(CampaignRecord record, GameContent content, bool typed = false)
    {
        var lines = new List<string> { $"Roster: company {record.Living}/{CampaignRecord.CompanyCap}" };
        foreach (var unit in record.Roster)
        {
            lines.AddRange(UnitLines(record, content, unit, detail: false, typed));
        }

        if (record.Fallen.Count > 0)
        {
            var names = UnitNames.Of(record, content);
            lines.Add($"  Fallen: {string.Join(", ", record.Fallen.Select(id => record.FellOnMap(id) is { } board ? $"{names[id]} (fell on {board})" : names[id]))}");
        }

        return lines;
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
        var lines = new List<string> { $"  {name}: {unitClass.Name} L{unit.Level}, EXP {unit.Exp}{bench}; {(unit.Inventory.Count == 0 ? "no items" : string.Join(", ", slots))}" };
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
    /// <c>the whole company fights: 11;</c> before the names. Throws <see cref="ArgumentException"/>
    /// as <see cref="CampaignRecord.Deployment"/> does.
    /// </summary>
    public static string DeploymentLine(CampaignRecord record, GameContent content, MapDefinition map)
    {
        var names = UnitNames.Of(record, content);
        var deployed = record.Deployment(map, content);
        var who = string.Join(", ", deployed.Select(id => names[id]));
        return map.DeploysAll
            ? $"Deploys to {map.Name}: the whole company fights: {deployed.Count}; {who}"
            : $"Deploys to {map.Name}: {who} (deploy {deployed.Count} of {record.Present(content).Count})";
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
