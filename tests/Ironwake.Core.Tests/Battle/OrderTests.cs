using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Commander's Word, arm B (DESIGN.md 13.2, issue 85; rounds 206 to 209): once a map, as the
/// captain's action after his Move or without one, an order reaches the allies within
/// <c>2 + Cha / 4</c> of him, Manhattan. Press gives +1 Mov this phase to those who have not
/// moved, Rally heals 15 percent of max HP (at least 1), Fall back owes those who have acted one
/// move of up to 2 that wakes no one. Open behind <c>orders: on</c> and on campaign maps from the
/// second. A Recall restores the charge with the board.
/// </summary>
public class OrderTests
{
    private const string Field =
        """
        name: Field
        size: 14x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        orders: on

        ..............
        ..............
        ..............
        ..............
        ..............

        units:
        P captain 2,2
        P recruit:wren 3,2
        P recruit:ivo 7,2
        E soldier 12,2 group:camp behavior:guard

        """;

    private static ValueList<Unit> Three => ValueList<Unit>.Of(Hale, Wren, Ivo);

    private static BattleState Begin() => Start(roster: Three, map: Field);

    [Fact]
    public void TheOrdersHeaderParsesAndRoundTrips()
    {
        var map = MapFixture.Parse(Field, "field.map");

        Assert.True(map.OrdersEnabled);
        Assert.Contains("orders: on\n", MapFormat.Write(map, Starter));
        Assert.Equal(map, MapFixture.Parse(MapFormat.Write(map, Starter), "again.map"));
    }

    [Fact]
    public void TheKeepRoundSampleIsHarrowWeirWithOrdersAndIsCanonical()
    {
        var path = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "harrow_weir_orders.map");

        var sample = MapFiles.Load(path, Starter);

        Assert.True(sample.OrdersEnabled);
        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), MapFormat.Write(sample, Starter));
    }

    [Fact]
    public void AnOrderIsRefusedOnAMapWithoutOrders()
    {
        var refused = Start().Refused(new Order(OrderKind.Press));

        Assert.Equal(RejectionReason.CannotOrder, refused.Reason);
        Assert.Contains("there are no orders on this map", refused.Message);
    }

    [Fact]
    public void OrdersOpenOnCampaignMapsFromTheSecond()
    {
        Assert.False((Start() with { CampaignMap = 1 }).OrdersOpen);
        Assert.True((Start() with { CampaignMap = 2 }).OrdersOpen);
        Assert.True((Start() with { CampaignMap = 9 }).OrdersOpen);
        Assert.False(Start().OrdersOpen);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(9, 4)]
    [InlineData(16, 6)]
    public void TheRadiusIsTwoPlusChaOverFour(int cha, int radius)
    {
        var captain = Begin().Find("hale")! with { Unit = Hale with { Stats = Hale.Stats with { Cha = cha } } };

        Assert.Equal(radius, Orders.Radius(captain, Starter));
    }

    [Fact]
    public void PressGivesOneMovToTheUnmovedAlliesInTheRadiusOnly()
    {
        var before = Begin();
        var result = before.Try(new Order(OrderKind.Press));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var called = Assert.Single(result.Events.OfType<OrderCalled>());
        Assert.Equal(ValueList<string>.Of("wren"), called.Reached);
        Assert.Equal((4, 1, 2), (called.Radius, called.InRadius, called.Alive));
        var next = result.Next;
        Assert.True(next.Find("wren")!.Pressed);
        Assert.False(next.Find("ivo")!.Pressed);
        Assert.Equal(before.ReachOf(before.Find("wren")!, Starter).Mov + 1, next.ReachOf(next.Find("wren")!, Starter).Mov);
        Assert.True(next.Find("hale")!.Acted);
        Assert.Equal(OrderKind.Press, next.OrderCalled);
    }

    [Fact]
    public void PressSkipsAnAllyThatHasAlreadyMoved()
    {
        var state = Begin().Do(new Move("wren", new Coord(3, 1))).Do(new Order(OrderKind.Press));

        Assert.False(state.Find("wren")!.Pressed);
    }

    [Fact]
    public void RallyHealsFifteenPercentOfMaxHpAtLeastOneNeverPastMax()
    {
        var hurt = Begin();
        hurt = hurt.WithUnit(hurt.Find("wren")! with { Hp = 10 });

        var result = hurt.Try(new Order(OrderKind.Rally));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var healed = Assert.Single(result.Events.OfType<UnitHealed>());
        Assert.Equal(("wren", 3, 13), (healed.UnitId, healed.Amount, healed.HpAfter));
        Assert.Equal(13, result.Next.Find("wren")!.Hp);
        Assert.Equal(1, Orders.RallyHeal(hurt.Find("wren")! with { Hp = 19 }, Starter));
        Assert.Equal(0, Orders.RallyHeal(hurt.Find("wren")! with { Hp = 20 }, Starter));
    }

    [Fact]
    public void RallyReachesNoOneOutsideTheRadius()
    {
        var hurt = Begin();
        hurt = hurt.WithUnit(hurt.Find("ivo")! with { Hp = 5 });

        var next = hurt.Do(new Order(OrderKind.Rally));

        Assert.Equal(5, next.Find("ivo")!.Hp);
    }

    [Fact]
    public void FallBackOwesAnActedAllyAMoveOfTwo()
    {
        var state = Begin().Do(new Wait("wren")).Do(new Order(OrderKind.FallBack));

        Assert.True(state.Find("wren")!.FallingBack);
        Assert.Equal(RejectionReason.OutOfReach, state.Refused(new FallBack("wren", new Coord(6, 2))).Reason);
        var moved = state.Try(new FallBack("wren", new Coord(4, 1)));
        Assert.True(moved.Accepted, moved.Rejection?.Message);
        var fell = Assert.Single(moved.Events.OfType<FellBack>());
        Assert.Equal((new Coord(3, 2), new Coord(4, 1)), (fell.From, fell.To));
        Assert.Equal(new Coord(4, 1), moved.Next.Find("wren")!.At);
        Assert.False(moved.Next.Find("wren")!.FallingBack);
        Assert.Equal(RejectionReason.NoFallBack, moved.Next.Refused(new FallBack("wren", new Coord(4, 2))).Reason);
    }

    [Fact]
    public void FallBackSkipsAnAllyThatHasNotActed()
    {
        var state = Begin().Do(new Order(OrderKind.FallBack));

        Assert.False(state.Find("wren")!.FallingBack);
        Assert.Equal(RejectionReason.NoFallBack, state.Refused(new FallBack("wren", new Coord(3, 1))).Reason);
    }

    [Fact]
    public void AFallBackThatWouldWakeAGroupIsRefusedNamingIt()
    {
        var state = Begin().Do(new Move("hale", new Coord(4, 2))).Do(new Move("wren", new Coord(6, 2))).Do(new Wait("wren"));
        state = state.Do(new Order(OrderKind.FallBack));

        var refused = state.Refused(new FallBack("wren", new Coord(8, 2)));

        Assert.Equal(RejectionReason.NoFallBack, refused.Reason);
        Assert.Contains("it would wake camp", refused.Message);
        Assert.True(state.Try(new FallBack("wren", new Coord(7, 1))).Accepted);
    }

    [Fact]
    public void TheOrderIsOnceAMapAndARecallRestoresIt()
    {
        var called = Begin().Do(new Order(OrderKind.Rally)).Do(new EndPhase());
        while (called.Phase != Side.Player)
        {
            called = called.Do(Resolver.Legal(called, Starter).First());
        }

        var refused = called.Refused(new Order(OrderKind.Press));
        Assert.Equal(RejectionReason.CannotOrder, refused.Reason);
        Assert.Contains("the order is spent this map (rally)", refused.Message);

        var recalled = called.Do(new Recall(0));
        Assert.Null(recalled.OrderCalled);
        Assert.True(recalled.Try(new Order(OrderKind.Press)).Accepted);
    }

    [Fact]
    public void TheOrderIsTheCaptainsActionAndComesAfterHisMove()
    {
        var waited = Begin().Do(new Wait("hale"));
        Assert.Equal(RejectionReason.CannotOrder, waited.Refused(new Order(OrderKind.Press)).Reason);

        var moved = Begin().Do(new Move("hale", new Coord(5, 2))).Do(new Order(OrderKind.Press));
        Assert.True(moved.Find("hale")!.Acted);
        Assert.True(moved.Find("ivo")!.Pressed);
        Assert.Equal(RejectionReason.AlreadyActed, moved.Refused(new Move("hale", new Coord(5, 3))).Reason);
    }

    [Fact]
    public void PressAndFallBackLapseWhenThePhaseEnds()
    {
        var state = Begin().Do(new Wait("ivo")).Do(new Order(OrderKind.Press));
        Assert.True(state.Find("wren")!.Pressed);

        var ended = state.Do(new EndPhase());

        Assert.All(ended.UnitsOf(Side.Player), u => Assert.False(u.Pressed || u.FallingBack));
    }

    [Fact]
    public void LegalListsTheOrdersAndOnlyTheFallBacksThatWakeNoOne()
    {
        var start = Begin();
        var legal = Resolver.Legal(start, Starter).ToList();
        Assert.Contains(new Order(OrderKind.Press), legal);
        Assert.Contains(new Order(OrderKind.FallBack), legal);

        var state = start.Do(new Move("hale", new Coord(4, 2))).Do(new Move("wren", new Coord(6, 2))).Do(new Wait("wren")).Do(new Order(OrderKind.FallBack));
        var falls = Resolver.Legal(state, Starter).OfType<FallBack>().ToList();
        Assert.Contains(new FallBack("wren", new Coord(6, 2)), falls);
        Assert.DoesNotContain(new FallBack("wren", new Coord(8, 2)), falls);
        Assert.DoesNotContain(new Order(OrderKind.Press), Resolver.Legal(state, Starter));
    }

    [Fact]
    public void TheCalledOrderAndTheUnitFlagsSurviveTheProtocolAndTheCanonicalText()
    {
        var state = Begin().Do(new Wait("wren")).Do(new Order(OrderKind.FallBack));

        var back = ProtocolJson.ReadState(ProtocolJson.State(state, Starter), Starter);

        Assert.Equal(OrderKind.FallBack, back.OrderCalled);
        Assert.True(back.Find("wren")!.FallingBack);
        Assert.Equal(state.Canonical(), back.Canonical());
        Assert.Contains("order fall back\n", state.Canonical());
        Assert.Contains(" fallingback", state.Canonical());
        Assert.Equal(new Order(OrderKind.FallBack), ProtocolJson.ReadCommand("""{"type":"order","kind":"fallBack"}"""));
    }

    [Fact]
    public void ThePreviewCountsWhoTheOrderWouldReachFromATile()
    {
        var state = Begin();

        Assert.Equal("Press would reach wren (1 of 2; radius 4 from 2,2)", PlaySession.OrderPreview(state, Starter, OrderKind.Press));
        Assert.Equal("Press would reach ivo, wren (2 of 2; radius 4 from 5,2)", PlaySession.OrderPreview(state, Starter, OrderKind.Press, new Coord(5, 2)));
        Assert.Equal("Fall back would reach no one (0 of 2; radius 4 from 2,2)", PlaySession.OrderPreview(state, Starter, OrderKind.FallBack));
    }

    [Fact]
    public void TheOrderLineNamesTheReachedOverTheLiving()
    {
        var line = PlaySession.Describe(new OrderCalled("hale", OrderKind.Press, 4, ValueList<string>.Of("wren"), 1, 2, 0), Starter, UnitNames.None);

        Assert.Equal("Hale calls press (radius 4): wren (1 of 2)", line);
    }
}
