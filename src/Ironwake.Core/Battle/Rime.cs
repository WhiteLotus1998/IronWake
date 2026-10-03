namespace Ironwake.Core;

/// <summary>
/// A tile of Rime ice the breath made (issue 805, experiment): where it is, the side whose breath
/// made it, and its thaw clock, counted as the chill's is (<see cref="Frost.AtPhaseChange"/>):
/// <c>1</c> from the breath until that side's next phase begins, <c>2</c> through that phase, and
/// <c>3</c> once it is due but a unit stands on it.
/// </summary>
public sealed record RimeTile(Coord At, Side Side, int Clock);

/// <summary>
/// The drake's rime breath (issue 805, the Unbroken stage; STORY draft 6). On a <c>breath:</c> map a
/// player rider whose drake is Unbroken breathes once a map, as its action, after a move or without
/// one: a line of <see cref="Reach"/> tiles straight out from it through the orthogonally adjacent
/// tile it names. Every unit on the line, of either side, is chilled (<see cref="Frost"/>: Mov -1,
/// never below 1, on the chill's clock). Every Water tile on the line turns to Rime ice
/// (<see cref="TerrainId"/>), ground anyone can walk, which thaws back to Water
/// (<see cref="Terrain.ThawsTo"/>) when the breather's side's next phase ends; a tile a unit stands
/// on then holds until a phase change finds it empty, so nobody is left standing on water. The ice
/// does not wear under feet, so both sides may cross it while it holds. No damage, no roll, no
/// Canto after it. The breath makes no noise of its own; the wake check reads the board after it
/// like any command. The enemy never breathes, and the planner, <see cref="Resolver.Legal"/> and the
/// Sim do not read it.
/// </summary>
public static class Rime
{
    /// <summary>How many tiles the line runs from the rider.</summary>
    public const int Reach = 3;

    /// <summary>The terrain the breath turns Water into.</summary>
    public const string TerrainId = "rime";

    /// <summary>The terrain the breath freezes.</summary>
    public const string WaterId = "water";

    /// <summary>Whether <paramref name="unit"/> rides a drake old enough to breathe.</summary>
    public static bool CanBreathe(BattleUnit unit) => unit.Unit.Drake is { Stage: >= DrakeStage.Unbroken };

    /// <summary>
    /// The line from <paramref name="from"/> through <paramref name="toward"/>, which must be orthogonally
    /// beside it: up to <see cref="Reach"/> tiles, stopping at the map's edge.
    /// </summary>
    public static IReadOnlyList<Coord> LineOf(MapDefinition map, Coord from, Coord toward)
    {
        var dx = toward.X - from.X;
        var dy = toward.Y - from.Y;
        var line = new List<Coord>();
        for (var step = 1; step <= Reach; step++)
        {
            var at = new Coord(from.X + dx * step, from.Y + dy * step);
            if (!map.Contains(at))
            {
                break;
            }

            line.Add(at);
        }

        return line;
    }

    /// <summary>
    /// Why <paramref name="rider"/> may not breathe toward <paramref name="toward"/>, or null when it may.
    /// Whether the rider may act at all is the caller's check.
    /// </summary>
    public static string? Refusal(BattleState state, BattleUnit rider, Coord toward)
    {
        if (state.Map.BreathRider is null)
        {
            return "this map has no breath: header";
        }

        if (rider.Side != Side.Player)
        {
            return "only player units breathe";
        }

        if (!CanBreathe(rider))
        {
            return $"{rider.Id} has no unbroken drake to breathe";
        }

        if (rider.Breathed)
        {
            return $"{rider.Id}'s drake has breathed this map (once a map)";
        }

        if (!state.Map.Contains(toward))
        {
            return $"{toward} is outside the map";
        }

        if (rider.At.DistanceTo(toward) != 1)
        {
            return $"{toward} is not beside {rider.Id}; name the first tile of the line";
        }

        return null;
    }

    /// <summary>
    /// The breath on <paramref name="state"/>, already checked: the rider marked as having moved, acted
    /// and breathed; <see cref="Breathed"/>; each unit on the line chilled (<see cref="UnitChilled"/>);
    /// each Water tile frozen (<see cref="TerrainChanged"/>) and put on the clock.
    /// </summary>
    public static BattleState Apply(BattleState state, GameContent content, BattleUnit rider, Coord toward, List<GameEvent> events)
    {
        var line = LineOf(state.Map, rider.At, toward);
        var frozen = line.Where(at => state.Map.TerrainAt(at, content).Id == WaterId).ToList();
        var struck = line.Select(state.UnitAt).OfType<BattleUnit>().ToList();
        events.Add(new Breathed(rider.Id, rider.At, ValueList<Coord>.From(line), ValueList<string>.From(struck.Select(u => u.Id)), ValueList<Coord>.From(frozen)));
        state = state.WithUnit(rider with { Moved = true, Acted = true, Canto = null, Braced = false, Breathed = true });
        foreach (var unit in struck)
        {
            events.Add(new UnitChilled(unit.Id, rider.Id, unit.Side, unit.Side == state.Phase));
            state = state.WithUnit(state.Find(unit.Id)! with { Chill = 1 });
        }

        var tiles = state.Rime.Where(r => !frozen.Contains(r.At)).ToList();
        foreach (var at in frozen)
        {
            events.Add(new TerrainChanged(at, TerrainId));
            state = state with { Map = state.Map.WithTerrain(at, TerrainId) };
            tiles.Add(new RimeTile(at, rider.Side, 1));
        }

        return state with { Rime = ValueList<RimeTile>.From(tiles.OrderBy(r => r.At.Y).ThenBy(r => r.At.X)) };
    }

    /// <summary>
    /// The ice across a phase change from <paramref name="ended"/> to <paramref name="begins"/>: a tile no
    /// longer Rime ice leaves the clock; a tile of the side whose phase begins turns 1 to 2; a tile at 2
    /// whose side's phase ended, or one already due, thaws to its <see cref="Terrain.ThawsTo"/> when no unit
    /// stands on it (<see cref="TerrainChanged"/>), and waits at 3 when one does.
    /// </summary>
    public static BattleState AtPhaseChange(BattleState state, GameContent content, Side ended, Side begins, List<GameEvent> events)
    {
        if (state.Rime.Count == 0)
        {
            return state;
        }

        var kept = new List<RimeTile>();
        var map = state.Map;
        foreach (var tile in state.Rime)
        {
            var terrain = map.TerrainAt(tile.At, content);
            if (terrain.ThawsTo is not { } thawsTo)
            {
                continue;
            }

            var due = tile.Clock >= 3 || (tile.Clock == 2 && tile.Side == ended);
            if (due)
            {
                if (state.UnitAt(tile.At) is null)
                {
                    events.Add(new TerrainChanged(tile.At, thawsTo));
                    map = map.WithTerrain(tile.At, thawsTo);
                }
                else
                {
                    kept.Add(tile with { Clock = 3 });
                }

                continue;
            }

            kept.Add(tile.Clock == 1 && tile.Side == begins ? tile with { Clock = 2 } : tile);
        }

        return state with { Map = map, Rime = ValueList<RimeTile>.From(kept) };
    }

    /// <summary>
    /// The board's line on a <c>breath:</c> map while a rider can still breathe:
    /// <c>breath (an unbroken drake's action, once a map): breathe rook &lt;x,y beside her&gt;; ...</c>,
    /// and while ice holds, where and when it thaws.
    /// </summary>
    public static string? Line(BattleState state)
    {
        if (state.Map.BreathRider is null)
        {
            return null;
        }

        var parts = new List<string>();
        var ready = state.UnitsOf(Side.Player).Where(u => CanBreathe(u) && !u.Breathed).Select(u => u.Id).ToList();
        if (ready.Count > 0)
        {
            parts.Add($"breath (an unbroken drake's action, once a map): breathe {ready[0]} <x,y beside it>; a line of {Reach} tiles chills everyone on it, either side, and freezes Water to Rime ice until the player's next phase ends");
        }

        if (state.Rime.Count > 0)
        {
            var groups = state.Rime.GroupBy(r => Thaw(state, r)).Select(g => string.Join(" ", g.Select(r => r.At)) + " (" + g.Key + ")");
            parts.Add("rime: " + string.Join("; ", groups));
        }

        return parts.Count == 0 ? null : string.Join("\n", parts);
    }

    /// <summary>When <paramref name="tile"/> thaws, in player words.</summary>
    public static string Thaw(BattleState state, RimeTile tile) =>
        tile.Clock >= 3 ? "thaws once no one stands on it"
        : $"thaws as {Frost.Until(tile.Side, tile.Clock == 1 && tile.Side == state.Phase)}";
}
