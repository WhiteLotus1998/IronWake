using Ironwake.Content;
using static Ironwake.Core.Tests.Battle.BattleFixture;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The <c>holds:</c> header (issue 1189, round 402): a woken member of the held group with no strike
/// waits on its rectangle and walks back to it, steps off it only to strike, and <c>threat</c> prices
/// that strike; other groups are untouched; the board prints the rule; a bad header is refused
/// naming the field.
/// </summary>
public class HeldGroundTests
{
    private static string Bank(string header, string units = "E soldier 9,1 group:bank behavior:guard") =>
        $"""
        name: Bank
        size: 12x6
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

        units:
        P captain 0,5
        P recruit:wren 0,4
        {units}

        """.Replace("\n\n\n", "\n\n");

    private static BattleState Woken(string header, string units = "E soldier 9,1 group:bank behavior:guard") =>
        BattleFixture.Start(map: Bank(header, units)).Wake("bank") with { Phase = Side.Enemy };

    private static readonly HeldGround Ground = new("bank", new Coord(6, 0), new Coord(11, 1));

    private static BattleState WithHale(BattleState state, Coord at) => state.WithUnit(state.Find("hale")! with { At = at });

    private static Coord? MovedTo(IEnumerable<Command> plan, string id) => plan.OfType<Move>().LastOrDefault(m => m.UnitId == id)?.To;

    [Fact]
    public void AWokenHeldMemberWithNoStrikeWaitsOnItsGround()
    {
        var unheld = EnemyAi.Plan(WithHale(Woken(""), new Coord(3, 5)), Starter);
        var held = EnemyAi.Plan(WithHale(Woken("holds: bank 6,0 11,1"), new Coord(3, 5)), Starter);

        Assert.True(MovedTo(unheld, "soldier-1") is { } off && !Ground.Contains(off), $"unheld ends {MovedTo(unheld, "soldier-1")}");
        Assert.True(Ground.Contains(MovedTo(held, "soldier-1") ?? new Coord(9, 1)), $"held ends {MovedTo(held, "soldier-1")}");
    }

    [Fact]
    public void AWokenHeldMemberStepsOffItsGroundToStrike()
    {
        var plan = EnemyAi.Plan(WithHale(Woken("holds: bank 6,0 11,1"), new Coord(9, 3)), Starter);

        Assert.Contains(plan, c => c is Attack { UnitId: "soldier-1", TargetId: "hale" });
        Assert.True(MovedTo(plan, "soldier-1") is { Y: 2 }, $"{MovedTo(plan, "soldier-1")}");
    }

    [Fact]
    public void AHeldMemberOffItsGroundWithNoStrikeWalksBack()
    {
        var state = WithHale(Woken("holds: bank 6,0 11,1"), new Coord(1, 5));
        state = state.WithUnit(state.Find("soldier-1")! with { At = new Coord(9, 4) });

        Assert.True(MovedTo(EnemyAi.Plan(state, Starter), "soldier-1") is { } back && Ground.Contains(back), $"{MovedTo(EnemyAi.Plan(state, Starter), "soldier-1")}");
    }

    [Fact]
    public void AnotherGroupIsNotHeld()
    {
        var state = WithHale(Woken("holds: bank 6,0 11,1", "E soldier 9,1 group:bank behavior:guard\nE brigand 8,0 group:road behavior:aggressive"), new Coord(3, 5));

        Assert.True(MovedTo(EnemyAi.Plan(state, Starter), "brigand-1") is { } off && !Ground.Contains(off), $"{MovedTo(EnemyAi.Plan(state, Starter), "brigand-1")}");
    }

    [Fact]
    public void ThreatStillPricesTheStrikeFromOffTheGround()
    {
        var state = WithHale(Woken("holds: bank 6,0 11,1"), new Coord(9, 3)) with { Phase = Side.Player };

        Assert.NotNull(EnemyAi.StrikeOn(state, Starter, state.Find("soldier-1")!, state.Find("hale")!));
    }

    [Fact]
    public void TheBoardPrintsTheRuleWhileAHeldMemberStands()
    {
        var state = Woken("holds: bank 6,0 11,1");
        const string line = "holds: the bank group leaves 6,0 to 11,1 only to strike, then goes back";

        Assert.Contains(line, MapRenderer.Render(state, Starter));
        Assert.DoesNotContain("holds:", MapRenderer.Render(Woken(""), Starter));
        Assert.DoesNotContain("holds:", MapRenderer.Render(state.WithoutUnit("soldier-1"), Starter));
    }

    [Fact]
    public void TheHeaderRoundTrips()
    {
        var map = MapFixture.Parse(Bank("holds: bank 6,0 11,1"));

        Assert.Equal(new HeldGround("bank", new Coord(6, 0), new Coord(11, 1)), map.Holds);
        Assert.Contains("holds: bank 6,0 11,1\n", MapFormat.Write(map, Starter));
        Assert.Equal(map, MapFixture.Parse(MapFormat.Write(map, Starter)));
    }

    [Theory]
    [InlineData("holds: bank 6,0", "needs a group and two corners")]
    [InlineData("holds: bank 6,0 eleven", "corner must be x,y")]
    [InlineData("holds: bank 6,0 12,1", "corner 12,1 is outside the 12x6 grid")]
    [InlineData("holds: ford 6,0 11,1", "no enemy is in group 'ford'")]
    [InlineData("holds: bank 0,0 5,1", "soldier at 9,1 is placed outside the ground its group holds")]
    public void ABadHeaderIsRefusedNamingTheField(string header, string message)
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Bank(header)));

        Assert.Contains("holds", error.Message);
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void AGroupWithNoGuardMemberIsRefused()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Bank("holds: bank 6,0 11,1", "E soldier 9,1 group:bank behavior:aggressive")));

        Assert.Contains("group 'bank' has no guard member", error.Message);
    }
}
