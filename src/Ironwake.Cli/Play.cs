using System.Text;
using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Cli;

/// <summary>
/// The <c>play</c> command (issue 11): a battle on one map, commands from a script file or
/// standard input, every event printed as a line and the board after every command. The
/// session renders only from events and asks the core for every number through
/// <see cref="Queries"/>, <see cref="Resolver"/>, and <see cref="EnemyAi"/>; it computes
/// nothing about the rules itself (DESIGN.md section 2). Output is plain ASCII.
/// </summary>
public sealed class PlaySession
{
    public const string Usage = "usage: ironwake play <map-file> [--seed N] [--script file] [--content dir]";

    /// <summary>The line every transcript starts with, naming the systems this build lacks (Design Table, fourth round).</summary>
    public const string MissingSystems = "this build has no items (issue 9); " + SyntheticRoster.Notice;

    private const string Help = """
        commands:
          move <unit> <x,y>        move a unit to a tile in its reach
          attack <unit> <target>   attack an enemy in range (the forecast prints first)
          wait <unit>              end the unit's action
          end                      end the player phase; the enemy phase plays out
          recall <n>               rewind to history state n (spends a charge)
          forecast <unit> <target> show the forecast without attacking
          reach <unit>             show the board with the unit's reachable tiles marked
          show <unit>              show a unit's numbers
          map                      show the board
          help                     this list
        unavailable in this build:
          item <unit> <slot> [target]   items land with issue 9
        """;

    private readonly GameContent _content;
    private readonly TextWriter _out;
    private BattleState _state;

    private PlaySession(GameContent content, BattleState state, TextWriter output)
    {
        _content = content;
        _state = state;
        _out = output;
    }

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
        var contentDir = "content";
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
                case "--content" when value is not null:
                    contentDir = value;
                    i++;
                    break;
                default:
                    Console.WriteLine($"ERROR: unexpected argument '{args[i]}'");
                    Console.WriteLine(Usage);
                    return 2;
            }
        }

        GameContent content;
        MapDefinition map;
        try
        {
            content = ContentLoader.Load(contentDir);
            map = MapFiles.Load(args[0], content);
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

        var session = new PlaySession(content, BattleState.From(map, content, SyntheticRoster.Cadets, seed), Console.Out);
        return session.Play(input, echo: script is not null, seed);
    }

    private int Play(TextReader input, bool echo, ulong seed)
    {
        _out.WriteLine(MissingSystems);
        _out.WriteLine($"{_state.Map.Name}, seed {seed}, scheme {_state.Scheme}");
        _out.Write(MapRenderer.Render(_state, _content));
        while (input.ReadLine() is { } line)
        {
            var text = line.Trim();
            if (text.Length == 0 || text.StartsWith('#'))
            {
                continue;
            }

            if (echo)
            {
                _out.WriteLine("> " + text);
            }

            Execute(text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        var outcome = _state.Outcome;
        _out.WriteLine(outcome.IsOver
            ? $"battle {(outcome.Result == BattleResult.Won ? "won" : "lost")}: {outcome.Reason}"
            : $"battle ongoing at turn {_state.Turn}, {_state.Phase.ToString().ToLowerInvariant()} phase");
        return outcome.Result == BattleResult.Won ? 0 : 1;
    }

    private void Execute(string[] words)
    {
        switch (words[0])
        {
            case "move" when words.Length == 3 && TryCoord(words[2], out var to):
                Apply(new Move(words[1], to));
                break;
            case "move":
                Error("usage: move <unit> <x,y>");
                break;
            case "attack" when words.Length == 3:
                if (PrintForecast(words[1], words[2]))
                {
                    Apply(new Attack(words[1], words[2]));
                }

                break;
            case "attack":
                Error("usage: attack <unit> <target>");
                break;
            case "wait" when words.Length == 2:
                Apply(new Wait(words[1]));
                break;
            case "wait":
                Error("usage: wait <unit>");
                break;
            case "end" when words.Length == 1:
                if (Apply(new EndPhase()))
                {
                    EnemyPhase();
                }

                break;
            case "end":
                Error("usage: end");
                break;
            case "recall" when words.Length == 2 && int.TryParse(words[1], out var index):
                Apply(new Recall(index));
                break;
            case "recall":
                Error("usage: recall <n>  (history holds " + _state.History.Count + " states)");
                break;
            case "item":
                Error("item is not in this build: items land with issue 9 (usage when it does: item <unit> <slot> [target])");
                break;
            case "forecast" when words.Length == 3:
                PrintForecast(words[1], words[2]);
                break;
            case "forecast":
                Error("usage: forecast <unit> <target>");
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
                }

                break;
            case "show":
                Error("usage: show <unit>");
                break;
            case "map":
                _out.Write(MapRenderer.Render(_state, _content));
                break;
            case "help":
                _out.WriteLine(Help);
                break;
            default:
                Error($"unknown command '{words[0]}'; type help");
                break;
        }
    }

    private bool Apply(Command command)
    {
        var result = Resolver.Apply(_state, _content, command);
        if (!result.Accepted)
        {
            Error(result.Rejection!.Message);
            return false;
        }

        _state = result.Next;
        foreach (var e in result.Events)
        {
            _out.WriteLine(Describe(e));
        }

        if (command is not EndPhase)
        {
            _out.Write(MapRenderer.Render(_state, _content));
            AnnounceOutcome();
        }

        return true;
    }

    /// <summary>Plays the enemy phase from <see cref="EnemyAi.Plan"/>, one command at a time through the same resolver, and prints the board once it is over.</summary>
    private void EnemyPhase()
    {
        foreach (var command in EnemyAi.Plan(_state, _content))
        {
            _out.WriteLine("enemy: " + Describe(command));
            var result = Resolver.Apply(_state, _content, command);
            if (!result.Accepted)
            {
                throw new InvalidOperationException($"the enemy AI's {command} was rejected: {result.Rejection!.Message}");
            }

            _state = result.Next;
            foreach (var e in result.Events)
            {
                _out.WriteLine(Describe(e));
            }
        }

        _out.Write(MapRenderer.Render(_state, _content));
        AnnounceOutcome();
    }

    private void AnnounceOutcome()
    {
        var outcome = _state.Outcome;
        if (outcome.IsOver)
        {
            _out.WriteLine($"battle {(outcome.Result == BattleResult.Won ? "won" : "lost")}: {outcome.Reason}; only recall is left");
        }
    }

    private bool PrintForecast(string unitId, string targetId)
    {
        if (Find(unitId) is not { } unit || Find(targetId) is not { } target)
        {
            return false;
        }

        var forecast = Queries.Forecast(_state, _content, unit, target);
        if (forecast is null)
        {
            Error($"{unit.Id} cannot attack {target.Id} from {unit.At}");
            return false;
        }

        _out.WriteLine($"forecast {unit.Id} -> {target.Id}: {Side(forecast.Attacker)}; counter: {(forecast.Defender.Strikes ? Side(forecast.Defender) : "none")}");
        return true;

        static string Side(SideForecast side) =>
            $"dmg {side.Damage}{(side.Doubles ? " x2" : "")} hit {side.DisplayedHit}% crit {side.CritChance}%";
    }

    private void Show(BattleUnit unit)
    {
        var stats = unit.Unit.EffectiveStats(_content.Class(unit.Unit.ClassId));
        var weapon = unit.EquippedWeapon(_content);
        _out.WriteLine($"{unit.Id}: {unit.Unit.Name}, {_content.Class(unit.Unit.ClassId).Name} L{unit.Unit.Level}, at {unit.At} on {_state.Map.TerrainAt(unit.At, _content).Name}");
        _out.WriteLine($"  hp {unit.Hp}/{stats.Hp}  str {stats.Str} mag {stats.Mag} dex {stats.Dex} spd {stats.Spd} lck {stats.Lck} def {stats.Def} res {stats.Res} cha {stats.Cha}");
        _out.WriteLine($"  weapon: {(weapon is null ? "none" : $"{weapon.Name} (mt {weapon.Mt} hit {weapon.Hit} crit {weapon.Crit} wt {weapon.Wt} range {weapon.MinRange}-{weapon.MaxRange})")}");
        var targets = string.Join(", ", Queries.Targets(_state, _content, unit).Select(t => t.Id));
        _out.WriteLine($"  targets from here: {(targets.Length == 0 ? "none" : targets)}");
    }

    private BattleUnit? Find(string id)
    {
        var unit = _state.Find(id);
        if (unit is null)
        {
            Error($"no living unit '{id}'");
        }

        return unit;
    }

    private void Error(string message) => _out.WriteLine("ERROR: " + message);

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

    private static string Describe(Command command) => command switch
    {
        Move m => $"move {m.UnitId} {m.To}",
        Attack a => $"attack {a.UnitId} {a.TargetId}",
        Wait w => $"wait {w.UnitId}",
        EndPhase => "end",
        Recall r => $"recall {r.ToIndex}",
        UseItem i => $"item {i.UnitId} {i.Slot}",
        _ => command.ToString() ?? "?",
    };

    /// <summary>One line per event, the words a transcript reader sees.</summary>
    public static string Describe(GameEvent e)
    {
        switch (e)
        {
            case UnitMoved m:
                return $"{m.UnitId} moves {m.From} -> {m.To}" + (m.Path.Count > 1 ? " via " + string.Join(" ", m.Path.Take(m.Path.Count - 1)) : "");
            case CombatFought f:
                var sb = new StringBuilder();
                sb.Append($"{f.AttackerId} attacks {f.TargetId}");
                foreach (var strike in f.Strikes)
                {
                    sb.Append('\n').Append("  ").Append(strike.AttackerId).Append(' ')
                        .Append(!strike.Hit ? "misses " + strike.TargetId : $"{(strike.Crit ? "crits" : "hits")} {strike.TargetId} for {strike.Damage} (hp {strike.TargetHpAfter})");
                }

                sb.Append('\n').Append($"  {f.AttackerId} hp {f.AttackerHpAfter}, {f.TargetId} hp {f.TargetHpAfter}");
                return sb.ToString();
            case UnitDied d:
                return $"{d.UnitId} falls at {d.At}";
            case ExpGained x:
                return $"{x.UnitId} gains {x.Amount} exp ({x.ExpAfter})";
            case LeveledUp l:
                var rose = string.Join(" ", Stats.All.Where(stat => l.Gains.Get(stat) > 0).Select(stat => stat.ToString().ToLowerInvariant() + " +1"));
                return $"{l.UnitId} reaches level {l.NewLevel}: {(rose.Length == 0 ? "nothing rose" : rose)}";
            case UnitWaited w:
                return $"{w.UnitId} waits";
            case UnitHealed h:
                return $"{h.UnitId} heals {h.Amount} (hp {h.HpAfter})";
            case PhaseEnded p:
                return $"-- {p.Side.ToString().ToLowerInvariant()} phase ends, turn {p.Turn} --";
            case PhaseBegan p:
                return $"-- {p.Side.ToString().ToLowerInvariant()} phase, turn {p.Turn} --";
            case GroupWoke g:
                return $"group {g.Group} wakes: {g.Cause.ToString().ToLowerInvariant()}";
            case Recalled r:
                return $"recalled to state {r.ToIndex}; {r.ChargesLeft} charges left";
            default:
                return e.ToString() ?? "?";
        }
    }
}
