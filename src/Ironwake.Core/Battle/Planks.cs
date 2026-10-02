namespace Ironwake.Core;

/// <summary>
/// Rotten planks (DESIGN.md 13.25, experiment): ground the board spends. A terrain with
/// <see cref="Terrain.WearsTo"/> wears when a unit walks off it: every tile of a walk, the
/// start included and the end excluded, whose terrain wears steps once toward what it wears
/// to, twice under a horse or armour (<see cref="Steps"/>), and never under a flyer. A tile
/// another unit stands on when the walk ends does not wear (it is crossed, not left). The
/// change lands when the walk is over, one <see cref="TerrainChanged"/> per step, in walk
/// order, so a tile the walker has left is always empty and may become water. The rule is
/// the terrain's, so it needs no header: a map that has no wearing terrain is unchanged.
/// Both sides wear planks alike; the enemy planner and the Sim's heuristic read the board as
/// it stands and are blind to the wear to come.
/// </summary>
public static class Planks
{
    /// <summary>
    /// How many steps one walk off a wearing tile wears it: none for a flyer, two for a horse
    /// or armour, one on foot.
    /// </summary>
    public static int Steps(MovementType movement) => movement switch
    {
        MovementType.Flying => 0,
        MovementType.Cavalry or MovementType.Armored => 2,
        _ => 1,
    };

    /// <summary>
    /// Wears the tiles <paramref name="walker"/> walked off going from <paramref name="from"/>
    /// along <paramref name="path"/> (which ends at its destination), on the board after it has
    /// moved. Returns <paramref name="state"/> unchanged when nothing wears.
    /// </summary>
    public static BattleState AfterWalk(BattleState state, GameContent content, BattleUnit walker, Coord from, IReadOnlyList<Coord> path, List<GameEvent> events)
    {
        if (path.Count == 0)
        {
            return state;
        }

        var steps = Steps(content.Class(walker.Unit.ClassId).Movement);
        if (steps == 0)
        {
            return state;
        }

        var left = new List<Coord> { from };
        for (var i = 0; i < path.Count - 1; i++)
        {
            left.Add(path[i]);
        }

        foreach (var tile in left.Distinct())
        {
            if (state.UnitAt(tile) is not null)
            {
                continue;
            }

            for (var step = 0; step < steps; step++)
            {
                var terrain = state.Map.TerrainAt(tile, content);
                if (terrain.WearsTo is not { } next)
                {
                    break;
                }

                events.Add(new TerrainChanged(tile, next));
                state = state with { Map = state.Map.WithTerrain(tile, next) };
            }
        }

        return state;
    }

    /// <summary>
    /// The chain a wearing terrain walks down, starting with <paramref name="terrain"/> itself:
    /// <c>Planks, Split planks, Water</c>. A terrain that does not wear is its own chain.
    /// </summary>
    public static IReadOnlyList<Terrain> Chain(GameContent content, Terrain terrain)
    {
        var chain = new List<Terrain> { terrain };
        while (chain[^1].WearsTo is { } next && chain.Count <= content.Terrain.Count)
        {
            chain.Add(content.TerrainById(next));
        }

        return chain;
    }

    /// <summary>
    /// The board's line for ground that wears, or null when the map has none: each wearing
    /// terrain with its tiles, row-major, then the rule in player words (rules go on screen,
    /// rounds 42 and 44): <c>wears: Planks 6,4 6,5; Split planks 7,5 (a unit that walks off
    /// one wears it a step, a horse or armour two, a flyer none: Planks, Split planks, Water)</c>.
    /// </summary>
    public static string? Line(MapDefinition map, GameContent content)
    {
        var tiles = new SortedDictionary<int, (Terrain Terrain, List<Coord> At)>();
        var order = new List<string>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var at = new Coord(x, y);
                var terrain = map.TerrainAt(at, content);
                if (terrain.WearsTo is null)
                {
                    continue;
                }

                if (!order.Contains(terrain.Id))
                {
                    order.Add(terrain.Id);
                }

                var index = order.IndexOf(terrain.Id);
                if (!tiles.TryGetValue(index, out var entry))
                {
                    entry = (terrain, new List<Coord>());
                    tiles[index] = entry;
                }

                entry.At.Add(at);
            }
        }

        if (tiles.Count == 0)
        {
            return null;
        }

        var longest = tiles.Values.Select(t => Chain(content, t.Terrain)).OrderByDescending(c => c.Count).First();
        var listed = string.Join("; ", tiles.Values.Select(t => $"{t.Terrain.Name} {string.Join(' ', t.At)}"));
        return $"wears: {listed} (a unit that walks off one wears it a step, a horse or armour two, a flyer none: {string.Join(", ", longest.Select(t => t.Name))})";
    }
}
