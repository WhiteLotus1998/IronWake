using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Rockfall (DESIGN.md 13.26, experiment): a player unit on a ledge brings the rock down as its
/// action; every event on the ledge fires once; a terrain change under a drop strikes whoever
/// stands on its tile for 10, never below 1, and leaves that tile as it was.
/// </summary>
public class RockfallTests
{
    private const string Gorge = """
        name: Gorge
        size: 7x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        .......
        ..n....
        .......
        .......

        units:
        {0}
        E brigand 5,2 group:far behavior:guard

        events:
        rock1 drop 2,1 terrain 4,2 M
        rock2 drop 2,1 terrain 5,2 M
        """;

    private static BattleState Start(string units) =>
        BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Wren), Gorge.Replace("{0}", units));

    private static string At(BattleState state, int x, int y) => state.Map.TerrainIdAt(new Coord(x, y));

    [Fact]
    public void ADropBringsDownEveryEmptyTileOfItsLedge()
    {
        var state = Start("P captain 2,1\nP recruit 0,3").Do(new Drop("hale"));

        Assert.Equal("mountain", At(state, 4, 2));
        Assert.True(state.HasFired("rock1"));
        Assert.True(state.HasFired("rock2"));
    }

    [Fact]
    public void TheRockStrikesAUnitUnderItForTenAndItsTileStaysOpen()
    {
        var before = Start("P captain 2,1\nP recruit 0,3");
        var brigand = before.UnitAt(new Coord(5, 2))!;
        var result = before.Try(new Drop("hale"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(new RockfallStruck(brigand.Id, new Coord(5, 2), 10, brigand.Hp - 10), result.Events);
        Assert.Equal(brigand.Hp - 10, result.Next.Find(brigand.Id)!.Hp);
        Assert.Equal("plain", At(result.Next, 5, 2));
        Assert.Contains(new MapEventFired("rock2", true), result.Events);
    }

    [Fact]
    public void TheRockStrikesAPlayerUnitUnderItToo()
    {
        var result = Start("P captain 2,1\nP recruit 4,2").Try(new Drop("hale"));

        Assert.Contains(result.Events, e => e is RockfallStruck { UnitId: "wren", Amount: 10 });
        Assert.Equal("plain", At(result.Next, 4, 2));
    }

    [Fact]
    public void TheRockNeverTakesAUnitBelowOne()
    {
        var start = Start("P captain 2,1\nP recruit 0,3");
        var brigand = start.UnitAt(new Coord(5, 2))!;
        var result = start.WithUnit(brigand with { Hp = 4 }).Try(new Drop("hale"));

        Assert.Contains(new RockfallStruck(brigand.Id, new Coord(5, 2), 3, 1), result.Events);
        Assert.Equal(1, result.Next.Find(brigand.Id)!.Hp);
    }

    [Fact]
    public void ADropIsTheUnitsAction()
    {
        var state = Start("P captain 2,1\nP recruit 0,3").Do(new Drop("hale"));

        Assert.True(state.Find("hale")!.Acted);
        Assert.Equal(RejectionReason.AlreadyActed, state.Refused(new Wait("hale")).Reason);
    }

    [Fact]
    public void ADropMayFollowAMove()
    {
        var state = Start("P captain 1,1\nP recruit 0,3").Do(new Move("hale", new Coord(2, 1))).Do(new Drop("hale"));

        Assert.Equal("mountain", At(state, 4, 2));
    }

    [Fact]
    public void ADropIsRefusedOffALedge()
    {
        var refusal = Start("P captain 1,1\nP recruit 0,3").Refused(new Drop("hale"));

        Assert.Equal(RejectionReason.CannotDrop, refusal.Reason);
        Assert.Contains("not a ledge", refusal.Message);
    }

    [Fact]
    public void ADropIsRefusedOnceTheRockHasFallen()
    {
        var state = Start("P captain 2,1\nP recruit 0,3").Do(new Drop("hale"));
        var again = state.WithUnit(state.Find("hale")! with { Acted = false, Moved = false });

        var refusal = again.Refused(new Drop("hale"));

        Assert.Equal(RejectionReason.CannotDrop, refusal.Reason);
        Assert.Contains("already fallen", refusal.Message);
    }

    [Fact]
    public void AnEnemyNeverDrops()
    {
        var state = Start("P captain 0,0\nP recruit 0,3");
        var brigand = state.UnitAt(new Coord(5, 2))!;

        Assert.NotNull(Rockfall.Refusal(state.WithUnit(brigand with { At = new Coord(2, 1) }), brigand with { At = new Coord(2, 1) }));
    }

    [Fact]
    public void TheLegalCommandsOfAUnitOnALedgeIncludeTheDrop()
    {
        var state = Start("P captain 2,1\nP recruit 0,3");

        Assert.Contains(new Drop("hale"), Resolver.Legal(state, Starter));
        Assert.DoesNotContain(new Drop("wren"), Resolver.Legal(state, Starter));
    }

    [Fact]
    public void TheBoardNamesTheLedgeUntilItsRockFalls()
    {
        var state = Start("P captain 2,1\nP recruit 0,3");

        Assert.Equal("ledges (drop, an action): 2,1 brings down 4,2 5,2 to Mountain; the rock strikes anyone under it for 10, never below 1, and a tile someone stands on stays open", Rockfall.Line(state, Starter));
        Assert.Null(Rockfall.Line(state.Do(new Drop("hale")), Starter));
    }

    [Fact]
    public void ADropTriggerRoundTripsThroughTheMapFormat()
    {
        var map = MapFixture.Parse(Gorge.Replace("{0}", "P captain 2,1"));

        Assert.Equal(new DropTrigger(new Coord(2, 1)), map.Events[0].Trigger);
        Assert.Equal(map, MapFixture.Parse(MapFormat.Write(map, MapFixture.Content)));
    }

    [Fact]
    public void ADropTriggerWithoutATileIsRefused()
    {
        var ex = Assert.Throws<MapException>(() => MapFixture.Parse(Gorge.Replace("{0}", "P captain 2,1").Replace("rock1 drop 2,1", "rock1 drop")));

        Assert.Contains("drop trigger needs the ledge's tile", ex.Message);
    }
}
