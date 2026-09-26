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
                if (rejection is null)
                {
                    next = MapEvents.AfterMove(next, content, next.Find(move.UnitId)!, events);
                }

                break;
            case Attack attack:
                (next, rejection) = ApplyAttack(state, content, attack, events);
                break;
            case UseItem item:
                (next, rejection) = ApplyUseItem(state, content, item, events);
                break;
            case Wait wait:
                (next, rejection) = ApplyWait(state, wait, events);
                break;
            case Exit exit:
                (next, rejection) = ApplyExit(state, exit, events);
                break;
            case Recover recover:
                (next, rejection) = ApplyRecover(state, recover, events);
                break;
            case Canto canto:
                (next, rejection) = ApplyCanto(state, content, canto, events);
                if (rejection is null)
                {
                    next = MapEvents.AfterMove(next, content, next.Find(canto.UnitId)!, events);
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
            next = OpenCanto(next, content, acted);
        }

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
    /// group per command.
    /// </summary>
    private static BattleState WakeGroups(BattleState before, BattleState after, GameContent content, List<GameEvent> events)
    {
        var died = new List<string>();
        var noisy = new List<Coord>();
        foreach (var e in events)
        {
            switch (e)
            {
                case UnitDied dead when before.Find(dead.UnitId) is { Group: { } group }:
                    died.Add(group);
                    break;
                case CombatFought fought:
                    noisy.Add(before.Find(fought.AttackerId)!.At);
                    noisy.Add(before.Find(fought.TargetId)!.At);
                    break;
            }
        }

        var next = after;
        foreach (var woke in WakeCheck.Run(before, after, content, noisy, died))
        {
            events.Add(woke);
            next = next.Wake(woke.Group);
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
        int? canto = AbilityRules.HasCanto(content.AbilitiesOf(unit.Unit)) ? reach.Mov - entry.Cost : null;
        return (EndMove(state, unit, unit with { At = move.To, Moved = true, Canto = canto }, events), null);
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
    /// full Mov when it acted without moving. A unit without Canto is left as it is.
    /// </summary>
    private static BattleState OpenCanto(BattleState state, GameContent content, string unitId)
    {
        if (state.Find(unitId) is not { } unit || !AbilityRules.HasCanto(content.AbilitiesOf(unit.Unit)))
        {
            return state;
        }

        return unit.Canto is null
            ? state.WithUnit(unit with { Canto = content.Class(unit.Unit.ClassId).Mov })
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
            var why = !AbilityRules.HasCanto(content.AbilitiesOf(unit.Unit)) ? "it has no Canto"
                : !unit.Acted ? "it has not acted yet this phase"
                : "its Canto is spent this phase";
            return (state, new Rejection(RejectionReason.NoCanto, $"{unit.Id} cannot Canto: {why}"));
        }

        var entry = reach.EntryAt(canto.To);
        if (entry is not { CanEnd: true })
        {
            var why = !state.Map.Contains(canto.To) ? "outside the map"
                : entry is null ? $"not within the {reach.Mov} movement its Canto has left from {unit.At}"
                : "occupied by an ally";
            return (state, new Rejection(RejectionReason.OutOfReach, $"{unit.Id} cannot Canto to {canto.To}: {why}"));
        }

        events.Add(new Cantoed(unit.Id, unit.At, canto.To, entry.Path));
        return (state.WithUnit(unit with { At = canto.To, Canto = null }), null);
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

        var defenderWeapon = target.EquippedWeapon(content);
        var result = CombatResolver.Resolve(
            unit.ToCombatant(state, content, art: art),
            target.ToCombatant(state, content, countering: true),
            distance,
            new CombatContext(state.Turn, state.Phase),
            new KeyedRng(state.Seed),
            state.Scheme);
        events.Add(new CombatFought(unit.Id, target.Id, state.Turn, state.Phase, result.Strikes, result.AttackerHp, result.DefenderHp));

        var attackerAfter = SpendDurability(unit with { Hp = result.AttackerHp, Moved = true, Acted = true }, result.Strikes, content, events, art?.Cost ?? 0);
        var targetAfter = SpendDurability(target with { Hp = result.DefenderHp }, result.Strikes, content, events);
        attackerAfter = AwardExp(attackerAfter, targetAfter, result.Strikes, result.DefenderDied, content, state.Seed, events);
        targetAfter = AwardExp(targetAfter, attackerAfter, result.Strikes, result.AttackerDied, content, state.Seed, events);
        attackerAfter = AwardRank(attackerAfter, weapon, result.Strikes, result.DefenderDied, events);
        targetAfter = AwardRank(targetAfter, defenderWeapon, result.Strikes, result.AttackerDied, events);
        attackerAfter = AwardMastery(attackerAfter, content, events);
        targetAfter = AwardMastery(targetAfter, content, events);
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

        return (next, null);
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
    /// of the art's type, at a rank the unit has reached, not broken, and holding at least
    /// the art's cost and the first strike's use (<see cref="RejectionReason.ArtRefused"/>).
    /// <paramref name="unit"/> is the unit with <paramref name="weapon"/> already equipped.
    /// </summary>
    public static (CombatArtEffect? Art, Rejection? Rejection) ChooseArt(BattleUnit unit, GameContent content, Weapon weapon, string artId)
    {
        var known = content.ArtsOf(unit.Unit).FirstOrDefault(a => a.Ability.Id == artId);
        if (known.Art is null)
        {
            return (null, new Rejection(RejectionReason.NoSuchArt, $"{unit.Id} knows no art '{artId}'"));
        }

        var (ability, art) = known;
        var uses = unit.Unit.Inventory.Items[unit.EquippedSlot(content)].Uses;
        var why = weapon.Type != art.Weapon ? $"{ability.Name} is a {Lower(art.Weapon)} art and {weapon.Name} is a {Lower(weapon.Type)}"
            : unit.Unit.Skill.Rank(art.Weapon) < art.Rank ? $"rank {unit.Unit.Skill.Rank(art.Weapon)} in {Lower(art.Weapon)}, and {ability.Name} needs {art.Rank}"
            : uses == 0 ? $"{weapon.Name} is broken and cannot pay for an art"
            : uses < art.UsesNeeded ? $"{ability.Name} costs {art.UsesNeeded} uses with the strike and {weapon.Name} has {uses} left"
            : null;
        return why is null ? (art, null) : (null, new Rejection(RejectionReason.ArtRefused, $"{unit.Id} cannot use {ability.Name}: {why}"));

        static string Lower(WeaponType type) => type.ToString().ToLowerInvariant();
    }

    /// <summary>The refusal's reason when a unit's rank is below a weapon's (issue 67): the unit's rank in the type and the rank the weapon needs.</summary>
    public static string RankShort(Unit unit, Weapon weapon) =>
        $"rank {unit.Skill.Rank(weapon.Type)} in {weapon.Type.ToString().ToLowerInvariant()}, and {weapon.Name} needs {weapon.Rank}";

    /// <summary>
    /// Section 5's durability: every strike a unit made in the combat, landed or not,
    /// spends one use of the weapon it struck with, never below zero, and a declared art's
    /// <paramref name="artCost"/> is spent with them, hit or miss (issue 68). The strike that
    /// empties a physical weapon emits <see cref="WeaponBroke"/> and the weapon stays,
    /// broken; the one that empties a spell emits <see cref="SpellSpent"/>. A gauntlet spends
    /// one use for the combat however many strikes it made (issue 70).
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

        var left = Math.Max(0, stack.Uses - made);
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

        var spell = content.Weapon(stack.ItemId);
        if (!spell.Heals || !content.Class(unit.Unit.ClassId).CanUse(spell.Type))
        {
            return (state, new Rejection(RejectionReason.NotUsable, $"{spell.Name} is a weapon, not an item; attack with it"));
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
    /// DESIGN.md 13.8's recovery: a player unit that has not acted, standing on a keepsake's
    /// tile with a free inventory slot, takes the newest keepsake of the tile's stack (issue 295) as its action, in place of Attack,
    /// Item or Wait, after its Move or without one. The stack goes to the end of the inventory
    /// under the fallen's name, and no Canto follows.
    /// </summary>
    private static (BattleState, Rejection?) ApplyRecover(BattleState state, Recover recover, List<GameEvent> events)
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
            return (state, new Rejection(RejectionReason.NoKeepsake, $"{unit.Id} cannot recover: its inventory is full"));
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
    /// Issue 269's exit: a unit that has not acted, standing on an exit tile of an Escape
    /// map, leaves the board for the state's escaped list. When the captain leaves, every
    /// player unit still on the board is left behind, one event each in id order. An enemy
    /// on an exit tile stays where it is.
    /// </summary>
    private static (BattleState, Rejection?) ApplyExit(BattleState state, Exit exit, List<GameEvent> events)
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

        return (EndMove(state, unit, unit with { At = retreat.To, Moved = true, Acted = true, Retreated = true }, events), null);
    }

    /// <summary>
    /// Flips the phase, increments the turn after the enemy phase, clears every flag, and
    /// heals the units of the side whose phase begins that stand on healing terrain
    /// (DESIGN.md section 4): the terrain's percent of max HP, integer floor, capped at
    /// max, reported as the amount actually gained; a unit at full HP is not reported.
    /// Then the map events whose turn trigger names the phase that has begun fire, in
    /// file order (issue 32).
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
            }

            units.Add(unit with { Hp = hp, Moved = false, Acted = false, Canto = null });
        }

        var next = state with { Phase = nextPhase, Turn = nextTurn, Units = ValueList<BattleUnit>.From(units) };
        return (MapEvents.AtPhaseStart(next, content, events), null);
    }

    /// <summary>
    /// Every command other than Recall that <see cref="Apply"/> would accept in a state,
    /// in a fixed order: for each unit of the acting side in id order, if it has acted, its
    /// Cantos (row-major, own tile included, issue 71), else its Moves
    /// (row-major, own tile excluded), its Attacks (targets in id order, per usable weapon
    /// slot when it carries more than one), its item uses
    /// (slots in order; a spell once per ally in range, allies in id order; only where
    /// something would heal), its Retreats (best tile first, issue 33), its Exit when it stands on an Escape exit (issue 269), its Recover when it stands on a keepsake with room for it (13.8), then Wait; then EndPhase. Empty once the battle is over.
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

            if (unit.Side == Side.Player && state.Map.Win == WinCondition.Escape && state.Map.IsExit(unit.At))
            {
                yield return new Exit(unit.Id);
            }

            if (unit.Side == Side.Player && state.KeepsakeAt(unit.At) is not null && !unit.Unit.Inventory.IsFull)
            {
                yield return new Recover(unit.Id);
            }

            yield return new Wait(unit.Id);
        }

        yield return new EndPhase();
    }

    /// <summary>
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

        var targets = state.UnitsOf(state.Phase == Side.Player ? Side.Enemy : Side.Player).ToList();
        foreach (var slot in slots)
        {
            var weapon = unit.UsableWeaponAt(content, slot)!;
            foreach (var target in targets)
            {
                if (weapon.InRange(unit.At.DistanceTo(target.At)))
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
                    if (reach.InRange(unit.At.DistanceTo(target.At)))
                    {
                        yield return new Attack(unit.Id, target.Id, slots.Count == 1 ? null : slot, ability.Id);
                    }
                }
            }
        }
    }

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

            var spell = content.Weapon(stack.ItemId);
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
