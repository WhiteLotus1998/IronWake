using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// Gate 4's yard arm (issue 1331, slice 3): the heuristic's campaign (<see cref="LevelRun.Measure"/>)
/// with one drill at each camp. After each won map's levy drill the heuristic camp sends its lowest
/// unit to the yard under the strongest teacher who can raise it (<see cref="Pick"/>), plays the
/// drill on the pool's board in turn with <see cref="HeuristicPlayer"/>, and carries the record on.
/// <see cref="Arm.NoPull"/> plays the same drills with the teacher's pull off, the pull's ablation
/// (Table round 448: a flag the Sim alone holds). A measurement only; nothing here changes what ships.
/// </summary>
public static class YardRun
{
    /// <summary>The campaign's camps without the yard (today's heuristic), with it, and with it and the pull off.</summary>
    public enum Arm
    {
        Off,
        On,
        NoPull,
    }

    /// <summary>The arm's name as printed.</summary>
    public static string Name(Arm arm) => arm switch
    {
        Arm.Off => "no yard",
        Arm.On => "yard",
        _ => "yard, no pull",
    };

    /// <summary>One drill the heuristic camp fought: who, in what, won or lost, who fell, and the levels the student took.</summary>
    public sealed record Drill(int Map, string TeacherId, string StudentId, WeaponType Weapon, bool Won, bool StudentFell, bool TeacherFell, int LevelsTaken);

    /// <summary>
    /// The drill the heuristic camp names on <paramref name="record"/>, or null when none can be
    /// fought: the student is the lowest-level unit standing, the teacher the highest-level one the
    /// record lets teach it (<see cref="CampaignRecord.YardRefusal"/>), in the student's main weapon
    /// (<see cref="LevelRun.MainType"/>) where the teacher uses it, else the first weapon both use.
    /// Roster order breaks every tie. A student no one can raise passes to the next lowest.
    /// </summary>
    public static (string Teacher, string Student, WeaponType Weapon)? Pick(CampaignRecord record, GameContent content)
    {
        var present = record.Present(content).Select((u, i) => (Unit: u, Order: i)).ToList();
        foreach (var student in present.OrderBy(s => s.Unit.Level).ThenBy(s => s.Order).Select(s => s.Unit))
        {
            var main = LevelRun.MainType(student, content);
            var weapons = new[] { main }.Concat(Enum.GetValues<WeaponType>().Where(t => t != main)).ToList();
            foreach (var teacher in present.OrderByDescending(t => t.Unit.Level).ThenBy(t => t.Order).Select(t => t.Unit))
            {
                foreach (var weapon in weapons)
                {
                    if (record.YardRefusal(teacher.Id, student.Id, weapon.ToString(), content) is null)
                    {
                        return (teacher.Id, student.Id, weapon);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>The yard's pool under <paramref name="contentRoot"/>, as the camp screen reads it: board ids in ordinal order.</summary>
    public static IReadOnlyList<string> Pool(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, MapFiles.YardDirectory);
        return Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*" + MapFiles.Extension).Select(f => Path.GetFileNameWithoutExtension(f)!).OrderBy(id => id, StringComparer.Ordinal).ToList()
            : new List<string>();
    }

    /// <summary>
    /// The camp after the heuristic's drill on <paramref name="record"/>, and the drill, or the record
    /// unchanged and null when <see cref="Pick"/> names none or the pool is empty. With
    /// <paramref name="pull"/> off the teacher's hand does not pull (the ablation arm).
    /// </summary>
    public static (CampaignRecord Record, Drill? Drill) Camp(CampaignRecord record, string contentRoot, GameContent content, bool pull = true)
    {
        if (Pick(record, content) is not { } pick || record.YardBoard(Pool(contentRoot)) is not { } board)
        {
            return (record, null);
        }

        var map = MapFiles.Load(Path.Combine(contentRoot, MapFiles.YardDirectory, board + MapFiles.Extension), content);
        var state = record.BeginYard(map, pick.Teacher, pick.Student, pick.Weapon, content);
        if (!pull)
        {
            var teacher = state.Units.Single(u => u.Id == pick.Teacher);
            state = state.WithUnit(teacher with { Yard = teacher.Yard! with { Pulls = false } });
        }

        var player = new YardPlayer(pick.Teacher, pick.Student);
        while (!state.Outcome.IsOver)
        {
            var commands = state.Phase == Side.Player ? player.Next(state, content) : EnemyAi.Plan(state, content);
            foreach (var command in commands)
            {
                var result = Resolver.Apply(state, content, command);
                if (!result.Accepted)
                {
                    throw new InvalidOperationException($"yard {board} map {record.MapIndex}: {command} was rejected: {result.Rejection!.Message}");
                }

                state = result.Next;
                if (state.Outcome.IsOver)
                {
                    break;
                }
            }
        }

        var before = record.Find(pick.Student)!.Level;
        var after = record.AfterYard(state, pick.Teacher, pick.Student, pick.Weapon, content).Record;
        var standing = state.Survivors().Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        var drill = new Drill(
            record.MapIndex,
            pick.Teacher,
            pick.Student,
            pick.Weapon,
            state.Outcome.Result == BattleResult.Won,
            !standing.Contains(pick.Student),
            !standing.Contains(pick.Teacher),
            standing.Contains(pick.Student) ? (after.Find(pick.Student)?.Level ?? before) - before : 0);
        return (after, drill);
    }

    /// <summary>
    /// The arm's drill tally over <paramref name="runs"/>, teacher falls first (Table rounds 451 to 453):
    /// the drills a teacher fell in, the runs with a first fall and the falls after it in the same run,
    /// then drills won, lost on the clock (lost with the student standing), students fallen, and the
    /// levels the students took, total and per drill won.
    /// </summary>
    public static string TallyLine(Arm arm, IReadOnlyList<LevelRun.Run> runs)
    {
        var drills = runs.SelectMany(r => r.Drills).ToList();
        if (arm == Arm.Off)
        {
            return $"  {Name(arm)}: {runs.Count} runs, no drills";
        }

        var won = drills.Count(d => d.Won);
        var levels = drills.Sum(d => d.LevelsTaken);
        var perWon = won == 0 ? "none won" : $"{(double)levels / won:F2} a drill won";
        var firstFalls = runs.Count(r => r.Drills.Any(d => d.TeacherFell));
        var afterFirst = runs.Sum(r => r.Drills.SkipWhile(d => !d.TeacherFell).Skip(1).Count(d => d.TeacherFell));
        return $"  {Name(arm)}: teacher fell in {drills.Count(d => d.TeacherFell)} of {drills.Count} drills; first falls {firstFalls} of {runs.Count} runs, {afterFirst} more after a first; won {won}, lost on the clock {drills.Count(d => !d.Won && !d.StudentFell)}, student fell {drills.Count(d => d.StudentFell)}, levels taken {levels} ({perWon})";
    }
}

/// <summary>
/// The heuristic camp's drill player (issue 1332, Table rounds 451 and 452): the number-free soften
/// rule. When the student can finish a hand this phase as it stands, the student acts first, as
/// <see cref="HeuristicPlayer"/> plays it. Otherwise the teacher acts first and strikes a hand only
/// where the forecast says the student can then reach that hand and finish it this phase, every
/// strike landing (<see cref="Soften"/>); a teacher's blow that finishes nothing is no soften. Failing
/// that, the teacher screens (<see cref="Screen"/>). The student, and anything the teacher is owed
/// after, plays as the heuristic plays. With the pull off (the ablation arm) the same rule runs, so a
/// teacher whose blow would kill strikes only a hand it cannot kill.
/// </summary>
public sealed class YardPlayer : IPlayer
{
    private readonly HeuristicPlayer _heuristic = new();
    private readonly string _teacher;
    private readonly string _student;

    public YardPlayer(string teacherId, string studentId)
    {
        _teacher = teacherId;
        _student = studentId;
    }

    public IReadOnlyList<Command> Next(BattleState state, GameContent content)
    {
        if (state.Outcome.IsOver || state.Phase != Side.Player
            || state.UnitsOf(Side.Player).FirstOrDefault(u => u.Id == _teacher) is not { Acted: false } teacher
            || state.UnitsOf(Side.Player).FirstOrDefault(u => u.Id == _student) is not { } student
            || state.UnitsOf(Side.Player).Any(u => state.CantoReachOf(u, content) is not null))
        {
            return _heuristic.Next(state, content);
        }

        if (!student.Acted && state.UnitsOf(Side.Enemy).Any(hand => Finishes(state, content, student, hand, hand.Hp, null)))
        {
            return HeuristicPlayer.PlanUnit(state, content, student);
        }

        return Soften(state, content, teacher, student) ?? Screen(state, content, teacher, student);
    }

    /// <summary>
    /// The teacher's soften: a move and a strike on the hand the student can then finish this phase,
    /// the blow that leaves it lowest first, then the tile nearest the student, row-major order
    /// breaking ties; or null when no strike leaves a hand the student can finish.
    /// </summary>
    public static IReadOnlyList<Command>? Soften(BattleState state, GameContent content, BattleUnit teacher, BattleUnit student)
    {
        (int Left, int Near, Coord Tile, string Hand, int Slot)? best = null;
        var equipped = teacher.EquippedSlot(content);
        foreach (var tile in state.ReachOf(teacher, content).Destinations)
        {
            foreach (var (slot, weapon, _) in HeuristicPlayer.Arms(content, teacher))
            {
                foreach (var hand in state.UnitsOf(Side.Enemy))
                {
                    if (!weapon.InRange(tile.DistanceTo(hand.At)) || Queries.Forecast(state, content, teacher, hand, tile, slot) is not { } forecast)
                    {
                        continue;
                    }

                    var blow = forecast.Attacker.Damage * forecast.Attacker.StrikesPerRound * (forecast.Attacker.Doubles ? 2 : 1);
                    var floor = teacher.Yard is { Teaches: true, Pulls: true } ? 1 : 0;
                    var left = Math.Max(floor, hand.Hp - blow);
                    if (left == hand.Hp || left == 0 || !Finishes(state, content, student, hand, left, tile))
                    {
                        continue;
                    }

                    var option = (left, tile.DistanceTo(student.At), tile, hand.Id, slot);
                    if (best is not { } held || left < held.Left || (left == held.Left && option.Item2 < held.Near))
                    {
                        best = option;
                    }
                }
            }
        }

        if (best is not { } b)
        {
            return null;
        }

        var attack = new Attack(teacher.Id, b.Hand, b.Slot == equipped ? null : b.Slot);
        return b.Tile == teacher.At ? new Command[] { attack } : new Command[] { new Move(teacher.Id, b.Tile), attack };
    }

    /// <summary>
    /// The teacher's screen: it ends on the free tile beside the student nearest the hand nearest the
    /// student, else on the tile of its reach nearest the student, row-major order breaking ties, and waits.
    /// </summary>
    public static IReadOnlyList<Command> Screen(BattleState state, GameContent content, BattleUnit teacher, BattleUnit student)
    {
        var hands = state.UnitsOf(Side.Enemy).ToList();
        var threat = hands.Count == 0 ? student.At : hands.OrderBy(h => h.At.DistanceTo(student.At)).First().At;
        var tiles = state.ReachOf(teacher, content).Destinations.ToList();
        var beside = tiles.Where(t => t.DistanceTo(student.At) == 1).ToList();
        var tile = beside.Count > 0
            ? beside.OrderBy(t => t.DistanceTo(threat)).First()
            : tiles.OrderBy(t => t.DistanceTo(student.At)).First();
        var wait = new Wait(teacher.Id);
        return tile == teacher.At ? new Command[] { wait } : new Command[] { new Move(teacher.Id, tile), wait };
    }

    /// <summary>
    /// Whether <paramref name="student"/> can end on a tile of its reach other than <paramref name="taken"/>
    /// and take <paramref name="hand"/> from <paramref name="hp"/> to 0 with one round, every strike landing.
    /// </summary>
    public static bool Finishes(BattleState state, GameContent content, BattleUnit student, BattleUnit hand, int hp, Coord? taken)
    {
        foreach (var tile in state.ReachOf(student, content).Destinations)
        {
            if (tile == taken)
            {
                continue;
            }

            foreach (var (slot, weapon, _) in HeuristicPlayer.Arms(content, student))
            {
                if (weapon.InRange(tile.DistanceTo(hand.At))
                    && Queries.Forecast(state, content, student, hand, tile, slot) is { } forecast
                    && forecast.Attacker.Damage * forecast.Attacker.StrikesPerRound * (forecast.Attacker.Doubles ? 2 : 1) >= hp)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
