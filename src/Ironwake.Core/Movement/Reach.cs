namespace Ironwake.Core;

/// <summary>
/// One tile a unit can get to within its Mov. <see cref="Path"/> lists the tiles walked
/// after the origin, in order, ending at <see cref="At"/>; the origin's own path is
/// empty. <see cref="CanEnd"/> is false for a tile an ally stands on: the unit may cross
/// it but not stop there (DESIGN.md section 4).
/// </summary>
public sealed record ReachEntry(Coord At, int Cost, ValueList<Coord> Path, bool CanEnd);

/// <summary>
/// The answer to "where can this unit move": every tile within its Mov, with the cost
/// and the path to each, in row-major order. Built by <see cref="Movement.Reach"/>.
/// Renderers and the AI ask this rather than recomputing costs, so every consumer
/// agrees on the same set and the same paths.
/// </summary>
public sealed record Reach(Coord Origin, MovementType Movement, int Mov, ValueList<ReachEntry> Entries)
{
    /// <summary>Tiles the unit may end its move on, in row-major order. Includes the origin.</summary>
    public IEnumerable<Coord> Destinations
    {
        get
        {
            foreach (var entry in Entries)
            {
                if (entry.CanEnd)
                {
                    yield return entry.At;
                }
            }
        }
    }

    /// <summary>The entry for a tile, or null if the tile is out of reach.</summary>
    public ReachEntry? EntryAt(Coord at)
    {
        var low = 0;
        var high = Entries.Count - 1;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            var order = Entries[mid].At.CompareTo(at);
            if (order == 0)
            {
                return Entries[mid];
            }

            if (order < 0)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return null;
    }

    /// <summary>True if the unit may end its move on the tile.</summary>
    public bool CanEnd(Coord at) => EntryAt(at) is { CanEnd: true };

    /// <summary>True if the unit can step onto the tile, whether or not it may stop there.</summary>
    public bool CanCross(Coord at) => EntryAt(at) is not null;

    /// <summary>Movement points spent getting to the tile, or null if out of reach.</summary>
    public int? CostTo(Coord at) => EntryAt(at)?.Cost;

    /// <summary>The tiles walked after the origin to reach the tile, or null if out of reach.</summary>
    public ValueList<Coord>? PathTo(Coord at) => EntryAt(at)?.Path;
}
