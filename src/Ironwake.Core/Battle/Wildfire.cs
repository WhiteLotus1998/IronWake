namespace Ironwake.Core;

/// <summary>
/// Wildfire (DESIGN.md 13.15, experiment), behind a map's <c>wildfire: on</c> header. A hit
/// from an igniting weapon (<see cref="Weapon.Ignites"/>) on a unit standing on forest sets
/// the tile alight once the combat ends, so the exchange itself is fought on the forest. At
/// the start of each side's phase that side's units on fire burn first; then, at a player
/// phase start only, every fire tile burns out to plain and sets alight each forest tile
/// orthogonally beside it: a front walks through a wood one
/// tile a round and leaves it bare. The burn itself is terrain (<see cref="Terrain.BurnPercent"/>)
/// and the resolver applies it where it applies a fort's heal. Both sides ignite alike. The
/// map is part of the state, so Recall restores the fire with the board.
/// </summary>
public static class Wildfire
{
    /// <summary>The terrain a wood burns into.</summary>
    public const string FireTerrainId = "fire";

    /// <summary>The only terrain that catches.</summary>
    public const string ForestTerrainId = "forest";

    /// <summary>The terrain a fire leaves behind.</summary>
    public const string BurntTerrainId = "plain";

    /// <summary>
    /// After a combat fought with <paramref name="weapon"/> by <paramref name="strikerId"/>:
    /// on a <c>wildfire: on</c> map, if any of the striker's strikes hit, and the target's
    /// tile <paramref name="at"/> is forest, the tile becomes fire, with one
    /// <see cref="TerrainChanged"/>. The target's tile is read from the combat, so a target
    /// that died still leaves the wood alight.
    /// </summary>
    public static BattleState AfterCombat(BattleState state, string strikerId, Weapon? weapon, Coord at, ValueList<StrikeEvent> strikes, List<GameEvent> events)
    {
        if (!state.Map.WildfireEnabled || weapon is not { Ignites: true } || state.Map.TerrainIdAt(at) != ForestTerrainId)
        {
            return state;
        }

        if (!strikes.Any(s => s.AttackerId == strikerId && s.Hit))
        {
            return state;
        }

        events.Add(new TerrainChanged(at, FireTerrainId));
        return state with { Map = state.Map.WithTerrain(at, FireTerrainId) };
    }

    /// <summary>
    /// The tiles a unit should not end on, on a <c>wildfire: on</c> map: every tile burning now
    /// and every forest tile the next front lights (<see cref="NextFront"/>). Empty on any other
    /// map. The enemy planner's stop choices rank these below every other tile (DESIGN.md 13.15).
    /// </summary>
    public static IReadOnlySet<Coord> Scorched(MapDefinition map) =>
        !map.WildfireEnabled ? Empty : Burning(map).Concat(NextFront(map)).ToHashSet();

    /// <summary>The tiles burning now, row-major.</summary>
    public static IReadOnlyList<Coord> Burning(MapDefinition map)
    {
        var burning = new List<Coord>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (map.TerrainIdAt(new Coord(x, y)) == FireTerrainId)
                {
                    burning.Add(new Coord(x, y));
                }
            }
        }

        return burning;
    }

    /// <summary>
    /// The forest tiles the next player phase start sets alight on a <c>wildfire: on</c> map:
    /// each orthogonally beside a tile burning now, row-major. Empty on any other map.
    /// </summary>
    public static IReadOnlyList<Coord> NextFront(MapDefinition map) =>
        !map.WildfireEnabled
            ? Array.Empty<Coord>()
            : Burning(map).SelectMany(c => c.Neighbors())
                .Where(c => map.Contains(c) && map.TerrainIdAt(c) == ForestTerrainId)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

    private static readonly IReadOnlySet<Coord> Empty = new HashSet<Coord>();

    /// <summary>
    /// The front's step at the start of a player phase, after the player's units on fire have
    /// burned: every fire tile becomes plain, and every forest tile orthogonally beside one of
    /// them becomes fire, all read from the board before the step, one <see cref="TerrainChanged"/>
    /// per tile in row-major order. A tile lit here harms nobody until the next phase start.
    /// </summary>
    public static BattleState Spread(BattleState state, List<GameEvent> events)
    {
        var map = state.Map;
        if (!map.WildfireEnabled)
        {
            return state;
        }

        var burning = Burning(map);
        if (burning.Count == 0)
        {
            return state;
        }

        var catching = NextFront(map);
        var changes = burning.Select(c => (At: c, To: BurntTerrainId))
            .Concat(catching.Select(c => (At: c, To: FireTerrainId)))
            .OrderBy(change => change.At)
            .ToList();
        foreach (var (at, to) in changes)
        {
            events.Add(new TerrainChanged(at, to));
            map = map.WithTerrain(at, to);
        }

        return state with { Map = map };
    }
}
