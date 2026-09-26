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

    [Fact]
    public void APlayerCannotStrikeATargetItsSideCannotSee()
    {
        var state = Start(1);

        var refused = state.Refused(new Attack("ottilie", "soldier-1"));

        Assert.Equal(RejectionReason.Unseen, refused.Reason);
        Assert.Null(Queries.Forecast(state, Starter, state.Find("ottilie")!, state.Find("soldier-1")!));
        Assert.Empty(Queries.Targets(state, Starter, state.Find("ottilie")!));
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
}
