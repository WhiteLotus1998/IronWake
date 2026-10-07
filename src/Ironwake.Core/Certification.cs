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

    /// <summary>
    /// A door's points gate (issue 1174, round 394): a minimum in rank points in each named weapon type,
    /// strictly between D's threshold and C's, so a gate never reads as a letter. Empty asks nothing.
    /// </summary>
    public ValueList<(WeaponType Type, int Points)> Points { get; init; } = ValueList<(WeaponType, int)>.Empty;

    /// <summary>Whether <paramref name="points"/> may stand as a points gate: strictly between D's threshold and C's.</summary>
    public static bool IsGate(int points) => points > WeaponRanks.Threshold(WeaponRank.D) && points < WeaponRanks.Threshold(WeaponRank.C);

    /// <summary>A points gate as the screen names it, <c>lance 50 (between D and C)</c>.</summary>
    public static string DescribeGate(WeaponType type, int points) => $"{type.Label()} {points} (between D and C)";

    /// <summary>
    /// The requirements as the between-map screen prints them: the level, then each rank in
    /// content order, then each points gate (issue 1174), then each stat minimum in draw order, as <c>level 4, sword D, spd 8</c>;
    /// <c>nothing</c> when they ask nothing a new recruit lacks.
    /// </summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (Level > Unit.MinLevel)
        {
            parts.Add($"level {Level}");
        }

        parts.AddRange(Ranks.Select(r => $"{r.Type.Label()} {r.Rank}"));
        parts.AddRange(Points.Select(g => DescribeGate(g.Type, g.Points)));
        parts.AddRange(Stats.All.Where(s => Stats.Get(s) > 0).Select(s => $"{s.ToString().ToLowerInvariant()} {Stats.Get(s)}"));
        return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
    }
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
    /// order: the class itself, a hidden class (issue 691), an advanced form's base (issue 704), level, ranks in content order, points gates in content order (issue 1174), stats in draw order. Empty
    /// when the unit may certify. A stat minimum reads the unit's own stats, so the class it
    /// would leave counts for nothing toward the class it enters.
    /// </summary>
    public static IReadOnlyList<CertificationRefusal> Check(Unit unit, UnitClass target) => Check(unit, target, null);

    /// <summary>
    /// <see cref="Check(Unit, UnitClass)"/> read with the class the unit would leave,
    /// <paramref name="from"/>: a unit in a hidden class keeps it (issue 691), so every other
    /// class is refused first, naming the class it holds.
    /// </summary>
    public static IReadOnlyList<CertificationRefusal> Check(Unit unit, UnitClass target, UnitClass? from) => Check(unit, target, from, captain: false);

    /// <summary>
    /// <see cref="Check(Unit, UnitClass, UnitClass)"/> read knowing whether <paramref name="unit"/> is the
    /// captain (issue 705): the captain's ladder (<see cref="UnitClass.Captain"/>) is the captain's alone,
    /// the captain takes no class off it, and once on it the captain goes only up, into the held class's
    /// own advanced form, each refused naming the ladder.
    /// </summary>
    public static IReadOnlyList<CertificationRefusal> Check(Unit unit, UnitClass target, UnitClass? from, bool captain) =>
        Check(unit, target, from, captain, ValueList<string>.Empty);

    /// <summary>
    /// <see cref="Check(Unit, UnitClass, UnitClass, bool)"/> read with the quests the campaign has won,
    /// <paramref name="questsWon"/> (issue 706): a unique class is refused to every unit but its own, and to
    /// its own until the quest that opens it is won; and a unit that has passed one door of a base's second
    /// promotion (<see cref="Unit.Doors"/>) is refused every other door of that base.
    /// </summary>
    public static IReadOnlyList<CertificationRefusal> Check(Unit unit, UnitClass target, UnitClass? from, bool captain, IReadOnlyCollection<string> questsWon)
    {
        var refusals = new List<CertificationRefusal>();
        if (from is { Hidden: true } && from.Id == unit.ClassId && target.Id != from.Id)
        {
            refusals.Add(new("from", $"{unit.Id} is a {from.Name}, earned and kept"));
        }

        if (target.Captain && !captain)
        {
            refusals.Add(new("captain", $"only the captain takes {target.Name}"));
        }
        else if (!target.Captain && captain && unit.ClassId != target.Id)
        {
            refusals.Add(new("captain", $"{target.Name} is not on the captain's ladder"));
        }
        else if (captain && from is { Captain: true } && from.Id == unit.ClassId && target.Id != from.Id && target.Advances?.Id != from.Id)
        {
            refusals.Add(new("captain", $"the captain's ladder is one-way; {from.Name} leads only to its own form"));
        }

        if (unit.ClassId == target.Id)
        {
            refusals.Add(new("class", $"{unit.Id} is already {Article(target.Name)} {target.Name}"));
        }

        if (target.Enemy)
        {
            refusals.Add(new("enemy", $"{target.Name} is an enemy's class; nobody joins it"));
        }

        if (target.Hidden)
        {
            refusals.Add(new("hidden", $"{target.Name} is not certified; it is earned"));
        }

        if (target.Unique is { } owner && owner != unit.Id)
        {
            refusals.Add(new("unique", $"{target.Name} is {owner}'s alone"));
        }
        else if (target.UnlockedBy is { } quest && !questsWon.Contains(quest))
        {
            refusals.Add(new("unlockedBy", $"{target.Name} opens when the quest {quest} is won"));
        }

        if (target.Advances is { } basis && unit.ClassId != basis.Id)
        {
            refusals.Add(new("advances", $"needs to be {Article(basis.Name)} {basis.Name} first"));
        }

        if (target.Advances is { } door && unit.Doors.Any(d => d.Base == door.Id && d.Form != target.Id))
        {
            refusals.Add(new("door", $"{unit.Id} took the other door above {door.Name}; {target.Name} is closed"));
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
                refusals.Add(new("ranks." + type.ToString().ToLowerInvariant(), $"needs {type.Label()} {rank}, has {has}"));
            }
        }

        foreach (var (type, points) in requirements.Points)
        {
            var has = unit.Skill.Points(type);
            if (has < points)
            {
                refusals.Add(new("points." + type.ToString().ToLowerInvariant(), $"needs {type.Label()} {points}, has {has}"));
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

    private static string Article(string name) => "AEIOU".Contains(name[0]) ? "an" : "a";

    /// <summary>
    /// <paramref name="unit"/> in <paramref name="target"/>, or an
    /// <see cref="InvalidOperationException"/> naming the first requirement it fails.
    /// </summary>
    public static Unit Certify(Unit unit, UnitClass target) => Certify(unit, target, captain: false);

    /// <summary>
    /// <see cref="Certify(Unit, UnitClass)"/> read knowing whether <paramref name="unit"/> is the captain (issue 705).
    /// </summary>
    public static Unit Certify(Unit unit, UnitClass target, bool captain) => Certify(unit, target, captain, ValueList<string>.Empty);

    /// <summary>
    /// <see cref="Certify(Unit, UnitClass, bool)"/> read with the quests the campaign has won (issue 706).
    /// Passing into an advanced form records the door taken (<see cref="Unit.Doors"/>).
    /// </summary>
    public static Unit Certify(Unit unit, UnitClass target, bool captain, IReadOnlyCollection<string> questsWon)
    {
        var refusals = Check(unit, target, null, captain, questsWon);
        if (refusals.Count > 0)
        {
            throw new InvalidOperationException($"{unit.Id} cannot be promoted to {target.Name}: {refusals[0].Text}");
        }

        var promoted = Grant(unit with { ClassId = target.Id }, target);
        return target.Advances is not { } basis || promoted.Doors.Any(d => d.Base == basis.Id)
            ? promoted
            : promoted with { Doors = promoted.Doors.Add((basis.Id, target.Id)) };
    }

    /// <summary>
    /// <paramref name="unit"/> with each rank <paramref name="target"/> grants (issue 704,
    /// <see cref="UnitClass.Grants"/>) raised to at least that rank's threshold; a rank it already holds stays.
    /// </summary>
    public static Unit Grant(Unit unit, UnitClass target)
    {
        var skill = unit.Skill;
        foreach (var (type, rank) in target.Grants)
        {
            skill = skill.With(type, Math.Max(skill.Points(type), WeaponRanks.Threshold(rank)));
        }

        return skill == unit.Skill ? unit : unit with { Skill = skill };
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
