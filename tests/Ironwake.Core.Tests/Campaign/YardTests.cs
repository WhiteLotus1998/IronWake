using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// The yard (issue 1331, Lotus's approval on round 433): between maps each unit takes one duty; a
/// drill takes a teacher and a student, both spent; the student trains no higher than the teacher's
/// level less one, nor past the teacher's rank in the taught weapon; the teacher earns nothing.
/// </summary>
public class YardTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private const string Board = """
        name: Drill Board
        size: 6x3
        win: rout
        turn_limit: 6
        recall: 0
        enemy_level: 9

        ......
        ......
        ......

        units:
        P captain 0,1
        P recruit 1,1
        E brigand 5,0 group:hands behavior:hold
        E brigand 5,2 group:hands behavior:hold
        """;

    private static MapDefinition Map => MapFixture.Parse(Board);

    /// <summary>A record before the mill with the captain at <paramref name="captainLevel"/> and sword <paramref name="captainSword"/> points, Wren at <paramref name="wrenLevel"/>.</summary>
    private static CampaignRecord Camp(int captainLevel = 6, int captainSword = 30, int wrenLevel = 2, int wrenSword = 0)
    {
        var record = CampaignRecord.StartAt(Content, 1331, "the_mill");
        return record with
        {
            Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id switch
            {
                "captain" => u with { Level = captainLevel, Exp = 0, Skill = u.Skill.With(WeaponType.Sword, captainSword) },
                "wren" => u with { Level = wrenLevel, Exp = 0, Skill = u.Skill.With(WeaponType.Sword, wrenSword) },
                _ => u,
            })),
        };
    }

    private static CampaignRecord Wounded(CampaignRecord record, string id) =>
        record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == id ? Wound.Inflict(u, Content.Class(u.ClassId)) : u)) };

    /// <summary>The drill's opening with brigand-1 moved beside <paramref name="unitId"/> at full HP.</summary>
    private static BattleState Beside(BattleState state, string unitId)
    {
        var unit = state.Units.Single(u => u.Id == unitId);
        var foe = state.Units.Single(u => u.Id == "brigand-1");
        return state.WithUnit(foe with { At = new Coord(unit.At.X, unit.At.Y - 1) });
    }

    [Theory]
    [InlineData(4, 50, 200, 5, 5, 99)]
    [InlineData(4, 50, 30, 5, 4, 80)]
    [InlineData(4, 50, 60, 5, 5, 10)]
    [InlineData(5, 0, 120, 5, 5, 99)]
    [InlineData(4, 50, 60, Unit.MaxLevel, 5, 10)]
    public void AStudentTakesNoLevelPastTheCeilingAndKeepsTheExpUpTo99(int level, int exp, int amount, int ceiling, int levelAfter, int expAfter)
    {
        var unit = Content.Unit("wren") with { Level = level, Exp = exp };

        var after = unit.GainExp(amount, Content.Class(unit.ClassId), new KeyedRng(7), ceiling).Unit;

        Assert.Equal((levelAfter, expAfter), (after.Level, after.Exp));
    }

    [Theory]
    [InlineData(10, WeaponRank.E, 10)]
    [InlineData(40, WeaponRank.E, 29)]
    [InlineData(79, WeaponRank.D, 79)]
    [InlineData(85, WeaponRank.D, 79)]
    [InlineData(500, WeaponRank.A, 449)]
    [InlineData(500, WeaponRank.S, 500)]
    public void TheTaughtWeaponsPointsStopOneShortOfTheRankAboveTheTeachers(int points, WeaponRank ceiling, int capped)
    {
        Assert.Equal(capped, YardRules.CapPoints(points, ceiling));
    }

    [Fact]
    public void TheStudentsLevelCeilingIsTheTeachersLevelLessOne()
    {
        Assert.Equal(5, YardRules.LevelCeiling(Content.Unit("captain") with { Level = 6 }));
        Assert.Equal(Unit.MinLevel, YardRules.LevelCeiling(Content.Unit("captain") with { Level = 1 }));
    }

    [Fact]
    public void AUnitNoCommandNamesRests()
    {
        Assert.Equal(Duty.Rest, Camp().DutyOf("wren"));
    }

    [Fact]
    public void EachUnitTakesOneDutyACamp()
    {
        var forge = Camp().AssignDuty("wren", "forge");
        Assert.True(forge.Accepted, forge.Text);
        Assert.Equal(Duty.Forge, forge.Record.DutyOf("wren"));

        var rest = forge.Record.AssignDuty("wren", "rest");
        Assert.False(rest.Accepted);
        Assert.Equal("Wren took the forge duty at this camp; one duty a unit", rest.Text);

        var yard = forge.Record.YardRefusal("captain", "wren", "sword", Content);
        Assert.Equal("Wren took the forge duty at this camp; one duty a unit", yard);
    }

    [Fact]
    public void TheQuestAndYardDutiesAreTakenByTheirOwnCommands()
    {
        Assert.Equal("a side map's party takes its duty there: quest <id> <ally>...", Camp().AssignDuty("wren", "quest").Text);
        Assert.Equal("the yard takes a teacher and a student: yard <teacher> <student> <weapon>", Camp().AssignDuty("wren", "yard").Text);
        Assert.False(Camp().AssignDuty("wren", "nap").Accepted);
    }

    [Fact]
    public void AWoundedUnitIsRefusedTheYardAloneAndTakesTheForgeOrRest()
    {
        var record = Wounded(Camp(), "wren");

        Assert.True(record.AssignDuty("wren", "forge").Accepted);
        Assert.True(record.AssignDuty("wren", "rest").Accepted);
        Assert.EndsWith("a wounded unit is refused the yard", record.YardRefusal("captain", "wren", "sword", Content));
        Assert.EndsWith("a wounded unit is refused the yard", Wounded(Camp(), "captain").YardRefusal("captain", "wren", "sword", Content));
    }

    [Fact]
    public void TheYardRefusesADrillThatCanRaiseNothing()
    {
        Assert.Null(Camp(captainLevel: 6, wrenLevel: 2).YardRefusal("captain", "wren", "sword", Content));
        Assert.Null(Camp(captainLevel: 3, wrenLevel: 2, captainSword: 80, wrenSword: 30).YardRefusal("captain", "wren", "sword", Content));
        Assert.Equal(
            "Wren stands at Alder Fenn's ceiling already: L2, sword D",
            Camp(captainLevel: 3, wrenLevel: 2, captainSword: 30, wrenSword: 30).YardRefusal("captain", "wren", "sword", Content));
    }

    [Fact]
    public void TheYardRefusesAWeaponEitherClassDoesNotUseAndAUnitTeachingItself()
    {
        Assert.Equal("Alder Fenn is a Cadet and does not use the bow", Camp().YardRefusal("captain", "wren", "bow", Content));
        Assert.StartsWith("no weapon 'spoon'", Camp().YardRefusal("captain", "wren", "spoon", Content));
        Assert.StartsWith("Alder Fenn cannot teach themselves", Camp().YardRefusal("captain", "captain", "sword", Content));
    }

    [Fact]
    public void TheDrillSeatsTheStudentInTheCaptainSlotWithEnemiesAtTheStudentsLevel()
    {
        var state = Camp().BeginYard(Map, "captain", "wren", WeaponType.Sword, Content);

        var wren = state.Units.Single(u => u.Id == "wren");
        var captain = state.Units.Single(u => u.Id == "captain");
        Assert.True(wren.IsCaptain);
        Assert.Equal(new YardHand(false, 5, WeaponType.Sword, WeaponRank.D), wren.Yard);
        Assert.Equal(new YardHand(true, 5, WeaponType.Sword, WeaponRank.D), captain.Yard);
        Assert.All(state.UnitsOf(Side.Enemy), e => Assert.Equal(2, e.Unit.Level));
    }

    [Fact]
    public void TheTeacherEarnsNoExpNoRankPointsAndNoMasteryFromTheDrill()
    {
        var state = Beside(Camp().BeginYard(Map, "captain", "wren", WeaponType.Sword, Content), "captain");
        var before = state.Units.Single(u => u.Id == "captain").Unit;

        var taught = Resolver.Apply(state, Content, new Attack("captain", "brigand-1"));
        var plain = Resolver.Apply(state.WithUnit(state.Units.Single(u => u.Id == "captain") with { Yard = null }), Content, new Attack("captain", "brigand-1"));

        Assert.Null(taught.Rejection);
        var after = taught.Next.Units.Single(u => u.Id == "captain").Unit;
        Assert.Equal((before.Level, before.Exp, before.Skill, before.Mastery), (after.Level, after.Exp, after.Skill, after.Mastery));
        Assert.DoesNotContain(taught.Events, e => e is ExpGained { UnitId: "captain" });
        Assert.Contains(plain.Events, e => e is ExpGained { UnitId: "captain" });
    }

    /// <summary>The drill's opening with brigand-1 beside <paramref name="unitId"/> at <paramref name="hp"/> HP, rolled from <paramref name="seed"/>.</summary>
    private static BattleState Softened(string unitId, int hp, ulong seed)
    {
        var state = Beside(Camp().BeginYard(Map, "captain", "wren", WeaponType.Sword, Content), unitId) with { Seed = seed };
        return state.WithUnit(state.Units.Single(u => u.Id == "brigand-1") with { Hp = hp });
    }

    [Fact]
    public void TheTeachersBlowsPullADrillHandAtOneHp()
    {
        var seeds = Enumerable.Range(1, 40).Select(s => (ulong)s).ToList();
        var killing = seeds.Where(seed => Resolver.Apply(Softened("captain", 3, seed).WithUnit(Softened("captain", 3, seed).Units.Single(u => u.Id == "captain") with { Yard = null }), Content, new Attack("captain", "brigand-1"))
            .Events.Any(e => e is UnitDied { UnitId: "brigand-1" })).ToList();
        Assert.NotEmpty(killing);

        foreach (var seed in killing)
        {
            var taught = Resolver.Apply(Softened("captain", 3, seed), Content, new Attack("captain", "brigand-1"));

            Assert.Null(taught.Rejection);
            Assert.DoesNotContain(taught.Events, e => e is UnitDied { UnitId: "brigand-1" });
            Assert.Equal(1, taught.Next.Units.Single(u => u.Id == "brigand-1").Hp);
            var fought = taught.Events.OfType<CombatFought>().Single();
            Assert.Equal(2, fought.Strikes.Where(s => s.AttackerId == "captain").Sum(s => s.Damage));
        }
    }

    [Fact]
    public void TheStudentsBlowsAreNotPulled()
    {
        var state = Softened("wren", 1, 1);

        Assert.False(state.Units.Single(u => u.Id == "wren").ToCombatant(state, Content).Pulls);
        Assert.True(state.Units.Single(u => u.Id == "captain").ToCombatant(state, Content).Pulls);
    }

    [Fact]
    public void ATeachersHandPullsUnlessTheSimsAblationArmTurnsItOff()
    {
        var state = Softened("wren", 1, 1);
        var captain = state.Units.Single(u => u.Id == "captain");

        Assert.True(captain.Yard!.Pulls);
        var ablated = state.WithUnit(captain with { Yard = captain.Yard with { Pulls = false } });
        Assert.False(ablated.Units.Single(u => u.Id == "captain").ToCombatant(ablated, Content).Pulls);
    }

    [Fact]
    public void ATeachersCounterPullsToo()
    {
        var seeds = Enumerable.Range(1, 40).Select(s => (ulong)s).ToList();
        var countered = 0;
        foreach (var seed in seeds)
        {
            var state = Softened("captain", 3, seed);
            var struck = state.Units.Single(u => u.Id == "brigand-1");
            var result = CombatResolver.Resolve(
                struck.ToCombatant(state, Content, against: state.Find("captain")),
                state.Find("captain")!.Answering(state, Content, struck.At, struck),
                1,
                new CombatContext(1, Side.Enemy),
                new KeyedRng(seed),
                state.Scheme);
            if (result.Strikes.Any(s => s.AttackerId == "captain" && s.Hit))
            {
                countered++;
                Assert.Equal(1, result.AttackerHp);
            }
        }

        Assert.True(countered > 0);
    }

    [Fact]
    public void TheForecastSaysTheTeacherPulls()
    {
        var state = Softened("captain", 3, 1);
        var captain = state.Find("captain")!;
        var brigand = state.Find("brigand-1")!;
        var forecast = Ironwake.Core.Combat.Forecast(captain.ToCombatant(state, Content, against: brigand), brigand.Answering(state, Content, captain.At, captain), 1, state.Scheme);
        var names = UnitNames.Of(state, Content);

        Assert.Equal($"  pulls: {names["captain"]} stops at 1 HP on {names["brigand-1"]}; the kill is the student's", Ironwake.Cli.PlaySession.PullLine(captain, brigand, forecast, names));
        Assert.Null(Ironwake.Cli.PlaySession.PullLine(captain with { Yard = null }, brigand, forecast, names));
    }

    [Fact]
    public void TheStudentsTaughtWeaponStopsUnderTheTeachersRank()
    {
        var state = Beside(Camp(captainSword: 30, wrenSword: 79).BeginYard(Map, "captain", "wren", WeaponType.Sword, Content), "wren");

        var taught = Resolver.Apply(state, Content, new Attack("wren", "brigand-1"));
        var plain = Resolver.Apply(state.WithUnit(state.Units.Single(u => u.Id == "wren") with { Yard = null }), Content, new Attack("wren", "brigand-1"));

        Assert.Null(taught.Rejection);
        Assert.Equal(79, taught.Next.Units.Single(u => u.Id == "wren").Unit.Skill.Points(WeaponType.Sword));
        Assert.True(plain.Next.Units.Single(u => u.Id == "wren").Unit.Skill.Points(WeaponType.Sword) >= 80);
    }

    [Fact]
    public void BothTeacherAndStudentSpendTheirDutyOnTheDrillAndTheLineNamesTheCeilings()
    {
        var record = Camp();
        var opening = record.BeginYard(Map, "captain", "wren", WeaponType.Sword, Content);
        var end = opening with
        {
            Units = ValueList<BattleUnit>.From(opening.UnitsOf(Side.Player)),
            History = ValueList<BattleState>.Of(opening),
        };

        var after = record.AfterYard(end, "captain", "wren", WeaponType.Sword, Content);

        Assert.True(after.Accepted);
        Assert.Equal("Wren trained under Alder Fenn: L2 -> L2 (ceiling L5), sword E -> E (ceiling D).", after.Text);
        Assert.Equal((Duty.Yard, Duty.Yard), (after.Record.DutyOf("captain"), after.Record.DutyOf("wren")));
        Assert.Equal("Wren drilled in the yard at this camp already; one duty a unit", after.Record.YardRefusal("teodor", "wren", "sword", Content));
        Assert.Equal("Alder Fenn took the yard duty at this camp; one duty a unit", after.Record.AssignDuty("captain", "forge").Text);
    }

    [Fact]
    public void ASideMapRefusesAPartyMemberWhoTookAnotherDuty()
    {
        var record = CampaignRecord.StartAt(Content, 1331, "the_tollgate");
        Assert.Null(record.QuestRefusal("maud_1", "wren", Content));

        var forge = record.AssignDuty("wren", "forge").Record;

        Assert.Equal("Wren took the forge duty at this camp; one duty a unit", forge.QuestRefusal("maud_1", "wren", Content));
        Assert.Equal("Maud took the forge duty at this camp; one duty a unit", record.AssignDuty("maud", "forge").Record.QuestRefusal("maud_1", "wren", Content));
    }

    [Fact]
    public void AStudentWhoFallsInTheDrillIsFallenForGood()
    {
        var record = Camp();
        var opening = record.BeginYard(Map, "captain", "wren", WeaponType.Sword, Content);
        var end = opening with
        {
            Units = ValueList<BattleUnit>.From(opening.Units.Where(u => u.Id != "wren")),
            History = ValueList<BattleState>.Of(opening),
        };

        var after = record.AfterYard(end, "captain", "wren", WeaponType.Sword, Content);

        Assert.Contains("wren", after.Record.Fallen);
        Assert.EndsWith("; fallen for good: Wren.", after.Text);
    }

    [Fact]
    public void TheDutiesRoundTripThroughTheProtocolRecord()
    {
        var record = Camp().AssignDuty("wren", "forge").Record;

        var back = ProtocolJson.ReadCampaign(ProtocolJson.Campaign(record), Content);

        Assert.Equal(record.Duties, back.Duties);
        Assert.Contains("\"duties\":[{\"unit\":\"wren\",\"duty\":\"forge\"}]", ProtocolJson.Campaign(record));
    }

    [Fact]
    public void AYardBoardTakesOneCaptainSlotAndOneRecruitSlot()
    {
        Assert.Null(CampaignRecord.YardMapRefusal(Map));
        Assert.NotNull(CampaignRecord.YardMapRefusal(MapFixture.Parse(Board.Replace("P recruit 1,1\n", ""))));
    }

    [Fact]
    public void TheYardsBoardsAreTakenInTurnByTheNextMapsIndex()
    {
        var record = Camp();

        Assert.Null(record.YardBoard(Array.Empty<string>()));
        Assert.Equal(new[] { "a", "b" }[record.MapIndex % 2], record.YardBoard(new[] { "a", "b" }));
    }
}
