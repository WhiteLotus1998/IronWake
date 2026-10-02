namespace Ironwake.Core;

/// <summary>
/// Reachable tiles, DESIGN.md section 4: Dijkstra over the 4-connected grid with the
/// terrain's per-movement-type entry costs, a strict Mov budget, no zone of control.
/// Enemy tiles are never entered; ally tiles are crossed but never ended on.
/// </summary>
/// <remarks>
/// The tie-break is part of the contract, because paths travel over the presentation
/// protocol and every renderer must show the same one: tiles settle in (cost, row-major)
/// order, and a tile's path changes only when a strictly cheaper one is found. So a tile's
/// path arrives through the row-major-first of its neighbours that can offer it its cost.
/// The order <see cref="Coord.Neighbors"/> enumerates in does no work here: every step
/// costs at least 1, so every tile at a cost is queued before the first tile at that cost
/// settles, and the queue key alone fixes the order. Same inputs, same paths, on every machine.
/// On a board with wearing terrain (13.25, issue 782) the key is (cost, wear, row-major): among
/// the cheapest routes to a tile the one that wears the fewest plank steps wins, a tile's wear
/// charged when the route leaves it (<see cref="WearOnLeaving"/>), so a tile's path changes only
/// when a strictly cheaper or, at equal cost, a strictly less wearing one is found. Where nothing
/// wears every wear is 0 and the order is the plain (cost, row-major) one.
/// </remarks>
public static class Movement
{
    /// <summary>
    /// Every tile a unit standing at <paramref name="from"/> can reach with
    /// <paramref name="mov"/> movement points.
    /// </summary>
    /// <param name="map">The grid.</param>
    /// <param name="content">Terrain definitions the grid's ids refer to.</param>
    /// <param name="from">
    /// Where the unit stands. Must be inside the map, on terrain the movement type can
    /// enter; a unit on impassable terrain is a corrupt state, refused rather than answered.
    /// </param>
    /// <param name="movement">The unit's movement type, which picks the terrain cost column.</param>
    /// <param name="mov">Movement points, at least 0.</param>
    /// <param name="occupantAt">
    /// Who stands on a tile, from the moving unit's side. Never asked about
    /// <paramref name="from"/> itself; the mover's own tile is always a destination.
    /// </param>
    /// <param name="abilities">The mover's abilities, read for footing (<see cref="AbilityRules.StepCost"/>, issue 705); none by default.</param>
    public static Reach Reach(
        MapDefinition map,
        GameContent content,
        Coord from,
        MovementType movement,
        int mov,
        Func<Coord, Occupant> occupantAt,
        ValueList<Ability> abilities = default)
    {
        if (!map.Contains(from))
        {
            throw new ArgumentOutOfRangeException(nameof(from), from, $"outside a {map.Width}x{map.Height} map");
        }

        if (mov < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mov), mov, "mov must be at least 0");
        }

        var standing = map.TerrainAt(from, content);
        if (!standing.IsPassable(movement))
        {
            throw new ArgumentException(CannotStandMessage(from, standing, movement), nameof(from));
        }

        var tiles = map.Width * map.Height;
        var costs = new int[tiles];
        Array.Fill(costs, int.MaxValue);
        var wears = new int[tiles];
        var parents = new int[tiles];
        Array.Fill(parents, -1);
        var occupants = new Occupant[tiles];
        var asked = new bool[tiles];

        var origin = Index(map, from);
        costs[origin] = 0;
        var frontier = new PriorityQueue<int, (int Cost, int Wear, int Index)>();
        frontier.Enqueue(origin, (0, 0, origin));

        while (frontier.TryDequeue(out var current, out var priority))
        {
            if (priority.Cost > costs[current] || (priority.Cost == costs[current] && priority.Wear > wears[current]))
            {
                continue;
            }

            var here = TileAt(map, current);
            var leaving = wears[current] + WearOnLeaving(content, map.TerrainAt(here, content), movement, current != origin && occupants[current] == Occupant.Ally);
            foreach (var next in here.Neighbors())
            {
                if (!map.Contains(next))
                {
                    continue;
                }

                var index = Index(map, next);
                if (index != origin && !asked[index])
                {
                    asked[index] = true;
                    occupants[index] = occupantAt(next);
                }

                if (occupants[index] == Occupant.Enemy)
                {
                    continue;
                }

                var step = AbilityRules.StepCost(content.TerrainById(map.TerrainIds[index]), movement, abilities);
                if (step is null)
                {
                    continue;
                }

                var cost = costs[current] + step.Value;
                if (cost > mov || cost > costs[index] || (cost == costs[index] && leaving >= wears[index]))
                {
                    continue;
                }

                costs[index] = cost;
                wears[index] = leaving;
                parents[index] = current;
                frontier.Enqueue(index, (cost, leaving, index));
            }
        }

        var entries = new List<ReachEntry>();
        for (var index = 0; index < tiles; index++)
        {
            if (costs[index] == int.MaxValue)
            {
                continue;
            }

            var canEnd = index == origin || occupants[index] == Occupant.None;
            entries.Add(new ReachEntry(TileAt(map, index), costs[index], PathTo(map, parents, index, origin), canEnd));
        }

        return new Reach(from, movement, mov, ValueList<ReachEntry>.From(entries));
    }

    /// <summary>
    /// The remaining path cost from every tile to the nearest of <paramref name="targets"/>,
    /// with no Mov budget, over the same costs and occupancy as <see cref="Reach"/>: the
    /// query the approach rule of DESIGN.md section 8 needs. A tile's distance is the sum
    /// of the entry costs of the tiles walked after it, so a target's own distance is 0 and
    /// a tile from which no target can be reached is null. Enemy-occupied tiles are never
    /// walked through; the targets are taken as given, so the caller decides which tiles
    /// count as ones the mover may end on. One multi-source Dijkstra over the reversed
    /// graph, so nothing re-counts per tile. <paramref name="abilities"/> are the mover's, read for
    /// footing as <see cref="Reach"/> reads them (issue 705).
    /// </summary>
    public static Distances DistancesTo(
        MapDefinition map,
        GameContent content,
        IEnumerable<Coord> targets,
        MovementType movement,
        Func<Coord, Occupant> occupantAt,
        ValueList<Ability> abilities = default)
    {
        var tiles = map.Width * map.Height;
        var costs = new int[tiles];
        Array.Fill(costs, int.MaxValue);
        var frontier = new PriorityQueue<int, (int Cost, int Index)>();
        foreach (var target in targets)
        {
            if (!map.Contains(target))
            {
                throw new ArgumentOutOfRangeException(nameof(targets), target, $"outside a {map.Width}x{map.Height} map");
            }

            var index = Index(map, target);
            costs[index] = 0;
            frontier.Enqueue(index, (0, index));
        }

        var occupants = new Occupant?[tiles];
        var stepInto = new int?[tiles];
        var stepAsked = new bool[tiles];
        while (frontier.TryDequeue(out var current, out var priority))
        {
            if (priority.Cost > costs[current])
            {
                continue;
            }

            var here = TileAt(map, current);
            if (!stepAsked[current])
            {
                stepAsked[current] = true;
                stepInto[current] = AbilityRules.StepCost(content.TerrainById(map.TerrainIds[current]), movement, abilities);
                occupants[current] ??= occupantAt(here);
            }

            if (stepInto[current] is not { } entry || occupants[current] == Occupant.Enemy)
            {
                continue;
            }

            foreach (var previous in here.Neighbors())
            {
                if (!map.Contains(previous))
                {
                    continue;
                }

                var index = Index(map, previous);
                var cost = costs[current] + entry;
                if (cost >= costs[index])
                {
                    continue;
                }

                costs[index] = cost;
                frontier.Enqueue(index, (cost, index));
            }
        }

        return new Distances(map, costs);
    }

    /// <summary>
    /// The plank steps a walk wears on <paramref name="terrain"/> by leaving it (13.25, issue 782):
    /// <see cref="Planks.Steps"/> for the movement type, held to how far down its chain the terrain
    /// can still go, and none on a tile an ally stands on, which is crossed, not left. Terrain
    /// without <see cref="Terrain.WearsTo"/> wears nothing.
    /// </summary>
    public static int WearOnLeaving(GameContent content, Terrain terrain, MovementType movement, bool allyHeld) =>
        allyHeld || terrain.WearsTo is null ? 0 : Math.Min(Planks.Steps(movement), Planks.Chain(content, terrain).Count - 1);

    /// <summary>
    /// The refusal for a unit standing where its movement type cannot go, in the same
    /// words the map loader uses for a placement it rejects.
    /// </summary>
    public static string CannotStandMessage(Coord at, Terrain terrain, MovementType movement) =>
        $"{movement.ToString().ToLowerInvariant()} cannot stand on {terrain.Name} at {at}";

    private static ValueList<Coord> PathTo(MapDefinition map, int[] parents, int index, int origin)
    {
        var path = new List<Coord>();
        for (var at = index; at != origin; at = parents[at])
        {
            path.Add(TileAt(map, at));
        }

        path.Reverse();
        return ValueList<Coord>.From(path);
    }

    private static int Index(MapDefinition map, Coord at) => at.Y * map.Width + at.X;

    private static Coord TileAt(MapDefinition map, int index) => new(index % map.Width, index / map.Width);
}
