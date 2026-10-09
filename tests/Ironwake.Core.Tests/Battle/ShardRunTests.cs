using Ironwake.Sim;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The secret path's shard race at the keep (issue 1386; Lotus 2026-10-08, Table round 487), on the Iron Warden
/// (<c>hask_warden</c>) on a <c>shard_race:</c> map: stage 1's fall sends him to the inner tile on 1 HP with the shard;
/// he cannot be attacked, swallows when his side's phases run out, and a company unit beside him takes the shard and
/// breaks it, which leaves him alive off the board and wins the map.
/// </summary>
public class ShardRunTests
{
    private const string Hall = """
        name: Hall
        size: 10x5
        win: defeat_boss
        shard_race: 9,2 3
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ..........
        ..........
        ..........
        ..........
        ..........

        units:
        P captain 3,2
        P recruit:wren 1,2
        P recruit:ivo 0,0
        B hask_warden 4,2 group:lord behavior:boss
        E soldier 7,0 group:lord behavior:hold
        """;

    private static BattleState Start(ulong seed = 1386, string map = Hall) =>
        BattleFixture.Start(seed, ValueList<Unit>.Of(Hale, Wren, Ivo), map);

    private static BattleUnit Captain(BattleState state) => state.Units.Single(u => u.IsCaptain);

    private static BattleUnit Hask(BattleState state) => state.Units.Single(u => u.Unit.ClassId == "iron_warden");

    private static (BattleState Next, List<GameEvent> Events) Felled(BattleState state)
    {
        var events = new List<GameEvent>();
        var next = Swallow.Take(state.WithUnit(Hask(state) with { Hp = 0 }), Starter, Hask(state).Id, events);
        return (next, events);
    }

    /// <summary>The board after the fall with the captain standing at <paramref name="at"/>.</summary>
    private static BattleState RunningWithCaptainAt(Coord at)
    {
        var (state, _) = Felled(Start());
        return state.WithUnit(Captain(state) with { At = at });
    }

    [Fact]
    public void TheHeaderRoundTripsAndNeedsDefeatBoss()
    {
        var map = MapFixture.Parse(Hall, "hall.map");
        Assert.Equal(new ShardRace(new Coord(9, 2), 3), map.ShardRace);
        Assert.Contains("shard_race: 9,2 3\n", MapFormat.Write(map, Starter));

        var rout = Assert.Throws<MapException>(() => MapFixture.Parse(Hall.Replace("win: defeat_boss", "win: rout"), "hall.map"));
        Assert.Contains("shard_race: needs win: defeat_boss", rout.Message);
        var outside = Assert.Throws<MapException>(() => MapFixture.Parse(Hall.Replace("shard_race: 9,2 3", "shard_race: 10,2 3"), "hall.map"));
        Assert.Contains("outside the map", outside.Message);
        var long_ = Assert.Throws<MapException>(() => MapFixture.Parse(Hall.Replace("shard_race: 9,2 3", "shard_race: 9,2 10"), "hall.map"));
        Assert.Contains("must be 1 to 9", long_.Message);
        Assert.Null(MapFixture.Parse(Hall.Replace("shard_race: 9,2 3\n", ""), "hall.map").ShardRace);
    }

    [Fact]
    public void StageOneFallingOnARaceMapRunsToTheInnerTileInsteadOfSwallowing()
    {
        var (state, events) = Felled(Start());
        var hask = Hask(state);

        Assert.Equal(new ShardRaceBegan(hask.Id, new Coord(4, 2), new Coord(9, 2), 3), Assert.Single(events));
        Assert.Equal(new Coord(9, 2), hask.At);
        Assert.Equal(1, hask.Hp);
        Assert.Equal(3, hask.ShardIn);
        Assert.False(hask.Swallowed);
        Assert.Equal(0, state.FrozenIron);
        Assert.False(state.Outcome.IsOver);
    }

    [Fact]
    public void WithTheInnerTileTakenHeRunsToTheNearestFreeTile()
    {
        var start = Start();
        start = start.WithUnit(start.Units.Single(u => u.Side == Side.Enemy && !u.IsBoss) with { At = new Coord(9, 2) });
        var (state, _) = Felled(start);

        Assert.Equal(1, Hask(state).At.DistanceTo(new Coord(9, 2)));
    }

    [Fact]
    public void ARunnerCannotBeAttackedAndIsNotOffered()
    {
        var state = RunningWithCaptainAt(new Coord(8, 2));
        var refused = state.Refused(new Attack(Captain(state).Id, Hask(state).Id));

        Assert.Equal(RejectionReason.CannotTakeShard, refused.Reason);
        Assert.Contains("holding the shard", refused.Message);
        Assert.DoesNotContain(Resolver.Legal(state, Starter), c => c is Attack a && a.TargetId == Hask(state).Id);
        Assert.Contains(new TakeShard(Captain(state).Id, Hask(state).Id), Resolver.Legal(state, Starter));
    }

    [Fact]
    public void AStrayHitWhileHeRunsLeavesHimOnOne()
    {
        var (state, _) = Felled(Start());
        var events = new List<GameEvent>();
        var again = Swallow.Take(state.WithUnit(Hask(state) with { Hp = 0 }), Starter, Hask(state).Id, events);

        Assert.Empty(events);
        Assert.Equal(1, Hask(again).Hp);
        Assert.Equal(3, Hask(again).ShardIn);
    }

    [Fact]
    public void TheRaceTicksAtHisPhaseStartAndHeSwallowsWhenItRunsOut()
    {
        var (state, _) = Felled(Start());
        var first = state.Try(new EndPhase());
        Assert.Contains(new ShardCountdown(Hask(state).Id, 2), first.Events);
        Assert.Equal(2, Hask(first.Next).ShardIn);
        Assert.False(Hask(first.Next).Swallowed);

        var board = first.Next;
        var events = new List<GameEvent>(first.Events);
        while (!events.OfType<ShardSwallowed>().Any())
        {
            var step = board.Try(new EndPhase());
            Assert.Null(step.Rejection);
            events.AddRange(step.Events);
            board = step.Next;
        }

        var hask = Hask(board);
        Assert.Contains(new ShardCountdown(hask.Id, 0), events);
        Assert.True(hask.Swallowed);
        Assert.Equal(hask.MaxHp(Starter), hask.Hp);
        Assert.Equal(new Coord(9, 2), hask.At);
        Assert.Equal(Side.Enemy, board.Phase);
        Assert.DoesNotContain(events, e => e is FrozenIronFell);
    }

    [Fact]
    public void ARunnerPlansNothingAndStrikesNothing()
    {
        var state = RunningWithCaptainAt(new Coord(8, 2));
        var hask = Hask(state);

        Assert.Equal(new Command[] { new Wait(hask.Id) }, EnemyAi.PlanUnit(state with { Phase = Side.Enemy }, Starter, hask with { Moved = false, Acted = false }));
        Assert.Null(EnemyAi.StrikeOn(state, Starter, hask with { Moved = false, Acted = false }, Captain(state)));
    }

    [Fact]
    public void TakingTheShardLeavesHimAliveInTheComaAndWinsTheMap()
    {
        var state = RunningWithCaptainAt(new Coord(8, 2));
        var hask = Hask(state);
        var result = state.Try(new TakeShard(Captain(state).Id, hask.Id));

        Assert.Null(result.Rejection);
        Assert.Equal(new ShardBroken(Captain(state).Id, hask.Id, new Coord(9, 2)), Assert.Single(result.Events));
        Assert.DoesNotContain(result.Next.Units, u => u.Id == hask.Id);
        Assert.Equal(ValueList<string>.Of(hask.Id), result.Next.Coma);
        Assert.True(Captain(result.Next).Acted);
        Assert.Equal(BattleResult.Won, result.Next.Outcome.Result);
    }

    [Fact]
    public void TheTakeIsRefusedFromAfarOrWithNoRunner()
    {
        var far = RunningWithCaptainAt(new Coord(6, 2));
        Assert.Equal(RejectionReason.CannotTakeShard, far.Refused(new TakeShard(Captain(far).Id, Hask(far).Id)).Reason);

        var normal = Start();
        normal = normal.WithUnit(Captain(normal) with { At = new Coord(3, 2) });
        var none = normal.Refused(new TakeShard(Captain(normal).Id, Hask(normal).Id));
        Assert.Equal(RejectionReason.CannotTakeShard, none.Reason);
        Assert.Contains("only a beaten boss running with the shard", none.Message);
    }

    [Fact]
    public void WhileHeRunsTheTurnLimitEndsNothing()
    {
        var (state, _) = Felled(Start());
        var late = state with { Turn = 11 };

        Assert.True(late.Racing);
        Assert.False(late.PastLimit);
    }

    [Fact]
    public void WithoutTheHeaderTheFallSwallowsAtOnce()
    {
        var (state, events) = Felled(Start(map: Hall.Replace("shard_race: 9,2 3\n", "")));

        Assert.IsType<ShardSwallowed>(Assert.Single(events));
        Assert.True(Hask(state).Swallowed);
        Assert.Null(Hask(state).ShardIn);
    }

    [Fact]
    public void TheBoardPrintsTheRace()
    {
        var (state, _) = Felled(Start());

        Assert.Contains($"Hask holds the shard: swallows in 3 (a unit beside takes it as its action: take <unit> {Hask(state).Id})", MapRenderer.Render(state, Starter));
    }

    [Fact]
    public void TheRaceSampleParses()
    {
        var map = MapFiles.Load(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "ironwake_keep_shard_race.map"), MapFixture.Content);

        Assert.Equal(new ShardRace(new Coord(15, 6), 5), map.ShardRace);
        Assert.Equal(new[] { 0, 2, 2 }, map.Events.Select(e => e.Trigger).OfType<RaceTrigger>().Select(r => r.Phases));
    }

    private const string Guarded = Hall + "\n\nevents:\nguard race 0 spawn soldier 5,0 group:shard behavior:hold\nwave race 2 spawn brigand 9,4 group:shard behavior:aggressive\n";

    [Fact]
    public void ARaceTriggerRoundTripsAndNeedsTheHeaderAndAPhaseBeforeTheSwallow()
    {
        var map = MapFixture.Parse(Guarded, "hall.map");
        Assert.Equal(new RaceTrigger(0), map.Events[0].Trigger);
        Assert.Equal(new RaceTrigger(2), map.Events[1].Trigger);
        var written = MapFormat.Write(map, Starter);
        Assert.Contains("guard race 0 spawn soldier 5,0 group:shard behavior:hold", written);
        Assert.Contains("wave race 2 spawn brigand 9,4 group:shard behavior:aggressive", written);

        var orphan = Assert.Throws<MapException>(() => MapFixture.Parse(Guarded.Replace("shard_race: 9,2 3\n", ""), "hall.map"));
        Assert.Contains("event 'guard' uses the race trigger but the map has no shard_race: header", orphan.Message);
        var late = Assert.Throws<MapException>(() => MapFixture.Parse(Guarded.Replace("race 2", "race 3"), "hall.map"));
        Assert.Contains("event 'wave' fires 3 phases into the race, but he swallows after 3", late.Message);
        var bare = Assert.Throws<MapException>(() => MapFixture.Parse(Guarded.Replace("race 2", "race x"), "hall.map"));
        Assert.Contains("race trigger needs the phases into the shard race", bare.Message);
    }

    [Fact]
    public void ARaceZeroEventFiresAsHeRunsOffTheEdge()
    {
        var (state, events) = Felled(Start(map: Guarded));

        Assert.Equal(new[] { "guard" }, events.OfType<MapEventFired>().Select(f => f.Name));
        Assert.Equal(Side.Enemy, state.UnitAt(new Coord(5, 0))!.Side);
        Assert.IsType<ShardRaceBegan>(events[0]);
    }

    [Fact]
    public void ARaceEventFiresAtItsTickAndNotBefore()
    {
        var (state, _) = Felled(Start(map: Guarded));
        var first = state.Try(new EndPhase());
        Assert.DoesNotContain(first.Events, e => e is MapEventFired { Name: "wave" });

        var player = first.Next.Try(new EndPhase());
        var second = player.Next.Try(new EndPhase());
        var tick = second.Events.ToList().FindIndex(e => e is ShardCountdown { Left: 1 });
        var fired = second.Events.ToList().FindIndex(e => e is MapEventFired { Name: "wave" });
        Assert.True(tick >= 0 && fired > tick);
        Assert.Equal(Side.Enemy, second.Next.UnitAt(new Coord(9, 4))!.Side);
    }

    [Fact]
    public void WithNoRaceTheRaceEventsNeverFire()
    {
        var state = Start(map: Guarded);
        var events = new List<GameEvent>();
        var board = state;
        for (var i = 0; i < 6; i++)
        {
            var step = board.Try(new EndPhase());
            events.AddRange(step.Events);
            board = step.Next;
        }

        Assert.DoesNotContain(events, e => e is MapEventFired);
    }

    [Fact]
    public void TheSimTakesTheShardFromATileBeside()
    {
        var state = RunningWithCaptainAt(new Coord(6, 2));
        var captain = Captain(state) with { Moved = false, Acted = false };
        var plan = HeuristicPlayer.PlanUnit(state.WithUnit(captain), Starter, captain);

        var move = Assert.IsType<Move>(plan[0]);
        Assert.Equal(1, move.To.DistanceTo(new Coord(9, 2)));
        Assert.Equal(new TakeShard(captain.Id, Hask(state).Id), plan[^1]);
    }

    private const string Long = """
        name: Long hall
        size: 16x5
        win: defeat_boss
        shard_race: 15,2 3
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ................
        ................
        ................
        ................
        ................

        units:
        P captain 6,2
        P recruit:wren 6,4
        P recruit:ivo 6,0
        B hask_warden 10,2 group:lord behavior:boss
        E soldier 0,0 group:lord behavior:hold
        """;

    [Fact]
    public void WithNoTakeInReachTheSimWalksTowardTheRunnerNotTheNearestEnemy()
    {
        var (state, _) = Felled(Start(map: Long));
        var captain = Captain(state) with { Moved = false, Acted = false };
        var plan = HeuristicPlayer.PlanUnit(state.WithUnit(captain), Starter, captain);

        Assert.DoesNotContain(plan, c => c is Attack or TakeShard);
        Assert.True(Assert.IsType<Move>(plan[0]).To.X > 6);
    }
}
