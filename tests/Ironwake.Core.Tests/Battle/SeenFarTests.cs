using Ironwake.Content;
using static Ironwake.Core.Tests.Battle.BattleFixture;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The <c>seen_far:</c> header (issue 973): a sleeping Guard member hears proximity to the named
/// unit the header's tiles farther than the wake rule's radius while that unit stands on the player
/// side; every other unit, every noise and an enemy-side unit of the same id read the rule's radius;
/// the board prints it, and a bad header is refused naming the field.
/// </summary>
public class SeenFarTests
{
    private static string Field(string header) =>
        $"""
        name: Field
        size: 16x7
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {header}

        ................
        ................
        ................
        ................
        ................
        ................
        ................

        units:
        P captain 0,0
        P recruit:wren 0,6
        E soldier 9,3 group:watch behavior:guard

        """.Replace("\n\n\n", "\n\n");

    private static BattleState Start(string header) => BattleFixture.Start(map: Field(header));

    /// <summary>Whether <paramref name="id"/> standing on <paramref name="at"/> wakes the watch on <paramref name="state"/>.</summary>
    private static bool Wakes(BattleState state, string id, Coord at)
    {
        var after = state.WithUnit(state.Find(id)! with { At = at });
        return WakeCheck.Run(state, after, Starter, Array.Empty<Noise>(), Array.Empty<string>()).Any(w => w.Group == "watch");
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    public void TheSeenUnitWakesASleeperFromTheRadiusPlusTheHeadersTiles(int away, bool wakes)
    {
        Assert.Equal(wakes, Wakes(Start("seen_far: wren 2"), "wren", new Coord(9 - away, 3)));
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(6, false)]
    public void AnotherUnitWakesASleeperOnlyAtTheRulesRadius(int away, bool wakes)
    {
        Assert.Equal(wakes, Wakes(Start("seen_far: wren 2"), "hale", new Coord(9 - away, 3)));
    }

    [Fact]
    public void WithoutTheHeaderTheSeenUnitReadsTheRulesRadius()
    {
        Assert.False(Wakes(Start(""), "wren", new Coord(4, 3)));
    }

    [Fact]
    public void NoiseIsUnchangedByTheSighting()
    {
        var state = Start("seen_far: wren 2");
        var near = new[] { new Noise(new Coord(3, 3), Starter.NoiseRadius) };
        var far = new[] { new Noise(new Coord(2, 3), Starter.NoiseRadius) };

        Assert.Single(WakeCheck.Run(state, state, Starter, near, Array.Empty<string>()));
        Assert.Empty(WakeCheck.Run(state, state, Starter, far, Array.Empty<string>()));
    }

    [Fact]
    public void AnEnemySideUnitOfTheSameIdReadsNothing()
    {
        var state = Start("seen_far: wren 2");
        var wren = state.Find("wren")!;

        Assert.Equal(2, state.Map.SeenFar!.For(wren));
        Assert.Equal(0, state.Map.SeenFar.For(wren with { Side = Side.Enemy }));
    }

    [Fact]
    public void ThreatFromNamesWhatAStopWouldWakeWithTheSighting()
    {
        var start = Start("seen_far: wren 2");
        var state = start.WithUnit(start.Find("wren")! with { At = new Coord(1, 3) }).WithUnit(start.Find("hale")! with { At = new Coord(1, 2) });

        Assert.Contains(Queries.StopWakes(state, Starter, state.Find("wren")!, new Coord(3, 3))!, w => w.Group == "watch");
        Assert.Empty(Queries.StopWakes(state, Starter, state.Find("hale")!, new Coord(3, 3))!);
    }

    [Fact]
    public void TheBoardPrintsTheSightingWhileTheUnitIsOnItAndASleeperIsLeft()
    {
        var state = Start("seen_far: wren 2");
        var line = $"seen far: {state.Find("wren")!.Unit.Name} wakes a sleeper within 6 (seen for miles)";

        Assert.Contains(line, MapRenderer.Render(state, Starter));
        Assert.DoesNotContain("seen far", MapRenderer.Render(Start(""), Starter));
        Assert.DoesNotContain("seen far", MapRenderer.Render(state.WithoutUnit("wren"), Starter));
    }

    [Fact]
    public void TheHeaderRoundTrips()
    {
        var map = MapFixture.Parse(Field("seen_far: wren 3"));

        Assert.Equal(new SeenFar("wren", 3), map.SeenFar);
        Assert.Contains("seen_far: wren 3\n", MapFormat.Write(map, Starter));
        Assert.Equal(map, MapFixture.Parse(MapFormat.Write(map, Starter)));
    }

    [Theory]
    [InlineData("seen_far: wren", "needs a unit and the tiles it adds")]
    [InlineData("seen_far: wren two", "needs a unit and the tiles it adds")]
    [InlineData("seen_far: nobody 2", "'nobody' is not a unit in the content")]
    [InlineData("seen_far: wren 0", "from 1 to 4")]
    [InlineData("seen_far: wren 5", "from 1 to 4")]
    public void ABadHeaderIsRefusedNamingTheField(string header, string message)
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Field(header)));

        Assert.Contains("seen_far", error.Message);
        Assert.Contains(message, error.Message);
    }
}
