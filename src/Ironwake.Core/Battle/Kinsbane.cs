namespace Ironwake.Core;

/// <summary>
/// The hungering weapon (DESIGN.md 13.23, experiment): a weapon marked <see cref="Weapon.Hungers"/>,
/// whose state lives on its <see cref="ItemStack"/> (<see cref="ItemStack.Fed"/>,
/// <see cref="ItemStack.Starved"/>, <see cref="ItemStack.Ate"/>), so Recall restores it with the board.
/// At its wielder's phase start, after heal and burn, a weapon that fed on nothing since the last one
/// drains them <see cref="Drain"/> HP; when the drain would reach 1 they sit at 1 and the weapon
/// starves: half Mt, rounded down, uses held at 1. A kill in a combat it fought, strike or counter,
/// feeds it: one more kill, uses back to full, the wielder healed <see cref="FeedHeal"/> to max HP,
/// the starved form ended. A starved weapon that lands a hit and does not kill returns to its normal
/// form, uses back to full, and heals <see cref="EasedHeal"/>. Every <see cref="KillsPerMt"/> kills add
/// 1 Mt, to <see cref="MtCap"/>; at <see cref="AwakeAt"/> kills it wakes and neither drains, starves
/// nor heals on a feed. Its uses never fall below 1, in either form. Once fed it is bound: it is the
/// wielder's equipped weapon and no other weapon they carry strikes (<see cref="BoundSlot"/>).
/// It drains while carried by a unit on the board, equipped or not. Both sides follow the same rules;
/// only content decides who carries it.
/// </summary>
public static class Kinsbane
{
    /// <summary>The id of the scythe in <c>weapons.json</c> that a <c>kinsbane:</c> header puts in its carrier's pack.</summary>
    public const string ItemId = "kinsbane";

    /// <summary>HP lost at an unfed phase start.</summary>
    public const int Drain = 5;

    /// <summary>HP gained on a kill, to max HP.</summary>
    public const int FeedHeal = 10;

    /// <summary>HP gained when a starved weapon lands a hit that does not kill.</summary>
    public const int EasedHeal = 5;

    /// <summary>Kills for each point of Mt.</summary>
    public const int KillsPerMt = 3;

    /// <summary>The most Mt kills can add.</summary>
    public const int MtCap = 5;

    /// <summary>The kills at which the weapon wakes: the Mt cap reached.</summary>
    public const int AwakeAt = KillsPerMt * MtCap;

    /// <summary>The Mt the stack's kills add: one per <see cref="KillsPerMt"/>, to <see cref="MtCap"/>.</summary>
    public static int MtBonus(int fed) => Math.Min(MtCap, fed / KillsPerMt);

    /// <summary>Whether the stack has reached the cap and woken: no drain, no starved form, no feed heal.</summary>
    public static bool IsAwake(ItemStack stack) => stack.Fed >= AwakeAt;

    /// <summary>
    /// The weapon as it strikes from <paramref name="stack"/>: its Mt plus the kills' bonus, halved,
    /// rounded down, in the starved form. Any weapon that does not hunger is returned unchanged.
    /// </summary>
    public static Weapon Form(Weapon weapon, ItemStack stack)
    {
        if (!weapon.Hungers)
        {
            return weapon;
        }

        var mt = weapon.Mt + MtBonus(stack.Fed);
        return weapon with { Mt = stack.Starved ? mt / 2 : mt };
    }

    /// <summary>
    /// The slot of a hungering weapon that has fed at least once, which binds its wielder to it, or -1.
    /// The first such slot when, against content, a unit carries two.
    /// </summary>
    public static int BoundSlot(Unit unit, GameContent content)
    {
        for (var slot = 0; slot < unit.Inventory.Count; slot++)
        {
            var stack = unit.Inventory.Items[slot];
            if (stack.Fed > 0 && content.Weapons.TryGetValue(stack.ItemId, out var weapon) && weapon.Hungers)
            {
                return slot;
            }
        }

        return -1;
    }

    /// <summary>The slot of the first hungering weapon the unit carries, fed or not, or -1.</summary>
    public static int Slot(Unit unit, GameContent content)
    {
        for (var slot = 0; slot < unit.Inventory.Count; slot++)
        {
            if (content.Weapons.TryGetValue(unit.Inventory.Items[slot].ItemId, out var weapon) && weapon.Hungers)
            {
                return slot;
            }
        }

        return -1;
    }

    /// <summary>
    /// What the next phase start of the unit's side drains if nothing is fed before it: 0 when the
    /// weapon has fed this phase, is awake, or the unit carries none; else the HP the drain takes,
    /// never below 1.
    /// </summary>
    public static int DrainComing(BattleUnit unit, GameContent content)
    {
        var slot = Slot(unit.Unit, content);
        if (slot < 0)
        {
            return 0;
        }

        var stack = unit.Unit.Inventory.Items[slot];
        return stack.Ate || IsAwake(stack) ? 0 : Math.Max(0, Math.Min(Drain, unit.Hp - 1));
    }

    /// <summary>
    /// The HP a kill would heal the unit now, or null when it carries no hungering weapon as its
    /// equipped one. 0 when it is awake or at full HP.
    /// </summary>
    public static int? KillHeal(BattleUnit unit, GameContent content)
    {
        var slot = unit.EquippedSlot(content);
        if (slot < 0 || !content.Weapon(unit.Unit.Inventory.Items[slot].ItemId).Hungers)
        {
            return null;
        }

        return IsAwake(unit.Unit.Inventory.Items[slot]) ? 0 : Math.Min(FeedHeal, unit.MaxHp(content) - unit.Hp);
    }

    /// <summary>
    /// The drain at the start of <paramref name="side"/>'s phase, after heal and burn: for each unit of
    /// that side carrying a hungering weapon, in unit order, an unfed weapon that is not awake takes
    /// <see cref="Drain"/> HP, never below 1, and starves when the drain would reach 1; then the
    /// weapon's <see cref="ItemStack.Ate"/> is cleared for the phase to come.
    /// </summary>
    public static BattleState AtPhaseStart(BattleState state, GameContent content, Side side, List<GameEvent> events)
    {
        foreach (var unit in state.Units.Where(u => u.Side == side).ToList())
        {
            var slot = Slot(unit.Unit, content);
            if (slot < 0)
            {
                continue;
            }

            var stack = unit.Unit.Inventory.Items[slot];
            var hp = unit.Hp;
            if (!stack.Ate && !IsAwake(stack))
            {
                var starves = hp - Drain <= 1;
                var after = Math.Max(1, hp - Drain);
                var entered = starves && !stack.Starved;
                if (after < hp || entered)
                {
                    events.Add(new HungerDrained(unit.Id, stack.ItemId, hp - after, after, entered));
                }

                hp = after;
                if (starves)
                {
                    stack = stack with { Starved = true, Uses = Math.Min(stack.Uses, 1) };
                }
            }

            stack = stack with { Ate = false };
            state = state.WithUnit(unit with { Hp = hp, Unit = unit.Unit with { Inventory = unit.Unit.Inventory.Replace(slot, stack) } });
        }

        return state;
    }

    /// <summary>
    /// After a combat in which <paramref name="unitId"/> fought with <paramref name="weapon"/>: when it
    /// hungers and the unit lives, a kill feeds it (<see cref="HungerFed"/>), else a landed hit by a
    /// starved one eases it (<see cref="HungerEased"/>). The stack is the unit's equipped one, the slot
    /// it fought from.
    /// </summary>
    public static BattleState AfterCombat(BattleState state, GameContent content, string unitId, Weapon? weapon, bool killed, ValueList<StrikeEvent> strikes, List<GameEvent> events)
    {
        if (weapon is not { Hungers: true } || state.Find(unitId) is not { } unit)
        {
            return state;
        }

        var slot = unit.EquippedSlot(content);
        if (slot < 0 || !content.Weapon(unit.Unit.Inventory.Items[slot].ItemId).Hungers)
        {
            return state;
        }

        var stack = unit.Unit.Inventory.Items[slot];

        var full = content.Weapon(stack.ItemId).Durability;
        var max = unit.MaxHp(content);
        int hp;
        if (killed)
        {
            var awake = IsAwake(stack);
            hp = awake ? unit.Hp : Math.Min(max, unit.Hp + FeedHeal);
            stack = stack with { Fed = stack.Fed + 1, Ate = true, Starved = false, Uses = full };
            events.Add(new HungerFed(unit.Id, stack.ItemId, stack.Fed, MtBonus(stack.Fed), hp - unit.Hp, hp, IsAwake(stack)));
        }
        else if (stack.Starved && strikes.Any(s => s.AttackerId == unitId && s.Hit))
        {
            hp = Math.Min(max, unit.Hp + EasedHeal);
            stack = stack with { Starved = false, Uses = full };
            events.Add(new HungerEased(unit.Id, stack.ItemId, hp - unit.Hp, hp));
        }
        else
        {
            return state;
        }

        return state.WithUnit(unit with { Hp = hp, Unit = unit.Unit with { Inventory = unit.Unit.Inventory.Replace(slot, stack) } });
    }
}
