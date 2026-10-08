using Ironwake.Content;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Gate 4's yard arm (issue 1331, slice 3): the Sim's heuristic camp sends its lowest unit to the
/// strongest teacher who can raise it, plays the drill, and carries the record on.
/// </summary>
public class YardArmTests
{
    private static readonly string Root = Fixture.RealContentDirectory();

    private static readonly GameContent Shipped = ContentLoader.Load(Root);

    /// <summary>A record before the mill with every unit at <paramref name="others"/>, the captain at <paramref name="captain"/> and Wren at <paramref name="wren"/>, permadeath off as <see cref="LevelRun.Measure"/> runs it.</summary>
    private static CampaignRecord Camp(int captain = 6, int wren = 2, int others = 4)
    {
        var record = CampaignRecord.StartAt(Shipped, 1331, "the_mill");
        return record with
        {
            Permadeath = false,
            Roster = ValueList<Unit>.From(record.Roster.Select(u => u with
            {
                Level = u.Id == "captain" ? captain : u.Id == "wren" ? wren : others,
                Exp = 0,
            })),
        };
    }

    [Fact]
    public void TheYardArmSendsTheLowestUnitToTheStrongestTeacherWhoCanRaiseIt()
    {
        var record = Camp();

        var pick = YardRun.Pick(record, Shipped);

        Assert.NotNull(pick);
        Assert.Equal(("captain", "wren"), (pick.Value.Teacher, pick.Value.Student));
        Assert.Null(record.YardRefusal(pick.Value.Teacher, pick.Value.Student, pick.Value.Weapon.ToString(), Shipped));
    }

    [Fact]
    public void TheYardArmPassesOverAWoundedStudent()
    {
        var record = Camp();
        record = record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == "wren" ? Wound.Inflict(u, Shipped.Class(u.ClassId)) : u)) };

        var pick = YardRun.Pick(record, Shipped);

        Assert.NotNull(pick);
        Assert.NotEqual("wren", pick.Value.Student);
        Assert.NotEqual("wren", pick.Value.Teacher);
    }

    [Fact]
    public void TheYardArmNamesNoDrillWhenNobodyCanRaiseAnyone()
    {
        var record = Camp(captain: 1, wren: 1, others: 1);
        record = record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u with { Skill = Enum.GetValues<WeaponType>().Aggregate(u.Skill, (skill, t) => skill.With(t, 0)) })) };

        Assert.Null(YardRun.Pick(record, Shipped));
        Assert.Equal((record, (YardRun.Drill?)null), YardRun.Camp(record, Root, Shipped));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheYardArmPlaysTheDrillAndSpendsBothDuties(bool pull)
    {
        var record = Camp();

        var (after, drill) = YardRun.Camp(record, Root, Shipped, pull);

        Assert.NotNull(drill);
        Assert.Equal(("captain", "wren"), (drill.TeacherId, drill.StudentId));
        foreach (var id in new[] { drill.TeacherId, drill.StudentId })
        {
            Assert.Equal(Duty.Yard, after.DutyOf(id));
        }

        Assert.Equal(drill.StudentFell ? 0 : after.Find("wren")!.Level - 2, drill.LevelsTaken);
    }

    [Fact]
    public void TheTallyReadsTeacherFallsFirstPerDrillAndFirstFallsPerRun()
    {
        var drills = new[]
        {
            new YardRun.Drill(1, "captain", "wren", WeaponType.Sword, true, false, false, 1),
            new YardRun.Drill(2, "captain", "wren", WeaponType.Sword, false, false, false, 0),
            new YardRun.Drill(3, "captain", "wren", WeaponType.Sword, false, true, true, 0),
            new YardRun.Drill(4, "captain", "wren", WeaponType.Sword, false, true, true, 0),
        };
        var run = new LevelRun.Run([], null) { Drills = drills };
        var clean = new LevelRun.Run([], null) { Drills = drills[..2] };

        Assert.Equal("  yard: teacher fell in 2 of 6 drills; first falls 1 of 2 runs, 1 more after a first; won 2, lost on the clock 2, student fell 2, levels taken 2 (1.00 a drill won)", YardRun.TallyLine(YardRun.Arm.On, new[] { run, clean }));
        Assert.Equal("  no yard: 1 runs, no drills", YardRun.TallyLine(YardRun.Arm.Off, new[] { new LevelRun.Run([], null) }));
    }

    /// <summary>A lane: the student (Wren) at 1,1, the teacher (the captain) at 3,1, one axe hand at <paramref name="hand"/>.</summary>
    private static BattleState Lane(string hand, int handHp = 0)
    {
        var map = MapFormat.Parse("lane.map", $"""
            name: Lane
            size: 9x3
            win: rout
            turn_limit: 6
            recall: 0
            enemy_level: 1

            .........
            .........
            .........

            units:
            P captain 1,1
            P recruit 3,1
            E axe_hand {hand} group:hands behavior:hold

            """, Shipped);
        var state = Camp().BeginYard(map, "captain", "wren", WeaponType.Sword, Shipped);
        if (handHp > 0)
        {
            var foe = state.UnitsOf(Side.Enemy).Single();
            state = state.WithUnit(foe with { Hp = handHp });
        }

        return state;
    }

    private static BattleUnit Unit(BattleState state, string id) => state.Units.Single(u => u.Id == id);

    [Fact]
    public void TheYardTeacherSoftensAHandTheStudentCanReachAndFinishThisPhase()
    {
        var state = Lane("5,1");

        var plan = new YardPlayer("captain", "wren").Next(state, Shipped);

        Assert.Equal(new Attack("captain", "axe_hand-1"), plan.OfType<Attack>().Single());
        Assert.True(YardPlayer.Finishes(state, Shipped, Unit(state, "wren"), Unit(state, "axe_hand-1"), 1, null));
    }

    [Fact]
    public void TheYardTeacherScreensWhenTheStudentCannotReachTheHand()
    {
        var state = Lane("8,1");
        var far = state.WithUnit(Unit(state, "wren") with { At = new Coord(0, 0) });
        var reach = far.ReachOf(Unit(far, "wren"), Shipped).Destinations.ToList();
        Assert.DoesNotContain(reach, t => t.DistanceTo(new Coord(8, 1)) <= 1);

        var plan = new YardPlayer("captain", "wren").Next(far, Shipped);

        Assert.Empty(plan.OfType<Attack>());
        Assert.Equal(new Wait("captain"), plan[^1]);
        Assert.Equal(1, plan.OfType<Move>().Single().To.DistanceTo(new Coord(0, 0)));
    }

    [Fact]
    public void TheYardStudentActsFirstWhenItCanFinishAHandNow()
    {
        var state = Lane("5,1", handHp: 1);

        var plan = new YardPlayer("captain", "wren").Next(state, Shipped);

        Assert.Equal(new Attack("wren", "axe_hand-1"), plan.OfType<Attack>().Single() with { Slot = null });
    }
}
