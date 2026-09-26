namespace Ironwake.Core;

/// <summary>
/// One entry on the keep's menu (issue 82, DESIGN section 13.5, an experiment): an edit that
/// turns one tile of the keep's map into <see cref="TerrainId"/> for <see cref="Price"/>, allowed
/// only on the tiles of <see cref="At"/>. A fixed placement set is what keeps it a menu and not a
/// terrain editor: content decides where a wall may be rebuilt, so no spend can wall a breach shut.
/// </summary>
public sealed record KeepEdit(string Id, string Name, string TerrainId, int Price, ValueList<Coord> At);

/// <summary>The keep's map id under <c>content/keep</c> and the edits sold for it, in content order (issue 82).</summary>
public sealed record KeepMenu(string MapId, ValueList<KeepEdit> Edits)
{
    public static KeepMenu None { get; } = new("", ValueList<KeepEdit>.Empty);

    /// <summary>The edit named <paramref name="id"/>, or null when the menu has none.</summary>
    public KeepEdit? Edit(string id) => Edits.FirstOrDefault(e => e.Id == id);
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
}
