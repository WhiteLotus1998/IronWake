using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Battle;

using static BattleFixture;

/// <summary>
/// Issue 47's acceptance: the free prefix reads at least 2 on a map whose deployment is out
/// of every enemy's reach for the opening turns and 0 on a map in contact on turn 1; the
/// refunded re-run fires only at a boundary; a sleeping Guard group makes no turn live, a
/// turn with reach into its radius is quiet and not dead, and the same turn is live once
/// the group wakes; the wake tax is zero without overlap and the overlap with it.
/// </summary>
public class FreePrefixTests
{
    /// <summary>One brigand nineteen tiles from the party with twenty turns to rout it: two random turns cost nothing.</summary>
    private const string Far = """
        name: Far
        size: 20x3
        win: rout
        turn_limit: 20
        recall: 3
        enemy_level: 1

        ....................
        ....................
        ....................

        units:
        P captain 0,1
        P recruit 0,2
        E brigand 19,1 group:road behavior:aggressive

        """;

    /// <summary>
    /// The yard with one brigand holding two tiles from the captain and two turns to rout
    /// it: in contact on turn 1, since either cadet can end beside it, and a random opening
    /// spends the whole clock. It holds so that no counter on its own phase kills it for the
    /// random arm.
    /// </summary>
    private const string Contact = """
        name: Contact
        size: 6x4
        win: rout
        turn_limit: 2
        recall: 3
        enemy_level: 1

        ......
        ......
        ......
        ......

        units:
        P captain 0,1
        P recruit 0,2
        E brigand 2,1 group:yard behavior:hold

        """;

    private const int Seeds = 20;

    private static (IReadOnlyList<GameResult> Baseline, List<FreePrefix.Arm> Arms, MapDefinition Map) Arms(string text)
    {
        var map = MapFixture.Parse(text);
        var (_, baseline) = Gates.Gate1(Starter, map, "test", Seeds);
        return (baseline, FreePrefix.Lengths.Select(n => FreePrefix.Play(Starter, map, n, baseline, RollScheme.TwoRollAverage)).ToList(), map);
    }

    [Fact]
    public void AMapOutOfReachForTheOpeningTurnsHasAFreePrefixOfAtLeastTwo()
    {
        var (baseline, arms, _) = Arms(Far);
        Assert.True(baseline.Count(g => g.Won) >= Seeds - 1, string.Join(", ", baseline.Select(g => g.Result)));
        Assert.True(FreePrefix.Length(arms) >= 2, string.Join("; ", arms));
    }

    [Fact]
    public void AMapInContactOnTurnOneHasAFreePrefixOfZero()
    {
        var (baseline, arms, _) = Arms(Contact);
        Assert.True(baseline.Count(g => g.Won) >= Seeds * 3 / 4, string.Join(", ", baseline.Select(g => g.Result)));
        Assert.Equal(0, FreePrefix.Length(arms));
        Assert.Equal(2, FreePrefix.Boundary(arms));
    }

    [Fact]
    public void TheRefundedReRunFiresAtTheBoundaryOfAPrefixThatIsNotFree()
    {
        var map = MapFixture.Parse(Contact);
        var (_, baseline) = Gates.Gate1(Starter, map, "contact", Seeds);
        var lines = FreePrefix.Report(Starter, map, "contact", baseline, Array.Empty<IReadOnlyList<TurnReading>>(), RollScheme.TwoRollAverage);
        var refunded = Assert.Single(lines, l => l.Contains("refunded", StringComparison.Ordinal));
        Assert.StartsWith("  prefix 2 refunded:", refunded);
        Assert.Contains("limit raised by 2 against the unraised baseline", refunded);
    }

    [Fact]
    public void TheRefundedReRunDoesNotFireOnAMapFreeAtTheLargestLength()
    {
        var map = MapFixture.Parse(Far);
        var (_, baseline) = Gates.Gate1(Starter, map, "far", Seeds);
        var lines = FreePrefix.Report(Starter, map, "far", baseline, Array.Empty<IReadOnlyList<TurnReading>>(), RollScheme.TwoRollAverage);
        Assert.StartsWith("free prefix: far, 6 of 2, 4, 6 tried", lines[0]);
        Assert.DoesNotContain(lines, l => l.Contains("refunded", StringComparison.Ordinal));
    }

    [Fact]
    public void APrefixIsFreeWhenItsDropIsWithinTwiceItsStandardError()
    {
        Assert.True(new FreePrefix.Arm(2, 0, 0.08, 0.04).Free);
        Assert.False(new FreePrefix.Arm(2, 0, 0.081, 0.04).Free);
        Assert.True(new FreePrefix.Arm(2, 0, -0.3, 0.04).Free);
    }

    [Fact]
    public void TheFreePrefixStopsAtTheFirstLengthThatIsNotFree()
    {
        var arms = new[] { new FreePrefix.Arm(2, 0, 0, 0.01), new FreePrefix.Arm(4, 0, 0.5, 0.01), new FreePrefix.Arm(6, 0, 0, 0.01) };
        Assert.Equal(2, FreePrefix.Length(arms));
        Assert.Equal(4, FreePrefix.Boundary(arms));
    }

    /// <summary>
    /// Hale at 0,1 and Wren at 0,2 on open ground, cadets of Mov 4, and one Guard soldier
    /// at <c>{x},1</c>: at 5 Hale can end beside it on twelve tiles, eight of them within the
    /// wake radius of 4, the largest fraction of the two (Wren's is 7 of 11).
    /// </summary>
    private static string Guarded(int x) => $$"""
        name: Guarded
        size: 20x3
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ....................
        ....................
        ....................

        units:
        P captain 0,1
        P recruit:wren 0,2
        E soldier {{x}},1 group:watch behavior:guard

        """;

    [Fact]
    public void ASleepingGuardGroupMakesNoTurnLive()
    {
        var reading = TurnState.Read(Start(map: Guarded(5)), Starter);
        Assert.NotEqual(TurnKind.Live, reading.Kind);
    }

    [Fact]
    public void ATurnWithReachIntoASleepingRadiusIsQuietNotDead()
    {
        var reading = TurnState.Read(Start(map: Guarded(5)), Starter);
        Assert.Equal(TurnKind.Quiet, reading.Kind);
    }

    [Fact]
    public void TheSameTurnIsLiveOnceTheGroupWakes()
    {
        var reading = TurnState.Read(Start(map: Guarded(5)).Wake("watch"), Starter);
        Assert.Equal(TurnKind.Live, reading.Kind);
    }

    [Fact]
    public void TheWakeTaxIsZeroWhenNoReachMeetsASleepingRadius()
    {
        var reading = TurnState.Read(Start(map: Guarded(15)), Starter);
        Assert.Equal(TurnKind.Dead, reading.Kind);
        Assert.Equal(0, reading.TaxCount);
        Assert.Equal(0.0, reading.TaxFraction);
    }

    [Fact]
    public void TheWakeTaxIsTheOverlapWhereReachMeetsASleepingRadius()
    {
        var reading = TurnState.Read(Start(map: Guarded(5)), Starter);
        Assert.Equal(8, reading.TaxCount);
        Assert.Equal(12, reading.Destinations);
    }

    [Fact]
    public void AnAwakeEnemyThatCanReachThePartyMakesTheTurnLive()
    {
        var reading = TurnState.Read(Start(), Starter);
        Assert.Equal(TurnKind.Live, reading.Kind);
    }

    /// <summary>An archer six tiles out: moving, its bow reaches the captain from 2,1; holding, it reaches nobody, and no cadet reaches it.</summary>
    private static string Archer(string behavior) => $$"""
        name: Archer
        size: 20x3
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ....................
        ....................
        ....................

        units:
        P captain 0,1
        P recruit:wren 0,2
        E archer 6,1 group:line behavior:{{behavior}}

        """;

    [Fact]
    public void AnEnemyThatHoldsThreatensOnlyFromItsOwnTile()
    {
        Assert.Equal(TurnKind.Live, TurnState.Read(Start(map: Archer("aggressive")), Starter).Kind);
        Assert.Equal(TurnKind.Dead, TurnState.Read(Start(map: Archer("hold")), Starter).Kind);
    }
}
