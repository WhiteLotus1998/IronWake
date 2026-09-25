namespace Ironwake.Core;

/// <summary>
/// A map as authored: the header, the terrain grid, and where everyone starts. This is
/// what DESIGN.md section 10 describes and what a battle is built from. It carries no
/// battle state; positions here are starting positions only.
/// </summary>
/// <param name="Name">Display name from the <c>name:</c> header.</param>
/// <param name="Width">Columns.</param>
/// <param name="Height">Rows.</param>
/// <param name="Win">The map's single win condition.</param>
/// <param name="TurnLimit">Turns before the map is lost (or, for Survive, won). At least 1.</param>
/// <param name="RecallCharges">Recall charges for the map. Section 7 says 3 on Normal.</param>
/// <param name="EnemyLevel">
/// Level enemy templates are raised to. A template already at or above it keeps its own level.
/// <see cref="EnemyUnit"/> applies it.
/// </param>
/// <param name="CheapShotsAllowed">The map waives gate 3 on purpose (section 11). Maps 4 and up only.</param>
/// <param name="TerrainIds">Terrain id per tile, row-major from the top-left.</param>
/// <param name="Placements">Starting units in file order.</param>
/// <param name="Exits">Exit tiles for an Escape map (the <c>exit:</c> header); empty otherwise.</param>
/// <param name="ProtectId">The recruit whose death loses the map (the <c>protect:</c> header), or null.</param>
/// <param name="Events">The <c>events:</c> block in file order (issue 32); empty on a map without one.</param>
/// <param name="RetreatEnabled">The map turns on enemy retreat (the <c>retreat: on</c> header, issue 33). Off by default.</param>
public sealed record MapDefinition(
    string Name,
    int Width,
    int Height,
    WinCondition Win,
    int TurnLimit,
    int RecallCharges,
    int EnemyLevel,
    bool CheapShotsAllowed,
    ValueList<string> TerrainIds,
    ValueList<Placement> Placements,
    ValueList<Coord> Exits = default,
    string? ProtectId = null,
    ValueList<MapEvent> Events = default,
    bool RetreatEnabled = false)
{
    public const int DefaultRecallCharges = 3;
    public const int DefaultEnemyLevel = 1;
    public const int MaxSide = 64;

    /// <summary>The terrain id of the seize target (glyph <c>T</c> in DESIGN.md section 4).</summary>
    public const string ThroneTerrainId = "throne";

    public bool IsExit(Coord at) => Exits.Contains(at);

    public bool IsThrone(Coord at) => TerrainIdAt(at) == ThroneTerrainId;

    public bool Contains(Coord at) => at.X >= 0 && at.X < Width && at.Y >= 0 && at.Y < Height;

    /// <summary>Terrain id at a tile. Throws for a tile outside the grid.</summary>
    public string TerrainIdAt(Coord at) =>
        Contains(at)
            ? TerrainIds[at.Y * Width + at.X]
            : throw new ArgumentOutOfRangeException(nameof(at), at, $"outside a {Width}x{Height} map");

    public Terrain TerrainAt(Coord at, GameContent content) => content.TerrainById(TerrainIdAt(at));

    /// <summary>The placement standing on a tile at the start of the map, or null.</summary>
    public Placement? PlacementAt(Coord at)
    {
        foreach (var placement in Placements)
        {
            if (placement.At == at)
            {
                return placement;
            }
        }

        return null;
    }

    /// <summary>
    /// Who stands on a tile at the start of the map, seen from <paramref name="moverSide"/>.
    /// The map view and the <c>reach</c> command use it; during a battle
    /// <see cref="BattleState.OccupantAt"/> answers from where units stand now.
    /// </summary>
    public Occupant OccupantAt(Coord at, Side moverSide) =>
        PlacementAt(at) switch
        {
            null => Occupant.None,
            var placement when placement.Side == moverSide => Occupant.Ally,
            _ => Occupant.Enemy,
        };

    /// <summary>
    /// The unit an enemy placement puts on the map: its template from the content, raised
    /// to <see cref="EnemyLevel"/> by <see cref="Unit.ScaledTo"/> on the growth of the class
    /// the template names when the template is below it. Every consumer that needs an
    /// enemy's level or stats asks here, so the floor rule has one caller to check rather
    /// than one per renderer.
    /// </summary>
    public Unit EnemyUnit(EnemyPlacement placement, GameContent content)
    {
        var template = content.Unit(placement.TemplateId);
        return template.ScaledTo(EnemyLevel, content.Class(template.ClassId));
    }

    /// <summary>This map with one tile's terrain replaced; what a <see cref="ChangeTerrain"/> event leaves behind.</summary>
    public MapDefinition WithTerrain(Coord at, string terrainId) =>
        Contains(at)
            ? this with { TerrainIds = TerrainIds.SetItem(at.Y * Width + at.X, terrainId) }
            : throw new ArgumentOutOfRangeException(nameof(at), at, $"outside a {Width}x{Height} map");

    /// <summary>The placement of every <see cref="SpawnEnemy"/> event, in file order.</summary>
    public IEnumerable<EnemyPlacement> Spawns() => Events.Select(e => e.Action).OfType<SpawnEnemy>().Select(s => s.Placement);

    /// <summary>
    /// The unit id a spawn event gives its unit: the template id and a counter that goes
    /// on from the map's placements of that template through the spawn events before it,
    /// in file order. It is fixed by the file and never by when or whether an earlier
    /// spawn fired, so ids are stable across replays and Recalls (issue 32).
    /// </summary>
    public string SpawnId(MapEvent spawn)
    {
        var template = ((SpawnEnemy)spawn.Action).Placement.TemplateId;
        var count = Placements.Count(p => p is EnemyPlacement e && e.TemplateId == template);
        foreach (var e in Events)
        {
            if (e.Action is SpawnEnemy s && s.Placement.TemplateId == template)
            {
                count++;
            }

            if (e == spawn)
            {
                return template + "-" + count;
            }
        }

        throw new ArgumentException($"event '{spawn.Name}' is not on this map", nameof(spawn));
    }

    /// <summary>The index a spawned unit's letter is drawn from: after every placement, in spawn order.</summary>
    public int SpawnIndex(MapEvent spawn)
    {
        var index = Placements.Count;
        foreach (var e in Events)
        {
            if (e == spawn)
            {
                return index;
            }

            if (e.Action is SpawnEnemy)
            {
                index++;
            }
        }

        throw new ArgumentException($"event '{spawn.Name}' is not on this map", nameof(spawn));
    }

    /// <summary>Every tile drawn with a given terrain, in row-major order.</summary>
    public IEnumerable<Coord> TilesOf(string terrainId)
    {
        for (var i = 0; i < TerrainIds.Count; i++)
        {
            if (TerrainIds[i] == terrainId)
            {
                yield return new Coord(i % Width, i / Width);
            }
        }
    }

    /// <summary>One row of the grid as terrain glyphs, the way the map file draws it.</summary>
    public string GlyphRow(int y, GameContent content)
    {
        var chars = new char[Width];
        for (var x = 0; x < Width; x++)
        {
            chars[x] = content.TerrainById(TerrainIds[y * Width + x]).Glyph;
        }

        return new string(chars);
    }
}
