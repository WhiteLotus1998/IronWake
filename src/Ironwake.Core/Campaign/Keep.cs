namespace Ironwake.Core;

/// <summary>
/// One entry on the keep's menu (issue 82, DESIGN section 13.5, an experiment): an edit that
/// turns one tile of the keep's map into <see cref="TerrainId"/> for <see cref="Price"/>, allowed
/// only on the tiles of <see cref="At"/>. A fixed placement set is what keeps it a menu and not a
/// terrain editor: content decides where a wall may be rebuilt, so no spend can wall a breach shut.
/// </summary>
public sealed record KeepEdit(string Id, string Name, string TerrainId, int Price, ValueList<Coord> At);

/// <summary>
/// One room the keep sells (issue 687, DESIGN section 13.20, DECISIONS/0137): <see cref="Beds"/>
/// more beds for <see cref="Price"/>, bought at most <see cref="Max"/> times, from the purse the
/// keep's edits spend. A room changes no tile; it changes who may join.
/// </summary>
public sealed record KeepRoom(string Id, string Name, int Price, int Beds, int Max)
{
    /// <summary>Whether this room is the forge (issue 647), where <c>refine</c> works; it may add no beds.</summary>
    public bool Forge { get; init; }

    /// <summary>
    /// The campaign map after whose win the room may be built (issue 647: the forge opens once the
    /// smith is met), or empty for a room open from map 1.
    /// </summary>
    public string After { get; init; } = "";

    /// <summary>
    /// The room that must be built before this one (issue 690: the barracks' upgrade needs the
    /// barracks), or empty for a room that stands alone.
    /// </summary>
    public string Requires { get; init; } = "";

    /// <summary>The hires this room puts on the barracks' list once built (issue 690), by id, in content order.</summary>
    public ValueList<string> Hires { get; init; } = ValueList<string>.Empty;
}

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

    /// <summary>
    /// The beds the keep starts with (issue 687, DESIGN section 13.20), or 0 when the campaign
    /// counts none and every meeting joins. Every member who has joined holds one, living or fallen.
    /// </summary>
    public int Beds { get; init; }

    /// <summary>The rooms sold for the keep (issue 687), in content order; bought at any camp from map 1.</summary>
    public ValueList<KeepRoom> Rooms { get; init; } = ValueList<KeepRoom>.Empty;

    /// <summary>
    /// The soldiers the barracks may hire (issue 690), in content order: each joins the roster
    /// as a unit built by <see cref="Barracks.Recruit"/> once the room that lists it is built.
    /// </summary>
    public ValueList<KeepHire> Hires { get; init; } = ValueList<KeepHire>.Empty;

    /// <summary>What one hire costs from the purse (issue 690), or 0 when the keep hires nobody.</summary>
    public int HirePrice { get; init; }

    /// <summary>The hire named <paramref name="id"/>, or null when the barracks lists none.</summary>
    public KeepHire? Hire(string id) => Hires.FirstOrDefault(h => h.Id == id);

    /// <summary>The room whose list carries the hire <paramref name="id"/>, or null when no room lists it.</summary>
    public KeepRoom? RoomHiring(string id) => Rooms.FirstOrDefault(r => r.Hires.Contains(id));

    /// <summary>The room named <paramref name="id"/>, or null when the keep sells none.</summary>
    public KeepRoom? Room(string id) => Rooms.FirstOrDefault(r => r.Id == id);

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
