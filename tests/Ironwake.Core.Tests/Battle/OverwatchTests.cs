using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Overwatch (DESIGN.md 13.17, experiment; issue 481's acceptance as rounds 112 to 115 amended
/// it): on an <c>overwatch: on</c> map a unit whose equipped weapon reaches range 2 may watch; the
/// first unit of the other side to end a move exactly two tiles from it is struck once before it
/// acts, no counter; a strike on the watcher ends the watch; the watch ends at its side's next phase.
/// </summary>
public class OverwatchTests
{
    private static readonly Unit Bow = Recruit("bo", "bowman", new Stats(20, 6, 0, 7, 7, 4, 4, 2, 3), "iron_bow");
    private static readonly Unit Bow2 = Recruit("bz", "bowman", new Stats(20, 6, 0, 7, 7, 4, 4, 2, 3), "iron_bow");

    private static string Field(bool overwatch, string units, bool brace = false) =>
        $"""
        name: Field
        size: 9x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(overwatch ? "overwatch: on" : "")}
        {(brace ? "brace: on" : "")}

        .........
        .........
        .........
        .........
        .........

        units:
        {units}
        """.Replace("\n\n\n", "\n\n").Replace("\n\n\n", "\n\n");

    private const string Line = """
        P captain 0,0
        P recruit:bo 2,1
        E brigand 6,1 group:field behavior:aggressive

        """;

    private static BattleState Start(bool overwatch = true, string units = Line, ulong seed = 7) =>
        BattleFixture.Start(seed, ValueList<Unit>.Of(Hale, Bow, Bow2), Field(overwatch, units));

    private static ApplyResult Step(BattleState state, Command command)
    {
        var result = state.Try(command);
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result;
    }

    /// <summary>Bo watches from 2,1 and the captain waits; the enemy phase begins.</summary>
    private static BattleState Watched(BattleState state) =>
        state.Do(new Watch("bo")).Do(new Wait("hale")).Do(new EndPhase());

    [Fact]
    public void AWatchFiresOnTheFirstEnemyToEndAMoveInItsRingBeforeItActs()
    {
        var watched = Watched(Start());
        Assert.True(watched.Find("bo")!.Watching);

        var moved = Step(watched, new Move("brigand-1", new Coord(4, 1)));

        var shot = Assert.Single(moved.Events.OfType<WatchFired>());
        Assert.Equal(("bo", "brigand-1"), (shot.UnitId, shot.TargetId));
        Assert.False(moved.Next.Find("bo")!.Watching);
        Assert.Equal(shot.Strike.TargetHpAfter, moved.Next.Find("brigand-1")!.Hp);
        Assert.False(moved.Next.Find("brigand-1")!.Acted);
    }

    [Fact]
    public void AWatchShotHasNoCounter()
    {
        var watched = Watched(Start());
        var hp = watched.Find("bo")!.Hp;

        var moved = Step(watched, new Move("brigand-1", new Coord(4, 1)));

        Assert.Empty(moved.Events.OfType<CombatFought>());
        Assert.Equal(hp, moved.Next.Find("bo")!.Hp);
    }

    [Fact]
    public void PassingThroughTheRingWithoutStoppingIsNotShot()
    {
        var watched = Watched(Start());

        var moved = Step(watched, new Move("brigand-1", new Coord(3, 1)));

        Assert.Empty(moved.Events.OfType<WatchFired>());
        Assert.True(moved.Next.Find("bo")!.Watching);
    }

    [Fact]
    public void TheTileBesideTheWatcherIsNotInItsRing()
    {
        var watcher = Watched(Start()).Find("bo")!;

        Assert.False(Overwatch.InRing(watcher, new Coord(3, 1)));
        Assert.True(Overwatch.InRing(watcher, new Coord(4, 1)));
        Assert.True(Overwatch.InRing(watcher, new Coord(3, 2)));
    }

    [Fact]
    public void AnEnemyThatStartsInTheRingAndStrikesWithoutMovingIsNotShotAndEndsTheWatch()
    {
        var units = """
            P captain 0,0
            P recruit:bo 2,1
            E archer 4,1 group:field behavior:hold

            """;
        var watched = Watched(Start(units: units));

        var struck = Step(watched, new Attack("archer-1", "bo"));

        Assert.Empty(struck.Events.OfType<WatchFired>());
        Assert.Single(struck.Events.OfType<WatchEnded>());
        Assert.False(struck.Next.Find("bo")?.Watching ?? false);
    }

    [Fact]
    public void AKillingShotCancelsTheArrivalsAction()
    {
        for (ulong seed = 1; seed < 64; seed++)
        {
            var watched = Watched(Start(seed: seed));
            watched = watched.WithUnit(watched.Find("brigand-1")! with { Hp = 1 });
            var moved = Step(watched, new Move("brigand-1", new Coord(4, 1)));
            if (!moved.Events.OfType<WatchFired>().Single().Strike.Hit)
            {
                continue;
            }

            Assert.Null(moved.Next.Find("brigand-1"));
            Assert.Contains(moved.Events, e => e is UnitDied { UnitId: "brigand-1" });
            Assert.False(moved.Next.Try(new Attack("brigand-1", "bo")).Accepted);
            return;
        }

        Assert.Fail("no seed in 1 to 63 landed the shot");
    }

    [Fact]
    public void AnArrivalInsideTwoRingsIsShotByEachInIdOrder()
    {
        var units = """
            P captain 0,0
            P recruit:bo 2,1
            P recruit:bz 6,1
            E brigand 4,4 group:field behavior:aggressive

            """;
        var watched = Start(units: units).Do(new Watch("bo")).Do(new Watch("bz")).Do(new Wait("hale")).Do(new EndPhase());

        var moved = Step(watched, new Move("brigand-1", new Coord(4, 1)));

        var shots = moved.Events.OfType<WatchFired>().Select(f => f.UnitId).ToList();
        Assert.Equal("bo", shots[0]);
        Assert.Equal(moved.Next.Find("brigand-1") is null ? 1 : 2, shots.Count);
    }

    [Fact]
    public void TheWatchEndsWhenItsSidesNextPhaseBegins()
    {
        var back = Watched(Start()).Do(new Wait("brigand-1")).Do(new EndPhase());

        Assert.False(back.Find("bo")!.Watching);
    }

    [Fact]
    public void RecallRestoresTheWatch()
    {
        var afterWatch = Start().Do(new Watch("bo"));
        var index = afterWatch.History.Count + 1;
        var later = afterWatch.Do(new Wait("hale")).Do(new EndPhase()).Do(new Move("brigand-1", new Coord(4, 1))).Do(new Wait("brigand-1")).Do(new EndPhase());
        Assert.False(later.Find("bo")!.Watching);

        var recalled = later.Do(new Recall(index));

        Assert.True(recalled.Find("bo")!.Watching);
    }

    [Fact]
    public void WatchIsRefusedWithoutTheHeaderOrAWeaponReachingTwo()
    {
        Assert.Equal(RejectionReason.CannotWatch, Start(overwatch: false).Refused(new Watch("bo")).Reason);
        Assert.Equal(RejectionReason.CannotWatch, Start().Refused(new Watch("hale")).Reason);
        Assert.DoesNotContain(Resolver.Legal(Start(overwatch: false), Starter), c => c is Watch);
        Assert.Contains(Resolver.Legal(Start(), Starter), c => c is Watch { UnitId: "bo" });
    }

    [Fact]
    public void AnUnarmedUnitCannotWatch()
    {
        var units = """
            P captain 0,0
            P recruit:pell 2,1
            E brigand 6,1 group:field behavior:aggressive

            """;
        var state = BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Unarmed), Field(true, units));

        var result = state.Try(new Watch("pell"));

        Assert.False(result.Accepted);
        Assert.Equal(RejectionReason.CannotWatch, result.Rejection!.Reason);
        Assert.Contains("pell has no weapon equipped", result.Rejection.Message);
        Assert.DoesNotContain(Resolver.Legal(state, Starter), c => c is Watch { UnitId: "pell" });
    }

    [Fact]
    public void WatchNamesTheBestStrikeItPassesUp()
    {
        var units = """
            P captain 0,0
            P recruit:bo 2,1
            E brigand 4,1 group:field behavior:aggressive

            """;
        var state = Start(units: units);
        var expected = Queries.Forecast(state, Starter, state.Find("bo")!, state.Find("brigand-1")!)!.Attacker.DisplayedHit;

        var taken = Assert.Single(Step(state, new Watch("bo")).Events.OfType<WatchTaken>());

        Assert.Equal(("brigand-1", expected), (taken.PassedUpTargetId, taken.PassedUpHit));
        Assert.Null(Assert.Single(Step(Start(), new Watch("bo")).Events.OfType<WatchTaken>()).PassedUpTargetId);
    }

    [Fact]
    public void AnEnemyWithNoStrikeAndARangeTwoWeaponWatchesInsteadOfWaiting()
    {
        var units = """
            P captain 0,0
            P recruit:bo 0,4
            E archer 8,2 group:field behavior:hold

            """;
        var state = Start(units: units).Do(new Wait("hale")).Do(new Wait("bo")).Do(new EndPhase());

        Assert.Contains(EnemyAi.Plan(state, Starter), c => c is Watch { UnitId: "archer-1" });
        var plain = Start(overwatch: false, units: units).Do(new Wait("hale")).Do(new Wait("bo")).Do(new EndPhase());
        Assert.Contains(EnemyAi.Plan(plain, Starter), c => c is Wait { UnitId: "archer-1" });
    }

    [Fact]
    public void ASleepingGuardNeverWatches()
    {
        var units = """
            P captain 0,0
            P recruit:bo 0,4
            E archer 8,2 group:field behavior:guard

            """;
        var state = Start(units: units).Do(new Wait("hale")).Do(new Wait("bo")).Do(new EndPhase());

        Assert.DoesNotContain(EnemyAi.Plan(state, Starter), c => c is Watch);
    }

    [Fact]
    public void TheHeaderRoundTripsAndIsExclusiveWithBrace()
    {
        var map = MapFixture.Parse(Field(true, Line), "field.map");
        Assert.True(map.OverwatchEnabled);
        Assert.True(MapFixture.Parse(MapFormat.Write(map, Starter), "again.map").OverwatchEnabled);

        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Field(true, Line, brace: true), "both.map"));
        Assert.Contains("exclusive", error.Message);
    }
    /// <summary>
    /// Issue 501: 13.17's keep round sample is the shipped Sallow Grange with only
    /// <c>overwatch: on</c> added, so a play of it reads against the plain map's entries.
    /// </summary>
    [Fact]
    public void TheSallowOverwatchSampleIsTheShippedMapWithOnlyTheOverwatchHeaderAdded()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var shippedPath = Path.Combine(repo, "content", "maps", "sallow_grange.map");
        var samplePath = Path.Combine(repo, "docs", "samples", "sallow_grange_overwatch.map");
        // The shipped map's campaign-only keziah_warning (issue 871) is not the sample's to carry.
        var shipped = File.ReadAllText(shippedPath).Replace("\r\n", "\n").Split('\n').Where(l => l != "keziah_warning: on").ToList();
        var sampleText = File.ReadAllText(samplePath).Replace("\r\n", "\n");
        var sampleLines = sampleText.Split('\n').ToList();

        var at = sampleLines.IndexOf("overwatch: on");
        Assert.True(at >= 0);
        sampleLines.RemoveAt(at);
        Assert.Equal(shipped, sampleLines);

        var sample = MapFiles.Load(samplePath, MapFixture.Content);
        var original = MapFiles.Load(shippedPath, MapFixture.Content);
        Assert.True(sample.OverwatchEnabled);
        Assert.False(original.OverwatchEnabled);
        Assert.Equal(original with { OverwatchEnabled = true, KeziahWarning = false }, sample);
        Assert.Equal(sampleText, MapFormat.Write(sample, Starter));
    }
}
