namespace Ironwake.Core;

/// <summary>
/// The Guard wake check of DESIGN.md section 8, the one predicate both the rules and the
/// captain veto read (issue 128): <see cref="Resolver"/> runs it after every accepted
/// command on where units stand afterwards, and <see cref="Exposure"/> runs it on the
/// board a plan certainly produces, so the veto and the rules cannot drift apart. A
/// sleeping group wakes on the death of a member (any distance), on noise (a combat
/// whose attacker or target stood within the noise radius of a living member), or on
/// proximity (a player unit within the wake radius of a living member). Distances are
/// Manhattan and walls are not considered. The side that wakes a group is always the
/// player side, never the mover's, so an enemy-side caller wakes nothing.
/// </summary>
public static class WakeCheck
{
    /// <summary>
    /// One <see cref="GroupWoke"/> per group that was asleep on <paramref name="before"/>
    /// and wakes on <paramref name="after"/>, in group order, each naming the loudest
    /// cause in the order death, noise, proximity. <paramref name="noisy"/> holds the
    /// tiles of every combat the command fought, <paramref name="diedGroups"/> the group
    /// of every unit it killed. A Guard group a map event spawned during the command
    /// (issue 32) is asleep on <paramref name="before"/> and is checked with the rest.
    /// </summary>
    public static IReadOnlyList<GroupWoke> Run(BattleState before, BattleState after, GameContent content, IReadOnlyCollection<Coord> noisy, IReadOnlyCollection<string> diedGroups)
    {
        var sleeping = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var unit in before.Units.Concat(after.Units))
        {
            if (unit is { Behavior: Behavior.Guard, Group: { } group } && !before.IsAwake(group))
            {
                sleeping.Add(group);
            }
        }

        var woke = new List<GroupWoke>();
        foreach (var group in sleeping)
        {
            var members = after.Units.Where(u => u.Group == group).Select(u => u.At).ToList();
            WakeCause? cause = null;
            if (diedGroups.Contains(group))
            {
                cause = WakeCause.Death;
            }
            else if (noisy.Any(tile => members.Any(m => m.DistanceTo(tile) <= content.NoiseRadius)))
            {
                cause = WakeCause.Noise;
            }
            else if (after.UnitsOf(Side.Player).Any(p => members.Any(m => m.DistanceTo(p.At) <= content.WakeRadius)))
            {
                cause = WakeCause.Proximity;
            }

            if (cause is { } why)
            {
                woke.Add(new GroupWoke(group, why));
            }
        }

        return woke;
    }
}
