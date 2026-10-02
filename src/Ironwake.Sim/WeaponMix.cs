using System.Collections.Immutable;
using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// The weapons a player unit fought with (issue 746): per weapon type, the attacks it made with
/// one and the counters it struck back with one on the enemy phase. <c>--ladder</c> prints it for
/// the captain and <c>--levels</c> per class, so a read shows the bow and the lance actually firing.
/// </summary>
public sealed record WeaponMix(ImmutableSortedDictionary<WeaponType, (int Attacks, int Counters)> ByType)
{
    public static WeaponMix Zero { get; } = new(ImmutableSortedDictionary<WeaponType, (int Attacks, int Counters)>.Empty);

    /// <summary>This mix with one attack (or, given <paramref name="counter"/>, one counter) with a weapon of <paramref name="type"/> added.</summary>
    public WeaponMix With(WeaponType type, bool counter)
    {
        var (attacks, counters) = ByType.GetValueOrDefault(type);
        return new(ByType.SetItem(type, counter ? (attacks, counters + 1) : (attacks + 1, counters)));
    }

    public WeaponMix Plus(WeaponMix other) =>
        new(other.ByType.Aggregate(ByType, (sum, kv) =>
        {
            var (attacks, counters) = sum.GetValueOrDefault(kv.Key);
            return sum.SetItem(kv.Key, (attacks + kv.Value.Attacks, counters + kv.Value.Counters));
        }));

    /// <summary>Each type fought with as <c>type attacks/counters</c>, in the enum's order; <c>none</c> when it fought with nothing.</summary>
    public override string ToString() =>
        ByType.Count == 0 ? "none" : string.Join(" ", ByType.Select(kv => $"{kv.Key.ToString().ToLowerInvariant()} {kv.Value.Attacks}/{kv.Value.Counters}"));

    /// <summary>
    /// The strikes one accepted command made with a weapon, read from the board before it and the
    /// events it raised: a player unit's attack, with the slot it named or its equipped weapon, and a
    /// player unit's counter on the enemy phase, with its equipped weapon (the counter's weapon is
    /// always the one in front). Each comes with the unit's id and class as it stood before the command.
    /// </summary>
    public static IEnumerable<(string UnitId, string ClassId, WeaponType Type, bool Counter, string ItemId)> Strikes(
        BattleState before, GameContent content, Command command, IReadOnlyList<GameEvent> events)
    {
        if (command is not Attack attack)
        {
            yield break;
        }

        foreach (var fought in events.OfType<CombatFought>())
        {
            if (fought.AttackerId == attack.UnitId && before.Find(attack.UnitId) is { Side: Side.Player } attacker)
            {
                var weapon = attack.Slot is { } slot ? attacker.UsableWeaponAt(content, slot) : attacker.EquippedWeapon(content);
                if (weapon is not null)
                {
                    yield return (attacker.Id, attacker.Unit.ClassId, weapon.Type, false, weapon.Id);
                }
            }
            else if (fought.Phase == Side.Enemy && before.Find(fought.TargetId) is { Side: Side.Player } defender
                && fought.Strikes.Any(s => s.AttackerId == defender.Id) && defender.EquippedWeapon(content) is { } held)
            {
                yield return (defender.Id, defender.Unit.ClassId, held.Type, true, held.Id);
            }
        }
    }
}

/// <summary>
/// The same count as <see cref="WeaponMix"/> keyed by the item struck with, not its type (issue 757),
/// so two weapons of one type tell apart: Pell's Cinder and Gust are both reason.
/// </summary>
public sealed record ItemMix(ImmutableSortedDictionary<string, (int Attacks, int Counters)> ById)
{
    public static ItemMix Zero { get; } = new(ImmutableSortedDictionary.Create<string, (int Attacks, int Counters)>(StringComparer.Ordinal));

    /// <summary>This mix with one attack (or, given <paramref name="counter"/>, one counter) with <paramref name="itemId"/> added.</summary>
    public ItemMix With(string itemId, bool counter)
    {
        var (attacks, counters) = ById.GetValueOrDefault(itemId);
        return new(ById.SetItem(itemId, counter ? (attacks, counters + 1) : (attacks + 1, counters)));
    }

    public ItemMix Plus(ItemMix other) =>
        new(other.ById.Aggregate(ById, (sum, kv) =>
        {
            var (attacks, counters) = sum.GetValueOrDefault(kv.Key);
            return sum.SetItem(kv.Key, (attacks + kv.Value.Attacks, counters + kv.Value.Counters));
        }));

    /// <summary>Each item fought with as <c>id attacks/counters</c>, by id; <c>none</c> when it fought with nothing.</summary>
    public override string ToString() =>
        ById.Count == 0 ? "none" : string.Join(" ", ById.Select(kv => $"{kv.Key} {kv.Value.Attacks}/{kv.Value.Counters}"));
}
