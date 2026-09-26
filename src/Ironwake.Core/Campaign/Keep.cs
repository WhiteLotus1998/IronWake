namespace Ironwake.Core;

/// <summary>
/// One entry on the keep's menu (issue 82, DESIGN section 13.5, an experiment): an edit that
/// turns one tile of the keep's map into <see cref="TerrainId"/> for <see cref="Price"/>, allowed
/// only on the tiles of <see cref="At"/>. A fixed placement set is what keeps it a menu and not a
/// terrain editor: content decides where a wall may be rebuilt, so no spend can wall a breach shut.
/// </summary>
public sealed record KeepEdit(string Id, string Name, string TerrainId, int Price, ValueList<Coord> At);

/// <summary>
/// The keep's map id under <c>content/keep</c> and the edits sold for it, in content order (issue 82),
/// and the raid's map id beside it (issue 288), empty when the keep has none: the raid is fought on
/// the bare keep with a smaller force from the finale's own spawn tiles, and the menu opens after it.
/// </summary>
public sealed record KeepMenu(string MapId, ValueList<KeepEdit> Edits)
{
    public static KeepMenu None { get; } = new("", ValueList<KeepEdit>.Empty);

    /// <summary>The raid's map id under <c>content/keep</c> (issue 288), or empty when the keep has no raid.</summary>
    public string RaidId { get; init; } = "";

    /// <summary>Whether <paramref name="mapId"/> is the keep or its raid, the campaign maps read from <c>content/keep</c>.</summary>
    public bool IsKeepMap(string mapId) => this != None && (mapId == MapId || (RaidId.Length > 0 && mapId == RaidId));

    /// <summary>The edit named <paramref name="id"/>, or null when the menu has none.</summary>
    public KeepEdit? Edit(string id) => Edits.FirstOrDefault(e => e.Id == id);
}

/// <summary>One edit the campaign has bought for the keep (issue 288): the menu entry's id and the tile it was made on.</summary>
public sealed record KeepWork(string EditId, Coord At)
{
    public override string ToString() => $"{EditId} {At}";
}

/// <summary>
/// Applying the keep's edits (issue 82): an edit changes one tile's terrain and nothing else, so an
/// edited keep is an ordinary map the parser, the writer and the gates read like any other.
/// </summary>
public static class Keep
{
    /// <summary>
    /// Why <paramref name="edit"/> may not be made at <paramref name="at"/> on <paramref name="map"/>,
    /// or null when it may: the tile must be in the edit's placement set, must not already be the
    /// edit's terrain, and must not hold a unit's placement.
    /// </summary>
    public static string? Refusal(MapDefinition map, KeepEdit edit, Coord at)
    {
        if (!edit.At.Contains(at))
        {
            return $"{edit.Name} goes only on {string.Join(" ", edit.At)}, not {at}";
        }

        if (map.TerrainIdAt(at) == edit.TerrainId)
        {
            return $"{at} is already {edit.TerrainId}";
        }

        if (map.Placements.Any(p => p.At == at))
        {
            return $"{at} holds a unit at the start";
        }

        return null;
    }

    /// <summary><paramref name="map"/> with <paramref name="edit"/> made at <paramref name="at"/>, or an exception naming the refusal.</summary>
    public static MapDefinition Apply(MapDefinition map, KeepEdit edit, Coord at) =>
        Refusal(map, edit, at) is { } refusal
            ? throw new InvalidOperationException($"{edit.Id} at {at}: {refusal}")
            : map.WithTerrain(at, edit.TerrainId);

    /// <summary>
    /// What making <paramref name="edit"/> at <paramref name="at"/> on <paramref name="map"/> does, in
    /// rules terms and never as advice (issue 288, DECISIONS/0059 decision 8): the terrain's name,
    /// which movement types cannot stand on it, any avoid, def, res or heal it grants, and, when the
    /// tile stands in a gap between two tiles no foot unit can enter along a row or a column, how
    /// wide that gap is before and after. Every word comes from the terrain content and the map,
    /// so changing the terrain changes the line.
    /// </summary>
    public static string Describe(MapDefinition map, KeepEdit edit, Coord at, GameContent content)
    {
        var terrain = content.TerrainById(edit.TerrainId);
        var blocked = Enum.GetValues<MovementType>().Where(m => !terrain.IsPassable(m)).ToList();
        var parts = new List<string> { terrain.Name };
        if (blocked.Count == Enum.GetValues<MovementType>().Length)
        {
            parts.Add("no unit can stand on it");
        }
        else if (blocked.Count > 0)
        {
            var open = Enum.GetValues<MovementType>().Except(blocked).Select(Word);
            parts.Add($"{Join(blocked.Select(Word))} units cannot stand on it; {Join(open)} can");
        }

        var bonuses = new List<string>();
        if (terrain.Avoid != 0)
        {
            bonuses.Add($"avoid {terrain.Avoid}");
        }

        if (terrain.Def != 0)
        {
            bonuses.Add($"def {terrain.Def}");
        }

        if (terrain.Res != 0)
        {
            bonuses.Add($"res {terrain.Res}");
        }

        if (terrain.HealPercent != 0)
        {
            bonuses.Add($"heals {terrain.HealPercent} percent");
        }

        if (bonuses.Count > 0)
        {
            parts.Add($"a unit on it gets {Join(bonuses)}");
        }

        if (Gap(map, at, content) is { } before)
        {
            var after = before.Where(c => c != at).ToList();
            var edited = map.WithTerrain(at, edit.TerrainId);
            var stillOpen = after.Where(c => edited.TerrainAt(c, content).IsPassable(MovementType.Infantry)).ToList();
            parts.Add(stillOpen.Count == before.Count
                ? $"the gap {Span(before)} stays {before.Count} {Tiles(before.Count)} wide"
                : stillOpen.Count == 0
                    ? $"the gap {Span(before)} closes"
                    : $"the gap {Span(before)} narrows from {before.Count} {Tiles(before.Count)} to {stillOpen.Count} ({string.Join(" ", stillOpen)})");
        }

        return $"{edit.Id} {at}: {string.Join("; ", parts)}";
    }

    /// <summary>
    /// The tiles of the narrowest run through <paramref name="at"/>, along its row or its column,
    /// that foot units can enter and that ends on both sides at a tile they cannot (the map's edge
    /// does not close a run), or null when neither line holds one.
    /// </summary>
    private static IReadOnlyList<Coord>? Gap(MapDefinition map, Coord at, GameContent content)
    {
        bool Open(Coord c) => map.TerrainAt(c, content).IsPassable(MovementType.Infantry);
        if (!Open(at))
        {
            return null;
        }

        IReadOnlyList<Coord>? best = null;
        foreach (var (dx, dy) in new[] { (0, 1), (1, 0) })
        {
            var run = new List<Coord> { at };
            var closed = true;
            foreach (var sign in new[] { -1, 1 })
            {
                var c = new Coord(at.X + (sign * dx), at.Y + (sign * dy));
                while (map.Contains(c) && Open(c))
                {
                    run.Add(c);
                    c = new Coord(c.X + (sign * dx), c.Y + (sign * dy));
                }

                closed &= map.Contains(c);
            }

            if (closed && (best is null || run.Count < best.Count))
            {
                best = run.OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
            }
        }

        return best;
    }

    private static string Span(IReadOnlyList<Coord> run) => run.Count == 1 ? $"at {run[0]}" : $"{run[0]} to {run[^1]}";

    private static string Tiles(int n) => n == 1 ? "tile" : "tiles";

    private static string Word(MovementType m) => m.ToString().ToLowerInvariant();

    private static string Join(IEnumerable<string> words)
    {
        var list = words.ToList();
        return list.Count <= 1 ? string.Concat(list) : string.Join(", ", list.Take(list.Count - 1)) + " and " + list[^1];
    }
}
