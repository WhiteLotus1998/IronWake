namespace Ironwake.Core;

/// <summary>
/// One signature item measured against the shop (issue 635, slice 3; DESIGN section 14): the
/// item, the shop weapon it is held to (null when the shop has none of its type and rank), the
/// ratio of their damage per combat summed over the targets, each signature art's verdict, and
/// why the item fails, or null when it passes.
/// </summary>
public sealed record CeilingReading(Weapon Item, Weapon? Comparator, double Ratio, ValueList<ArtReading> Arts, string? Failure)
{
    public bool Passed => Failure is null;
}

/// <summary>
/// A signature art on its item (DECISIONS/0099 on arts): how many of the targets it deals less
/// damage per combat to than the item's plain attack. It passes when that is at least one.
/// </summary>
public sealed record ArtReading(string ArtId, int LosesTo, int Targets)
{
    public bool Passed => LosesTo > 0;
}

/// <summary>
/// The Sim's ceiling on signature items (issue 635, DESIGN section 14): a bound weapon may be at
/// most <see cref="MaxRatio"/> of the best shop weapon of its type and rank in damage per combat,
/// and each signature art on it must lose to its plain attack against at least one target.
/// <para>
/// Damage per combat is the attacker's expected damage in one combat through the section 5
/// functions: damage times the hit probability times one plus two times the crit chance (a crit
/// deals <see cref="Combat.CritMultiplier"/> times), times the strike count. It is not capped at
/// the target's HP, so a kill never hides the margin. The owner fights at their cast card on
/// <see cref="TerrainId"/>; the targets are every unit outside the cast that carries a weapon
/// that strikes, with the first such weapon, on the same terrain at full HP, each struck at the
/// attacking weapon's nearest range. The comparator is the shop weapon (one some campaign map
/// stocks, else any priced weapon) of the item's type and rank with the most damage summed over
/// the targets.
/// </para>
/// </summary>
public static class SignatureCeiling
{
    /// <summary>The ceiling: the item's damage per combat over the comparator's, 15 percent over at most.</summary>
    public const double MaxRatio = 1.15;

    /// <summary>The terrain both sides stand on.</summary>
    public const string TerrainId = "plain";

    /// <summary>Every signature item in the content, in id order.</summary>
    public static IEnumerable<Weapon> Items(GameContent content) =>
        content.Weapons.Values.Where(w => w.BoundTo is not null);

    /// <summary>Reads every signature item; empty when none ships.</summary>
    public static IReadOnlyList<CeilingReading> ReadAll(GameContent content, RollScheme scheme) =>
        Items(content).Select(item => Read(content, item, scheme)).ToList();

    /// <summary>Reads one bound weapon against the shop, as the class summary describes.</summary>
    public static CeilingReading Read(GameContent content, Weapon item, RollScheme scheme)
    {
        if (item.BoundTo is not { } ownerId || content.Cast.FirstOrDefault(u => u.Id == ownerId) is not { } owner)
        {
            return new CeilingReading(item, null, 0, ValueList<ArtReading>.Empty, $"{item.Id} is bound to no cast member");
        }

        if (!content.Class(owner.ClassId).CanUse(item.Type))
        {
            return new CeilingReading(item, null, 0, ValueList<ArtReading>.Empty, $"{owner.Id} cannot wield {item.Id} ({item.Type.ToString().ToLowerInvariant()})");
        }

        var targets = Targets(content);
        var itemDamage = targets.Select(t => DamagePerCombat(content, owner, item, t, scheme)).ToList();
        var arts = ValueList<ArtReading>.From(content.Abilities.Values
            .Where(a => a.Effect is CombatArtEffect art && art.Item == item.Id)
            .Select(a =>
            {
                var struck = ((CombatArtEffect)a.Effect).Apply(item);
                var loses = targets.Select((t, i) => DamagePerCombat(content, owner, struck, t, scheme) < itemDamage[i]).Count(x => x);
                return new ArtReading(a.Id, loses, targets.Count);
            }));

        var comparator = Shop(content, item)
            .Select(w => (Weapon: w, Damage: targets.Sum(t => DamagePerCombat(content, owner, w, t, scheme))))
            .OrderByDescending(x => x.Damage)
            .ThenBy(x => x.Weapon.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        var rank = item.Rank.ToString();
        if (comparator.Weapon is null)
        {
            return new CeilingReading(item, null, 0, arts, $"the shop sells no {item.Type.ToString().ToLowerInvariant()} of rank {rank} to hold {item.Id} to");
        }

        var ratio = comparator.Damage <= 0 ? double.PositiveInfinity : itemDamage.Sum() / comparator.Damage;
        string? failure = null;
        if (ratio > MaxRatio)
        {
            failure = $"{item.Id} deals {ratio:F2} of {comparator.Weapon.Id}, over the ceiling of {MaxRatio:F2}";
        }
        else if (arts.FirstOrDefault(a => !a.Passed) is { } art)
        {
            failure = $"{art.ArtId} never loses to {item.Id}'s plain attack";
        }

        return new CeilingReading(item, comparator.Weapon, ratio, arts, failure);
    }

    /// <summary>
    /// The shop weapons an item is held to: unbound weapons of its type and rank that some
    /// campaign map stocks, or, when the campaign stocks none of them, every priced one. Healing
    /// spells are never comparators.
    /// </summary>
    public static IReadOnlyList<Weapon> Shop(GameContent content, Weapon item)
    {
        var kin = content.Weapons.Values
            .Where(w => w.BoundTo is null && w.Price is not null && !w.Heals && w.Type == item.Type && w.Rank == item.Rank)
            .ToList();
        var stocked = kin.Where(w => content.Campaign.Maps.Any(m => m.Stock.Contains(w.Id))).ToList();
        return stocked.Count > 0 ? stocked : kin;
    }

    /// <summary>The targets: every unit outside the cast with a weapon that strikes, in id order, with that weapon, at full HP.</summary>
    public static IReadOnlyList<(Unit Unit, Weapon Weapon)> Targets(GameContent content)
    {
        var cast = content.Cast.Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        var targets = new List<(Unit, Weapon)>();
        foreach (var unit in content.Units.Values.Where(u => !cast.Contains(u.Id)))
        {
            var weapon = unit.Inventory.Items
                .Select(s => content.Weapons.TryGetValue(s.ItemId, out var w) ? w : null)
                .FirstOrDefault(w => w is not null && !w.Heals);
            if (weapon is not null && content.Class(unit.ClassId).CanUse(weapon.Type))
            {
                targets.Add((unit, weapon));
            }
        }

        return targets;
    }

    /// <summary>The owner's expected damage in one combat against the target, as the class summary defines it.</summary>
    public static double DamagePerCombat(GameContent content, Unit owner, Weapon weapon, (Unit Unit, Weapon Weapon) target, RollScheme scheme)
    {
        var terrain = content.Terrain[TerrainId];
        var attacker = content.CombatantOf(owner, weapon, terrain, content.StatsOf(owner).Hp);
        var defender = content.CombatantOf(target.Unit, target.Weapon, terrain, content.StatsOf(target.Unit).Hp);
        var side = Combat.Forecast(attacker, defender, Math.Max(1, weapon.MinRange), scheme).Attacker;
        var perStrike = side.Damage * Combat.HitProbability(side.HitChance, scheme) * (1 + (Combat.CritMultiplier - 1) * side.CritChance / 100.0);
        return perStrike * side.StrikeCount;
    }
}
