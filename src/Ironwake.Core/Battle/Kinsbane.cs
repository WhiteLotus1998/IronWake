namespace Ironwake.Core;

/// <summary>
/// The hungering weapon (DESIGN.md 13.23, experiment, issue 645): a weapon marked
/// <see cref="Weapon.Hungers"/> keeps a feed count and a starved form on its stack
/// (<see cref="ItemStack.Fed"/>, <see cref="ItemStack.Starved"/>). The rule is the weapon's, on any
/// map, for whoever carries it, equipped or not:
/// <list type="bullet">
/// <item>The drain: at each of its carrier's side's phase starts after the first, if the carrier
/// fed it nothing since the last one (<see cref="BattleUnit.HasFed"/>), the carrier loses
/// <see cref="Drain"/> HP, never below 1. A drain that would take the carrier to 1 or below sets
/// it at 1 and starves the weapon.</item>
/// <item>The feed: a kill by its carrier with it, a counter-kill included, counts one, heals the
/// carrier <see cref="FeedHeal"/> (to max HP), ends the starved form and restores its uses.</item>
/// <item>The growth: <see cref="KillsPerMt"/> kills buy +1 Mt, to <see cref="MtCap"/>.</item>
/// <item>The starved form: half Mt rounded down, uses held at 1. Any hit it lands that kills nothing
/// ends the form, restores its uses and heals <see cref="EasedHeal"/>.</item>
/// <item>The uses: a hungering weapon never spends below 1, so it never breaks; at 1 a strike spends nothing.</item>
/// <item>The cap: at <see cref="WakeKills"/> fed it wakes and neither drains, starves nor heals;
/// a kill still counts and restores its uses.</item>
/// </list>
/// Everything is board state, so Recall restores it with the board.
/// </summary>
public static class Kinsbane
{
    /// <summary>The hungering weapon's id in <c>weapons.json</c>, which a map's <c>kinsbane:</c> header issues.</summary>
    public const string ItemId = "kinsbane";

    /// <summary>HP an unfed carrier loses at its phase start.</summary>
    public const int Drain = 5;

    /// <summary>HP a kill heals the carrier, to max HP.</summary>
    public const int FeedHeal = 10;

    /// <summary>HP a starved weapon's non-lethal hit heals the carrier as it leaves the starved form.</summary>
    public const int EasedHeal = 5;

    /// <summary>Kills per +1 Mt.</summary>
    public const int KillsPerMt = 3;

    /// <summary>The most Mt it grows.</summary>
    public const int MtCap = 5;

    /// <summary>The feed count at which it wakes: the kill that buys the last Mt.</summary>
    public const int WakeKills = KillsPerMt * MtCap;

    /// <summary>The Mt a weapon fed <paramref name="fed"/> times has grown.</summary>
    public static int MtBonus(int fed) => Math.Min(MtCap, Math.Max(0, fed) / KillsPerMt);

    /// <summary>
    /// The teeth on the blade (issue 804): one hooked tooth grows with each Mt step, so the count is
    /// <see cref="MtBonus"/>, zero to <see cref="MtCap"/>, and at the last it wakes. The card and the
    /// feed line print it as <c>teeth n/5</c>, the progress read on the weapon itself.
    /// </summary>
    public static int Teeth(int fed) => MtBonus(fed);

    /// <summary>Whether the kill that took the count to <paramref name="fed"/> grew a tooth.</summary>
    public static bool ToothGrew(int fed) => fed > 0 && Teeth(fed) > Teeth(fed - 1);

    /// <summary>Whether a weapon fed <paramref name="fed"/> times has woken: no drain, no starved form, no feed heal.</summary>
    public static bool Woken(int fed) => fed >= WakeKills;

    /// <summary>
    /// The weapon as its stack makes it: a hungering weapon at its grown Mt, halved (rounded down)
    /// in the starved form. Any other weapon unchanged.
    /// </summary>
    public static Weapon Shape(Weapon weapon, ItemStack stack)
    {
        if (!weapon.Hungers || (stack.Fed == 0 && !stack.Starved))
        {
            return weapon;
        }

        var mt = weapon.Mt + MtBonus(stack.Fed);
        return weapon with { Mt = stack.Starved ? mt / 2 : mt };
    }

    /// <summary>The first slot holding a hungering weapon, or -1.</summary>
    public static int Slot(BattleUnit unit, GameContent content)
    {
        for (var slot = 0; slot < unit.Unit.Inventory.Count; slot++)
        {
            if (content.Weapons.TryGetValue(unit.Unit.Inventory.Items[slot].ItemId, out var weapon) && weapon.Hungers)
            {
                return slot;
            }
        }

        return -1;
    }

    /// <summary>
    /// What the drain will do to <paramref name="unit"/> at its side's next phase start if it feeds
    /// on nothing before then: the HP it loses and whether the weapon starves. Null when the unit
    /// carries no hungering weapon, has fed it since its last phase start, or carries one woken.
    /// </summary>
    public static (int Amount, bool Starves)? Coming(BattleUnit unit, GameContent content)
    {
        var slot = Slot(unit, content);
        if (slot < 0 || unit.HasFed || Woken(unit.Unit.Inventory.Items[slot].Fed))
        {
            return null;
        }

        var after = unit.Hp - Drain;
        return after <= 1 ? (Math.Max(0, unit.Hp - 1), true) : (Drain, false);
    }

    /// <summary>
    /// The drain at the start of <paramref name="side"/>'s phase, after heal and burn: each unit of
    /// the side carrying a hungering weapon pays <see cref="Coming"/> unless this is the side's
    /// first phase (turn 1), then every such unit's <see cref="BattleUnit.HasFed"/> clears, so the
    /// next drain reads only kills from here on.
    /// </summary>
    public static BattleState AtPhaseStart(BattleState state, GameContent content, Side side, List<GameEvent> events)
    {
        foreach (var unit in state.UnitsOf(side).ToList())
        {
            var slot = Slot(unit, content);
            if (slot < 0)
            {
                continue;
            }

            var next = unit with { HasFed = false };
            var stack = unit.Unit.Inventory.Items[slot];
            if (state.Turn > 1 && Coming(unit, content) is var (amount, starves) && (amount > 0 || !stack.Starved))
            {
                var hp = unit.Hp - amount;
                events.Add(new HungerDrained(unit.Id, stack.ItemId, amount, hp, starves));
                next = next with { Hp = hp };
                if (starves && !stack.Starved)
                {
                    next = WithStack(next, slot, stack with { Starved = true, Uses = 1 });
                }
            }

            state = state.WithUnit(next);
        }

        return state;
    }

    /// <summary>
    /// After a combat in which <paramref name="unit"/> (alive, as the combat left it) struck with
    /// a hungering weapon equipped: a kill feeds it (<see cref="HungerFed"/>); else, in the starved
    /// form, a hit it landed eases it (<see cref="HungerEased"/>). Any other unit is unchanged.
    /// </summary>
    public static BattleUnit AfterCombat(BattleUnit unit, GameContent content, ValueList<StrikeEvent> strikes, bool killed, List<GameEvent> events)
    {
        var slot = unit.EquippedSlot(content);
        if (slot < 0 || unit.Hp <= 0)
        {
            return unit;
        }

        var stack = unit.Unit.Inventory.Items[slot];
        var weapon = content.Weapon(stack.ItemId);
        if (!weapon.Hungers)
        {
            return unit;
        }

        var max = unit.MaxHp(content);
        if (killed)
        {
            var fed = stack.Fed + 1;
            var hp = Woken(stack.Fed) ? unit.Hp : Math.Min(max, unit.Hp + FeedHeal);
            events.Add(new HungerFed(unit.Id, stack.ItemId, fed, hp - unit.Hp, hp, MtBonus(fed), Woken(fed) && !Woken(stack.Fed)));
            return WithStack(unit with { Hp = hp, HasFed = true }, slot, stack with { Fed = fed, Starved = false, Uses = weapon.Durability });
        }

        if (stack.Starved && strikes.Any(s => s.AttackerId == unit.Id && s.Hit))
        {
            var hp = Math.Min(max, unit.Hp + EasedHeal);
            events.Add(new HungerEased(unit.Id, stack.ItemId, hp - unit.Hp, hp));
            return WithStack(unit with { Hp = hp }, slot, stack with { Starved = false, Uses = weapon.Durability });
        }

        return unit;
    }

    /// <summary>
    /// The unit card's line for a carrier (DESIGN.md 13.23): the weapon, its feed count, its teeth and growth,
    /// then its state: woken, starved, fed since the last phase start, or the drain coming at the
    /// next one. Null for a unit carrying no hungering weapon.
    /// </summary>
    public static string? Card(BattleUnit unit, GameContent content)
    {
        var slot = Slot(unit, content);
        if (slot < 0)
        {
            return null;
        }

        var stack = unit.Unit.Inventory.Items[slot];
        var head = $"{content.ItemName(stack.ItemId)}: fed {stack.Fed}, teeth {Teeth(stack.Fed)}/{MtCap}. Power +{MtBonus(stack.Fed)}.";
        var state = Woken(stack.Fed) ? "Woken: no drain."
            : stack.Starved ? "Starved: half Power, uses 1; any hit eases it (+" + EasedHeal + " HP), a kill feeds it."
            : unit.HasFed ? "Fed this phase."
            : Coming(unit, content) is var (amount, starves) ? $"Hungry: -{amount} HP at the next phase start" + (starves ? ", and it starves." : ".")
            : "";
        return $"{head} {state}".TrimEnd();
    }

    /// <summary>
    /// The forecast's lines for <paramref name="unit"/> fighting with <paramref name="weapon"/>
    /// equipped from <paramref name="stack"/> (DESIGN.md 13.23): <c>kill: +10 HP</c> when a kill
    /// would heal it, and in the starved form the hit that eases it. Empty for any other weapon
    /// and for one woken.
    /// </summary>
    public static IEnumerable<string> ForecastLines(BattleUnit unit, GameContent content, Weapon? weapon, ItemStack stack, string name)
    {
        if (weapon is not { Hungers: true } || Woken(stack.Fed))
        {
            yield break;
        }

        var max = unit.MaxHp(content);
        yield return $"  kill: {name} +{FeedHeal} HP, to max {max} ({content.ItemName(stack.ItemId)} feeds, fed {stack.Fed + 1})";
        if (stack.Starved)
        {
            yield return $"  hit: {name} +{EasedHeal} HP, the starved form ends";
        }
    }

    private static BattleUnit WithStack(BattleUnit unit, int slot, ItemStack stack) =>
        unit with { Unit = unit.Unit with { Inventory = unit.Unit.Inventory.Replace(slot, stack) } };
}
