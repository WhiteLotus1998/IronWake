using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The Guard wake rule of DESIGN.md section 8: a sleeping group holds until proximity,
/// noise, or a death wakes it; the radius is content, Manhattan, walls not considered;
/// the check runs on where units stand after each command; the event names the cause.
/// </summary>
public class WakeTests
{
    /// <summary>A 12x4 field: Hale at 0,1 and Wren at 0,2, a brigand at 3,1, and a Guard soldier at 7,0 with a second member at 11,3.</summary>
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
        P recruit:wren 0,2
        E brigand 3,1 group:road behavior:aggressive
        E soldier 7,0 group:watch behavior:guard
        E archer 11,3 group:watch behavior:guard

        """;

    private static BattleState Start() => BattleFixture.Start(map: Field);

    private static IEnumerable<GroupWoke> Woke(ApplyResult result) => result.Events.OfType<GroupWoke>();

    [Fact]
    public void TheWakeRadiusIsContentAndIsFour()
    {
        Assert.Equal(4, Starter.WakeRadius);
        Assert.Equal(6, Starter.NoiseRadius);
    }

    [Fact]
    public void AGuardGroupDoesNotMoveUntilWoken()
    {
        var state = Start().Do(new EndPhase());
        var soldier = state.Find("soldier-1")!;

        Assert.Equal(Behavior.Hold, state.EffectiveBehavior(soldier));
        Assert.Equal(new Command[] { new Wait("soldier-1") }, EnemyAi.PlanUnit(state, Starter, soldier));

        var woken = state.Wake("watch");
        Assert.Equal(Behavior.Aggressive, woken.EffectiveBehavior(woken.Find("soldier-1")!));
        Assert.IsType<Move>(EnemyAi.PlanUnit(woken, Starter, woken.Find("soldier-1")!)[0]);
    }

    [Fact]
    public void AGuardDoesAttackWhatStandsInItsRangeWhileAsleep()
    {
        var state = Start().WithUnit(Start().Find("hale")! with { At = new Coord(6, 0) }).Do(new EndPhase());

        Assert.Equal(new Command[] { new Attack("soldier-1", "hale") }, EnemyAi.PlanUnit(state, Starter, state.Find("soldier-1")!));
    }

    [Fact]
    public void ProximityWakesTheGroupWhenAPlayerUnitStandsWithinTheRadius()
    {
        var state = Start();
        var result = state.Try(new Move("hale", new Coord(3, 0)));

        Assert.Equal(4, result.Next.Find("hale")!.At.DistanceTo(result.Next.Find("soldier-1")!.At));
        var woke = Assert.Single(Woke(result));
        Assert.Equal(new GroupWoke("watch", WakeCause.Proximity), woke);
        Assert.True(result.Next.IsAwake("watch"));
        Assert.Contains("awake watch\n", result.Next.Canonical());
    }

    [Fact]
    public void APlayerUnitAtRadiusPlusOneWithNoCombatDoesNotWakeIt()
    {
        var state = Start();
        var result = state.Try(new Move("hale", new Coord(2, 0)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(5, result.Next.Find("hale")!.At.DistanceTo(result.Next.Find("soldier-1")!.At));
        Assert.Empty(Woke(result));
        Assert.False(result.Next.IsAwake("watch"));
        Assert.Contains("awake\n", result.Next.Canonical());
    }

    [Fact]
    public void NoiseWakesTheGroupFromRadiusPlusTwo()
    {
        var state = Start().Do(new Move("hale", new Coord(2, 1)));
        Assert.Equal(6, state.Find("hale")!.At.DistanceTo(state.Find("soldier-1")!.At));
        Assert.Equal(5, state.Find("brigand-1")!.At.DistanceTo(state.Find("soldier-1")!.At));
        var result = state.Try(new Attack("hale", "brigand-1"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(new GroupWoke("watch", WakeCause.Noise), Assert.Single(Woke(result)));
    }

    [Fact]
    public void ACombatOutsideTheNoiseRadiusIsSilent()
    {
        var quiet = Start();
        quiet = quiet.WithUnit(quiet.Find("brigand-1")! with { At = new Coord(0, 0) });
        Assert.Equal(7, quiet.Find("brigand-1")!.At.DistanceTo(quiet.Find("soldier-1")!.At));
        Assert.Equal(8, quiet.Find("hale")!.At.DistanceTo(quiet.Find("soldier-1")!.At));
        var result = quiet.Try(new Attack("hale", "brigand-1"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Empty(Woke(result));
    }

    [Fact]
    public void ADeathWakesTheGroupAtAnyDistance()
    {
        var state = Start();
        state = state.WithUnit(state.Find("archer-1")! with { At = new Coord(0, 3), Hp = 1 });
        var result = state.Try(new Attack("wren", "archer-1"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(result.Events, e => e is UnitDied { UnitId: "archer-1" });
        Assert.Equal(new GroupWoke("watch", WakeCause.Death), Assert.Single(Woke(result)));
        Assert.Equal(Behavior.Aggressive, result.Next.EffectiveBehavior(result.Next.Find("soldier-1")!));
    }

    [Fact]
    public void AWokenGroupStaysAwakeAndIsNotAnnouncedTwice()
    {
        var state = Start().Do(new Move("hale", new Coord(3, 0)));
        var result = state.Try(new Move("wren", new Coord(4, 2)));

        Assert.Empty(Woke(result));
        Assert.True(result.Next.IsAwake("watch"));
    }

    [Fact]
    public void RecallRestoresASleepingGroup()
    {
        var woken = Start().Do(new Move("hale", new Coord(3, 0)));
        Assert.True(woken.IsAwake("watch"));
        var restored = woken.Do(new Recall(0));

        Assert.False(restored.IsAwake("watch"));
    }
}
