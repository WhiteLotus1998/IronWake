using Ironwake.Cli;
using Ironwake.Core;

namespace Ironwake.Client;

/// <summary>One forecast shown while a tile is hovered: the target, where it stands, and the console's forecast text.</summary>
public sealed record HoverForecast(string TargetId, Coord TargetAt, string Text);

/// <summary>
/// The thin renderer's presenter (issue 347): everything the Godot client shows, with no
/// rules in it. Reach, targets and forecasts are core queries; every line of the event log
/// is the console's own text for an event (<see cref="PlaySession.Describe(GameEvent, GameContent)"/>),
/// so the log equals what <c>ironwake play --log</c> writes for the same commands, which the
/// parity gate checks byte for byte. The enemy phase does not resolve as one jump: after
/// <c>end</c> the planner's commands wait in a queue, and <see cref="Step"/> reveals one event
/// line at a time, applying each command as its first line is shown.
/// </summary>
public sealed class ClientSession
{
    private readonly List<string> _log = new();
    private readonly Queue<Command> _enemy = new();
    private readonly Queue<string> _pending = new();

    public ClientSession(GameContent content, BattleState state)
    {
        Content = content;
        State = state;
    }

    public GameContent Content { get; }

    /// <summary>The board as the client draws it: the state after the last command applied.</summary>
    public BattleState State { get; private set; }

    /// <summary>Every event revealed so far, in order, as the console prints it; a combat's entry runs over several lines.</summary>
    public IReadOnlyList<string> Log => _log;

    /// <summary>The event log as <c>play --log</c> writes it: each line ending in <c>\n</c>.</summary>
    public string LogText => string.Concat(_log.Select(line => line + "\n"));

    /// <summary>The last refusal or hint for the status bar, never part of the event log.</summary>
    public string? Status { get; private set; }

    /// <summary>The selected player unit's id, or null.</summary>
    public string? Selected { get; private set; }

    /// <summary>The selected unit's reach, as the core answers it, or null with no selection.</summary>
    public Reach? Reach => Selected is { } id && State.Find(id) is { } unit ? Queries.Reachable(State, Content, unit) : null;

    /// <summary>Whether enemy-phase events are still waiting to be shown; no player command is taken until they are.</summary>
    public bool EnemyPhasePlaying => _pending.Count > 0 || _enemy.Count > 0;

    /// <summary>The living unit on a tile, or null.</summary>
    public BattleUnit? UnitAt(Coord at) => State.Units.FirstOrDefault(u => u.At == at);

    /// <summary>
    /// Selects the player unit on a tile, if it has not acted or is owed a Canto; anything
    /// else clears the selection. True when a unit is selected.
    /// </summary>
    public bool Select(Coord at)
    {
        Selected = UnitAt(at) is { Side: Side.Player } unit && (!unit.Acted || State.CantoReachOf(unit, Content) is not null) ? unit.Id : null;
        return Selected is not null;
    }

    public void ClearSelection() => Selected = null;

    /// <summary>
    /// The forecast against each enemy the selected unit could strike from <paramref name="tile"/>
    /// (issue 151's mechanic, where it is felt): one entry per target the core forecasts from
    /// there, with the console's text. Empty with no selection or from a tile it cannot stand on.
    /// </summary>
    public IReadOnlyList<HoverForecast> Hover(Coord tile)
    {
        if (Selected is not { } id || State.Find(id) is not { } unit || EnemyPhasePlaying)
        {
            return Array.Empty<HoverForecast>();
        }

        var lines = new List<HoverForecast>();
        foreach (var target in State.UnitsOf(Side.Enemy).OrderBy(u => u.Id, StringComparer.Ordinal))
        {
            if (Queries.Forecast(State, Content, unit, target, tile) is { } forecast)
            {
                lines.Add(new HoverForecast(target.Id, target.At, PlaySession.ForecastText(State, Content, unit, target, forecast, tile, tile != unit.At)));
            }
        }

        return lines;
    }

    /// <summary>
    /// What a click on a tile does with a unit selected: the unit itself waits, a hostile unit
    /// is attacked with the equipped weapon, a tile it can end on is moved to (a Canto when one
    /// is owed); anything else selects what is there. Returns the command applied, or null.
    /// </summary>
    public Command? Click(Coord at)
    {
        if (EnemyPhasePlaying)
        {
            Status = "the enemy phase is playing; step or continue";
            return null;
        }

        if (Selected is not { } id || State.Find(id) is not { } unit)
        {
            Select(at);
            return null;
        }

        Command? command = null;
        if (at == unit.At && !unit.Acted)
        {
            command = new Wait(unit.Id);
        }
        else if (UnitAt(at) is { Side: Side.Enemy } target)
        {
            command = new Attack(unit.Id, target.Id);
        }
        else if (Reach is { } reach && reach.CanEnd(at))
        {
            command = unit.Acted ? new Canto(unit.Id, at) : new Move(unit.Id, at);
        }

        if (command is null)
        {
            Select(at);
            return null;
        }

        return Submit(command) ? command : null;
    }

    /// <summary>
    /// Applies a player command through the resolver and logs its events. <c>end</c> also
    /// queues the enemy phase for <see cref="Step"/>. False, with the core's refusal in
    /// <see cref="Status"/>, when it is refused or the enemy phase is still playing.
    /// </summary>
    public bool Submit(Command command)
    {
        if (EnemyPhasePlaying)
        {
            Status = "the enemy phase is playing; step or continue";
            return false;
        }

        var result = Resolver.Apply(State, Content, command);
        if (!result.Accepted)
        {
            Status = result.Rejection!.Message;
            return false;
        }

        Status = null;
        State = result.Next;
        _log.AddRange(result.Events.Select(e => PlaySession.Describe(e, Content)));
        if (command is EndPhase)
        {
            Selected = null;
            foreach (var enemy in EnemyAi.Plan(State, Content))
            {
                _enemy.Enqueue(enemy);
            }
        }
        else if (Selected is { } id && State.Find(id) is { } unit && unit.Acted && State.CantoReachOf(unit, Content) is null)
        {
            Selected = null;
        }

        return true;
    }

    /// <summary>
    /// Reveals the next enemy-phase event line, applying the next planned command when the
    /// last one's lines are all shown. An enemy's Move or Wait no player unit sees prints the
    /// console's dark line and only the events it set off beyond the move, as the console
    /// does (DESIGN.md 13.7). False when nothing is waiting.
    /// </summary>
    public bool Step()
    {
        while (_pending.Count == 0 && _enemy.Count > 0)
        {
            var command = _enemy.Dequeue();
            var dark = command is Move or Wait && Dusk.InTheDark(State, Content, command);
            var result = Resolver.Apply(State, Content, command);
            if (!result.Accepted)
            {
                throw new InvalidOperationException($"the enemy AI's {command} was rejected: {result.Rejection!.Message}");
            }

            State = result.Next;
            if (dark)
            {
                _pending.Enqueue(ProtocolSession.DarkLine);
            }

            foreach (var e in result.Events.Where(e => !dark || e is not UnitMoved and not UnitWaited))
            {
                _pending.Enqueue(PlaySession.Describe(e, Content));
            }
        }

        if (_pending.Count == 0)
        {
            return false;
        }

        _log.Add(_pending.Dequeue());
        return true;
    }

    /// <summary>Reveals every event left in the enemy phase.</summary>
    public void Continue()
    {
        while (Step())
        {
        }
    }
}
