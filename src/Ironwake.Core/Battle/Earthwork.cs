namespace Ironwake.Core;

/// <summary>
/// A timed terrain overlay on one tile (issue 1245): the terrain it stands in for while it lasts
/// (<paramref name="TerrainId"/>), the terrain it lies on and gives back (<paramref name="UnderId"/>), the
/// unit that laid it, its side, and its clock, counted as the chill's is (<see cref="Frost.AtPhaseChange"/>):
/// <c>1</c> from the cast until that side's next phase begins, <c>2</c> through that phase; it falls as that
/// phase ends. The battle's map carries the overlay's terrain while it lasts, so every rule that reads
/// terrain (move cost, Def, Avo, heal, burn, the forecast, <c>threat</c>, the planners) reads it with no
/// rule of its own; the map file is never touched. A fallen owner's overlay lasts out its clock.
/// </summary>
public sealed record TileOverlay(Coord At, string TerrainId, string UnderId, string OwnerId, Side Side, int Clock);

/// <summary>
/// Earth's raise rider (issue 1245, Chat round 416, Code round 417): a tome that names its school's
/// <see cref="RiderKind.Raise"/> rider is cast through the Item action on an ally in its range, as a heal
/// is, and lays the rider's terrain (<see cref="SchoolRider.Terrain"/>, <c>earthwork</c> in the shipped
/// content: a fort's cover with no heal) on the ally's tile as a <see cref="TileOverlay"/> until the end of
/// the caster's next phase. One per caster: a second cast takes the first one up. Whoever stands on it holds
/// it, either side. It rises only on open ground (<see cref="OpenGround"/>), never on a fort or gate, water,
/// a wall, a mountain, fire, planks or ice, so geography never retiles a tuned map. It spends one use and
/// the caster's action and earns no EXP. The enemy lays it in place of a strike that is not a kill (<see cref="EnemyAi.Rampart"/>, issue 1286); <see cref="Resolver.Legal"/> and the
/// Sim's player do not offer it; both read a raised tile as terrain.
/// </summary>
public static class Earthwork
{
    /// <summary>The raise rider <paramref name="spell"/> carries from its school, or null.</summary>
    public static SchoolRider? Rider(GameContent content, Weapon? spell) =>
        content.RiderOf(spell) is { Kind: RiderKind.Raise, Terrain: not null } rider ? rider : null;

    /// <summary>
    /// Whether <paramref name="terrain"/> is ground an overlay may rise on: every movement type may enter
    /// it, it neither heals nor burns, and it neither wears nor thaws.
    /// </summary>
    public static bool OpenGround(Terrain terrain) =>
        Enum.GetValues<MovementType>().All(terrain.IsPassable)
        && terrain.HealPercent <= 0 && terrain.BurnPercent <= 0 && terrain.WearsTo is null && terrain.ThawsTo is null;

    /// <summary>The overlay on <paramref name="at"/>, or null.</summary>
    public static TileOverlay? At(BattleState state, Coord at) => state.Overlays.FirstOrDefault(o => o.At == at);

    /// <summary>
    /// <paramref name="caster"/> raises <paramref name="rider"/>'s terrain under <paramref name="target"/> with the
    /// tome in <paramref name="slot"/>: the use and range are the caller's checks. Refused when the tile is another
    /// caster's overlay or is not open ground once the caster's own overlay is taken up. Otherwise the caster's
    /// old overlay gives its tile back (<see cref="TerrainChanged"/>), the new one is laid (<see cref="ItemUsed"/>,
    /// <see cref="GroundRaised"/>, <see cref="TerrainChanged"/>), and the caster has moved and acted.
    /// </summary>
    public static (BattleState, Rejection?) Raise(
        BattleState state, GameContent content, BattleUnit caster, int slot, Weapon spell, SchoolRider rider, BattleUnit target, List<GameEvent> events)
    {
        var at = target.At;
        if (At(state, at) is { } other && other.OwnerId != caster.Id)
        {
            return (state, new Rejection(RejectionReason.NotUsable, $"{at} is already raised by {other.OwnerId}; it falls as {Falls(state, other)}"));
        }

        var map = state.Map;
        var lifted = new List<GameEvent>();
        if (state.Overlays.FirstOrDefault(o => o.OwnerId == caster.Id) is { } old && map.TerrainIdAt(old.At) == old.TerrainId)
        {
            map = map.WithTerrain(old.At, old.UnderId);
            lifted.Add(new TerrainChanged(old.At, old.UnderId));
        }

        var under = map.TerrainAt(at, content);
        if (!OpenGround(under))
        {
            return (state, new Rejection(
                RejectionReason.NotUsable,
                $"{spell.Name} raises ground only on open ground; {under.Name} at {at} is not (never a fort, gate, water, wall, mountain, fire, planks or ice)"));
        }

        var stack = caster.Unit.Inventory.Items[slot];
        var usesLeft = stack.Uses - 1;
        events.Add(new ItemUsed(caster.Id, spell.Id, target.Id, usesLeft));
        events.AddRange(lifted);
        events.Add(new GroundRaised(caster.Id, target.Id, at, rider.Terrain!));
        events.Add(new TerrainChanged(at, rider.Terrain!));
        if (usesLeft == 0)
        {
            events.Add(new SpellSpent(caster.Id, spell.Id));
        }

        var overlays = state.Overlays.Where(o => o.OwnerId != caster.Id)
            .Append(new TileOverlay(at, rider.Terrain!, under.Id, caster.Id, caster.Side, 1))
            .OrderBy(o => o.At.Y).ThenBy(o => o.At.X);
        var next = state with { Map = map.WithTerrain(at, rider.Terrain!), Overlays = ValueList<TileOverlay>.From(overlays) };
        var after = caster with { Moved = true, Acted = true, Unit = caster.Unit with { Inventory = caster.Unit.Inventory.Replace(slot, stack with { Uses = usesLeft }) } };
        return (next.WithUnit(after), null);
    }

    /// <summary>
    /// The overlays across a phase change from <paramref name="ended"/> to <paramref name="begins"/>: an overlay
    /// of the side whose phase begins turns 1 to 2; one at 2 whose side's phase ended falls, giving its tile back
    /// (<see cref="TerrainChanged"/>), whoever stands there. A tile something else retiled meanwhile (a map event)
    /// keeps what it became and the overlay is dropped.
    /// </summary>
    public static BattleState AtPhaseChange(BattleState state, Side ended, Side begins, List<GameEvent> events)
    {
        if (state.Overlays.Count == 0)
        {
            return state;
        }

        var kept = new List<TileOverlay>();
        var map = state.Map;
        foreach (var overlay in state.Overlays)
        {
            if (map.TerrainIdAt(overlay.At) != overlay.TerrainId)
            {
                continue;
            }

            var clock = Frost.AtPhaseChange(overlay.Clock, overlay.Side, ended, begins);
            if (clock == 0)
            {
                events.Add(new TerrainChanged(overlay.At, overlay.UnderId));
                map = map.WithTerrain(overlay.At, overlay.UnderId);
                continue;
            }

            kept.Add(overlay with { Clock = clock });
        }

        return state with { Map = map, Overlays = ValueList<TileOverlay>.From(kept) };
    }

    /// <summary>
    /// When <paramref name="overlay"/> falls, in player words: <c>the next player phase ends</c> on the phase it
    /// was cast, <c>the player phase ends</c> through the other side's, <c>this player phase ends</c> through its last.
    /// </summary>
    public static string Falls(BattleState state, TileOverlay overlay) =>
        (overlay.Side != state.Phase ? "the " : overlay.Clock == 1 ? "the next " : "this ") + Frost.Word(overlay.Side) + " phase ends";

    /// <summary>
    /// What one overlay is, for the board and the terrain card:
    /// <c>6,7 raised by pell, falls as the next player phase ends</c>.
    /// </summary>
    public static string Describe(BattleState state, TileOverlay overlay) =>
        $"{overlay.At} raised by {overlay.OwnerId}, falls as {Falls(state, overlay)}";

    /// <summary>
    /// The board's line while any overlay stands: <c>earthwork ([): 6,7 raised by pell, falls as the next
    /// player phase ends</c>, one group per terrain, or null.
    /// </summary>
    public static string? Line(BattleState state, GameContent content)
    {
        if (state.Overlays.Count == 0)
        {
            return null;
        }

        var groups = state.Overlays.GroupBy(o => o.TerrainId).Select(g =>
        {
            var terrain = content.TerrainById(g.Key);
            return $"{terrain.Name.ToLowerInvariant()} ({terrain.Glyph}): " + string.Join("; ", g.Select(o => Describe(state, o)));
        });
        return string.Join("\n", groups);
    }
}
