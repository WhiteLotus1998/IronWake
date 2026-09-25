using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// A player controller for the Sim: asked for the next step of the player phase on the
/// board as it stands, it returns the commands of one unit's turn (a Move then an action)
/// or <see cref="EndPhase"/> once nothing is left to do. The runner applies each command
/// through <see cref="Resolver.Apply"/> and asks again, so a controller can be swapped at
/// any turn boundary (issue 47's prefix arm).
/// </summary>
public interface IPlayer
{
    IReadOnlyList<Command> Next(BattleState state, GameContent content);
}

/// <summary>Gate 2's player: one uniformly random command from <see cref="Resolver.Legal"/> each step.</summary>
public sealed class RandomLegalPlayer : IPlayer
{
    private readonly Random _random;

    public RandomLegalPlayer(int seed) => _random = new Random(seed);

    public IReadOnlyList<Command> Next(BattleState state, GameContent content)
    {
        var legal = Resolver.Legal(state, content).ToList();
        return legal.Count == 0 ? Array.Empty<Command>() : new[] { legal[_random.Next(legal.Count)] };
    }
}

/// <summary>
/// Gate 1's baseline (issue 12): a planner of its own over <see cref="EnemyAi.Score"/> and
/// <see cref="EnemyAi.AttackTiles"/>, per unit in id order. The best-scoring attack from
/// the best tile with section 8's tie-breaks mirrored (exposure counted over enemy reach
/// sets); else a heal below half HP (a healing spell on the most wounded ally in range,
/// else a consumable on itself); else the approach: toward the nearest enemy by section
/// 8's rule for Rout and Defeat Boss, toward the throne for Seize, toward the nearest exit
/// for Escape, hold for Survive. The veto is arithmetic (Design Table, seventh round)
/// and covers every unit whose death loses the map (fourteenth round, issue 141): the
/// captain, and the recruit a <c>protect:</c> header names, by section 7's loss order.
/// A plan is refused when <see cref="Exposure"/>'s no-crit sum over the whole cycle
/// reaches that unit's current HP, and among plans that pass, one a crit cannot kill on
/// is preferred ahead of the tile keys. Every other recruit plays without a veto. The
/// planner knows nothing of the wake rule and walks into sleeping groups; that is the
/// baseline gate 1 measures.
/// </summary>
public sealed class HeuristicPlayer : IPlayer
{
    /// <summary>
    /// The highest kill probability among the attack plans the veto refused in this game,
    /// or null when it refused none (issue 125). A refused plan whose kill was near certain
    /// is the veto rescuing nobody, and gate 1 and gate 4 print this so a stall reads as a
    /// veto rescue or a risk stall without a threshold.
    /// </summary>
    public double? HighestRefusedKill { get; private set; }

    public IReadOnlyList<Command> Next(BattleState state, GameContent content)
    {
        if (state.Outcome.IsOver || state.Phase != Side.Player)
        {
            return Array.Empty<Command>();
        }

        var unit = state.UnitsOf(Side.Player).FirstOrDefault(u => !u.Acted);
        if (unit is null)
        {
            return new Command[] { new EndPhase() };
        }

        var plan = PlanUnit(state, content, unit, out var refused);
        if (refused is { } p && (HighestRefusedKill is null || p > HighestRefusedKill))
        {
            HighestRefusedKill = p;
        }

        return plan;
    }

    /// <summary>One player unit's commands on the board as it stands.</summary>
    public static IReadOnlyList<Command> PlanUnit(BattleState state, GameContent content, BattleUnit unit) =>
        PlanUnit(state, content, unit, out _);

    /// <summary>
    /// One player unit's commands on the board as it stands. <paramref name="refusedKill"/>
    /// is the highest kill probability among the attacks the veto refused for this unit,
    /// or null when it refused none.
    /// </summary>
    public static IReadOnlyList<Command> PlanUnit(BattleState state, GameContent content, BattleUnit unit, out double? refusedKill)
    {
        refusedKill = null;
        var weapon = unit.EquippedWeapon(content);
        var movement = content.Class(unit.Unit.ClassId).Movement;
        var reach = state.ReachOf(unit, content);
        var tiles = unit.Moved ? new List<Coord> { unit.At } : reach.Destinations.Where(t => MayEndOn(state, content, unit, t)).ToList();
        if (tiles.Count == 0)
        {
            tiles.Add(unit.At);
        }
        var enemies = state.UnitsOf(Side.Enemy).ToList();
        var enemyReach = enemies.Select(e => state.ReachOf(e, content)).ToList();

        if (weapon is not null)
        {
            Option? best = null;
            foreach (var tile in tiles)
            {
                var avoid = state.Map.TerrainAt(tile, content).AvoidFor(movement);
                var exposed = enemyReach.Count(r => r.CanEnd(tile));
                var cost = reach.CostTo(tile)!.Value;
                foreach (var target in enemies)
                {
                    if (!weapon.InRange(tile.DistanceTo(target.At)))
                    {
                        continue;
                    }

                    var critSafe = true;
                    if (LosesTheMap(state, unit))
                    {
                        var sum = Exposure.Of(state, content, unit, tile, target);
                        if (sum.NoCrit >= unit.Hp)
                        {
                            var kill = KillProbability(state, content, unit, tile, target);
                            if (refusedKill is null || kill > refusedKill)
                            {
                                refusedKill = kill;
                            }

                            continue;
                        }

                        critSafe = sum.WithCrit < unit.Hp;
                    }

                    var option = new Option(EnemyAi.Score(state, content, unit, tile, target), target.Id, tile, critSafe, avoid, exposed, cost);
                    if (best is null || option.Beats(best))
                    {
                        best = option;
                    }
                }
            }

            if (best is not null)
            {
                return WithMove(unit, best.Tile, new Attack(unit.Id, best.TargetId));
            }
        }

        var heal = Heal(state, content, unit, tiles, enemyReach);
        if (heal is not null)
        {
            return heal;
        }

        if (unit.Moved || state.Map.Win == WinCondition.Survive)
        {
            return new Command[] { new Wait(unit.Id) };
        }

        var destination = Approach(state, content, unit, weapon, reach, enemies, enemyReach, movement);
        return WithMove(unit, destination ?? unit.At, new Wait(unit.Id));
    }

    /// <summary>
    /// The chance that <paramref name="attacker"/>, attacking from <paramref name="tile"/>
    /// with its equipped weapon, kills <paramref name="target"/> over the strikes it lives to
    /// make: the first strike, and the second when it doubles, each landing at the forecast's
    /// resolved hit probability under the game's scheme and critting at its crit chance for
    /// <see cref="Combat.CritMultiplier"/> times the damage (issue 125). A kill that needs the
    /// second strike is weighted by the chance the attacker survives the counter thrown
    /// between them (issue 147): the defender's strike when it reaches, at its own hit and
    /// crit, killing when its damage reaches the attacker's current HP. A first-strike kill
    /// takes no counter and is unchanged; a defender that cannot strike back, or whose raw
    /// hit is 0, weighs the second strike at one.
    /// </summary>
    public static double KillProbability(BattleState state, GameContent content, BattleUnit attacker, Coord tile, BattleUnit target)
    {
        var me = (attacker with { At = tile }).ToCombatant(state.Map, content);
        var them = target.ToCombatant(state.Map, content);
        var forecast = Combat.Forecast(me, them, tile.DistanceTo(target.At), state.Scheme, state.Formula);
        var side = forecast.Attacker;
        var outcomes = Outcomes(side, state.Scheme);
        var survivesCounter = 1 - KillsWith(forecast.Defender, attacker.Hp, state.Scheme);
        var kill = 0.0;
        foreach (var (firstP, firstDamage) in outcomes)
        {
            if (firstDamage >= target.Hp)
            {
                kill += firstP;
                continue;
            }

            if (!side.Doubles)
            {
                continue;
            }

            foreach (var (secondP, secondDamage) in outcomes)
            {
                kill += firstDamage + secondDamage >= target.Hp ? firstP * survivesCounter * secondP : 0;
            }
        }

        return kill;
    }

    /// <summary>The three outcomes of one strike with their probabilities: a miss, a plain hit, a crit.</summary>
    private static (double P, int Damage)[] Outcomes(SideForecast side, RollScheme scheme)
    {
        var hit = Combat.HitProbability(side.HitChance, scheme);
        var crit = Math.Clamp(side.CritChance, 0, 100) / 100.0;
        return new[] { (1 - hit, 0), (hit * (1 - crit), side.Damage), (hit * crit, side.Damage * Combat.CritMultiplier) };
    }

    /// <summary>The chance one strike of <paramref name="side"/> kills a unit at <paramref name="hp"/>; zero when the side does not strike.</summary>
    private static double KillsWith(SideForecast side, int hp, RollScheme scheme) =>
        side.Strikes ? Outcomes(side, scheme).Where(o => o.Damage >= hp).Sum(o => o.P) : 0;

    private static IReadOnlyList<Command> WithMove(BattleUnit unit, Coord tile, Command action) =>
        tile == unit.At ? new[] { action } : new Command[] { new Move(unit.Id, tile), action };

    /// <summary>
    /// A recruit never ends a move on a throne tile of a Seize map (issue 111): only the
    /// captain can seize (section 7), so a recruit standing there blocks the win for as long
    /// as it stays, and the approach toward the throne would otherwise put the first recruit
    /// to arrive exactly there. The captain may end anywhere in reach.
    /// </summary>
    private static bool MayEndOn(BattleState state, GameContent content, BattleUnit unit, Coord tile) =>
        unit.IsCaptain || state.Map.Win != WinCondition.Seize || !state.Map.IsThrone(tile);

    /// <summary>
    /// Whether the veto applies to this unit: its death is a loss condition by section 7's
    /// outcome order, so the captain on every map and the recruit named by the map's
    /// <c>protect:</c> header (issue 141). One predicate at every site, since a unit that
    /// refuses lethal attacks but approaches blind walks onto a lethal tile by another route.
    /// </summary>
    public static bool LosesTheMap(BattleState state, BattleUnit unit) =>
        unit.IsCaptain || (state.Map.ProtectId is { } protectId && string.Equals(unit.Id, protectId, StringComparison.Ordinal));

    /// <summary>A heal when one is wanted: a healing spell on the most wounded ally below half, else a consumable on itself below half, from the safest tile that allows it.</summary>
    private static IReadOnlyList<Command>? Heal(BattleState state, GameContent content, BattleUnit unit, List<Coord> tiles, List<Reach> enemyReach)
    {
        var unitClass = content.Class(unit.Unit.ClassId);
        var safest = tiles.OrderBy(t => LosesTheMap(state, unit) ? Exposure.Of(state, content, unit, t).NoCrit : enemyReach.Count(r => r.CanEnd(t))).ThenBy(t => t).ToList();
        for (var slot = 0; slot < unit.Unit.Inventory.Count; slot++)
        {
            var stack = unit.Unit.Inventory.Items[slot];
            if (content.Items.ContainsKey(stack.ItemId))
            {
                if (unit.Hp * 2 < unit.MaxHp(content) && stack.Uses > 0)
                {
                    return WithMove(unit, safest[0], new UseItem(unit.Id, slot));
                }

                continue;
            }

            var spell = content.Weapon(stack.ItemId);
            if (!spell.Heals || !unitClass.CanUse(spell.Type) || stack.Uses == 0)
            {
                continue;
            }

            foreach (var tile in safest)
            {
                BattleUnit? wounded = null;
                foreach (var ally in state.UnitsOf(Side.Player))
                {
                    if (ally.Id != unit.Id && spell.InRange(tile.DistanceTo(ally.At)) && ally.Hp * 2 < ally.MaxHp(content)
                        && (wounded is null || ally.Hp < wounded.Hp))
                    {
                        wounded = ally;
                    }
                }

                if (wounded is not null)
                {
                    return WithMove(unit, tile, new UseItem(unit.Id, slot, wounded.Id));
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Where a unit that can attack nobody walks: section 8's approach toward the nearest
    /// enemy on Rout and Defeat Boss, the reachable tile nearest the throne on Seize, the
    /// nearest exit on Escape. A unit the veto covers (<see cref="LosesTheMap"/>) refuses
    /// tiles whose no-crit exposure reaches its HP when any tile passes, and prefers a tile
    /// a crit cannot kill on; every other recruit takes <see cref="EnemyAi.Approach"/>.
    /// </summary>
    private static Coord? Approach(
        BattleState state, GameContent content, BattleUnit unit, Weapon? weapon, Reach reach,
        List<BattleUnit> enemies, List<Reach> enemyReach, MovementType movement)
    {
        Occupant OccupantAt(Coord at) => at == unit.At ? Occupant.None : state.OccupantAt(at, unit.Side);
        Distances? TowardNearestEnemy(Weapon armed)
        {
            Distances? chosen = null;
            var nearest = int.MaxValue;
            foreach (var target in enemies)
            {
                var distances = Movement.DistancesTo(state.Map, content, EnemyAi.AttackTiles(state, content, unit, armed, target, movement), movement, OccupantAt);
                if (distances.From(unit.At) is { } c && c < nearest)
                {
                    chosen = distances;
                    nearest = c;
                }
            }

            return chosen;
        }

        Distances? toward = null;
        switch (state.Map.Win)
        {
            case WinCondition.Seize:
                toward = Movement.DistancesTo(state.Map, content, state.Map.TilesOf(MapDefinition.ThroneTerrainId), movement, OccupantAt);
                break;
            case WinCondition.Escape:
                toward = Movement.DistancesTo(state.Map, content, state.Map.Exits, movement, OccupantAt);
                break;
            default:
                if (weapon is null)
                {
                    return null;
                }

                if (!LosesTheMap(state, unit))
                {
                    return EnemyAi.Approach(state, content, unit, weapon, reach, enemies, enemyReach);
                }

                toward = TowardNearestEnemy(weapon);
                break;
        }

        if (toward is not null && weapon is not null && toward.From(unit.At) is null)
        {
            // The objective has no open path (an enemy holds the corridor), so the way
            // there is through the nearest enemy: approach it as a Rout map would.
            toward = null;
        }

        if (toward is null && weapon is not null && state.Map.Win is WinCondition.Seize or WinCondition.Escape)
        {
            if (!LosesTheMap(state, unit))
            {
                return EnemyAi.Approach(state, content, unit, weapon, reach, enemies, enemyReach);
            }

            toward = TowardNearestEnemy(weapon);
        }

        if (toward is null)
        {
            return null;
        }

        Coord? destination = null;
        (bool Lethal, int Remaining, bool CritLethal, int Avoid, int Exposed, int Cost) bestKey = default;
        foreach (var tile in reach.Destinations)
        {
            if (toward.From(tile) is not { } remaining || !MayEndOn(state, content, unit, tile))
            {
                continue;
            }

            var lethal = false;
            var critLethal = false;
            if (LosesTheMap(state, unit))
            {
                var sum = Exposure.Of(state, content, unit, tile);
                lethal = sum.NoCrit >= unit.Hp;
                critLethal = sum.WithCrit >= unit.Hp;
            }

            var key = (lethal, remaining, critLethal, -state.Map.TerrainAt(tile, content).AvoidFor(movement), enemyReach.Count(r => r.CanEnd(tile)), reach.CostTo(tile)!.Value);
            if (destination is null || key.CompareTo(bestKey) < 0)
            {
                bestKey = key;
                destination = tile;
            }
        }

        return destination;
    }

    /// <summary>Section 8's order for the player's side: score, lower target id, then the captain's crit-safe key, then avoid, fewer enemies reaching the tile, cost, row-major.</summary>
    private sealed record Option(double Score, string TargetId, Coord Tile, bool CritSafe, int Avoid, int Exposed, int Cost)
    {
        public bool Beats(Option other)
        {
            if (Score != other.Score)
            {
                return Score > other.Score;
            }

            var byId = string.CompareOrdinal(TargetId, other.TargetId);
            if (byId != 0)
            {
                return byId < 0;
            }

            if (CritSafe != other.CritSafe)
            {
                return CritSafe;
            }

            if (Avoid != other.Avoid)
            {
                return Avoid > other.Avoid;
            }

            if (Exposed != other.Exposed)
            {
                return Exposed < other.Exposed;
            }

            if (Cost != other.Cost)
            {
                return Cost < other.Cost;
            }

            return Tile.CompareTo(other.Tile) < 0;
        }
    }
}
