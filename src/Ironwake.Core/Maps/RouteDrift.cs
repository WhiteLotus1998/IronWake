namespace Ironwake.Core;

/// <summary>
/// The <c>route_drift:</c> header (issue 81, rounds 272 to 274; DECISIONS/0226): two Guard groups,
/// each the group of one route across the map, each with the tile where the party crosses when
/// it takes that route. The first of the two groups to wake fixes the route taken; on
/// <paramref name="Turn"/>'s enemy phase, or the first enemy phase after the route is fixed if
/// that is later, the other group wakes once and makes for the taken route's crossing, so it
/// arrives behind the party (<see cref="Routes"/>).
/// </summary>
public sealed record RouteDrift(string First, Coord FirstCrossing, string Second, Coord SecondCrossing, int Turn)
{
    /// <summary>Whether <paramref name="group"/> is one of the two route groups.</summary>
    public bool Names(string group) => group == First || group == Second;

    /// <summary>The route group that is not <paramref name="group"/>.</summary>
    public string Other(string group) => group == First ? Second : First;

    /// <summary>The crossing of the route whose group is <paramref name="group"/>.</summary>
    public Coord CrossingOf(string group) => group == First ? FirstCrossing : SecondCrossing;

    /// <summary>The header's value as <see cref="MapDefinition"/> files write it: <c>line 12,7; south 12,13; turn 6</c>.</summary>
    public override string ToString() => $"{First} {FirstCrossing}; {Second} {SecondCrossing}; turn {Turn}";
}
