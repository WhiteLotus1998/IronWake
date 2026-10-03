namespace Ironwake.Core;

/// <summary>
/// What a carried ally may do once it is set down (issue 805, round 247): the three costs the spike
/// plays as a map's switch, one of which the keep round names.
/// </summary>
public enum CarrySetting
{
    /// <summary>The ally lands moved and acted, as if it had waited.</summary>
    Waited,

    /// <summary>The ally lands unmoved and free to act, its own Move included.</summary>
    Free,

    /// <summary>The ally lands moved, may not strike, and a Wait on the landing tile braces (DESIGN.md 13.14).</summary>
    Brace,
}

/// <summary>
/// The <c>carry:</c> header (issue 805, samples): the <see cref="CarrySetting"/> the map plays, and
/// the recruit, placed by name, whose drake begins the map at Grown at least, so a sample plays the
/// carry without a campaign behind it.
/// </summary>
public sealed record CarryRule(CarrySetting Setting, string Rider);

/// <summary>
/// The drake's carry (issue 805, the Grown stage; STORY draft 6: "it carries an ally over a river or
/// a wall"). On a <c>carry:</c> map a player rider whose drake is Grown or more, who has neither moved
/// nor acted and is not grounded, lifts an ally orthogonally beside it that has neither moved nor
/// acted, flies to a tile its own Move reaches (read with the ally lifted off its tile), and sets the
/// ally down on an empty tile orthogonally beside that one which the ally can stand on. It is the
/// rider's whole turn, Move and action, and no Canto follows but the Drover's long carry (issue 872). The ally lands as the map's
/// <see cref="CarrySetting"/> says, marked as shoved, so it does not exit on that tile this phase
/// (issue 396). The carry is quiet: it makes no noise beyond where the two now stand. The enemy never
/// carries, and the planner, <see cref="Resolver.Legal"/> and the Sim do not read it.
/// </summary>
public static class DrakeCarry
{
    /// <summary>Whether <paramref name="unit"/> rides a drake grown enough to carry.</summary>
    public static bool CanLift(BattleUnit unit) => unit.Unit.Drake is { Stage: >= DrakeStage.Grown };

    /// <summary>The setting's word as the header and the screen print it.</summary>
    public static string Word(CarrySetting setting) => setting switch
    {
        CarrySetting.Waited => "waited",
        CarrySetting.Free => "free",
        _ => "brace",
    };

    /// <summary>The setting a header word names, or null for none.</summary>
    public static CarrySetting? Parse(string word) => word switch
    {
        "waited" => CarrySetting.Waited,
        "free" => CarrySetting.Free,
        "brace" => CarrySetting.Brace,
        _ => null,
    };

    /// <summary>
    /// Why <paramref name="rider"/> may not carry <paramref name="allyId"/> to <paramref name="to"/> and set
    /// it down on <paramref name="setDown"/>, or null when it may. Whether the rider may act at all is the
    /// caller's check. The reach is the rider's Move with the ally lifted off its tile.
    /// </summary>
    public static string? Refusal(BattleState state, GameContent content, BattleUnit rider, string allyId, Coord to, Coord setDown)
    {
        if (state.Map.Carry is null)
        {
            return "this map has no carry: header";
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
    /// The board's line on a <c>carry:</c> map: <c>carry (a grown drake's whole turn): carry rook &lt;ally&gt; &lt;to&gt; &lt;set down&gt;; the ally lands free to act</c>.
    /// </summary>
    public static string? Line(BattleState state)
    {
        if (state.Map.Carry is not { } rule)
        {
            return null;
        }

        var riders = state.UnitsOf(Side.Player).Where(CanLift).Select(u => u.Id).ToList();
        if (riders.Count == 0)
        {
            return null;
        }

        var lands = rule.Setting switch
        {
            CarrySetting.Waited => "lands done for the phase",
            CarrySetting.Free => "lands free to move and act",
            _ => "lands moved, cannot strike, and braces if it waits",
        };
        return $"carry (a grown drake's whole turn, from beside an ally that has not moved): carry {riders[0]} <ally> <x,y> <set down x,y>; the ally {lands}";
    }
}
