namespace Ironwake.Core;

/// <summary>
/// Overwatch (DESIGN.md 13.17, experiment), behind a map's <c>overwatch: on</c> header. A unit
/// whose equipped weapon reaches range 2 may take <see cref="Watch"/> as its action. Until its
/// side's next phase begins it watches its ring, the tiles exactly two steps away
/// (<see cref="InRing"/>), never the tile beside it. The first unit of the other side to end a
/// move (a Move or a Canto, never a shove or a spawn) on a ring tile its side can see is struck
/// once before it acts: the watcher's normal hit, damage and crit, no counter, no double, keyed
/// under <see cref="RollKey.Watch"/>. The watch is then spent. A strike on the watcher, hit or
/// miss, ends the watch. An arrival inside two rings is shot by each in id order until one kills.
/// A watch is its own action, not a Wait, so a watcher never braces. The watch is
/// <see cref="BattleUnit.Watching"/>, so Recall restores it with the unit.
/// </summary>
public static class Overwatch
{
    /// <summary>The displayed hit at or above which a strike passed up by watching counts toward the kill criterion's first clause (round 115).</summary>
    public const int OverStrikeHit = 50;

    /// <summary>The distance a ring covers, for every weapon (round 113).</summary>
    public const int Distance = 2;

    /// <summary>Why <paramref name="unit"/> cannot watch now, or null when it can: the map needs the header and the equipped weapon must reach range 2.</summary>
    public static string? Refusal(BattleState state, GameContent content, BattleUnit unit) =>
        !state.Map.OverwatchEnabled ? "this map has no overwatch (overwatch: on)"
        : unit.EquippedWeapon(content) is not { } weapon ? $"{unit.Id} has no weapon equipped"
        : !weapon.InRange(Distance) ? $"{unit.Id}'s {weapon.Name} does not reach range {Distance}"
        : null;

    /// <summary>Whether <paramref name="tile"/> lies in <paramref name="watcher"/>'s ring: exactly <see cref="Distance"/> steps away.</summary>
    public static bool InRing(BattleUnit watcher, Coord tile) => watcher.At.DistanceTo(tile) == Distance;

    /// <summary>The tiles under a watch, row-major: every watcher's ring inside the map. Empty on a map without the header.</summary>
    public static IReadOnlyList<Coord> Marked(BattleState state)
    {
        if (!state.Map.OverwatchEnabled)
        {
            return Array.Empty<Coord>();
        }

        var tiles = new SortedSet<Coord>();
        foreach (var watcher in state.Units.Where(u => u.Watching))
        {
            foreach (var tile in Ring(watcher.At).Where(state.Map.Contains))
            {
                tiles.Add(tile);
            }
        }

        return tiles.ToList();
    }

    /// <summary>The living watchers whose ring holds <paramref name="tile"/> and who would shoot a unit of <paramref name="side"/> ending a move there, in id order.</summary>
    public static IReadOnlyList<BattleUnit> WatchersOver(BattleState state, Side side, Coord tile) =>
        state.Units.Where(u => u.Watching && u.Side != side && InRing(u, tile) && Dusk.Sees(state, u.Side, tile))
            .OrderBy(u => u.Id, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The best legal strike <paramref name="unit"/> passes up by watching where it stands: the
    /// highest displayed hit among every target any usable weapon reaches from its tile, first
    /// target in id order on a tie. Null when it has no strike. The transcript prints it so the
    /// kill criterion's first clause is a count (round 115).
    /// </summary>
    public static (string TargetId, int Hit)? PassedUp(BattleState state, GameContent content, BattleUnit unit)
    {
        (string, int)? best = null;
        var targets = state.UnitsOf(unit.Side == Side.Player ? Side.Enemy : Side.Player)
            .Where(t => Dusk.Sees(state, unit.Side, t.At))
            .OrderBy(t => t.Id, StringComparer.Ordinal);
        foreach (var target in targets)
        {
            for (var slot = 0; slot < unit.Unit.Inventory.Count; slot++)
            {
                if (unit.UsableWeaponAt(content, slot) is null || Queries.Forecast(state, content, unit, target, slot) is not { } forecast)
                {
                    continue;
                }

                if (best is null || forecast.Attacker.DisplayedHit > best.Value.Item2)
                {
                    best = (target.Id, forecast.Attacker.DisplayedHit);
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Whether an enemy planning its phase watches instead of Waiting (the enemy's rule, round
    /// 112): the header is on, it has no strike this phase, its equipped weapon reaches range 2,
    /// whatever its behavior (Hold, Boss or Aggressive). A Guard whose group sleeps never watches.
    /// </summary>
    public static bool EnemyWatches(BattleState state, GameContent content, BattleUnit unit) =>
        state.Map.OverwatchEnabled
        && Refusal(state, content, unit) is null
        && unit.Side == Side.Enemy
        && !(unit.Behavior == Behavior.Guard && unit.Group is { } group && !state.IsAwake(group));

    /// <summary>
    /// The one strike a watch shot is: the watcher against the arrival at distance 2, no counter,
    /// no double, rolled under <see cref="RollKey.Watch"/>. Returns the strike as the resolver records it.
    /// </summary>
    public static StrikeEvent Shoot(BattleState state, GameContent content, BattleUnit watcher, BattleUnit target, IRng rng)
    {
        var forecast = Combat.Forecast(
            watcher.ToCombatant(state, content, against: target),
            target.Answering(state, content, watcher.At, watcher),
            watcher.At.DistanceTo(target.At),
            state.Scheme).Attacker;
        var rollA = rng.Roll(RollKey.Watch(state.Turn, state.Phase, watcher.Id, target.Id, CombatRoll.HitA));
        var rollB = state.Scheme == RollScheme.TwoRollAverage
            ? rng.Roll(RollKey.Watch(state.Turn, state.Phase, watcher.Id, target.Id, CombatRoll.HitB))
            : 0;
        var hit = Combat.Lands(forecast.HitChance, rollA, rollB, state.Scheme);
        var crit = hit && rng.Roll(RollKey.Watch(state.Turn, state.Phase, watcher.Id, target.Id, CombatRoll.Crit)) < forecast.CritChance;
        var damage = !hit ? 0 : crit ? forecast.Damage * Combat.CritMultiplier : forecast.Damage;
        var after = Math.Max(0, target.Hp - damage);
        return new StrikeEvent(0, watcher.Id, target.Id, hit, crit, damage, after);
    }

    private static IEnumerable<Coord> Ring(Coord at)
    {
        for (var dx = -Distance; dx <= Distance; dx++)
        {
            var dy = Distance - Math.Abs(dx);
            yield return new Coord(at.X + dx, at.Y + dy);
            if (dy != 0)
            {
                yield return new Coord(at.X + dx, at.Y - dy);
            }
        }
    }
}
