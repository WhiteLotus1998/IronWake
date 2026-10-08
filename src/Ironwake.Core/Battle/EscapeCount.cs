namespace Ironwake.Core;

/// <summary>
/// One player unit's count on an Escape map (issue 928): <paramref name="Phases"/> is the fewest
/// player phases of walking it needs to stand on an exit (0 when it already does), or null when no
/// way to an exit is open on the current board. <paramref name="LastStart"/> is the last turn on which
/// it can begin that walk and still leave by the turn limit, and <paramref name="LeavesOn"/> the
/// turn it leaves if it walks from now on; both null with no way.
/// </summary>
public sealed record ExitCount(BattleUnit Unit, int? Phases, int? LastStart, int? LeavesOn)
{
    /// <summary>Whether the unit can still leave by the map's turn limit.</summary>
    public bool CanLeave(int turnLimit) => LeavesOn is { } on && on <= turnLimit;
}

/// <summary>
/// The count on an Escape map (issue 928, Design Table round 313): how many turns each player unit
/// still on the board needs to leave, and the last turn it can start. Since issue 377 a unit exits
/// only from an exit it began its turn on, so a unit that needs N phases of walking to stand on an
/// exit leaves on turn <c>first + N</c>, where <c>first</c> is this turn when it can still move now
/// (its side's phase, not moved, not acted) and the next turn otherwise; on a map with
/// <see cref="MapDefinition.ExitAfterMove"/> it leaves on <c>first + N - 1</c>. Its last start is
/// <c>limit - N</c>, one later under <see cref="MapDefinition.ExitAfterMove"/>. A unit standing on
/// an exit leaves this turn if it may exit now, else the next.
/// N is read over the current board: the section 4 step costs (<see cref="AbilityRules.StepCost"/>),
/// the unit's movement type now (<see cref="Grounding.MovementOf"/>) and its Mov (<see cref="Frost.Mov"/>)
/// for every phase; enemy tiles are never entered, an ally's tile is crossed but no phase ends on it.
/// Every enemy blocks, seen or not: dusk draws an unseen enemy on its tile, so where it stands is on screen.
/// </summary>
public static class EscapeCount
{
    /// <summary>The count of every player unit on the board, in board order; empty off an Escape map.</summary>
    public static IReadOnlyList<ExitCount> Of(BattleState state, GameContent content) =>
        state.Map.Win != WinCondition.Escape || state.Map.Exits.Count == 0
            ? Array.Empty<ExitCount>()
            : state.UnitsOf(Side.Player).Select(u => For(state, content, u, ending: false)).ToList();

    /// <summary>
    /// The units whose count this player phase's end passes: each could leave by the limit if it
    /// walked this phase, and cannot from the next. The <c>end</c> warning names them (issue 928).
    /// Empty outside the player phase and off an Escape map.
    /// </summary>
    public static IReadOnlyList<ExitCount> PassedByEnding(BattleState state, GameContent content)
    {
        if (state.Phase != Side.Player || state.Outcome.IsOver)
        {
            return Array.Empty<ExitCount>();
        }

        return Of(state, content)
            .Where(c => c.CanLeave(state.Map.TurnLimit) && !For(state, content, c.Unit, ending: true).CanLeave(state.Map.TurnLimit))
            .ToList();
    }

    /// <summary>
    /// The board's count line on an Escape map while the battle runs, each unit by name:
    /// <c>count: Ottilie 2 turns to an exit (last start: turn 6), Teodor on an exit (leaves by turn 8)</c>,
    /// a unit out of time as <c>cannot leave by turn 8</c> and one walled off as <c>no way to an exit</c>.
    /// Null off an Escape map, once the battle is over, and with no player unit on the board.
    /// </summary>
    public static string? Line(BattleState state, GameContent content, UnitNames names)
    {
        if (state.Outcome.IsOver)
        {
            return null;
        }

        var counts = Of(state, content);
        return counts.Count == 0 ? null : "count: " + string.Join(", ", counts.Select(c => $"{names[c.Unit.Id]} {Words(c, state.Map.TurnLimit)}"));
    }

    /// <summary>
    /// The <c>end</c> warning for one unit <see cref="PassedByEnding"/> names, in the lethal line's style:
    /// <c>Count: after this phase Ottilie cannot reach an exit by turn 8</c>; an ally's line adds that the
    /// captain's exit leaves them behind.
    /// </summary>
    public static string Warning(ExitCount count, int turnLimit, UnitNames names) =>
        $"Count: after this phase {names[count.Unit.Id]} cannot reach an exit by turn {turnLimit}" + (count.Unit.IsCaptain ? "" : "; the captain's exit leaves them behind");

    private static string Words(ExitCount count, int turnLimit) =>
        count switch
        {
            { Phases: null } => "no way to an exit",
            _ when !count.CanLeave(turnLimit) => $"cannot leave by turn {turnLimit}",
            { Phases: 0 } => $"on an exit (leaves by turn {turnLimit})",
            { Phases: 1 } => $"1 turn to an exit (last start: turn {count.LastStart})",
            _ => $"{count.Phases} turns to an exit (last start: turn {count.LastStart})",
        };

    /// <summary>
    /// One unit's count; with <paramref name="ending"/> as it will read once this phase has ended,
    /// so nothing is taken this turn.
    /// </summary>
    private static ExitCount For(BattleState state, GameContent content, BattleUnit unit, bool ending)
    {
        var map = state.Map;
        if (Phases(state, content, unit) is not { } phases)
        {
            return new ExitCount(unit, null, null, null);
        }

        var ownPhase = !ending && state.Phase == unit.Side && !unit.Acted;
        var first = ownPhase && !unit.Moved ? state.Turn : state.Turn + 1;
        if (phases == 0)
        {
            var exitsNow = ownPhase && (map.ExitAfterMove || (!unit.Moved && !unit.Shoved));
            return new ExitCount(unit, 0, map.TurnLimit, exitsNow ? state.Turn : state.Turn + 1);
        }

        var late = map.ExitAfterMove ? 1 : 0;
        return new ExitCount(unit, phases, map.TurnLimit - phases + late, first + phases - late);
    }

    /// <summary>
    /// The fewest phases of walking from where the unit stands to a free exit tile: Dijkstra over
    /// (phases, Mov spent in the last phase), compared in that order, so a walk that reaches a tile
    /// in fewer phases always wins and, at equal phases, the one with more Mov left. A phase ends only
    /// on a tile no unit holds (or the unit's own). Null when no exit can be reached, or the unit has
    /// no Mov.
    /// </summary>
    private static int? Phases(BattleState state, GameContent content, BattleUnit unit)
    {
        var map = state.Map;
        if (map.IsExit(unit.At))
        {
            return 0;
        }

        var mov = Freeze.Mov(Armor.Mov(Frost.Mov(content.Class(unit.Unit.ClassId).Mov, unit), unit), unit);
        if (mov <= 0)
        {
            return null;
        }

        var movement = Grounding.MovementOf(unit, content);
        var abilities = content.AbilitiesOf(unit.Unit);
        var tiles = map.Width * map.Height;
        var best = new (int Phases, int Spent)[tiles];
        Array.Fill(best, (int.MaxValue, int.MaxValue));
        var origin = unit.At.Y * map.Width + unit.At.X;
        best[origin] = (1, 0);
        var frontier = new PriorityQueue<int, (int Phases, int Spent, int Index)>();
        frontier.Enqueue(origin, (1, 0, origin));
        while (frontier.TryDequeue(out var current, out var label))
        {
            if ((label.Phases, label.Spent) != best[current])
            {
                continue;
            }

            var here = new Coord(current % map.Width, current / map.Width);
            var free = current == origin || state.OccupantAt(here, unit.Side) == Occupant.None;
            if (free && map.IsExit(here))
            {
                return label.Phases;
            }

            foreach (var next in here.Neighbors())
            {
                if (!map.Contains(next) || state.OccupantAt(next, unit.Side) == Occupant.Enemy)
                {
                    continue;
                }

                var index = next.Y * map.Width + next.X;
                if (AbilityRules.StepCost(content.TerrainById(map.TerrainIds[index]), movement, abilities) is not { } step || step > mov)
                {
                    continue;
                }

                (int Phases, int Spent) reached;
                if (label.Spent + step <= mov)
                {
                    reached = (label.Phases, label.Spent + step);
                }
                else if (free)
                {
                    reached = (label.Phases + 1, step);
                }
                else
                {
                    continue;
                }

                if (reached.CompareTo(best[index]) < 0)
                {
                    best[index] = reached;
                    frontier.Enqueue(index, (reached.Phases, reached.Spent, index));
                }
            }
        }

        return null;
    }
}
