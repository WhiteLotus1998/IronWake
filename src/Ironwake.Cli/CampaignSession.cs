using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core;

namespace Ironwake.Cli;

/// <summary>
/// <c>ironwake campaign</c> (issue 74, DESIGN section 9): the maps of <c>campaign.json</c> in
/// order, each preceded by the between-map screen, text only. The screen reads the roster, the
/// shop and the next map's deployment, and takes the actions of <see cref="CampaignRecord"/>:
/// buy, repair, certify, bench and unbench; <c>march</c> starts the battle, which plays as
/// <c>play</c> does until <c>leave</c> after it is decided. A won battle returns to the screen
/// with the reward paid and the fallen gone; a lost one ends the campaign. The same script
/// grammar and <c>--strict</c> as <c>play</c>, one script for the whole campaign.
/// </summary>
public sealed class CampaignSession
{
    public const string Usage = "usage: ironwake campaign [--seed N] [--script file] [--strict] [--content dir] [--difficulty id] [--scheme one|two]";

    private const string Help = """
        between maps:
          roster                   every unit: class, level, EXP, items with uses, mastery
          show <unit>              one unit's numbers, ranks and mastery
          shop                     what the shop sells before this map, and each price
          buy <item> <unit>        buy an item at full uses into the unit's next free slot
          repair <unit> <slot>     restore a weapon's uses, at its price per use
          certify <unit> <class>   change class, paying a seal from the purse
          bench <unit>             keep a unit off the next map; the next in roster order fills its slot
          unbench <unit>           return a benched unit to the deployment order
          record                   the campaign record as one JSON line (the protocol's campaign shape)
          march                    start the next map
          help                     this list
        in battle, every play command; leave ends a battle once it is won or lost
        slots count from 1
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

        var session = new CampaignSession(content, contentDir, CampaignRecord.Start(content, seed, difficulty), Console.Out, script is not null, scheme);
        return session.Play(input, strict);
    }

    private int Play(TextReader input, bool strict)
    {
        _out.WriteLine($"campaign, seed {_record.Seed}, difficulty {_record.Difficulty}, scheme {_scheme}, {_content.Campaign.Maps.Count} maps");
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

            _out.WriteLine($"map {_record.MapIndex + 1} of {_content.Campaign.Maps.Count}: {map.Name}, seed {_record.BattleSeed}");
            var battle = new PlaySession(_content, _record.Begin(map, _content, _scheme), _out, _scripted, _line);
            _out.Write(MapRenderer.Render(battle.State, _content));
            stopped = battle.RunCommands(input, strict, ref _commands);
            _line = battle.Line;
            _rejections.AddRange(battle.Rejections);
            if (stopped || !battle.Left)
            {
                _out.WriteLine($"campaign stopped in {map.Name} at turn {battle.State.Turn}, {(battle.State.Outcome.IsOver ? "decided and not left" : "undecided")}");
                break;
            }

            var outcome = battle.State.Outcome;
            if (outcome.Result != BattleResult.Won)
            {
                _out.WriteLine($"campaign lost on {map.Name}: {outcome.Reason}");
                break;
            }

            var before = _record;
            _record = _record.AfterBattle(battle.State, _content);
            var fallen = _record.Fallen.Skip(before.Fallen.Count).ToList();
            _out.WriteLine($"{map.Name} won: {outcome.Reason}; reward {_record.Purse - before.Purse}, the purse holds {_record.Purse}"
                + (fallen.Count > 0 ? $"; fallen: {string.Join(", ", fallen)}" : "; nobody fell"));
        }

        if (_record.IsFinished(_content))
        {
            _out.WriteLine($"campaign won: all {_content.Campaign.Maps.Count} maps, the purse holds {_record.Purse}");
            code = 0;
        }

        if (_scripted && _rejections.Count > 0)
        {
            _out.WriteLine($"rejected {_rejections.Count} of {_commands} commands:");
            foreach (var (at, command, reason) in _rejections)
            {
                _out.WriteLine($"  line {at}: {command}: {reason}");
            }
        }

        return stopped ? PlaySession.StrictStop : code;
    }

    private MapDefinition LoadMap(string mapId) => MapFiles.Load(Path.Combine(_contentDir, "maps", mapId + ".map"), _content);

    /// <summary>
    /// The between-map screen before <paramref name="map"/>: prints it, then applies commands until
    /// <c>march</c> (true) or the input ends or a strict stop (null).
    /// </summary>
    private bool? Screen(TextReader input, MapDefinition map, bool strict)
    {
        _out.WriteLine($"-- before map {_record.MapIndex + 1} of {_content.Campaign.Maps.Count}: {map.Name}; the purse holds {_record.Purse} --");
        PrintRoster();
        PrintShop();
        PrintDeployment(map);
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

            Execute(words, text, map);
            if (strict && _rejections.Count > 0)
            {
                _out.WriteLine($"strict: stopped at line {_line} ({text}); no later command applied");
                return null;
            }
        }

        _out.WriteLine($"campaign stopped before {map.Name}: the script ended on the screen");
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
            case ["certify", var unitId, var classId]:
                Take(_record.Certify(unitId, classId, _content), text);
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
            case ["bench" or "unbench" or "show", ..]:
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
        _out.WriteLine(result.Text);
        return true;
    }

    private void Error(string command, string message)
    {
        _out.WriteLine("ERROR: " + message);
        if (_scripted)
        {
            _rejections.Add((_line, command, message));
        }
    }

    private void PrintRoster()
    {
        _out.WriteLine("roster:");
        foreach (var unit in _record.Roster)
        {
            PrintUnit(unit, detail: false);
        }

        if (_record.Fallen.Count > 0)
        {
            _out.WriteLine($"  fallen: {string.Join(", ", _record.Fallen)}");
        }
    }

    private void PrintUnit(Unit unit, bool detail)
    {
        var unitClass = _content.Class(unit.ClassId);
        var slots = unit.Inventory.Items.Select((item, slot) => $"{slot + 1}: {ItemText(item)}");
        var bench = _record.Benched.Contains(unit.Id) ? ", benched" : "";
        _out.WriteLine($"  {unit.Id}: {unitClass.Name} L{unit.Level} exp {unit.Exp}{bench}; {(unit.Inventory.Count == 0 ? "no items" : string.Join(", ", slots))}");
        if (!detail)
        {
            return;
        }

        var stats = _content.StatsOf(unit);
        _out.WriteLine($"    hp {stats.Hp} str {stats.Str} mag {stats.Mag} dex {stats.Dex} spd {stats.Spd} lck {stats.Lck} def {stats.Def} res {stats.Res} cha {stats.Cha}");
        var ranks = unitClass.Weapons.Select(type => $"{type.ToString().ToLowerInvariant()} {unit.Skill.Rank(type)} ({unit.Skill.Points(type)})");
        _out.WriteLine($"    ranks: {string.Join(", ", ranks)}");
        if (PlaySession.MasteryLine(new BattleUnit(unit, Side.Player, default, stats.Hp, false, false), _content) is { } mastery)
        {
            _out.WriteLine("  " + mastery);
        }
    }

    /// <summary>An inventory entry with its uses out of its full uses, and what repairing it costs where it can be repaired and is short.</summary>
    private string ItemText(ItemStack stack)
    {
        if (!_content.Weapons.TryGetValue(stack.ItemId, out var weapon))
        {
            var item = _content.Item(stack.ItemId);
            return $"{item.Name} {stack.Uses}/{item.Uses}";
        }

        var text = $"{weapon.Name} {stack.Uses}/{weapon.Durability}";
        if (stack.Uses < weapon.Durability && CampaignRules.RepairPricePerUse(weapon) is { } perUse)
        {
            text += $" (repair {(weapon.Durability - stack.Uses) * perUse})";
        }

        return text;
    }

    private void PrintShop()
    {
        var wares = _record.NextMap(_content).Stock.Select(id => _content.Weapons.TryGetValue(id, out var weapon)
            ? $"{id} {weapon.Price}"
            : $"{id} {_content.Item(id).Price}");
        _out.WriteLine($"shop: {string.Join(", ", wares)}; a seal to certify costs {_content.Campaign.CertificationPrice}");
    }

    private void PrintDeployment(MapDefinition map)
    {
        try
        {
            _out.WriteLine($"deploys to {map.Name}: {string.Join(", ", _record.Deployment(map, _content))}");
        }
        catch (ArgumentException e)
        {
            _out.WriteLine("ERROR: " + e.Message);
        }
    }
}
