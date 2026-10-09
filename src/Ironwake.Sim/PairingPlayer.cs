using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// The supports' ceiling (issue 77, slice 5): <see cref="HeuristicPlayer"/> committed to one named
/// support pair. It plans every unit as the heuristic does, with two changes for the pair. When the
/// heuristic would move one member without a strike while the other, not yet acted, has one, the
/// striker goes first, so the idle one can follow it. When a member would end its turn not beside its
/// partner, it ends beside the partner instead where that tile is no worse: for an attack, a tile
/// beside the partner from which the same weapon strikes the same target at a score no lower than the
/// heuristic's tile; for a plain Wait, the tile beside the partner nearest the heuristic's
/// destination. A unit whose death loses the map keeps the veto on every tile it ends on: the
/// no-crit sum of what could strike it there over the cycle stays under its HP. A heal, an exit, a Move Again, a Wait on
/// an exit tile and a unit that has already moved are left as the heuristic planned them. Rapport
/// accrues only on a threatened phase (0042), so this reads how fast a player who keeps the pair
/// together climbs, not a strategy. A measurement only.
/// </summary>
public sealed class PairingPlayer : IPlayer
{
    private readonly HeuristicPlayer _inner = new();
    private readonly string _a;
    private readonly string _b;

    public PairingPlayer(string a, string b)
    {
        _a = a;
        _b = b;
    }

    public IReadOnlyList<Command> Next(BattleState state, GameContent content)
    {
        var plan = _inner.Next(state, content);
        if (UnitOf(plan) is { } id && (id == _a || id == _b) && plan[^1] is not Attack
            && state.Find(id == _a ? _b : _a) is { Side: Side.Player, Acted: false } partner
            && state.MoveAgainReachOf(partner, content) is null)
        {
            var partnerPlan = HeuristicPlayer.PlanUnit(state, content, partner);
            if (partnerPlan.Count > 0 && partnerPlan[^1] is Attack)
            {
                plan = partnerPlan;
            }
        }

        return Adjust(state, content, plan, _a, _b);
    }

    private static string? UnitOf(IReadOnlyList<Command> plan) => plan.Count == 0 ? null : plan[^1] switch
    {
        Attack attack => attack.UnitId,
        Wait wait => wait.UnitId,
        _ => null,
    };

    /// <summary>
    /// <paramref name="plan"/>, one unit's commands from the heuristic, with its end tile moved beside
    /// the partner when the unit is a member of the pair <paramref name="a"/> and <paramref name="b"/>
    /// and a tile no worse exists (the class summary's rule); otherwise <paramref name="plan"/> unchanged.
    /// </summary>
    public static IReadOnlyList<Command> Adjust(BattleState state, GameContent content, IReadOnlyList<Command> plan, string a, string b)
    {
        var (move, action) = plan switch
        {
            [Move m, var act] => (m, act),
            [var act] => ((Move?)null, act),
            _ => ((Move?)null, (Command?)null),
        };
        var unitId = action switch
        {
            Attack attack => attack.UnitId,
            Wait wait => wait.UnitId,
            _ => null,
        };
        if (unitId is null || (unitId != a && unitId != b) || (move is not null && move.UnitId != unitId))
        {
            return plan;
        }

        var unit = state.Find(unitId);
        var partner = state.Find(unitId == a ? b : a);
        if (unit is null || partner is null || unit.Side != Side.Player || partner.Side != Side.Player || unit.Moved)
        {
            return plan;
        }

        var end = move?.To ?? unit.At;
        if (end.DistanceTo(partner.At) == 1 || state.Map.Exits.Contains(end))
        {
            return plan;
        }

        var reach = state.ReachOf(unit, content);
        var beside = reach.Destinations
            .Where(t => t.DistanceTo(partner.At) == 1 && HeuristicPlayer.MayEndOn(state, content, unit, t))
            .ToList();
        if (beside.Count == 0)
        {
            return plan;
        }

        var vetoed = HeuristicPlayer.LosesTheMap(state, unit);
        Coord? chosen = action switch
        {
            Attack attack => AttackTile(state, content, unit, attack, end, beside, vetoed),
            Wait => WaitTile(state, content, unit, reach, end, beside, vetoed),
            _ => null,
        };
        return chosen is { } tile ? HeuristicPlayer.WithMove(unit, tile, action!) : plan;
    }

    private static Coord? AttackTile(BattleState state, GameContent content, BattleUnit unit, Attack attack, Coord end, IReadOnlyList<Coord> beside, bool vetoed)
    {
        var target = state.Find(attack.TargetId);
        var slot = attack.Slot ?? unit.EquippedSlot(content);
        var arm = HeuristicPlayer.Arms(content, unit).FirstOrDefault(x => x.Slot == slot);
        if (target is null || arm.Weapon is null || attack.Art is not null)
        {
            return null;
        }

        var floor = EnemyAi.Score(state, content, arm.Armed, end, target);
        Coord? best = null;
        var bestScore = double.NegativeInfinity;
        foreach (var tile in beside)
        {
            if (!arm.Weapon.InRange(tile.DistanceTo(target.At))
                || !Dusk.Sees(state, unit.Side, target.At, unit.Id, tile)
                || HeuristicPlayer.LedgerRefuses(state, content, arm.Armed, tile, target)
                || (vetoed && Exposure.Of(state, content, unit, tile, target, slot).NoCrit >= unit.Hp))
            {
                continue;
            }

            var score = EnemyAi.Score(state, content, arm.Armed, tile, target);
            if (score >= floor && score > bestScore)
            {
                best = tile;
                bestScore = score;
            }
        }

        return best;
    }

    private static Coord? WaitTile(BattleState state, GameContent content, BattleUnit unit, Reach reach, Coord end, IReadOnlyList<Coord> beside, bool vetoed) =>
        beside
            .Where(t => !vetoed || Exposure.Of(state, content, unit, t).NoCrit < unit.Hp)
            .OrderBy(t => t.DistanceTo(end))
            .ThenBy(t => reach.CostTo(t))
            .ThenBy(t => t.Y)
            .ThenBy(t => t.X)
            .Cast<Coord?>()
            .FirstOrDefault();

    /// <summary>
    /// <paramref name="record"/> with the bench set so both of <paramref name="a"/> and
    /// <paramref name="b"/> deploy on <paramref name="map"/> where the roster holds them: while one is
    /// left out, the last unit in the deployment that is neither of them is benched, a refused bench
    /// (the captain, a protected or a named recruit) passing to the next one back. A pair member not on
    /// the roster yet stays out, and so does one no bench can make room for.
    /// </summary>
    public static CampaignRecord Deploy(CampaignRecord record, MapDefinition map, GameContent content, string a, string b)
    {
        var present = record.Present(content).Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        var wanted = new[] { a, b }.Where(present.Contains).ToList();
        for (var guard = 0; guard < present.Count; guard++)
        {
            var deployed = record.Deployment(map, content);
            if (wanted.All(deployed.Contains))
            {
                return record;
            }

            var benched = false;
            foreach (var id in deployed.Reverse().Where(id => !wanted.Contains(id)))
            {
                var result = record.Bench(id, map, content);
                if (result.Accepted)
                {
                    record = result.Record;
                    benched = true;
                    break;
                }
            }

            if (!benched)
            {
                return record;
            }
        }

        return record;
    }
}
