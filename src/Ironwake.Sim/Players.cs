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

/// <summary>
/// Whom the heuristic lays a one-hit shell on (issue 1403, Lotus asked for the read on three targets): the most exposed
/// ally or the caster, or only the drake rider, only a healer, or only the ally of the highest Def other than the caster;
/// or no one, the tome held and never laid, the read's baseline for what holding it costs the plan order.
/// </summary>
public enum ShellAim
{
    Exposed,
    Never,
    Drake,
    Healer,
    Front,
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
/// sets); else a heal at half HP or below (a healing spell on the most wounded ally in range,
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
    /// <see cref="AreaCast.Best"/>); on unless a caller turns it off, as the campaign script's writer does for the
    /// Psalter-art script (issue 1392).
    /// </summary>
    public bool Casts { get; init; } = true;

    /// <summary>Whom this player lays a one-hit shell on (issue 1403, <see cref="ShellAim"/>); the most exposed ally unless a caller names a target.</summary>
    public ShellAim Shell { get; init; } = ShellAim.Exposed;

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

        var plan = PlanUnit(state, content, unit, out var refused, Casts, Shell);
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
    /// captain finishes from the safe tile. Ahead of either, a unit that can feed a hungering weapon
    /// this turn (<see cref="CanFeed"/>, issue 1395) plans first, so the kill its hunt would take is
    /// not taken by an ally planning before it; the rest keep their order. A unit holding a shell it can lay
    /// (<see cref="ShellTome"/>, issue 1403) plans after the rest, so the allies it may shell stand where they end.
    /// </summary>
    public static IEnumerable<BattleUnit> PlanOrder(BattleState state, GameContent content)
    {
        var order = Corked(state, content) is { } captain && !captain.Acted && SafeStrikeTiles(state, content, captain).Count > 0
            ? state.UnitsOf(Side.Player).OrderBy(u => u.IsCaptain)
            : PlanOrder(state);
        return order.OrderBy(u => !CanFeed(state, content, u)).ThenBy(u => ShellTome(content, u) is not null);
    }

    /// <summary>
    /// Whether <paramref name="unit"/>, not yet acted, carries a hungering weapon not yet woken
    /// (<see cref="Hunts"/>) that kills some enemy it can see on one plain hit (<see cref="KillsOnHit"/>)
    /// from a tile it may end on this turn, one not left free for a corked captain: the attack its hunt
    /// ranks first (issue 1395). On the keep the scythe-bearer planned after allies who took those kills,
    /// went unfed and drained, and the company won fewer games carrying Kinsbane than without it.
    /// </summary>
    public static bool CanFeed(BattleState state, GameContent content, BattleUnit unit)
    {
        if (unit.Acted || unit.Moved)
        {
            return false;
        }

        var slot = Kinsbane.Slot(unit, content);
        if (slot < 0 || unit.UsableWeaponAt(content, slot) is not { } weapon || !Hunts(unit, slot, weapon))
        {
            return false;
        }

        var armed = unit.WithSlotInFront(slot);
        var captains = CaptainsTiles(state, content, unit);
        var enemies = state.UnitsOf(Side.Enemy).ToList();
        foreach (var tile in state.ReachOf(unit, content).Destinations)
        {
            if (!MayEndOn(state, content, unit, tile) || captains.Contains(tile))
            {
                continue;
            }

            foreach (var target in enemies)
            {
                if (weapon.InRange(tile.DistanceTo(target.At)) && Dusk.Sees(state, unit.Side, target.At, unit.Id, tile)
                    && !LedgerRefuses(state, content, armed, tile, target) && KillsOnHit(state, content, armed, tile, target))
                {
                    return true;
                }
            }
        }

        return false;
    }

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
    /// <paramref name="shell"/> names whom it lays a one-hit shell on (<see cref="LayShell"/>).
    /// </summary>
    public static IReadOnlyList<Command> PlanUnit(BattleState state, GameContent content, BattleUnit unit, out double? refusedKill, bool casts = true, ShellAim shell = ShellAim.Exposed)
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

        if (TakeTile(state, content, unit, tiles) is { } take)
        {
            return WithMove(unit, take.Tile, new TakeShard(unit.Id, take.BossId));
        }

        var seized = SeizeTile(state, content, unit, tiles);
        if (seized is { } throne)
        {
            tiles = new List<Coord> { throne };
        }

        var enemies = state.UnitsOf(Side.Enemy).Where(e => !ShardRun.Running(e)).ToList();
        var enemyReach = enemies.Select(e => state.ReachOf(e, content)).ToList();

        var arms = Arms(content, unit);
        var captains = CaptainsTiles(state, content, unit);
        if (Finisher(state, content, unit, tiles.Where(t => !captains.Contains(t)).ToList(), enemies) is { } finish)
        {
            return WithMove(unit, finish.Tile, new Attack(unit.Id, finish.TargetId, finish.Slot == unit.EquippedSlot(content) ? null : finish.Slot, finish.Art));
        }

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

        if ((best is null || (!best.Hunt && KillProbability(state, content, arms.First(a => a.Slot == best.Slot).Armed, best.Tile, state.Find(best.TargetId)!) < ShellOverKill))
            && LayShell(state, content, unit, tiles.Where(t => !captains.Contains(t)).ToList(), shell) is { } laid)
        {
            return laid;
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

        if (seized is { } held)
        {
            return WithMove(unit, held, Idle(state, content, unit, held));
        }

        if (unit.Moved || state.Map.Win == WinCondition.Survive)
        {
            return new Command[] { Idle(state, content, unit) };
        }

        var destination = weapon is null && arms.Count == 0 && state.Map.Win is not (WinCondition.Seize or WinCondition.Escape) && Healer(content, unit)
            ? HealerWalk(state, content, unit, reach, enemies, Grounding.MovementOf(unit, content), captains)
            : Approach(state, content, unit, weapon, reach, enemies, enemyReach, Grounding.MovementOf(unit, content), CaptainsTiles(state, content, unit));
        if (Clear(state, content, unit, tiles.Where(t => !captains.Contains(t)).ToList(), enemies, destination ?? unit.At) is { } clear)
        {
            destination = clear;
        }

        return WithMove(unit, destination ?? unit.At, Idle(state, content, unit, destination));
    }

    /// <summary>
    /// The take (issue 1386): a unit that has not acted and can end on a tile beside a boss running with the shard takes
    /// it from there, ahead of every other plan, since the take ends the race and, on Defeat Boss, the map. The tile is
    /// the first of <paramref name="tiles"/> beside the first runner in unit order. With no runner, a shard lying under
    /// the hill (<see cref="KinShard"/>, slice 3b) is taken from the tile of <paramref name="tiles"/> on or beside it whose
    /// no-crit exposure is lowest, then nearest, then first, and only from one whose exposure stays under the unit's HP:
    /// that take ends nothing, so it is not exempt from the veto. The boss id is then <see cref="KinShard.Ground"/>. Null
    /// when neither is on offer.
    /// </summary>
    public static (Coord Tile, string BossId)? TakeTile(BattleState state, GameContent content, BattleUnit unit, IReadOnlyList<Coord> tiles)
    {
        if (unit.Acted)
        {
            return null;
        }

        if (KinShard.Of(state) is { Lies: { } lies })
        {
            (int, int, int)? bestKey = null;
            Coord? best = null;
            for (var i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                if (tile.DistanceTo(lies) > 1)
                {
                    continue;
                }

                var exposure = Exposure.Of(state, content, unit, tile).NoCrit;
                if (exposure >= unit.Hp)
                {
                    continue;
                }

                var key = (exposure, tile.DistanceTo(unit.At), i);
                if (bestKey is null || key.CompareTo(bestKey.Value) < 0)
                {
                    bestKey = key;
                    best = tile;
                }
            }

            if (best is { } taken)
            {
                return (taken, KinShard.Ground);
            }
        }

        foreach (var runner in state.UnitsOf(Side.Enemy).Where(ShardRun.Running))
        {
            foreach (var tile in tiles)
            {
                if (tile.DistanceTo(runner.At) == 1)
                {
                    return (tile, runner.Id);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The hit at or above which the finisher is declared whatever the veto's sum says (issue 1441, Table round 535;
    /// Code's number, open): a miss leaves him where the sum prices, and at this hit the miss is rare enough to take.
    /// </summary>
    public const int FinisherHit = 90;

    /// <summary>A finisher <see cref="Finisher"/> found: the art, the slot it strikes from, the tile and the boss.</summary>
    public sealed record Finish(string Art, int Slot, Coord Tile, string TargetId, int Hit);

    /// <summary>
    /// Whether <paramref name="boss"/>'s fall ends the map (issue 1441): on Defeat Boss, a boss that would not swallow
    /// (<see cref="Swallow.Takes"/>: it carries no stage, or has swallowed), the only boss standing, with every boss spawn
    /// fired. Stage 1 Hask is not one; his fall is the swallow.
    /// </summary>
    public static bool EndsTheMap(BattleState state, BattleUnit boss) =>
        state.Map.Win == WinCondition.DefeatBoss && boss is { IsBoss: true, Side: Side.Enemy } && !Swallow.Takes(boss)
        && state.UnitsOf(Side.Enemy).Count(u => u.IsBoss) == 1 && state.Map.BossSpawns().All(e => state.HasFired(e.Name));

    /// <summary>
    /// The finisher (issue 1441, Table round 535): an art <paramref name="unit"/> knows with a per-map charge
    /// (Full Measure) that kills a swallowed boss whose fall ends the map (<see cref="EndsTheMap"/>, stage 2) on its one hit, a single strike
    /// whose non-crit first hit reaches his HP, from a tile in <paramref name="tiles"/>. The Sim never declared an art
    /// before, and on the Warden sample the captain had this kill on offer in 172 of full's stage-2 phases and swung
    /// none (`keep-1441-captain.txt`). A landed hit ends the map, so only the miss is priced: for a unit the veto covers,
    /// a tile is taken when it survives the miss there (<see cref="MissPrice"/>: two enemy phases for Full Measure, which
    /// costs the next phase) or the hit is at least <see cref="FinisherHit"/>. The best hit wins, then the lower no-crit exposure, then row order. Null when none.
    /// Stage 2 alone (DECISIONS/0380): on every Defeat Boss map it moved Harrow Weir, a tuned map, from 132 to 164 of 200,
    /// which waits on the Table.
    /// </summary>
    public static Finish? Finisher(BattleState state, GameContent content, BattleUnit unit, IReadOnlyList<Coord> tiles, IReadOnlyList<BattleUnit> enemies)
    {
        var arts = content.ArtsOf(unit.Unit).Where(a => a.Art.PerMap is not null).ToDictionary(a => a.Ability.Id, a => a.Art.CostsNextPhase);
        var bosses = enemies.Where(e => e.Swallowed && EndsTheMap(state, e)).ToList();
        if (arts.Count == 0 || bosses.Count == 0)
        {
            return null;
        }

        Finish? best = null;
        var bestExposure = 0;
        foreach (var boss in bosses)
        {
            foreach (var tile in tiles)
            {
                foreach (var (slot, _, _) in Arms(content, unit))
                {
                    foreach (var (art, costs) in arts)
                    {
                        if (Queries.Forecast(state, content, unit, boss, tile, slot, art) is not { } forecast
                            || forecast.Attacker is not { Strikes: true, Doubles: false } side
                            || side.FirstHit(crit: false) + side.Stoop < boss.Hp)
                        {
                            continue;
                        }

                        var exposure = LosesTheMap(state, unit) ? MissPrice(state, content, unit, tile, boss, slot, costs) : 0;
                        if (exposure >= unit.Hp && side.HitChance < FinisherHit)
                        {
                            continue;
                        }

                        var finish = new Finish(art, slot, tile, boss.Id, side.HitChance);
                        if (best is null || finish.Hit > best.Hit || (finish.Hit == best.Hit && (exposure < bestExposure
                            || (exposure == bestExposure && (tile.Y, tile.X).CompareTo((best.Tile.Y, best.Tile.X)) < 0))))
                        {
                            (best, bestExposure) = (finish, exposure);
                        }
                    }
                }
            }
        }

        return best;
    }

    /// <summary>
    /// What a missed finisher from <paramref name="tile"/> costs <paramref name="unit"/> (Table rounds 536, 537): the veto's
    /// no-crit sum after a plain strike on <paramref name="boss"/>, and for an art that costs the next phase, which leaves
    /// him on that tile through two enemy phases (<c>Resolver</c>'s <c>Spent</c>), the sum twice with the second phase's
    /// Frozen Iron landing in place of the first's (the dose climbs a step a landing, <see cref="Swallow.Step"/>; a held
    /// first landing lands the set dose second).
    /// </summary>
    public static int MissPrice(BattleState state, GameContent content, BattleUnit unit, Coord tile, BattleUnit boss, int slot, bool costsNextPhase)
    {
        var sum = Exposure.Of(state, content, unit, tile, boss, slot).NoCrit;
        if (!costsNextPhase)
        {
            return sum;
        }

        var enemy = unit.Side == Side.Player ? Side.Enemy : Side.Player;
        var first = Swallow.NextLanding(state, unit.Side);
        var second = Swallow.Casts(state, enemy) ? state.FrozenIron + (state.FrozenIronHeld ? 0 : Swallow.Step(state, enemy)) : 0;
        return 2 * sum - first + second;
    }

    /// <summary>
    /// The captain standing clear in stage 2 (issue 1441, Table round 535): with a swallowed boss standing whose fall ends
    /// the map, when the captain's walk (or his stand) ends on <paramref name="end"/>, a tile that boss's arms or line strike
    /// reach (<see cref="InBossReach"/>), the tile of <paramref name="tiles"/> the boss does not reach and whose no-crit sum
    /// stays under his HP, nearest <paramref name="end"/>, then the lower sum, then row order. On the Warden sample he ended
    /// 198 stage-2 phases in Hask's reach, 129 of them walked in, and dealt him 27 damage in 164 games: the dose's
    /// body, not a striker. Null for anyone else, outside stage 2, when <paramref name="end"/> is already clear, or when
    /// no tile is.
    /// </summary>
    public static Coord? Clear(BattleState state, GameContent content, BattleUnit unit, IReadOnlyList<Coord> tiles, IReadOnlyList<BattleUnit> enemies, Coord end)
    {
        if (!unit.IsCaptain || unit.Moved || enemies.FirstOrDefault(e => e.Swallowed && EndsTheMap(state, e)) is not { } boss
            || !InBossReach(state, content, boss, end))
        {
            return null;
        }

        return tiles
            .Where(t => !InBossReach(state, content, boss, t))
            .Select(t => (Tile: t, Sum: Exposure.Of(state, content, unit, t).NoCrit))
            .Where(t => t.Sum < unit.Hp)
            .OrderBy(t => t.Tile.DistanceTo(end))
            .ThenBy(t => t.Sum)
            .ThenBy(t => t.Tile.Y)
            .ThenBy(t => t.Tile.X)
            .Select(t => (Coord?)t.Tile)
            .FirstOrDefault();
    }

    /// <summary>
    /// Whether <paramref name="boss"/> can strike <paramref name="tile"/> in his coming phase (issue 1441): an arm in range
    /// from his tile or, unless he holds, from any tile he can end on, or his line strike's cross (<see cref="LineStrike"/>).
    /// </summary>
    public static bool InBossReach(BattleState state, GameContent content, BattleUnit boss, Coord tile)
    {
        var arms = Arms(content, boss);
        if (arms.Any(a => a.Weapon.InRange(tile.DistanceTo(boss.At)))
            || (LineStrike.Of(content, boss) is { } line && LineStrike.Cross(state, content, boss.At, line.Reach).Contains(tile)))
        {
            return true;
        }

        return boss.Behavior != Behavior.Hold
            && state.ReachOf(boss, content).Destinations.Any(d => arms.Any(a => a.Weapon.InRange(tile.DistanceTo(d))));
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
    /// The kill probability under which the heuristic lays a shell rather than take its best attack (issue 1403): a strike
    /// likely to kill is taken, one likely to leave the target standing gives way to the shell.
    /// </summary>
    public const double ShellOverKill = 0.5;

    /// <summary>The slot and tome of a one-hit shell <paramref name="unit"/> can lay now (issue 1403, <see cref="Armor"/>): one it can wield with a use left; null when it holds none.</summary>
    public static (int Slot, Weapon Spell)? ShellTome(GameContent content, BattleUnit unit)
    {
        var unitClass = content.Class(unit.Unit.ClassId);
        for (var slot = 0; slot < unit.Unit.Inventory.Count; slot++)
        {
            var stack = unit.Unit.Inventory.Items[slot];
            if (stack.Uses == 0 || !content.Weapons.ContainsKey(stack.ItemId))
            {
                continue;
            }

            var spell = content.Weapon(stack.ItemId);
            if (Armor.Armors(content, spell) && spell.Armor!.Shell && unit.Unit.CanWield(spell, unitClass))
            {
                return (slot, spell);
            }
        }

        return null;
    }

    /// <summary>
    /// The heuristic's one-hit shell (issue 1403, Lotus's Sim read): with a shell tome it can lay, on an ally
    /// <paramref name="aim"/> admits that wears no armor, has already acted (so the tile it meets the enemy phase on is
    /// known) and stands where some enemy can strike it this coming phase (no-crit exposure above 0), within the tome's
    /// range of a tile the caster may end on; or on the caster itself at that tile when the aim admits it. Among them the
    /// most exposed, from the tile where the caster's own exposure is least, row order breaking ties. Null when none qualifies.
    /// </summary>
    private static IReadOnlyList<Command>? LayShell(BattleState state, GameContent content, BattleUnit unit, IReadOnlyList<Coord> tiles, ShellAim aim)
    {
        if (ShellTome(content, unit) is not var (slot, spell))
        {
            return null;
        }

        var allies = state.UnitsOf(unit.Side).ToList();
        var wearers = allies.Where(a => a.Armor is null && (a.Id == unit.Id || a.Acted) && Aimed(content, unit, a, allies, aim)).ToList();
        if (wearers.Count == 0)
        {
            return null;
        }

        var exposed = wearers.Where(w => w.Id != unit.Id).ToDictionary(w => w.Id, w => Exposure.Of(state, content, w, w.At).NoCrit);
        foreach (var (tile, own) in tiles.Select(t => (Tile: t, Own: Exposure.Of(state, content, unit, t).NoCrit)).OrderBy(t => t.Own).ThenBy(t => t.Tile))
        {
            BattleUnit? pick = null;
            var most = 0;
            foreach (var wearer in wearers)
            {
                var exposure = wearer.Id == unit.Id ? own : tile.DistanceTo(wearer.At) <= spell.Armor!.Range ? exposed[wearer.Id] : 0;
                if (exposure > most)
                {
                    (pick, most) = (wearer, exposure);
                }
            }

            if (pick is not null)
            {
                return WithMove(unit, tile, new UseItem(unit.Id, slot, pick.Id == unit.Id ? null : pick.Id));
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="aim"/> admits <paramref name="ally"/> as the wearer of <paramref name="caster"/>'s shell.</summary>
    private static bool Aimed(GameContent content, BattleUnit caster, BattleUnit ally, IReadOnlyList<BattleUnit> allies, ShellAim aim) => aim switch
    {
        ShellAim.Drake => ally.Unit.Drake is not null,
        ShellAim.Healer => Healer(content, ally),
        ShellAim.Never => false,
        ShellAim.Front => ally.Id == allies.Where(a => a.Id != caster.Id).OrderByDescending(a => content.StatsOf(a.Unit).Def).ThenBy(a => a.Id, StringComparer.Ordinal).FirstOrDefault()?.Id,
        _ => true,
    };

    /// <summary>Whether <paramref name="unit"/> carries a healing spell it can wield (issue 1403's healer target).</summary>
    public static bool Healer(GameContent content, BattleUnit unit) =>
        unit.Unit.Inventory.Items.Any(s => content.Weapons.TryGetValue(s.ItemId, out var w) && w.Heals && unit.Unit.CanWield(w, content.Class(unit.Unit.ClassId)));

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
    /// The seize step (issue 1409): on a Seize map, the throne tile a captain that has not moved can end on this turn,
    /// or null. Its attack, cast or heal is then planned from that tile alone, and with none it waits there, so a heal
    /// or a strike from elsewhere never costs the throne. On a plain Seize map the step wins outright and is always
    /// taken. On a map that must hold the throne through an enemy phase it is taken when the tile's no-crit exposure
    /// stays under the captain's HP, the veto's rule, and on the last turn regardless, when not stepping loses the map
    /// as surely as falling does.
    /// </summary>
    public static Coord? SeizeTile(BattleState state, GameContent content, BattleUnit unit, IReadOnlyList<Coord> tiles)
    {
        if (state.Map.Win != WinCondition.Seize || !unit.IsCaptain || unit.Moved)
        {
            return null;
        }

        foreach (var tile in tiles.Where(state.Map.IsThrone).OrderBy(t => t == unit.At ? 0 : 1).ThenBy(t => t))
        {
            if (!state.Map.SeizeHold || state.Turn >= state.Map.TurnLimit || Exposure.Of(state, content, unit, tile).NoCrit < unit.Hp)
            {
                return tile;
            }
        }

        return null;
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

    /// <summary>
    /// Whether <paramref name="unit"/> is wounded enough for the heuristic to heal it: at half its max HP or below (issue 1429).
    /// The threshold was below half, and on the Mill 20 of 41 timeouts were the captain parked at exactly 11/22 with every
    /// approach lethal at 11 and his own Field Dressing and Maud's Salve unused to the turn limit (DECISIONS/0367).
    /// </summary>
    public static bool Wounded(GameContent content, BattleUnit unit) => unit.Hp * 2 <= unit.MaxHp(content);

    /// <summary>
    /// A heal when one is wanted: a healing spell on the most wounded ally at half or below, else a consumable on itself at
    /// half or below (<see cref="Wounded"/>), from the safest tile that allows it. Safest is the lowest no-crit exposure for a unit the veto covers, and the
    /// fewest enemies able to end beside the tile for the rest; a healer with no weapon (issue 1441, Table rounds 533 and
    /// 534) ranks by the no-crit exposure first and never casts from a tile whose no-crit sum reaches her HP, so with only
    /// lethal tiles in reach of the patient she heals no one and walks instead. Without the rule the keep's chaplain healed
    /// from the tile the fewest enemies reached, however hard one of them hit, and 40 of her 83 depleted falls came after a heal.
    /// </summary>
    private static IReadOnlyList<Command>? Heal(BattleState state, GameContent content, BattleUnit unit, List<Coord> tiles, List<Reach> enemyReach)
    {
        var unitClass = content.Class(unit.Unit.ClassId);
        var careful = LosesTheMap(state, unit) || (Arms(content, unit).Count == 0 && Healer(content, unit));
        var exposure = careful ? tiles.ToDictionary(t => t, t => Exposure.Of(state, content, unit, t).NoCrit) : null;
        var guarded = !LosesTheMap(state, unit) && exposure is not null;
        var safest = tiles.OrderBy(t => exposure is null ? 0 : exposure[t]).ThenBy(t => LosesTheMap(state, unit) ? 0 : enemyReach.Count(r => r.CanEnd(t))).ThenBy(t => t).ToList();
        for (var slot = 0; slot < unit.Unit.Inventory.Count; slot++)
        {
            var stack = unit.Unit.Inventory.Items[slot];
            if (content.Items.TryGetValue(stack.ItemId, out var item))
            {
                if (item.Teaches is null && Wounded(content, unit) && stack.Uses > 0)
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
                if (guarded && exposure![tile] >= unit.Hp)
                {
                    break;
                }

                BattleUnit? wounded = null;
                foreach (var ally in state.UnitsOf(Side.Player))
                {
                    if (ally.Id != unit.Id && spell.InRange(tile.DistanceTo(ally.At)) && Wounded(content, ally)
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
    /// Where a healer with no weapon walks when it has no heal to give (issue 1395, Table rounds 524 and 525, DECISIONS/0377):
    /// on a Rout or Defeat Boss map section 8's approach has no destination for a unit that cannot strike, and without this
    /// rule the keep's chaplain stood where she deployed all game. Toward the nearest ally at half HP or below (the heal's own
    /// threshold) when one has a path, by the fewest steps left to a tile her heal reaches it from; else behind the ally
    /// standing nearest a seen enemy (any enemy when none is seen): a tile from which that ally is within her Move and her
    /// heal's range next phase first, then the fewest steps left. Only a tile that closes on the ally, where no enemy's
    /// no-crit strike reaches her (exposure 0), and that is not left free for a corked captain counts; null when none
    /// does, and she waits: the idle read counts that wait apart, as a healer's. Round 525's fallback to the least
    /// exposed tile was screened and lost (0377): she fell more and the company won less.
    /// </summary>
    private static Coord? HealerWalk(BattleState state, GameContent content, BattleUnit unit, Reach reach, List<BattleUnit> enemies, MovementType movement, IReadOnlySet<Coord> captains)
    {
        var allies = state.UnitsOf(unit.Side).Where(a => a.Id != unit.Id).OrderBy(a => a.Id, StringComparer.Ordinal).ToList();
        if (allies.Count == 0)
        {
            return null;
        }

        var unitClass = content.Class(unit.Unit.ClassId);
        var heals = unit.Unit.Inventory.Items
            .Where(s => content.Weapons.TryGetValue(s.ItemId, out var w) && w.Heals && unit.Unit.CanWield(w, unitClass))
            .Select(s => content.Weapon(s.ItemId))
            .ToList();
        Occupant OccupantAt(Coord at) => at == unit.At ? Occupant.None : state.OccupantAt(at, unit.Side);
        var footing = content.AbilitiesOf(unit.Unit);
        Distances Toward(BattleUnit ally)
        {
            var from = new List<Coord>();
            for (var y = 0; y < state.Map.Height; y++)
            {
                for (var x = 0; x < state.Map.Width; x++)
                {
                    var tile = new Coord(x, y);
                    if (heals.Any(h => h.InRange(tile.DistanceTo(ally.At))))
                    {
                        from.Add(tile);
                    }
                }
            }

            return Movement.DistancesTo(state.Map, content, from, movement, OccupantAt, footing);
        }

        var hurt = allies.Where(a => Wounded(content, a))
            .Select(a => (Ally: a, Toward: Toward(a)))
            .Where(a => a.Toward.From(unit.At) is not null)
            .OrderBy(a => a.Toward.From(unit.At))
            .Select(a => ((BattleUnit, Distances)?)a)
            .FirstOrDefault();
        var tending = hurt is not null;
        Distances toward;
        if (hurt is { } h)
        {
            toward = h.Item2;
        }
        else
        {
            var seen = enemies.Where(e => Dusk.Sees(state, unit.Side, e.At)).ToList();
            var foes = seen.Count > 0 ? seen : enemies;
            if (foes.Count == 0)
            {
                return null;
            }

            var front = allies.OrderBy(a => foes.Min(f => a.At.DistanceTo(f.At))).First();
            toward = Toward(front);
        }

        if (toward.From(unit.At) is not { } now)
        {
            return null;
        }

        Coord? destination = null;
        (int, int, int, Coord) bestKey = default;
        foreach (var tile in reach.Destinations)
        {
            if (toward.From(tile) is not { } remaining || remaining >= now || !MayEndOn(state, content, unit, tile) || captains.Contains(tile))
            {
                continue;
            }

            if (Exposure.Of(state, content, unit, tile).NoCrit > 0)
            {
                continue;
            }

            var cost = reach.CostTo(tile)!.Value;
            var key = tending
                ? (remaining, 0, cost, tile)
                : (remaining <= unitClass.Mov ? 0 : 1, remaining, cost, tile);
            if (destination is null || key.CompareTo(bestKey) < 0)
            {
                bestKey = key;
                destination = tile;
            }
        }

        return destination ?? StepBack(state, content, unit, reach, toward, captains);
    }

    /// <summary>
    /// Where a healer with no weapon goes when no exposure-0 tile closes on the ally she walks to (issue 1441, Table rounds
    /// 533 and 534): when some enemy's no-crit strike reaches the tile she stands on, the exposure-0 tile nearest that ally
    /// by the steps left, then the cheapest, then row order; null when she already stands at exposure 0 or no tile in her
    /// reach is, and she waits. Without it she waited on the tile her last heal left her on, and 42 of her 83 depleted
    /// falls came after such a wait. No exposed fallback: 0377's screen showed a healer who arrives hurt does not pay.
    /// </summary>
    private static Coord? StepBack(BattleState state, GameContent content, BattleUnit unit, Reach reach, Distances toward, IReadOnlySet<Coord> captains)
    {
        if (Exposure.Of(state, content, unit, unit.At).NoCrit == 0)
        {
            return null;
        }

        Coord? destination = null;
        (int, int, Coord) bestKey = default;
        foreach (var tile in reach.Destinations)
        {
            if (tile == unit.At || !MayEndOn(state, content, unit, tile) || captains.Contains(tile) || Exposure.Of(state, content, unit, tile).NoCrit > 0)
            {
                continue;
            }

            var key = (toward.From(tile) ?? int.MaxValue, reach.CostTo(tile)!.Value, tile);
            if (destination is null || key.CompareTo(bestKey) < 0)
            {
                bestKey = key;
                destination = tile;
            }
        }

        return destination;
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
    /// section 8's tile is one of them it takes the veto's key as for a lethal one. While a beaten boss runs with the
    /// shard (issue 1386), every unit, armed or not, walks toward the tiles beside him instead, by the same key, and while
    /// the shard lies under the hill (slice 3b), toward its tile and the tiles beside it.
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
        var runners = state.UnitsOf(Side.Enemy).Where(ShardRun.Running).ToList();
        if (runners.Count > 0)
        {
            // A beaten boss runs with the shard (issue 1386): the way is to a tile beside him, armed or not.
            var beside = runners.SelectMany(r => r.At.Neighbors()).Where(t => state.Map.Contains(t) && (t == unit.At || state.UnitAt(t) is null)).Distinct().ToList();
            toward = Movement.DistancesTo(state.Map, content, beside, movement, OccupantAt, footing);
            if (toward.From(unit.At) is null)
            {
                toward = null;
            }
        }
        else if (KinShard.Of(state) is { Lies: { } lies })
        {
            // The shard lies under the hill (issue 1386 slice 3b): the way is to its tile or a tile beside it, armed or not.
            var beside = lies.Neighbors().Append(lies).Where(t => state.Map.Contains(t) && (t == unit.At || state.UnitAt(t) is null)).Distinct().ToList();
            toward = Movement.DistancesTo(state.Map, content, beside, movement, OccupantAt, footing);
            if (toward.From(unit.At) is null)
            {
                toward = null;
            }
        }

        if (toward is null)
        {
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
