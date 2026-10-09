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

        Assert.Equal(new ShardRace(new Coord(0, 6), 5), map.ShardRace);
    }
}
