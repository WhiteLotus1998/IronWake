using System.Collections.Immutable;

namespace Ironwake.Core;

/// <summary>
/// The hungering weapon (DESIGN.md 13.23, experiment, issue 645): a weapon marked
/// <see cref="Weapon.Hungers"/> keeps a feed count and a starved form on its stack
/// (<see cref="ItemStack.Fed"/>, <see cref="ItemStack.Starved"/>). The rule is the weapon's, on any
/// map, for whoever carries it, equipped or not:
/// <list type="bullet">
/// <item>The drain: at each of its carrier's side's phase starts after the first, with an enemy near
/// or not (issue 871, reverting 0210's reach gate at Lotus's wish), if the carrier
/// fed it nothing since the last one (<see cref="BattleUnit.HasFed"/>), the carrier loses
/// <see cref="Drain"/> HP, never below 1. A drain that would take the carrier to 1 or below sets
/// it at 1 and starves the weapon.</item>
/// <item>The feed: a kill by its carrier with it, a counter-kill included, counts one, heals the
/// carrier <see cref="FeedHeal"/> (to max HP), ends the starved form and restores its uses.</item>
/// <item>The growth: each tooth of <see cref="ToothAt"/> buys +1 Mt, to <see cref="MtCap"/>.</item>
/// <item>The starved form: half Mt rounded down, uses held at 1. Any hit it lands that kills nothing
/// ends the form, restores its uses and heals <see cref="EasedHeal"/>.</item>
/// <item>The uses: a hungering weapon never spends below 1, so it never breaks; at 1 a strike spends nothing.</item>
/// <item>The cap: at <see cref="WakeKills"/> fed it wakes and neither drains, starves nor heals;
/// a kill still counts and restores its uses.</item>
/// <item>The hunt runs on (issue 804, round 251): once a battle, a kill with it on its carrier's own
/// Attack that leaves it woken, the waking kill included, gives the carrier its full Move again as a
/// Canto, with no second strike (<see cref="RunsOn"/>).</item>
/// <item>The voice (issue 804 item 3): it speaks to its carrier when a drain starves it, when a kill
/// grows a tooth, and on the kill that wakes it, at most <see cref="VoiceCap"/> lines a battle
/// (<see cref="Speak"/>).</item>
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

    /// <summary>The most lines a hungering weapon says to its carrier in one battle (issue 804 item 3).</summary>
    public const int VoiceCap = 3;

    /// <summary>HP a starved weapon's non-lethal hit heals the carrier as it leaves the starved form.</summary>
    public const int EasedHeal = 5;

    /// <summary>
    /// The feed count at which each tooth grows, one Mt step each (round 263's clause for a field
    /// median under 13, taken in round 279; issue 856): steps of 2, 2, 1, 3 and 4 kills, front-loaded so
    /// a claimant who joins at the raid can wake it by the field or the keep. The last entry is the waking.
    /// </summary>
    public static readonly ImmutableArray<int> ToothAt = ImmutableArray.Create(2, 4, 5, 8, 12);

    /// <summary>The most Mt it grows: one per tooth.</summary>
    public static int MtCap => ToothAt.Length;

    /// <summary>The feed count at which it wakes: the kill that grows the last tooth.</summary>
    public static int WakeKills => ToothAt[^1];

    /// <summary>The feed count at which tooth <paramref name="tooth"/> (1 to <see cref="MtCap"/>) grows.</summary>
    public static int FedFor(int tooth) => tooth < 1 || tooth > MtCap
        ? throw new ArgumentOutOfRangeException(nameof(tooth), tooth, $"a tooth is 1 to {MtCap}")
        : ToothAt[tooth - 1];

    /// <summary>The Mt a weapon fed <paramref name="fed"/> times has grown: the teeth on its blade.</summary>
    public static int MtBonus(int fed) => ToothAt.Count(at => fed >= at);

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

    /// <summary>
    /// The unit a <c>keziah_warning</c> map warns about (issue 871): whoever the hungering weapon is
    /// bound to (<see cref="Weapon.BoundTo"/>), or null when the content has no such weapon or binding.
    /// </summary>
    public static string? Bearer(GameContent content) =>
        content.Weapons.TryGetValue(ItemId, out var weapon) ? weapon.BoundTo : null;

    /// <summary>
    /// Lotus's line (issue 871), printed verbatim with the bearer's name when a campaign marches
    /// onto a <c>keziah_warning</c> map with the bearer deployed.
    /// </summary>
    public static string WarningLine(string name) =>
        $"This map is not ideal for {name}. Are you sure you want to continue with her?";

    /// <summary>
    /// Why <paramref name="map"/> may not carry <c>keziah_warning: on</c> (issue 871), or null when it
    /// may: a map that places the bearer by name or fields the whole company (<c>deploy: all</c>)
    /// leaves no choice to warn about, and neither does the bearer's own side map
    /// (<paramref name="questMember"/>, the side map's member, null for a main map).
    /// </summary>
    public static string? WarningRefusal(MapDefinition map, GameContent content, string? questMember = null)
    {
        if (!map.KeziahWarning || Bearer(content) is not { } bearer)
        {
            return null;
        }

        if (map.DeploysAll)
        {
            return "keziah_warning: on is refused on a deploy: all map, which leaves nobody to bench";
        }

        if (map.Placements.Any(p => p is PlayerPlacement { Slot: PlayerSlot.NamedRecruit } named && named.RecruitId == bearer))
        {
            return $"keziah_warning: on is refused on a map that places '{bearer}' by name";
        }

        return questMember == bearer ? $"keziah_warning: on is refused on '{bearer}''s own side map" : null;
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
    /// Whether some unit of the other side stands on a tile <paramref name="unit"/> could strike next
    /// phase, the strike set <c>threat</c> and both planners read (<see cref="Threat.StruckByUnit"/>:
    /// its move plus every usable weapon's range). A measurement only (issue 871): 0210 gated the
    /// drain on it and that gate is reverted, so the Sim reads it to count the drains paid with no
    /// enemy in reach, the walking tax a <c>keziah_warning</c> map warns of. It is never a rule.
    /// </summary>
    public static bool Smells(BattleState state, GameContent content, BattleUnit unit)
    {
        var struck = Threat.StruckByUnit(state, content, unit);
        return state.UnitsOf(unit.Side == Side.Player ? Side.Enemy : Side.Player).Any(other => struck.Contains(other.At));
    }

    /// <summary>
    /// The drain at the start of <paramref name="side"/>'s phase, after heal and burn: each unit of
    /// the side carrying a hungering weapon pays <see cref="Coming"/> unless this is the side's
    /// first phase (turn 1), then every such unit's <see cref="BattleUnit.HasFed"/> clears, so the
    /// next drain reads only kills from here on. A skipped drain emits nothing.
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
                    var voice = content.Weapon(stack.ItemId).Voice;
                    next = Speak(next, stack.ItemId, voice?.Starved, state.Turn, events);
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
            var woke = Woken(fed) && !Woken(stack.Fed);
            events.Add(new HungerFed(unit.Id, stack.ItemId, fed, hp - unit.Hp, hp, MtBonus(fed), woke));
            var fedUnit = WithStack(unit with { Hp = hp, HasFed = true }, slot, stack with { Fed = fed, Starved = false, Uses = weapon.Durability });
            return woke ? Speak(fedUnit, stack.ItemId, weapon.Voice?.Woken, 0, events)
                : ToothGrew(fed) ? Speak(fedUnit, stack.ItemId, weapon.Voice?.Tooth, Teeth(fed) - 1, events)
                : fedUnit;
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
        var state = Woken(stack.Fed) ? "Woken: no drain. " + (unit.HuntRan ? "The hunt has run this map." : "A kill: move again (once a map).")
            : stack.Starved ? "Starved: half Power, uses 1; any hit eases it (+" + EasedHeal + " HP), a kill feeds it."
            : unit.HasFed ? "Fed this phase."
            : Coming(unit, content) is var (amount, starves) ? $"Hungry: -{amount} HP at the next phase start" + (starves ? ", and it starves." : ".")
            : "";
        return $"{head} {state}".TrimEnd();
    }

    /// <summary>
    /// The forecast's lines for <paramref name="unit"/> fighting with <paramref name="weapon"/>
    /// equipped from <paramref name="stack"/> (DESIGN.md 13.23): <c>kill: +10 HP</c> when a kill
    /// would heal it, and in the starved form the hit that eases it; for the striker on its own
    /// phase (<paramref name="huntMov"/> set, the Move <see cref="HuntMov"/> gives), the hunt running
    /// on when the kill would leave it woken with the charge unspent (issue 804). Empty for any other weapon.
    /// </summary>
    public static IEnumerable<string> ForecastLines(BattleUnit unit, GameContent content, Weapon? weapon, ItemStack stack, string name, int? huntMov = null)
    {
        if (weapon is not { Hungers: true })
        {
            yield break;
        }

        if (!Woken(stack.Fed))
        {
            var max = unit.MaxHp(content);
            yield return $"  kill: {name} +{FeedHeal} HP, to max {max} ({content.ItemName(stack.ItemId)} feeds, fed {stack.Fed + 1})";
            if (stack.Starved)
            {
                yield return $"  hit: {name} +{EasedHeal} HP, the starved form ends";
            }
        }

        if (huntMov is { } mov && WouldRunOn(unit, stack))
        {
            yield return $"  kill: {name} moves again, {mov} movement (the hunt runs on, once a map)";
        }
    }

    /// <summary>
    /// The Mov the hunt gives back (issue 804): the carrier's full Move this phase as
    /// <see cref="BattleState.ReachOf"/> reads it (a Press counted, the chill taken off, 0 while locked).
    /// </summary>
    public static int HuntMov(BattleState state, GameContent content, BattleUnit unit) =>
        Lock.Holds(state, unit) ? 0 : Frost.Mov(content.Class(unit.Unit.ClassId).Mov + (unit.Pressed ? 1 : 0), unit);

    /// <summary>
    /// Whether a kill now by <paramref name="unit"/> with the hungering weapon on <paramref name="stack"/>
    /// would run the hunt on (issue 804): the kill leaves it woken and the unit has not run it this battle.
    /// </summary>
    public static bool WouldRunOn(BattleUnit unit, ItemStack stack) => !unit.HuntRan && Woken(stack.Fed + 1);

    /// <summary>
    /// The hunt runs on (issue 804, round 251; DESIGN.md 13.23): after <paramref name="unitId"/>'s own
    /// Attack, if <paramref name="events"/> hold a feed of its weapon that left it woken and the unit
    /// has not run the hunt this battle, the unit is owed a Canto of its full Move
    /// (<see cref="HuntMov"/>; never less than a Canto it was already owed), the charge is spent
    /// (<see cref="BattleUnit.HuntRan"/>) and <see cref="HuntRanOn"/> is emitted. The Canto moves
    /// only, so there is no second strike. Any other command, a counter-kill, or a dead carrier: unchanged.
    /// </summary>
    public static BattleState RunsOn(BattleState state, GameContent content, string unitId, List<GameEvent> events)
    {
        if (state.Find(unitId) is not { } unit || unit.HuntRan || unit.Side != state.Phase
            || !events.OfType<HungerFed>().Any(f => f.UnitId == unitId && Woken(f.Fed)))
        {
            return state;
        }

        var mov = Math.Max(HuntMov(state, content, unit), unit.Canto ?? 0);
        events.Add(new HuntRanOn(unitId, mov));
        return state.WithUnit(unit with { Canto = mov, HuntRan = true });
    }

    /// <summary>
    /// The voice (issue 804 item 3): the line of <paramref name="register"/> at <paramref name="pick"/>
    /// (wrapped to the register's length) said to <paramref name="unit"/> as <see cref="KinsbaneSpoke"/>,
    /// its <c>{name}</c> the carrier's name, and the unit's <see cref="BattleUnit.VoiceSpoken"/> counted.
    /// Silent, the unit unchanged, when the register is missing or empty or the battle's
    /// <see cref="VoiceCap"/> lines are spent. The pick is a number of the board (a tooth, a turn), never a roll.
    /// </summary>
    public static BattleUnit Speak(BattleUnit unit, string itemId, ValueList<VoiceLine>? register, int pick, List<GameEvent> events)
    {
        if (register is not { Count: > 0 } lines || unit.VoiceSpoken >= VoiceCap)
        {
            return unit;
        }

        var line = lines[((pick % lines.Count) + lines.Count) % lines.Count];
        events.Add(new KinsbaneSpoke(unit.Id, itemId, line.Id, line.Text.Replace("{name}", unit.Unit.Name, StringComparison.Ordinal)));
        return unit with { VoiceSpoken = unit.VoiceSpoken + 1 };
    }

    private static BattleUnit WithStack(BattleUnit unit, int slot, ItemStack stack) =>
        unit with { Unit = unit.Unit with { Inventory = unit.Unit.Inventory.Replace(slot, stack) } };
}
