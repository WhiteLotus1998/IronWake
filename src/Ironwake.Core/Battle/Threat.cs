namespace Ironwake.Core;

/// <summary>
/// The strike set of DESIGN.md section 8, one function for every rule that asks where a
/// side can strike next phase: the retreat rule's refuge (issue 204) and the rivalry
/// spike's threatened phase (issue 209). A unit strikes from every tile it can end a move
/// on, its own tile included, when it may move next phase, and from its own tile only when
/// it holds; a sleeping Guard threatens nothing, since it acts only once its group wakes.
/// Player units always may move. Walls are respected through the reach set; the weapon
/// ranges are Manhattan, every usable weapon the unit carries counted.
/// </summary>
public static class Threat
{
    /// <summary>Every tile some unit of <paramref name="side"/> could strike next phase on the board as it stands.</summary>
    public static IReadOnlySet<Coord> StruckBy(BattleState state, GameContent content, Side side)
    {
        var struck = new HashSet<Coord>();
        foreach (var unit in state.UnitsOf(side))
        {
            foreach (var tile in StruckByUnit(state, content, unit))
            {
                struck.Add(tile);
            }
        }

        return struck;
    }

    /// <summary>Whether some unit of the other side could strike <paramref name="unit"/> where it stands next phase.</summary>
    public static bool IsThreatened(BattleState state, GameContent content, BattleUnit unit) =>
        StruckBy(state, content, unit.Side == Side.Player ? Side.Enemy : Side.Player).Contains(unit.At);

    /// <summary>The tiles one unit could strike next phase; empty for a sleeping Guard or a unit with no usable weapon.</summary>
    public static IReadOnlySet<Coord> StruckByUnit(BattleState state, GameContent content, BattleUnit unit)
    {
        var struck = new HashSet<Coord>();
        if (unit is { Behavior: Behavior.Guard, Group: { } group } && !state.IsAwake(group))
        {
            return struck;
        }

        var weapons = Enumerable.Range(0, unit.Unit.Inventory.Count)
            .Select(slot => unit.UsableWeaponAt(content, slot))
            .OfType<Weapon>()
            .ToList();
        if (weapons.Count == 0)
        {
            return struck;
        }

        var mayMove = unit.Side == Side.Player || state.EffectiveBehavior(unit, content) == Behavior.Aggressive;
        var origins = mayMove
            ? state.ReachOf(unit, content).Destinations.Append(unit.At).Distinct()
            : new[] { unit.At };
        var maxRange = weapons.Max(w => w.MaxRange);
        foreach (var from in origins)
        {
            for (var dy = -maxRange; dy <= maxRange; dy++)
            {
                for (var dx = -maxRange; dx <= maxRange; dx++)
                {
                    var tile = new Coord(from.X + dx, from.Y + dy);
                    var distance = Math.Abs(dx) + Math.Abs(dy);
                    if (state.Map.Contains(tile) && weapons.Any(w => w.InRange(distance)))
                    {
                        struck.Add(tile);
                    }
                }
            }
        }

        return struck;
    }
}
