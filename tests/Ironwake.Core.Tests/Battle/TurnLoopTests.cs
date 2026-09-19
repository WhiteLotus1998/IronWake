using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// DESIGN.md section 7: the five win conditions, the two loss conditions, the turn
/// limit, healing terrain at the start of a phase, and what a finished battle refuses (issue 7).
/// </summary>
public class TurnLoopTests
{
    private static string YardWith(string win, string extraHeader = "", string grid = "......\n......\n......\n......", string enemies = "E brigand 3,1 group:yard behavior:aggressive\nE soldier 3,2 group:yard behavior:aggressive") =>
        $"""
        name: Yard
        size: 6x4
        win: {win}
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {extraHeader}

        {grid}

        units:
        P captain 0,1
        P recruit:wren 0,2
        {enemies}

        """.Replace("\n\n\n", "\n\n");

    private static BattleState Wounded(BattleState state, string id, int hp) => state.WithUnit(state.Find(id)! with { Hp = hp });

    [Fact]
    public void RoutIsWonWhenTheLastEnemyDies()
    {
        var state = Wounded(Start(map: YardWith("rout", enemies: "E brigand 3,1 group:yard behavior:aggressive")), "brigand-1", 1);
        Assert.Equal(BattleOutcome.Ongoing, state.Outcome);

        var won = state.Do(new Move("hale", new Coord(2, 1))).Do(new Attack("hale", "brigand-1"));

        Assert.Equal(new BattleOutcome(BattleResult.Won, "rout"), won.Outcome);
        Assert.Contains("outcome Won", won.Canonical());
    }

    [Fact]
    public void SeizeIsWonWhenTheCaptainStandsOnTheThrone()
    {
        var state = Start(map: YardWith("seize", grid: "......\n..T...\n......\n......"));

        var recruitOnThrone = state.Do(new Move("wren", new Coord(2, 1)));
        Assert.Equal(BattleOutcome.Ongoing, recruitOnThrone.Outcome);

        var won = state.Do(new Move("hale", new Coord(2, 1)));
        Assert.Equal(new BattleOutcome(BattleResult.Won, "seize"), won.Outcome);
    }

    [Fact]
    public void DefeatBossIsWonWhenNoBossIsLeftWhateverElseLives()
    {
        var map = YardWith("defeat_boss", enemies: "B bandit_leader 3,1 group:yard behavior:boss\nE soldier 3,2 group:yard behavior:aggressive");
        var state = Start(map: map);

        Assert.Equal(BattleOutcome.Ongoing, state.Outcome);
        Assert.Equal(new BattleOutcome(BattleResult.Won, "defeat_boss"), state.WithoutUnit("bandit_leader-1").Outcome);
        Assert.Equal(BattleOutcome.Ongoing, state.WithoutUnit("soldier-1").Outcome);
    }

    [Fact]
    public void SurviveIsWonWhenTheTurnPassesTheLimit()
    {
        var state = Start(map: YardWith("survive").Replace("turn_limit: 10", "turn_limit: 2"));

        var turn2 = state.Do(new EndPhase()).Do(new EndPhase());
        Assert.Equal(BattleOutcome.Ongoing, turn2.Outcome);

        var turn3 = turn2.Do(new EndPhase()).Do(new EndPhase());
        Assert.Equal(3, turn3.Turn);
        Assert.Equal(new BattleOutcome(BattleResult.Won, "survive"), turn3.Outcome);
    }

    [Fact]
    public void EscapeIsWonWhenEveryLivingPlayerUnitStandsOnAnExit()
    {
        var state = Start(map: YardWith("escape", "exit: 1,3 2,3"));
        Assert.Equal(ValueList<Coord>.Of(new Coord(1, 3), new Coord(2, 3)), state.Map.Exits);

        var one = state.Do(new Move("hale", new Coord(1, 3)));
        Assert.Equal(BattleOutcome.Ongoing, one.Outcome);

        var both = one.Do(new Move("wren", new Coord(2, 3)));
        Assert.Equal(new BattleOutcome(BattleResult.Won, "escape"), both.Outcome);
        Assert.Equal(new BattleOutcome(BattleResult.Won, "escape"), one.WithoutUnit("wren").Outcome);
    }

    [Fact]
    public void TheCaptainsDeathLosesTheMapBeforeAnyWinIsRead()
    {
        var state = Start(map: YardWith("rout"));

        var lost = state.WithoutUnit("hale");

        Assert.Equal(new BattleOutcome(BattleResult.Lost, "the captain is dead", LossCause.Captain), lost.Outcome);
        Assert.Equal(new BattleOutcome(BattleResult.Lost, "the captain is dead", LossCause.Captain), lost.WithoutUnit("brigand-1").WithoutUnit("soldier-1").Outcome);
        Assert.True(state.Find("hale")!.IsCaptain);
        Assert.False(state.Find("wren")!.IsCaptain);
        Assert.Contains("unit hale Player 0,1 hp 22 unmoved ready class cadet level 1 exp 0 stats HP 22 Str 8 Mag 0 Dex 7 Spd 8 Lck 6 Def 5 Res 2 Cha 9 items iron_swordx40 captain\n", state.Canonical());
    }

    [Fact]
    public void TheProtectedRecruitsDeathLosesTheMap()
    {
        var state = Start(map: YardWith("rout", "protect: wren"));
        Assert.Equal("wren", state.Map.ProtectId);

        Assert.Equal(new BattleOutcome(BattleResult.Lost, "wren is dead", LossCause.Protected), state.WithoutUnit("wren").Outcome);
        Assert.Equal(BattleOutcome.Ongoing, Start(map: YardWith("rout")).WithoutUnit("wren").Outcome);
    }

    [Fact]
    public void TheTurnLimitLosesEveryMapButSurvive()
    {
        var state = Start(map: YardWith("rout").Replace("turn_limit: 10", "turn_limit: 1"));

        var turn2 = state.Do(new EndPhase()).Do(new EndPhase());

        Assert.Equal(new BattleOutcome(BattleResult.Lost, "turn 1 passed", LossCause.Timeout), turn2.Outcome);
    }

    [Fact]
    public void AFinishedBattleRefusesEverythingButRecall()
    {
        var lost = Start(map: YardWith("rout").Replace("turn_limit: 10", "turn_limit: 1")).Do(new EndPhase()).Do(new EndPhase());

        var rejection = lost.Refused(new Move("hale", new Coord(1, 1)));
        Assert.Equal(RejectionReason.BattleOver, rejection.Reason);
        Assert.Equal("the battle is lost (turn 1 passed); only Recall is left", rejection.Message);
        Assert.Equal(RejectionReason.BattleOver, lost.Refused(new EndPhase()).Reason);
        Assert.Empty(Resolver.Legal(lost, Starter));

        var recalled = lost.Do(new Recall(1));
        Assert.Equal(BattleOutcome.Ongoing, recalled.Outcome);
        Assert.NotEmpty(Resolver.Legal(recalled, Starter));
    }

    [Fact]
    public void AUnitCannotActTwiceInOnePhase()
    {
        var state = Start().Do(new Move("hale", new Coord(2, 1))).Do(new Attack("hale", "brigand-1"));

        if (state.Find("hale") is not null && !state.Outcome.IsOver)
        {
            Assert.Equal(RejectionReason.AlreadyActed, state.Refused(new Wait("hale")).Reason);
            Assert.Equal(RejectionReason.AlreadyActed, state.Refused(new Move("hale", new Coord(1, 1))).Reason);
        }

        Assert.Equal(RejectionReason.AlreadyActed, Start().Do(new Wait("wren")).Refused(new Wait("wren")).Reason);
    }

    [Fact]
    public void HealingTerrainHealsTheOwnersUnitsAtTheStartOfTheirPhaseFlooredAndCapped()
    {
        // Hale stands on a fort at 0,1 with 22 max HP: 20 percent is 4.4, floored to 4.
        var state = Start(map: YardWith("rout", grid: "......\nF.....\n......\n...F.."));
        state = Wounded(Wounded(state, "hale", 10), "soldier-1", 5);
        var soldierMax = state.Find("soldier-1")!.MaxHp(Starter);
        state = state.WithUnit(state.Find("soldier-1")! with { At = new Coord(3, 3) });

        var enemyPhase = state.Try(new EndPhase());
        Assert.Equal(new UnitHealed("soldier-1", soldierMax * 20 / 100, 5 + soldierMax * 20 / 100), enemyPhase.Events[2]);
        Assert.Equal(3, enemyPhase.Events.Count);
        Assert.Equal(10, enemyPhase.Next.Find("hale")!.Hp);

        var playerPhase = enemyPhase.Next.Try(new EndPhase());
        Assert.Equal(new UnitHealed("hale", 4, 14), playerPhase.Events[2]);
        Assert.Equal(3, playerPhase.Events.Count);

        var nearlyFull = Wounded(state, "hale", 20).Do(new EndPhase()).Try(new EndPhase());
        Assert.Equal(new UnitHealed("hale", 2, 22), nearlyFull.Events[2]);
        Assert.Equal(22, nearlyFull.Next.Find("hale")!.Hp);

        var full = Wounded(state, "hale", 22).Do(new EndPhase()).Try(new EndPhase());
        Assert.Equal(2, full.Events.Count);
    }

    [Fact]
    public void PlainGroundHealsNobody()
    {
        var state = Wounded(Start(), "hale", 10).Do(new EndPhase());

        var result = state.Try(new EndPhase());

        Assert.Equal(2, result.Events.Count);
        Assert.Equal(10, result.Next.Find("hale")!.Hp);
    }

    [Fact]
    public void EveryLegalCommandIsAcceptedAndComesInAFixedOrder()
    {
        var state = Start();
        var legal = Resolver.Legal(state, Starter).ToList();

        Assert.Equal(new Wait("hale"), legal.First(c => c is Wait));
        Assert.IsType<EndPhase>(legal[^1]);
        Assert.DoesNotContain(legal, c => c is Attack);
        Assert.DoesNotContain(legal, c => c is Move { To: var to } && to == new Coord(0, 1));
        Assert.All(legal, command => Assert.True(state.Try(command).Accepted, command.ToString()));

        var beside = state.Do(new Move("hale", new Coord(2, 1)));
        Assert.Contains(new Attack("hale", "brigand-1"), Resolver.Legal(beside, Starter));
        Assert.DoesNotContain(Resolver.Legal(beside, Starter), c => c is Move { UnitId: "hale" });
    }

    [Fact]
    public void ARandomWalkOfLegalCommandsIsNeverRejectedUntilTheBattleEnds()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);
        var random = new Random(11);
        var finished = 0;
        for (var seed = 1; seed <= 20; seed++)
        {
            var state = BattleState.From(map, Starter, Roster, (ulong)seed);
            for (var i = 0; i < 300; i++)
            {
                var legal = Resolver.Legal(state, Starter).ToList();
                if (legal.Count == 0)
                {
                    Assert.True(state.Outcome.IsOver);
                    finished++;
                    break;
                }

                var result = state.Try(legal[random.Next(legal.Count)]);
                Assert.True(result.Accepted, result.Rejection?.Message);
                state = result.Next;
            }
        }

        Assert.True(finished > 0, "no random walk reached an outcome in 300 commands; the turn limit should have ended some");
    }
}
