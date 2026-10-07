namespace Ironwake.Core;

/// <summary>
/// Light's area heal (issue 1321 slice 3, Lotus's second spell rulings, DECISIONS/0317: "a big late heal for the area
/// around the caster"). A healing spell that names <c>areaHeal</c> (<see cref="Weapon.AreaHeal"/>, the radius) is cast
/// through the Item action with no target (<c>item &lt;unit&gt; &lt;slot&gt;</c>), checked as a heal is (a class that heals
/// with its type, the rank, a use left, no art).
/// <list type="bullet">
/// <item>It heals every wounded unit of the caster's side within the radius of the caster, the caster too, each by the
/// caster's single heal (<see cref="Combat.Heal"/>), never past max HP.</item>
/// <item>It is refused (<see cref="RejectionReason.NothingToHeal"/>) when no one in the area is wounded, and
/// (<see cref="RejectionReason.NotUsable"/>) when it names a target.</item>
/// <item>The healer spends one use and its action, and earns one heal's EXP, rank and mastery: the below-half bonus when
/// any unit it healed was under half HP.</item>
/// </list>
/// Its uses are the limit; the fixture carries one, so it is once a map. <see cref="Resolver.Legal"/> offers it when
/// someone in the area is wounded; the Sim's player and the enemy planner never cast it until a shipped unit carries one.
/// </summary>
public static class AreaHeal
{
    /// <summary>
    /// Every wounded unit of <paramref name="caster"/>'s side within <paramref name="spell"/>'s radius of it, the caster
    /// included, with the HP it would end at; board order. Empty when no one there is wounded.
    /// </summary>
    public static IReadOnlyList<(BattleUnit Unit, int HpAfter)> Healed(BattleState state, GameContent content, BattleUnit caster, Weapon spell)
    {
        var heal = Combat.Heal(caster.ToCombatant(state.Map, content), spell);
        var healed = new List<(BattleUnit, int)>();
        foreach (var ally in state.UnitsOf(caster.Side))
        {
            var max = ally.MaxHp(content);
            if (caster.At.DistanceTo(ally.At) <= spell.AreaHeal && ally.Hp < max)
            {
                healed.Add((ally, Math.Min(max, ally.Hp + heal)));
            }
        }

        return healed;
    }

    /// <summary>
    /// The forecast's line for the cast: <c>test_mend heals wren 6 (hp 15), mira 3 (hp 16)</c>, or what refuses it.
    /// </summary>
    public static string Preview(BattleState state, GameContent content, BattleUnit caster, Weapon spell)
    {
        var healed = Healed(state, content, caster, spell);
        return healed.Count == 0
            ? $"{spell.Name} would heal no one: no unit within {spell.AreaHeal} of {caster.Id} is wounded"
            : $"{spell.Name} heals " + string.Join(", ", healed.Select(h => $"{h.Unit.Id} {h.HpAfter - h.Unit.Hp} (hp {h.HpAfter})"));
    }
}
