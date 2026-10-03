namespace Ironwake.Core;

/// <summary>
/// The cursed heirloom (issue 646, DESIGN section 14): a weapon with a <see cref="Weapon.Heirloom"/>
/// ladder keeps a hidden counter and a stage on its stack (<see cref="ItemStack.Combats"/>,
/// <see cref="ItemStack.Stage"/>). The rule is the weapon's, for whoever carries it:
/// <list type="bullet">
/// <item>The counter: each combat its carrier lands or misses a strike with it equipped, and
/// survives, counts one. Never printed; benching it, or carrying it unequipped, delays it.</item>
/// <item>The turn: after such a combat, when the counter has reached the next stage's threshold
/// and the battle is on or after the ladder's first campaign map (<see cref="HeirloomLadder.FromMap"/>,
/// <see cref="BattleState.CampaignMap"/>), it turns to that stage (<see cref="HeirloomTurned"/>),
/// one stage a combat. Counts reached on an earlier map turn it on the first combat that may.</item>
/// <item>The numbers: its current stage's Mt, hit, crit and weight, always printed truthfully
/// (pillar 2); only its future is hidden.</item>
/// <item>The hold (round 266): a ladder that <see cref="HeirloomLadder.HoldsAt"/> a stage stops there
/// in a battle that lists it <see cref="BattleState.Held"/>, the counter still counting, until the
/// campaign wins the quest that wakes it (<see cref="CampaignQuest.Wakes"/>).</item>
/// <item>The uses: an heirloom never spends below 1, so it never breaks.</item>
/// </list>
/// Everything is board state, so Recall restores the counter and the stage with the board.
/// </summary>
public static class Heirloom
{
    /// <summary>The line the smith gives an heirloom still at its first stage (issue 646), which the forge refuses to work.</summary>
    public const string SmithRefusal = "Nothing here to work with. It's all rust.";

    /// <summary>The weapon as its stack's stage makes it: the stage's numbers and line; any other weapon, or stage 0, unchanged.</summary>
    public static Weapon Shape(Weapon weapon, ItemStack stack)
    {
        if (weapon.Heirloom is not { } ladder || stack.Stage <= 0)
        {
            return weapon;
        }

        var stage = ladder.Turns[Math.Min(stack.Stage, ladder.Turns.Count) - 1];
        return weapon with { Mt = stage.Mt, Hit = stage.Hit, Crit = stage.Crit, Wt = stage.Wt, Description = stage.Description };
    }

    /// <summary>The id of the stage <paramref name="stack"/> has reached; null for a weapon with no ladder.</summary>
    public static string? StageId(Weapon weapon, ItemStack stack) =>
        weapon.Heirloom is not { } ladder ? null
        : stack.Stage <= 0 ? ladder.First
        : ladder.Turns[Math.Min(stack.Stage, ladder.Turns.Count) - 1].Id;

    /// <summary>The weapon at its last stage: what the signature ceiling holds to the shop. Any other weapon unchanged.</summary>
    public static Weapon Last(Weapon weapon) =>
        weapon.Heirloom is { } ladder ? Shape(weapon, new ItemStack(weapon.Id, weapon.Durability) { Stage = ladder.Turns.Count }) : weapon;

    /// <summary>
    /// Whether the smith refuses to work <paramref name="stack"/> (issue 646): an heirloom at its
    /// first stage. The forge reads this before any Refine; any other weapon is never refused here.
    /// </summary>
    public static bool SmithRefuses(Weapon weapon, ItemStack stack) => weapon.Heirloom is not null && stack.Stage <= 0;

    /// <summary>The card's line for a ladder held past its count (round 266, pillar 2): the hold is printed, so it never reads as a bug.</summary>
    public static string HoldLine(string ownerName) => $"the rust holds; it waits on {ownerName}";

    /// <summary>Whether a stage may turn in <paramref name="state"/>: outside the campaign, or on or after the ladder's first map.</summary>
    public static bool Open(HeirloomLadder ladder, BattleState state) => state.CampaignMap is not { } map || map >= ladder.FromMap;

    /// <summary>Whether <paramref name="stack"/> of <paramref name="itemId"/> stands at its ladder's hold while the hold applies (<paramref name="held"/> lists it).</summary>
    public static bool AtHold(HeirloomLadder ladder, ItemStack stack, string itemId, IEnumerable<string> held) =>
        ladder.HoldsAt is { } at && stack.Stage >= at && held.Contains(itemId, StringComparer.Ordinal);

    /// <summary>
    /// Whether the card prints <see cref="HoldLine"/> for <paramref name="stack"/>: it stands at its
    /// hold, the hold applies, and the counter has reached the next stage's threshold, so only the
    /// quest keeps it from turning.
    /// </summary>
    public static bool Waiting(HeirloomLadder ladder, ItemStack stack, string itemId, IEnumerable<string> held) =>
        AtHold(ladder, stack, itemId, held) && stack.Stage < ladder.Turns.Count && stack.Combats >= ladder.Turns[stack.Stage].At;

    /// <summary>
    /// <paramref name="stack"/> as a won waking quest leaves it (round 266): one stage on when it
    /// stood at the hold with the count already past the next threshold, otherwise unchanged, so
    /// the next combat that reaches the threshold turns it as usual.
    /// </summary>
    public static ItemStack Release(HeirloomLadder ladder, ItemStack stack) =>
        ladder.HoldsAt is { } at && stack.Stage == at && stack.Stage < ladder.Turns.Count && stack.Combats >= ladder.Turns[stack.Stage].At
            ? stack with { Stage = stack.Stage + 1 }
            : stack;

    /// <summary>
    /// After a combat in <paramref name="state"/> in which <paramref name="unit"/> (as the combat
    /// left it) struck with an heirloom equipped: the counter counts it, and the next stage turns
    /// when its threshold is reached and the floor is open. A fallen unit, a unit that made no
    /// strike, and any other weapon are unchanged.
    /// </summary>
    public static BattleUnit AfterCombat(BattleUnit unit, BattleState state, GameContent content, ValueList<StrikeEvent> strikes, List<GameEvent> events)
    {
        var slot = unit.EquippedSlot(content);
        if (slot < 0 || unit.Hp <= 0 || !strikes.Any(s => s.AttackerId == unit.Id))
        {
            return unit;
        }

        var stack = unit.Unit.Inventory.Items[slot];
        if (content.Weapon(stack.ItemId).Heirloom is not { } ladder)
        {
            return unit;
        }

        stack = stack with { Combats = stack.Combats + 1 };
        if (stack.Stage < ladder.Turns.Count && stack.Combats >= ladder.Turns[stack.Stage].At && Open(ladder, state) && !AtHold(ladder, stack, stack.ItemId, state.Held))
        {
            stack = stack with { Stage = stack.Stage + 1 };
            events.Add(new HeirloomTurned(unit.Id, stack.ItemId, stack.Stage, ladder.Turns[stack.Stage - 1].Id));
        }

        return unit with { Unit = unit.Unit with { Inventory = unit.Unit.Inventory.Replace(slot, stack) } };
    }

    /// <summary>
    /// The unit card's line for a carrier (issue 646): the weapon, the stage it has reached and that
    /// stage's line, and the hold's line when only a quest keeps it from turning (<see cref="Waiting"/>,
    /// <paramref name="held"/> the battle's <see cref="BattleState.Held"/>). Never the counter. Null
    /// for a unit carrying no heirloom.
    /// </summary>
    public static string? Card(BattleUnit unit, GameContent content, IEnumerable<string>? held = null)
    {
        foreach (var stack in unit.Unit.Inventory.Items)
        {
            if (content.Weapons.TryGetValue(stack.ItemId, out var weapon) && weapon.Heirloom is { } ladder)
            {
                var line = $"{weapon.Name}, {StageId(weapon, stack)}: {Shape(weapon, stack).Description}";
                return held is not null && Waiting(ladder, stack, stack.ItemId, held) ? $"{line} ({HoldLine(unit.Unit.Name)})" : line;
            }
        }

        return null;
    }
}
