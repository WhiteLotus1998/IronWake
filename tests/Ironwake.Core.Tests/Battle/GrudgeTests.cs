using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Named rivals, the grudge arm (DESIGN.md 13.4, experiment): on a <c>grudges: on</c> map a
/// player unit that kills an enemy is sworn against by every living enemy of that enemy's
/// group; the planner strikes the sworn unit whenever any tile reaches it, takes its best
/// other strike only when none does, and approaches the sworn unit first.
/// </summary>
public class GrudgeTests
{
    private static string Yard(bool grudges) =>
        $"""
        name: Yard
        size: 8x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(grudges ? "grudges: on" : "")}

        ........
        ........
        ........
        ........

        units:
        P captain 0,1
        P recruit:wren 0,2
        E brigand 3,1 group:yard behavior:aggressive
        E soldier 7,3 group:yard behavior:aggressive
        E archer 7,0 group:tower behavior:hold

        """.Replace("\n\n\n", "\n\n");

    /// <summary>A lane twelve wide: the captain, Wren, and one soldier, placed by the test.</summary>
    private static BattleState Lane(Coord hale, Coord wren, Coord soldier)
    {
        var map = $"""
            name: Lane
            size: 12x4
            win: rout
            turn_limit: 10
            recall: 3
            enemy_level: 1
            grudges: on

            ............
            ............
            ............
            ............

            units:
            P captain {hale.X},{hale.Y}
            P recruit:wren {wren.X},{wren.Y}
            E soldier {soldier.X},{soldier.Y} group:lane behavior:aggressive

            """;
        return Start(map: map).Do(new EndPhase());
    }

    /// <summary>Wren at 2,1 strikes the brigand at 1 HP: the first seed on which the brigand dies and Wren lives.</summary>
    private static ApplyResult WrenKillsTheBrigand(bool grudges = true)
    {
        for (ulong seed = 1; seed < 200; seed++)
        {
            var state = Start(seed, map: Yard(grudges));
            state = state.WithUnit(state.Find("brigand-1")! with { Hp = 1 }).Do(new Move("wren", new Coord(2, 1)));
            var result = state.Try(new Attack("wren", "brigand-1"));
            if (result.Next.Find("brigand-1") is null && result.Next.Find("wren") is not null)
            {
                return result;
            }
        }

        throw new InvalidOperationException("no seed below 200 lets Wren kill the brigand");
    }

    [Fact]
    public void AKillSwearsTheDeadEnemysGroupAgainstTheKiller()
    {
        var kill = WrenKillsTheBrigand();

        Assert.Equal("wren", kill.Next.Find("soldier-1")!.Grudge);
        Assert.Contains(new GrudgeSworn("soldier-1", "wren"), kill.Events);
    }

    [Fact]
    public void AnEnemyOfAnotherGroupSwearsNothing()
    {
        var kill = WrenKillsTheBrigand();

        Assert.Null(kill.Next.Find("archer-1")!.Grudge);
        Assert.DoesNotContain(kill.Events, e => e is GrudgeSworn { UnitId: "archer-1" });
    }

    [Fact]
    public void AMapWithoutTheHeaderSwearsNoGrudge()
    {
        var kill = WrenKillsTheBrigand(grudges: false);

        Assert.Null(kill.Next.Find("soldier-1")!.Grudge);
        Assert.DoesNotContain(kill.Events, e => e is GrudgeSworn);
    }

    [Fact]
    public void ACounterKillSwearsTheGroupAgainstTheUnitThatCountered()
    {
        for (ulong seed = 1; seed < 200; seed++)
        {
            var state = Start(seed, map: Yard(grudges: true)).Do(new EndPhase());
            state = state.WithUnit(state.Find("brigand-1")! with { Hp = 1, At = new Coord(1, 2) });
            var result = state.Try(new Attack("brigand-1", "wren"));
            if (result.Next.Find("brigand-1") is null && result.Next.Find("wren") is not null)
            {
                Assert.Equal("wren", result.Next.Find("soldier-1")!.Grudge);
                return;
            }
        }

        throw new InvalidOperationException("no seed below 200 lets Wren's counter kill the brigand");
    }

    [Fact]
    public void ANewerKillReplacesTheGrudge()
    {
        var state = WrenKillsTheBrigand().Next;
        var archer = state.Find("archer-1")! with { Group = "yard" };
        state = state.WithUnit(archer).WithUnit(state.Find("soldier-1")! with { Hp = 1, At = new Coord(0, 0) });

        for (ulong seed = 1; seed < 200; seed++)
        {
            var result = (state with { Seed = seed }).Try(new Attack("hale", "soldier-1"));
            if (result.Next.Find("soldier-1") is null && result.Next.Find("hale") is not null)
            {
                Assert.Equal("hale", result.Next.Find("archer-1")!.Grudge);
                return;
            }
        }

        throw new InvalidOperationException("no seed below 200 lets the captain kill the soldier");
    }

    [Fact]
    public void ASwornEnemyStrikesTheSwornUnitOverABetterScoringKill()
    {
        var state = Lane(hale: new Coord(4, 0), wren: new Coord(4, 2), soldier: new Coord(5, 1));
        state = state.WithUnit(state.Find("hale")! with { Hp = 1 });
        var soldier = state.Find("soldier-1")!;

        var unsworn = EnemyAi.PlanUnit(state, Starter, soldier);
        var sworn = EnemyAi.PlanUnit(state.WithUnit(soldier with { Grudge = "wren" }), Starter, soldier with { Grudge = "wren" });

        Assert.Equal("hale", unsworn.OfType<Attack>().Single().TargetId);
        Assert.Equal("wren", sworn.OfType<Attack>().Single().TargetId);
    }

    [Fact]
    public void ASwornEnemyThatCannotReachTheSwornUnitTakesItsBestOtherStrike()
    {
        var state = Lane(hale: new Coord(4, 0), wren: new Coord(11, 3), soldier: new Coord(5, 1));
        var soldier = state.Find("soldier-1")! with { Grudge = "wren" };

        var plan = EnemyAi.PlanUnit(state.WithUnit(soldier), Starter, soldier);

        Assert.Equal("hale", plan.OfType<Attack>().Single().TargetId);
    }

    [Fact]
    public void ASwornEnemyThatCanStrikeNobodyApproachesTheSwornUnitFirst()
    {
        var state = Lane(hale: new Coord(0, 3), wren: new Coord(11, 0), soldier: new Coord(5, 1));
        var soldier = state.Find("soldier-1")!;

        var unsworn = EnemyAi.PlanUnit(state, Starter, soldier).OfType<Move>().Single();
        var sworn = EnemyAi.PlanUnit(state.WithUnit(soldier with { Grudge = "wren" }), Starter, soldier with { Grudge = "wren" }).OfType<Move>().Single();

        Assert.True(unsworn.To.X < soldier.At.X, $"unsworn moved to {unsworn.To}");
        Assert.True(sworn.To.X > soldier.At.X, $"sworn moved to {sworn.To}");
    }

    [Fact]
    public void StrikeOnNamesNoStrikeOnAnotherUnitWhileTheSwornUnitIsInReach()
    {
        var state = Lane(hale: new Coord(4, 0), wren: new Coord(4, 2), soldier: new Coord(5, 1));
        var soldier = state.Find("soldier-1")! with { Grudge = "wren" };
        state = state.WithUnit(soldier);

        Assert.Null(EnemyAi.StrikeOn(state, Starter, soldier, state.Find("hale")!));
        Assert.NotNull(EnemyAi.StrikeOn(state, Starter, soldier, state.Find("wren")!));
    }

    [Fact]
    public void StrikeOnNamesTheOtherStrikeWhenTheSwornUnitIsOutOfReach()
    {
        var state = Lane(hale: new Coord(4, 0), wren: new Coord(11, 3), soldier: new Coord(5, 1));
        var soldier = state.Find("soldier-1")! with { Grudge = "wren" };
        state = state.WithUnit(soldier);

        Assert.NotNull(EnemyAi.StrikeOn(state, Starter, soldier, state.Find("hale")!));
    }

    [Fact]
    public void TheEnemyRowNamesTheSwornUnit()
    {
        var kill = WrenKillsTheBrigand();

        Assert.Contains("group yard, aggressive, sworn: wren", MapRenderer.Render(kill.Next, Starter));
    }

    [Fact]
    public void TheHeaderRoundTripsThroughTheMapFormat()
    {
        var map = MapFixture.Parse(Yard(grudges: true));

        Assert.True(map.GrudgesEnabled);
        Assert.Contains("grudges: on\n", MapFormat.Write(map, Starter));
        Assert.Equal(map, MapFixture.Parse(MapFormat.Write(map, Starter)));
        Assert.False(MapFixture.Parse(Yard(grudges: false)).GrudgesEnabled);
    }

    [Fact]
    public void AGrudgeRoundTripsThroughTheProtocol()
    {
        var state = WrenKillsTheBrigand().Next;

        var json = ProtocolJson.State(state, Starter);

        Assert.Contains("\"grudge\":\"wren\"", json);
        Assert.Equal("wren", ProtocolJson.ReadState(json, Starter).Find("soldier-1")!.Grudge);
    }

    [Fact]
    public void RecallTakesTheGrudgeBackWithTheKill()
    {
        var state = WrenKillsTheBrigand().Next;

        var back = state.Do(new Recall(0));

        Assert.Null(back.Find("soldier-1")!.Grudge);
    }
}
