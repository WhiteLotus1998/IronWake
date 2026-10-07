namespace Ironwake.Core;

/// <summary>
/// The numbers an armor tome lays on its caster (issue 1282, <see cref="Armor"/>): <paramref name="Def"/> added to
/// its Def in every combat, <paramref name="Mov"/> taken from its Mov, never below 1, for <paramref name="Phases"/> of
/// the caster's own phases after the cast. Read from the tome's <c>armor</c> block in weapons.json.
/// </summary>
public sealed record ArmorSpell(int Def, int Mov, int Phases);

/// <summary>
/// The armor a unit wears (issue 1282, <see cref="Armor"/>): the tome that laid it, its numbers, and
/// <paramref name="Phases"/>, how many of its own side's phases are still to begin under it. It falls as its side's
/// phase ends with <paramref name="Phases"/> at 0.
/// </summary>
public sealed record ArmorMark(string SpellId, int Def, int Mov, int Phases);

/// <summary>
/// Earth's armor (issue 1282, Lotus's #1247 rulings, DECISIONS/0307: "stone around the caster, heavy and hard to move
/// in"). A tome naming <see cref="RiderKind.Armor"/> on a school whose rider is <see cref="RiderKind.Raise"/> carries
/// its own numbers (<see cref="Weapon.Armor"/>: Earth Armor and Obsidian Armor differ). Cast through the Item action
/// on the caster alone, it spends one use and the caster's action, earns no EXP, and sets an <see cref="ArmorMark"/>:
/// <see cref="ArmorSpell.Def"/> more Def in every combat (<see cref="Bonus"/>) and <see cref="ArmorSpell.Mov"/> less
/// Mov, never below 1 (<see cref="Mov"/>), through the caster's next <see cref="ArmorSpell.Phases"/> own phases and
/// every enemy phase between; it falls as the last of them ends. A recast replaces it. Sunder does not strip it. It is
/// board state, so Recall restores it. The enemy dons it only when it has no strike (<see cref="EnemyAi.Don"/>, issue 1286); <see cref="Resolver.Legal"/> and the Sim's
/// player do not offer it.
/// </summary>
public static class Armor
{
    /// <summary>Whether <paramref name="weapon"/> is an armor tome: it names armor and carries its numbers.</summary>
    public static bool Armors(GameContent content, Weapon? weapon) =>
        weapon?.Armor is not null && content.RiderOf(weapon) is { Kind: RiderKind.Armor };

    /// <summary>What the unit's armor adds to its stats in a combat: its Def, nothing else; zero when it wears none.</summary>
    public static Stats Bonus(BattleUnit unit) =>
        unit.Armor is { } armor ? default(Stats) with { Def = armor.Def } : default;

    /// <summary><paramref name="mov"/> under the unit's armor: its Mov cost taken, never below 1; a Mov already at 1 or 0 stays.</summary>
    public static int Mov(int mov, BattleUnit unit) =>
        unit.Armor is { } armor && mov > 1 ? Math.Max(1, mov - armor.Mov) : mov;

    /// <summary>
    /// <paramref name="caster"/> dons the armor of <paramref name="spell"/> from <paramref name="slot"/>: the use and
    /// the wield are the caller's checks. Emits <see cref="ItemUsed"/>, <see cref="ArmorDonned"/> and, on its last use,
    /// <see cref="SpellSpent"/>; the caster has moved and acted.
    /// </summary>
    public static BattleState Don(BattleState state, BattleUnit caster, int slot, Weapon spell, List<GameEvent> events)
    {
        var numbers = spell.Armor!;
        var stack = caster.Unit.Inventory.Items[slot];
        var usesLeft = stack.Uses - 1;
        events.Add(new ItemUsed(caster.Id, spell.Id, caster.Id, usesLeft));
        events.Add(new ArmorDonned(caster.Id, spell.Id, numbers.Def, numbers.Mov, numbers.Phases));
        if (usesLeft == 0)
        {
            events.Add(new SpellSpent(caster.Id, spell.Id));
        }

        return state.WithUnit(caster with
        {
            Moved = true,
            Acted = true,
            Armor = new ArmorMark(spell.Id, numbers.Def, numbers.Mov, numbers.Phases),
            Unit = caster.Unit with { Inventory = caster.Unit.Inventory.Replace(slot, stack with { Uses = usesLeft }) },
        });
    }

    /// <summary>
    /// The unit's armor across a phase change from <paramref name="ended"/> to <paramref name="begins"/>: it falls
    /// (<see cref="ArmorFell"/>) when its side's phase ended with none of its phases still to begin; otherwise one
    /// is counted off when its side's phase begins.
    /// </summary>
    public static ArmorMark? AtPhaseChange(BattleUnit unit, Side ended, Side begins, List<GameEvent> events)
    {
        if (unit.Armor is not { } armor)
        {
            return null;
        }

        if (unit.Side == ended && armor.Phases == 0)
        {
            events.Add(new ArmorFell(unit.Id, armor.SpellId));
            return null;
        }

        return unit.Side == begins ? armor with { Phases = armor.Phases - 1 } : armor;
    }

    /// <summary>
    /// When the armor falls, in player words, by how many of its side's phases are still to begin under it:
    /// <c>as this player phase ends</c> (none, in its own phase), <c>as the next player phase ends</c> (one), or
    /// <c>after 2 more player phases</c>.
    /// </summary>
    public static string Falls(BattleUnit unit, ArmorMark armor)
    {
        var word = Frost.Word(unit.Side);
        return armor.Phases switch
        {
            0 => $"as this {word} phase ends",
            1 => $"as the next {word} phase ends",
            var owed => $"after {owed} more {word} phases",
        };
    }

    /// <summary>The unit card's line while it wears armor: <c>armor: Def +10, Mov -2 (Earth Armor), falls as the next player phase ends</c>.</summary>
    public static string? CardLine(GameContent content, BattleUnit unit) =>
        unit.Armor is { } armor
            ? $"armor: Def +{armor.Def}, Mov -{armor.Mov} ({(content.Weapons.TryGetValue(armor.SpellId, out var spell) ? spell.Name : armor.SpellId)}), falls {Falls(unit, armor)}"
            : null;
}
