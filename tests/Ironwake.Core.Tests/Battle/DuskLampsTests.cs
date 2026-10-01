using Ironwake.Cli;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 382, DESIGN.md 13.7: a Guard group a player-phase command wakes on a dusk map lights
/// its lamps. The wake event names its members and tiles, and the player side sees them
/// wherever they stand until the enemy phase that follows ends; a group still asleep stays
/// in the dark, a wake in daylight or on the enemy phase lights nothing, and a Recall past
/// the waking step puts the lamps out with the rest of the board.
/// </summary>
public class DuskLampsTests
{
    /// <summary>A 12x4 field at sight 1: Hale at 0,1 and Wren at 0,2, Guard group watch at 7,0 and 11,3, Guard group far at 11,0.</summary>
    private static string Field(bool dusk) =>
        $"""
        name: Field
        size: 12x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(dusk ? "dusk: 1" : "")}

        ............
        ............
        ............
        ............

        units:
        P captain 0,1
        P recruit:wren 0,2
        E soldier 7,0 group:watch behavior:guard
        E archer 11,3 group:watch behavior:guard
        E brigand 11,0 group:far behavior:guard

        """.Replace("\n\n\n", "\n\n");

    private static BattleState Start(bool dusk = true) => BattleFixture.Start(map: Field(dusk));

    private static readonly Move Wakes = new("hale", new Coord(3, 0));

    [Fact]
    public void AGroupWokenAtDuskOnThePlayerPhaseIsSeenThroughTheNextEnemyPhaseAndNoLonger()
    {
        var woken = Start().Do(Wakes);

        Assert.True(Dusk.Seen(woken, woken.Find("soldier-1")!));
        Assert.True(Dusk.Seen(woken, woken.Find("archer-1")!));
        Assert.True(Dusk.Sees(woken, Side.Player, new Coord(11, 3)));

        var enemyPhase = woken.Do(new EndPhase());
        Assert.Equal(Side.Enemy, enemyPhase.Phase);
        Assert.True(Dusk.Seen(enemyPhase, enemyPhase.Find("archer-1")!));

        var nextTurn = enemyPhase.Do(new EndPhase());
        Assert.Equal(Side.Player, nextTurn.Phase);
        Assert.Empty(nextTurn.LitGroups);
        Assert.False(Dusk.Seen(nextTurn, nextTurn.Find("archer-1")!));
    }

    [Fact]
    public void AGroupStillAsleepStaysUnseenWhileAnotherGroupsLampsAreLit()
    {
        var woken = Start().Do(Wakes);

        Assert.False(woken.IsAwake("far"));
        Assert.False(Dusk.Seen(woken, woken.Find("brigand-1")!));
        Assert.False(Dusk.Sees(woken, Side.Player, new Coord(11, 0)));
    }

    [Fact]
    public void BeforeTheWakeTheGroupIsUnseen()
    {
        var state = Start();

        Assert.False(Dusk.Seen(state, state.Find("soldier-1")!));
        Assert.False(Dusk.Seen(state, state.Find("archer-1")!));
    }

    [Fact]
    public void TheWakeEventNamesTheLitMembersRowMajorByTile()
    {
        var result = Start().Try(Wakes);

        var woke = Assert.Single(result.Events.OfType<GroupWoke>());
        Assert.Equal("watch", woke.Group);
        Assert.Equal(WakeCause.Proximity, woke.Cause);
        Assert.Equal(new[] { new Lamp("soldier-1", new Coord(7, 0)), new Lamp("archer-1", new Coord(11, 3)) }, woke.Lamps.ToArray());
        Assert.Equal(new[] { "watch" }, result.Next.LitGroups.ToArray());
    }

    [Fact]
    public void AWakeInDaylightLightsNothing()
    {
        var result = Start(dusk: false).Try(Wakes);

        Assert.Equal(new GroupWoke("watch", WakeCause.Proximity), Assert.Single(result.Events.OfType<GroupWoke>()));
        Assert.Empty(result.Next.LitGroups);
    }

    [Fact]
    public void AGroupWokenOnTheEnemyPhaseIsNotLit()
    {
        var enemyPhase = Start().Do(new EndPhase());
        var near = enemyPhase.WithUnit(enemyPhase.Find("hale")! with { At = new Coord(3, 0) });
        var result = near.Try(new Wait("brigand-1"));

        Assert.Equal(new GroupWoke("watch", WakeCause.Proximity), Assert.Single(result.Events.OfType<GroupWoke>()));
        Assert.Empty(result.Next.LitGroups);
        Assert.False(Dusk.Seen(result.Next, result.Next.Find("archer-1")!));
    }

    [Fact]
    public void ALitMemberIsSeenWhereverItMovesOnTheEnemyPhase()
    {
        var enemyPhase = Start().Do(Wakes).Do(new EndPhase());
        var moved = enemyPhase.WithUnit(enemyPhase.Find("archer-1")! with { At = new Coord(10, 2) });

        Assert.True(Dusk.Seen(moved, moved.Find("archer-1")!));
        Assert.False(Dusk.Sees(moved, Side.Player, new Coord(11, 3)));
    }

    [Fact]
    public void ARecallPastTheWakingStepPutsTheLampsOut()
    {
        var woken = Start().Do(Wakes);
        var recalled = woken.Do(new Recall(0));

        Assert.Empty(recalled.LitGroups);
        Assert.False(Dusk.Seen(recalled, recalled.Find("archer-1")!));
    }

    [Fact]
    public void TheWakeLineNamesTheLampsItLit()
    {
        var woke = Start().Try(Wakes).Events.OfType<GroupWoke>().Single();

        Assert.Equal("Group watch wakes: proximity; its lamps are lit (soldier-1 7,0, archer-1 11,3)", PlaySession.Describe(woke, Starter, UnitNames.None));
        Assert.Equal("Group watch wakes: proximity", PlaySession.Describe(new GroupWoke("watch", WakeCause.Proximity), Starter, UnitNames.None));
    }

    [Fact]
    public void ThreatPricesALitMemberAsInDaylight()
    {
        var woken = Start().Do(Wakes);
        var hale = woken.Find("hale")!;
        var text = PlaySession.ThreatText(woken, Starter, hale, hale.At, Queries.Threats(woken, Starter, hale, hale.At)!, Queries.SleepingThreats(woken, Starter, hale, hale.At)!, Queries.Unseeing(woken, Starter, hale, hale.At));

        Assert.Contains("  soldier-1 from ", text);
        Assert.Contains("if all land:", text);

        var asleep = Start();
        var dark = PlaySession.ThreatText(asleep, Starter, asleep.Find("hale")!, new Coord(3, 0), Queries.Threats(asleep, Starter, asleep.Find("hale")!, new Coord(3, 0))!, Queries.SleepingThreats(asleep, Starter, asleep.Find("hale")!, new Coord(3, 0))!, Queries.Unseeing(asleep, Starter, asleep.Find("hale")!, new Coord(3, 0)));
        Assert.DoesNotContain("soldier-1", dark);
    }

    [Fact]
    public void TheLitGroupsAreInTheCanonicalText()
    {
        Assert.Contains("\nlit watch\n", Start().Do(Wakes).Canonical());
        Assert.DoesNotContain("\nlit", Start().Canonical());
    }
}
