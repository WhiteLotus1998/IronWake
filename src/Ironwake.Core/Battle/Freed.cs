namespace Ironwake.Core;

/// <summary>
/// The bond (issue 750, Marrit's release on the finale stand-ins; provisional): on a map whose
/// <c>freed: &lt;x,y&gt; by &lt;group&gt;</c> header binds the enemy placed on that tile to the boss
/// of that group, placed or spawned, the bound enemy leaves the board when a boss of the group
/// falls. That is not a kill: no EXP, no drop, no <see cref="UnitDied"/>, one <see cref="UnitFreed"/>,
/// and a rout no longer counts her. Before then she is an ordinary enemy, and killing her is a kill
/// like any other, which the board records (<see cref="BattleState.Bond"/>) and the campaign carries
/// (<see cref="CampaignRecord.FreedUnitFell"/>). The board, <c>threat</c> and every forecast with the
/// boss or her in it print the bond (<see cref="Line"/>). The planner does not read it.
/// </summary>
public static class Freed
{
    /// <summary>Whether <paramref name="unit"/> is the map's bound enemy: the enemy that filled the placement on the <c>freed:</c> tile.</summary>
    public static bool IsBound(BattleState state, BattleUnit unit) =>
        state.Map.Bond is { } bond
        && unit.Side == Side.Enemy
        && unit.PlacementIndex >= 0
        && unit.PlacementIndex < state.Map.Placements.Count
        && state.Map.Placements[unit.PlacementIndex].At == bond.Bound;

    /// <summary>
    /// The record of a combat kill of the bound enemy by <paramref name="killer"/>, as the combat left
    /// it (issue 635 slice 16): fed when it stands with a hungering weapon equipped, the condition a kill feeds on.
    /// </summary>
    public static BondKill KillBy(BattleUnit killer, GameContent content) =>
        new(killer.Id, killer.Hp > 0 && killer.EquippedWeapon(content) is { Hungers: true });

    /// <summary>Whether <paramref name="unit"/> is a boss the bond answers to: an enemy boss of the header's group.</summary>
    public static bool IsBinder(MapDefinition map, BattleUnit unit) =>
        map.Bond is { } bond && unit.Side == Side.Enemy && unit.IsBoss && unit.Group == bond.BossGroup;

    /// <summary>The bound enemy on the board, or null when the map has none or she has left it.</summary>
    public static BattleUnit? On(BattleState state) =>
        state.Map.Bond is null ? null : state.UnitsOf(Side.Enemy).FirstOrDefault(u => IsBound(state, u));

    /// <summary>
    /// The id of the boss the bond names, for the names a reader sees: the first boss of the group
    /// standing on the board, else the first placed in the file, else the first spawned. Null without a bond.
    /// </summary>
    public static string? BinderId(BattleState state)
    {
        if (state.Map.Bond is not { } bond)
        {
            return null;
        }

        if (state.UnitsOf(Side.Enemy).FirstOrDefault(u => IsBinder(state.Map, u)) is { } standing)
        {
            return standing.Id;
        }

        var map = state.Map;
        var perTemplate = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var placement in map.Placements.OfType<EnemyPlacement>())
        {
            var count = perTemplate.GetValueOrDefault(placement.TemplateId) + 1;
            perTemplate[placement.TemplateId] = count;
            if (placement.IsBoss && placement.Group == bond.BossGroup)
            {
                return placement.TemplateId + "-" + count;
            }
        }

        return map.BossSpawns().Where(e => ((SpawnEnemy)e.Action).Placement.Group == bond.BossGroup).Select(map.SpawnId).FirstOrDefault();
    }

    /// <summary>
    /// The bond as the board, <c>threat</c> and the forecast print it while the bound enemy stands:
    /// <c>Sworn Hunter is bound to Hask: freed when Hask falls</c>. Null otherwise.
    /// </summary>
    public static string? Line(BattleState state, UnitNames names)
    {
        if (On(state) is not { } bound || BinderId(state) is not { } binder)
        {
            return null;
        }

        return $"{names[bound.Id]} is bound to {names[binder]}: freed when {names[binder]} falls";
    }

    /// <summary>
    /// The bond line under a forecast between <paramref name="attacker"/> and <paramref name="target"/>
    /// when either is the bound enemy or a boss she is bound to; null otherwise.
    /// </summary>
    public static string? ForecastLine(BattleState state, BattleUnit attacker, BattleUnit target, UnitNames names) =>
        new[] { attacker, target }.Any(u => IsBound(state, u) || IsBinder(state.Map, u)) ? Line(state, names) : null;

    /// <summary>
    /// After a command: when a boss of the bond's group is among <paramref name="events"/>' dead
    /// (read from <paramref name="before"/>, where he still stood) and the bound enemy stands on
    /// <paramref name="after"/>'s board, she leaves it, one <see cref="UnitFreed"/>, and the board
    /// records her freed. The event lands after the boss's death, so a win on him reads: he falls,
    /// she is freed, the map is won.
    /// </summary>
    public static BattleState After(BattleState before, BattleState after, List<GameEvent> events)
    {
        if (after.Map.Bond is null || On(after) is not { } bound)
        {
            return after;
        }

        var bossFell = events.OfType<UnitDied>().Any(d => before.Find(d.UnitId) is { } dead && IsBinder(before.Map, dead));
        if (!bossFell)
        {
            return after;
        }

        events.Add(new UnitFreed(bound.Id, bound.At, bound.Hp));
        return after.WithoutUnit(bound.Id) with { Bond = BondFate.Freed };
    }
}
