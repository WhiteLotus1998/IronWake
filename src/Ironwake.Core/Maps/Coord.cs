namespace Ironwake.Core;

/// <summary>
/// A tile position. X is the column from the left, Y the row from the top, both from 0.
/// Map files write it as <c>x,y</c>. Distance is Manhattan, the metric every rule in
/// DESIGN.md uses (ranges in section 5, the wake radius in section 8).
/// </summary>
public readonly record struct Coord(int X, int Y)
{
    public int DistanceTo(Coord other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    public override string ToString() => X + "," + Y;
}
