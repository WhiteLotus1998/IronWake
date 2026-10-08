using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The yard's first two boards (issue 1332): the ring and the post, their drill hands and the
/// practice weapons they carry (Table round 448: a lone hand at the student's level needs three
/// hits to kill the student; round 451: a teacher five levels up is barely scratched).
/// </summary>
public class YardBoardTests
{
    private static readonly string Root = Fixture.RealContentDirectory();

    private static readonly GameContent Shipped = ContentLoader.Load(Root);

    private static MapDefinition Board(string id) => MapFiles.Load(Path.Combine(Root, MapFiles.YardDirectory, id + MapFiles.Extension), Shipped);

    [Theory]
    [InlineData("the_ring", WinCondition.Rout, 6)]
    [InlineData("the_post", WinCondition.Survive, 5)]
    public void EachYardBoardIsAShortDrillWithNoRecall(string id, WinCondition win, int limit)
    {
        var map = Board(id);

        Assert.Null(CampaignRecord.YardMapRefusal(map));
        Assert.Equal((win, limit, 0), (map.Win, map.TurnLimit, map.RecallCharges));
        Assert.All(map.Placements.OfType<EnemyPlacement>(), e => Assert.EndsWith("_hand", e.TemplateId, StringComparison.Ordinal));
    }

    [Fact]
    public void TheYardPoolIsTheRingAndThePostTakenInTurn()
    {
        var pool = Ironwake.Sim.YardRun.Pool(Root);
        var record = CampaignRecord.StartAt(Shipped, 1332, "the_mill");

        Assert.Equal(new[] { "the_post", "the_ring" }, pool);
        Assert.NotEqual(record.YardBoard(pool), (record with { MapIndex = record.MapIndex + 1 }).YardBoard(pool));
    }

    [Theory]
    [InlineData("practice_axe")]
    [InlineData("practice_lance")]
    public void APracticeWeaponIsLowMightNoCritAndNeverSold(string id)
    {
        var weapon = Shipped.Weapon(id);

        Assert.Equal((1, 0, WeaponRank.E), (weapon.Mt, weapon.Crit, weapon.Rank));
        Assert.Null(weapon.Price);
    }

    /// <summary>
    /// Every main member but Pell (a caster, Def 2, who drills where a hand reaches it at its peril) at
    /// levels 1, 3, 5 and 7, under the captain five levels up, on the ring: a lone hand needs three
    /// hits or more to kill the student, and takes no more than a fifth of the teacher's HP a hit.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    public void ALoneDrillHandNeedsThreeHitsOnTheStudentAndBarelyScratchesTheTeacher(int level)
    {
        var start = CampaignRecord.StartAt(Shipped, 1332, "the_mill");
        foreach (var student in start.Roster.Where(u => u.Id is not ("captain" or "pell")))
        {
            var scaled = student.ScaledTo(level, Shipped.Class(student.ClassId));
            var captain = start.Find("captain")!;
            var teacher = captain.ScaledTo(level + 5, Shipped.Class(captain.ClassId));
            var record = start with { Roster = ValueList<Unit>.From(start.Roster.Select(u => u.Id == scaled.Id ? scaled : u.Id == "captain" ? teacher : u)) };
            var state = record.BeginYard(Board("the_ring"), "captain", scaled.Id, WeaponType.Sword, Shipped);
            var s = state.Units.Single(u => u.Id == scaled.Id);
            var t = state.Units.Single(u => u.Id == "captain");
            foreach (var hand in state.UnitsOf(Side.Enemy))
            {
                var onStudent = Struck(state, hand, s);
                var onTeacher = Struck(state, hand, t);

                Assert.True(onStudent * 2 < s.Hp, $"{hand.Id} kills {s.Id} at L{level} in two hits of {onStudent} against {s.Hp} HP");
                Assert.True(onTeacher * 5 <= t.Hp, $"{hand.Id} strikes the L{level + 5} teacher for {onTeacher} of {t.Hp} HP");
            }
        }
    }

    /// <summary>What <paramref name="hand"/> strikes <paramref name="target"/> for, a round's worth, standing beside it.</summary>
    private static int Struck(BattleState state, BattleUnit hand, BattleUnit target)
    {
        var beside = new Coord(target.At.X, target.At.Y - 1);
        var moved = state.WithUnit(hand with { At = beside });
        var forecast = Queries.Forecast(moved, Shipped, moved.Units.Single(u => u.Id == hand.Id), target)!;
        return forecast.Attacker.Damage * (forecast.Attacker.Doubles ? 2 : 1);
    }
}
