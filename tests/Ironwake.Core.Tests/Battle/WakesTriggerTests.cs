using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The <c>wakes</c> trigger (issue 1365): an event fires in the same resolution that wakes its
/// group, after the group's <see cref="GroupWoke"/>, once a battle; a group that never wakes never
/// fires it; a late one waits for the next player phase's start; a group with no guard is refused on
/// load. Saltmarsh Ford's pair rides it on two samples, the shipped map untouched (DECISIONS/0339).
/// </summary>
public class WakesTriggerTests
{
    /// <summary>
    /// A 12x4 field. Hale at 0,1, five tiles from the fort's guard at 6,1 (asleep at the wake
    /// radius of 4), eleven or more from the deep guard at 11,3. The fort's wake calls a brigand
    /// to 0,3 behind Hale; the deep group's would call one to 11,0.
    /// </summary>
    private const string Field = """
        name: Field
        size: 12x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ............
        ............
        ............
        ............

        units:
        P captain 0,1
        E soldier 6,1 group:fort behavior:guard
        E soldier 11,3 group:deep behavior:guard

        events:
        rear wakes fort spawn brigand 0,3 group:rear behavior:aggressive
        far wakes deep spawn brigand 11,0 group:far behavior:aggressive

        """;

    private static BattleState Start() => BattleFixture.Start(map: Field);

    [Fact]
    public void TheWakesTriggerParsesAndWritesBackCanonically()
    {
        var map = MapFixture.Parse(Field);

        Assert.Equal(new WakesTrigger("fort"), map.Events[0].Trigger);
        Assert.Equal(Field.Replace("\r\n", "\n"), MapFormat.Write(map, BattleFixture.Starter));
    }

    [Fact]
    public void AWakesEventFiresInTheResolutionThatWakesItsGroup()
    {
        var result = Start().Try(new Move("hale", new Coord(2, 1)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var tail = result.Events.SkipWhile(e => e is not GroupWoke).ToArray();
        Assert.Equal(new GroupWoke("fort", WakeCause.Proximity), tail[0]);
        Assert.Equal(new MapEventFired("rear", false), tail[1]);
        Assert.Equal(new Coord(0, 3), Assert.IsType<UnitSpawned>(tail[2]).At);
        Assert.True(result.Next.HasFired("rear"));
        Assert.False(result.Next.HasFired("far"));
        Assert.Single(result.Next.Units, u => u.Group == "rear");
    }

    [Fact]
    public void AWakesEventDoesNotFireWhileItsGroupSleeps()
    {
        var state = Start().Do(new Move("hale", new Coord(1, 1)));
        Assert.False(state.IsAwake("fort"));

        for (var i = 0; i < 4; i++)
        {
            state = state.Do(new EndPhase());
        }

        Assert.False(state.HasFired("rear"));
        Assert.False(state.HasFired("far"));
        Assert.DoesNotContain(state.Units, u => u.Group is "rear" or "far");
    }

    [Fact]
    public void AWakesEventFiresOnceABattle()
    {
        var state = Start().Do(new Move("hale", new Coord(2, 1)));

        for (var i = 0; i < 4; i++)
        {
            var result = state.Try(new EndPhase());
            Assert.DoesNotContain(result.Events, e => e is MapEventFired { Name: "rear" });
            state = result.Next;
        }

        Assert.Single(state.Units, u => u.Group == "rear" && u.Side == Side.Enemy);
    }

    [Theory]
    [InlineData("a wakes", "wakes trigger needs a group's name")]
    [InlineData("a wakes 3,1 flag x", "wakes trigger needs a group's name")]
    [InlineData("a wakes ghosts flag x", "no E line places a guard in it")]
    [InlineData("a wakes rear flag x", "no E line places a guard in it")]
    public void AWakesTriggerOnAGroupThatCannotWakeIsRefused(string line, string fragment)
    {
        var text = Field.Replace("far wakes deep spawn brigand 11,0 group:far behavior:aggressive", "far wakes deep spawn brigand 11,0 group:far behavior:aggressive\n" + line);
        var e = Assert.Throws<MapException>(() => MapFixture.Parse(text, "bad.map"));

        Assert.Contains(fragment, e.Problem);
        Assert.Contains("line 21", e.Message);
    }

    [Fact]
    public void AWakesEventIsDescribedByItsGroup()
    {
        var map = MapFixture.Parse(Field);

        Assert.StartsWith("when fort wakes", PlaySession.DescribeEvent(map.Events[0], BattleFixture.Starter));
    }

    [Fact]
    public void ALateWakesEventFiresAtTheNextPlayerPhaseStart()
    {
        var late = Field.Replace("rear wakes fort spawn", "rear wakes fort late spawn");
        Assert.Equal(new WakesTrigger("fort", Late: true), MapFixture.Parse(late).Events[0].Trigger);
        Assert.Equal(late.Replace("\r\n", "\n"), MapFormat.Write(MapFixture.Parse(late), BattleFixture.Starter));

        var woke = BattleFixture.Start(map: late).Try(new Move("hale", new Coord(2, 1)));
        Assert.Contains(woke.Events, e => e is GroupWoke { Group: "fort" });
        Assert.False(woke.Next.HasFired("rear"));

        var enemyPhase = woke.Next.Try(new EndPhase());
        Assert.False(enemyPhase.Next.HasFired("rear"));

        var state = enemyPhase.Next;
        while (state.Phase == Side.Enemy)
        {
            var step = state.Try(new EndPhase());
            Assert.True(step.Accepted, step.Rejection?.Message);
            state = step.Next;
        }

        Assert.Equal((2, Side.Player), (state.Turn, state.Phase));
        Assert.True(state.HasFired("rear"));
        Assert.Contains(state.Units, u => u.Group == "rear" && u.At == new Coord(0, 3));
        Assert.StartsWith("at the start of your next phase after fort wakes", PlaySession.DescribeEvent(MapFixture.Parse(late).Events[0], BattleFixture.Starter));
    }

    [Theory]
    [InlineData("saltmarsh_ford_wakes.map", false)]
    [InlineData("saltmarsh_ford_wakes_late.map", true)]
    public void TheSaltmarshWakesSamplesAreTheShippedMapWithOnlyThePairsTriggerChanged(string file, bool late)
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var shipped = File.ReadAllLines(Path.Combine(root, "content", "maps", "saltmarsh_ford.map"));
        var sample = File.ReadAllLines(Path.Combine(root, "docs", "samples", file));

        Assert.Equal(shipped.Length, sample.Length);
        var changed = Enumerable.Range(0, shipped.Length).Where(i => shipped[i] != sample[i]).ToList();
        Assert.Equal(new[] { "ford", "ford_second" }, changed.Select(i => sample[i].Split(' ')[0]));
        var map = MapFixture.Parse(string.Join('\n', sample) + "\n", file);
        Assert.All(map.Events, e => Assert.Equal(new WakesTrigger("fort", late), e.Trigger));
    }
}
