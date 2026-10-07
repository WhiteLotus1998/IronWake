using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The <c>wake_on_death:</c> header (issue 1264, DESIGN.md sections 8 and 10): a named Guard group
/// sleeps through proximity and noise and wakes only on a death in the group it names or its own,
/// with the cause <c>death</c> and the named group as its caller. The loader refuses an entry it
/// cannot honor, and the board prints the rule in place of the wake legend.
/// </summary>
public class WakeOnDeathTests
{
    /// <summary>A 12x4 field: Hale at 0,1 and Wren at 0,2, a Hold soldier in group door at 7,0, a Guard archer in group loft at 6,1.</summary>
    private const string Field = """
        name: Field
        size: 12x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        wake_on_death: loft by door

        ............
        ............
        ............
        ............

        units:
        P captain 0,1
        P recruit:wren 0,2
        E soldier 7,0 group:door behavior:hold
        E archer 6,1 group:loft behavior:guard

        """;

    private static IEnumerable<GroupWoke> Woke(ApplyResult result) => result.Events.OfType<GroupWoke>();

    [Fact]
    public void ADeafGroupSleepsThroughAUnitBesideIt()
    {
        var result = Start(map: Field).Try(new Move("hale", new Coord(3, 0)));

        Assert.Empty(Woke(result));
        Assert.False(result.Next.IsAwake("loft"));
    }

    [Fact]
    public void WithoutTheHeaderTheSameStopWakesIt()
    {
        var result = Start(map: Field.Replace("wake_on_death: loft by door\n", "")).Try(new Move("hale", new Coord(3, 0)));

        Assert.Equal(new[] { new GroupWoke("loft", WakeCause.Proximity) }, Woke(result));
    }

    [Fact]
    public void ADeafGroupSleepsThroughAFightBesideIt()
    {
        var state = Start(map: Field);

        Assert.Empty(WakeCheck.Run(state, state, Starter, WakeCheck.At(Starter, new Coord(8, 0), new Coord(8, 1)), Array.Empty<string>()));
    }

    [Fact]
    public void ADeathInTheNamedGroupWakesIt()
    {
        var state = Start(map: Field);

        Assert.Equal(new[] { new GroupWoke("loft", WakeCause.Death, CalledBy: "door") }, WakeCheck.Run(state, state, Starter, Array.Empty<Noise>(), new[] { "door" }));
    }

    [Fact]
    public void ADeathInItsOwnGroupWakesItWithNoCaller()
    {
        var state = Start(map: Field);

        Assert.Equal(new[] { new GroupWoke("loft", WakeCause.Death) }, WakeCheck.Run(state, state, Starter, Array.Empty<Noise>(), new[] { "loft" }));
    }

    [Fact]
    public void ADeafGroupHasNoWakers()
    {
        var state = Start(map: Field);
        state = state.WithUnit(state.Find("hale")! with { At = new Coord(8, 0) });

        Assert.Empty(WakeCheck.Wakers(state, Starter, "loft"));
        Assert.NotEmpty(WakeCheck.Wakers(Start(map: Field.Replace("wake_on_death: loft by door\n", "")).WithUnit(state.Find("hale")!), Starter, "loft"));
    }

    [Fact]
    public void TheWakeOnDeathHeaderParsesAndWritesBack()
    {
        var map = MapFixture.Parse(Field);

        Assert.Equal(new[] { new DeathWake("loft", "door") }, map.DeathWakes);
        Assert.Equal(Field.Replace("\r\n", "\n"), MapFormat.Write(map, MapFixture.Content));
        Assert.Empty(MapFixture.Parse(Field.Replace("wake_on_death: loft by door\n", "")).DeathWakes);
    }

    [Theory]
    [InlineData("loft", "wake_on_death: 'loft' is not 'group by other'")]
    [InlineData("loft by moat", "wake_on_death: no enemy is in group 'moat'")]
    [InlineData("loft by loft", "wake_on_death: group 'loft' names itself")]
    [InlineData("door by loft", "wake_on_death: group 'door' has no guard member")]
    [InlineData("loft by door, loft by door", "wake_on_death: group 'loft' is named twice")]
    public void TheWakeOnDeathHeaderRefusesAnEntryItCannotHonor(string value, string message)
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Field.Replace("wake_on_death: loft by door", "wake_on_death: " + value)));

        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void TheBoardPrintsTheDeafRuleInPlaceOfTheWakeLegend()
    {
        var state = Start(map: Field);
        const string deaf = "deaf: group loft hears and sees nothing; it wakes only when a unit of group door or its own dies\n";

        Assert.Contains(deaf, MapRenderer.Render(state, Starter));
        Assert.Contains(deaf, MapRenderer.Render(state.Map, Starter));
        Assert.DoesNotContain("asleep: ", MapRenderer.Render(state, Starter));
        Assert.DoesNotContain("asleep: ", MapRenderer.Render(state.Map, Starter));
        Assert.DoesNotContain("deaf:", MapRenderer.Render(state.Wake("loft"), Starter));
        Assert.Contains("asleep: ", MapRenderer.Render(Start(map: Field.Replace("wake_on_death: loft by door\n", "")), Starter));
    }
}
