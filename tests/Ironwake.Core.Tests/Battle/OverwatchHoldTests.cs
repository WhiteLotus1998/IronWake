using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Overwatch redrafted (DESIGN.md 13.17b, issue 556): on an <c>overwatch: hold</c> map only a
/// player unit that has not moved this turn may watch; the watch is its action and ends its turn
/// with no Canto; the ring is its equipped weapon's whole reach on tiles a unit can stand on; the
/// enemy never watches; the event names the move the watch gives up.
/// </summary>
public class OverwatchHoldTests
{
    private static readonly Unit Pike = Recruit("pk", "pikeman", new Stats(22, 7, 0, 6, 6, 4, 6, 1, 3), "toll_spear", "field_dressing");
    private static readonly Unit Bow = Recruit("bo", "bowman", new Stats(20, 6, 0, 7, 7, 4, 4, 2, 3), "iron_bow");
    private static readonly Unit Rider = Recruit("ou", "outrider", new Stats(22, 7, 0, 6, 7, 4, 6, 1, 3), "iron_lance");

    private static string Field(string header, string units) =>
        $"""
        name: Field
        size: 9x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {header}

        .........
        ....#....
        .........
        .........
        .........

        units:
        {units}
        """.Replace("\n\n\n", "\n\n");

    private const string Line = """
        P captain 0,0
        P recruit:pk 3,2
        P recruit:bo 2,4
        P recruit:ou 0,4
        E brigand 7,2 group:field behavior:aggressive

        """;

    private static BattleState Start(string header = "overwatch: hold", string units = Line) =>
        BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Pike, Bow, Rider), Field(header, units));

    private static ApplyResult Step(BattleState state, Command command)
    {
        var result = state.Try(command);
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result;
    }

    [Fact]
    public void AUnitThatMovedCannotWatch()
    {
        var moved = Start().Do(new Move("pk", new Coord(3, 3)));

        var result = moved.Try(new Watch("pk"));

        Assert.False(result.Accepted);
        Assert.Equal(RejectionReason.CannotWatch, result.Rejection!.Reason);
        Assert.Contains("pk has moved this turn; a watch holds the tile pk began on", result.Rejection.Message);
        Assert.DoesNotContain(Resolver.Legal(moved, Starter), c => c is Watch { UnitId: "pk" });
        Assert.Contains(Resolver.Legal(Start(), Starter), c => c is Watch { UnitId: "pk" });
    }

    [Fact]
    public void OnTheSpikesHeaderAMovedUnitStillWatches()
    {
        var units = Line.Replace("recruit:pk 3,2", "recruit:pk 3,0");
        var moved = Start("overwatch: on", units).Do(new Move("bo", new Coord(2, 3)));

        Assert.True(moved.Try(new Watch("bo")).Accepted);
    }

    [Fact]
    public void ARecallPastTheMoveRestoresTheWatch()
    {
        var start = Start();
        var moved = start.Do(new Move("pk", new Coord(3, 3)));
        Assert.False(moved.Try(new Watch("pk")).Accepted);

        var recalled = moved.Do(new Recall(start.History.Count));

        Assert.Equal(new Coord(3, 2), recalled.Find("pk")!.At);
        Assert.True(recalled.Try(new Watch("pk")).Accepted);
    }

    [Fact]
    public void NoCantoFollowsAWatch()
    {
        var waited = Step(Start(), new Wait("ou")).Next;
        Assert.True(waited.Try(new Canto("ou", new Coord(0, 3))).Accepted);

        var watched = Step(Start(), new Watch("ou")).Next;

        var canto = watched.Try(new Canto("ou", new Coord(0, 3)));
        Assert.False(canto.Accepted);
        Assert.Equal(RejectionReason.NoCanto, canto.Rejection!.Reason);
    }

    [Fact]
    public void AWatcherCannotUseAnItemInTheSameTurn()
    {
        var watched = Step(Start(), new Watch("pk")).Next;

        Assert.False(watched.Try(new UseItem("pk", 1)).Accepted);
    }

    [Fact]
    public void TheRingIsTheWeaponsWholeReachAndLeavesOutWalls()
    {
        var state = Start();
        var pike = state.Find("pk")!;
        var bow = state.Find("bo")!;

        Assert.True(Overwatch.InRing(state, Starter, pike, new Coord(3, 1)));
        Assert.True(Overwatch.InRing(state, Starter, pike, new Coord(5, 2)));
        Assert.False(Overwatch.InRing(state, Starter, pike, new Coord(4, 1)));
        Assert.False(Overwatch.InRing(state, Starter, pike, new Coord(6, 2)));
        Assert.False(Overwatch.InRing(state, Starter, pike, pike.At));
        Assert.False(Overwatch.InRing(state, Starter, bow, new Coord(2, 3)));
        Assert.True(Overwatch.InRing(state, Starter, bow, new Coord(2, 2)));
        Assert.Equal(11, Overwatch.RingOf(state, Starter, pike).Count);
    }

    [Fact]
    public void AWatchWithARangeOneTwoWeaponFiresOnAnArrivalBesideIt()
    {
        var watched = Start().Do(new Watch("pk")).Do(new Wait("hale")).Do(new Wait("bo")).Do(new Wait("ou")).Do(new EndPhase());

        var moved = Step(watched, new Move("brigand-1", new Coord(4, 2)));

        var shot = Assert.Single(moved.Events.OfType<WatchFired>());
        Assert.Equal(("pk", "brigand-1"), (shot.UnitId, shot.TargetId));
    }

    [Fact]
    public void WatchNamesTheMoveItGivesUp()
    {
        var state = Start();

        var taken = Assert.Single(Step(state, new Watch("pk")).Events.OfType<WatchTaken>());

        Assert.True(taken.Holds);
        var instead = Assert.NotNull(taken.HoldsInsteadOf);
        Assert.Equal(instead, Overwatch.GivesUp(state, Starter, state.Find("pk")!));
        Assert.True(instead.DistanceTo(new Coord(7, 2)) < new Coord(3, 2).DistanceTo(new Coord(7, 2)));
    }

    [Fact]
    public void WatchSaysNoMoveCloserWhenThePlannerStays()
    {
        var units = Line.Replace("brigand 7,2", "brigand 4,2");
        var state = Start(units: units);

        var taken = Assert.Single(Step(state, new Watch("pk")).Events.OfType<WatchTaken>());

        Assert.True(taken.Holds);
        Assert.Null(taken.HoldsInsteadOf);
        Assert.Equal("brigand-1", taken.PassedUpTargetId);
    }

    [Fact]
    public void TheEnemyNeverWatches()
    {
        var units = """
            P captain 0,0
            P recruit:bo 0,4
            E archer 8,2 group:field behavior:hold

            """;
        var state = Start(units: units).Do(new Wait("hale")).Do(new Wait("bo")).Do(new EndPhase());

        Assert.Contains("the enemy does not watch", Overwatch.Refusal(state, Starter, state.Find("archer-1")!));
        Assert.DoesNotContain(EnemyAi.Plan(state, Starter), c => c is Watch);
        Assert.Contains(EnemyAi.Plan(state, Starter), c => c is Wait { UnitId: "archer-1" });
    }

    [Fact]
    public void TheHeaderRoundTripsAndRefusesAnotherWord()
    {
        var map = MapFixture.Parse(Field("overwatch: hold", Line), "field.map");
        Assert.True(map.OverwatchEnabled && map.OverwatchHold);
        Assert.Contains("overwatch: hold\n", MapFormat.Write(map, Starter));
        Assert.False(MapFixture.Parse(Field("overwatch: on", Line), "field.map").OverwatchHold);

        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Field("overwatch: yes", Line), "bad.map"));
        Assert.Contains("'on' or 'hold'", error.Message);
    }

    /// <summary>Issue 556: the sample is the shipped raid with only <c>overwatch: hold</c> added.</summary>
    [Fact]
    public void TheRaidOverwatchSampleIsTheShippedRaidWithOnlyTheHoldHeaderAdded()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var shippedPath = Path.Combine(repo, "content", "keep", "ironwake_raid.map");
        var samplePath = Path.Combine(repo, "docs", "samples", "ironwake_raid_overwatch.map");
        var shipped = File.ReadAllText(shippedPath).Replace("\r\n", "\n").Split('\n').ToList();
        var sampleText = File.ReadAllText(samplePath).Replace("\r\n", "\n");
        var sampleLines = sampleText.Split('\n').ToList();

        var at = sampleLines.IndexOf("overwatch: hold");
        Assert.True(at >= 0);
        sampleLines.RemoveAt(at);
        Assert.Equal(shipped, sampleLines);

        var sample = MapFiles.Load(samplePath, MapFixture.Content);
        var original = MapFiles.Load(shippedPath, MapFixture.Content);
        Assert.Equal(original with { OverwatchEnabled = true, OverwatchHold = true }, sample);
        Assert.Equal(sampleText, MapFormat.Write(sample, Starter));
    }
}
