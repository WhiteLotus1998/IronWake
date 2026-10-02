namespace Ironwake.Core;

/// <summary>
/// A front (issue 692): a named breach on a map with a <c>fronts:</c> header, its tiles in file
/// order. It falls the first time an enemy stands on or moves through any of them, and stays
/// fallen; a fall never loses the map, and it fires the events whose trigger is
/// <see cref="FallsTrigger"/> on it. The name is one word; an underscore prints as a space.
/// </summary>
public sealed record Front(string Name, ValueList<Coord> Tiles)
{
    /// <summary>The front as the board names it: <c>west_breach</c> reads <c>west breach</c>.</summary>
    public string Words => Name.Replace('_', ' ');
}

/// <summary>
/// When the front named <paramref name="Front"/> falls (issue 692): every event with this trigger
/// fires then, in file order. A spawn on it may land on any tile, not only an edge, since it is the
/// wave coming in through the breach. A map whose <c>fronts:</c> header does not name the front may
/// not use it.
/// </summary>
public sealed record FallsTrigger(string Front) : MapEventTrigger;
