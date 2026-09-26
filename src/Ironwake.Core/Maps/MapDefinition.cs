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
/// <param name="RivalryArm">The rivalry arm the map turns on (the <c>rivalry:</c> header, issue 16), an id in <c>rules.json</c>; null for none.</param>
/// <param name="Supplies">
/// The <c>supplies:</c> header (issue 160): every consumable stack a deployed player unit carries is
/// capped at this many uses when the map starts; null for no cap. <see cref="Supplied"/> applies it.
/// </param>
/// <param name="DifficultyId">
/// The difficulty this map is played under (issue 76), set only by <see cref="Under"/>, which has
/// already folded its level offset and Recall charges into the header; <see cref="EnemyUnit"/>
/// applies its stat percents. Null for a map as authored. A map file never declares one; the
/// <c>difficulty:</c> header exists so a battle state written out reads back under the same one.
/// </param>
/// <param name="Certification">
/// The <c>certification:</c> header (issue 73): the map is a certification trial, its one player
/// slot played in the trial's class with the trial's loadout (<see cref="Trial"/>); null for an
/// ordinary map.
/// </param>
/// <param name="Announced">
/// The <c>announce: on</c> header (issue 78): the console lists the map's events before the first
/// command and on <c>map</c> while any is still to fire, so a reinforcement turn is counted in
/// advance. Off by default, and an unannounced event's only tell is its own line when it fires.
/// </param>
/// <param name="KeepsakesEnabled">
/// The <c>keepsakes: on</c> header (DESIGN.md 13.8, experiment): a player unit that falls leaves
/// its equipped weapon on its tile under its name, for an ally to recover (<see cref="Recover"/>).
/// Off by default.
/// </param>
/// <param name="Dusk">
/// The <c>dusk:</c> header (DESIGN.md 13.7, experiment): each side's sight in tiles on turn 1,
/// one less every turn after, never under 1 (<see cref="Ironwake.Core.Dusk"/>). Null for a map
/// in daylight, where every unit sees the whole board.
/// </param>
/// <param name="ArsenalShown">
/// The <c>arsenal: on</c> header (DESIGN.md 13.11, experiment): every forecast line names the
/// weapon each side strikes with, the counter's included, so a boss carrying a heavy and a
/// light weapon shows which one it took and which one it will counter with. Display only; no
/// rule reads it. Off by default.
/// </param>
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
    bool RetreatEnabled = false,
    string? RivalryArm = null,
    int? Supplies = null,
    string? DifficultyId = null,
    CertificationTrial? Certification = null,
    bool Announced = false,
    bool KeepsakesEnabled = false,
    int? Dusk = null,
    bool ArsenalShown = false)
{
    public const int DefaultRecallCharges = 3;
    public const int DefaultEnemyLevel = 1;
    public const int MaxSide = 64;

    /// <summary>The terrain id of the seize target (glyph <c>T</c> in DESIGN.md section 4).</summary>
    public const string ThroneTerrainId = "throne";

    public bool IsExit(Coord at) => Exits.Contains(at);

    /// <summary>
    /// A player unit as this map issues it: every consumable stack (an entry of items.json;
    /// weapons and spells are not supplies) is capped at <see cref="Supplies"/> uses. A cap and
    /// never a set, so a stack already under it keeps its own, and a map can take supplies away
    /// but never hand out more than the unit carried.
    /// </summary>
    public Unit Supplied(Unit unit, GameContent content)
    {
        if (Supplies is not { } cap)
        {
            return unit;
        }

        var items = new List<ItemStack>(unit.Inventory.Count);
        foreach (var item in unit.Inventory.Items)
        {
            items.Add(content.Items.ContainsKey(item.ItemId) && item.Uses > cap ? item with { Uses = cap } : item);
        }

        return unit with { Inventory = new Inventory(ValueList<ItemStack>.From(items)) };
    }

    /// <summary>A player unit as a certification trial fields it (<see cref="CertificationTrial.Candidate"/>); unchanged on an ordinary map.</summary>
    public Unit Trial(Unit unit, GameContent content) => Certification is { } trial ? trial.Candidate(unit, content) : unit;

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
        var scaled = template.ScaledTo(EnemyLevel, content.Class(template.ClassId));
        return DifficultyId is { } id ? content.Difficulty(id).Apply(scaled) : scaled;
    }

    /// <summary>
    /// This map under a difficulty (issue 76): its enemy level floor moved by the offset and
    /// held to the level range, its Recall charges replaced when the difficulty names a count,
    /// and the difficulty recorded so <see cref="EnemyUnit"/> applies its stat percents to every
    /// placement and spawn. A difficulty is chosen once, so a map already under one refuses a second.
    /// </summary>
    public MapDefinition Under(Difficulty difficulty)
    {
        if (DifficultyId is { } current)
        {
            throw new InvalidOperationException($"map '{Name}' is already under difficulty '{current}'");
        }

        return this with
        {
            DifficultyId = difficulty.Id,
            EnemyLevel = Math.Clamp(EnemyLevel + difficulty.EnemyLevelOffset, Unit.MinLevel, Unit.MaxLevel),
            RecallCharges = difficulty.RecallCharges ?? RecallCharges,
        };
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
