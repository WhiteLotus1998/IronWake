namespace Ironwake.Core;

/// <summary>
/// A tile position. X is the column from the left, Y the row from the top, both from 0.
/// Map files write it as <c>x,y</c>. Distance is Manhattan, the metric every rule in
/// DESIGN.md uses (ranges in section 5, the wake radius in section 8). Ordering is
/// row-major (top row first, left to right), which is the tie-break order movement
/// uses (section 4) and the order every listing of tiles is printed in.
/// </summary>
public readonly record struct Coord(int X, int Y) : IComparable<Coord>
{
    public int DistanceTo(Coord other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    /// <summary>Row-major order: by Y, then by X.</summary>
    public int CompareTo(Coord other)
    {
        var byRow = Y.CompareTo(other.Y);
        return byRow != 0 ? byRow : X.CompareTo(other.X);
    }

    /// <summary>
    /// The four 4-connected neighbours in the fixed order north, west, east, south, which
    /// is row-major order around the tile. Callers filter tiles outside the map.
    /// </summary>
    public IEnumerable<Coord> Neighbors()
    {
        yield return new Coord(X, Y - 1);
        yield return new Coord(X - 1, Y);
        yield return new Coord(X + 1, Y);
        yield return new Coord(X, Y + 1);
    }

    public override string ToString() => X + "," + Y;
}
