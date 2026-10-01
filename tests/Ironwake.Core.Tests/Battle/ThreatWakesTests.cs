using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 458: <c>threat ... from</c> names the sleeping groups the stop would wake, by proximity
/// and through <c>wake_links:</c>, read from the wake check itself on the board with the unit
/// moved, unpriced; nothing on a tile that wakes nothing, and nothing on the unit's own tile.
/// </summary>
public class ThreatWakesTests
{
    /// <summary>The ford's brigand four tiles from 4,2; the weir's soldier six from it, linked to the ford.</summary>
    private const string Weir = """
        name: Weir
        size: 9x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        wake_links: ford>weir

        .........
        .........
        .........
        .........
        .........

        units:
        P captain 0,2
        E brigand 8,2 group:ford behavior:guard
        E soldier 8,0 group:weir behavior:guard

        """;

    private static BattleState State() => Start(map: Weir);

    private static BattleUnit Captain(BattleState state) => state.UnitsOf(Side.Player).First();

    private static string Text(BattleState state, Coord tile)
    {
        var unit = Captain(state);
        return Ironwake.Cli.PlaySession.ThreatText(state, Starter, unit, tile, Queries.Threats(state, Starter, unit, tile)!, Queries.SleepingThreats(state, Starter, unit, tile)!, wakes: Queries.StopWakes(state, Starter, unit, tile));
    }

    [Fact]
    public void AStopInsideTheRadiusNamesTheGroupItWakesAndTheGroupItCalls()
    {
        var state = State();

        var wakes = Queries.StopWakes(state, Starter, Captain(state), new Coord(4, 2))!;

        Assert.Equal(new[] { ("ford", WakeCause.Proximity, (string?)null), ("weir", WakeCause.Call, "ford") }, wakes.Select(w => (w.Group, w.Cause, w.CalledBy)));
        Assert.Contains("\n  Stopping here wakes: the ford group (proximity), the weir group (called by the ford group)", Text(state, new Coord(4, 2)));
    }

    [Fact]
    public void AStopOutsideTheRadiusPrintsNoWakeLine()
    {
        var state = State();

        Assert.Empty(Queries.StopWakes(state, Starter, Captain(state), new Coord(3, 2))!);
        Assert.DoesNotContain("stopping here wakes", Text(state, new Coord(3, 2)));
    }

    [Fact]
    public void TheUnitsOwnTileWakesNothingNew()
    {
        var state = State();
        var captain = Captain(state);

        Assert.Empty(Queries.StopWakes(state, Starter, captain, captain.At)!);
        Assert.DoesNotContain("stopping here wakes", Text(state, captain.At));
    }

    [Fact]
    public void ATileTheUnitCannotStandOnHasNoAnswer()
    {
        var state = State();

        Assert.Null(Queries.StopWakes(state, Starter, Captain(state), new Coord(8, 4)));
    }

    [Fact]
    public void TheProtocolsThreatAnswerCarriesTheWakes()
    {
        var state = State();
        var captain = Captain(state);

        var near = new Ironwake.Cli.ProtocolSession(Starter, state, TextWriter.Null).Answer("{\"query\":\"threat\",\"unit\":\"" + captain.Id + "\",\"from\":{\"x\":4,\"y\":2}}");
        var home = new Ironwake.Cli.ProtocolSession(Starter, state, TextWriter.Null).Answer($$"""{"query":"threat","unit":"{{captain.Id}}"}""");

        Assert.Contains("\"wakes\":[{\"group\":\"ford\",\"cause\":\"proximity\"},{\"group\":\"weir\",\"cause\":\"call\",\"by\":\"ford\"}]", near);
        Assert.Contains("\"wakes\":[]", home);
    }
}
