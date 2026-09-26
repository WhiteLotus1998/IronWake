using Ironwake.Core.Tests.Maps;
using Ironwake.Content;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Dusk maps (DESIGN.md 13.7, experiment): on a map with the <c>dusk:</c> header sight falls
/// one tile a turn to a floor of 1, a side sees a tile within sight of any of its units,
/// neither side strikes what it cannot see, and the console draws an unseen enemy as a
/// question mark with no row.
/// </summary>
public class DuskTests
{
    private static readonly Unit Ottilie = Recruit("ottilie", "bowman", new Stats(18, 6, 0, 8, 7, 4, 3, 2, 3), "iron_bow");

    private static string Field(int? dusk, string archerBehavior = "hold") =>
        $"""
        name: Field
        size: 8x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(dusk is { } d ? $"dusk: {d}" : "")}

        ........
        ........
        ........
        ........

        units:
        P captain 0,0
        P recruit:ottilie 2,1
        E soldier 4,1 group:field behavior:hold
        E archer 6,3 group:far behavior:{archerBehavior}

        """.Replace("\n\n\n", "\n\n");

    private static BattleState Start(int? dusk, string archerBehavior = "hold") =>
        BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Ottilie), Field(dusk, archerBehavior));

    [Theory]
    [InlineData(3, 1, 3)]
    [InlineData(3, 2, 2)]
    [InlineData(3, 3, 1)]
    [InlineData(3, 9, 1)]
    [InlineData(1, 1, 1)]
    public void SightFallsOneTileATurnAndNeverUnderOne(int start, int turn, int sight)
    {
        var map = Start(start).Map;

        Assert.Equal(sight, Dusk.Sight(map, turn));
    }

    [Fact]
    public void AMapWithoutTheHeaderIsDaylightAndEveryTileIsSeen()
    {
        var state = Start(null);

        Assert.Null(Dusk.Sight(state));
        Assert.True(Dusk.Sees(state, Side.Player, new Coord(6, 3)));
        Assert.Null(Dusk.Line(state));
    }

    [Fact]
    public void ASideSeesATileWithinSightOfAnyOfItsUnits()
    {
        var state = Start(2);

        Assert.True(Dusk.Sees(state, Side.Player, new Coord(4, 1)));
        Assert.False(Dusk.Sees(state, Side.Player, new Coord(6, 3)));
        Assert.True(Dusk.Sees(state, Side.Player, new Coord(6, 3), "ottilie", new Coord(5, 3)));
    }

    /// <summary>
    /// Issue 309: a forecast from a tile names an enemy the mover would see from there, on top
    /// of what the side sees now; the mover's tile read elsewhere does not see for it.
    /// </summary>
    [Fact]
    public void AnEnemyIsNamedFromATileTheMoverWouldSeeItFrom()
    {
        var state = Start(1);
        var soldier = state.Find("soldier-1")!;

        Assert.False(Dusk.Seen(state, soldier));
        Assert.True(Dusk.Seen(state, soldier, "hale", new Coord(3, 1)));
        Assert.False(Dusk.Seen(state, soldier, "ottilie", new Coord(2, 1)));
        Assert.False(Dusk.Seen(state, soldier, "hale", new Coord(2, 1)));
    }

    [Fact]
    public void APlayerCannotStrikeATargetItsSideCannotSee()
    {
        var state = Start(1);

        var refused = state.Refused(new Attack("ottilie", "soldier-1"));

        Assert.Equal(RejectionReason.Unseen, refused.Reason);
        Assert.Null(Queries.Forecast(state, Starter, state.Find("ottilie")!, state.Find("soldier-1")!));
        Assert.Empty(Queries.Targets(state, Starter, state.Find("ottilie")!));
    }

    /// <summary>
    /// Issue 318: the legal list offers no strike on a target its side cannot see, so gate 2's
    /// random player, which draws from it, never draws a command the resolver refuses.
    /// </summary>
    [Fact]
    public void TheLegalListOffersNoStrikeOnATargetItsSideCannotSee()
    {
        var state = Start(1);

        var attacks = Resolver.Legal(state, Starter).OfType<Attack>().ToList();

        Assert.DoesNotContain(attacks, a => a.UnitId == "ottilie" && a.TargetId == "soldier-1");
        Assert.All(attacks, a => Assert.True(state.Try(a).Accepted, a.ToString()));
        Assert.Contains(Resolver.Legal(Start(1).Do(new Move("hale", new Coord(4, 0))), Starter), c => c is Attack { UnitId: "ottilie", TargetId: "soldier-1" });
    }

    /// <summary>
    /// Issue 318: the Sim's heuristic player strikes only a target its side would see from the
    /// tile it strikes from, so its plan is one the resolver accepts. Before the guard it
    /// planned Ottilie's bow on the soldier from two tiles away at sight 1, and `--full` on
    /// Brackwater Cut at dusk threw on the rejected Attack.
    /// </summary>
    [Fact]
    public void TheHeuristicPlansNoStrikeOnATargetItsSideCannotSee()
    {
        var state = Start(1);

        var plan = Ironwake.Sim.HeuristicPlayer.PlanUnit(state, Starter, state.Find("ottilie")!);

        Assert.DoesNotContain(plan, c => c is Attack);
        foreach (var command in plan)
        {
            Assert.True(state.Try(command).Accepted, command.ToString());
            state = state.Do(command);
        }
    }

    [Fact]
    public void AFriendBesideTheTargetLetsTheArcherStrike()
    {
        var state = Start(1).Do(new Move("hale", new Coord(4, 0)));

        Assert.True(state.Try(new Attack("ottilie", "soldier-1")).Accepted);
    }

    [Fact]
    public void TheSameShotInDaylightIsAccepted()
    {
        Assert.True(Start(null).Try(new Attack("ottilie", "soldier-1")).Accepted);
    }

    [Fact]
    public void TheEnemyPlannerDoesNotStrikeWhatItsSideCannotSee()
    {
        var dark = Start(1).Do(new Move("ottilie", new Coord(4, 3))).Do(new Wait("ottilie")).Do(new EndPhase());
        var day = Start(null).Do(new Move("ottilie", new Coord(4, 3))).Do(new Wait("ottilie")).Do(new EndPhase());

        Assert.DoesNotContain(EnemyAi.PlanUnit(dark, Starter, dark.Find("archer-1")!), c => c is Attack);
        Assert.Contains(EnemyAi.PlanUnit(day, Starter, day.Find("archer-1")!), c => c is Attack { TargetId: "ottilie" });
    }

    [Fact]
    public void TheBoardDrawsAnUnseenEnemyAsAQuestionMarkWithNoRow()
    {
        var text = MapRenderer.Render(Start(2), Starter);

        Assert.Contains("\n 3 ......?.\n", text);
        Assert.DoesNotContain("archer-1", text);
        Assert.Contains("?  unseen at 6,3\n", text);
        Assert.Contains("dusk: sight 2, 1 next turn; 1 unseen (?); no side strikes what it cannot see\n", text);
    }

    [Fact]
    public void TheDuskLineSaysWhenItGetsNoDarker()
    {
        Assert.Contains("dusk: sight 1; it gets no darker;", MapRenderer.Render(Start(1), Starter));
    }

    [Fact]
    public void TheDuskHeaderRoundTripsThroughTheMapWriter()
    {
        var map = Start(4).Map;

        Assert.Contains("dusk: 4\n", MapFormat.Write(map, Starter));
        Assert.Equal(4, MapFixture.Parse(MapFormat.Write(map, Starter), "again.map").Dusk);
    }

    [Fact]
    public void ADuskOfZeroIsRefusedOnLoad()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Field(0), "zero.map"));

        Assert.Contains("dusk", error.Message);
    }
    private static string Night(int? dusk, string enemies) =>
        $"""
        name: Night
        size: 12x3
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(dusk is { } d ? $"dusk: {d}" : "")}

        ............
        ............
        ............

        units:
        P captain 0,1
        P recruit:ottilie 0,2
        {enemies}

        """.Replace("\n\n\n", "\n\n");

    /// <summary>Hale at 0,1 and Ottilie at 0,2, then the player phase ended, so the enemy planner is asked at sight 1.</summary>
    private static BattleState EnemyPhase(int? dusk, string enemies) =>
        BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Ottilie), Night(dusk, enemies)).Do(new EndPhase());

    [Fact]
    public void AnEnemyThatNeitherSeesNorHearsAnyUnitWaits()
    {
        const string Far = "E soldier 5,1 group:a behavior:aggressive";
        var dark = EnemyPhase(1, Far);
        var day = EnemyPhase(null, Far);

        Assert.True(new Coord(5, 1).DistanceTo(new Coord(0, 1)) > Starter.WakeRadius);
        Assert.False(Dusk.Knows(dark, Starter, dark.Find("soldier-1")!, dark.Find("hale")!));
        Assert.Equal(new Command[] { new Wait("soldier-1") }, EnemyAi.PlanUnit(dark, Starter, dark.Find("soldier-1")!));
        Assert.Contains(EnemyAi.PlanUnit(day, Starter, day.Find("soldier-1")!), c => c is Attack);
    }

    [Fact]
    public void AWokenEnemyPathsTowardAUnitItHearsButCannotSee()
    {
        var dark = EnemyPhase(1, "E archer 4,1 group:a behavior:aggressive");

        var plan = EnemyAi.PlanUnit(dark, Starter, dark.Find("archer-1")!);

        Assert.True(Dusk.Knows(dark, Starter, dark.Find("archer-1")!, dark.Find("hale")!));
        Assert.Equal(2, plan.Count);
        var move = Assert.IsType<Move>(plan[0]);
        Assert.True(move.To.DistanceTo(new Coord(0, 1)) < 4);
        Assert.IsType<Wait>(plan[1]);
    }

    [Fact]
    public void WithinHearingAMeleeEnemyStepsUpSeesAndStrikes()
    {
        var dark = EnemyPhase(1, "E soldier 4,1 group:a behavior:aggressive");

        var plan = EnemyAi.PlanUnit(dark, Starter, dark.Find("soldier-1")!);

        Assert.Contains(plan, c => c is Attack);
    }

    [Fact]
    public void AnEnemyKnowsAUnitAnotherOfItsSideSees()
    {
        var dark = EnemyPhase(1, "E soldier 5,1 group:a behavior:aggressive\nE soldier 0,0 group:b behavior:hold");

        Assert.True(Dusk.Knows(dark, Starter, dark.Find("soldier-1")!, dark.Find("hale")!));
        Assert.Contains(EnemyAi.PlanUnit(dark, Starter, dark.Find("soldier-1")!), c => c is Attack);
    }

    [Fact]
    public void ThreatPricesAnUnseeingEnemyAtZeroAndSaysWhy()
    {
        var map = Night(1, "E soldier 5,1 group:a behavior:aggressive");
        var state = BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Ottilie), map.Replace("P recruit:ottilie 0,2", "P recruit:ottilie 6,1"));
        var hale = state.Find("hale")!;

        var lines = Queries.Threats(state, Starter, hale, hale.At)!;
        var unseeing = Queries.Unseeing(state, Starter, hale, hale.At)!;
        var text = Ironwake.Cli.PlaySession.ThreatText(state, Starter, hale, hale.At, lines, Queries.SleepingThreats(state, Starter, hale, hale.At)!, unseeing);

        Assert.DoesNotContain(lines, l => l.Enemy.Id == "soldier-1");
        Assert.Equal(new[] { "soldier-1" }, unseeing.Select(u => u.Id));
        Assert.Contains("\n  soldier-1: cannot see you (dark)", text);
    }

    [Fact]
    public void InDaylightNoEnemyIsUnseeing()
    {
        var state = BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Ottilie), Night(null, "E soldier 5,1 group:a behavior:aggressive"));
        var hale = state.Find("hale")!;

        Assert.Empty(Queries.Unseeing(state, Starter, hale, hale.At)!);
        Assert.Contains(Queries.Threats(state, Starter, hale, hale.At)!, l => l.Enemy.Id == "soldier-1");
    }

    /// <summary>
    /// A 12x3 night for issue 308's objective drift: Hale and Ottilie at the west edge,
    /// a soldier at 6,1 six tiles off (past hearing), forest on columns 3 to 5, and the
    /// objective line and tiles the test asks for.
    /// </summary>
    private static BattleState Drifting(string win, string behavior = "aggressive", string row1 = "............")
    {
        var map = $"""
            name: Drift
            size: 12x3
            win: {win}
            turn_limit: 10
            recall: 3
            enemy_level: 1
            dusk: 1

            ...^^^......
            {row1}
            ...^^^......

            units:
            P captain 0,1
            P recruit:ottilie 0,2
            E soldier 6,1 group:a behavior:{behavior}

            """;
        return BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Ottilie), map).Do(new EndPhase());
    }

    [Fact]
    public void AnEnemyThatKnowsOfNobodyMakesForTheExitNearestByItsOwnMovementCost()
    {
        var state = Drifting("escape\nexit: 2,1 11,1", row1: "...^^^......");
        var soldier = state.Find("soldier-1")!;

        var plan = EnemyAi.PlanUnit(state, Starter, soldier);

        Assert.False(Dusk.Knows(state, Starter, soldier, state.Find("hale")!));
        Assert.True(new Coord(6, 1).DistanceTo(new Coord(2, 1)) < new Coord(6, 1).DistanceTo(new Coord(11, 1)));
        var move = Assert.IsType<Move>(plan[0]);
        Assert.True(move.To.X > 6, $"moved to {move.To}, away from the cheaper exit at 11,1");
        Assert.IsType<Wait>(plan[1]);
    }

    [Fact]
    public void OnSeizeAnEnemyThatKnowsOfNobodyMakesForTheThrone()
    {
        var state = Drifting("seize", row1: "..T.........");

        var move = Assert.IsType<Move>(EnemyAi.PlanUnit(state, Starter, state.Find("soldier-1")!)[0]);

        Assert.True(move.To.DistanceTo(new Coord(2, 1)) < 4, $"moved to {move.To}");
    }

    [Fact]
    public void AnExitAnotherUnitStandsOnIsNoDestination()
    {
        var state = Drifting("escape\nexit: 11,0 11,1");
        var blocked = state.WithUnit(state.Find("hale")! with { At = new Coord(11, 1) })
            .WithUnit(state.Find("ottilie")! with { At = new Coord(11, 0) });
        var soldier = blocked.Find("soldier-1")!;

        Assert.False(Dusk.Knows(blocked, Starter, soldier, blocked.Find("hale")!));
        Assert.Null(EnemyAi.Drift(blocked, Starter, soldier, blocked.ReachOf(soldier, Starter), Array.Empty<Reach>()));
        Assert.NotNull(EnemyAi.Drift(state, Starter, state.Find("soldier-1")!, state.ReachOf(state.Find("soldier-1")!, Starter), Array.Empty<Reach>()));
    }

    [Fact]
    public void OnRoutAnEnemyThatKnowsOfNobodyStillWaits()
    {
        var state = Drifting("rout");

        Assert.Equal(new Command[] { new Wait("soldier-1") }, EnemyAi.PlanUnit(state, Starter, state.Find("soldier-1")!));
    }

    [Fact]
    public void AnEnemyThatHoldsKeepsItsHoldOnAnObjectiveMap()
    {
        var state = Drifting("escape\nexit: 11,0 11,1", behavior: "hold");

        Assert.Equal(new Command[] { new Wait("soldier-1") }, EnemyAi.PlanUnit(state, Starter, state.Find("soldier-1")!));
    }

    /// <summary>
    /// Issue 308: Hale spots the archer from 1,1 and Ottilie shoots it from 0,1 at range 2.
    /// No unit of the archer's side stands within sight 1 of 0,1, so it answers nothing.
    /// </summary>
    private static BattleState SpottedShot(int? dusk) =>
        BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Ottilie), Night(dusk, "E archer 2,1 group:a behavior:hold"))
            .Do(new Move("hale", new Coord(1, 1)))
            .Do(new Wait("hale"))
            .Do(new Move("ottilie", new Coord(0, 1)));

    [Fact]
    public void ARangeTwoStrikeFromATileTheDefendersSideCannotSeeTakesNoCounter()
    {
        var dark = SpottedShot(1);
        var ottilie = dark.Find("ottilie")!;
        var archer = dark.Find("archer-1")!;

        Assert.False(Dusk.Sees(dark, Side.Enemy, ottilie.At));
        Assert.True(archer.Answering(dark, Starter, ottilie.At).Blind);
        Assert.False(Queries.Forecast(dark, Starter, ottilie, archer)!.Defender.Strikes);
        var fought = dark.Try(new Attack("ottilie", "archer-1")).Events.OfType<CombatFought>().Single();
        Assert.All(fought.Strikes, s => Assert.Equal("ottilie", s.AttackerId));
        Assert.Equal(ottilie.Hp, fought.AttackerHpAfter);
    }

    [Fact]
    public void TheSameStrikeInDaylightIsCountered()
    {
        var day = SpottedShot(null);
        var ottilie = day.Find("ottilie")!;
        var archer = day.Find("archer-1")!;

        Assert.False(archer.Answering(day, Starter, ottilie.At).Blind);
        Assert.True(Queries.Forecast(day, Starter, ottilie, archer)!.Defender.Strikes);
        var fought = day.Try(new Attack("ottilie", "archer-1")).Events.OfType<CombatFought>().Single();
        Assert.Contains(fought.Strikes, s => s.AttackerId == "archer-1");
    }

    [Fact]
    public void AnAdjacentAttackerIsAlwaysAnswered()
    {
        var dark = BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Ottilie), Night(1, "E soldier 2,1 group:a behavior:hold"))
            .Do(new Move("hale", new Coord(1, 1)));
        var soldier = dark.Find("soldier-1")!;

        Assert.False(soldier.Answering(dark, Starter, new Coord(1, 1)).Blind);
        Assert.True(Queries.Forecast(dark, Starter, dark.Find("hale")!, soldier)!.Defender.Strikes);
    }

    /// <summary>
    /// The mirror for the enemy side: the soldier at 1,2 spots Ottilie at 0,2 and the archer
    /// at 2,2 shoots her at range 2, three tiles from Hale and two from her, so she answers
    /// nothing at sight 1 and the planner scores the shot as unanswered.
    /// </summary>
    [Fact]
    public void TheEnemyPlannerScoresASpottedRangeTwoStrikeAsUnanswered()
    {
        const string Pair = "E archer 2,2 group:a behavior:hold\nE soldier 1,2 group:b behavior:hold";
        var dark = EnemyPhase(1, Pair);
        var day = EnemyPhase(null, Pair);
        var archer = dark.Find("archer-1")!;

        Assert.True(dark.Find("ottilie")!.Answering(dark, Starter, archer.At).Blind);
        Assert.False(day.Find("ottilie")!.Answering(day, Starter, archer.At).Blind);
        Assert.True(EnemyAi.Score(dark, Starter, archer, archer.At, dark.Find("ottilie")!)
            > EnemyAi.Score(day, Starter, day.Find("archer-1")!, archer.At, day.Find("ottilie")!));
    }
}
