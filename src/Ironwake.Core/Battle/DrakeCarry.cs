namespace Ironwake.Core;

/// <summary>
/// The drake's carry (issue 805, the Grown stage; STORY draft 6: "it carries an ally over a river or
/// a wall"). On every campaign map, main or side, and on a sample with the <c>carry:</c> header
/// (issue 1094, DECISIONS/0254), a player rider whose drake is Grown or more, who has neither moved
/// nor acted and is not grounded, lifts an ally orthogonally beside it that has neither moved nor
/// acted, flies to a tile its own Move reaches (read with the ally lifted off its tile), and sets the
/// ally down on an empty tile orthogonally beside that one which the ally can stand on. It is the
/// rider's whole turn, Move and action, and no Canto follows but the Drake Warden's long carry (issue 872). The ally lands unmoved and free to
/// move and act (the setting <c>free</c>, the keep round's, 0252), marked as shoved, so it does not
/// exit on that tile this phase (issue 396). The carry is quiet: it makes no noise beyond where the two now stand. The enemy never
/// carries, and the planner, <see cref="Resolver.Legal"/> and the Sim do not read it.
/// </summary>
public static class DrakeCarry
{
    /// <summary>Whether <paramref name="unit"/> rides a drake grown enough to carry.</summary>
    public static bool CanLift(BattleUnit unit) => unit.Unit.Drake is { Stage: >= DrakeStage.Grown };

    /// <summary>
    /// Whether the carry is open on this battle (issue 1094): on every campaign battle, main map or side
    /// map (<see cref="BattleState.InCampaign"/>), and on a map with the <c>carry:</c> header.
    /// </summary>
    public static bool Open(BattleState state) => state.Map.CarryRider is not null || state.InCampaign;

    /// <summary>
    /// Why <paramref name="rider"/> may not carry <paramref name="allyId"/> to <paramref name="to"/> and set
    /// it down on <paramref name="setDown"/>, or null when it may. Whether the rider may act at all is the
    /// caller's check. The reach is the rider's Move with the ally lifted off its tile.
    /// </summary>
    public static string? Refusal(BattleState state, GameContent content, BattleUnit rider, string allyId, Coord to, Coord setDown)
    {
        if (!Open(state))
        {
            return "the carry is open on campaign maps and carry: samples, and this is neither";
        }

        if (rider.Side != Side.Player)
        {
            return "only player units carry";
        }

        if (!CanLift(rider))
        {
            return $"{rider.Id} has no grown drake to carry with";
        }

        if (rider.Moved || rider.Shoved)
        {
            return $"{rider.Id} has already moved this phase; the carry is its whole turn";
        }

        if (rider.Grounded > 0)
        {
            return $"{rider.Id} is grounded and cannot fly";
        }

        if (state.Find(allyId) is not { } ally || ally.Side != rider.Side || ally.Id == rider.Id)
        {
            return $"no ally '{allyId}' to carry";
        }

        if (rider.At.DistanceTo(ally.At) != 1)
        {
            return $"{ally.Id} is not beside it";
        }

        if (ally.Moved || ally.Acted || ally.Shoved || ally.Resting)
        {
            return $"{ally.Id} has already moved or acted this phase";
        }

        if (Lock.Holds(state, ally))
        {
            return $"{ally.Id} is locked where it stands";
        }

        var lifted = state.WithoutUnit(ally.Id);
        var entry = lifted.ReachOf(rider, content).EntryAt(to);
        if (entry is not { CanEnd: true })
        {
            return !state.Map.Contains(to) ? $"{to} is outside the map" : $"{to} is not within {rider.Id}'s movement from {rider.At}";
        }

        if (to.DistanceTo(setDown) != 1)
        {
            return $"{setDown} is not beside {to}";
        }

        if (!state.Map.Contains(setDown))
        {
            return $"{setDown} is outside the map";
        }

        if (!state.Map.TerrainAt(setDown, content).IsPassable(content.Class(ally.Unit.ClassId).Movement))
        {
            return $"{ally.Id} cannot stand on {setDown}";
        }

        if (lifted.UnitAt(setDown) is { } blocker && blocker.Id != rider.Id)
        {
            return $"{setDown} is taken by {blocker.Id}";
        }

        return null;
    }

    /// <summary>
    /// The board's line where the carry is open and a player rider can lift: <c>carry (a grown drake's whole turn, ...): carry rook &lt;ally&gt; &lt;x,y&gt; &lt;set down x,y&gt;; the ally lands free to move and act</c>.
    /// </summary>
    public static string? Line(BattleState state)
    {
        if (!Open(state))
        {
            return null;
        }

        var riders = state.UnitsOf(Side.Player).Where(CanLift).Select(u => u.Id).ToList();
        if (riders.Count == 0)
        {
            return null;
        }

        return $"carry (a grown drake's whole turn, from beside an ally that has not moved): carry {riders[0]} <ally> <x,y> <set down x,y>; the ally lands free to move and act";
    }
}
