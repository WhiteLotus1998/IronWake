using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// The even-company chair (issue 1150, rounds 385 and 387): <see cref="HeuristicPlayer"/> with one
/// change, in who takes a kill. When the heuristic's next plan is a plain attack (no art) that kills
/// its target on a hit, every other unacted player unit not owed a Canto is checked for the same kill
/// on the same target from a tile the heuristic itself would accept (<see cref="KillFrom"/>). The
/// kill goes to the lowest level among the planned attacker and those that qualify, then the fewest
/// rank points in the unit's main weapon (<see cref="LevelRun.MainType"/>), ties to the planned
/// attacker. A unit qualifies only if its chance to kill is no lower than the planned attacker's and
/// its tile is in reach of no more enemies than the planned attacker's tile, so the chair changes who
/// takes a kill, never whether it happens or how exposed the striker stands. With no qualifier it plays
/// the heuristic's plan. A measurement only; the bar's read (round 387) is what it is for.
/// </summary>
public sealed class EvenPlayer : IPlayer
{
    private readonly HeuristicPlayer _inner = new();

    /// <summary>How many kills this player gave to a unit other than the one the heuristic planned.</summary>
    public int Handed { get; private set; }

    public IReadOnlyList<Command> Next(BattleState state, GameContent content)
    {
        var plan = _inner.Next(state, content);
        var handed = Hand(state, content, plan);
        if (!ReferenceEquals(handed, plan))
        {
            Handed++;
        }

        return handed;
    }

    /// <summary>
    /// <paramref name="plan"/>, the heuristic's commands for one unit, with its kill handed to the
    /// lowest unit that qualifies (the class summary's rule), or <paramref name="plan"/> itself when
    /// it is no killing plain attack or no other unit qualifies.
    /// </summary>
    public static IReadOnlyList<Command> Hand(BattleState state, GameContent content, IReadOnlyList<Command> plan)
    {
        var (move, attack) = plan switch
        {
            [Move m, Attack a] => (m, a),
            [Attack a] => ((Move?)null, a),
            _ => ((Move?)null, (Attack?)null),
        };
        if (attack is null || attack.Art is not null || (move is not null && move.UnitId != attack.UnitId))
        {
            return plan;
        }

        var planned = state.Find(attack.UnitId);
        var target = state.Find(attack.TargetId);
        if (planned is null || target is null)
        {
            return plan;
        }

        var tile = move?.To ?? planned.At;
        var slot = attack.Slot ?? planned.EquippedSlot(content);
        var arm = HeuristicPlayer.Arms(content, planned).FirstOrDefault(x => x.Slot == slot);
        if (arm.Weapon is null || !HeuristicPlayer.KillsOnHit(state, content, arm.Armed, tile, target))
        {
            return plan;
        }

        var enemyReach = state.UnitsOf(Side.Enemy).Select(e => state.ReachOf(e, content)).ToList();
        var floor = HeuristicPlayer.KillProbability(state, content, arm.Armed, tile, target);
        var exposure = enemyReach.Count(r => r.CanEnd(tile));
        var best = (Unit: planned, Plan: plan);
        foreach (var other in state.UnitsOf(Side.Player))
        {
            if (other.Id == planned.Id || other.Acted || state.CantoReachOf(other, content) is not null || !Lower(other, best.Unit, content))
            {
                continue;
            }

            if (KillFrom(state, content, other, target, floor, exposure, enemyReach) is { } kill)
            {
                best = (other, kill);
            }
        }

        return best.Plan;
    }

    /// <summary>Whether <paramref name="a"/> takes a kill before <paramref name="b"/>: a lower level, else fewer rank points in its main weapon.</summary>
    public static bool Lower(BattleUnit a, BattleUnit b, GameContent content) =>
        a.Unit.Level != b.Unit.Level
            ? a.Unit.Level < b.Unit.Level
            : a.Unit.Skill.Points(LevelRun.MainType(a.Unit, content)) < b.Unit.Skill.Points(LevelRun.MainType(b.Unit, content));

    /// <summary>
    /// <paramref name="unit"/>'s commands for the kill on <paramref name="target"/> from the best-scoring
    /// tile the heuristic would accept, or null when none qualifies: a tile in reach the unit may end on
    /// (only its own tile once it has moved), a weapon in range, the target seen, the ledger not refusing,
    /// the veto passed where the unit's death loses the map, the target killed on a hit, a kill chance of
    /// at least <paramref name="floor"/>, and at most <paramref name="exposure"/> enemies able to reach the tile.
    /// </summary>
    public static IReadOnlyList<Command>? KillFrom(BattleState state, GameContent content, BattleUnit unit, BattleUnit target, double floor, int exposure, IReadOnlyList<Reach> enemyReach)
    {
        var tiles = unit.Moved
            ? new List<Coord> { unit.At }
            : state.ReachOf(unit, content).Destinations.Where(t => HeuristicPlayer.MayEndOn(state, content, unit, t)).ToList();
        var vetoed = HeuristicPlayer.LosesTheMap(state, unit);
        var equipped = unit.EquippedSlot(content);
        (Coord Tile, int Slot, double Score)? best = null;
        foreach (var tile in tiles)
        {
            if (enemyReach.Count(r => r.CanEnd(tile)) > exposure)
            {
                continue;
            }

            foreach (var (slot, weapon, armed) in HeuristicPlayer.Arms(content, unit))
            {
                if (!weapon.InRange(tile.DistanceTo(target.At))
                    || !Dusk.Sees(state, unit.Side, target.At, unit.Id, tile)
                    || HeuristicPlayer.LedgerRefuses(state, content, armed, tile, target)
                    || (vetoed && Exposure.Of(state, content, unit, tile, target, slot).NoCrit >= unit.Hp)
                    || !HeuristicPlayer.KillsOnHit(state, content, armed, tile, target)
                    || HeuristicPlayer.KillProbability(state, content, armed, tile, target) < floor)
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

        return best is { } b ? HeuristicPlayer.WithMove(unit, b.Tile, new Attack(unit.Id, target.Id, b.Slot == equipped ? null : b.Slot)) : null;
    }
}
