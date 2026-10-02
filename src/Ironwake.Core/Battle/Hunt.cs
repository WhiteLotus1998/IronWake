namespace Ironwake.Core;

/// <summary>
/// The hunt (issue 692, Marrit's rule on the keep finale): on a map with a <c>hunter:</c> header,
/// the enemy placed on that tile hunts the weakest front. Each player unit defends the front
/// nearest it by Manhattan distance to any of the front's tiles, within <see cref="DefendRadius"/>,
/// the earlier front in file order on a tie; a unit farther than that from every front defends
/// none. The hunted front is the standing front whose defenders' summed HP is lowest, the earlier
/// in file order on a tie, chosen once as each enemy phase begins (<see cref="BattleState.Hunting"/>)
/// and read fresh on the player phase, so <c>threat</c> names the front that comes. The hunter
/// strikes only defenders of the hunted front; with none it can strike it approaches them, and with
/// none at all it marches on the front's tiles, the party left out of the field as in
/// <see cref="EnemyAi.Drift"/>. It still counters anyone. With every front fallen it hunts nothing
/// and acts as any enemy of its behavior.
/// </summary>
public static class Hunt
{
    /// <summary>The rule the board prints on a map with a hunter on it.</summary>
    public const string Rule = "A unit defends the front nearest it, within 3 tiles. The hunter strikes only the defenders of the standing front with the least HP among its defenders, chosen as each enemy phase begins.";

    /// <summary>How far from a front's nearest tile a player unit still defends it.</summary>
    public const int DefendRadius = 3;

    /// <summary>Whether <paramref name="unit"/> is the map's hunter: the enemy that filled the placement on the <c>hunter:</c> tile.</summary>
    public static bool Is(BattleState state, BattleUnit unit) =>
        state.Map.Hunter is { } at
        && unit.Side == Side.Enemy
        && unit.PlacementIndex >= 0
        && unit.PlacementIndex < state.Map.Placements.Count
        && state.Map.Placements[unit.PlacementIndex].At == at;

    /// <summary>The hunter on the board, or null when the map has none or it has fallen.</summary>
    public static BattleUnit? On(BattleState state) =>
        state.Map.Hunter is null ? null : state.UnitsOf(Side.Enemy).FirstOrDefault(u => Is(state, u));

    /// <summary>The front a player unit standing on <paramref name="at"/> defends, or null when it is farther than <see cref="DefendRadius"/> from every front.</summary>
    public static Front? DefendedFrom(MapDefinition map, Coord at)
    {
        Front? nearest = null;
        var best = DefendRadius + 1;
        foreach (var front in map.Fronts)
        {
            var distance = front.Tiles.Min(t => t.DistanceTo(at));
            if (distance < best)
            {
                best = distance;
                nearest = front;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Each standing front in file order with its defenders, in unit order, and their summed HP:
    /// what the hunter weighs. Empty on a map without fronts.
    /// </summary>
    public static IReadOnlyList<FrontHold> Holds(BattleState state)
    {
        var holds = new List<FrontHold>();
        foreach (var front in state.Map.Fronts)
        {
            if (Fronts.HasFallen(state, front))
            {
                continue;
            }

            var defenders = state.UnitsOf(Side.Player).Where(p => DefendedFrom(state.Map, p.At) == front).ToList();
            holds.Add(new FrontHold(front, ValueList<BattleUnit>.From(defenders), defenders.Sum(d => d.Hp)));
        }

        return holds;
    }

    /// <summary>The standing front with the lowest summed defender HP, the earlier in file order on a tie; null when none stands.</summary>
    public static Front? Choose(BattleState state)
    {
        FrontHold? weakest = null;
        foreach (var hold in Holds(state))
        {
            if (weakest is null || hold.Hp < weakest.Hp)
            {
                weakest = hold;
            }
        }

        return weakest?.Front;
    }

    /// <summary>
    /// The front the hunter hunts on <paramref name="state"/>: in an enemy phase the one chosen as
    /// it began (<see cref="BattleState.Hunting"/>), else the one <see cref="Choose"/> names on the
    /// board as it stands, which is the one the coming enemy phase will choose. Null on a map
    /// without a hunter, or with no front standing.
    /// </summary>
    public static Front? Hunted(BattleState state)
    {
        if (state.Map.Hunter is null)
        {
            return null;
        }

        return state.Phase == Side.Enemy
            ? state.Map.Fronts.FirstOrDefault(f => f.Name == state.Hunting)
            : Choose(state);
    }

    /// <summary>At the start of an enemy phase: fixes <see cref="BattleState.Hunting"/> for the phase; clears it as a player phase begins.</summary>
    public static BattleState AtPhaseStart(BattleState state, Side phase) =>
        state.Map.Hunter is null
            ? state
            : state with { Hunting = phase == Side.Enemy ? Choose(state)?.Name : null };

    /// <summary>
    /// The player units <paramref name="unit"/> may strike or approach among <paramref name="known"/>:
    /// all of them unless it is the hunter with a front to hunt, then only that front's defenders.
    /// </summary>
    public static IReadOnlyList<BattleUnit> Narrow(BattleState state, BattleUnit unit, IReadOnlyList<BattleUnit> known) =>
        Is(state, unit) && Hunted(state) is { } front
            ? known.Where(p => DefendedFrom(state.Map, p.At) == front).ToList()
            : known;

    /// <summary>Whether the hunt keeps <paramref name="unit"/> off <paramref name="target"/>: it is the hunter, a front is hunted, and the target does not defend it.</summary>
    public static bool Spares(BattleState state, BattleUnit unit, BattleUnit target) =>
        Is(state, unit) && Hunted(state) is { } front && DefendedFrom(state.Map, target.At) != front;

    /// <summary>
    /// The line <c>threat</c> and the board print for the hunt, units by the names a reader sees:
    /// <c>Marrit hunts the north next: 2 defenders, 41 hp (weakest)</c>, or that the hunt is over.
    /// Null on a map without a hunter or once it has fallen.
    /// </summary>
    public static string? Line(BattleState state, UnitNames names)
    {
        if (On(state) is not { } hunter)
        {
            return null;
        }

        if (Hunted(state) is not { } front)
        {
            return $"{names[hunter.Id]} hunts no front: every front has fallen";
        }

        var hold = Holds(state).FirstOrDefault(h => h.Front == front);
        var defenders = hold is null ? "fallen this phase" : hold.Defenders.Count == 0 ? "no defenders" : $"{hold.Defenders.Count} defender{(hold.Defenders.Count == 1 ? "" : "s")}, {hold.Hp} hp";
        return $"{names[hunter.Id]} hunts the {front.Words}{(state.Phase == Side.Player ? " next" : "")}: {defenders} (weakest); strikes only its defenders";
    }
}

/// <summary>A standing front, its defenders in unit order (<see cref="Hunt.DefendedFrom"/>) and their summed HP (issue 692).</summary>
public sealed record FrontHold(Front Front, ValueList<BattleUnit> Defenders, int Hp);
