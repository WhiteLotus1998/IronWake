using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Hask's line strike (issue 1384, round 487; numbers provisional on #1247). The pitched Iron Warden is <c>hask_warden</c>,
/// seated on the sample <c>docs/samples/ironwake_keep_warden.map</c> while the campaign keep keeps the stand-in Hask
/// (DECISIONS/0346: the Sim wins the keep with him in 0 of 200). As his action he strikes every company unit
/// on up to four tiles in one cardinal line out from him, once each, at his lance's numbers read as if adjacent, with no
/// double and no counter. The line stops at the map's edge and before a wall. The planner takes it when a line catches
/// two or more units, and swings plainly otherwise; <c>threat</c> prints the line through a unit.
/// </summary>
public class LineStrikeTests
{
    private const string Hall = """
        name: Hall
        size: 10x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ..........
        ..........
        ........#.
        ..........
        ..........

        units:
        P captain 3,2
        P recruit:wren 1,2
        P recruit:ivo 0,0
        B hask_warden 4,2 group:lord behavior:boss
        E soldier 2,2 group:lord behavior:hold
        """;

    private static readonly Coord West = new(3, 2);

    private static BattleState Start(ulong seed = 1384, string? map = null) =>
        BattleFixture.Start(seed, ValueList<Unit>.Of(Hale, Wren, Ivo), map ?? Hall) with { Phase = Side.Enemy };

    private static BattleUnit Hask(BattleState state) => state.Units.Single(u => u.Unit.ClassId == "iron_warden");

    private static BattleUnit Soldier(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Unit.ClassId != "iron_warden");

    [Fact]
    public void TheCampaignKeepsTheStandInHaskUntilTheWardenPassesTheSim()
    {
        Assert.Equal("bulwark", Starter.Unit("hask").ClassId);
        Assert.Null(AbilityRules.LineStrike(Starter.AbilitiesOf(Starter.Unit("hask"))));
    }

    [Fact]
    public void TheWardenSampleSpawnsTheIronWardenAtTheKeep()
    {
        var path = Path.Combine(Directory.GetParent(Ironwake.Core.Tests.Content.Fixture.RealContentDirectory())!.FullName, "docs", "samples", "ironwake_keep_warden.map");
        var map = Ironwake.Content.MapFiles.Load(path, Starter);

        var lord = Assert.IsType<SpawnEnemy>(map.Events.Single(e => e.Name == "lord").Action).Placement;
        Assert.Equal("hask_warden", lord.TemplateId);
        Assert.True(lord.IsBoss);
    }

    [Fact]
    public void TheWardenCarriesThePitchedNumbersAndTheLineStrike()
    {
        var hask = Starter.Unit("hask_warden");

        Assert.Equal("iron_warden", hask.ClassId);
        Assert.Equal(14, hask.Level);
        Assert.Equal(new Stats(52, 13, 0, 16, 9, 10, 14, 11, 12), hask.Stats);
        Assert.Equal(WeaponRank.A, hask.Skill.Rank(WeaponType.Lance));
        Assert.Equal(4, LineStrike.Of(Starter, Hask(Start()))!.Reach);
    }

    [Fact]
    public void TheLineRunsFourTilesAndStopsAtTheMapsEdge()
    {
        var state = Start();

        Assert.Equal(new[] { new Coord(3, 2), new Coord(2, 2), new Coord(1, 2), new Coord(0, 2) }, LineStrike.LineOf(state, Starter, new Coord(4, 2), West, 4));
        Assert.Equal(new[] { new Coord(4, 1), new Coord(4, 0) }, LineStrike.LineOf(state, Starter, new Coord(4, 2), new Coord(4, 1), 4));
    }

    [Fact]
    public void TheLineStopsBeforeAWall()
    {
        Assert.Equal(new[] { new Coord(5, 2), new Coord(6, 2), new Coord(7, 2) }, LineStrike.LineOf(Start(), Starter, new Coord(4, 2), new Coord(5, 2), 4));
    }

    [Fact]
    public void TheLineStrikesEveryCompanyUnitOnItOnceAndPassesHisOwnSide()
    {
        var state = Start();
        var result = state.Try(new StrikeLine(Hask(state).Id, West));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var struck = result.Events.OfType<LineStruck>().Single();
        Assert.Equal(new[] { "hale", "wren" }, struck.Struck);
        var fights = result.Events.OfType<CombatFought>().ToList();
        Assert.Equal(new[] { "hale", "wren" }, fights.Select(f => f.TargetId));
        Assert.All(fights, f => Assert.Equal(Hask(state).Id, Assert.Single(f.Strikes).AttackerId));
        Assert.Equal(Soldier(state).Hp, result.Next.Find(Soldier(state).Id)!.Hp);
        Assert.True(result.Next.Find(Hask(state).Id)!.Acted);
    }

    [Fact]
    public void TheLineDrawsNoCounterAndNeverDoubles()
    {
        var state = Start();
        var forecast = LineStrike.Forecast(state, Starter, Hask(state), state.Find("hale")!);
        var result = state.Try(new StrikeLine(Hask(state).Id, West));

        Assert.False(forecast.Defender.Strikes);
        Assert.False(forecast.Attacker.Doubles);
        Assert.Equal(Hask(state).Hp, result.Next.Find(Hask(state).Id)!.Hp);
    }

    [Fact]
    public void ATargetAtTheLinesFarEndIsStruckAtTheSameNumbersAsBesideHim()
    {
        var state = Start();
        var near = LineStrike.Forecast(state, Starter, Hask(state), state.Find("hale")!).Attacker;
        var far = LineStrike.Forecast(state.WithUnit(state.Find("hale")! with { At = new Coord(0, 2) }), Starter, Hask(state), state.Find("hale")! with { At = new Coord(0, 2) }).Attacker;

        Assert.Equal(near.Damage, far.Damage);
        Assert.Equal(near.DisplayedHit, far.DisplayedHit);
    }

    [Fact]
    public void AHitDealsTheLancesMightPastDefence()
    {
        var state = Start();
        var hale = state.Find("hale")!;
        var side = LineStrike.Forecast(state, Starter, Hask(state), hale).Attacker;

        // Str 13 plus the Warden's Lance's Mt 9, against the captain's Def 5 on plain.
        Assert.Equal(13 + Starter.Weapon("wardens_lance").Mt - hale.Unit.Stats.Def, side.Damage);
    }

    [Fact]
    public void ALineWithNoCompanyUnitOnItIsRefused()
    {
        var state = Start();

        Assert.Equal(RejectionReason.CannotStrikeLine, state.Refused(new StrikeLine(Hask(state).Id, new Coord(4, 3))).Reason);
    }

    [Fact]
    public void ALineThroughATileNotBesideHimIsRefused()
    {
        var state = Start();

        Assert.Equal(RejectionReason.CannotStrikeLine, state.Refused(new StrikeLine(Hask(state).Id, new Coord(2, 2))).Reason);
    }

    [Fact]
    public void AUnitWithoutTheLineStrikeIsRefused()
    {
        var state = Start();

        Assert.Equal(RejectionReason.CannotStrikeLine, state.Refused(new StrikeLine(Soldier(state).Id, new Coord(3, 2))).Reason);
    }

    [Fact]
    public void ACompanyUnitNeverStrikesALine()
    {
        var state = Start() with { Phase = Side.Player };

        Assert.Equal(RejectionReason.CannotStrikeLine, state.Refused(new StrikeLine("hale", new Coord(4, 2))).Reason);
    }

    [Fact]
    public void AKillOnTheLineTakesTheUnitOffTheBoard()
    {
        var state = Start();
        state = state.WithUnit(state.Find("wren")! with { Hp = 1 });
        for (ulong seed = 1; seed < 200; seed++)
        {
            var result = (state with { Seed = seed }).Try(new StrikeLine(Hask(state).Id, West));
            if (result.Events.OfType<UnitDied>().Any(d => d.UnitId == "wren"))
            {
                Assert.Null(result.Next.Find("wren"));
                return;
            }
        }

        Assert.Fail("no seed under 200 killed wren");
    }

    [Fact]
    public void ThePlannerStrikesALineThatCatchesTwo()
    {
        var state = Start();
        var plan = EnemyAi.PlanUnit(state, Starter, Hask(state));

        Assert.Equal(new Command[] { new StrikeLine(Hask(state).Id, West) }, plan);
    }

    [Fact]
    public void ThePlannerSwingsPlainlyWhenTheLineCatchesOne()
    {
        var state = Start();
        state = state.WithUnit(state.Find("wren")! with { At = new Coord(1, 4) });
        var plan = EnemyAi.PlanUnit(state, Starter, Hask(state));

        Assert.DoesNotContain(plan, c => c is StrikeLine);
        Assert.Contains(plan, c => c is Attack { TargetId: "hale" });
    }

    [Fact]
    public void ThePlannerPicksTheLineThatCatchesMost()
    {
        var state = Start();
        state = state.WithUnit(state.Find("ivo")! with { At = new Coord(4, 0) });
        state = state.WithUnit(state.Find("wren")! with { At = new Coord(4, 1) });

        Assert.Equal(new Command[] { new StrikeLine(Hask(state).Id, new Coord(4, 1)) }, EnemyAi.PlanUnit(state, Starter, Hask(state)));
    }

    [Fact]
    public void AnAggressiveWardenMovesToTheTileWhoseLineCatchesTwo()
    {
        var map = Hall.Replace("B hask_warden 4,2 group:lord behavior:boss", "E hask_warden 6,2 group:lord behavior:aggressive");
        var state = Start(map: map);
        state = state.WithUnit(state.Find("hale")! with { At = new Coord(3, 4) }).WithUnit(state.Find("wren")! with { At = new Coord(1, 4) });
        var plan = EnemyAi.PlanUnit(state, Starter, Hask(state));

        var strike = Assert.IsType<StrikeLine>(plan.Last());
        var from = Assert.IsType<Move>(plan.First()).To;
        Assert.Equal(4, from.Y);
        Assert.Equal(new Coord(from.X - 1, 4), strike.Toward);
    }

    [Fact]
    public void ThreatPrintsTheLineThroughTheUnit()
    {
        var state = Start() with { Phase = Side.Player };
        var line = Ironwake.Cli.PlaySession.LineStrikeThreat(state, Starter, state.Find("wren")!, new Coord(1, 2));

        Assert.NotNull(line);
        Assert.StartsWith("Hask's line strike from 4,2 west catches", line);
        Assert.Contains("no counter; not in the total", line);
    }

    [Fact]
    public void ThreatPrintsNoLineWhenItWouldCatchOnlyTheUnit()
    {
        var state = Start() with { Phase = Side.Player };

        Assert.Null(Ironwake.Cli.PlaySession.LineStrikeThreat(state, Starter, state.Find("ivo")!, new Coord(4, 0)));
    }
}
