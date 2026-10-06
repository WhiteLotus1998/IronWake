using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// The focused chair (issue 1157, round 389): <see cref="HeuristicPlayer"/> with one change, in who takes
/// a kill. When the heuristic's next plan is a plain attack (no art) by another unit that kills its target
/// on a hit, the kill goes to one named non-captain, <see cref="Fed"/>, if he can take it from a tile the
/// heuristic itself would accept (<see cref="EvenPlayer.KillFrom"/>). The <see cref="Guard.Guarded"/> line
/// keeps <see cref="EvenPlayer"/>'s guard: no lower kill chance and no more exposure than the planned
/// attacker. The <see cref="Guard.Paying"/> line lets the fed unit pay for the door: a kill chance at most
/// <see cref="PayingGap"/> below the planned attacker's, a tile in reach of at most <see cref="PayingReach"/>
/// more enemies, and a no-crit forecast death refused for him always. The <see cref="Guard.Striking"/> line
/// (issue 1167, round 392) plays the paying line, and where it hands no kill and the heuristic plans a plain
/// attack by another unit, kill or not, the fed unit strikes that target first from a tile the paying guard
/// allows (<see cref="StrikeFrom"/>); the planned attacker is re-planned on the next decision against the
/// chipped target. With no such tile it plays the heuristic's plan. A measurement only; the bar's read
/// (round 389) is what it is for.
/// </summary>
public sealed class FocusedPlayer : IPlayer
{
    /// <summary>
    /// The fed unit: the earliest main joiner who is not a healer. The whole levy is in the roster from
    /// map 1 and Maud, the healer, arrives on map 2; in cast order Wren comes first, but her issued sword
    /// has no door (round 389), so the next is Teodor, the unit the Table named.
    /// </summary>
    public const string Fed = "teodor";

    /// <summary>How much lower the fed unit's kill chance may be than the planned attacker's on the paying line (round 389: 15 points).</summary>
    public const double PayingGap = 0.15;

    /// <summary>How many more enemies may reach the fed unit's tile than the planned attacker's on the paying line (round 389: one).</summary>
    public const int PayingReach = 1;

    /// <summary>The two guards of round 389, read from the same seeds.</summary>
    public enum Guard
    {
        /// <summary>The even chair's guard: no lower kill chance, no more exposure.</summary>
        Guarded,

        /// <summary>The paying guard: <see cref="PayingGap"/>, <see cref="PayingReach"/>, and the forecast death refused.</summary>
        Paying,

        /// <summary>The strike-then-finish chair (round 392): the paying line, and a strike on any planned target the paying guard allows, kill or not.</summary>
        Striking,
    }

    /// <summary>Why a kill offered to the fed unit stayed with the planned attacker, the first clause that refused it.</summary>
    public enum Refusal
    {
        /// <summary>No tile he may end on puts the target in range, seen and killed on a hit, at any kill chance.</summary>
        NoTile,

        /// <summary>Such a tile exists, but none at the guard's kill chance.</summary>
        Chance,

        /// <summary>Such a tile exists, but every one is in reach of more enemies than the guard allows.</summary>
        Exposure,

        /// <summary>Such a tile exists within the exposure, but every one forecasts his death, no crit landing.</summary>
        ForecastDeath,
    }

    private readonly HeuristicPlayer _inner = new();
    private readonly Dictionary<Refusal, int> _refused = [];
    private readonly Guard _guard;
    private (string Target, bool CaptainPlanned)? _pending;

    public FocusedPlayer(Guard guard) => _guard = guard;

    /// <summary>How many kills this player handed to <see cref="Fed"/>.</summary>
    public int Handed { get; private set; }

    /// <summary>How many strikes that do not kill on a hit this player handed to <see cref="Fed"/> (the striking line only).</summary>
    public int Strikes { get; private set; }

    /// <summary>How many attacks <see cref="Fed"/> made under this player, whoever planned them: the combats his rank is paid for.</summary>
    public int Combats { get; private set; }

    /// <summary>
    /// The kills given up: handed attacks whose target still stood at this player's next decision, where
    /// the captain was the planned attacker. A target that dies some other way before that decision (a
    /// counter in the enemy phase between them) is not counted, a small undercount.
    /// </summary>
    public int GivenUp { get; private set; }

    /// <summary>
    /// The kills offered to the fed unit (a killing plain attack planned for another unit while he could still
    /// act) that stayed with the planned attacker, by the first clause that refused each (<see cref="Classify"/>).
    /// </summary>
    public IReadOnlyDictionary<Refusal, int> Refused => _refused;

    public IReadOnlyList<Command> Next(BattleState state, GameContent content)
    {
        if (_pending is { } pending)
        {
            if (pending.CaptainPlanned && state.Find(pending.Target) is { Hp: > 0 })
            {
                GivenUp++;
            }

            _pending = null;
        }

        var plan = _inner.Next(state, content);
        var (handed, refusal) = Decide(state, content, plan, _guard);
        if (!ReferenceEquals(handed, plan) && plan[^1] is Attack attack)
        {
            Handed++;
            _pending = (attack.TargetId, state.Find(attack.UnitId) is { } planned && CampaignRecord.IsCaptain(planned.Unit, content));
        }
        else
        {
            if (refusal is { } why)
            {
                _refused[why] = _refused.GetValueOrDefault(why) + 1;
            }

            if (_guard == Guard.Striking && Strike(state, content, plan) is { } strike)
            {
                Strikes++;
                handed = strike;
            }
        }

        if (handed.Count > 0 && handed[^1] is Attack { UnitId: Fed })
        {
            Combats++;
        }

        return handed;
    }

    /// <summary>
    /// <paramref name="plan"/>, the heuristic's commands for one unit, with its kill handed to
    /// <see cref="Fed"/> under <paramref name="guard"/> (the class summary's rule), or <paramref name="plan"/>
    /// itself when it is no killing plain attack, is the fed unit's own, or he cannot take it.
    /// </summary>
    public static IReadOnlyList<Command> Hand(BattleState state, GameContent content, IReadOnlyList<Command> plan, Guard guard) =>
        Decide(state, content, plan, guard).Plan;

    /// <summary><see cref="Hand"/>, and when a kill was offered to the fed unit and stayed with the planned attacker, why.</summary>
    private static (IReadOnlyList<Command> Plan, Refusal? Refused) Decide(BattleState state, GameContent content, IReadOnlyList<Command> plan, Guard guard)
    {
        var (move, attack) = plan switch
        {
            [Move m, Attack a] => (m, a),
            [Attack a] => ((Move?)null, a),
            _ => ((Move?)null, (Attack?)null),
        };
        if (attack is null || attack.Art is not null || attack.UnitId == Fed || (move is not null && move.UnitId != attack.UnitId))
        {
            return (plan, null);
        }

        var planned = state.Find(attack.UnitId);
        var target = state.Find(attack.TargetId);
        var fed = state.Find(Fed);
        if (planned is null || target is null || fed is null || fed.Side != Side.Player || fed.Acted || state.CantoReachOf(fed, content) is not null)
        {
            return (plan, null);
        }

        var tile = move?.To ?? planned.At;
        var slot = attack.Slot ?? planned.EquippedSlot(content);
        var arm = HeuristicPlayer.Arms(content, planned).FirstOrDefault(x => x.Slot == slot);
        if (arm.Weapon is null || !HeuristicPlayer.KillsOnHit(state, content, arm.Armed, tile, target))
        {
            return (plan, null);
        }

        var enemyReach = state.UnitsOf(Side.Enemy).Select(e => state.ReachOf(e, content)).ToList();
        var floor = HeuristicPlayer.KillProbability(state, content, arm.Armed, tile, target);
        var exposure = enemyReach.Count(r => r.CanEnd(tile));
        var (chance, reach, refuseDeath) = guard != Guard.Guarded
            ? (floor - PayingGap - Tolerance, exposure + PayingReach, true)
            : (floor, exposure, false);
        return EvenPlayer.KillFrom(state, content, fed, target, chance, reach, enemyReach, refuseDeath) is { } kill
            ? (kill, null)
            : (plan, Classify(state, content, fed, target, chance, reach, enemyReach, refuseDeath));
    }

    /// <summary>
    /// The first clause that refuses <paramref name="fed"/> the kill on <paramref name="target"/>, read by
    /// adding the guard's clauses one at a time: no kill chance floor, no exposure ceiling and no death refusal;
    /// then the floor; then the ceiling; then all three (the even chair's veto on a unit whose death loses the
    /// map always stands).
    /// </summary>
    public static Refusal Classify(BattleState state, GameContent content, BattleUnit fed, BattleUnit target, double chance, int reach, IReadOnlyList<Reach> enemyReach, bool refuseDeath) =>
        EvenPlayer.KillFrom(state, content, fed, target, 0, int.MaxValue, enemyReach) is null ? Refusal.NoTile
        : EvenPlayer.KillFrom(state, content, fed, target, chance, int.MaxValue, enemyReach) is null ? Refusal.Chance
        : EvenPlayer.KillFrom(state, content, fed, target, chance, reach, enemyReach) is null ? Refusal.Exposure
        : Refusal.ForecastDeath;

    /// <summary>
    /// The paying guard's kill for <paramref name="fed"/> on <paramref name="target"/>, or null: a tile the
    /// heuristic accepts with a kill chance at most <see cref="PayingGap"/> below <paramref name="plannedChance"/>,
    /// in reach of at most <see cref="PayingReach"/> more enemies than <paramref name="plannedExposure"/>, the
    /// target killed on a hit, and no no-crit forecast death on the tile. The gap is read in whole points,
    /// so a chance exactly 15 points lower passes whatever the floating-point sum rounds to.
    /// </summary>
    public static IReadOnlyList<Command>? Paying(BattleState state, GameContent content, BattleUnit fed, BattleUnit target, double plannedChance, int plannedExposure, IReadOnlyList<Reach> enemyReach) =>
        EvenPlayer.KillFrom(state, content, fed, target, plannedChance - PayingGap - Tolerance, plannedExposure + PayingReach, enemyReach, refuseDeath: true);

    /// <summary>
    /// The striking line's strike (issue 1167): when <paramref name="plan"/> is a plain attack by another unit,
    /// the fed unit's strike on the same target from <see cref="StrikeFrom"/>, read against the planned tile's
    /// exposure; else null.
    /// </summary>
    public static IReadOnlyList<Command>? Strike(BattleState state, GameContent content, IReadOnlyList<Command> plan)
    {
        var (move, attack) = plan switch
        {
            [Move m, Attack a] => (m, a),
            [Attack a] => ((Move?)null, a),
            _ => ((Move?)null, (Attack?)null),
        };
        if (attack is null || attack.Art is not null || attack.UnitId == Fed || (move is not null && move.UnitId != attack.UnitId))
        {
            return null;
        }

        var planned = state.Find(attack.UnitId);
        var target = state.Find(attack.TargetId);
        var fed = state.Find(Fed);
        if (planned is null || target is null || fed is null || fed.Side != Side.Player || fed.Acted || state.CantoReachOf(fed, content) is not null)
        {
            return null;
        }

        var enemyReach = state.UnitsOf(Side.Enemy).Select(e => state.ReachOf(e, content)).ToList();
        var tile = move?.To ?? planned.At;
        return StrikeFrom(state, content, fed, target, enemyReach.Count(r => r.CanEnd(tile)) + PayingReach, enemyReach);
    }

    /// <summary>
    /// <paramref name="fed"/>'s commands for a strike on <paramref name="target"/>, kill or not, from the
    /// best-scoring tile the paying guard allows, or null: a tile he may end on (only his own once he has
    /// moved), a weapon in range, the target seen, the ledger not refusing, his strike landing at all (the
    /// forecast says he strikes, at a hit chance above 0), no no-crit forecast death, and at most
    /// <paramref name="exposure"/> enemies able to reach the tile. No kill chance is asked (round 392).
    /// </summary>
    public static IReadOnlyList<Command>? StrikeFrom(BattleState state, GameContent content, BattleUnit fed, BattleUnit target, int exposure, IReadOnlyList<Reach> enemyReach)
    {
        var tiles = fed.Moved
            ? new List<Coord> { fed.At }
            : state.ReachOf(fed, content).Destinations.Where(t => HeuristicPlayer.MayEndOn(state, content, fed, t)).ToList();
        var equipped = fed.EquippedSlot(content);
        (Coord Tile, int Slot, double Score)? best = null;
        foreach (var tile in tiles)
        {
            if (enemyReach.Count(r => r.CanEnd(tile)) > exposure)
            {
                continue;
            }

            foreach (var (slot, weapon, armed) in HeuristicPlayer.Arms(content, fed))
            {
                if (!weapon.InRange(tile.DistanceTo(target.At))
                    || !Dusk.Sees(state, fed.Side, target.At, fed.Id, tile)
                    || HeuristicPlayer.LedgerRefuses(state, content, armed, tile, target)
                    || !Lands(state, content, armed, tile, target)
                    || Exposure.Of(state, content, fed, tile, target, slot).NoCrit >= fed.Hp)
                {
                    continue;
                }

                var score = EnemyAi.Score(state, content, armed, tile, target);
                if (best is null || score > best.Value.Score)
                {
                    best = (tile, slot, score);
                }
            }
        }

        return best is { } b ? HeuristicPlayer.WithMove(fed, b.Tile, new Attack(fed.Id, target.Id, b.Slot == equipped ? null : b.Slot)) : null;
    }

    /// <summary>Whether <paramref name="armed"/>'s forecast from <paramref name="tile"/> strikes <paramref name="target"/> at a hit chance above 0.</summary>
    private static bool Lands(BattleState state, GameContent content, BattleUnit armed, Coord tile, BattleUnit target)
    {
        var me = (armed with { At = tile }).ToCombatant(state, content, against: target);
        var them = target.Answering(state, content, tile, armed);
        var side = Combat.Forecast(me, them, tile.DistanceTo(target.At), state.Scheme).Attacker;
        return side.Strikes && side.HitChance > 0;
    }

    /// <summary>Far under any one point of kill chance, so the gap's edge is not decided by rounding.</summary>
    private const double Tolerance = 1e-9;
}
