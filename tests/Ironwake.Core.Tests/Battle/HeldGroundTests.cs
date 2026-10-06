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

    [Theory]
    [InlineData("E soldier 9,1 group:bank behavior:hold")]
    [InlineData("B bandit_leader 9,1 group:bank behavior:boss")]
    public void AGroupWithNoMemberThatMovesIsRefused(string units)
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Bank("holds: bank 6,0 11,1", units)));

        Assert.Contains("group 'bank' has no guard or aggressive member", error.Message);
    }

    [Fact]
    public void AnAggressiveGroupMayHoldGround()
    {
        var map = MapFixture.Parse(Bank("holds: bank 6,0 11,1", "E soldier 9,1 group:bank behavior:aggressive"));

        Assert.Equal(Ground, map.Holds);
    }

    /// <summary>
    /// Issue 1204, lever 3: a spawned member counts as a member of the held group, and its spawn
    /// tile must lie on the ground, as a placed member's tile must.
    /// </summary>
    [Theory]
    [InlineData("holds: lord 0,2 0,2", null)]
    [InlineData("holds: lord 1,2 1,2", "soldier at 0,2 is placed outside the ground its group holds")]
    public void ASpawnedMemberCountsAndMustSpawnOnItsGround(string header, string? message)
    {
        var text = Bank(header) + "\nevents:\nlord turn 2 enemy spawn soldier 0,2 group:lord behavior:aggressive\n";
        if (message is null)
        {
            Assert.Equal(new HeldGround("lord", new Coord(0, 2), new Coord(0, 2)), MapFixture.Parse(text).Holds);
            return;
        }

        Assert.Contains(message, Assert.Throws<MapException>(() => MapFixture.Parse(text)).Message);
    }

    [Fact]
    public void AOneTilePostPrintsAsOneTile()
    {
        Assert.Equal("holds: the lord group leaves 0,6 only to strike, then goes back", new HeldGround("lord", new Coord(0, 6), new Coord(0, 6)).Line());
    }

    private const string Post = """
        name: Post
        size: 12x6
        win: defeat_boss
        turn_limit: 10
        recall: 3
        enemy_level: 1
        holds: lord 0,2 0,2

        ............
        ............
        ............
        ............
        ............
        ............

        units:
        P captain 11,5
        P recruit:wren 11,4
        E soldier 11,0 group:yard behavior:hold

        events:
        lord turn 1 enemy spawn boss bandit_leader 0,2 group:lord behavior:aggressive

        """;

    private static BattleState Arrived(Coord hale)
    {
        var state = WithHale(BattleFixture.Start(map: Post) with { Phase = Side.Enemy }, hale);
        return MapEvents.AtPhaseStart(state, Starter, new List<GameEvent>());
    }

    /// <summary>
    /// Issue 1204, lever 3 (the keep's Hask): a spawned boss on a one-tile post strikes a unit in his
    /// Move plus his range, not only one beside him, and with no strike in reach walks back to the post.
    /// </summary>
    [Fact]
    public void ASpawnedBossOnAOneTilePostStrikesInReachAndWalksBack()
    {
        var reached = Arrived(new Coord(3, 2));
        var boss = reached.Units.Single(u => u.IsBoss);
        var strike = EnemyAi.PlanUnit(reached, Starter, boss);

        Assert.Equal(new Coord(0, 2), boss.At);
        Assert.Contains(strike, c => c is Attack { TargetId: "hale" });
        Assert.Contains(strike, c => c is Move);

        var away = Arrived(new Coord(11, 3));
        var off = away.WithUnit(away.Units.Single(u => u.IsBoss) with { At = new Coord(2, 2) });

        Assert.Equal(new Coord(0, 2), MovedTo(EnemyAi.PlanUnit(off, Starter, off.Units.Single(u => u.IsBoss)), off.Units.Single(u => u.IsBoss).Id));
        Assert.DoesNotContain(EnemyAi.PlanUnit(away, Starter, away.Units.Single(u => u.IsBoss)), c => c is Move);
    }

    /// <summary>
    /// Issue 1204, lever 3: an arriving boss may be Aggressive, since he has no sleep to wake from; a
    /// placed <c>B</c> line stays boss or guard, and an arriving boss is never Hold.
    /// </summary>
    [Theory]
    [InlineData("B bandit_leader 9,1 group:bank behavior:aggressive", "", "a B line's behavior is boss or guard, got 'aggressive'")]
    [InlineData("E soldier 9,1 group:bank behavior:guard", "\nevents:\nlord turn 2 enemy spawn boss bandit_leader 0,2 group:lord behavior:hold\n", "a boss spawn's behavior is boss, guard or aggressive, got 'hold'")]
    public void OnlyAnArrivingBossMayBeAggressive(string units, string events, string message)
    {
        Assert.Contains(message, Assert.Throws<MapException>(() => MapFixture.Parse(Bank("", units) + events)).Message);
    }
}
