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

        var player = new HeuristicPlayer();
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
    /// The arm's drill tally over <paramref name="runs"/>: drills fought, won, students and teachers
    /// fallen, drills lost on the clock (lost with the student standing), and the levels the students
    /// took, total and per drill won.
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
        return $"  {Name(arm)}: {runs.Count} runs, {drills.Count} drills, won {won}, lost on the clock {drills.Count(d => !d.Won && !d.StudentFell)}, student fell {drills.Count(d => d.StudentFell)}, teacher fell {drills.Count(d => d.TeacherFell)}, levels taken {levels} ({perWon})";
    }
}
