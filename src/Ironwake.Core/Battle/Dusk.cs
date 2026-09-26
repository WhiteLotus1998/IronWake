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

    /// <summary>
    /// Whether <paramref name="unit"/> knows where <paramref name="target"/> is, the symmetric
    /// knowledge of 13.7's second arm (issue 302): its side sees the target's tile, or the
    /// target is within the wake radius of the unit itself, which is hearing, the same
    /// distance a sleeping Guard wakes at. A unit plans strikes on and paths toward only the
    /// units it knows; a strike still needs its side to see the target from where it strikes.
    /// Always true in daylight.
    /// </summary>
    public static bool Knows(BattleState state, GameContent content, BattleUnit unit, BattleUnit target) =>
        Sees(state, unit.Side, target.At) || unit.At.DistanceTo(target.At) <= content.WakeRadius;

    /// <summary>
    /// Whether an enemy's Move or Wait happens where no player unit sees it, before and after
    /// (issue 301): the console and the protocol's player view then report only that something
    /// in the dark acted, since naming the unit or its tiles would light the dark. A Move that
    /// ends in sight is reported in full. False in daylight and for any other command.
    /// </summary>
    public static bool InTheDark(BattleState state, GameContent content, Command command)
    {
        var id = command switch { Move m => m.UnitId, Wait w => w.UnitId, _ => null };
        if (Sight(state) is null || id is null || state.Find(id) is not { } unit || Seen(state, unit))
        {
            return false;
        }

        var after = Resolver.Apply(state, content, command).Next.Find(id);
        return after is null || !Seen(state, after);
    }

    /// <summary>Whether the player sees <paramref name="unit"/>: every player unit is seen, an enemy only within the player's sight.</summary>
    public static bool Seen(BattleState state, BattleUnit unit) =>
        unit.Side == Side.Player || Sees(state, Side.Player, unit.At);

    /// <summary>
    /// Whether the player may name <paramref name="unit"/> in a forecast from a tile the mover
    /// has not moved to (issue 309): seen now (<see cref="Seen(BattleState, BattleUnit)"/>), or
    /// seen with the mover read as standing on <paramref name="moverAt"/>. The mover's own
    /// tile does not count once it is read elsewhere, since it will have left it, and a unit
    /// of its side that has not moved counts only from where it stands now.
    /// </summary>
    public static bool Seen(BattleState state, BattleUnit unit, string moverId, Coord moverAt) =>
        Seen(state, unit) || Sees(state, Side.Player, unit.At, moverId, moverAt);

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
