namespace Ironwake.Core;

/// <summary>
/// DESIGN.md 13.7, Dusk maps (experiment): on a map with the <c>dusk:</c> header the light
/// fails one tile a turn. A side sees a tile when any of its living units stands within
/// <see cref="Sight(MapDefinition, int)"/> tiles of it, counted the way movement and ranges are, and neither side
/// may strike a unit its own side cannot see. Sight is shared: an archer at range 2 on a night
/// of sight 1 needs a friend beside the target. Counters are not strikes chosen, so a unit
/// always answers what hit it. The console draws an enemy no player unit sees as
/// <see cref="Unseen"/>, with no name, numbers or forecast. Sight is a function of the turn,
/// so a Recall rewinds it with the board.
/// </summary>
public static class Dusk
{
    /// <summary>The glyph and the name the console gives an enemy no player unit sees.</summary>
    public const char Unseen = '?';

    /// <summary>
    /// Each side's sight in tiles on <paramref name="turn"/>: the header's number on turn 1,
    /// one less each turn after, never under 1. Null on a map without the header.
    /// </summary>
    public static int? Sight(MapDefinition map, int turn) =>
        map.Dusk is { } start ? Math.Max(1, start - (turn - 1)) : null;

    /// <summary>Sight on the state's own turn; null in daylight.</summary>
    public static int? Sight(BattleState state) => Sight(state.Map, state.Turn);

    /// <summary>
    /// Whether <paramref name="side"/> sees <paramref name="at"/> on the state's turn: always in
    /// daylight, otherwise when a living unit of that side stands within sight of it. Given
    /// <paramref name="moverId"/> and <paramref name="moverAt"/>, that unit is read as standing
    /// on the tile it would strike from, which is how the enemy planner asks before it moves.
    /// </summary>
    public static bool Sees(BattleState state, Side side, Coord at, string? moverId = null, Coord? moverAt = null)
    {
        if (Sight(state) is not { } sight)
        {
            return true;
        }

        foreach (var unit in state.UnitsOf(side))
        {
            var from = unit.Id == moverId && moverAt is { } tile ? tile : unit.At;
            if (from.DistanceTo(at) <= sight)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the player sees <paramref name="unit"/>: every player unit is seen, an enemy only within the player's sight.</summary>
    public static bool Seen(BattleState state, BattleUnit unit) =>
        unit.Side == Side.Player || Sees(state, Side.Player, unit.At);

    /// <summary>The console line for a dusk map's sight on the state's turn, with the next turn's, or null in daylight.</summary>
    public static string? Line(BattleState state)
    {
        if (Sight(state) is not { } sight)
        {
            return null;
        }

        var next = Sight(state.Map, state.Turn + 1)!.Value;
        var tail = next == sight ? "; it gets no darker" : $", {next} next turn";
        var hidden = state.UnitsOf(Side.Enemy).Count(u => !Seen(state, u));
        var dark = hidden == 0 ? "" : $"; {hidden} unseen ({Unseen})";
        return $"dusk: sight {sight}{tail}{dark}; no side strikes what it cannot see";
    }
}
