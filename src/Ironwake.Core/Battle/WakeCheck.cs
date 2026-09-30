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
    /// <summary>Every tile in <paramref name="tiles"/> as noise at the content's radius: a combat with no signature in it.</summary>
    public static IReadOnlyCollection<Noise> At(GameContent content, params Coord[] tiles) =>
        tiles.Select(t => new Noise(t, content.NoiseRadius)).ToList();

    /// <summary>
    /// One <see cref="GroupWoke"/> per group that was asleep on <paramref name="before"/>
    /// and wakes on <paramref name="after"/>, in group order, each naming the loudest
    /// cause in the order death, noise, proximity. <paramref name="noisy"/> holds the
    /// tiles of every combat the command fought, each with its radius (the content's, or Wren's
    /// <see cref="Signatures.TalkRadius"/>, DESIGN.md 13.18), <paramref name="diedGroups"/> the group
    /// of every unit it killed. A Guard group a map event spawned during the command
    /// (issue 32) is asleep on <paramref name="before"/> and is checked with the rest.
    /// After the three causes, each group that woke calls every sleeping group the map's
    /// <c>wake_links:</c> header links it to (issue 393), transitively, and each called group
    /// follows in the order it was called, with <see cref="WakeCause.Call"/> and the caller.
    /// </summary>
    public static IReadOnlyList<GroupWoke> Run(BattleState before, BattleState after, GameContent content, IReadOnlyCollection<Noise> noisy, IReadOnlyCollection<string> diedGroups)
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
            else if (noisy.Any(noise => members.Any(m => m.DistanceTo(noise.At) <= noise.Radius)))
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

        for (var i = 0; i < woke.Count; i++)
        {
            foreach (var link in before.Map.WakeLinks.Where(l => l.From == woke[i].Group))
            {
                if (sleeping.Contains(link.To) && woke.All(w => w.Group != link.To))
                {
                    woke.Add(new GroupWoke(link.To, WakeCause.Call, CalledBy: link.From));
                }
            }
        }

        return woke;
    }
}

/// <summary>A tile a combat or a shove was fought on and the radius its noise wakes a group within (DESIGN.md section 8, 13.18).</summary>
public readonly record struct Noise(Coord At, int Radius);
