using Ironwake.Content;
using static Ironwake.Core.Tests.Battle.BattleFixture;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The <c>goes_home:</c> header (issue 1372, round 484): a woken member of the named group standing
/// off its post walks home and strikes only from the tile nearest its post; on its post it strikes
/// out as any woken unit does; <c>threat</c> and the danger overlay read the same tile; other groups
/// are untouched; the board prints the rule; a bad header is refused naming the field.
/// </summary>
public class HomingTests
{
    private static string Hall(string header, string units = "E soldier 9,1 group:hall behavior:guard") =>
        $"""
        name: Hall
        size: 12x8
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {header}

        ............
        ............
        ............
        ............
        ............
        ............
        ............
        ............

        units:
        P captain 0,7
        P recruit:wren 0,6
        {units}

        """.Replace("\n\n\n", "\n\n");

    private static BattleState Woken(string header, Coord soldier, Coord hale, string units = "E soldier 9,1 group:hall behavior:guard")
    {
        var state = BattleFixture.Start(map: Hall(header, units)).Wake("hall") with { Phase = Side.Enemy };
        state = state.WithUnit(state.Find("hale")! with { At = hale });
        return state.WithUnit(state.Find("soldier-1")! with { At = soldier });
    }

    private static IReadOnlyList<Command> Plan(BattleState state, string id) => EnemyAi.PlanUnit(state, Starter, state.Find(id)!);

    [Fact]
    public void AMemberOffItsPostWalksHomeAndStrikesFromThere()
    {
        var unbound = Plan(Woken("", new Coord(9, 4), new Coord(10, 1)), "soldier-1");
        var bound = Plan(Woken("goes_home: hall", new Coord(9, 4), new Coord(10, 1)), "soldier-1");

        Assert.Contains(unbound, c => c is Attack { TargetId: "hale" });
        Assert.Equal(new Command[] { new Move("soldier-1", new Coord(9, 1)), new Attack("soldier-1", "hale") }, bound);
    }

    [Fact]
    public void AMemberOffItsPostWithNoStrikeFromHomeEndsAtHome()
    {
        var unbound = Plan(Woken("", new Coord(9, 4), new Coord(9, 7)), "soldier-1");
        var bound = Plan(Woken("goes_home: hall", new Coord(9, 4), new Coord(9, 7)), "soldier-1");

        Assert.Contains(unbound, c => c is Attack { TargetId: "hale" });
        Assert.Equal(new Command[] { new Move("soldier-1", new Coord(9, 1)), new Wait("soldier-1") }, bound);
    }

    [Fact]
    public void AMemberOnItsPostStrikesOutAsBefore()
    {
        var unbound = Plan(Woken("", new Coord(9, 1), new Coord(9, 5)), "soldier-1");
        var bound = Plan(Woken("goes_home: hall", new Coord(9, 1), new Coord(9, 5)), "soldier-1");

        Assert.Contains(bound, c => c is Attack { TargetId: "hale" });
        Assert.Equal(unbound, bound);
    }

    [Fact]
    public void AnotherGroupIsNotBound()
    {
        const string units = "E soldier 9,1 group:hall behavior:guard\nE brigand 3,1 group:road behavior:aggressive";
        var state = Woken("goes_home: hall", new Coord(9, 1), new Coord(3, 4), units).Wake("road");
        state = state.WithUnit(state.Find("brigand-1")! with { At = new Coord(5, 4) });

        Assert.Null(EnemyAi.HomeTile(state, state.Find("brigand-1")!, state.ReachOf(state.Find("brigand-1")!, Starter)));
        Assert.Contains(Plan(state, "brigand-1"), c => c is Attack { TargetId: "hale" });
    }

    [Fact]
    public void ThreatPricesOnlyTheStrikeFromHome()
    {
        var unbound = Woken("", new Coord(9, 4), new Coord(9, 7)) with { Phase = Side.Player };
        var bound = Woken("goes_home: hall", new Coord(9, 4), new Coord(9, 7)) with { Phase = Side.Player };
        var near = Woken("goes_home: hall", new Coord(9, 4), new Coord(10, 1)) with { Phase = Side.Player };

        Assert.NotNull(EnemyAi.StrikeOn(unbound, Starter, unbound.Find("soldier-1")!, unbound.Find("hale")!));
        Assert.Null(EnemyAi.StrikeOn(bound, Starter, bound.Find("soldier-1")!, bound.Find("hale")!));
        Assert.Equal(new Coord(9, 1), EnemyAi.StrikeOn(near, Starter, near.Find("soldier-1")!, near.Find("hale")!)?.From);
        Assert.Contains(new Coord(9, 7), Threat.StruckByUnit(unbound, Starter, unbound.Find("soldier-1")!));
        Assert.DoesNotContain(new Coord(9, 7), Threat.StruckByUnit(bound, Starter, bound.Find("soldier-1")!));
        Assert.Contains(new Coord(10, 1), Threat.StruckByUnit(bound, Starter, bound.Find("soldier-1")!));
    }

    [Fact]
    public void TheBoardPrintsTheRuleWhileAMemberStands()
    {
        var state = Woken("goes_home: hall", new Coord(9, 1), new Coord(3, 4));
        const string line = "goes_home: the hall group strikes out from its posts, and off them walks home and strikes only from there";

        Assert.Contains(line, MapRenderer.Render(state, Starter));
        Assert.DoesNotContain("goes_home:", MapRenderer.Render(Woken("", new Coord(9, 1), new Coord(3, 4)), Starter));
        Assert.DoesNotContain("goes_home:", MapRenderer.Render(state.WithoutUnit("soldier-1"), Starter));
    }

    [Fact]
    public void TheHeaderRoundTrips()
    {
        var map = MapFixture.Parse(Hall("goes_home: hall"));

        Assert.Equal(new Homing("hall"), map.Homing);
        Assert.Contains("goes_home: hall\n", MapFormat.Write(map, Starter));
        Assert.Equal(map, MapFixture.Parse(MapFormat.Write(map, Starter)));
    }

    [Theory]
    [InlineData("goes_home: hall road", "goes_home", "needs one group")]
    [InlineData("goes_home: ford", "goes_home", "no enemy is placed in group 'ford'")]
    public void ABadHeaderIsRefusedNamingTheField(string header, string field, string message)
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Hall(header)));

        Assert.Contains(field, error.Message);
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void AGroupWithNoMemberThatLeavesItsPostIsRefused()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Hall("goes_home: hall", "E soldier 9,1 group:hall behavior:hold")));

        Assert.Contains("group 'hall' has no guard or aggressive member", error.Message);
    }
}
