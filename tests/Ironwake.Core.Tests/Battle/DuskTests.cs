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
        Assert.Null(Dusk.Line(state, Starter));
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
        Assert.Contains("dusk: sight 2, 1 next turn; 1 unseen (?); no side strikes what it cannot see; the enemy hears within 4\n", text);
    }

    [Fact]
    public void TheDuskLineNamesTheHearingRadiusFromContent()
    {
        Assert.EndsWith("; the enemy hears within 4", Dusk.Line(Start(1), Starter));
        Assert.EndsWith("; the enemy hears within 2", Dusk.Line(Start(1), Starter with { WakeRadius = 2 }));
        Assert.DoesNotContain("hears", MapRenderer.Render(Start(null), Starter));
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
        Assert.Contains("\n  Soldier: cannot see you (dark)", text);
    }

    /// <summary>
    /// Issue 399: while an enemy is in the dark, the empty case of <c>threat</c> claims only the
    /// enemies in sight; in daylight, or at dusk with every enemy seen, it keeps the plain line.
    /// </summary>
    [Theory]
    [InlineData(1, "no enemy in sight can strike hale next phase", true)]
    [InlineData(9, "no enemy can strike hale next phase", false)]
    [InlineData(null, "no enemy can strike hale next phase", false)]
    public void AnEmptyThreatAtDuskClaimsOnlyTheEnemiesInSight(int? dusk, string claim, bool dark)
    {
        var state = Start(dusk);
        var hale = state.Find("hale")!;

        var text = Ironwake.Cli.PlaySession.ThreatText(state, Starter, hale, hale.At, Queries.Threats(state, Starter, hale, hale.At)!, Queries.SleepingThreats(state, Starter, hale, hale.At)!, Queries.Unseeing(state, Starter, hale, hale.At));

        Assert.StartsWith($"Threat on hale at 0,0 (Plain): {claim}", text);
        Assert.Equal(dark, text.Contains("in the dark", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Issue 403: the longest reach is the most move of any class plus the most range of any
    /// weapon, 8 on the shipped content (the outrider's 6 and range 2), and it follows the content,
    /// so a longer weapon widens it and a content with no classes counts range alone.
    /// </summary>
    [Fact]
    public void TheLongestReachIsTheMostMovePlusTheMostRangeInTheContent()
    {
        var longBow = Starter with { Weapons = Starter.Weapons.SetItem("long_bow", Starter.Weapon("iron_bow") with { Id = "long_bow", MaxRange = 3 }) };
        var noClasses = Starter with { Classes = Starter.Classes.Clear() };

        Assert.Equal(9, Starter.LongestReach);
        Assert.Equal(10, longBow.LongestReach);
        Assert.Equal(2, noClasses.LongestReach);
    }

    /// <summary>
    /// Issue 403: at dusk <c>threat</c> lists the unseen tiles within the longest reach of the tile
    /// asked about, nearest first with the distance, and never a name: a rider and a soldier at the
    /// same distance print the same way, and a <c>?</c> at 10 is not listed (the reach is 9 since the
    /// Wing Captain, issue 704: Mov 7 and range 2).
    /// </summary>
    [Fact]
    public void ThreatAtDuskListsTheUnseenTilesWithinReachNearestFirstAndUnnamed()
    {
        var state = BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Ottilie), Night(1, "E soldier 5,2 group:a behavior:hold\nE rider 5,0 group:b behavior:hold\nE soldier 10,1 group:c behavior:hold\nE soldier 4,1 group:d behavior:hold"));
        var hale = state.Find("hale")!;

        var text = Ironwake.Cli.PlaySession.ThreatText(state, Starter, hale, hale.At, Queries.Threats(state, Starter, hale, hale.At)!, Queries.SleepingThreats(state, Starter, hale, hale.At)!, Queries.Unseeing(state, Starter, hale, hale.At));

        Assert.Contains("\n  In the dark, unpriced: ? at 4,1 (4), ? at 5,0 (6), ? at 5,2 (6)", text);
        Assert.DoesNotContain("10,1", text);
        Assert.DoesNotContain("rider", text);
        Assert.DoesNotContain("whatever is in the dark", text);
    }

    /// <summary>
    /// Issue 403: with every unseen enemy past the longest reach the bare dark line stays, and a
    /// daylight map prints no dark row at all.
    /// </summary>
    [Theory]
    [InlineData(1, true)]
    [InlineData(null, false)]
    public void WithNothingUnseenInReachTheBareDarkLineStays(int? dusk, bool dark)
    {
        var state = BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Ottilie), Night(dusk, "E soldier 10,1 group:c behavior:hold"));
        var hale = state.Find("hale")!;

        var text = Ironwake.Cli.PlaySession.ThreatText(state, Starter, hale, hale.At, Queries.Threats(state, Starter, hale, hale.At)!, Queries.SleepingThreats(state, Starter, hale, hale.At)!, Queries.Unseeing(state, Starter, hale, hale.At));

        Assert.Equal(dark, text.Contains("\n  And whatever is in the dark (?), unpriced"));
        Assert.DoesNotContain("in the dark, unpriced:", text);
        Assert.Equal(dark ? 1 : 0, Dusk.UnseenNear(state, hale.At, 99).Count);
    }

    /// <summary>
    /// Issue 987's board, at sight 2: hale at 0,1; the shieldbearer at 3,1 hears him and steps
    /// beside him; the <paramref name="lit"/> enemy at 4,0 is past hearing and does not know where
    /// he is until a side-mate stands within sight of him. Ottilie at <paramref name="ottilie"/>.
    /// The enemy phase acts in unit id order, so the shieldbearer acts before a soldier and after a brigand.
    /// </summary>
    private static BattleState Lit(string lit, string ottilie = "5,1", bool lighter = true) =>
        BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Ottilie), Night(2, $"E {lit} 4,0 group:b behavior:aggressive" + (lighter ? "\nE shieldbearer 3,1 group:a behavior:aggressive" : ""))
            .Replace("P recruit:ottilie 0,2", $"P recruit:ottilie {ottilie}"));

    /// <summary>
    /// Issue 987: an enemy that does not know where the unit is strikes it once a side-mate acting
    /// before it stands within sight of it, so <c>threat</c> prices it, marks who lights the unit,
    /// counts it in the total, and <c>end</c>'s lethal names the unit when the strikes reach its HP.
    /// </summary>
    [Fact]
    public void ThreatPricesAnEnemyASideMateLightsTheUnitFor()
    {
        var state = Lit("soldier");
        var hale = state.Find("hale")!;

        var lines = Queries.Threats(state, Starter, hale, hale.At)!;
        var lit = Assert.Single(lines, l => l.Enemy.Id == "soldier-1");
        var text = Ironwake.Cli.PlaySession.ThreatText(state, Starter, hale, hale.At, lines, Queries.SleepingThreats(state, Starter, hale, hale.At)!, Queries.Unseeing(state, Starter, hale, hale.At));

        Assert.Equal("shieldbearer-1", lit.LitBy?.Id);
        Assert.Contains(lines, l => l.Enemy.Id == "shieldbearer-1" && l.LitBy is null);
        Assert.Empty(Queries.Unseeing(state, Starter, hale, hale.At)!);
        Assert.Equal(lines.Sum(l => l.IfAllLand), Queries.IfAllLand(lines));
        Assert.Contains("\n  Soldier (once Shieldbearer lights you) from ", text);
        Assert.DoesNotContain("cannot see you", text);

        var weak = state.WithUnit(hale with { Hp = lines.Sum(l => l.IfAllLand) });
        var lethal = Assert.Single(Queries.Lethal(weak, Starter), t => t.Unit.Id == "hale");
        Assert.Contains(lethal.Strikers, s => s.Enemy.Id == "soldier-1");
    }

    /// <summary>
    /// Issue 987's guard: with no side-mate to light the unit, or with the only one acting after
    /// the enemy in the phase's order, the enemy stays <c>cannot see you (dark)</c> and out of the total.
    /// </summary>
    [Theory]
    [InlineData("soldier", false)]
    [InlineData("brigand", true)]
    public void AnEnemyNoEarlierSideMateLightsTheUnitForStaysUnseeing(string enemy, bool lighter)
    {
        var state = Lit(enemy, lighter: lighter);
        var hale = state.Find("hale")!;

        var lines = Queries.Threats(state, Starter, hale, hale.At)!;
        var text = Ironwake.Cli.PlaySession.ThreatText(state, Starter, hale, hale.At, lines, Queries.SleepingThreats(state, Starter, hale, hale.At)!, Queries.Unseeing(state, Starter, hale, hale.At));

        Assert.DoesNotContain(lines, l => l.Enemy.Id == $"{enemy}-1");
        Assert.Equal(new[] { $"{enemy}-1" }, Queries.Unseeing(state, Starter, hale, hale.At)!.Select(u => u.Id));
        Assert.Contains($"\n  {char.ToUpperInvariant(enemy[0])}{enemy[1..]}: cannot see you (dark)", text);
    }

    /// <summary>
    /// Issue 987: the lit line is what the enemy phase does. Played out with Ottilie out of the
    /// way, the shieldbearer strikes hale and the soldier, lit by it, strikes him after.
    /// </summary>
    [Fact]
    public void TheEnemyPhaseStrikesTheUnitTheLitLineNames()
    {
        var state = Lit("soldier", ottilie: "11,2");
        var hale = state.Find("hale")!;
        Assert.Contains(Queries.Threats(state, Starter, hale, hale.At)!, l => l.Enemy.Id == "soldier-1" && l.LitBy?.Id == "shieldbearer-1");

        var plan = EnemyAi.Plan(state.Do(new EndPhase()), Starter);

        Assert.Contains(plan, c => c is Attack { UnitId: "shieldbearer-1", TargetId: "hale" });
        Assert.Contains(plan, c => c is Attack { UnitId: "soldier-1", TargetId: "hale" });
    }

    /// <summary>
    /// Issue 1104's board, at sight 3: hale at 0,1, Ottilie at 1,2; the <paramref name="walker"/> at
    /// 5,2 hears Ottilie, not hale, and walks to strike her; the <paramref name="lit"/> enemy at 4,0
    /// is past hearing of hale and nobody of its side sees him at the phase start.
    /// </summary>
    private static BattleState Walked(string walker, string lit) =>
        BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Ottilie), Night(3, $"E {lit} 4,0 group:b behavior:aggressive\nE {walker} 5,2 group:a behavior:aggressive")
            .Replace("P recruit:ottilie 0,2", "P recruit:ottilie 1,2"));

    /// <summary>
    /// Issue 1104: a side-mate that walks within sight of the unit to strike someone else, never
    /// striking the unit itself, still lights it for an enemy acting after it, so <c>threat</c>
    /// prices that enemy and marks the walker as the one who lights the unit.
    /// </summary>
    [Fact]
    public void ASideMateWalkingPastTheUnitLightsItForAnEnemyAfterIt()
    {
        var state = Walked("brigand", "soldier");
        var hale = state.Find("hale")!;
        var board = state.Do(new EndPhase());
        var walk = EnemyAi.PlanUnit(board, Starter, board.Find("brigand-1")!).OfType<Move>().Single();

        var lines = Queries.Threats(state, Starter, hale, hale.At)!;
        var text = Ironwake.Cli.PlaySession.ThreatText(state, Starter, hale, hale.At, lines, Queries.SleepingThreats(state, Starter, hale, hale.At)!, Queries.Unseeing(state, Starter, hale, hale.At));

        Assert.True(walk.To.DistanceTo(hale.At) <= Dusk.Sight(board));
        Assert.DoesNotContain(EnemyAi.PlanUnit(board, Starter, board.Find("brigand-1")!), c => c is Attack { TargetId: "hale" });
        Assert.Equal("brigand-1", Assert.Single(lines, l => l.Enemy.Id == "soldier-1").LitBy?.Id);
        Assert.DoesNotContain(Queries.Unseeing(state, Starter, hale, hale.At)!, u => u.Id == "soldier-1");
        Assert.DoesNotContain("Soldier: cannot see you (dark)", text);
    }

    /// <summary>
    /// Issue 1104's guard: a walker that acts after the enemy in the phase's order lights nobody
    /// for it, so the enemy stays <c>cannot see you (dark)</c>.
    /// </summary>
    [Fact]
    public void ASideMateWalkingPastAfterTheEnemyLightsNothingForIt()
    {
        var state = Walked("soldier", "brigand");
        var hale = state.Find("hale")!;

        var lines = Queries.Threats(state, Starter, hale, hale.At)!;

        Assert.DoesNotContain(lines, l => l.Enemy.Id == "brigand-1");
        Assert.Contains(Queries.Unseeing(state, Starter, hale, hale.At)!, u => u.Id == "brigand-1");
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

    /// <summary>
    /// Issue 326's cork: a 14x3 night with a wall down column 7 and one gap at 7,1, the exits
    /// at 13,1 and 13,2 beyond it, and a soldier at 1,1. Ottilie holds the gap six tiles from the
    /// soldier, past hearing; Hale stands far off at 12,0.
    /// </summary>
    private static BattleState Corked() =>
        BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Ottilie), """
            name: Cork
            size: 14x3
            win: escape
            exit: 13,1 13,2
            turn_limit: 10
            recall: 3
            enemy_level: 1
            dusk: 1

            .......#......
            ..............
            .......#......

            units:
            P captain 12,0
            P recruit:ottilie 7,1
            E soldier 1,1 group:a behavior:aggressive

            """).Do(new EndPhase());

    [Fact]
    public void DriftPathsAsIfThePartyWereNotThereSoAHeldGapDoesNotFreezeTheChase()
    {
        var state = Corked();
        var soldier = state.Find("soldier-1")!;

        Assert.False(Dusk.Knows(state, Starter, soldier, state.Find("ottilie")!));
        Assert.False(Dusk.Knows(state, Starter, soldier, state.Find("hale")!));
        var move = Assert.IsType<Move>(EnemyAi.PlanUnit(state, Starter, soldier)[0]);
        Assert.Equal(new Coord(5, 1), move.To);
    }

    [Fact]
    public void ADriftingUnitStopsShortOfThePlayerUnitOnItsPath()
    {
        var state = Corked().WithUnit(Corked().Find("soldier-1")! with { At = new Coord(5, 1) });
        var soldier = state.Find("soldier-1")!;
        var reach = state.ReachOf(soldier, Starter);

        var to = EnemyAi.Drift(state, Starter, soldier, reach, Array.Empty<Reach>());

        Assert.Equal(new Coord(6, 1), to);
        Assert.True(reach.CanEnd(to!.Value));
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
