namespace Ironwake.Core;

/// <summary>
/// What a class asks of a unit certifying into it (issue 72, DESIGN section 3): a minimum
/// level, a minimum rank in each named weapon type, and a minimum in each named stat. A
/// stat at 0 in <see cref="Stats"/> asks nothing. <see cref="None"/> is the requirement of
/// a class whose content names none, which any unit meets.
/// </summary>
public sealed record CertificationRequirements(int Level, ValueList<(WeaponType Type, WeaponRank Rank)> Ranks, Stats Stats)
{
    public static CertificationRequirements None { get; } = new(Unit.MinLevel, ValueList<(WeaponType, WeaponRank)>.Empty, Stats.Zero);
}

/// <summary>One requirement a unit fails, with the text the screen prints for it.</summary>
public sealed record CertificationRefusal(string Requirement, string Text);

/// <summary>
/// Class certification (issue 72): a unit that meets a class's requirements certifies into
/// it, with no roll. Certifying changes the unit's class and nothing else, so its stats in
/// the new class are its own plus the new class's modifiers (<see cref="Unit.EffectiveStats"/>),
/// and its mastery points and mastered abilities stay. What certifying costs is the between-map
/// screen's (issue 74), not this rule's.
/// </summary>
public static class Certifications
{
    /// <summary>
    /// Every requirement of <paramref name="target"/> that <paramref name="unit"/> fails, in
    /// order: the class itself, level, ranks in content order, stats in draw order. Empty
    /// when the unit may certify. A stat minimum reads the unit's own stats, so the class it
    /// would leave counts for nothing toward the class it enters.
    /// </summary>
    public static IReadOnlyList<CertificationRefusal> Check(Unit unit, UnitClass target)
    {
        var refusals = new List<CertificationRefusal>();
        if (unit.ClassId == target.Id)
        {
            refusals.Add(new("class", $"{unit.Id} is already a {target.Name}"));
        }

        var requirements = target.Certification;
        if (unit.Level < requirements.Level)
        {
            refusals.Add(new("level", $"needs level {requirements.Level}, has {unit.Level}"));
        }

        foreach (var (type, rank) in requirements.Ranks)
        {
            var has = unit.Skill.Rank(type);
            if (has < rank)
            {
                var name = type.ToString().ToLowerInvariant();
                refusals.Add(new("ranks." + name, $"needs {name} {rank}, has {has}"));
            }
        }

        foreach (var stat in Stats.All)
        {
            var needs = requirements.Stats.Get(stat);
            var has = unit.Stats.Get(stat);
            if (has < needs)
            {
                var name = stat.ToString().ToLowerInvariant();
                refusals.Add(new("stats." + name, $"needs {name} {needs}, has {has}"));
            }
        }

        return refusals;
    }

    /// <summary>
    /// <paramref name="unit"/> in <paramref name="target"/>, or an
    /// <see cref="InvalidOperationException"/> naming the first requirement it fails.
    /// </summary>
    public static Unit Certify(Unit unit, UnitClass target)
    {
        var refusals = Check(unit, target);
        if (refusals.Count > 0)
        {
            throw new InvalidOperationException($"{unit.Id} cannot certify as {target.Name}: {refusals[0].Text}");
        }

        return unit with { ClassId = target.Id };
    }
}

/// <summary>
/// A certification trial (issue 73, DESIGN section 13.6): the <c>certification:</c> header of a
/// one-unit puzzle map. Whoever fills the map's one player slot plays it in <see cref="ClassId"/>
/// with exactly <see cref="Loadout"/>, each at full uses, and clearing the map earns the class.
/// </summary>
/// <param name="ClassId">The class the trial is played in and grants.</param>
/// <param name="Loadout">Weapon and item ids the candidate carries, in slot order.</param>
public sealed record CertificationTrial(string ClassId, ValueList<string> Loadout)
{
    /// <summary>
    /// <paramref name="unit"/> as the trial fields it: in the trial's class, carrying the loadout
    /// and nothing else, every stack at its full uses. Level, stats, ranks and mastery are its own.
    /// </summary>
    public Unit Candidate(Unit unit, GameContent content)
    {
        var items = Loadout.Select(id => new ItemStack(id, content.Weapons.TryGetValue(id, out var weapon) ? weapon.Durability : content.Item(id).Uses));
        return unit with { ClassId = ClassId, Inventory = new Inventory(ValueList<ItemStack>.From(items)) };
    }
}
