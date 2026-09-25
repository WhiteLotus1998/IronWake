namespace Ironwake.Core.Tests.Battle;

using static BattleFixture;

/// <summary>
/// Issue 151: a forecast from any tile the unit could still move to, so the player can
/// compare tiles before a move is made and final. The query builds the unit on that
/// tile's terrain at that tile's distance and moves nothing.
/// </summary>
public sealed class ForecastFromTileTests
{
    /// <summary>The Yard with a forest tile beside the brigand, so the tile changes the counter's hit.</summary>
    private const string ForestYard = """
        name: Forest Yard
        size: 6x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ......
        ..^...
        ......
        ......

        units:
        P captain 1,0
        P recruit:wren 0,2
        E brigand 3,1 group:yard behavior:aggressive
        E soldier 3,2 group:yard behavior:aggressive

        """;

    [Fact]
    public void AForecastFromATileIsTheStandingForecastTheUnitWouldHaveThere()
    {
        var state = Start(map: ForestYard);
        var hale = state.Find("hale")!;
        var brigand = state.Find("brigand-1")!;
        var forest = new Coord(2, 1);
        var plain = new Coord(3, 0);

        var fromForest = Queries.Forecast(state, Starter, hale, brigand, forest)!;
        var fromPlain = Queries.Forecast(state, Starter, hale, brigand, plain)!;

        var standingInForest = Queries.Forecast(state.WithUnit(hale with { At = forest }), Starter, hale with { At = forest }, brigand)!;
        Assert.Equal(standingInForest, fromForest);
        Assert.True(fromForest.Defender.HitChance < fromPlain.Defender.HitChance, "the forest's avoid lowers the counter's hit");
        Assert.Equal(fromPlain.Attacker.Damage, fromForest.Attacker.Damage);
        Assert.Equal(hale.At, state.Find("hale")!.At);
    }

    [Fact]
    public void TheStandingForecastIsTheForecastFromTheUnitsOwnTile()
    {
        var state = Start(map: ForestYard).Do(new Move("hale", new Coord(2, 1)));
        var hale = state.Find("hale")!;
        var brigand = state.Find("brigand-1")!;

        Assert.NotNull(Queries.Forecast(state, Starter, hale, brigand));
        Assert.Equal(Queries.Forecast(state, Starter, hale, brigand), Queries.Forecast(state, Starter, hale, brigand, hale.At));
    }

    [Fact]
    public void AForecastFromATileOutOfReachOrOccupiedIsNull()
    {
        var state = Start(map: ForestYard);
        var hale = state.Find("hale")!;
        var brigand = state.Find("brigand-1")!;

        Assert.Null(Queries.Forecast(state, Starter, hale, brigand, new Coord(5, 3)));
        Assert.Null(Queries.Forecast(state, Starter, hale, brigand, new Coord(0, 2)));
        Assert.False(Queries.CanStandOn(state, Starter, hale, new Coord(0, 2)));
        Assert.Null(Queries.Forecast(state, Starter, hale, brigand, new Coord(0, 0)));
        Assert.True(Queries.CanStandOn(state, Starter, hale, new Coord(0, 0)));
        Assert.Null(Queries.Forecast(state, Starter, hale, brigand, new Coord(3, 2)));
    }

    [Fact]
    public void OnceTheUnitHasMovedOnlyItsOwnTileForecasts()
    {
        var state = Start(map: ForestYard).Do(new Move("hale", new Coord(2, 0)));
        var hale = state.Find("hale")!;
        var brigand = state.Find("brigand-1")!;

        Assert.True(hale.Moved);
        Assert.Null(Queries.Forecast(state, Starter, hale, brigand, new Coord(3, 0)));
        Assert.False(Queries.CanStandOn(state, Starter, hale, new Coord(3, 0)));
        Assert.True(Queries.CanStandOn(state, Starter, hale, hale.At));
    }

    [Fact]
    public void AForecastFromATileOutOfTheWeaponsRangeIsNull()
    {
        var state = Start(map: ForestYard);
        var hale = state.Find("hale")!;
        var brigand = state.Find("brigand-1")!;

        Assert.Null(Queries.Forecast(state, Starter, hale, brigand, new Coord(2, 0)));
        Assert.NotNull(Queries.Forecast(state, Starter, hale, brigand, new Coord(3, 0)));
    }
}
