using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The <c>wake_links:</c> header (issue 393, DESIGN.md sections 8 and 10): when the first group of
/// a pair wakes, the second wakes with it in the same check, with the cause <c>call</c>. The
/// link runs one way, the loader refuses a pair it cannot honor, and the legend names it.
/// </summary>
public class WakeLinkTests
{
    /// <summary>A 12x4 field: Hale at 0,1 and Wren at 0,2, a Guard soldier in group ford at 7,0, a Guard archer in group weir at 11,3.</summary>
    private const string Field = """
        name: Field
        size: 12x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        wake_links: ford>weir

        ............
        ............
        ............
        ............

        units:
        P captain 0,1
        P recruit:wren 0,2
        E soldier 7,0 group:ford behavior:guard
        E archer 11,3 group:weir behavior:guard

        """;

    private static IEnumerable<GroupWoke> Woke(ApplyResult result) => result.Events.OfType<GroupWoke>();

    [Fact]
    public void WakingTheFordWakesTheWeir()
    {
        var result = Start(map: Field).Try(new Move("hale", new Coord(3, 0)));

        Assert.Equal(
            new[] { new GroupWoke("ford", WakeCause.Proximity), new GroupWoke("weir", WakeCause.Call, CalledBy: "ford") },
            Woke(result));
        Assert.True(result.Next.IsAwake("weir"));
    }

    [Fact]
    public void WithoutTheLinkTheWeirSleepsThroughTheFordWaking()
    {
        var result = Start(map: Field.Replace("wake_links: ford>weir\n", "")).Try(new Move("hale", new Coord(3, 0)));

        Assert.Equal(new[] { new GroupWoke("ford", WakeCause.Proximity) }, Woke(result));
        Assert.False(result.Next.IsAwake("weir"));
    }

    [Fact]
    public void ALinkRunsOneWay()
    {
        var state = Start(map: Field);
        var result = state.WithUnit(state.Find("hale")! with { At = new Coord(11, 2) }).Try(new Move("wren", new Coord(1, 2)));

        Assert.Equal(new[] { new GroupWoke("weir", WakeCause.Proximity) }, Woke(result));
        Assert.False(result.Next.IsAwake("ford"));
    }

    [Fact]
    public void AnAwakeGroupIsNotCalledAgain()
    {
        var state = Start(map: Field).Wake("weir");
        var result = state.Try(new Move("hale", new Coord(3, 0)));

        Assert.Equal(new[] { new GroupWoke("ford", WakeCause.Proximity) }, Woke(result));
    }

    [Fact]
    public void TheExposureSumSeesACalledGroupAwake()
    {
        var state = Start(map: Field);
        var board = Exposure.Board(state, Starter, state.Find("hale")!, new Coord(3, 0));

        Assert.True(board.IsAwake("weir"));
    }

    [Fact]
    public void TheWakeLinksHeaderParsesAndWritesBack()
    {
        var map = MapFixture.Parse(Field);

        Assert.Equal(new[] { new WakeLink("ford", "weir") }, map.WakeLinks);
        Assert.Equal(Field.Replace("\r\n", "\n"), MapFormat.Write(map, MapFixture.Content));
        Assert.Empty(MapFixture.Parse(Field.Replace("wake_links: ford>weir\n", "")).WakeLinks);
    }

    [Theory]
    [InlineData("ford", "wake_links: 'ford' is not a pair 'from>to'")]
    [InlineData("ford>moat", "wake_links: no enemy is in group 'moat'")]
    [InlineData("ford>ford", "wake_links: group 'ford' links to itself")]
    [InlineData("ford>weir, ford>weir", "wake_links: 'ford>weir' is listed twice")]
    public void TheWakeLinksHeaderRefusesAPairItCannotHonor(string value, string message)
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Field.Replace("wake_links: ford>weir", "wake_links: " + value)));

        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void TheWakeLinksHeaderRefusesAGroupWithNoGuardToCall()
    {
        var text = Field.Replace("E archer 11,3 group:weir behavior:guard", "E archer 11,3 group:weir behavior:hold");
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(text));

        Assert.Contains("wake_links: group 'weir' has no guard member", error.Message);
    }

    [Fact]
    public void TheLegendNamesALinkWhileItsGroupSleeps()
    {
        var state = Start(map: Field);

        Assert.Contains("called: group weir wakes when group ford does\n", MapRenderer.Render(state, Starter));
        Assert.Contains("called: group weir wakes when group ford does\n", MapRenderer.Render(state.Map, Starter));
        Assert.DoesNotContain("called:", MapRenderer.Render(state.Wake("weir"), Starter));
    }
}
