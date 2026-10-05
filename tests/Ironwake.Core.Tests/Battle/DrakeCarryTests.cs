using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The drake's carry (issue 805; shipped by issue 1094): on every campaign map and on a <c>carry:</c> sample a
/// rider whose drake is Grown or more, unmoved and not acted, lifts an adjacent ally that has neither moved
/// nor acted, flies to a tile its Move reaches, and sets the ally down beside it, as its whole turn. The ally
/// lands free to move and act.
/// </summary>
public class DrakeCarryTests
{
    private const string River = """
        name: River
        size: 8x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        carry: rook

        ...~~...
        ...~~...
        ...~~...
        ...~~...
        ...~~...

        units:
        P captain 0,0
        P recruit:rook 1,2
        P recruit:wren 1,3
        E brigand 7,0 group:far behavior:guard
        """;

    private static readonly Unit Rook = Recruit("rook", "skyrider", new Stats(20, 7, 0, 6, 8, 4, 4, 2, 3), "iron_lance");

    private static readonly Coord Over = new(5, 2);
    private static readonly Coord Down = new(5, 3);

    private static BattleState Start(string? map = null) =>
        BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Rook, Wren), map ?? River);

    /// <summary>The River without its header, Rook's drake Grown as a campaign grows it.</summary>
    private static BattleState Headerless()
    {
        var start = Start(River.Replace("carry: rook\n", ""));
        var rook = start.Find("rook")!;
        return start.WithUnit(rook with { Unit = rook.Unit with { Drake = new DrakeState(DrakeStage.Grown, 2) } });
    }

    [Fact]
    public void TheHeadersRiderIsPlacedWithAGrownDrake()
    {
        Assert.Equal(DrakeStage.Grown, Start().Find("rook")!.Unit.Drake!.Stage);
    }

    [Fact]
    public void ACarryFliesTheAllyOverWaterItCannotWalk()
    {
        var result = Start().Try(new Carry("rook", "wren", Over, Down));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(Over, result.Next.Find("rook")!.At);
        Assert.Equal(Down, result.Next.Find("wren")!.At);
        Assert.Contains(new Carried("rook", "wren", new Coord(1, 2), Over, new Coord(1, 3), Down), result.Events);
    }

    [Fact]
    public void TheCarryIsTheRidersWholeTurn()
    {
        var rook = Start().Do(new Carry("rook", "wren", Over, Down)).Find("rook")!;

        Assert.True(rook.Moved);
        Assert.True(rook.Acted);
        Assert.Null(rook.Canto);
    }

    [Fact]
    public void TheAllyLandsUnmovedAndMayMoveAndAct()
    {
        var state = Start().Do(new Carry("rook", "wren", Over, Down));
        var wren = state.Find("wren")!;

        Assert.False(wren.Moved);
        Assert.False(wren.Acted);
        Assert.True(wren.Shoved);
        state.Do(new Move("wren", new Coord(6, 3)));
    }

    [Fact]
    public void TheLandedAllyMayStrike()
    {
        var state = Start().Do(new Carry("rook", "wren", new Coord(6, 1), new Coord(6, 0)));

        Assert.Contains(Resolver.Legal(state, Starter), c => c is Attack { UnitId: "wren" });
    }

    [Fact]
    public void ACarryIsRefusedOutsideTheCampaignWithoutTheHeader()
    {
        var refusal = Headerless().Refused(new Carry("rook", "wren", Over, Down));

        Assert.Equal(RejectionReason.CannotCarry, refusal.Reason);
        Assert.Contains("campaign maps", refusal.Message);
    }

    [Fact]
    public void TheCarryIsOpenOnEveryCampaignMainMapWithoutAHeader()
    {
        var result = (Headerless() with { CampaignMap = 1 }).Try(new Carry("rook", "wren", Over, Down));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(Down, result.Next.Find("wren")!.At);
    }

    [Fact]
    public void TheCarryIsOpenOnACampaignSideMapWithoutAHeader()
    {
        var result = (Headerless() with { SideMap = true }).Try(new Carry("rook", "wren", Over, Down));

        Assert.True(result.Accepted, result.Rejection?.Message);
    }

    [Fact]
    public void TheBoardPrintsTheCarryLineOnACampaignMapAndNotOutside()
    {
        Assert.Null(DrakeCarry.Line(Headerless()));
        Assert.Contains("lands free to move and act", DrakeCarry.Line(Headerless() with { SideMap = true }));
    }

    [Fact]
    public void AHalfGrownDrakeCannotCarry()
    {
        var start = Start();
        var rook = start.Find("rook")!;
        var young = start.WithUnit(rook with { Unit = rook.Unit with { Drake = new DrakeState(DrakeStage.HalfGrown, 0) } });

        Assert.Contains("no grown drake", young.Refused(new Carry("rook", "wren", Over, Down)).Message);
    }

    [Fact]
    public void ARiderThatMovedCannotCarry()
    {
        var state = Start().Do(new Move("rook", new Coord(0, 3)));

        Assert.Contains("already moved", state.Refused(new Carry("rook", "wren", Over, Down)).Message);
    }

    [Fact]
    public void AGroundedRiderCannotCarry()
    {
        var start = Start();
        var grounded = start.WithUnit(start.Find("rook")! with { Grounded = 1 });

        Assert.Contains("grounded", grounded.Refused(new Carry("rook", "wren", Over, Down)).Message);
    }

    [Fact]
    public void TheAllyMustStandBesideTheRider()
    {
        Assert.Contains("not beside it", Start().Refused(new Carry("rook", "hale", Over, Down)).Message);
    }

    [Fact]
    public void AnAllyThatMovedCannotBeCarried()
    {
        var state = Start().Do(new Move("wren", new Coord(0, 2)));

        Assert.Contains("already moved or acted", state.Refused(new Carry("rook", "wren", Over, Down)).Message);
    }

    [Fact]
    public void TheDestinationMustBeWithinTheRidersMove()
    {
        Assert.Contains("not within", Start().Refused(new Carry("rook", "wren", new Coord(7, 4), new Coord(6, 4))).Message);
    }

    [Fact]
    public void TheSetDownTileMustBeBesideTheDestination()
    {
        Assert.Contains("not beside", Start().Refused(new Carry("rook", "wren", Over, new Coord(7, 3))).Message);
    }

    [Fact]
    public void TheAllyIsNeverSetDownOnGroundItCannotStandOn()
    {
        Assert.Contains("cannot stand on", Start().Refused(new Carry("rook", "wren", Over, new Coord(4, 2))).Message);
    }

    [Fact]
    public void TheAllyIsNeverSetDownOnATakenTile()
    {
        var state = Start().Do(new Move("hale", new Coord(1, 0)));

        Assert.Contains("is taken by hale", state.Refused(new Carry("rook", "wren", new Coord(1, 1), new Coord(1, 0))).Message);
    }

    [Fact]
    public void TheRiderMayLandOnTheTileTheAllyLeft()
    {
        var state = Start().Do(new Carry("rook", "wren", new Coord(1, 3), new Coord(2, 3)));

        Assert.Equal(new Coord(1, 3), state.Find("rook")!.At);
        Assert.Equal(new Coord(2, 3), state.Find("wren")!.At);
    }

    [Fact]
    public void TheCarryHeaderWritesBackCanonically()
    {
        var map = MapFixture.Parse(River.Replace("\r\n", "\n"));

        Assert.Equal("rook", map.CarryRider);
        Assert.Contains("carry: rook\n", MapFormat.Write(map, MapFixture.Content));
    }

    [Theory]
    [InlineData("waited")]
    [InlineData("brace")]
    [InlineData("free")]
    public void ADroppedCarrySettingIsRefusedOnLoad(string setting)
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(River.Replace("carry: rook", $"carry: {setting} rook")));

        Assert.Contains($"the setting '{setting}' was dropped", error.Message);
        Assert.Contains("carry", error.Message);
    }

    [Fact]
    public void ACarryHeaderNamesOneRider()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(River.Replace("carry: rook", "carry: rook wren")));

        Assert.Contains("carry needs '<rider>'", error.Message);
    }

    [Fact]
    public void ACarryRiderMustBePlacedByName()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(River.Replace("carry: rook", "carry: teodor")));

        Assert.Contains("no 'P recruit:teodor' line", error.Message);
    }
}
