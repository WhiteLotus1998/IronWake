using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The shard under the hill (issue 1386 slice 3a): a <c>kin_shard:</c> map names the sworn who carries it; it drops where
/// he falls; a company unit on or beside it takes it and breaks it; a lying shard goes to the nearest sworn who reaches
/// it at the enemy phase start; and while it is whole the Kin re-takes the oldest fallen sworn each enemy phase start.
/// </summary>
public class KinShardTests
{
    private const string Hill = """
        name: Hill
        size: 10x5
        win: defeat_boss
        kin_shard: 5,2
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
        P recruit:wren 0,4
        P recruit:ivo 0,0
        B soldier 9,2 group:kin behavior:guard
        E soldier 5,2 group:sworn behavior:hold
        E soldier 7,2 group:sworn behavior:hold
        """;

    private static BattleState Start(ulong seed = 1386) => BattleFixture.Start(seed, ValueList<Unit>.Of(Hale, Wren, Ivo), Hill);

    private static BattleUnit Captain(BattleState state) => state.Units.Single(u => u.IsCaptain);

    private static BattleUnit At(BattleState state, int x, int y) => state.UnitAt(new Coord(x, y))!;

    /// <summary>The board with the bearer dead on 5,2, his body left, the shard lying there, in the enemy's coming phase.</summary>
    private static BattleState Dropped(BattleState state)
    {
        var bearer = At(state, 5, 2);
        return Hollow.LeaveBody(state, bearer).WithoutUnit(bearer.Id) with { Shard = ShardHold.Lying(new Coord(5, 2)) };
    }

    [Fact]
    public void TheHeaderRoundTripsAndNamesAPlacedSworn()
    {
        var map = MapFixture.Parse(Hill, "hill.map");
        Assert.Equal(new Coord(5, 2), map.KinShard);
        Assert.Contains("kin_shard: 5,2\n", MapFormat.Write(map, Starter));

        var empty = Assert.Throws<MapException>(() => MapFixture.Parse(Hill.Replace("kin_shard: 5,2", "kin_shard: 4,2"), "hill.map"));
        Assert.Contains("no E line places an enemy there", empty.Message);
        var boss = Assert.Throws<MapException>(() => MapFixture.Parse(Hill.Replace("kin_shard: 5,2", "kin_shard: 9,2"), "hill.map"));
        Assert.Contains("is a boss", boss.Message);
        Assert.Null(MapFixture.Parse(Hill.Replace("kin_shard: 5,2\n", ""), "hill.map").KinShard);
    }

    [Fact]
    public void TheBearerCarriesTheShardUntilHeFalls()
    {
        var state = Start();

        Assert.Null(state.Shard);
        Assert.Equal(ShardHold.Carried(At(state, 5, 2).Id), KinShard.Of(state));
        Assert.Contains("carries the shard", KinShard.Line(state, UnitNames.Of(state, Starter)));
        Assert.Null(KinShard.Of(BattleFixture.Start(1386, ValueList<Unit>.Of(Hale, Wren, Ivo), Hill.Replace("kin_shard: 5,2\n", ""))));
    }

    [Fact]
    public void TheBearersFallDropsTheShardOnHisTile()
    {
        for (ulong seed = 1; seed < 60; seed++)
        {
            var start = Start(seed);
            var bearer = At(start, 5, 2);
            var state = start.WithUnit(bearer with { Hp = 1 }).WithUnit(Captain(start) with { At = new Coord(4, 2) });
            var result = state.Try(new Attack(Captain(state).Id, bearer.Id));
            Assert.Null(result.Rejection);
            if (result.Next.Find(bearer.Id) is not null)
            {
                Assert.DoesNotContain(result.Events, e => e is ShardDropped);
                continue;
            }

            Assert.Contains(new ShardDropped(bearer.Id, new Coord(5, 2)), result.Events);
            Assert.Equal(ShardHold.Lying(new Coord(5, 2)), result.Next.Shard);
            Assert.Contains("lies on 5,2", KinShard.Line(result.Next, UnitNames.Of(result.Next, Starter)));
            return;
        }

        Assert.Fail("no seed in 1 to 59 landed the captain's hit");
    }

    [Fact]
    public void ACompanyUnitOnOrBesideTakesTheShardAndBreaksIt()
    {
        var state = Dropped(Start()).WithUnit(Captain(Start()) with { At = new Coord(4, 2) });
        Assert.Contains(new TakeShard(Captain(state).Id, KinShard.Ground), Resolver.Legal(state, Starter));

        var result = state.Try(new TakeShard(Captain(state).Id, KinShard.Ground));

        Assert.Null(result.Rejection);
        Assert.Equal(new KinShardBroken(Captain(state).Id, new Coord(5, 2)), Assert.Single(result.Events));
        Assert.Equal(ShardHold.Gone, result.Next.Shard);
        Assert.True(Captain(result.Next).Acted);
        Assert.Contains("broken", KinShard.Line(result.Next, UnitNames.Of(result.Next, Starter)));

        var on = Dropped(Start()).WithUnit(Captain(Start()) with { At = new Coord(5, 2) });
        Assert.Null(on.Try(new TakeShard(Captain(on).Id, KinShard.Ground)).Rejection);
    }

    [Fact]
    public void TheTakeIsRefusedFromAfarAndWhileTheBearerCarriesIt()
    {
        var far = Dropped(Start());
        var refused = far.Refused(new TakeShard(Captain(far).Id, KinShard.Ground));
        Assert.Equal(RejectionReason.CannotTakeShard, refused.Reason);
        Assert.Contains("stands on it or on a tile beside it", refused.Message);
        Assert.DoesNotContain(Resolver.Legal(far, Starter), c => c is TakeShard);

        var carried = Start();
        carried = carried.WithUnit(Captain(carried) with { At = new Coord(4, 2) });
        var held = carried.Refused(new TakeShard(Captain(carried).Id, KinShard.Ground));
        Assert.Equal(RejectionReason.CannotTakeShard, held.Reason);
        Assert.Contains("carries it; it falls where he falls", held.Message);
    }

    [Fact]
    public void ALyingShardGoesToTheNearestSwornWhoReachesItAtTheEnemyPhase()
    {
        var state = Dropped(Start());
        var sworn = At(state, 7, 2);

        var result = state.Try(new EndPhase());

        Assert.Contains(new ShardPicked(sworn.Id, new Coord(7, 2), new Coord(5, 2)), result.Events);
        Assert.Equal(ShardHold.Carried(sworn.Id), result.Next.Shard);
        var picker = result.Next.Find(sworn.Id)!;
        Assert.Equal(new Coord(5, 2), picker.At);
        Assert.True(picker.Moved && picker.Acted);
    }

    [Fact]
    public void AShardUnderACompanyUnitLiesAnotherPhase()
    {
        var state = Dropped(Start()).WithUnit(Captain(Start()) with { At = new Coord(5, 2) });

        var result = state.Try(new EndPhase());

        Assert.DoesNotContain(result.Events, e => e is ShardPicked);
        Assert.Equal(ShardHold.Lying(new Coord(5, 2)), result.Next.Shard);
    }

    [Fact]
    public void WhileTheShardIsWholeTheKinRetakesTheOldestFallenSworn()
    {
        var start = Start();
        var first = At(start, 7, 2);
        var state = Hollow.LeaveBody(start, first).WithoutUnit(first.Id);

        var result = state.Try(new EndPhase());

        var risen = result.Next.Find(first.Id)!;
        Assert.Contains(new SwornRetaken(first.Id, new Coord(7, 2), risen.MaxHp(Starter)), result.Events);
        Assert.Equal(risen.MaxHp(Starter), risen.Hp);
        Assert.Equal(Side.Enemy, risen.Side);
        Assert.True(risen.Moved && risen.Acted);
        Assert.Empty(result.Next.Bodies);
    }

    [Fact]
    public void ARetakenSwornRisesBesideItsTileWhenItIsTaken()
    {
        var start = Start();
        var first = At(start, 7, 2);
        var state = Hollow.LeaveBody(start, first).WithoutUnit(first.Id);
        state = state.WithUnit(Captain(state) with { At = new Coord(7, 2) });

        var result = state.Try(new EndPhase());

        Assert.Equal(1, result.Next.Find(first.Id)!.At.DistanceTo(new Coord(7, 2)));
    }

    [Fact]
    public void ABrokenShardRetakesNoOneAndNoBossIsRetaken()
    {
        var start = Start();
        var first = At(start, 7, 2);
        var broken = (Hollow.LeaveBody(start, first).WithoutUnit(first.Id)) with { Shard = ShardHold.Gone };
        Assert.DoesNotContain(broken.Try(new EndPhase()).Events, e => e is SwornRetaken);

        var boss = At(start, 9, 2);
        var bossDead = Hollow.LeaveBody(start, boss).WithoutUnit(boss.Id);
        Assert.DoesNotContain(bossDead.Try(new EndPhase()).Events, e => e is SwornRetaken);
    }

    [Fact]
    public void TheShardReadsBackFromTheProtocolAndPrintsInTheCanonicalState()
    {
        foreach (var shard in new[] { ShardHold.Carried("soldier-3"), ShardHold.Lying(new Coord(5, 2)), ShardHold.Gone })
        {
            var state = Start() with { Shard = shard, History = ValueList<BattleState>.Empty };
            var back = ProtocolJson.ReadState(ProtocolJson.State(state, Starter), Starter);

            Assert.Equal(shard, back.Shard);
            Assert.Contains("shard " + KinShard.Word(shard) + "\n", state.Canonical());
        }
    }
}
