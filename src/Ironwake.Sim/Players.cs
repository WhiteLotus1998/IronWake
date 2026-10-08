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
/// the best tile with any weapon it carries (<see cref="Arms"/>, issue 746), a hungering weapon not yet woken ahead of any other when it kills on a hit and behind any other when it does not (the hunt, issues 804 and 851), with section 8's tie-breaks mirrored (exposure counted over enemy reach
/// sets); else a heal below half HP (a healing spell on the most wounded ally in range,
/// else a consumable on itself); else the approach: toward the nearest enemy by section
/// 8's rule for Rout and Defeat Boss, toward the throne for Seize, toward the nearest exit
/// for Escape, hold for Survive. On Escape a unit that can reach an exit leaves through it
/// ahead of all of that, and the captain plans last and leaves last (<see cref="ExitTile"/>, issue 269),
/// the rest farthest from an exit first (<see cref="PlanOrder(BattleState, GameContent)"/>, issue 332); on a corked Seize map the captain plans last and the recruits leave it its no-counter tiles (<see cref="CaptainsTiles"/>, issue 1206). The veto is arithmetic (Design Table, seventh round)
/// and covers every unit whose death loses the map (fourteenth round, issue 141): the
/// captain, and the recruit a <c>protect:</c> header names, by section 7's loss order.
/// A plan is refused when <see cref="Exposure"/>'s no-crit sum over the whole cycle
/// reaches that unit's current HP, and among plans that pass, one a crit cannot kill on
/// is preferred ahead of the tile keys. Every other recruit plays without a veto on its
/// strikes; its approach is section 8's unless that tile is lethal by the same sum, when it
/// takes the captain's approach key instead (issue 1044), so a fast recruit no longer
/// outruns its party into a group it meets alone. The
/// planner knows nothing of the wake rule and walks into sleeping groups; that is the
/// baseline gate 1 measures. A unit owed a Canto takes it before any other unit acts, to
/// the safest tile in its Canto reach by <see cref="PlanCanto"/> (issue 262).
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

    /// <summary>
    /// The highest kill probability among the attack plans the veto refused on the last
    /// player phase this player planned, or null when it refused none there (issue 157).
    /// On a timeout that phase is the board the game stalled on, which the game's highest,
    /// often ten turns earlier, does not name.
    /// </summary>
    public double? LastPhaseRefusedKill { get; private set; }

    private int _lastPhaseTurn;

    /// <summary>
    /// Whether this player casts an area tome when the cast outscores its best attack (issue 1391,
    /// <see cref="AreaCast.Best"/>); on unless a caller turns it off, as the campaign script's writer does while the
    /// client's click path takes no area cast (issue 1392).
    /// </summary>
    public bool Casts { get; init; } = true;

    public IReadOnlyList<Command> Next(BattleState state, GameContent content)
    {
        if (state.Outcome.IsOver || state.Phase != Side.Player)
        {
            return Array.Empty<Command>();
        }

        if (state.Turn != _lastPhaseTurn)
        {
            _lastPhaseTurn = state.Turn;
            LastPhaseRefusedKill = null;
        }

        var owed = state.UnitsOf(Side.Player).FirstOrDefault(u => state.CantoReachOf(u, content) is not null);
        if (owed is not null)
        {
            return new Command[] { PlanCanto(state, content, owed) };
        }

        var unit = PlanOrder(state, content).FirstOrDefault(u => !u.Acted);
        if (unit is null)
        {
            return new Command[] { new EndPhase() };
        }

        var plan = PlanUnit(state, content, unit, out var refused, Casts);
        if (refused is { } p && (HighestRefusedKill is null || p > HighestRefusedKill))
        {
            HighestRefusedKill = p;
        }

        if (refused is { } q && (LastPhaseRefusedKill is null || q > LastPhaseRefusedKill))
        {
            LastPhaseRefusedKill = q;
        }

        return plan;
    }

    /// <summary>
    /// The order the player units plan in: id order, except on an Escape map, where the
    /// captain plans last and the rest plan farthest from an exit first, by straight
    /// distance to the nearest exit tile, id order breaking ties (issue 332). The rear moves
    /// before the front, so the unit nearest the exits no longer walks the lane first,
    /// takes the tiles ahead of the others, and leaves them to meet whatever its walk woke.
    /// </summary>
    public static IEnumerable<BattleUnit> PlanOrder(BattleState state) =>
        state.Map.Win != WinCondition.Escape
            ? state.UnitsOf(Side.Player)
            : state.UnitsOf(Side.Player)
                .OrderBy(u => u.IsCaptain)
                .ThenByDescending(u => state.Map.Exits.Min(x => x.DistanceTo(u.At)));

    /// <summary>
    /// <see cref="PlanOrder(BattleState)"/>, and on a Seize map whose throne the captain has no
    /// open path to (<see cref="Corked"/>, issue 1206), on a turn it can strike some enemy without
    /// a counter, the captain plans last, so the cork's other attackers strike first and the
    /// captain finishes from the safe tile.
    /// </summary>
    public static IEnumerable<BattleUnit> PlanOrder(BattleState state, GameContent content) =>
        Corked(state, content) is { } captain && !captain.Acted && SafeStrikeTiles(state, content, captain).Count > 0
            ? state.UnitsOf(Side.Player).OrderBy(u => u.IsCaptain)
            : PlanOrder(state);

    /// <summary>
    /// The living captain on a Seize map when no throne tile has an open path from where it
    /// stands (an enemy holds the corridor, the case <c>Approach</c> turns into a Rout approach),
    /// else null (issue 1206).
    /// </summary>
    public static BattleUnit? Corked(BattleState state, GameContent content)
    {
        if (state.Map.Win != WinCondition.Seize
            || state.UnitsOf(Side.Player).FirstOrDefault(u => u.IsCaptain) is not { } captain)
        {
            return null;
        }

        var movement = Grounding.MovementOf(captain, content);
        Occupant OccupantAt(Coord at) => at == captain.At ? Occupant.None : state.OccupantAt(at, captain.Side);
        var toward = Movement.DistancesTo(state.Map, content, state.Map.TilesOf(MapDefinition.ThroneTerrainId), movement, OccupantAt, content.AbilitiesOf(captain.Unit));
        return toward.From(captain.At) is null ? captain : null;
    }

    /// <summary>
    /// The tiles a recruit leaves free for a corked captain who has not acted (issue 1206): the
    /// captain's <see cref="SafeStrikeTiles"/>. A recruit never strikes from one, so on a cork one
    /// tile reaches without a counter the recruit takes the counter from another tile or does not
    /// strike, and the captain finishes from the safe tile. Empty for the captain, or when not corked.
    /// </summary>
    public static IReadOnlySet<Coord> CaptainsTiles(BattleState state, GameContent content, BattleUnit unit) =>
        unit.IsCaptain || Corked(state, content) is not { } captain || captain.Acted
            ? new HashSet<Coord>()
            : SafeStrikeTiles(state, content, captain);

    /// <summary>
    /// Each tile <paramref name="captain"/> can end on this turn from which one of its arms strikes
    /// an enemy that cannot answer at that distance (issue 1206).
    /// </summary>
    public static IReadOnlySet<Coord> SafeStrikeTiles(BattleState state, GameContent content, BattleUnit captain)
    {
        var tiles = new HashSet<Coord>();
        var arms = Arms(content, captain);
        var enemies = state.UnitsOf(Side.Enemy).ToList();
        foreach (var tile in state.ReachOf(captain, content).Destinations)
        {
            foreach (var (_, weapon, armed) in arms)
            {
                if (enemies.Any(e => weapon.InRange(tile.DistanceTo(e.At)) && !Answers(state, content, armed, tile, e)))
                {
                    tiles.Add(tile);
                    break;
                }
            }
        }

        return tiles;
    }

    /// <summary>Whether <paramref name="target"/> strikes back at <paramref name="armed"/> attacking from <paramref name="tile"/>.</summary>
    private static bool Answers(BattleState state, GameContent content, BattleUnit armed, Coord tile, BattleUnit target)
    {
        var me = (armed with { At = tile }).ToCombatant(state, content, against: target);
        var them = target.Answering(state, content, tile, armed);
        return Combat.Forecast(me, them, tile.DistanceTo(target.At), state.Scheme).Defender.Strikes;
    }

    /// <summary>
    /// The weapons <paramref name="unit"/> may strike with, by slot in inventory order, each with
    /// the unit as it strikes (that slot moved to the front), as the enemy AI reads its own
    /// (issue 746). <c>PlanUnit</c> scores every one per tile and target, so a unit that
    /// carries a bow and a sword draws whichever the board pays for, and the attack names the slot,
    /// which then stays in front for the enemy phase's counter. Healing spells, items and a spent
    /// spell are left out, as <see cref="BattleUnit.UsableWeaponAt"/> leaves them out.
    /// </summary>
    public static IReadOnlyList<(int Slot, Weapon Weapon, BattleUnit Armed)> Arms(GameContent content, BattleUnit unit) =>
        Enumerable.Range(0, unit.Unit.Inventory.Count)
            .Select(slot => (Slot: slot, Weapon: unit.UsableWeaponAt(content, slot)))
            .Where(arm => arm.Weapon is not null)
            .Select(arm => (arm.Slot, arm.Weapon!, unit.WithSlotInFront(arm.Slot)))
            .ToList();

    /// <summary>
    /// Whether Ottilie's ledger (DESIGN.md 13.18) would refuse this attack from <paramref name="tile"/>.
    /// The heuristic plays no signature, but it obeys a refusal, so every plan it makes is legal.
    /// </summary>
    internal static bool LedgerRefuses(BattleState state, GameContent content, BattleUnit unit, Coord tile, BattleUnit target) =>
        Signatures.Of(state, content, unit) == SignatureKind.Ledger
        && Queries.Forecast(state, content, unit, target, tile) is { } forecast
        && Signatures.Refuses(state, content, unit, forecast.Attacker.DisplayedHit);

    /// <summary>One player unit's commands on the board as it stands.</summary>
    public static IReadOnlyList<Command> PlanUnit(BattleState state, GameContent content, BattleUnit unit) =>
        PlanUnit(state, content, unit, out _);

    /// <summary>
    /// Whether an attack by <paramref name="unit"/> with <paramref name="weapon"/> from
    /// <paramref name="slot"/> is with a hungering weapon not yet woken (issue 804, DESIGN.md 13.23),
    /// the weapon the hunt weighs apart from the rest (<see cref="KillsOnHit"/>).
    /// </summary>
    public static bool Hunts(BattleUnit unit, int slot, Weapon weapon) =>
        weapon.Hungers && !Kinsbane.Woken(unit.Unit.Inventory.Items[slot].Fed);

    /// <summary>
    /// Whether <paramref name="armed"/>'s forecast from <paramref name="tile"/> kills
    /// <paramref name="target"/> on one plain hit (issue 851, round 276): the hunt swings a hungering
    /// weapon only for a kill, the scythe where the forecast kills and iron where a miss would cost.
    /// </summary>
    public static bool KillsOnHit(BattleState state, GameContent content, BattleUnit armed, Coord tile, BattleUnit target)
    {
        var me = (armed with { At = tile }).ToCombatant(state, content, against: target);
        var them = target.Answering(state, content, tile, armed);
        var side = Combat.Forecast(me, them, tile.DistanceTo(target.At), state.Scheme).Attacker;
        return side.Strikes && side.Damage >= target.Hp;
    }

    /// <summary>
    /// One player unit's commands on the board as it stands. <paramref name="refusedKill"/>
    /// is the highest kill probability among the attacks the veto refused for this unit,
    /// or null when it refused none. With <paramref name="casts"/> off it never casts an area tome.
    /// </summary>
    public static IReadOnlyList<Command> PlanUnit(BattleState state, GameContent content, BattleUnit unit, out double? refusedKill, bool casts = true)
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
        if (ExitTile(state, content, unit, tiles, reach) is { } exit)
        {
            return WithMove(unit, exit, new Exit(unit.Id));
        }

        if (StandTile(state, unit, tiles, reach) is { } stand)
        {
            return WithMove(unit, stand, new Wait(unit.Id));
        }

        var enemies = state.UnitsOf(Side.Enemy).ToList();
        var enemyReach = enemies.Select(e => state.ReachOf(e, content)).ToList();

        var arms = Arms(content, unit);
        var captains = CaptainsTiles(state, content, unit);
        Option? best = null;
        if (arms.Count > 0)
        {
            foreach (var tile in tiles)
            {
                var avoid = state.Map.TerrainAt(tile, content).AvoidFor(movement);
                var exposed = enemyReach.Count(r => r.CanEnd(tile));
                var cost = reach.CostTo(tile)!.Value;
                foreach (var (slot, armWeapon, armed) in arms)
                {
                    foreach (var target in enemies)
                    {
                        if (captains.Contains(tile) || !armWeapon.InRange(tile.DistanceTo(target.At)) || !Dusk.Sees(state, unit.Side, target.At, unit.Id, tile) || LedgerRefuses(state, content, armed, tile, target))
                        {
                            continue;
                        }

                        var critSafe = true;
                        if (LosesTheMap(state, unit))
                        {
                            var sum = Exposure.Of(state, content, unit, tile, target, slot);
                            if (sum.NoCrit >= unit.Hp)
                            {
                                var kill = KillProbability(state, content, armed, tile, target);
                                if (refusedKill is null || kill > refusedKill)
                                {
                                    refusedKill = kill;
                                }

                                continue;
                            }

                            critSafe = sum.WithCrit < unit.Hp;
                        }

                        var hungers = Hunts(unit, slot, armWeapon);
                        var feeds = hungers && KillsOnHit(state, content, armed, tile, target);
                        var option = new Option(EnemyAi.Score(state, content, armed, tile, target), target.Id, tile, critSafe, avoid, exposed, cost, slot)
                        {
                            Hunt = feeds,
                            Spares = hungers && !feeds,
                        };
                        if (best is null || option.Beats(best))
                        {
                            best = option;
                        }
                    }
                }
            }

        }

        if (casts && Cast(state, content, unit, tiles, captains, enemies) is { } cast && (best is null || (!best.Hunt && cast.Score > best.Score)))
        {
            return WithMove(unit, cast.From, new UseItem(unit.Id, cast.Slot, cast.At.ToString()));
        }

        if (best is not null)
        {
            return WithMove(unit, best.Tile, new Attack(unit.Id, best.TargetId, best.Slot == unit.EquippedSlot(content) ? null : best.Slot));
        }

        var heal = Heal(state, content, unit, tiles, enemyReach);
        if (heal is not null)
        {
            return heal;
        }

        if (unit.Moved || state.Map.Win == WinCondition.Survive)
        {
            return new Command[] { Idle(state, content, unit) };
        }

        var destination = Approach(state, content, unit, weapon, reach, enemies, enemyReach, Grounding.MovementOf(unit, content), CaptainsTiles(state, content, unit));
        return WithMove(unit, destination ?? unit.At, Idle(state, content, unit, destination));
    }

    /// <summary>
    /// The area cast the heuristic weighs against its best attack (issue 1391, <see cref="AreaCast.Best"/>): from a tile it
    /// may end on that is not one left free for a corked captain, at the enemies its side can see, and only from a tile
    /// whose no-crit exposure stays under its HP (issue 1395). A cast takes no counter, so its price never reads the tile
    /// it stands on; without the rule the storm walked the caster into the crowd it struck, and on the keep Pell died in
    /// twice the games she died in without it. The attack keeps its own rule: the veto, for a unit whose death loses the map.
    /// </summary>
    private static AreaCast.Choice? Cast(BattleState state, GameContent content, BattleUnit unit, IReadOnlyList<Coord> tiles, IReadOnlySet<Coord> captains, IReadOnlyList<BattleUnit> enemies)
    {
        if (AreaCast.Tomes(content, unit).Count == 0)
        {
            return null;
        }

        var seen = enemies.Where(e => Dusk.Sees(state, unit.Side, e.At)).ToList();
        return AreaCast.Best(state, content, unit, tiles.Where(t => !captains.Contains(t)), seen, tile => Exposure.Of(state, content, unit, tile).NoCrit >= unit.Hp);
    }

    /// <summary>
    /// The heuristic's action with no strike and no heal: Watch on an <c>overwatch: on</c> map when
    /// its equipped weapon reaches range 2 (DESIGN.md 13.17, the enemy's rule), else Wait. It never
    /// watches over a strike, so its own watches-over-a-strike figure is 0 by construction. On an
    /// <c>overwatch: hold</c> map (13.17b) it watches only when it stays where it began, since a
    /// watch there costs the move; the heuristic always takes the move when it has one.
    /// </summary>
    private static Command Idle(BattleState state, GameContent content, BattleUnit unit, Coord? at = null)
    {
        var moves = at is { } to && to != unit.At;
        if (state.Map.OverwatchEnabled && Overwatch.Refusal(state, content, unit) is null && !(state.Map.OverwatchHold && moves))
        {
            return new Watch(unit.Id);
        }

        return CoverChoice(state, content, unit with { At = at ?? unit.At }) is { } ally ? new Cover(unit.Id, ally) : new Wait(unit.Id);
    }

    /// <summary>
    /// The ally the heuristic covers in place of an idle Wait on a <c>cover: on</c> map (DESIGN.md
    /// 13.19), standing on <paramref name="unit"/>'s <see cref="BattleUnit.At"/>: of the allies beside
    /// it it may cover that some enemy could strike next phase, the lowest HP, then the lowest id.
    /// Null when none. It never covers over a strike, so every heuristic cover is one by a unit with
    /// nothing else to do, the case the kill criterion's second clause names.
    /// </summary>
    private static string? CoverChoice(BattleState state, GameContent content, BattleUnit unit)
    {
        if (!state.Map.CoverEnabled)
        {
            return null;
        }

        var struck = Threat.StruckBy(state, content, Side.Enemy);
        return state.UnitsOf(Side.Player)
            .Where(ally => CoverRule.Refusal(state, unit, ally) is null && struck.Contains(ally.At))
            .OrderBy(ally => ally.Hp)
            .ThenBy(ally => ally.Id, StringComparer.Ordinal)
            .FirstOrDefault()?.Id;
    }

    /// <summary>
    /// Issue 269's Escape approach: the exit tile a unit leaves from this turn, or null. A
    /// recruit leaves as soon as it can, ahead of any attack. The captain leaves last: only once
    /// no other player unit could still leave this turn or the next, since its exit leaves
    /// everyone on the board behind, and on the last turn regardless. Since issue 377 a unit
    /// leaves only from the exit it began its turn on (<see cref="StandTile"/> puts it there);
    /// on a map with <see cref="MapDefinition.ExitAfterMove"/> it leaves from the cheapest exit
    /// in its reach, then row-major. Null on any other map.
    /// </summary>
    public static Coord? ExitTile(BattleState state, GameContent content, BattleUnit unit, IReadOnlyList<Coord> tiles, Reach reach)
    {
        if (state.Map.Win != WinCondition.Escape)
        {
            return null;
        }

        if (!state.Map.ExitAfterMove)
        {
            if (unit.Moved || !state.Map.IsExit(unit.At))
            {
                return null;
            }

            if (unit.IsCaptain && state.Turn < state.Map.TurnLimit && state.UnitsOf(Side.Player).Any(other =>
                    other.Id != unit.Id
                    && (state.Map.IsExit(other.At) || (!other.Acted && !other.Moved && state.ReachOf(other, content).Destinations.Any(state.Map.IsExit)))))
            {
                return null;
            }

            return unit.At;
        }

        if (unit.IsCaptain && state.UnitsOf(Side.Player).Any(other =>
                other.Id != unit.Id && !other.Acted
                && (other.Moved ? state.Map.IsExit(other.At) : state.ReachOf(other, content).Destinations.Any(state.Map.IsExit))))
        {
            return null;
        }

        return CheapestExit(state, tiles, reach);
    }

    /// <summary>
    /// Issue 377's half of the Escape approach: the exit tile a unit that cannot leave this turn
    /// ends its turn on, so that it leaves on the next, the cheapest in its reach and then
    /// row-major, or null. A unit already on an exit stays on it. Null on a map with
    /// <see cref="MapDefinition.ExitAfterMove"/>, where <see cref="ExitTile"/> moves and leaves
    /// in one turn, and on any map that is not Escape.
    /// </summary>
    public static Coord? StandTile(BattleState state, BattleUnit unit, IReadOnlyList<Coord> tiles, Reach reach)
    {
        if (state.Map.Win != WinCondition.Escape || state.Map.ExitAfterMove)
        {
            return null;
        }

        return state.Map.IsExit(unit.At) ? unit.At : CheapestExit(state, tiles, reach);
    }

    private static Coord? CheapestExit(BattleState state, IReadOnlyList<Coord> tiles, Reach reach)
    {
        Coord? chosen = null;
        foreach (var tile in tiles)
        {
            if (state.Map.IsExit(tile)
                && (chosen is not { } best || reach.CostTo(tile)!.Value < reach.CostTo(best)!.Value
                    || (reach.CostTo(tile)!.Value == reach.CostTo(best)!.Value && tile.CompareTo(best) < 0)))
            {
                chosen = tile;
            }
        }

        return chosen;
    }

    /// <summary>
    /// The Canto of a unit owed one after its Attack, Item or Wait (issue 262): the tile in
    /// its Canto reach whose <see cref="Exposure"/> no-crit sum is lowest, priced on the
    /// board the Canto itself certainly determines (a group its stop wakes by proximity is
    /// awake on it; a wake already fired is already on the state). Lowest first is the
    /// veto's refusal for a unit whose death loses the map (<see cref="LosesTheMap"/>): a
    /// tile whose sum reaches its HP is taken only when no tile in reach is below it. Then
    /// that unit's crit-lethal flag, then staying, so the unit stays whenever its own tile
    /// is already as safe, then the approach's tile keys: avoid, fewer enemies reaching the
    /// tile, cost, row-major. A recruit never ends on a Seize throne (<see cref="MayEndOn"/>),
    /// and a unit standing on an Escape exit only moves between exits, since leaving one
    /// undoes the escape. A stay is the Canto declined.
    /// </summary>
    public static Canto PlanCanto(BattleState state, GameContent content, BattleUnit unit)
    {
        var reach = state.CantoReachOf(unit, content)
            ?? throw new ArgumentException($"{unit.Id} is not owed a Canto", nameof(unit));
        var movement = content.Class(unit.Unit.ClassId).Movement;
        var enemyReach = state.UnitsOf(Side.Enemy).Select(e => state.ReachOf(e, content)).ToList();
        var veto = LosesTheMap(state, unit);
        var onExit = state.Map.Win == WinCondition.Escape && state.Map.IsExit(unit.At);
        var chosen = unit.At;
        (int NoCrit, bool CritLethal, bool Moves, int Avoid, int Exposed, int Cost, Coord Tile) bestKey = default;
        var found = false;
        foreach (var tile in reach.Destinations)
        {
            if (!MayEndOn(state, content, unit, tile) || (onExit && !state.Map.IsExit(tile)))
            {
                continue;
            }

            var sum = Exposure.Of(state, content, unit, tile);
            var key = (
                sum.NoCrit,
                veto && sum.WithCrit >= unit.Hp,
                tile != unit.At,
                -state.Map.TerrainAt(tile, content).AvoidFor(movement),
                enemyReach.Count(r => r.CanEnd(tile)),
                reach.CostTo(tile)!.Value,
                tile);
            if (!found || key.CompareTo(bestKey) < 0)
            {
                bestKey = key;
                chosen = tile;
                found = true;
            }
        }

        return new Canto(unit.Id, chosen);
    }

    /// <summary>
    /// The chance that <paramref name="attacker"/>, attacking from <paramref name="tile"/>
    /// with its equipped weapon, kills <paramref name="target"/> over the strikes it lives to
    /// make: the first round, and the second when it doubles, each strike landing at the forecast's
    /// resolved hit probability under the game's scheme and critting at its crit chance for
    /// <see cref="Combat.CritMultiplier"/> times the damage (issue 125). A kill that needs the
    /// second round is weighted by the chance the attacker survives the counter thrown
    /// between them (issue 147): the defender's round when it reaches, at its own hit and
    /// crit, killing when its damage reaches the attacker's current HP. A first-round kill
    /// takes no counter and is unchanged; a defender that cannot strike back, or whose raw
    /// hit is 0, weighs the second round at one. A round is one strike, or two for a
    /// gauntlet (issue 70).
    /// </summary>
    public static double KillProbability(BattleState state, GameContent content, BattleUnit attacker, Coord tile, BattleUnit target)
    {
        var me = (attacker with { At = tile }).ToCombatant(state, content, against: target);
        var them = target.Answering(state, content, tile, attacker);
        var forecast = Combat.Forecast(me, them, tile.DistanceTo(target.At), state.Scheme);
        var side = forecast.Attacker;
        var outcomes = RoundOutcomes(side, state.Scheme);
        var survivesCounter = 1 - KillsWith(forecast.Defender, attacker.Hp, state.Scheme);
        var kill = 0.0;
        foreach (var (firstP, firstDamage) in outcomes)
        {
            if (firstDamage >= target.Hp)
            {
                kill += firstP;
                continue;
            }

            if (side.Rounds < 2)
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
        return new[] { (1 - hit, 0), (hit * (1 - crit), side.Damage), (hit * crit, side.CritDamage) };
    }

    /// <summary>
    /// The outcomes of one round of <paramref name="side"/>'s strikes with their
    /// probabilities: a single strike's three, or every sequence of them over a gauntlet's
    /// two strikes with their damage summed.
    /// </summary>
    private static IReadOnlyList<(double P, int Damage)> RoundOutcomes(SideForecast side, RollScheme scheme)
    {
        var one = Outcomes(side, scheme);
        IReadOnlyList<(double P, int Damage)> round = one;
        for (var strike = 1; strike < side.StrikesPerRound; strike++)
        {
            round = round.SelectMany(a => one.Select(b => (a.P * b.P, a.Damage + b.Damage))).ToList();
        }

        return round;
    }

    /// <summary>The chance one round of <paramref name="side"/> kills a unit at <paramref name="hp"/>; zero when the side does not strike.</summary>
    private static double KillsWith(SideForecast side, int hp, RollScheme scheme) =>
        side.Strikes ? RoundOutcomes(side, scheme).Where(o => o.Damage >= hp).Sum(o => o.P) : 0;

    internal static IReadOnlyList<Command> WithMove(BattleUnit unit, Coord tile, Command action) =>
        tile == unit.At ? new[] { action } : new Command[] { new Move(unit.Id, tile), action };

    /// <summary>
    /// A recruit never ends a move on a throne tile of a Seize map (issue 111): only the
    /// captain can seize (section 7), so a recruit standing there blocks the win for as long
    /// as it stays, and the approach toward the throne would otherwise put the first recruit
    /// to arrive exactly there. The captain may end anywhere in reach.
    /// </summary>
    internal static bool MayEndOn(BattleState state, GameContent content, BattleUnit unit, Coord tile) =>
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
            if (content.Items.TryGetValue(stack.ItemId, out var item))
            {
                if (item.Teaches is null && unit.Hp * 2 < unit.MaxHp(content) && stack.Uses > 0)
                {
                    return WithMove(unit, safest[0], new UseItem(unit.Id, slot));
                }

                continue;
            }

            var spell = content.WeaponOf(unit.Unit, content.Weapon(stack.ItemId));
            if (!spell.Heals || spell.Cleanses || spell.AreaHeal > 0 || !unit.Unit.CanWield(spell, unitClass) || stack.Uses == 0)
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
    /// a crit cannot kill on; every other recruit takes <see cref="EnemyAi.Approach"/>, unless
    /// that tile's no-crit exposure reaches its HP, when it takes the veto's key with lethal
    /// tiles last and crit-lethal ones after the rest (issue 1044). It never ends on one of
    /// <paramref name="captains"/>, the tiles left free for a corked captain (issue 1206), and when
    /// section 8's tile is one of them it takes the veto's key as for a lethal one.
    /// </summary>
    private static Coord? Approach(
        BattleState state, GameContent content, BattleUnit unit, Weapon? weapon, Reach reach,
        List<BattleUnit> enemies, List<Reach> enemyReach, MovementType movement, IReadOnlySet<Coord> captains)
    {
        Occupant OccupantAt(Coord at) => at == unit.At ? Occupant.None : state.OccupantAt(at, unit.Side);
        var footing = content.AbilitiesOf(unit.Unit);
        Distances? TowardNearestEnemy(Weapon armed)
        {
            Distances? chosen = null;
            var nearest = int.MaxValue;
            foreach (var target in enemies)
            {
                var distances = Movement.DistancesTo(state.Map, content, EnemyAi.AttackTiles(state, content, unit, armed, target, movement), movement, OccupantAt, footing);
                if (distances.From(unit.At) is { } c && c < nearest)
                {
                    chosen = distances;
                    nearest = c;
                }
            }

            return chosen;
        }

        // Section 8's approach, and whether it ends where the cycle's no-crit sum kills (issue 1044).
        var careful = LosesTheMap(state, unit);
        (Coord? At, bool Lethal) Blind(Weapon armed)
        {
            var at = EnemyAi.Approach(state, content, unit, armed, reach, enemies, enemyReach);
            careful = at is { } end && (captains.Contains(end) || Exposure.Of(state, content, unit, end).NoCrit >= unit.Hp);
            return (at, careful);
        }

        Distances? toward = null;
        switch (state.Map.Win)
        {
            case WinCondition.Seize:
                toward = Movement.DistancesTo(state.Map, content, state.Map.TilesOf(MapDefinition.ThroneTerrainId), movement, OccupantAt, footing);
                break;
            case WinCondition.Escape:
                toward = Movement.DistancesTo(state.Map, content, state.Map.Exits, movement, OccupantAt, footing);
                break;
            default:
                if (weapon is null)
                {
                    return null;
                }

                if (!LosesTheMap(state, unit) && Blind(weapon) is var blind && !blind.Lethal)
                {
                    return blind.At;
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
            if (!LosesTheMap(state, unit) && Blind(weapon) is var blind && !blind.Lethal)
            {
                return blind.At;
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
            if (toward.From(tile) is not { } remaining || !MayEndOn(state, content, unit, tile) || captains.Contains(tile))
            {
                continue;
            }

            var lethal = false;
            var critLethal = false;
            if (careful)
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
    private sealed record Option(double Score, string TargetId, Coord Tile, bool CritSafe, int Avoid, int Exposed, int Cost, int Slot)
    {
        /// <summary>
        /// The hunt (issue 804, round 251; issue 851, round 276: only to kill): an attack with a
        /// hungering weapon not yet woken (DESIGN.md 13.23) whose forecast kills on a hit beats any
        /// other attack, so every kill it can take feeds it; among such attacks the score decides.
        /// </summary>
        public bool Hunt { get; init; }

        /// <summary>
        /// An attack with a hungering weapon not yet woken that does not kill on a hit (issue 851):
        /// it loses to any attack with another weapon, so the carrier swings the best other weapon by
        /// the usual score, and the scythe only when nothing else reaches.
        /// </summary>
        public bool Spares { get; init; }

        public bool Beats(Option other)
        {
            if (Hunt != other.Hunt)
            {
                return Hunt;
            }

            if (Spares != other.Spares)
            {
                return !Spares;
            }

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
