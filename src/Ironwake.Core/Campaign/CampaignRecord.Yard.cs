namespace Ironwake.Core;

/// <summary>The camp's duties and the yard (issue 1331).</summary>
public sealed partial record CampaignRecord
{
    /// <summary>The duty <paramref name="unitId"/> took at this camp; rest when no command named one.</summary>
    public Duty DutyOf(string unitId) => Duties.FirstOrDefault(d => d.UnitId == unitId)?.Duty ?? Duty.Rest;

    /// <summary>
    /// Why <paramref name="unitId"/> cannot take <paramref name="duty"/> at this camp, or null when it can:
    /// it took another already (one duty a unit; a second side map is the same duty), or it is
    /// wounded and the duty is the forge or the yard, since a wounded unit's only duty is rest.
    /// </summary>
    public string? DutyRefusal(string unitId, Duty duty)
    {
        var name = Find(unitId)?.Name ?? unitId;
        if (Duties.FirstOrDefault(d => d.UnitId == unitId) is { } taken && taken.Duty != duty)
        {
            return $"{name} took the {YardRules.Word(taken.Duty)} duty at this camp; one duty a unit";
        }

        return duty is Duty.Forge or Duty.Yard && Find(unitId)?.Wound is { } wound
            ? $"{name} is {wound.Label}; a wounded unit's only duty is rest"
            : null;
    }

    /// <summary>The duties with each of <paramref name="unitIds"/> holding <paramref name="duty"/>, a unit already holding it unchanged.</summary>
    private ValueList<UnitDuty> WithDuty(IEnumerable<string> unitIds, Duty duty) =>
        ValueList<UnitDuty>.From(Duties.Concat(unitIds.Where(id => Duties.All(d => d.UnitId != id)).Select(id => new UnitDuty(id, duty))));

    /// <summary>
    /// <c>duty &lt;unit&gt; &lt;duty&gt;</c> (issue 1331): rest or the forge, refused as
    /// <see cref="DutyRefusal"/> says; a side map's party and the yard take theirs through
    /// their own commands, so the words <c>quest</c> and <c>yard</c> point there.
    /// </summary>
    public ScreenResult AssignDuty(string unitId, string word)
    {
        if (Find(unitId) is not { } unit)
        {
            return ScreenResult.Refused(this, $"no unit '{unitId}' on the roster");
        }

        switch (YardRules.Parse(word))
        {
            case null:
                return ScreenResult.Refused(this, "usage: duty <unit> rest|forge|quest|yard");
            case Duty.Quest:
                return ScreenResult.Refused(this, "a side map's party takes its duty there: quest <id> <ally>...");
            case Duty.Yard:
                return ScreenResult.Refused(this, "the yard takes a teacher and a student: yard <teacher> <student> <weapon>");
            case { } duty:
                if (DutyRefusal(unitId, duty) is { } refusal)
                {
                    return ScreenResult.Refused(this, refusal);
                }

                return DutyOf(unitId) == duty && Duties.Any(d => d.UnitId == unitId)
                    ? ScreenResult.Refused(this, $"{unit.Name} took the {word} duty at this camp already")
                    : new ScreenResult(this with { Duties = WithDuty(new[] { unitId }, duty) }, $"{unit.Name} takes the {word} duty at this camp", true);
        }
    }

    /// <summary>
    /// Why <paramref name="teacherId"/> cannot teach <paramref name="studentId"/> the weapon
    /// <paramref name="weaponName"/> at this camp, or null when they can: both on the roster and
    /// distinct, both free to take the yard duty (<see cref="DutyRefusal"/>), both of classes that
    /// use the weapon, and the student under at least one of the teacher's ceilings, since a drill
    /// that can raise nothing is not offered.
    /// </summary>
    public string? YardRefusal(string teacherId, string studentId, string weaponName, GameContent content)
    {
        if (Find(teacherId) is not { } teacher)
        {
            return $"no unit '{teacherId}' on the roster";
        }

        if (Find(studentId) is not { } student)
        {
            return $"no unit '{studentId}' on the roster";
        }

        if (teacherId == studentId)
        {
            return $"{teacher.Name} cannot teach themselves; the yard takes a teacher and a student";
        }

        if (YardRules.Weapon(weaponName) is not { } weapon)
        {
            return $"no weapon '{weaponName}'; one of {string.Join(", ", Enum.GetValues<WeaponType>().Select(t => t.ToString().ToLowerInvariant()))}";
        }

        bool Drilled(string id) => Duties.Any(d => d.UnitId == id && d.Duty == Duty.Yard);
        if (Drilled(teacherId) || Drilled(studentId))
        {
            return $"{(Drilled(teacherId) ? teacher.Name : student.Name)} drilled in the yard at this camp already; one duty a unit";
        }

        if ((DutyRefusal(teacherId, Duty.Yard) ?? DutyRefusal(studentId, Duty.Yard)) is { } busy)
        {
            return busy;
        }

        var word = weapon.ToString().ToLowerInvariant();
        foreach (var unit in new[] { teacher, student })
        {
            if (!content.Class(unit.ClassId).CanUse(weapon))
            {
                return $"{unit.Name} is a {content.Class(unit.ClassId).Name} and does not use the {word}";
            }
        }

        var level = YardRules.LevelCeiling(teacher);
        var rank = YardRules.RankCeiling(teacher, weapon);
        return student.Level >= level && student.Skill.Rank(weapon) >= rank
            ? $"{student.Name} stands at {teacher.Name}'s ceiling already: L{level}, {word} {rank}"
            : null;
    }

    /// <summary>
    /// The seed a yard drill before the next map runs on: past every map's, trial's and side map's
    /// seed, one a map.
    /// </summary>
    public ulong YardSeed(GameContent content)
    {
        var maps = (ulong)content.Campaign.Maps.Count;
        return unchecked(Seed + (2 + (ulong)content.Campaign.Quests.Count) * maps + (ulong)MapIndex);
    }

    /// <summary>The board the yard drills on before the next map: the pool's boards in turn, by the next map's index; null when the pool is empty.</summary>
    public string? YardBoard(IReadOnlyList<string> pool) => pool.Count == 0 ? null : pool[MapIndex % pool.Count];

    /// <summary>
    /// Why <paramref name="map"/> cannot be a yard board, or null when it can: one <c>captain</c>
    /// slot (the student's), one bare <c>recruit</c> slot (the teacher's), nobody placed by name, no
    /// certification header.
    /// </summary>
    public static string? YardMapRefusal(MapDefinition map)
    {
        var players = map.Placements.OfType<PlayerPlacement>().ToList();
        return map.Certification is null
            && players.Count(p => p.Slot == PlayerSlot.Captain) == 1
            && players.Count(p => p.Slot == PlayerSlot.AnyRecruit) == 1
            && players.Count == 2
            ? null
            : $"the yard board '{map.Name}' needs one captain slot (the student's) and one recruit slot (the teacher's), and nothing else";
    }

    /// <summary>
    /// The yard drill (issue 1331): <paramref name="map"/> under the campaign's difficulty, its
    /// enemies at the student's level, the student in the captain slot (the drill is lost if the
    /// student falls) and the teacher in the bare slot, each carrying its <see cref="YardHand"/>,
    /// on <see cref="YardSeed"/>. The caller has checked <see cref="YardRefusal"/> and <see cref="YardMapRefusal"/>.
    /// </summary>
    public BattleState BeginYard(MapDefinition map, string teacherId, string studentId, WeaponType weapon, GameContent content, RollScheme scheme = RollScheme.TwoRollAverage)
    {
        var teacher = Find(teacherId) ?? throw new ArgumentException($"no unit '{teacherId}' on the roster");
        var student = Find(studentId) ?? throw new ArgumentException($"no unit '{studentId}' on the roster");
        map = map with { EnemyLevel = student.Level };
        var played = content.Difficulties.Count > 0 ? map.Under(content.Difficulty(Difficulty)) : map;
        var battle = BattleState.From(played, content, ValueList<Unit>.From(new[] { student, teacher }), YardSeed(content), scheme) with { Rapport = Rapport, SideMap = true };
        var (studentHand, teacherHand) = YardRules.Hands(teacher, weapon);
        foreach (var unit in battle.UnitsOf(Side.Player).ToList())
        {
            battle = battle.WithUnit(unit with { Yard = unit.Id == teacherId ? teacherHand : studentHand });
        }

        return battle;
    }

    /// <summary>
    /// The record after a decided yard drill (issue 1331). Both spend their duty on it, won or lost.
    /// Whoever stands comes back as the drill left them, spells refreshed: the student with the EXP,
    /// levels and rank points it earned under the ceilings, the teacher with none (the battle never
    /// paid them). Who fell is fallen for good, or with <see cref="Permadeath"/> off back wounded,
    /// as on a side map. The drill pays nothing else.
    /// </summary>
    public ScreenResult AfterYard(BattleState end, string teacherId, string studentId, WeaponType weapon, GameContent content)
    {
        if (!end.Outcome.IsOver)
        {
            throw new InvalidOperationException("the drill is not decided");
        }

        var opening = end.History.Count > 0 ? end.History[0] : end;
        var deployed = opening.UnitsOf(Side.Player).ToDictionary(u => u.Id, u => u.Unit, StringComparer.Ordinal);
        var standing = end.Survivors().ToDictionary(u => u.Id, u => u.Unit, StringComparer.Ordinal);
        var roster = new List<Unit>();
        var fallen = Fallen.ToList();
        var lost = new List<string>();
        var wounded = new List<string>();
        var gone = new List<Unit>();
        foreach (var unit in Roster)
        {
            if (!deployed.TryGetValue(unit.Id, out var started))
            {
                roster.Add(unit);
            }
            else if (standing.TryGetValue(unit.Id, out var after))
            {
                roster.Add(BattleState.RefreshSpells(ReturnWithheld(unit, started, after, content), content));
            }
            else if (!Permadeath)
            {
                roster.Add(Wound.Inflict(unit, content.Class(unit.ClassId)));
                wounded.Add(unit.Name);
            }
            else
            {
                fallen.Add(unit.Id);
                lost.Add(unit.Name);
                gone.Add(unit);
            }
        }

        var teacher = Find(teacherId)!;
        var before = Find(studentId)!;
        var word = weapon.ToString().ToLowerInvariant();
        var line = $"{before.Name} trained under {teacher.Name}";
        if (standing.ContainsKey(studentId) && roster.FirstOrDefault(u => u.Id == studentId) is { } trained)
        {
            line += $": L{before.Level} -> L{trained.Level} (ceiling L{YardRules.LevelCeiling(teacher)}), {word} {before.Skill.Rank(weapon)} -> {trained.Skill.Rank(weapon)} (ceiling {YardRules.RankCeiling(teacher, weapon)})";
        }

        if (end.Outcome.Result != BattleResult.Won)
        {
            line += $"; the drill is lost: {Objective.Reason(end, content)}";
        }

        line += lost.Count > 0 ? $"; fallen for good: {string.Join(", ", lost)}"
            : wounded.Count > 0 ? $"; fell and came back wounded: {string.Join(", ", wounded)}"
            : "";
        var record = this with
        {
            Roster = ValueList<Unit>.From(roster),
            Fallen = ValueList<string>.From(fallen),
            FellOn = FellOnAdd(fallen.Skip(Fallen.Count), end.Map.Name),
            DrakeFlew = DrakeFlewAfter(gone),
            Duties = WithDuty(new[] { teacherId, studentId }.Where(id => !fallen.Skip(Fallen.Count).Contains(id)), Duty.Yard),
            Rapport = end.Rapport,
        };
        return new ScreenResult(record, line + ".", true);
    }
}
