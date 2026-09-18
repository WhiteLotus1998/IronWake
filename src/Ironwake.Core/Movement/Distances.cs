namespace Ironwake.Core;

/// <summary>
/// The answer to <see cref="Movement.DistancesTo"/>: for every tile, the remaining path
/// cost to the nearest target with no Mov budget, or null when no target can be reached
/// from it. Cost is what the mover would spend walking on from the tile, so a target's
/// own distance is 0.
/// </summary>
public sealed class Distances
{
    private readonly MapDefinition _map;
    private readonly int[] _costs;

    internal Distances(MapDefinition map, int[] costs)
    {
        _map = map;
        _costs = costs;
    }

    /// <summary>Remaining path cost from a tile to the nearest target, or null if none is reachable. Throws for a tile outside the map.</summary>
    public int? From(Coord at)
    {
        if (!_map.Contains(at))
        {
            throw new ArgumentOutOfRangeException(nameof(at), at, $"outside a {_map.Width}x{_map.Height} map");
        }

        var cost = _costs[at.Y * _map.Width + at.X];
        return cost == int.MaxValue ? null : cost;
    }
}
