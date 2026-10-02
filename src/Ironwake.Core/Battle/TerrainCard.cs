namespace Ironwake.Core;

/// <summary>
/// What a terrain does for a unit standing on it, in player words (issue 610): one source for
/// the console's <c>terrain</c> command, the protocol's <c>terrain</c> query and the client's
/// legend hover. Every number is read from the <see cref="Terrain"/> record and every rule from
/// the map, never written by hand, so the card cannot drift from the rules.
/// </summary>
public static class TerrainCard
{
    /// <summary>
    /// The card for <paramref name="terrainId"/> on this battle's map: the name; on a Seize map's
    /// throne that the captain wins by standing there; the cover a unit there gets and whether
    /// flyers get it; the heal or burn at its phase start; what entering costs by movement type;
    /// then the map's own rules for this ground: wildfire on forest and fire, and the held-tile
    /// rule on a terrain an announced event will turn tiles into.
    /// </summary>
    public static string Text(BattleState state, GameContent content, string terrainId)
    {
        var terrain = content.TerrainById(terrainId);
        var map = state.Map;
        var parts = new List<string> { terrain.Name + "." };
        if (map.Win == WinCondition.Seize && terrain.Id == MapDefinition.ThroneTerrainId)
        {
            parts.Add("The captain wins by standing here.");
        }

        var walked = Enum.GetValues<MovementType>().Any(m => m != MovementType.Flying && terrain.IsPassable(m));
        var cover = new List<string>();
        if (terrain.Avoid > 0)
        {
            cover.Add($"-{terrain.Avoid} to hit a unit here");
        }

        if (terrain.Def > 0)
        {
            cover.Add($"+{terrain.Def} Def");
        }

        if (terrain.Res > 0)
        {
            cover.Add($"+{terrain.Res} Res");
        }

        if (cover.Count > 0)
        {
            parts.Add(Capital(string.Join(", ", cover)) + ".");
            if (!terrain.AppliesToFlyers && terrain.IsPassable(MovementType.Flying))
            {
                parts.Add("Flyers get none of it.");
            }
        }
        else if (walked && terrain.HealPercent <= 0 && terrain.BurnPercent <= 0)
        {
            parts.Add("No cover.");
        }

        if (terrain.HealPercent > 0)
        {
            parts.Add($"A unit here heals {terrain.HealPercent} percent of max HP at its phase start.");
        }

        if (terrain.BurnPercent > 0)
        {
            parts.Add($"A unit here loses {terrain.BurnPercent} percent of max HP at its phase start, never below 1.");
        }

        parts.Add(Costs(terrain));
        if (terrain.WearsTo is not null)
        {
            var chain = string.Join(", ", Planks.Chain(content, terrain).Select(t => t.Name));
            parts.Add($"Wears: each unit that walks off it wears it one step, a horse or armour two, a flyer none ({chain}).");
        }
        if (map.WildfireEnabled && terrain.Id == Wildfire.ForestTerrainId)
        {
            parts.Add($"On this map a {Igniters(content)} hit on a unit here sets the tile alight.");
        }

        if (map.WildfireEnabled && terrain.Id == Wildfire.FireTerrainId)
        {
            parts.Add("On this map fire burns out to plain at each player phase start and lights the forest beside it.");
        }

        if (HeldByEvents(state, terrain))
        {
            var name = terrain.Name.ToLowerInvariant();
            parts.Add($"On this map events turn ground into {name}; a unit that cannot enter {name} holds its tile, and that change is spent.");
        }

        return string.Join(" ", parts);
    }

    /// <summary>The terrain ids on the board now, row-major by first appearance: the order the client's legend draws them in.</summary>
    public static IReadOnlyList<string> OnBoard(MapDefinition map) =>
        Enumerable.Range(0, map.Height)
            .SelectMany(row => Enumerable.Range(0, map.Width).Select(col => map.TerrainIdAt(new Coord(col, row))))
            .Distinct()
            .ToList();

    /// <summary>
    /// The terrain a player names: by its glyph, its id or its name, case ignored; null when none
    /// matches.
    /// </summary>
    public static Terrain? Find(GameContent content, string text) =>
        content.Terrain.Values.FirstOrDefault(t => text.Length == 1 && t.Glyph == text[0])
            ?? content.Terrain.Values.FirstOrDefault(t => string.Equals(t.Id, text, StringComparison.OrdinalIgnoreCase))
            ?? content.Terrain.Values.FirstOrDefault(t => string.Equals(t.Name, text, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// What entering costs, grouped by cost in movement type order: <c>Costs 1 to enter.</c> when
    /// every type pays the same, <c>Costs 2 to enter on foot or armored, 3 mounted, 1 flying.</c>
    /// otherwise, then who cannot enter; <c>Only flyers can enter.</c> and <c>Nobody can enter.</c>
    /// for the ground most cannot.
    /// </summary>
    private static string Costs(Terrain terrain)
    {
        var types = Enum.GetValues<MovementType>();
        var passable = types.Where(terrain.IsPassable).ToList();
        if (passable.Count == 0)
        {
            return "Nobody can enter.";
        }

        if (passable.Count == 1 && passable[0] == MovementType.Flying)
        {
            return "Only flyers can enter.";
        }

        var groups = passable.GroupBy(m => terrain.MoveCost(m)!.Value).ToList();
        if (groups.Count == 1 && passable.Count == types.Length)
        {
            return $"Costs {groups[0].Key} to enter.";
        }

        var costs = string.Join(", ", groups.Select((g, i) => (i == 0 ? $"{g.Key} to enter " : $"{g.Key} ") + string.Join(" or ", g.Select(Words))));
        var barred = types.Where(m => !terrain.IsPassable(m)).ToList();
        return barred.Count == 0
            ? $"Costs {costs}."
            : $"Costs {costs}. {Capital(string.Join(" and ", barred.Select(Words)))} cannot enter.";
    }

    private static string Words(MovementType movement) => movement switch
    {
        MovementType.Infantry => "on foot",
        MovementType.Cavalry => "mounted",
        MovementType.Flying => "flying",
        MovementType.Armored => "armored",
        _ => throw new ArgumentOutOfRangeException(nameof(movement), movement, "unknown movement type"),
    };

    /// <summary>The weapons that light forest, by name, joined by "or"; "fire weapon" when the content has none.</summary>
    private static string Igniters(GameContent content)
    {
        var names = content.Weapons.Values.Where(w => w.Ignites).Select(w => w.Name).ToList();
        return names.Count == 0 ? "fire weapon" : string.Join(" or ", names);
    }

    /// <summary>
    /// True when an unfired event the player is told about turns a tile into this terrain and some
    /// movement type cannot enter it: the change waits on no one, and a unit that cannot stand
    /// there holds the tile (<see cref="MapEvents"/>). Unannounced events are never told.
    /// </summary>
    private static bool HeldByEvents(BattleState state, Terrain terrain) =>
        (state.Map.Announced || state.Map.Certification is not null)
            && Enum.GetValues<MovementType>().Any(m => !terrain.IsPassable(m))
            && state.Map.Events.Any(e => !state.HasFired(e.Name) && e.Action is ChangeTerrain change && change.TerrainId == terrain.Id);

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
