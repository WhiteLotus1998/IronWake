using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core;

namespace Ironwake.Cli;

/// <summary>
/// <c>ironwake campaign</c> (issue 74, DESIGN section 9): the maps of <c>campaign.json</c> in
/// order, each preceded by the between-map screen, text only. The screen reads the roster, the
/// shop and the next map's deployment, and takes the actions of <see cref="CampaignRecord"/>:
/// buy, repair, certify, trial, build (the keep's menu, after the raid on it; issue 288), bench and unbench; <c>march</c> starts the battle, which plays as
/// <c>play</c> does until <c>leave</c> after it is decided. A won battle returns to the screen
/// with the reward paid and the fallen gone; a lost one ends the campaign. The same script
/// grammar and <c>--strict</c> as <c>play</c>, one script for the whole campaign.
/// </summary>
public sealed class CampaignSession
{
    public const string Usage = "usage: ironwake campaign [--seed N] [--script file] [--strict] [--content dir] [--difficulty id] [--scheme one|two] [--from map] [--log file]";

    private const string Help = """
        Between maps:
          roster                   Every unit: class, level, EXP, items with uses, mastery
          show <unit>              One unit's numbers, ranks and mastery
          shop                     What the shop sells before this map, and each price
          buy <item> <unit>        Buy an item at full uses into the unit's next free slot
          repair <unit> <slot>     Restore a weapon's uses, at its price per use
          classes [unit]           What each class asks to certify into it, and what the unit still lacks
          certify <unit> <class>   Change class, paying a seal from the purse
          trial <unit> <class>     Try the class's certification trial instead of a seal; one attempt per camp
          keep                     The keep's menu once the raid is fought: each placement, its price and what it does
          build <edit> <x,y>       Buy one edit of the keep's menu at one of its placements
          bench <unit>             Keep a unit off the next map; the next in roster order fills its slot
          unbench <unit>           Return a benched unit to the deployment order
          record                   The campaign record as one JSON line (the protocol's campaign shape)
          march                    Start the next map
          help                     This list
        In battle, every play command; leave ends a battle once it is won or lost
        Slots count from 1
        """;

    private readonly GameContent _content;
    private readonly string _contentDir;
    private readonly TextWriter _out;
    private readonly bool _scripted;
    private readonly RollScheme _scheme;
    private readonly List<(int Line, string Command, string Reason)> _rejections = new();
    private CampaignRecord _record;
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

    private CampaignSession(GameContent content, string contentDir, CampaignRecord record, TextWriter output, bool scripted, RollScheme scheme)
    {
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

        if (content.Difficulties.Count > 0 && !content.Difficulties.ContainsKey(difficulty))
        {
            Console.WriteLine($"ERROR: no difficulty '{difficulty}'; the content declares {string.Join(", ", content.Difficulties.Keys)}");
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

        var record = from is null ? CampaignRecord.Start(content, seed, difficulty) : CampaignRecord.StartAt(content, seed, from, difficulty);
        var session = new CampaignSession(content, contentDir, record, Console.Out, script is not null, scheme);
        var code = session.Play(input, strict);
        if (log is not null)
        {
            File.WriteAllText(log, session.EventLog);
        }

        return code;
    }

    private int Play(TextReader input, bool strict)
    {
        _out.WriteLine($"Campaign, seed {_record.Seed}, difficulty {_record.Difficulty}, scheme {_scheme}, {_content.Campaign.Maps.Count} maps");
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

            if (Screen(input, map, strict) != true)
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
            var battle = new PlaySession(_content, _record.Begin(map, _content, _scheme), _out, _scripted, _line);
            _out.WriteLine("Objective: " + Objective.Line(battle.State, _content));
            _out.Write(MapRenderer.Render(battle.State, _content));
            stopped = battle.RunCommands(input, strict, ref _commands);
            _line = battle.Line;
            _rejections.AddRange(battle.Rejections);
            _log.Write(battle.EventLog);
            if (stopped || !battle.Left)
            {
                _out.WriteLine($"Campaign stopped in {map.Name} at turn {battle.State.Turn}, {(battle.State.Outcome.IsOver ? "decided and not left" : "undecided")}");
                break;
            }

            if (battle.State.Outcome.Result != BattleResult.Won)
            {
                WriteEvent(LostLine(battle.State, _content));
                break;
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
        $"Certification trial: {UnitNames.Of(record, content)[unitId]} plays as {content.Class(classId).Name} with {string.Join(", ", trial.Certification!.Loadout)}",
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
    /// The between-map screen before <paramref name="map"/>: prints it, then applies commands until
    /// <c>march</c> (true) or the input ends or a strict stop (null).
    /// </summary>
    private bool? Screen(TextReader input, MapDefinition map, bool strict)
    {
        Lines(BeforeCard(_record, _content, map));
        _out.WriteLine(ScreenHeading(_record, _content, map));
        PrintRoster();
        PrintShop();
        PrintDeployment(map);
        if (_record.KeepMenuRefusal(_content) is null)
        {
            PrintKeep();
        }

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
                return true;
            }

            if (words is ["trial", var trialUnit, var trialClass])
            {
                if (!RunTrial(input, text, trialUnit, trialClass, strict))
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
                    Error(text, closed);
                }
                else
                {
                    PrintKeep();
                }

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
                Error(text, "usage: build <edit> <x,y>");
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
            case ["help"]:
                _out.WriteLine(Help);
                break;
            case ["buy", ..]:
                Error(text, "usage: buy <item> <unit>");
                break;
            case ["repair", ..]:
                Error(text, "usage: repair <unit> <slot>");
                break;
            case ["certify", ..]:
                Error(text, "usage: certify <unit> <class>");
                break;
            case ["trial", ..]:
                Error(text, "usage: trial <unit> <class>");
                break;
            case ["bench" or "unbench" or "show" or "classes", ..]:
                Error(text, $"usage: {words[0]} <unit>");
                break;
            default:
                Error(text, $"unknown command '{words[0]}' between maps; type help");
                break;
        }
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
                line += refusals.Count == 0 ? $" -- {names[unit.Id]} may certify" : $" -- {names.Named(string.Join("; ", refusals.Select(r => r.Text)))}";
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
    /// The roster as the screen prints it: a heading, one row per unit, then the fallen if any,
    /// each by name (issue 615); with <paramref name="typed"/>, as the console prints it, a name a
    /// command types differently is followed by that id: <c>Alder Fenn (captain)</c>.
    /// </summary>
    public static IReadOnlyList<string> RosterLines(CampaignRecord record, GameContent content, bool typed = false)
    {
        var lines = new List<string> { "Roster:" };
        foreach (var unit in record.Roster)
        {
            lines.AddRange(UnitLines(record, content, unit, detail: false, typed));
        }

        if (record.Fallen.Count > 0)
        {
            var names = UnitNames.Of(record, content);
            lines.Add($"  Fallen: {string.Join(", ", record.Fallen.Select(id => names[id]))}");
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
        var bench = record.Benched.Contains(unit.Id) ? ", benched" : "";
        var lines = new List<string> { $"  {name}: {unitClass.Name} L{unit.Level}, EXP {unit.Exp}{bench}; {(unit.Inventory.Count == 0 ? "no items" : string.Join(", ", slots))}" };
        if (!detail)
        {
            return lines;
        }

        var stats = content.StatsOf(unit);
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

        var text = $"{weapon.Name}{Keepsake.Suffix(stack, content)} {stack.Uses}/{weapon.Durability}";
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
        var lines = new List<string> { $"Shop: {string.Join(", ", wares)}; a seal to certify costs {content.Campaign.CertificationPrice}" };
        if (content.Campaign.Trials.Count > 0)
        {
            var trials = content.Campaign.Trials.Select(t => content.Class(t.ClassId).Name);
            lines.Add($"Trials in place of a seal (one attempt per unit and class before each map): {string.Join(", ", trials)}");
        }

        return lines;
    }

    /// <summary>Who deploys to <paramref name="map"/>, in slot order, by name; throws <see cref="ArgumentException"/> as <see cref="CampaignRecord.Deployment"/> does.</summary>
    public static string DeploymentLine(CampaignRecord record, GameContent content, MapDefinition map)
    {
        var names = UnitNames.Of(record, content);
        return $"Deploys to {map.Name}: {string.Join(", ", record.Deployment(map, content).Select(id => names[id]))}";
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
