using Ironwake.Core.Tests.Battle;

namespace Ironwake.Core.Tests.Maps;

/// <summary>
/// Issue 260: while any Guard group is asleep, both board views print one line stating the
/// wake rule of DESIGN.md section 8, built from the content's radii and never a literal.
/// </summary>
public class WakeLegendTests
{
    private const string Line = "asleep: wakes if a unit ends within 4 tiles of a member, a combat happens within 6, or a member dies";

    private static BattleState Start() => BattleFixture.Start(map: MapFixture.OldMillRoad);

    [Fact]
    public void TheWakeLegendStatesTheRuleFromTheContentRadii()
    {
        Assert.Equal(Line, MapRenderer.WakeLegend(MapFixture.Content));
    }

    [Fact]
    public void TheWakeLegendFollowsWakeRadius()
    {
        var content = MapFixture.Content with { WakeRadius = 2 };

        Assert.Equal(
            "asleep: wakes if a unit ends within 2 tiles of a member, a combat happens within 4, or a member dies",
            MapRenderer.WakeLegend(content));
    }

    [Fact]
    public void TheBattleViewPrintsTheWakeLegendWhileAGroupSleeps()
    {
        var lines = MapRenderer.Render(Start(), MapFixture.Content).Split('\n');

        Assert.Single(lines, l => l == Line);
        var legend = Array.IndexOf(lines, Line);
        var lastAsleepRow = Array.FindLastIndex(lines, l => l.EndsWith(", asleep", StringComparison.Ordinal));
        Assert.InRange(lastAsleepRow, 0, legend - 1);
    }

    [Fact]
    public void TheBattleViewDropsTheWakeLegendOnceNoGroupSleeps()
    {
        var view = MapRenderer.Render(Start().Wake("mill"), MapFixture.Content);

        Assert.DoesNotContain("asleep", view);
    }

    [Fact]
    public void TheBattleViewPrintsTheWakeLegendFromItsOwnContent()
    {
        var content = MapFixture.Content with { WakeRadius = 3 };

        var view = MapRenderer.Render(Start(), content);

        Assert.Contains("within 3 tiles of a member, a combat happens within 5,", view);
        Assert.DoesNotContain(Line, view);
    }

    [Fact]
    public void TheMapViewPrintsTheWakeLegendWhenAPlacementIsAGuard()
    {
        var view = MapRenderer.Render(MapFixture.Parse(MapFixture.OldMillRoad), MapFixture.Content);

        Assert.Contains(Line + "\n", view);
    }

    [Fact]
    public void TheMapViewHasNoWakeLegendWithoutAGuard()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad.Replace("behavior:guard", "behavior:hold"));

        Assert.DoesNotContain("asleep:", MapRenderer.Render(map, MapFixture.Content));
    }
}
