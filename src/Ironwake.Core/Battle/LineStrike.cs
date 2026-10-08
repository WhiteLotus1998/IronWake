namespace Ironwake.Core;

/// <summary>
/// The Iron Warden's line strike (issue 1384, Lotus's Hask rework, shaped in round 487; numbers provisional on #1247).
/// A unit holding <see cref="LineStrikeEffect"/> takes it as its action, after a move or without one, out through a
/// tile orthogonally beside it (<see cref="StrikeLine"/>):
/// <list type="bullet">
/// <item>The line runs up to <see cref="LineStrikeEffect.Reach"/> tiles straight out, stopping at the map's edge and
/// before a wall (a tile even a flier cannot enter); water and the rest pass it.</item>
/// <item>Every unit of another side on it is struck once, nearest first, with the equipped weapon read as if adjacent:
/// one forecast, one hit roll keyed by striker and target, no double, and no counter. The striker's own side on the
/// line is passed over.</item>
/// <item>It is refused when no unit of another side stands on the line. It spends no weapon use and earns nothing.</item>
/// </list>
/// The enemy planner takes it when the line catches two or more units it knows (<see cref="Best"/>); <c>threat</c>
/// prints the line the planner would strike through a unit (<see cref="Through"/>).
/// </summary>
public static class LineStrike
{
    /// <summary>The distance every target is read at: the lance's own reach, whatever the tile.</summary>
    public const int ReadAt = 1;

    /// <summary>The four directions, in the order a tie is broken: north, east, south, west.</summary>
    public static readonly IReadOnlyList<(int Dx, int Dy, string Name)> Directions =
        new[] { (0, -1, "north"), (1, 0, "east"), (0, 1, "south"), (-1, 0, "west") };

    /// <summary>The line strike <paramref name="unit"/> holds, or null.</summary>
    public static LineStrikeEffect? Of(GameContent content, BattleUnit unit) =>
        AbilityRules.LineStrike(content.AbilitiesOf(unit.Unit));

    /// <summary>
    /// The tiles of a line of <paramref name="reach"/> from <paramref name="from"/> out through <paramref name="toward"/>,
    /// nearest first: it stops at the map's edge and before a wall, a tile even a flier cannot enter.
    /// </summary>
    public static IReadOnlyList<Coord> LineOf(BattleState state, GameContent content, Coord from, Coord toward, int reach)
    {
        var dx = toward.X - from.X;
        var dy = toward.Y - from.Y;
        var line = new List<Coord>();
        for (var step = 1; step <= reach; step++)
        {
            var at = new Coord(from.X + dx * step, from.Y + dy * step);
            if (!state.Map.Contains(at) || !state.Map.TerrainAt(at, content).IsPassable(MovementType.Flying))
            {
                break;
            }

            line.Add(at);
        }

        return line;
    }

    /// <summary>
    /// Every tile a line of <paramref name="reach"/> struck from <paramref name="from"/> could fall on, in all four
    /// directions (<see cref="LineOf"/>): the cross the Sim's danger area reads (issue 1389).
    /// </summary>
    public static IEnumerable<Coord> Cross(BattleState state, GameContent content, Coord from, int reach) =>
        Directions.SelectMany(d => LineOf(state, content, from, new Coord(from.X + d.Dx, from.Y + d.Dy), reach));

    /// <summary>
    /// Every tile a line of <paramref name="reach"/> could be struck from to fall on <paramref name="target"/>: the tiles
    /// up to the reach away in one cardinal line whose <see cref="LineOf"/> runs on to it, so a wall between them
    /// shuts the tile out (issue 1389, the exposure the Sim's player prices).
    /// </summary>
    public static IEnumerable<Coord> StruckFrom(BattleState state, GameContent content, Coord target, int reach)
    {
        foreach (var (dx, dy, _) in Directions)
        {
            for (var step = 1; step <= reach; step++)
            {
                var from = new Coord(target.X - dx * step, target.Y - dy * step);
                if (!state.Map.Contains(from))
                {
                    break;
                }

                if (LineOf(state, content, from, new Coord(from.X + dx, from.Y + dy), reach).Contains(target))
                {
                    yield return from;
                }
            }
        }
    }

    /// <summary>Every unit of another side than <paramref name="striker"/>'s on <paramref name="line"/>, nearest first.</summary>
    public static IReadOnlyList<BattleUnit> Struck(BattleState state, BattleUnit striker, IReadOnlyList<Coord> line) =>
        line.Select(state.UnitAt).OfType<BattleUnit>().Where(u => u.Side != striker.Side).ToList();

    /// <summary>The forecast of <paramref name="striker"/>'s line on <paramref name="target"/>: one strike, no counter, read at <see cref="ReadAt"/>.</summary>
    public static CombatForecast Forecast(BattleState state, GameContent content, BattleUnit striker, BattleUnit target) =>
        Combat.Forecast(
            striker.ToCombatant(state, content, against: target) with { SingleStrike = true },
            target.ToCombatant(state, content, countering: true, against: striker) with { Blind = true },
            ReadAt,
            state.Scheme);

    /// <summary>
    /// Why <paramref name="unit"/> may not strike a line out through <paramref name="toward"/>, or null when it may.
    /// Whether the unit may act at all is the caller's check.
    /// </summary>
    public static string? Refusal(BattleState state, GameContent content, BattleUnit unit, Coord toward)
    {
        if (Of(content, unit) is not { } strike)
        {
            return $"{unit.Id} has no line strike";
        }

        if (unit.EquippedWeapon(content) is null)
        {
            return $"{unit.Id} has no weapon to strike with";
        }

        if (!state.Map.Contains(toward))
        {
            return $"{toward} is outside the map";
        }

        if (unit.At.DistanceTo(toward) != 1)
        {
            return $"{toward} is not beside {unit.Id}; name the first tile of the line";
        }

        return Struck(state, unit, LineOf(state, content, unit.At, toward, strike.Reach)).Count == 0
            ? $"no enemy of {unit.Id} on the line through {toward}"
            : null;
    }

    /// <summary>
    /// The strike on <paramref name="state"/>, already checked: <see cref="LineStruck"/>, the striker marked as having moved
    /// and acted, then each unit on the line struck in turn (<see cref="CombatFought"/>, and <see cref="UnitDied"/> by
    /// <paramref name="died"/>, the resolver's own handling of a death).
    /// </summary>
    public static BattleState Apply(
        BattleState state,
        GameContent content,
        BattleUnit unit,
        Coord toward,
        List<GameEvent> events,
        Func<BattleState, BattleUnit, BattleUnit, BattleState> died)
    {
        var line = LineOf(state, content, unit.At, toward, Of(content, unit)!.Reach);
        var struck = Struck(state, unit, line);
        events.Add(new LineStruck(unit.Id, unit.At, ValueList<Coord>.From(line), ValueList<string>.From(struck.Select(t => t.Id))));
        var striker = unit with { Moved = true, Acted = true, Canto = null, Braced = false };
        var next = state.WithUnit(striker);
        foreach (var aimed in struck)
        {
            var target = next.Find(aimed.Id)!;
            var result = CombatResolver.Resolve(
                striker.ToCombatant(next, content, against: target) with { SingleStrike = true },
                target.ToCombatant(next, content, countering: true, against: striker) with { Blind = true },
                ReadAt,
                new CombatContext(next.Turn, next.Phase),
                new KeyedRng(next.Seed),
                next.Scheme);
            events.Add(new CombatFought(unit.Id, target.Id, next.Turn, next.Phase, result.Strikes, striker.Hp, result.DefenderHp));
            next = next.WithUnit(target with { Hp = result.DefenderHp });
            if (result.DefenderDied && Swallow.Takes(target))
            {
                next = Swallow.Take(next, content, target.Id, events);
            }
            else if (result.DefenderDied)
            {
                events.Add(new UnitDied(target.Id, target.Side, target.At));
                next = died(next, target with { Hp = 0 }, striker);
            }
            else
            {
                next = Armor.AfterCombat(next, unit.Id, target.Id, result.Strikes, events);
            }
        }

        return next;
    }

    /// <summary>A line the planner may strike: the tile struck from, the first tile out, and the units it catches.</summary>
    public sealed record Choice(Coord From, Coord Toward, string Direction, IReadOnlyList<BattleUnit> Caught, double Expected);

    /// <summary>
    /// The line <paramref name="unit"/> strikes from one of <paramref name="tiles"/> (its planner's tiles, nearest in reach
    /// order first), or null: the line catching the most of <paramref name="known"/>, at least two, then the most expected
    /// damage, then the first tile and direction (<see cref="Directions"/>). A tile <paramref name="refused"/> names is no
    /// option, which is how the boss veto applies; a tile another unit stands on is never one but the unit's own.
    /// </summary>
    public static Choice? Best(
        BattleState state,
        GameContent content,
        BattleUnit unit,
        IEnumerable<Coord> tiles,
        IReadOnlyCollection<BattleUnit> known,
        Func<Coord, bool>? refused = null)
    {
        if (Of(content, unit) is not { } strike || unit.EquippedWeapon(content) is null)
        {
            return null;
        }

        var knownIds = known.Select(k => k.Id).ToHashSet();
        Choice? best = null;
        foreach (var tile in tiles)
        {
            if ((tile != unit.At && state.UnitAt(tile) is not null) || refused?.Invoke(tile) == true)
            {
                continue;
            }

            var there = unit with { At = tile };
            var board = state.WithUnit(there);
            foreach (var (dx, dy, name) in Directions)
            {
                var toward = new Coord(tile.X + dx, tile.Y + dy);
                if (!state.Map.Contains(toward))
                {
                    continue;
                }

                var caught = Struck(board, there, LineOf(board, content, tile, toward, strike.Reach)).Where(u => knownIds.Contains(u.Id)).ToList();
                if (caught.Count < 2)
                {
                    continue;
                }

                var expected = caught.Sum(target =>
                {
                    var side = Forecast(board, content, there, target).Attacker;
                    return Math.Min(target.Hp, side.Damage) * side.DisplayedHit / 100.0;
                });
                if (best is null || caught.Count > best.Caught.Count || (caught.Count == best.Caught.Count && expected > best.Expected))
                {
                    best = new Choice(tile, toward, name, caught, expected);
                }
            }
        }

        return best;
    }

    /// <summary>
    /// The line the planner would strike through <paramref name="unit"/> standing on <paramref name="tile"/> (issue 1384's
    /// <c>threat</c> line): for each enemy holding a line strike, its planned commands on the board with the unit moved
    /// there; the first whose line catches it, with that striker. Null when none does.
    /// </summary>
    public static (BattleUnit Striker, Choice Line)? Through(BattleState state, GameContent content, BattleUnit unit, Coord tile)
    {
        var board = tile == unit.At ? state : state.WithUnit(unit with { At = tile });
        var other = unit.Side == Side.Player ? Side.Enemy : Side.Player;
        foreach (var striker in board.UnitsOf(other).Where(u => Of(content, u) is not null))
        {
            var planned = EnemyAi.PlanUnit(board with { Phase = other }, content, striker with { Moved = false, Acted = false });
            if (planned.OfType<StrikeLine>().SingleOrDefault() is not { } line)
            {
                continue;
            }

            var from = planned.OfType<Move>().SingleOrDefault()?.To ?? striker.At;
            var there = striker with { At = from };
            var after = board.WithUnit(there);
            var caught = Struck(after, there, LineOf(after, content, from, line.Toward, Of(content, striker)!.Reach));
            if (caught.Any(u => u.Id == unit.Id))
            {
                var name = Directions.Single(d => from.X + d.Dx == line.Toward.X && from.Y + d.Dy == line.Toward.Y).Name;
                return (there, new Choice(from, line.Toward, name, caught, 0));
            }
        }

        return null;
    }
}
