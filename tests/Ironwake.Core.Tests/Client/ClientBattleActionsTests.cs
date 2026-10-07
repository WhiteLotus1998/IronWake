using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The battle side of issue 786: chests on the board and opened from the action list,
/// Commander's Word called from it with the console's preview, the move a Fall back order owes
/// taken by a click, and the three lines (<c>open</c>, <c>order</c>, <c>fallback</c>) held to
/// the console by the parity gate.
/// </summary>
[Collection("console")]
public class ClientBattleActionsTests
{
    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static string Sample(string name) => Path.Combine(Repo, "docs", "samples", name + ".map");

    private static string Transcript(string name) => Path.Combine(Repo, "docs", "transcripts", name + ".script");

    private static ClientSession Open(string sample, ulong seed, Func<BattleState, BattleState>? edit = null)
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var state = BattleState.From(MapFiles.Load(Sample(sample), content), content, content.Cast, seed);
        return new ClientSession(content, edit is null ? state : edit(state));
    }

    private static BattleState Place(BattleState state, string id, Coord at) => state.WithUnit(state.Find(id)! with { At = at });

    private static string ConsoleLog(string sample, ulong seed, string script)
    {
        var log = Path.Combine(Path.GetTempPath(), $"ironwake-parity-{Guid.NewGuid():N}.log");
        try
        {
            ConsoleCapture.Run(() => PlaySession.Run(new[]
            {
                Sample(sample), "--seed", seed.ToString(), "--script", script,
                "--content", Fixture.RealContentDirectory(), "--log", log,
            }));
            return File.ReadAllText(log);
        }
        finally
        {
            File.Delete(log);
        }
    }

    [Theory]
    [InlineData("open pell 3,4", "open")]
    [InlineData("order press", "press")]
    [InlineData("order rally", "rally")]
    [InlineData("order fall back", "fall back")]
    [InlineData("order fallback", "fall back")]
    [InlineData("fallback pell 3,4", "fallback")]
    [InlineData("fallback pell stay", "fallback")]
    public void ScriptReadsOpenOrderAndFallBackAsCommands(string line, string kind)
    {
        var client = Open("harrow_weir_orders", 85);
        var pell = client.State.Find("pell");

        var command = Script.Parse(line, client.State);

        switch (kind)
        {
            case "open":
                Assert.Equal(new Ironwake.Core.Open("pell", new Coord(3, 4)), command);
                break;
            case "fallback":
                var to = line.EndsWith("stay", StringComparison.Ordinal) ? pell!.At : new Coord(3, 4);
                Assert.Equal(new FallBack("pell", to), command);
                break;
            default:
                Assert.Equal(new Order(Orders.Parse(kind)!.Value), command);
                break;
        }
    }

    [Theory]
    [InlineData("order press preview")]
    [InlineData("order press preview from 5,4")]
    [InlineData("order charge")]
    [InlineData("open pell")]
    public void ScriptSkipsAnOrderPreviewAndAMalformedLine(string line)
    {
        var client = Open("harrow_weir_orders", 85);

        Assert.Null(Script.Parse(line, client.State));
    }

    [Theory]
    [InlineData("strongbox_chests", 649UL, "2026-10-01-strongbox_chests-649", " opens the chest at ")]
    [InlineData("harrow_weir_orders", 85UL, "2026-10-01-harrow_weir_orders-85", " calls press ")]
    public void ClientEventLogMatchesTheConsoleWithChestsAndOrders(string sample, ulong seed, string transcript, string mark)
    {
        var script = Transcript(transcript);
        var console = ConsoleLog(sample, seed, script);
        var client = Open(sample, seed);
        Script.Apply(client, File.ReadAllText(script));

        Assert.Contains(mark, console);
        Assert.Null(Parity.FirstDifference(console, client.LogText));
    }

    [Fact]
    public void ChestsAreTheClosedChestsAndAnOpenedOneLeavesTheBoard()
    {
        var client = Open("strongbox_chests", 649, s => Place(s, "captain", new Coord(1, 9)));
        Assert.Equal(new[] { new Coord(1, 9), new Coord(2, 5), new Coord(3, 0), new Coord(14, 0) }, client.Chests);
        client.Select(new Coord(1, 9));

        var row = Assert.Single(client.Actions(), r => !r.Label.StartsWith("Item:", StringComparison.Ordinal));
        Assert.Equal(row, client.Actions()[0]);
        Assert.Equal("Open chest 1,9", row.Label);
        Assert.Equal(client.Content.ItemName("field_dressing"), row.Line);
        Assert.True(row.Legal);
        Assert.Equal(new Ironwake.Core.Open("captain", new Coord(1, 9)), client.TakeAction(0));

        Assert.DoesNotContain(new Coord(1, 9), client.Chests);
        Assert.Contains(" opens the chest at 1,9", client.LogText);
    }

    [Fact]
    public void AChestRowOnlyOffersAChestOnOrBesideTheUnit()
    {
        var client = Open("strongbox_chests", 649);
        client.Select(client.State.Find("captain")!.At);

        Assert.DoesNotContain(client.Actions(), r => r.Command is Ironwake.Core.Open);
    }

    [Fact]
    public void AChestAnEnemyStandsOnIsGreyedWithTheResolversRefusal()
    {
        var client = Open("strongbox_chests", 649, s => Place(Place(s, "captain", new Coord(3, 1)), "archer-2", new Coord(3, 0)));
        client.Select(new Coord(3, 1));
        var before = client.State;

        var row = Assert.Single(client.Actions(), r => r.Command is Ironwake.Core.Open);
        Assert.False(row.Legal);
        var expected = Resolver.Apply(before, client.Content, row.Command).Rejection!;
        Assert.Equal(UnitNames.Of(before, client.Content).Message(expected.Message), row.Refusal);

        Assert.Null(client.TakeAction(client.Actions().ToList().IndexOf(row)));
        Assert.Equal(row.Refusal, client.Status);
        Assert.Same(before, client.State);
    }

    [Fact]
    public void TheCaptainsOrderRowsCarryTheConsolesPreviewAndCallTheOrder()
    {
        var client = Open("harrow_weir_orders", 85);
        var captain = client.State.Find("captain")!;
        client.Select(captain.At);

        // The captain's item rows (issue 1308) follow the orders, so the orders keep rows 0 to 2.
        var rows = client.Actions().TakeWhile(r => !r.Label.StartsWith("Item:", StringComparison.Ordinal)).ToList();

        Assert.Equal(new[] { "Order: Press", "Order: Rally", "Order: Fall back" }, rows.Select(r => r.Label));
        Assert.All(rows, r => Assert.True(r.Legal));
        Assert.Equal(PlaySession.OrderPreview(client.State, client.Content, OrderKind.Press), rows[0].Line);
        Assert.Equal(new Order(OrderKind.Press), client.TakeAction(0));
        Assert.Equal(OrderKind.Press, client.State.OrderCalled);
        Assert.Contains(" calls press ", client.LogText);
    }

    [Fact]
    public void AHoveredTileInReachPreviewsTheOrderFromThere()
    {
        var client = Open("harrow_weir_orders", 85);
        var captain = client.State.Find("captain")!;
        client.Select(captain.At);
        var tile = client.Reach!.Destinations.First(at => at != captain.At && client.Reach.CanEnd(at));

        var rows = client.Actions(tile);

        Assert.Equal(PlaySession.OrderPreview(client.State, client.Content, OrderKind.Rally, tile), rows[1].Line);
        Assert.Equal(PlaySession.OrderPreview(client.State, client.Content, OrderKind.Rally), client.Actions(new Coord(-1, -1))[1].Line);
    }

    [Fact]
    public void NoOrderRowsForAnAllyOnAMapWithoutOrdersOrOnceTheOrderIsSpent()
    {
        var harrow = Open("harrow_weir_orders", 85);
        harrow.Select(harrow.State.Find("pell")!.At);
        Assert.Empty(harrow.Actions());

        var closed = Open("strongbox_chests", 649);
        closed.Select(closed.State.Find("captain")!.At);
        Assert.DoesNotContain(closed.Actions(), r => r.Command is Order);

        var spent = Open("harrow_weir_orders", 85);
        spent.Select(spent.State.Find("captain")!.At);
        spent.TakeAction(1);
        spent.Select(spent.State.Find("pell")!.At);
        Assert.DoesNotContain(spent.Actions(), r => r.Command is Order);
    }

    [Fact]
    public void AFallBackOrderLetsAnActedAllyBeSelectedAndClickedToItsMove()
    {
        var client = Open("harrow_weir_orders", 85);
        var captain = client.State.Find("captain")!;
        var ally = Orders.InRadius(client.State, client.Content, captain, captain.At).First();
        client.Submit(new Wait(ally.Id));
        Assert.False(client.Select(ally.At));

        client.Select(captain.At);
        client.TakeAction(2);

        Assert.True(client.Select(ally.At));
        var reach = client.State.FallBackReachOf(client.State.Find(ally.Id)!, client.Content)!;
        Assert.Equal(reach, client.Reach);
        var to = reach.Destinations.First(at => at != ally.At && reach.CanEnd(at)
            && Resolver.Apply(client.State, client.Content, new FallBack(ally.Id, at)).Accepted);
        Assert.Equal(new FallBack(ally.Id, to), client.Click(to));
        Assert.Equal(to, client.State.Find(ally.Id)!.At);
        Assert.Null(client.Selected);
        Assert.False(client.Select(to));
    }

    [Fact]
    public void ClickingAFallingBackAllyOnItsOwnTileDeclinesTheMove()
    {
        var client = Open("harrow_weir_orders", 85);
        var captain = client.State.Find("captain")!;
        var ally = Orders.InRadius(client.State, client.Content, captain, captain.At).First();
        client.Submit(new Wait(ally.Id));
        client.Select(captain.At);
        client.TakeAction(2);
        client.Select(ally.At);

        Assert.Equal(new FallBack(ally.Id, ally.At), client.Click(ally.At));

        Assert.Contains(" holds at ", client.LogText);
        Assert.False(client.State.Find(ally.Id)!.FallingBack);
    }
}
