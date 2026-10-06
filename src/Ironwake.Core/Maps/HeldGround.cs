namespace Ironwake.Core;

/// <summary>
/// The <c>holds:</c> header (issue 1189, round 402): an enemy group and the rectangle of ground it
/// holds, corners <paramref name="From"/> and <paramref name="To"/> inclusive. A woken member steps
/// off it only to strike: with no strike on offer it approaches over its ground alone and walks back
/// to it (<see cref="EnemyAi"/>), so a woken Guard group waits on its ground instead of marching out,
/// and a unit that strikes it from off the ground is answered as anywhere else. Strikes, and so
/// <c>threat</c> and the exposure sum, are untouched.
/// </summary>
public sealed record HeldGround(string Group, Coord From, Coord To)
{
    /// <summary>Whether <paramref name="at"/> lies inside the rectangle.</summary>
    public bool Contains(Coord at) =>
        at.X >= Math.Min(From.X, To.X) && at.X <= Math.Max(From.X, To.X)
        && at.Y >= Math.Min(From.Y, To.Y) && at.Y <= Math.Max(From.Y, To.Y);

    /// <summary>Manhattan distance from <paramref name="at"/> to the nearest tile of the rectangle, 0 inside it.</summary>
    public int DistanceTo(Coord at) =>
        Math.Max(0, Math.Max(Math.Min(From.X, To.X) - at.X, at.X - Math.Max(From.X, To.X)))
        + Math.Max(0, Math.Max(Math.Min(From.Y, To.Y) - at.Y, at.Y - Math.Max(From.Y, To.Y)));

    /// <summary>Whether the rule binds <paramref name="unit"/>: an enemy of the held group.</summary>
    public bool Binds(BattleUnit unit) => unit.Side == Side.Enemy && unit.Group == Group;

    /// <summary>The header's value as <see cref="MapDefinition"/> files write it: <c>mill 0,0 11,2</c>.</summary>
    public override string ToString() => $"{Group} {From.X},{From.Y} {To.X},{To.Y}";

    /// <summary>
    /// The rule line under the unit rows:
    /// <c>holds: the mill group leaves 0,0 to 11,2 only to strike, then goes back</c>.
    /// </summary>
    public string Line() =>
        $"holds: the {Group} group leaves {From.X},{From.Y} to {To.X},{To.Y} only to strike, then goes back";
}
