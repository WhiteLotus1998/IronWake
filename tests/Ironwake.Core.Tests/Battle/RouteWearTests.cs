using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The player owns the route (DESIGN.md 13.25 and section 4, issue 782): among the cheapest
/// routes to a tile the one that wears the fewest plank steps is walked, a tile an ally stands
/// on wearing nothing; <c>move ... via</c> walks by way of a chosen tile within Mov; a preview
/// names what a move would wear without making it. On ground that never wears the path is the
/// plain (cost, row-major) one.
/// </summary>
public class RouteWearTests
{
    private const string Square = """
        name: Square
        size: 5x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        {0}
        .....
        .....
        .....
        .....

        units:
        {1}
        E brigand 4,4 group:far behavior:guard
        """;

    private static BattleState Start(string row0, string units, params Unit[] roster) =>
        BattleFixture.Start(7, ValueList<Unit>.From(roster.Length == 0 ? new[] { Hale } : roster), Square.Replace("{0}", row0).Replace("{1}", units));

    private static string At(BattleState state, int x, int y) => state.Map.TerrainIdAt(new Coord(x, y));

    [Fact]
    public void OnGroundThatNeverWearsTheRowMajorFirstNeighbourStillCarriesThePath()
    {
        var state = Start(".....", "P captain 0,0");

        var reach = state.ReachOf(state.Find("hale")!, Starter);

        Assert.Equal(new[] { new Coord(1, 0), new Coord(1, 1) }, reach.EntryAt(new Coord(1, 1))!.Path);
    }

    [Fact]
    public void AmongTheCheapestRoutesTheOneThatWearsTheFewestPlanksIsWalked()
    {
        var state = Start(".+...", "P captain 0,0");

        var result = state.Try(new Move("hale", new Coord(1, 1)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(new[] { new Coord(0, 1), new Coord(1, 1) }, result.Events.OfType<UnitMoved>().Single().Path);
        Assert.Equal("planks", At(result.Next, 1, 0));
        Assert.Empty(result.Events.OfType<TerrainChanged>());
    }

    [Fact]
    public void APlankAnAllyHoldsCountsAsNoWearSoTheRouteCrossesIt()
    {
        var state = Start(".++..", "P captain 0,0\nP recruit:wren 1,0", Hale, Wren);
        state = state with { Map = state.Map.WithTerrain(new Coord(0, 1), "planks") };

        var walked = state.Do(new Move("hale", new Coord(2, 1)));

        Assert.Equal("planks", At(walked, 1, 0));
        Assert.Equal("planks", At(walked, 0, 1));
        Assert.Equal("planks", At(walked, 2, 0));
    }

    [Theory]
    [InlineData("planks", MovementType.Infantry, false, 1)]
    [InlineData("planks", MovementType.Armored, false, 2)]
    [InlineData("planks", MovementType.Cavalry, false, 2)]
    [InlineData("split_planks", MovementType.Armored, false, 1)]
    [InlineData("planks", MovementType.Flying, false, 0)]
    [InlineData("planks", MovementType.Infantry, true, 0)]
    [InlineData("plain", MovementType.Armored, false, 0)]
    public void ARouteWeighsATileAtTheStepsLeavingItWouldWear(string terrain, MovementType movement, bool allyHeld, int wear)
    {
        Assert.Equal(wear, Movement.WearOnLeaving(Starter, Starter.TerrainById(terrain), movement, allyHeld));
    }

    [Fact]
    public void AMoveViaAWaypointWalksThroughItAndWearsTheTileChosen()
    {
        var state = Start(".+...", "P captain 0,0");

        var result = state.Try(new Move("hale", new Coord(1, 1), new Coord(1, 0)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(new[] { new Coord(1, 0), new Coord(1, 1) }, result.Events.OfType<UnitMoved>().Single().Path);
        Assert.Equal("split_planks", At(result.Next, 1, 0));
    }

    [Fact]
    public void AMoveViaCountsBothLegsAgainstMovAndMoveAgainReadsTheTotal()
    {
        var state = Start(".....", "P captain 0,0");
        var mov = state.ReachOf(state.Find("hale")!, Starter).Mov;

        var refused = state.Refused(new Move("hale", new Coord(0, 0), new Coord(mov - 1, 1)));

        Assert.Equal(RejectionReason.OutOfReach, refused.Reason);
        Assert.Contains("via", refused.Message);
        Assert.Contains("movement left at", refused.Message);
    }

    [Fact]
    public void AMoveViaAWaypointOutOfReachIsRefusedNamingTheWaypoint()
    {
        var state = Start(".....", "P captain 0,0");

        var refused = state.Refused(new Move("hale", new Coord(1, 0), new Coord(4, 3)));

        Assert.Equal(RejectionReason.OutOfReach, refused.Reason);
        Assert.Contains("4,3 is not within", refused.Message);
    }

    [Fact]
    public void AMoveViaMayEndOnTheTileTheUnitLeft()
    {
        var state = Start("++...", "P captain 0,0");

        var walked = state.Do(new Move("hale", new Coord(0, 0), new Coord(1, 0)));

        Assert.Equal(new Coord(0, 0), walked.Find("hale")!.At);
        Assert.Equal("planks", At(walked, 0, 0));
        Assert.Equal("split_planks", At(walked, 1, 0));
    }

    [Fact]
    public void AMoveViaMayNotEndOnAnAlly()
    {
        var state = Start(".....", "P captain 0,0\nP recruit:wren 2,0", Hale, Wren);

        var refused = state.Refused(new Move("hale", new Coord(2, 0), new Coord(1, 0)));

        Assert.Contains("occupied by an ally", refused.Message);
    }

    [Fact]
    public void APreviewNamesWhatTheMoveWouldWearAndMovesNothing()
    {
        var state = Start(".+...", "P captain 0,0");

        var preview = Queries.PreviewMove(state, Starter, new Move("hale", new Coord(2, 0), new Coord(1, 0)), out var rejection);

        Assert.Null(rejection);
        Assert.Equal(new[] { (new Coord(1, 0), "split_planks") }, preview!.Worn);
        Assert.Equal(new Coord(0, 0), state.Find("hale")!.At);
        Assert.Equal("planks", At(state, 1, 0));
    }

    [Fact]
    public void APreviewOfARefusedMoveCarriesTheRefusal()
    {
        var state = Start(".....", "P captain 0,0");

        var preview = Queries.PreviewMove(state, Starter, new Move("hale", new Coord(4, 4)), out var rejection);

        Assert.Null(preview);
        Assert.Equal(RejectionReason.OutOfReach, rejection!.Reason);
    }
}
