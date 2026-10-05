namespace Ironwake.Core;

/// <summary>
/// The untaken route's drift (issue 81, rounds 272 to 274 on the Design Table; DECISIONS/0226):
/// on a map with a <c>route_drift:</c> header, the first of the header's two Guard groups to wake
/// fixes the route the party took (<see cref="BattleState.RouteTaken"/>). On the header's turn, or
/// the first enemy phase after that if the route is fixed later, the other group wakes, once, as
/// its enemy phase begins, and each of its members makes for the taken route's crossing: it
/// strikes what it can reach like any woken unit, and with nothing to strike it marches on the
/// crossing, the party left out of the path field as in <see cref="EnemyAi.Drift"/>, so it comes
/// up behind the party rather than racing ahead of it. A member within <see cref="ArriveRadius"/>
/// of the crossing has arrived and approaches as any woken unit does. A group already awake when
/// the drift comes due does not move for it; the drift is spent all the same. Nothing else on the
/// map changes: the other groups keep their own rules.
/// </summary>
public static class Routes
{
    /// <summary>How near the crossing, by Manhattan distance, a drifting unit has arrived.</summary>
    public const int ArriveRadius = 2;

    /// <summary>
    /// After a command's wake check: fixes the route as the first woken of the header's groups, in
    /// the order the check woke them, when no route is fixed yet. Unchanged on any other map.
    /// </summary>
    public static BattleState AfterWake(BattleState state, IEnumerable<string> woke)
    {
        if (state.Map.RouteDrift is not { } drift || state.RouteTaken is not null)
        {
            return state;
        }

        return woke.FirstOrDefault(drift.Names) is { } taken ? state with { RouteTaken = taken } : state;
    }

    /// <summary>
    /// As a phase begins: drops the drifting units that have fallen or arrived, then, as an enemy
    /// phase begins on or after the header's turn with the route fixed and the drift unspent, spends
    /// it: wakes the untaken route's group and sets its living members drifting, with one
    /// <see cref="RouteDrifted"/>, or, when that group is already awake or gone, spends it silently.
    /// </summary>
    public static BattleState AtPhaseStart(BattleState state, List<GameEvent> events)
    {
        if (state.Map.RouteDrift is not { } drift)
        {
            return state;
        }

        if (state.Drifting.Count > 0 && state.RouteTaken is { } route)
        {
            var crossing = drift.CrossingOf(route);
            var still = state.Drifting.Where(id => state.Find(id) is { } u && u.At.DistanceTo(crossing) > ArriveRadius).ToList();
            if (still.Count != state.Drifting.Count)
            {
                state = state with { Drifting = ValueList<string>.From(still) };
            }
        }

        if (state.Phase != Side.Enemy || state.Drifted || state.RouteTaken is not { } taken || state.Turn < drift.Turn)
        {
            return state;
        }

        var group = drift.Other(taken);
        var members = state.UnitsOf(Side.Enemy).Where(u => u.Group == group).Select(u => u.Id).Order(StringComparer.Ordinal).ToList();
        state = state with { Drifted = true };
        if (state.IsAwake(group) || members.Count == 0)
        {
            return state;
        }

        var to = drift.CrossingOf(taken);
        events.Add(new RouteDrifted(group, to, ValueList<string>.From(members)));
        return state.Wake(group) with { Drifting = ValueList<string>.From(members) };
    }

    /// <summary>The crossing <paramref name="unit"/> is making for, or null when it is not drifting or has arrived.</summary>
    public static Coord? CrossingFor(BattleState state, BattleUnit unit)
    {
        if (state.Map.RouteDrift is not { } drift || state.RouteTaken is not { } taken || !state.Drifting.Contains(unit.Id))
        {
            return null;
        }

        var crossing = drift.CrossingOf(taken);
        return unit.At.DistanceTo(crossing) > ArriveRadius ? crossing : null;
    }

    /// <summary>
    /// The line the board prints under the wake legend on a <c>route_drift:</c> map while the drift
    /// is still to come or under way; null once it is spent and every drifter has arrived or fallen,
    /// and null when a route group woke without fixing the route, since no drift can follow.
    /// </summary>
    public static string? Line(BattleState state)
    {
        if (state.Map.RouteDrift is not { } drift)
        {
            return null;
        }

        if (state.RouteTaken is not { } taken)
        {
            // A route group woken before any route is fixed (a seen_far sighting, issue 1044) leaves no
            // drift to come: whichever group wakes next fixes the route, and the other is already up.
            return state.IsAwake(drift.First) || state.IsAwake(drift.Second) ? null : Line(drift, state.Turn);
        }

        var group = drift.Other(taken);
        var crossing = drift.CrossingOf(taken);
        if (!state.Drifted)
        {
            if (state.IsAwake(group) || state.UnitsOf(Side.Enemy).All(u => u.Group != group))
            {
                return null;
            }

            var when = Math.Max(drift.Turn, state.Turn);
            return $"drift: {UnitNames.Group(group)} wakes on turn {when}'s enemy phase and makes for {crossing}, the crossing you took, behind you";
        }

        return state.Drifting.Count == 0 ? null : $"drift: {UnitNames.Group(group)} is making for {crossing}, the crossing you took";
    }

    /// <summary>
    /// The line before any route is fixed: both halves of the rule, the turn printed. Once
    /// <paramref name="turn"/> has passed the header's turn the drift fires on the first enemy phase
    /// after a route is fixed, so the later of the two is printed.
    /// </summary>
    public static string Line(RouteDrift drift, int turn) =>
        $"drift: on turn {Math.Max(drift.Turn, turn)}'s enemy phase the untaken route's group wakes and makes for your crossing: "
        + $"{UnitNames.Group(drift.Second)} for {drift.FirstCrossing} if {UnitNames.Group(drift.First)} wakes first, {UnitNames.Group(drift.First)} for {drift.SecondCrossing} if {UnitNames.Group(drift.Second)} does";
}
