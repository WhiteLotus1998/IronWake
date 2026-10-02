namespace Ironwake.Core;

/// <summary>
/// The only way a <see cref="BattleState"/> changes (DESIGN.md section 2): apply one
/// command, get the next state and the events that explain it. An illegal command is
/// answered with a <see cref="Rejection"/> naming the rule, never with an exception.
/// Every accepted command except <see cref="Recall"/> and <see cref="Undo"/> pushes the state
/// it left onto the history; a Recall restores a prior state, truncates the history to before
/// it, and spends a charge (section 7); an Undo restores the state a move left, history and
/// all (issue 676). Rolls come from a <see cref="KeyedRng"/> over the state's seed under the
/// section 5 keys, so the same attack on the same turn draws the same numbers whatever was
/// resolved before it or rewound since.
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
                if (rejection is null)
                {
                    next = MapEvents.AfterMove(next, content, next.Find(move.UnitId)!, events);
                    next = FireWatches(next, content, move.UnitId, events);
                    if (next.Find(move.UnitId) is { } mover)
                    {
                        next = Messenger.AfterMove(next, content, mover, events);
                    }
                }

                break;
            case Attack attack:
                (next, rejection) = ApplyAttack(state, content, attack, events);
                break;
            case UseItem item:
                (next, rejection) = ApplyUseItem(state, content, item, events);
                break;
            case Wait wait:
                (next, rejection) = ApplyWait(state, content, wait, events);
                break;
            case Watch watch:
                (next, rejection) = ApplyWatch(state, content, watch, events);
                break;
            case Cover cover:
                (next, rejection) = ApplyCover(state, content, cover, events);
                break;
            case Exit exit:
                (next, rejection) = ApplyExit(state, content, exit, events);
                break;
            case Recover recover:
                (next, rejection) = ApplyRecover(state, content, recover, events);
                break;
            case Open open:
                (next, rejection) = ApplyOpen(state, content, open, events);
                break;
            case Drop drop:
                (next, rejection) = ApplyDrop(state, content, drop, events);
                break;
            case Shove shove:
                (next, rejection) = ApplyShove(state, content, shove, events);
                if (rejection is null)
                {
                    next = MapEvents.AfterMove(next, content, next.Find(shove.TargetId)!, events);
                }

                break;
            case Canto canto:
                (next, rejection) = ApplyCanto(state, content, canto, events);
                if (rejection is null)
                {
                    next = MapEvents.AfterMove(next, content, next.Find(canto.UnitId)!, events);
                    if (next.Find(canto.UnitId) is { } cantoed && cantoed.At != state.Find(canto.UnitId)!.At)
                    {
                        next = FireWatches(next, content, canto.UnitId, events);
                    }
                }

                break;
            case Order order:
                (next, rejection) = ApplyOrder(state, content, order, events);
                break;
            case FallBack fallBack:
                (next, rejection) = ApplyFallBack(state, content, fallBack, events);
                if (rejection is null)
                {
                    next = MapEvents.AfterMove(next, content, next.Find(fallBack.UnitId)!, events);
                    if (next.Find(fallBack.UnitId) is { } fellBack && fellBack.At != state.Find(fallBack.UnitId)!.At)
                    {
                        next = FireWatches(next, content, fallBack.UnitId, events);
                    }
                }

                break;
            case Retreat retreat:
                (next, rejection) = ApplyRetreat(state, content, retreat, events);
                break;
            case EndPhase:
                (next, rejection) = ApplyEndPhase(state, content, events);
                break;
            case Recall recall:
                return ApplyRecall(state, recall);
            case Undo undo:
                return TakeBack.Apply(state, undo);
            default:
                next = state;
                rejection = new Rejection(RejectionReason.UnknownCommand, $"unknown command {command.GetType().Name}");
                break;
        }

        if (rejection is not null)
        {
            return new ApplyResult(state, ValueList<GameEvent>.Empty, rejection);
        }

        var acted = command switch
        {
            Attack a => a.UnitId,
            UseItem i => i.UnitId,
            Wait w => w.UnitId,
            _ => null,
        };
        if (acted is not null)
        {
            next = OpenCanto(next, content, acted, healed: command is UseItem && events.OfType<UnitHealed>().Any(h => h.UnitId != acted));
        }

        next = Freed.After(state, next, events);
        next = Break.After(state, next, content, events);
        next = Fronts.After(state, next, content, events);
        next = WakeGroups(state, next, content, events);
        if (next.Map.KeepsakesEnabled && next.Outcome.IsOver)
        {
            AddLostKeepsakes(next, events);
        }

        next = next with { History = state.History.Add(state with { History = ValueList<BattleState>.Empty }) };
        return new ApplyResult(next, ValueList<GameEvent>.From(events), null);
    }

    /// <summary>
    /// The Guard wake check of DESIGN.md section 8 through <see cref="WakeCheck"/>, run
    /// after every accepted command on where units stand afterwards, with the dead and
    /// the fought tiles read off the command's events. One <see cref="GroupWoke"/> per
    /// group per command. On a dusk map a group a player-phase command wakes has its lamps
    /// lit (issue 382): the event names its members, and the player sees them until the
    /// enemy phase that follows ends. A wake at a phase's turn is not a player-phase wake.
    /// </summary>
    private static BattleState WakeGroups(BattleState before, BattleState after, GameContent content, List<GameEvent> events)
    {
        var died = new List<string>();
        var noisy = new List<Noise>();
        foreach (var e in events)
        {
            switch (e)
            {
                case UnitDied dead when before.Find(dead.UnitId) is { Group: { } group }:
                    died.Add(group);
                    break;
                case CombatFought fought:
                    var attacker = before.Find(fought.AttackerId)!;
                    var target = before.Find(fought.TargetId)!;
                    var radius = Signatures.NoiseRadius(before, content, attacker, target);
                    noisy.Add(new Noise(attacker.At, radius));
                    noisy.Add(new Noise(target.At, radius));
                    break;
                case Shoved shoved:
                    noisy.Add(new Noise(shoved.From, content.NoiseRadius));
                    noisy.Add(new Noise(shoved.To, content.NoiseRadius));
                    break;
            }
        }

        var next = after;
        var lamps = Dusk.Sight(after) is not null && before.Phase == Side.Player && after.Phase == Side.Player;
        foreach (var woke in WakeCheck.Run(before, after, content, noisy, died))
        {
            next = next.Wake(woke.Group);
            if (!lamps)
            {
                events.Add(woke);
                continue;
            }

            var members = next.UnitsOf(Side.Enemy).Where(u => u.Group == woke.Group).OrderBy(u => u.At.Y).ThenBy(u => u.At.X).Select(u => new Lamp(u.Id, u.At));
            events.Add(woke with { Lamps = ValueList<Lamp>.From(members) });
            next = next.Light(woke.Group);
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

        if (unit.Resting)
        {
            rejection = new Rejection(RejectionReason.AlreadyActed, $"{unit.Id} spent this phase on last phase's strike and cannot move or act until its side's next phase");
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
        var refused = "";
        var entry = move.Via is { } via ? state.RouteVia(unit, content, via, move.To, out refused) : reach.EntryAt(move.To);
        if (move.Via is not null && entry is null)
        {
            return (state, new Rejection(RejectionReason.OutOfReach, $"{unit.Id} cannot move to {move.To} via {move.Via}: {refused}"));
        }

        if (entry is not { CanEnd: true })
        {
            var why = !state.Map.Contains(move.To) ? "outside the map"
                : entry is null ? $"not within {reach.Mov} movement from {unit.At}"
                : "occupied by an ally";
            return (state, new Rejection(RejectionReason.OutOfReach, $"{unit.Id} cannot move to {move.To}: {why}"));
        }

        events.Add(new UnitMoved(unit.Id, unit.At, move.To, entry.Path));
        int? canto = Signatures.HasCanto(state, content, unit) ? reach.Mov - entry.Cost : null;
        var moved = EndMove(state, unit, unit with { At = move.To, Moved = true, Canto = canto }, events);
        return (Planks.AfterWalk(moved, content, unit, unit.At, entry.Path, events), null);
    }

    /// <summary>
    /// Puts <paramref name="after"/> on the board where its move ended. On a <c>keepsakes: on</c>
    /// map an enemy that ends a move on a stack of keepsakes takes all of it (DESIGN.md 13.8,
    /// issue 295), one <see cref="KeepsakeTaken"/> each, oldest first, through
    /// <see cref="BattleState.Carrying"/>; the stack leaves the tile.
    /// </summary>
    private static BattleState EndMove(BattleState state, BattleUnit before, BattleUnit after, List<GameEvent> events)
    {
        var carrier = state.Carrying(before, after.At);
        if (ReferenceEquals(carrier, before))
        {
            return state.WithUnit(after);
        }

        foreach (var keepsake in state.KeepsakesAt(after.At))
        {
            events.Add(new KeepsakeTaken(before.Id, keepsake.FallenId, keepsake.Item.ItemId));
        }

        return state.WithUnit(after with { Unit = carrier.Unit }) with
        {
            Keepsakes = ValueList<Keepsake>.From(state.Keepsakes.Where(k => k.At != after.At)),
        };
    }

    /// <summary>
    /// Issue 295's closing lines: once the battle is over, one <see cref="KeepsakeLost"/> per
    /// keepsake nobody recovered, those lying on the board in the order they were left, then
    /// those still on a carrier, enemies in board order and stacks in inventory order.
    /// </summary>
    private static void AddLostKeepsakes(BattleState state, List<GameEvent> events)
    {
        foreach (var keepsake in state.Keepsakes)
        {
            events.Add(new KeepsakeLost(keepsake.FallenId, keepsake.Item.ItemId, keepsake.At, null));
        }

        foreach (var enemy in state.UnitsOf(Side.Enemy))
        {
            foreach (var stack in enemy.Unit.Inventory.Items)
            {
                if (stack.Keepsake is { } fallen)
                {
                    events.Add(new KeepsakeLost(fallen, stack.ItemId, enemy.At, enemy.Id));
                }
            }
        }
    }

    /// <summary>
    /// Owes a Canto (issue 71) to a unit that has just attacked, used an item or waited and
    /// is still on the board: what its Move left, set by <see cref="ApplyMove"/>, or its
    /// full Mov when it acted without moving. A unit without Canto is left as it is; one whose Canto
    /// is owed only after a heal (issue 706) is owed it when <paramref name="healed"/>, its spell having healed an ally.
    /// </summary>
    private static BattleState OpenCanto(BattleState state, GameContent content, string unitId, bool healed)
    {
        if (state.Find(unitId) is not { } unit
            || !(Signatures.HasCanto(state, content, unit) || (healed && AbilityRules.HasCantoAfterHeal(content.AbilitiesOf(unit.Unit)))))
        {
            return state;
        }

        return unit.Canto is null
            ? state.WithUnit(unit with { Canto = Frost.Mov(content.Class(unit.Unit.ClassId).Mov, unit) })
            : state;
    }

    /// <summary>
    /// Canto (issue 71): a unit that has acted and is owed a Canto moves within the reach
    /// <see cref="BattleState.CantoReachOf"/> gives, its own tile included, and is done.
    /// </summary>
    private static (BattleState, Rejection?) ApplyCanto(BattleState state, GameContent content, Canto canto, List<GameEvent> events)
    {
        var unit = state.Find(canto.UnitId);
        if (unit is null)
        {
            return (state, new Rejection(RejectionReason.NoSuchUnit, $"no living unit '{canto.UnitId}'"));
        }

        if (unit.Side != state.Phase)
        {
            return (state, new Rejection(RejectionReason.NotThisSide, $"{unit.Id} is a {unit.Side} unit and it is the {state.Phase} phase"));
        }

        if (state.CantoReachOf(unit, content) is not { } reach)
        {
            var who = Referent.For(content, unit.Unit);
            var why = !Signatures.HasCanto(state, content, unit) ? $"{who.Subject} {who.Verb("has", "have")} no Canto"
                : !unit.Acted ? $"{who.Subject} {who.Verb("has", "have")} not acted yet this phase"
                : $"{who.Possessive} Canto is spent this phase";
            return (state, new Rejection(RejectionReason.NoCanto, $"{unit.Id} cannot Canto: {why}"));
        }

        var entry = reach.EntryAt(canto.To);
        if (entry is not { CanEnd: true })
        {
            var why = !state.Map.Contains(canto.To) ? "outside the map"
                : entry is null ? $"not within the {reach.Mov} movement {Referent.For(content, unit.Unit).Possessive} Canto has left from {unit.At}"
                : "occupied by an ally";
            return (state, new Rejection(RejectionReason.OutOfReach, $"{unit.Id} cannot Canto to {canto.To}: {why}"));
        }

        events.Add(new Cantoed(unit.Id, unit.At, canto.To, entry.Path));
        // A Canto that leaves the tile takes a brace off (DESIGN 13.14); staying keeps it.
        var braced = unit.Braced && canto.To == unit.At;
        var cantoed = state.WithUnit(unit with { At = canto.To, Canto = null, Braced = braced });
        return (Planks.AfterWalk(cantoed, content, unit, unit.At, entry.Path, events), null);
    }

    /// <summary>
    /// Commander's Word (DESIGN.md 13.2, issue 85): the captain's action, after his Move or
    /// without one, once a map. Press gives +1 Mov this phase to the allies in his radius who
    /// have not moved; Rally heals those in it <see cref="Orders.RallyHeal"/>; Fall back owes
    /// those in it who have acted one <see cref="FallBack"/> move. No Canto follows.
    /// </summary>
    private static (BattleState, Rejection?) ApplyOrder(BattleState state, GameContent content, Order order, List<GameEvent> events)
    {
        if (Orders.Refusal(state) is { } refusal)
        {
            return (state, new Rejection(RejectionReason.CannotOrder, $"cannot call {Orders.Word(order.Kind)}: {refusal}"));
        }

        var captain = state.UnitsOf(Side.Player).First(u => u.IsCaptain);
        var inRadius = Orders.InRadius(state, content, captain, captain.At);
        var reached = Orders.Reached(state, content, captain, captain.At, order.Kind);
        var exposure = Exposure.Of(state, content, captain, captain.At).NoCrit;
        events.Add(new OrderCalled(captain.Id, order.Kind, Orders.Radius(captain, content), ValueList<string>.From(reached.Select(u => u.Id)), inRadius.Count, Orders.Allies(state).Count, exposure));
        var next = state.WithUnit(captain with { Moved = true, Acted = true, Canto = null }) with { OrderCalled = order.Kind };
        foreach (var ally in reached)
        {
            switch (order.Kind)
            {
                case OrderKind.Press:
                    next = next.WithUnit(ally with { Pressed = true });
                    break;
                case OrderKind.Rally:
                    var heal = Orders.RallyHeal(ally, content);
                    if (heal > 0)
                    {
                        events.Add(new UnitHealed(ally.Id, heal, ally.Hp + heal));
                        next = next.WithUnit(ally with { Hp = ally.Hp + heal });
                    }

                    break;
                default:
                    next = next.WithUnit(ally with { FallingBack = true });
                    break;
            }
        }

        return (next, null);
    }

    /// <summary>
    /// The move a Fall back order owed (issue 85): within <see cref="Orders.FallBackMov"/> of where
    /// the unit stands, its own tile included as a decline, and refused, naming the group, if the
    /// unit standing there would wake a sleeping group by the section 8 proximity rule.
    /// </summary>
    private static (BattleState, Rejection?) ApplyFallBack(BattleState state, GameContent content, FallBack fallBack, List<GameEvent> events)
    {
        var unit = state.Find(fallBack.UnitId);
        if (unit is null)
        {
            return (state, new Rejection(RejectionReason.NoSuchUnit, $"no living unit '{fallBack.UnitId}'"));
        }

        if (unit.Side != state.Phase)
        {
            return (state, new Rejection(RejectionReason.NotThisSide, $"{unit.Id} is a {unit.Side} unit and it is the {state.Phase} phase"));
        }

        if (state.FallBackReachOf(unit, content) is not { } reach)
        {
            return (state, new Rejection(RejectionReason.NoFallBack, $"{unit.Id} cannot fall back: no Fall back order reached it this phase, or it has taken it"));
        }

        var entry = reach.EntryAt(fallBack.To);
        if (entry is not { CanEnd: true })
        {
            var why = !state.Map.Contains(fallBack.To) ? "outside the map"
                : entry is null ? $"not within the {reach.Mov} movement a fall back gives from {unit.At}"
                : "occupied by an ally";
            return (state, new Rejection(RejectionReason.OutOfReach, $"{unit.Id} cannot fall back to {fallBack.To}: {why}"));
        }

        var moved = state.WithUnit(unit with { At = fallBack.To, FallingBack = false, Braced = unit.Braced && fallBack.To == unit.At });
        var woken = WakeCheck.Run(state, moved, content, Array.Empty<Noise>(), Array.Empty<string>());
        if (woken.Count > 0)
        {
            return (state, new Rejection(RejectionReason.NoFallBack, $"{unit.Id} cannot fall back to {fallBack.To}: it would wake {string.Join(", ", woken.Select(w => w.Group))}"));
        }

        events.Add(new FellBack(unit.Id, unit.At, fallBack.To, entry.Path));
        return (moved, null);
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

        var (armed, weapon, choice) = ChooseWeapon(unit, content, attack.Slot);
        if (choice is not null)
        {
            return (state, choice);
        }

        unit = armed;
        CombatArtEffect? art = null;
        if (attack.Art is not null)
        {
            (art, var refused) = ChooseArt(unit, content, weapon!, attack.Art);
            if (refused is not null)
            {
                return (state, refused);
            }

            weapon = art!.Apply(weapon!);
        }

        var distance = unit.At.DistanceTo(target.At);
        if (!weapon!.InRange(distance))
        {
            return (state, new Rejection(
                RejectionReason.OutOfRange,
                $"{target.Id} at {target.At} is {distance} tiles from {unit.Id} at {unit.At}; {weapon.Name} reaches {weapon.MinRange}-{weapon.MaxRange}"));
        }

        if (!Dusk.Sees(state, unit.Side, target.At))
        {
            return (state, new Rejection(
                RejectionReason.Unseen,
                $"no unit on {unit.Id}'s side can see {target.At} at dusk (sight {Dusk.Sight(state)})"));
        }

        if (attack.Slot is { } chosen && chosen != state.Find(unit.Id)!.EquippedSlot(content))
        {
            events.Add(new WeaponEquipped(unit.Id, weapon.Id));
        }

        if (art is not null)
        {
            events.Add(new ArtDeclared(unit.Id, attack.Art!, weapon.Id, art.Cost));
        }

        if (Windup.Raises(state, weapon))
        {
            events.Add(new BlowRaised(unit.Id, target.Id, target.At));
            return (state.WithUnit(unit with { Moved = true, Acted = true, WindupAt = target.At }), null);
        }

        if (CoverRule.Swapped(state, target) is ({ } swappedBoard, { } coverer, { } ally))
        {
            var aimed = Combat.Forecast(unit.ToCombatant(state, content, art: art, against: target), target.Answering(state, content, unit.At, unit), distance, state.Scheme);
            var covering = Combat.Forecast(unit.ToCombatant(swappedBoard, content, art: art, against: coverer), coverer.Answering(swappedBoard, content, unit.At, unit), distance, state.Scheme);
            events.Add(new CoverFired(coverer.Id, ally.Id, unit.Id, coverer.At, ally.At, aimed.AttackerDamageLivedFor(unit.Hp) >= target.Hp, covering.Defender.Strikes));
            state = swappedBoard;
            target = coverer;
        }

        var striker = unit.ToCombatant(state, content, art: art, against: target);
        var answer = target.Answering(state, content, unit.At, unit);
        var shown = Combat.Forecast(striker, answer, distance, state.Scheme).Attacker.DisplayedHit;
        if (Signatures.Refuses(state, content, unit, shown))
        {
            return (state, new Rejection(RejectionReason.SignatureRefused, Signatures.LedgerRefusal(unit, target.Id, shown)));
        }

        var defenderWeapon = target.EquippedWeapon(content);
        var result = CombatResolver.Resolve(
            striker,
            answer,
            distance,
            new CombatContext(state.Turn, state.Phase),
            new KeyedRng(state.Seed),
            state.Scheme);
        events.Add(new CombatFought(unit.Id, target.Id, state.Turn, state.Phase, result.Strikes, result.AttackerHp, result.DefenderHp));

        var attackerAfter = SpendDurability(unit with { Hp = result.AttackerHp, Moved = true, Acted = true }, result.Strikes, content, events, art?.Cost ?? 0);
        if (art is { PerMap: not null })
        {
            attackerAfter = attackerAfter with { ArtsDeclared = (attackerAfter.ArtsDeclared ?? ValueList<string>.Empty).Add(attack.Art!) };
        }

        if (art is { CostsNextPhase: true })
        {
            attackerAfter = attackerAfter with { Spent = 1 };
        }

        var targetAfter = SpendDurability(target with { Hp = result.DefenderHp }, result.Strikes, content, events);
        attackerAfter = AwardExp(attackerAfter, targetAfter, result.Strikes, result.DefenderDied, content, state.Seed, events);
        targetAfter = AwardExp(targetAfter, attackerAfter, result.Strikes, result.AttackerDied, content, state.Seed, events);
        attackerAfter = AwardRank(attackerAfter, weapon, result.Strikes, result.DefenderDied, events);
        targetAfter = AwardRank(targetAfter, defenderWeapon, result.Strikes, result.AttackerDied, events);
        attackerAfter = AwardMastery(attackerAfter, content, events);
        targetAfter = AwardMastery(targetAfter, content, events);
        attackerAfter = Kinsbane.AfterCombat(attackerAfter, content, result.Strikes, result.DefenderDied, events);
        targetAfter = Kinsbane.AfterCombat(targetAfter, content, result.Strikes, result.AttackerDied, events);
        attackerAfter = KillHeal(attackerAfter, weapon, result.DefenderDied, content, events);
        targetAfter = KillHeal(targetAfter, defenderWeapon, result.AttackerDied, content, events);
        attackerAfter = Heirloom.AfterCombat(attackerAfter, state, content, result.Strikes, events);
        targetAfter = Heirloom.AfterCombat(targetAfter, state, content, result.Strikes, events);
        var next = state.WithUnit(attackerAfter);
        next = next.WithUnit(targetAfter);
        if (result.DefenderDied)
        {
            events.Add(new UnitDied(target.Id, target.Side, target.At));
            next = LeaveKeepsake(next, targetAfter, content, events).WithoutUnit(target.Id);
        }

        if (result.AttackerDied)
        {
            events.Add(new UnitDied(unit.Id, unit.Side, unit.At));
            next = LeaveKeepsake(next, attackerAfter, content, events).WithoutUnit(unit.Id);
        }

        if (result.DefenderDied && !result.AttackerDied)
        {
            next = SwearGrudges(next, content, target, unit, events);
        }
        else if (result.AttackerDied && !result.DefenderDied)
        {
            next = SwearGrudges(next, content, unit, target, events);
        }

        next = Wildfire.AfterCombat(next, unit.Id, weapon, target.At, result.Strikes, events);
        next = Wildfire.AfterCombat(next, target.Id, defenderWeapon, unit.At, result.Strikes, events);
        next = Frost.AfterCombat(next, unit.Id, weapon, target.Id, defenderWeapon, result.Strikes, events);
        next = Grounding.AfterCombat(next, content, unit.Id, weapon, target.Id, defenderWeapon, result.Strikes, events);
        next = Opening.AfterAttack(next, content, unit, target.Id, result.Strikes, events);
        next = Windup.AfterCombat(next, content, unit, target, result.Strikes, events);
        next = EndStruckWatches(next, result.Strikes, events);
        return (next, null);
    }

    /// <summary>
    /// DESIGN.md 13.17: a unit taking Watch. It needs the header and an equipped weapon reaching
    /// range 2; the event names the best strike it passes up. No Canto follows. Under
    /// <c>overwatch: hold</c> (13.17b) only an unmoved player unit watches, with any weapon, and the
    /// event also names the move it gives up.
    /// </summary>
    private static (BattleState, Rejection?) ApplyWatch(BattleState state, GameContent content, Watch watch, List<GameEvent> events)
    {
        var unit = Acting(state, watch.UnitId, out var rejection);
        if (unit is null)
        {
            return (state, rejection);
        }

        if (Overwatch.Refusal(state, content, unit) is { } why)
        {
            return (state, new Rejection(RejectionReason.CannotWatch, $"{unit.Id} cannot watch: {why}"));
        }

        var passed = Overwatch.PassedUp(state, content, unit);
        var hold = state.Map.OverwatchHold;
        events.Add(new WatchTaken(unit.Id, unit.At, passed?.TargetId, passed?.Hit, hold, hold ? Overwatch.GivesUp(state, content, unit) : null));
        return (state.WithUnit(unit with { Moved = true, Acted = true, Watching = true, Canto = null }), null);
    }

    /// <summary>
    /// DESIGN.md 13.19: a unit taking Cover on an ally beside it, refused by
    /// <see cref="CoverRule.Refusal"/>. The event names where the ally lands if the swap
    /// fires and the best strike the coverer passes up. No Canto follows.
    /// </summary>
    private static (BattleState, Rejection?) ApplyCover(BattleState state, GameContent content, Cover cover, List<GameEvent> events)
    {
        var unit = Acting(state, cover.UnitId, out var rejection);
        if (unit is null)
        {
            return (state, rejection);
        }

        if (state.Find(cover.AllyId) is not { } ally)
        {
            return (state, new Rejection(RejectionReason.NoSuchTarget, $"no living unit '{cover.AllyId}' to cover"));
        }

        if (CoverRule.Refusal(state, unit, ally) is { } why)
        {
            return (state, new Rejection(RejectionReason.CannotCover, $"{unit.Id} cannot cover {ally.Id}: {why}"));
        }

        var passed = Overwatch.PassedUp(state, content, unit);
        events.Add(new CoverTaken(unit.Id, ally.Id, unit.At, passed?.TargetId, passed?.Hit));
        return (state.WithUnit(unit with { Moved = true, Acted = true, Canto = null }).WithUnit(ally with { CoveredBy = unit.Id }), null);
    }

    /// <summary>
    /// DESIGN.md 13.17: after <paramref name="moverId"/> ends a move, each watcher whose ring
    /// holds its tile, in id order, shoots it once (<see cref="Overwatch.Shoot"/>), until one
    /// kills. A watch that fires is spent. The shot spends a use, gives EXP and rank as any
    /// strike, and a kill is a death like any other. A shot Ottilie's ledger refuses (DESIGN.md
    /// 13.18) is printed as held and the watch stays for the next arrival.
    /// </summary>
    private static BattleState FireWatches(BattleState state, GameContent content, string moverId, List<GameEvent> events)
    {
        if (!state.Map.OverwatchEnabled || state.Find(moverId) is not { } mover)
        {
            return state;
        }

        foreach (var watcherId in Overwatch.WatchersOver(state, content, mover.Side, mover.At).Select(w => w.Id).ToList())
        {
            if (state.Find(moverId) is not { } target || state.Find(watcherId) is not { } watcher)
            {
                break;
            }

            if (Overwatch.Refused(state, content, watcher, target) is { } held)
            {
                events.Add(new WatchHeld(watcher.Id, target.Id, target.At, held));
                continue;
            }

            var strike = Overwatch.Shoot(state, content, watcher, target, new KeyedRng(state.Seed));
            var strikes = ValueList<StrikeEvent>.Empty.Add(strike);
            events.Add(new WatchFired(watcher.Id, target.Id, target.At, strike));
            var died = strike.TargetHpAfter == 0;
            var weapon = watcher.EquippedWeapon(content);
            var shooter = SpendDurability(watcher with { Watching = false }, strikes, content, events);
            var struck = target with { Hp = strike.TargetHpAfter };
            shooter = AwardExp(shooter, struck, strikes, died, content, state.Seed, events);
            struck = AwardExp(struck, shooter, strikes, false, content, state.Seed, events);
            shooter = AwardRank(shooter, weapon, strikes, died, events);
            shooter = AwardMastery(shooter, content, events);
            state = state.WithUnit(shooter).WithUnit(struck);
            if (died)
            {
                events.Add(new UnitDied(target.Id, target.Side, target.At));
                state = LeaveKeepsake(state, struck, content, events).WithoutUnit(target.Id);
                state = SwearGrudges(state, content, target, shooter, events);
                break;
            }

            state = Frost.AfterCombat(state, shooter.Id, weapon, struck.Id, null, strikes, events);
            state = Grounding.AfterCombat(state, content, shooter.Id, weapon, struck.Id, null, strikes, events);
            state = Windup.AfterCombat(state, content, shooter, struck, strikes, events);
            state = EndStruckWatches(state, strikes, events);
        }

        return state;
    }

    /// <summary>DESIGN.md 13.17: every watching unit a strike in <paramref name="strikes"/> targeted, hit or miss, stops watching.</summary>
    private static BattleState EndStruckWatches(BattleState state, ValueList<StrikeEvent> strikes, List<GameEvent> events)
    {
        foreach (var id in strikes.Select(s => s.TargetId).Distinct().ToList())
        {
            if (state.Find(id) is { Watching: true } watcher)
            {
                events.Add(new WatchEnded(watcher.Id));
                state = state.WithUnit(watcher with { Watching = false });
            }
        }

        return state;
    }

    /// <summary>
    /// DESIGN.md 13.4 (Named rivals, the grudge arm, experiment): on a <c>grudges: on</c> map,
    /// when a player unit kills an enemy, every living enemy of the dead enemy's group swears
    /// against the killer, replacing any grudge it held, one <see cref="GrudgeSworn"/> each.
    /// A counter-kill counts the same as a strike. Nothing happens on a map without the header,
    /// when the dead unit is a player unit, or when the dead enemy had no group. At dusk only
    /// the witnesses swear (issue 331): a group-mate that knows of the killer at the kill
    /// (<see cref="Dusk.Knows"/>) on the board the dead has left, so the dead unit's own eyes
    /// tell nobody and a kill in the dark makes no grudge in those who never saw it.
    /// </summary>
    private static BattleState SwearGrudges(BattleState state, GameContent content, BattleUnit dead, BattleUnit killer, List<GameEvent> events)
    {
        if (!state.Map.GrudgesEnabled || dead.Side != Side.Enemy || killer.Side != Side.Player || dead.Group is not { } group)
        {
            return state;
        }

        var next = state;
        var witness = state.Find(killer.Id) ?? killer;
        foreach (var mate in state.UnitsOf(Side.Enemy).Where(u => u.Group == group && u.Grudge != killer.Id && Dusk.Knows(state, content, u, witness)).ToList())
        {
            events.Add(new GrudgeSworn(mate.Id, killer.Id));
            next = next.WithUnit(mate with { Grudge = killer.Id });
        }

        return next;
    }

    /// <summary>
    /// The unit as it strikes and the weapon it strikes with: the equipped weapon, or the
    /// weapon in <paramref name="slot"/> moved to the front. A slot that holds no usable
    /// weapon (empty, an item, a healing spell, a spent spell) is refused by name.
    /// </summary>
    public static (BattleUnit Unit, Weapon? Weapon, Rejection? Rejection) ChooseWeapon(BattleUnit unit, GameContent content, int? slot)
    {
        if (slot is null)
        {
            var equipped = unit.EquippedWeapon(content);
            return equipped is null
                ? (unit, null, new Rejection(RejectionReason.NoWeapon, $"{unit.Id} has no weapon to attack with"))
                : (unit, equipped, null);
        }

        var count = unit.Unit.Inventory.Count;
        if (slot < 0 || slot >= count)
        {
            return (unit, null, new Rejection(RejectionReason.EmptySlot, $"{unit.Id} has nothing in slot {slot}; slots run 0-{count - 1}"));
        }

        var weapon = unit.UsableWeaponAt(content, slot.Value);
        if (weapon is null)
        {
            var itemId = unit.Unit.Inventory.Items[slot.Value].ItemId;
            var why = content.Items.ContainsKey(itemId) ? "an item, not a weapon"
                : content.Weapon(itemId).Heals ? "a healing spell; use it with item"
                : content.Weapon(itemId).IsMagic && unit.Unit.Inventory.Items[slot.Value].Uses == 0 ? "spent for this battle"
                : !content.Class(unit.Unit.ClassId).CanUse(content.Weapon(itemId).Type) ? $"not a weapon a {unit.Unit.ClassId} can use"
                : RankShort(unit.Unit, content.Weapon(itemId));
            return (unit, null, new Rejection(RejectionReason.NotUsable, $"{unit.Id} cannot attack with {itemId}: {why}"));
        }

        return (unit.WithSlotInFront(slot.Value), weapon, null);
    }

    /// <summary>
    /// The combat art an attack declares (issue 68), or why it cannot: the unit must know
    /// it (<see cref="RejectionReason.NoSuchArt"/>), and the weapon it strikes with must be
    /// of the art's type (and the art's own item, for a signature art), at a rank the unit has reached, not broken, and holding at least
    /// the art's cost and the first strike's use (<see cref="RejectionReason.ArtRefused"/>).
    /// <paramref name="unit"/> is the unit with <paramref name="weapon"/> already equipped.
    /// </summary>
    public static (CombatArtEffect? Art, Rejection? Rejection) ChooseArt(BattleUnit unit, GameContent content, Weapon weapon, string artId)
    {
        var known = content.ArtsOf(unit.Unit).FirstOrDefault(a => a.Ability.Id == artId);
        if (known.Art is null)
        {
            return (null, new Rejection(RejectionReason.NoSuchArt, $"{unit.Id} knows no technique '{artId}'"));
        }

        var (ability, art) = known;
        var uses = unit.Unit.Inventory.Items[unit.EquippedSlot(content)].Uses;
        var why = weapon.Type != art.Weapon ? $"{ability.Name} is a {Lower(art.Weapon)} technique and {weapon.Name} is a {Lower(weapon.Type)}"
            : art.Item is { } item && item != weapon.Id ? $"{ability.Name} is declared only with {content.ItemName(item)}"
            : unit.Unit.Skill.Rank(art.Weapon) < art.Rank ? $"rank {unit.Unit.Skill.Rank(art.Weapon)} in {Lower(art.Weapon)}, and {ability.Name} needs {art.Rank}"
            : art.PerMap is { } cap && unit.TimesDeclared(artId) >= cap ? $"{ability.Name} is {Times(cap)} a map and is spent"
            : uses == 0 ? $"{weapon.Name} is broken and cannot pay for a technique"
            : uses < art.UsesNeeded ? $"{ability.Name} costs {art.UsesNeeded} uses with the strike and {weapon.Name} has {uses} left"
            : null;
        return why is null ? (art, null) : (null, new Rejection(RejectionReason.ArtRefused, $"{unit.Id} cannot use {ability.Name}: {why}"));

        static string Lower(WeaponType type) => type.Label();
        static string Times(int cap) => cap == 1 ? "once" : $"{cap} times";
    }

    /// <summary>The refusal's reason when a unit's rank is below a weapon's (issue 67): the unit's rank in the type and the rank the weapon needs.</summary>
    public static string RankShort(Unit unit, Weapon weapon) =>
        $"rank {unit.Skill.Rank(weapon.Type)} in {weapon.Type.Label()}, and {weapon.Name} needs {weapon.Rank}";

    /// <summary>
    /// Section 5's durability: every strike a unit made in the combat, landed or not,
    /// spends one use of the weapon it struck with, never below zero, and a declared art's
    /// <paramref name="artCost"/> is spent with them, hit or miss (issue 68). The strike that
    /// empties a physical weapon emits <see cref="WeaponBroke"/> and the weapon stays,
    /// broken; the one that empties a spell emits <see cref="SpellSpent"/>. A gauntlet spends
    /// one use for the combat however many strikes it made (issue 70). A hungering weapon never
    /// spends below 1 (DESIGN.md 13.23, <see cref="Kinsbane"/>), nor does an heirloom (issue 646,
    /// <see cref="Heirloom"/>), so neither ever breaks.
    /// </summary>
    private static BattleUnit SpendDurability(BattleUnit unit, ValueList<StrikeEvent> strikes, GameContent content, List<GameEvent> events, int artCost = 0)
    {
        var struck = strikes.Count(s => s.AttackerId == unit.Id);
        var slot = unit.EquippedSlot(content);
        if (struck + artCost == 0 || slot < 0)
        {
            return unit;
        }

        var stack = unit.Unit.Inventory.Items[slot];
        if (stack.Uses == 0)
        {
            return unit;
        }

        var made = (content.Weapon(stack.ItemId).Type.SpendsPerStrike() ? struck : Math.Min(1, struck)) + artCost;

        var left = Math.Max(content.Weapon(stack.ItemId) is { Hungers: true } or { Heirloom: not null } ? 1 : 0, stack.Uses - made);
        if (left == 0)
        {
            events.Add(content.Weapon(stack.ItemId).IsMagic ? new SpellSpent(unit.Id, stack.ItemId) : new WeaponBroke(unit.Id, stack.ItemId));
        }

        return unit with { Unit = unit.Unit with { Inventory = unit.Unit.Inventory.Replace(slot, stack with { Uses = left }) } };
    }

    /// <summary>
    /// Section 7's Item action. A consumable (an entry of <c>items.json</c>) heals its user
    /// by its amount and needs no target; a healing spell heals the ally named as the
    /// target, within the spell's range, by section 5's formula, and the healer earns
    /// section 5's heal EXP. Either spends one use and ends the action; a consumable at
    /// zero leaves the inventory, a spell at zero stays for the next map. Healing a unit
    /// at full HP is refused, so no script burns a use on nothing.
    /// </summary>
    private static (BattleState, Rejection?) ApplyUseItem(BattleState state, GameContent content, UseItem use, List<GameEvent> events)
    {
        var unit = Acting(state, use.UnitId, out var rejection);
        if (unit is null)
        {
            return (state, rejection);
        }

        var inventory = unit.Unit.Inventory;
        if (use.Slot < 0 || use.Slot >= inventory.Count)
        {
            return (state, new Rejection(RejectionReason.EmptySlot, $"{unit.Id} has nothing in slot {use.Slot}; slots run 0-{inventory.Count - 1}"));
        }

        var stack = inventory.Items[use.Slot];
        if (content.Items.TryGetValue(stack.ItemId, out var item))
        {
            if (use.TargetId is not null && use.TargetId != unit.Id)
            {
                return (state, new Rejection(RejectionReason.NotUsable, $"{item.Name} heals its user; it cannot be used on {use.TargetId}"));
            }

            var max = unit.MaxHp(content);
            if (unit.Hp >= max)
            {
                return (state, new Rejection(RejectionReason.NothingToHeal, $"{unit.Id} is at full HP"));
            }

            var hp = Math.Min(max, unit.Hp + item.Heals);
            var left = stack.Uses - 1;
            var after = left == 0 ? inventory.RemoveAt(use.Slot) : inventory.Replace(use.Slot, stack with { Uses = left });
            events.Add(new ItemUsed(unit.Id, item.Id, unit.Id, left));
            events.Add(new UnitHealed(unit.Id, hp - unit.Hp, hp));
            return (state.WithUnit(unit with { Hp = hp, Moved = true, Acted = true, Unit = unit.Unit with { Inventory = after } }), null);
        }

        var spell = content.WeaponOf(unit.Unit, content.Weapon(stack.ItemId));
        if (!spell.Heals || !content.Class(unit.Unit.ClassId).CanUse(spell.Type))
        {
            return (state, new Rejection(RejectionReason.NotUsable, $"{spell.Name} is a weapon, not an item; attack with it"));
        }

        if (!content.Class(unit.Unit.ClassId).CanHealWith(spell.Type))
        {
            return (state, new Rejection(RejectionReason.NotUsable, $"{unit.Id} cannot use {stack.ItemId}: a {content.Class(unit.Unit.ClassId).Name} strikes with {spell.Type.Label()} and never heals"));
        }

        if (!unit.Unit.CanWield(spell, content.Class(unit.Unit.ClassId)))
        {
            return (state, new Rejection(RejectionReason.NotUsable, $"{unit.Id} cannot use {stack.ItemId}: {RankShort(unit.Unit, spell)}"));
        }

        if (stack.Uses == 0)
        {
            return (state, new Rejection(RejectionReason.NotUsable, $"{spell.Name} has no uses left this battle"));
        }

        if (use.TargetId is null)
        {
            return (state, new Rejection(RejectionReason.NoTarget, $"{spell.Name} needs a target: item {unit.Id} {use.Slot} <ally>"));
        }

        var target = state.Find(use.TargetId);
        if (target is null)
        {
            return (state, new Rejection(RejectionReason.NoSuchTarget, $"no living unit '{use.TargetId}' to heal"));
        }

        if (target.Side != unit.Side)
        {
            return (state, new Rejection(RejectionReason.NotAnAlly, $"{target.Id} is not on {unit.Id}'s side"));
        }

        var distance = unit.At.DistanceTo(target.At);
        if (!spell.InRange(distance))
        {
            return (state, new Rejection(
                RejectionReason.OutOfRange,
                $"{target.Id} at {target.At} is {distance} tiles from {unit.Id} at {unit.At}; {spell.Name} reaches {spell.MinRange}-{spell.MaxRange}"));
        }

        var targetMax = target.MaxHp(content);
        if (target.Hp >= targetMax)
        {
            return (state, new Rejection(RejectionReason.NothingToHeal, $"{target.Id} is at full HP"));
        }

        var healed = Math.Min(targetMax, target.Hp + Combat.Heal(unit.ToCombatant(state.Map, content), spell));
        var usesLeft = stack.Uses - 1;
        events.Add(new ItemUsed(unit.Id, spell.Id, target.Id, usesLeft));
        events.Add(new UnitHealed(target.Id, healed - target.Hp, healed));
        if (usesLeft == 0)
        {
            events.Add(new SpellSpent(unit.Id, spell.Id));
        }

        var healer = unit with { Moved = true, Acted = true, Unit = unit.Unit with { Inventory = inventory.Replace(use.Slot, stack with { Uses = usesLeft }) } };
        healer = AwardHealExp(healer, target.Hp * 2 < targetMax, content, state.Seed, events);
        healer = GainRank(healer, spell.Type, WeaponRanks.PerCombat, events);
        healer = AwardMastery(healer, content, events);
        var next = state.WithUnit(healer);
        return (next.WithUnit(target with { Hp = healed }), null);
    }

    /// <summary>
    /// A kill's heal (issue 704, <see cref="KillHealEffect"/>): <paramref name="unit"/>, alive after a combat
    /// in which it killed with <paramref name="weapon"/>, heals what its abilities give, never above its max HP,
    /// with a <see cref="UnitHealed"/> when that is more than nothing.
    /// </summary>
    private static BattleUnit KillHeal(BattleUnit unit, Weapon? weapon, bool killed, GameContent content, List<GameEvent> events)
    {
        if (!killed || unit.Hp <= 0)
        {
            return unit;
        }

        var heal = AbilityRules.KillHeal(content.AbilitiesOf(unit.Unit), weapon);
        var hp = Math.Min(unit.MaxHp(content), unit.Hp + heal);
        if (hp <= unit.Hp)
        {
            return unit;
        }

        events.Add(new UnitHealed(unit.Id, hp - unit.Hp, hp));
        return unit with { Hp = hp };
    }

    /// <summary>Section 5's healer EXP through the same level-up path as combat: player units only, nothing at the cap.</summary>
    private static BattleUnit AwardHealExp(BattleUnit healer, bool targetBelowHalf, GameContent content, ulong seed, List<GameEvent> events)
    {
        if (healer.Side != Side.Player || healer.Unit.Level >= Unit.MaxLevel)
        {
            return healer;
        }

        var amount = Experience.ForHeal(targetBelowHalf);
        var result = healer.Unit.GainExp(amount, content.Class(healer.Unit.ClassId), new KeyedRng(seed));
        events.Add(new ExpGained(healer.Id, amount, result.Unit.Exp));
        var hp = healer.Hp;
        foreach (var levelUp in result.LevelUps)
        {
            events.Add(new LeveledUp(healer.Id, levelUp.NewLevel, levelUp.Gains));
            hp += levelUp.Gains.Hp;
        }

        return healer with { Unit = result.Unit, Hp = hp };
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

    /// <summary>
    /// Issue 67 for one side of a combat: a living player unit that struck with its weapon
    /// earns rank points in the weapon's type once, 5 if the combat killed and 3 otherwise.
    /// A unit that made no strike (out of range, or dead before its turn) used nothing.
    /// Enemies earn nothing, as with EXP (DECISIONS/0017).
    /// </summary>
    private static BattleUnit AwardRank(BattleUnit earner, Weapon? weapon, ValueList<StrikeEvent> strikes, bool killed, List<GameEvent> events)
    {
        if (earner.Hp == 0 || weapon is null || !strikes.Any(s => s.AttackerId == earner.Id))
        {
            return earner;
        }

        return GainRank(earner, weapon.Type, WeaponRanks.ForCombat(killed), events);
    }

    /// <summary>
    /// Issue 69 for one side of a combat, and issue 245 for a healer's accepted cast: a living
    /// player unit earns a mastery point in its class whether or not it struck, since it
    /// fought the combat, and emits
    /// <see cref="MasteryEarned"/> on reaching the class's requirement. A mastery that
    /// raises max HP raises current HP by as much, as a level-up does, and one that lowers it caps current HP at the new max. Enemies earn
    /// nothing, as with EXP and ranks (DECISIONS/0017).
    /// </summary>
    private static BattleUnit AwardMastery(BattleUnit earner, GameContent content, List<GameEvent> events)
    {
        if (earner.Side != Side.Player || earner.Hp == 0)
        {
            return earner;
        }

        var (unit, mastered) = Masteries.ForCombat(earner.Unit, content.Class(earner.Unit.ClassId));
        if (mastered is not null)
        {
            events.Add(new MasteryEarned(earner.Id, unit.ClassId, mastered));
        }

        var max = content.StatsOf(unit).Hp;
        var raised = max - earner.MaxHp(content);
        return earner with { Unit = unit, Hp = raised > 0 ? earner.Hp + raised : Math.Min(earner.Hp, max) };
    }

    /// <summary>Adds rank points to a player unit and emits <see cref="RankRaised"/> when they cross a threshold; an enemy is returned unchanged.</summary>
    private static BattleUnit GainRank(BattleUnit earner, WeaponType type, int points, List<GameEvent> events)
    {
        if (earner.Side != Side.Player)
        {
            return earner;
        }

        var before = earner.Unit.Skill.Rank(type);
        var skill = earner.Unit.Skill.Add(type, points);
        var after = skill.Rank(type);
        if (after != before)
        {
            events.Add(new RankRaised(earner.Id, type, after));
        }

        return earner with { Unit = earner.Unit with { Skill = skill } };
    }

    private static (BattleState, Rejection?) ApplyWait(BattleState state, GameContent content, Wait wait, List<GameEvent> events)
    {
        var unit = Acting(state, wait.UnitId, out var rejection);
        if (unit is null)
        {
            return (state, rejection);
        }

        var braced = Brace.BracesOnWait(state, content, unit);
        events.Add(new UnitWaited(unit.Id, braced));
        return (state.WithUnit(unit with { Moved = true, Acted = true, Braced = braced }), null);
    }

    /// <summary>
    /// DESIGN.md 13.8 (Carry the fallen, experiment): on a <c>keepsakes: on</c> map a unit
    /// that dies leaves what <see cref="Keepsake.Dropped"/> names on its tile, one
    /// <see cref="KeepsakeLeft"/> each: a player unit its weapon and any keepsake it carried,
    /// an enemy every keepsake it carried (issue 295). Nothing is left on a map without the
    /// header.
    /// </summary>
    private static BattleState LeaveKeepsake(BattleState state, BattleUnit fallen, GameContent content, List<GameEvent> events)
    {
        if (!state.Map.KeepsakesEnabled)
        {
            return state;
        }

        var dropped = Keepsake.Dropped(fallen, content);
        foreach (var keepsake in dropped)
        {
            events.Add(new KeepsakeLeft(keepsake.FallenId, keepsake.Item.ItemId, keepsake.At));
        }

        return dropped.Count == 0 ? state : state with { Keepsakes = ValueList<Keepsake>.From(state.Keepsakes.Concat(dropped)) };
    }

    /// <summary>
    /// DESIGN.md 13.12's shove: a player unit that has not acted pushes an orthogonally adjacent
    /// ally one tile straight away as its action, refused by <see cref="ShoveRefusal"/>. The
    /// pusher ends its turn; the pushed ally keeps its own Move and action, and is marked
    /// <see cref="BattleUnit.Shoved"/> so it may not exit where it lands this phase (issue 396).
    /// </summary>
    private static (BattleState, Rejection?) ApplyShove(BattleState state, GameContent content, Shove shove, List<GameEvent> events)
    {
        var unit = Acting(state, shove.UnitId, out var rejection);
        if (unit is null)
        {
            return (state, rejection);
        }

        if (state.Find(shove.TargetId) is not { } target)
        {
            return (state, new Rejection(RejectionReason.NoSuchTarget, $"no living unit '{shove.TargetId}'"));
        }

        if (ShoveRefusal(state, content, unit, target) is { } refusal)
        {
            return (state, new Rejection(RejectionReason.CannotShove, $"{unit.Id} cannot shove {target.Id}: {refusal}"));
        }

        var to = Beyond(unit.At, target.At);
        events.Add(new Shoved(unit.Id, target.Id, target.At, to));
        var next = state.WithUnit(unit with { Moved = true, Acted = true, Canto = null });
        return (next.WithUnit(target with { At = to, Shoved = true, Braced = false }), null);
    }

    /// <summary>The tile one step past <paramref name="target"/>, directly away from <paramref name="from"/>.</summary>
    public static Coord Beyond(Coord from, Coord target) =>
        new(target.X + (target.X - from.X), target.Y + (target.Y - from.Y));

    /// <summary>
    /// Why <paramref name="unit"/> may not shove <paramref name="target"/> (DESIGN.md 13.12), or null
    /// when it may: the map needs <c>shove: on</c>, the pusher must be a player unit, the target a
    /// different ally orthogonally adjacent to it, and the tile beyond on the map, passable for the
    /// target and empty. An enemy is never pushed (issue 355). The resolver and
    /// <see cref="Legal"/> share it.
    /// </summary>
    public static string? ShoveRefusal(BattleState state, GameContent content, BattleUnit unit, BattleUnit target)
    {
        if (!state.Map.ShoveEnabled)
        {
            return "this map has no shove: on header";
        }

        if (unit.Side != Side.Player)
        {
            return "only player units shove";
        }

        if (target.Side != unit.Side)
        {
            return "only allies are shoved";
        }

        if (target.Id == unit.Id || unit.At.DistanceTo(target.At) != 1)
        {
            return "it is not beside it";
        }

        var to = Beyond(unit.At, target.At);
        if (!state.Map.Contains(to))
        {
            return $"{to} is off the map";
        }

        if (!state.Map.TerrainAt(to, content).IsPassable(content.Class(target.Unit.ClassId).Movement))
        {
            return $"{target.Id} cannot stand on {to}";
        }

        if (state.UnitAt(to) is { } blocker)
        {
            return $"{blocker.Id} stands on {to}";
        }

        return null;
    }

    /// <summary>
    /// DESIGN.md 13.8's recovery: a player unit that has not acted, standing on a keepsake's
    /// tile with a free inventory slot, takes the newest keepsake of the tile's stack (issue 295) as its action, in place of Attack,
    /// Item or Wait, after its Move or without one. The stack goes to the end of the inventory
    /// under the fallen's name, and no Canto follows.
    /// </summary>
    private static (BattleState, Rejection?) ApplyRecover(BattleState state, GameContent content, Recover recover, List<GameEvent> events)
    {
        var unit = Acting(state, recover.UnitId, out var rejection);
        if (unit is null)
        {
            return (state, rejection);
        }

        if (unit.Side != Side.Player)
        {
            return (state, new Rejection(RejectionReason.NoKeepsake, $"{unit.Id} cannot recover: only player units carry the fallen"));
        }

        if (state.KeepsakeAt(unit.At) is not { } keepsake)
        {
            return (state, new Rejection(RejectionReason.NoKeepsake, $"{unit.Id} cannot recover: nothing was left at {unit.At}"));
        }

        if (unit.Unit.Inventory.IsFull)
        {
            return (state, new Rejection(RejectionReason.NoKeepsake, $"{unit.Id} cannot recover: {Referent.For(content, unit.Unit).Possessive} inventory is full"));
        }

        events.Add(new KeepsakeRecovered(unit.Id, keepsake.FallenId, keepsake.Item.ItemId));
        var carrier = unit with
        {
            Unit = unit.Unit with { Inventory = unit.Unit.Inventory.Add(keepsake.Item) },
            Moved = true,
            Acted = true,
            Canto = null,
        };
        var top = state.Keepsakes.Count - 1;
        while (state.Keepsakes[top].At != unit.At)
        {
            top--;
        }

        var next = state.WithUnit(carrier) with { Keepsakes = state.Keepsakes.RemoveAt(top) };
        return (next, null);
    }

    /// <summary>
    /// Issue 649's chest: a player unit that has not acted, on a closed chest's tile or orthogonally
    /// beside it, with no enemy on that tile, opens it as its action, in place of Attack, Item or
    /// Wait, after its Move or without one. The chest always opens (issue 679): its stacks go to the
    /// end of the opener's inventory at full uses in file order while there is room, and the rest
    /// to the state's <see cref="BattleState.Wagon"/>. The chest stays open, and no Canto follows.
    /// </summary>
    private static (BattleState, Rejection?) ApplyOpen(BattleState state, GameContent content, Open open, List<GameEvent> events)
    {
        var unit = Acting(state, open.UnitId, out var rejection);
        if (unit is null)
        {
            return (state, rejection);
        }

        if (OpenRefusal(state, unit, open.At) is { } refusal)
        {
            return (state, new Rejection(RejectionReason.CannotOpen, refusal));
        }

        var chest = state.Map.ChestAt(open.At)!;
        var inventory = unit.Unit.Inventory;
        var packed = new List<string>();
        var wagon = new List<string>();
        foreach (var stack in chest.Stacks(content))
        {
            if (inventory.Count < Inventory.Capacity)
            {
                inventory = inventory.Add(stack);
                packed.Add(stack.ItemId);
            }
            else
            {
                wagon.Add(stack.ItemId);
            }
        }

        events.Add(new ChestOpened(unit.Id, chest.At, ValueList<string>.From(packed), ValueList<string>.From(wagon)));
        var opener = unit with
        {
            Unit = unit.Unit with { Inventory = inventory },
            Moved = true,
            Acted = true,
            Canto = null,
        };
        var next = state.WithUnit(opener) with
        {
            Opened = ValueList<Coord>.From(state.Opened.Append(chest.At).Order()),
            Wagon = ValueList<string>.From(state.Wagon.Concat(wagon)),
        };
        return (next, null);
    }

    /// <summary>
    /// DESIGN.md 13.26's drop: a player unit that has not acted, on a ledge whose rock is still up,
    /// brings it down as its action, in place of Attack, Item or Wait, after its Move or without
    /// one. Every event on the ledge fires through <see cref="MapEvents.AfterDrop"/>. No Canto follows.
    /// </summary>
    private static (BattleState, Rejection?) ApplyDrop(BattleState state, GameContent content, Drop drop, List<GameEvent> events)
    {
        var unit = Acting(state, drop.UnitId, out var rejection);
        if (unit is null)
        {
            return (state, rejection);
        }

        if (Rockfall.Refusal(state, unit) is { } refusal)
        {
            return (state, new Rejection(RejectionReason.CannotDrop, refusal));
        }

        var next = state.WithUnit(unit with { Moved = true, Acted = true, Canto = null });
        next = MapEvents.AfterDrop(next, content, unit.At, events);
        return (next, null);
    }

    /// <summary>
    /// Why <paramref name="unit"/> cannot open a chest at <paramref name="at"/> (issue 649), in the
    /// order the rules are checked, or null when it can. Whether the unit may act at all is the
    /// caller's check.
    /// </summary>
    public static string? OpenRefusal(BattleState state, BattleUnit unit, Coord at)
    {
        if (unit.Side != Side.Player)
        {
            return $"{unit.Id} cannot open a chest: only player units open chests";
        }

        if (state.Map.ChestAt(at) is not { } chest)
        {
            return $"{unit.Id} cannot open a chest at {at}: there is none";
        }

        if (state.Opened.Contains(at))
        {
            return $"{unit.Id} cannot open the chest at {at}: it is already open";
        }

        if (!chest.OpensFrom(unit.At))
        {
            return $"{unit.Id} cannot open the chest at {at}: open it from its tile or one beside it";
        }

        if (state.UnitAt(at) is { Side: Side.Enemy } guard)
        {
            return $"{unit.Id} cannot open the chest at {at}: {guard.Id} stands on it";
        }

        return null;
    }

    /// <summary>
    /// Issue 269's exit: a unit that has not acted, standing on an exit tile of an Escape
    /// map, leaves the board for the state's escaped list. Since issue 377 the exit is taken
    /// without a Move, so only a unit that began its turn on the exit leaves, and whoever means
    /// to leave stands on the edge through an enemy phase first; a map with
    /// <see cref="MapDefinition.ExitAfterMove"/> keeps the older rule. When the captain leaves, every
    /// player unit still on the board is left behind, one event each in id order. An enemy
    /// on an exit tile stays where it is.
    /// </summary>
    private static (BattleState, Rejection?) ApplyExit(BattleState state, GameContent content, Exit exit, List<GameEvent> events)
    {
        var unit = Acting(state, exit.UnitId, out var rejection);
        if (unit is null)
        {
            return (state, rejection);
        }

        if (unit.Side != Side.Player)
        {
            return (state, new Rejection(RejectionReason.NotOnAnExit, $"{unit.Id} cannot exit: only player units leave through an exit"));
        }

        if (state.Map.Win != WinCondition.Escape)
        {
            return (state, new Rejection(RejectionReason.NotOnAnExit, $"{unit.Id} cannot exit: {state.Map.Name} is not an Escape map"));
        }

        if (!state.Map.IsExit(unit.At))
        {
            return (state, new Rejection(RejectionReason.NotOnAnExit, $"{unit.Id} cannot exit: {unit.At} is not an exit tile"));
        }

        if (unit.Moved && !state.Map.ExitAfterMove)
        {
            return (state, new Rejection(RejectionReason.MovedBeforeExit, $"{unit.Id} cannot exit: {Referent.For(content, unit.Unit).Subject} moved this turn; a unit exits without moving, from an exit it began its turn on"));
        }

        if (unit.Shoved && !state.Map.ExitAfterMove)
        {
            return (state, new Rejection(RejectionReason.MovedBeforeExit, $"{unit.Id} cannot exit: {Referent.For(content, unit.Unit).Subject} {Referent.For(content, unit.Unit).Verb("was", "were")} shoved this turn; a unit exits from an exit it began its turn on"));
        }

        events.Add(new UnitExited(unit.Id, unit.At));
        var next = state.WithoutUnit(unit.Id) with { Escaped = state.Escaped.Add(unit with { Moved = true, Acted = true, Canto = null }) };
        if (unit.IsCaptain)
        {
            foreach (var left in next.UnitsOf(Side.Player))
            {
                events.Add(new UnitLeftBehind(left.Id, left.At));
            }
        }

        return (next, null);
    }

    /// <summary>
    /// Issue 33's retreat through <see cref="RetreatRule"/>: the unit moves to a healing
    /// tile in its reach and ends its action, marked so it never retreats again. The path
    /// is the reach's, as for a Move.
    /// </summary>
    private static (BattleState, Rejection?) ApplyRetreat(BattleState state, GameContent content, Retreat retreat, List<GameEvent> events)
    {
        var unit = Acting(state, retreat.UnitId, out var rejection);
        if (unit is null)
        {
            return (state, rejection);
        }

        if (RetreatRule.Refusal(state, content, unit) is { } why)
        {
            return (state, new Rejection(RejectionReason.CannotRetreat, why));
        }

        if (!RetreatRule.Tiles(state, content, unit).Contains(retreat.To))
        {
            return (state, new Rejection(RejectionReason.CannotRetreat, $"{unit.Id} cannot retreat to {retreat.To}: not a healing tile it can reach this phase"));
        }

        var path = state.ReachOf(unit, content).EntryAt(retreat.To)!.Path;
        events.Add(new UnitRetreated(unit.Id, unit.At, retreat.To));
        if (retreat.To != unit.At)
        {
            events.Add(new UnitMoved(unit.Id, unit.At, retreat.To, path));
        }

        var retreated = EndMove(state, unit, unit with { At = retreat.To, Moved = true, Acted = true, Retreated = true }, events);
        return (Planks.AfterWalk(retreated, content, unit, unit.At, path, events), null);
    }

    /// <summary>
    /// Flips the phase, increments the turn after the enemy phase, clears every flag, and
    /// heals the units of the side whose phase begins that stand on healing terrain
    /// (DESIGN.md section 4): the terrain's percent of max HP, integer floor, capped at
    /// max, reported as the amount actually gained; a unit at full HP is not reported.
    /// A hungering weapon's drain follows heal and burn (DESIGN.md 13.23, <see cref="Kinsbane.AtPhaseStart"/>).
    /// A chill's clock turns (issue 702, <see cref="Frost.AtPhaseChange"/>), and a grounding's with it (issue 703).
    /// Every open mark clears (issue 772, <see cref="Opening"/>).
    /// Then the map events whose turn trigger names the phase that has begun fire, in
    /// file order (issue 32). The enemy phase of the last turn ends the battle (DESIGN.md
    /// section 7), so past the turn limit the turn still advances, which decides the outcome,
    /// but no phase begins: no <see cref="PhaseBegan"/>, no heal, no event (issue 374).
    /// </summary>
    private static (BattleState, Rejection?) ApplyEndPhase(BattleState state, GameContent content, List<GameEvent> events)
    {
        var ended = state.Phase;
        var nextPhase = ended == Side.Player ? Side.Enemy : Side.Player;
        var nextTurn = ended == Side.Enemy ? state.Turn + 1 : state.Turn;
        if (ended == Side.Player)
        {
            state = Rivalry.Accrue(state, content, events);
        }

        events.Add(new PhaseEnded(ended, state.Turn));
        if (nextTurn > state.Map.TurnLimit)
        {
            var cleared = state.Units.Select(u => u with { Moved = false, Acted = false, Canto = null, Shoved = false, Pressed = false, FallingBack = false, Open = null });
            return (state with { Phase = nextPhase, Turn = nextTurn, Units = ValueList<BattleUnit>.From(cleared), LitGroups = ValueList<string>.Empty }, null);
        }

        events.Add(new PhaseBegan(nextPhase, nextTurn));
        var units = new List<BattleUnit>(state.Units.Count);
        foreach (var unit in state.Units)
        {
            var hp = unit.Hp;
            if (unit.Side == nextPhase)
            {
                var max = unit.MaxHp(content);
                hp = Math.Min(max, hp + state.Map.TerrainAt(unit.At, content).HealFor(max));
                if (hp > unit.Hp)
                {
                    events.Add(new UnitHealed(unit.Id, hp - unit.Hp, hp));
                }

                var burnt = Math.Max(Math.Min(1, hp), hp - state.Map.TerrainAt(unit.At, content).BurnFor(max));
                if (burnt < hp)
                {
                    events.Add(new UnitBurned(unit.Id, hp - burnt, burnt));
                    hp = burnt;
                }
            }

            var spent = unit.Side == nextPhase ? (unit.Spent == 1 ? 2 : 0) : unit.Spent;
            var resting = unit.Side == nextPhase && spent == 2;
            if (resting)
            {
                events.Add(new UnitRested(unit.Id));
            }

            units.Add(unit with { Hp = hp, Moved = resting, Acted = resting, Spent = spent, Canto = null, Shoved = false, Pressed = false, FallingBack = false, Braced = unit.Braced && unit.Side != nextPhase, Watching = unit.Watching && unit.Side != nextPhase, CoveredBy = unit.Side != nextPhase ? unit.CoveredBy : null, Chill = Frost.AtPhaseChange(unit.Chill, unit.Side, ended, nextPhase), Grounded = Frost.AtPhaseChange(unit.Grounded, unit.Side, ended, nextPhase), Open = null });
        }

        var next = state with { Phase = nextPhase, Turn = nextTurn, Units = ValueList<BattleUnit>.From(units) };
        if (ended == Side.Enemy)
        {
            next = next with { LitGroups = ValueList<string>.Empty };
        }

        next = Kinsbane.AtPhaseStart(next, content, nextPhase, events);
        next = LandBlows(next, content, nextPhase, events);
        if (nextPhase == Side.Player)
        {
            next = Wildfire.Spread(next, events);
        }

        return (Hunt.AtPhaseStart(MapEvents.AtPhaseStart(next, content, events), nextPhase), null);
    }

    /// <summary>
    /// DESIGN.md 13.16's windup at a phase start, after heal and burn: each unit of
    /// <paramref name="side"/> with a raised blow, in unit order, lands it on the unit standing on
    /// its tile, of either side, for <see cref="Windup.Damage"/> (a certain hit, no crit, no
    /// counter), or lets it fall on an empty tile. The blow is spent either way. A unit the blow
    /// kills dies as in a combat, leaving its keepsake; nothing is earned for it.
    /// </summary>
    private static BattleState LandBlows(BattleState state, GameContent content, Side side, List<GameEvent> events)
    {
        if (!state.Map.WindupEnabled)
        {
            return state;
        }

        foreach (var id in state.Units.Where(u => u.Side == side && u.WindupAt is not null).Select(u => u.Id).ToList())
        {
            if (state.Find(id) is not { WindupAt: { } at } wielder)
            {
                continue;
            }

            state = state.WithUnit(wielder with { WindupAt = null });
            wielder = state.Find(id)!;
            var target = state.Units.FirstOrDefault(u => u.At == at && u.Id != id);
            if (target is null)
            {
                events.Add(new BlowFell(id, at));
                continue;
            }

            var damage = Windup.Damage(state, content, wielder, target);
            var hp = Math.Max(0, target.Hp - damage);
            events.Add(new BlowLanded(id, target.Id, at, target.Hp - hp, hp));
            var struck = target with { Hp = hp };
            state = state.WithUnit(struck);
            if (hp == 0)
            {
                events.Add(new UnitDied(target.Id, target.Side, target.At));
                state = LeaveKeepsake(state, struck, content, events).WithoutUnit(target.Id);
            }
        }

        return state;
    }

    /// <summary>
    /// Every command other than Recall and Undo that <see cref="Apply"/> would accept in a state,
    /// in a fixed order: for each unit of the acting side in id order, if it has acted, its
    /// Cantos (row-major, own tile included, issue 71) and its Fall back moves (issue 85, those
    /// that wake no one), else, for the captain while an order is open, the three orders, then its Moves
    /// (row-major, own tile excluded), its Attacks (targets in id order, per usable weapon
    /// slot when it carries more than one), its item uses
    /// (slots in order; a spell once per ally in range, allies in id order; only where
    /// something would heal), its Retreats (best tile first, issue 33), its Exit when it stands on an Escape exit (issue 269), its Recover when it stands on a keepsake with room for it (13.8), its Opens of the chests it can open (file order, issue 649), its Shoves on a <c>shove: on</c> map (targets in id order, 13.12), then Wait; then EndPhase. Empty once the battle is over.
    /// The random player of gates 2 and 8 draws from this list, so a command it picks is
    /// legal by construction.
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
                if (state.CantoReachOf(unit, content) is { } canto)
                {
                    foreach (var to in canto.Destinations)
                    {
                        yield return new Canto(unit.Id, to);
                    }
                }

                if (state.FallBackReachOf(unit, content) is { } fallBack)
                {
                    foreach (var to in fallBack.Destinations)
                    {
                        if (Resolver.Apply(state, content, new FallBack(unit.Id, to)).Accepted)
                        {
                            yield return new FallBack(unit.Id, to);
                        }
                    }
                }

                continue;
            }

            if (unit.IsCaptain && Orders.Refusal(state) is null)
            {
                foreach (var kind in new[] { OrderKind.Press, OrderKind.Rally, OrderKind.FallBack })
                {
                    yield return new Order(kind);
                }
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

            foreach (var attack in LegalAttacks(state, content, unit))
            {
                yield return attack;
            }

            foreach (var use in LegalItemUses(state, content, unit))
            {
                yield return use;
            }

            if (RetreatRule.Refusal(state, content, unit) is null)
            {
                foreach (var to in RetreatRule.Tiles(state, content, unit))
                {
                    yield return new Retreat(unit.Id, to);
                }
            }

            if (unit.Side == Side.Player && state.Map.Win == WinCondition.Escape && state.Map.IsExit(unit.At)
                && ((!unit.Moved && !unit.Shoved) || state.Map.ExitAfterMove))
            {
                yield return new Exit(unit.Id);
            }

            if (unit.Side == Side.Player && state.KeepsakeAt(unit.At) is not null && !unit.Unit.Inventory.IsFull)
            {
                yield return new Recover(unit.Id);
            }

            foreach (var chest in state.ClosedChests)
            {
                if (OpenRefusal(state, unit, chest.At) is null)
                {
                    yield return new Open(unit.Id, chest.At);
                }
            }

            if (Rockfall.Refusal(state, unit) is null)
            {
                yield return new Drop(unit.Id);
            }

            if (unit.Side == Side.Player && state.Map.ShoveEnabled)
            {
                foreach (var other in state.Units.OrderBy(u => u.Id, StringComparer.Ordinal))
                {
                    if (ShoveRefusal(state, content, unit, other) is null)
                    {
                        yield return new Shove(unit.Id, other.Id);
                    }
                }
            }

            if (state.Map.OverwatchEnabled && Overwatch.Refusal(state, content, unit) is null)
            {
                yield return new Watch(unit.Id);
            }

            if (state.Map.CoverEnabled)
            {
                foreach (var ally in state.UnitsOf(unit.Side).OrderBy(u => u.Id, StringComparer.Ordinal))
                {
                    if (CoverRule.Refusal(state, unit, ally) is null)
                    {
                        yield return new Cover(unit.Id, ally.Id);
                    }
                }
            }

            yield return new Wait(unit.Id);
        }

        yield return new EndPhase();
    }

    /// <summary>
    /// Only targets the unit's side sees count, on a dusk map (13.7, issue 318).
    /// One Attack per target in range of the equipped weapon, slot unnamed; when the unit
    /// carries a second usable weapon, one per usable slot per target in its range, slots
    /// named. Then, per slot, one per art the unit knows and may declare with that weapon
    /// (issue 68), arts in the unit's order, per target in the art's range.
    /// </summary>
    private static IEnumerable<Attack> LegalAttacks(BattleState state, GameContent content, BattleUnit unit)
    {
        var slots = new List<int>();
        for (var slot = 0; slot < unit.Unit.Inventory.Count; slot++)
        {
            if (unit.UsableWeaponAt(content, slot) is not null)
            {
                slots.Add(slot);
            }
        }

        var targets = state.UnitsOf(state.Phase == Side.Player ? Side.Enemy : Side.Player).Where(t => Dusk.Sees(state, unit.Side, t.At)).ToList();
        foreach (var slot in slots)
        {
            var weapon = unit.UsableWeaponAt(content, slot)!;
            foreach (var target in targets)
            {
                if (weapon.InRange(unit.At.DistanceTo(target.At)) && !LedgerRefuses(state, content, unit, target, slot, null))
                {
                    yield return slots.Count == 1 ? new Attack(unit.Id, target.Id) : new Attack(unit.Id, target.Id, slot);
                }
            }

            foreach (var (ability, _) in content.ArtsOf(unit.Unit))
            {
                var (art, refused) = ChooseArt(unit.WithSlotInFront(slot), content, weapon, ability.Id);
                if (refused is not null)
                {
                    continue;
                }

                var reach = art!.Apply(weapon);
                foreach (var target in targets)
                {
                    if (reach.InRange(unit.At.DistanceTo(target.At)) && !LedgerRefuses(state, content, unit, target, slot, ability.Id))
                    {
                        yield return new Attack(unit.Id, target.Id, slots.Count == 1 ? null : slot, ability.Id);
                    }
                }
            }
        }
    }

    /// <summary>Whether Ottilie's ledger (DESIGN.md 13.18) refuses this attack from where the unit stands, so <see cref="Legal"/> never lists it.</summary>
    private static bool LedgerRefuses(BattleState state, GameContent content, BattleUnit unit, BattleUnit target, int slot, string? art) =>
        Signatures.Of(state, content, unit) == SignatureKind.Ledger
        && Queries.Forecast(state, content, unit, target, slot, art) is { } forecast
        && Signatures.Refuses(state, content, unit, forecast.Attacker.DisplayedHit);

    private static IEnumerable<UseItem> LegalItemUses(BattleState state, GameContent content, BattleUnit unit)
    {
        var unitClass = content.Class(unit.Unit.ClassId);
        for (var slot = 0; slot < unit.Unit.Inventory.Count; slot++)
        {
            var stack = unit.Unit.Inventory.Items[slot];
            if (content.Items.ContainsKey(stack.ItemId))
            {
                if (unit.Hp < unit.MaxHp(content))
                {
                    yield return new UseItem(unit.Id, slot);
                }

                continue;
            }

            var spell = content.WeaponOf(unit.Unit, content.Weapon(stack.ItemId));
            if (!spell.Heals || !unit.Unit.CanWield(spell, unitClass) || stack.Uses == 0)
            {
                continue;
            }

            foreach (var ally in state.UnitsOf(unit.Side))
            {
                if (spell.InRange(unit.At.DistanceTo(ally.At)) && ally.Hp < ally.MaxHp(content))
                {
                    yield return new UseItem(unit.Id, slot, ally.Id);
                }
            }
        }
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

        var target = state.History[recall.ToIndex];
        if (target.Phase != Side.Player)
        {
            var targets = state.RecallTargets().ToList();
            var before = targets.LastOrDefault(i => i < recall.ToIndex, -1);
            var after = targets.FirstOrDefault(i => i > recall.ToIndex, -1);
            var nearest = (before, after) switch
            {
                (>= 0, >= 0) => $"; the nearest player-phase states are {before} and {after}",
                (>= 0, _) => $"; the nearest player-phase state is {before}",
                (_, >= 0) => $"; the nearest player-phase state is {after}",
                _ => "",
            };
            return new ApplyResult(state, ValueList<GameEvent>.Empty, new Rejection(
                RejectionReason.NotAPlayerPhase,
                $"state {recall.ToIndex} is inside the enemy phase of turn {target.Turn}; Recall returns only to a player phase{nearest}"));
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
