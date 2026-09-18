namespace Ironwake.Core;

/// <summary>
/// The only way a <see cref="BattleState"/> changes (DESIGN.md section 2): apply one
/// command, get the next state and the events that explain it. An illegal command is
/// answered with a <see cref="Rejection"/> naming the rule, never with an exception.
/// Every accepted command except <see cref="Recall"/> pushes the state it left onto the
/// history; a Recall restores a prior state, truncates the history to before it, and
/// spends a charge (section 7). Rolls come from a <see cref="KeyedRng"/> over the state's
/// seed under the section 5 keys, so the same attack on the same turn draws the same
/// numbers whatever was resolved before it or rewound since.
/// </summary>
public static class Resolver
{
    public static ApplyResult Apply(BattleState state, GameContent content, Command command)
    {
        var events = new List<GameEvent>();
        BattleState next;
        Rejection? rejection;
        if (command is not Recall && state.Outcome is { IsOver: true } outcome)
        {
            var verdict = outcome.Result == BattleResult.Won ? "won" : "lost";
            return new ApplyResult(state, ValueList<GameEvent>.Empty, new Rejection(
                RejectionReason.BattleOver, $"the battle is {verdict} ({outcome.Reason}); only Recall is left"));
        }

        switch (command)
        {
            case Move move:
                (next, rejection) = ApplyMove(state, content, move, events);
                break;
            case Attack attack:
                (next, rejection) = ApplyAttack(state, content, attack, events);
                break;
            case UseItem item:
                var actor = Acting(state, item.UnitId, out rejection);
                next = state;
                rejection ??= new Rejection(RejectionReason.NotAvailable, $"{actor!.Id} cannot use an item: items are not in this build (issue 9)");
                break;
            case Wait wait:
                (next, rejection) = ApplyWait(state, wait, events);
                break;
            case EndPhase:
                (next, rejection) = ApplyEndPhase(state, content, events);
                break;
            case Recall recall:
                return ApplyRecall(state, recall);
            default:
                next = state;
                rejection = new Rejection(RejectionReason.UnknownCommand, $"unknown command {command.GetType().Name}");
                break;
        }

        if (rejection is not null)
        {
            return new ApplyResult(state, ValueList<GameEvent>.Empty, rejection);
        }

        next = WakeGroups(state, next, content, events);
        next = next with { History = state.History.Add(state with { History = ValueList<BattleState>.Empty }) };
        return new ApplyResult(next, ValueList<GameEvent>.From(events), null);
    }

    /// <summary>
    /// The Guard wake check of DESIGN.md section 8, run after every accepted command on
    /// where units stand afterwards. A sleeping group wakes on the death of a member
    /// (any distance), on noise (a combat this command whose attacker or target stood
    /// within the noise radius of a living member), or on proximity (a player unit within
    /// the wake radius of a living member). One <see cref="GroupWoke"/> per group per
    /// command, naming the loudest cause in that order. Distances are Manhattan and walls
    /// are not considered.
    /// </summary>
    private static BattleState WakeGroups(BattleState before, BattleState after, GameContent content, List<GameEvent> events)
    {
        var sleeping = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var unit in before.Units)
        {
            if (unit is { Behavior: Behavior.Guard, Group: { } group } && !before.IsAwake(group))
            {
                sleeping.Add(group);
            }
        }

        if (sleeping.Count == 0)
        {
            return after;
        }

        var died = new List<BattleUnit>();
        var noisy = new List<Coord>();
        foreach (var e in events)
        {
            switch (e)
            {
                case UnitDied dead when before.Find(dead.UnitId) is { } unit:
                    died.Add(unit);
                    break;
                case CombatFought fought:
                    noisy.Add(before.Find(fought.AttackerId)!.At);
                    noisy.Add(before.Find(fought.TargetId)!.At);
                    break;
            }
        }

        var next = after;
        foreach (var group in sleeping)
        {
            var members = after.Units.Where(u => u.Group == group).Select(u => u.At).ToList();
            WakeCause? cause = null;
            if (died.Any(u => u.Group == group))
            {
                cause = WakeCause.Death;
            }
            else if (noisy.Any(tile => members.Any(m => m.DistanceTo(tile) <= content.NoiseRadius)))
            {
                cause = WakeCause.Noise;
            }
            else if (after.UnitsOf(Side.Player).Any(p => members.Any(m => m.DistanceTo(p.At) <= content.WakeRadius)))
            {
                cause = WakeCause.Proximity;
            }

            if (cause is { } woke)
            {
                events.Add(new GroupWoke(group, woke));
                next = next.Wake(group);
            }
        }

        return next;
    }

    /// <summary>The unit a command names, if it is on the board, on the acting side, and has not acted.</summary>
    private static BattleUnit? Acting(BattleState state, string unitId, out Rejection? rejection)
    {
        var unit = state.Find(unitId);
        if (unit is null)
        {
            rejection = new Rejection(RejectionReason.NoSuchUnit, $"no living unit '{unitId}'");
            return null;
        }

        if (unit.Side != state.Phase)
        {
            rejection = new Rejection(RejectionReason.NotThisSide, $"{unit.Id} is a {unit.Side} unit and it is the {state.Phase} phase");
            return null;
        }

        if (unit.Acted)
        {
            rejection = new Rejection(RejectionReason.AlreadyActed, $"{unit.Id} has already acted this phase");
            return null;
        }

        rejection = null;
        return unit;
    }

    private static (BattleState, Rejection?) ApplyMove(BattleState state, GameContent content, Move move, List<GameEvent> events)
    {
        var unit = Acting(state, move.UnitId, out var rejection);
        if (unit is null)
        {
            return (state, rejection);
        }

        if (unit.Moved)
        {
            return (state, new Rejection(RejectionReason.AlreadyMoved, $"{unit.Id} has already moved this phase"));
        }

        var reach = state.ReachOf(unit, content);
        var entry = reach.EntryAt(move.To);
        if (entry is not { CanEnd: true })
        {
            var why = !state.Map.Contains(move.To) ? "outside the map"
                : entry is null ? $"not within {reach.Mov} movement from {unit.At}"
                : "occupied by an ally";
            return (state, new Rejection(RejectionReason.OutOfReach, $"{unit.Id} cannot move to {move.To}: {why}"));
        }

        events.Add(new UnitMoved(unit.Id, unit.At, move.To, entry.Path));
        return (state.WithUnit(unit with { At = move.To, Moved = true }), null);
    }

    private static (BattleState, Rejection?) ApplyAttack(BattleState state, GameContent content, Attack attack, List<GameEvent> events)
    {
        var unit = Acting(state, attack.UnitId, out var rejection);
        if (unit is null)
        {
            return (state, rejection);
        }

        var target = state.Find(attack.TargetId);
        if (target is null)
        {
            return (state, new Rejection(RejectionReason.NoSuchTarget, $"no living unit '{attack.TargetId}' to attack"));
        }

        if (target.Side == unit.Side)
        {
            return (state, new Rejection(RejectionReason.NotAnEnemy, $"{target.Id} is on {unit.Id}'s own side"));
        }

        var weapon = unit.EquippedWeapon(content);
        if (weapon is null)
        {
            return (state, new Rejection(RejectionReason.NoWeapon, $"{unit.Id} has no weapon to attack with"));
        }

        var distance = unit.At.DistanceTo(target.At);
        if (!weapon.InRange(distance))
        {
            return (state, new Rejection(
                RejectionReason.OutOfRange,
                $"{target.Id} at {target.At} is {distance} tiles from {unit.Id} at {unit.At}; {weapon.Name} reaches {weapon.MinRange}-{weapon.MaxRange}"));
        }

        var result = CombatResolver.Resolve(
            unit.ToCombatant(state.Map, content),
            target.ToCombatant(state.Map, content),
            distance,
            new CombatContext(state.Turn, state.Phase),
            new KeyedRng(state.Seed),
            state.Scheme);
        events.Add(new CombatFought(unit.Id, target.Id, state.Turn, state.Phase, result.Strikes, result.AttackerHp, result.DefenderHp));

        var attackerAfter = unit with { Hp = result.AttackerHp, Moved = true, Acted = true };
        var targetAfter = target with { Hp = result.DefenderHp };
        attackerAfter = AwardExp(attackerAfter, targetAfter, result.Strikes, result.DefenderDied, content, state.Seed, events);
        targetAfter = AwardExp(targetAfter, attackerAfter, result.Strikes, result.AttackerDied, content, state.Seed, events);
        var next = state.WithUnit(attackerAfter);
        next = next.WithUnit(targetAfter);
        if (result.DefenderDied)
        {
            events.Add(new UnitDied(target.Id, target.Side, target.At));
            next = next.WithoutUnit(target.Id);
        }

        if (result.AttackerDied)
        {
            events.Add(new UnitDied(unit.Id, unit.Side, unit.At));
            next = next.WithoutUnit(unit.Id);
        }

        return (next, null);
    }

    /// <summary>
    /// Section 6 for one side of a combat: a living player unit earns EXP once, from its
    /// best outcome (a strike landed, the enemy killed, a boss killed), and levels up for
    /// every 100 crossed under the section 3 growth keys. Enemies earn nothing: they are
    /// templates that do not outlive the map (DECISIONS/0017). An HP gain raises current HP
    /// by the same amount. Events follow the combat and precede any death.
    /// </summary>
    private static BattleUnit AwardExp(
        BattleUnit earner, BattleUnit other, ValueList<StrikeEvent> strikes, bool killed, GameContent content, ulong seed, List<GameEvent> events)
    {
        if (earner.Side != Side.Player || earner.Hp == 0)
        {
            return earner;
        }

        var landed = strikes.Any(s => s.AttackerId == earner.Id && s.Hit);
        var amount = Experience.ForCombat(earner.Unit.Level, other.Unit.Level, landed, killed, other.IsBoss);
        if (amount == 0 || earner.Unit.Level >= Unit.MaxLevel)
        {
            return earner;
        }

        var result = earner.Unit.GainExp(amount, content.Class(earner.Unit.ClassId), new KeyedRng(seed));
        events.Add(new ExpGained(earner.Id, amount, result.Unit.Exp));
        var hp = earner.Hp;
        foreach (var levelUp in result.LevelUps)
        {
            events.Add(new LeveledUp(earner.Id, levelUp.NewLevel, levelUp.Gains));
            hp += levelUp.Gains.Hp;
        }

        return earner with { Unit = result.Unit, Hp = hp };
    }

    private static (BattleState, Rejection?) ApplyWait(BattleState state, Wait wait, List<GameEvent> events)
    {
        var unit = Acting(state, wait.UnitId, out var rejection);
        if (unit is null)
        {
            return (state, rejection);
        }

        events.Add(new UnitWaited(unit.Id));
        return (state.WithUnit(unit with { Moved = true, Acted = true }), null);
    }

    /// <summary>
    /// Flips the phase, increments the turn after the enemy phase, clears every flag, and
    /// heals the units of the side whose phase begins that stand on healing terrain
    /// (DESIGN.md section 4): the terrain's percent of max HP, integer floor, capped at
    /// max, reported as the amount actually gained; a unit at full HP is not reported.
    /// </summary>
    private static (BattleState, Rejection?) ApplyEndPhase(BattleState state, GameContent content, List<GameEvent> events)
    {
        var ended = state.Phase;
        var nextPhase = ended == Side.Player ? Side.Enemy : Side.Player;
        var nextTurn = ended == Side.Enemy ? state.Turn + 1 : state.Turn;
        events.Add(new PhaseEnded(ended, state.Turn));
        events.Add(new PhaseBegan(nextPhase, nextTurn));
        var units = new List<BattleUnit>(state.Units.Count);
        foreach (var unit in state.Units)
        {
            var hp = unit.Hp;
            if (unit.Side == nextPhase)
            {
                var max = unit.MaxHp(content);
                var percent = state.Map.TerrainAt(unit.At, content).HealPercent;
                hp = Math.Min(max, hp + max * percent / 100);
                if (hp > unit.Hp)
                {
                    events.Add(new UnitHealed(unit.Id, hp - unit.Hp, hp));
                }
            }

            units.Add(unit with { Hp = hp, Moved = false, Acted = false });
        }

        return (state with { Phase = nextPhase, Turn = nextTurn, Units = ValueList<BattleUnit>.From(units) }, null);
    }

    /// <summary>
    /// Every command other than Recall that <see cref="Apply"/> would accept in a state,
    /// in a fixed order: for each unacted unit of the acting side in id order, its Moves
    /// (row-major, own tile excluded), its Attacks (targets in id order), then Wait; then
    /// EndPhase. Empty once the battle is over. The random player of gates 2 and 8 draws
    /// from this list, so a command it picks is legal by construction.
    /// </summary>
    public static IEnumerable<Command> Legal(BattleState state, GameContent content)
    {
        if (state.Outcome.IsOver)
        {
            yield break;
        }

        foreach (var unit in state.UnitsOf(state.Phase))
        {
            if (unit.Acted)
            {
                continue;
            }

            if (!unit.Moved)
            {
                foreach (var to in state.ReachOf(unit, content).Destinations)
                {
                    if (to != unit.At)
                    {
                        yield return new Move(unit.Id, to);
                    }
                }
            }

            var weapon = unit.EquippedWeapon(content);
            if (weapon is not null)
            {
                foreach (var target in state.UnitsOf(state.Phase == Side.Player ? Side.Enemy : Side.Player))
                {
                    if (weapon.InRange(unit.At.DistanceTo(target.At)))
                    {
                        yield return new Attack(unit.Id, target.Id);
                    }
                }
            }

            yield return new Wait(unit.Id);
        }

        yield return new EndPhase();
    }

    private static ApplyResult ApplyRecall(BattleState state, Recall recall)
    {
        if (state.RecallCharges < 1)
        {
            return new ApplyResult(state, ValueList<GameEvent>.Empty, new Rejection(RejectionReason.NoRecallCharges, "no Recall charges left on this map"));
        }

        if (recall.ToIndex < 0 || recall.ToIndex >= state.History.Count)
        {
            return new ApplyResult(state, ValueList<GameEvent>.Empty, new Rejection(
                RejectionReason.NoSuchHistoryIndex, $"history holds {state.History.Count} states; there is no state {recall.ToIndex} to recall"));
        }

        var kept = new List<BattleState>(recall.ToIndex);
        for (var i = 0; i < recall.ToIndex; i++)
        {
            kept.Add(state.History[i]);
        }

        var charges = state.RecallCharges - 1;
        var restored = state.History[recall.ToIndex] with { History = ValueList<BattleState>.From(kept), RecallCharges = charges };
        return new ApplyResult(restored, ValueList<GameEvent>.Of(new Recalled(recall.ToIndex, charges)), null);
    }
}
