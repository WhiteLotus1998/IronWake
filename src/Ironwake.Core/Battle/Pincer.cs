namespace Ironwake.Core;

/// <summary>
/// The pincer (DESIGN.md 13.13, experiment), behind a map's <c>pincer: on</c> header: a unit
/// struck from an orthogonally adjacent tile, while a living unit of the striker's side stands
/// on the tile directly behind it (the target's tile plus the step from striker to target),
/// is pinned, and the striker's hit rises by <see cref="Hit"/>. It holds on both sides and on
/// counters, so a unit that attacks into a pair of foes is pinned on the answer. A ranged
/// strike never pins. It sits in rivalry's hit slot (<see cref="Combatant.HitModifier"/>), so
/// every forecast, <c>threat</c>, the planner's score and the resolver read one number.
/// Units are matched by id, so a forecast may pass the striker at a tile it has not moved to.
/// </summary>
public static class Pincer
{
    /// <summary>The hit a pinning strike gains (DESIGN.md 13.13, provisional).</summary>
    public const int Hit = 15;

    /// <summary>
    /// The unit that pins <paramref name="target"/> against <paramref name="striker"/> on this
    /// board, or null: none on a map without the header, at range other than 1, or when the
    /// tile behind the target is off the map, empty or held by the target's side.
    /// </summary>
    public static BattleUnit? PinnedBy(BattleState state, BattleUnit striker, BattleUnit target)
    {
        if (!state.Map.PincerEnabled || striker.Side == target.Side || striker.At.DistanceTo(target.At) != 1)
        {
            return null;
        }

        var behind = new Coord(2 * target.At.X - striker.At.X, 2 * target.At.Y - striker.At.Y);
        return state.Units.FirstOrDefault(u => u.At == behind && u.Side == striker.Side && u.Id != striker.Id && u.Id != target.Id);
    }

    /// <summary>The hit modifier <paramref name="striker"/> fights <paramref name="target"/> with: <see cref="Hit"/> when pinned, else 0.</summary>
    public static int HitAgainst(BattleState state, BattleUnit striker, BattleUnit? target) =>
        target is not null && PinnedBy(state, striker, target) is not null ? Hit : 0;
}
