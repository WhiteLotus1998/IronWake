using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// What a broken shard does to the frost (issue 1386 slices 3e and 3f, Table rounds 550 to 558, DECISIONS/0395). On a
/// <c>shard_breaks: turns</c> map it keeps climbing and lands on the Kin too, never taking him below half his stage's bar,
/// so the frost alone never wins the hill. Without the header it climbs as before. The held dose, <c>stills</c>, was
/// killed (0395) and no longer parses.
/// </summary>
public class ShardBreaksTests
{
    private const string Hill = """
        name: Hill
        size: 10x5
        win: defeat_boss
        kin_shard: 5,2
        swallowed: 9,2
        shard_breaks: turns
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
        B hask_warden 9,2 group:kin behavior:guard
        E soldier 5,2 group:sworn behavior:hold
        E soldier 7,2 group:sworn behavior:hold
        """;

    private static string Turns => Hill;

    private static string Climbing => Hill.Replace("shard_breaks: turns\n", "");

    private static KinStage Stage => Starter.Unit("hask_warden").Swallow!;

    private static BattleUnit Captain(BattleState state) => state.Units.Single(u => u.IsCaptain);

    private static BattleUnit Kin(BattleState state) => state.Units.Single(u => u.IsBoss);

    /// <summary>The board with the bearer dead on 5,2, the shard lying there, the captain beside it, and the clock lifted at <paramref name="dose"/>.</summary>
    private static BattleState Dropped(string map, int dose)
    {
        var state = BattleFixture.Start(1386, ValueList<Unit>.Of(Hale, Wren, Ivo), map);
        var bearer = state.UnitAt(new Coord(5, 2))!;
        state = Hollow.LeaveBody(state, bearer).WithoutUnit(bearer.Id) with { Shard = ShardHold.Lying(new Coord(5, 2)), FrozenIron = dose, FrozenIronHeld = false };
        return state.WithUnit(Captain(state) with { At = new Coord(4, 2) });
    }

    private static ApplyResult Break(BattleState state) => state.Try(new TakeShard(Captain(state).Id, KinShard.Ground));

    /// <summary>The Frozen Iron landings over the next <paramref name="rounds"/> rounds of ended phases.</summary>
    private static List<FrozenIronFell> Landings(BattleState state, int rounds)
    {
        var landed = new List<FrozenIronFell>();
        for (var i = 0; i < rounds * 2; i++)
        {
            var result = state.Try(new EndPhase());
            Assert.Null(result.Rejection);
            landed.AddRange(result.Events.OfType<FrozenIronFell>());
            state = result.Next;
        }

        return landed;
    }

    [Fact]
    public void TheHeaderRoundTripsAndNeedsAKinShardAndASwallowedBoss()
    {
        Assert.Equal(ShardBreak.Turns, MapFixture.Parse(Turns, "hill.map").ShardBreaks);
        Assert.Contains("shard_breaks: turns\n", MapFormat.Write(MapFixture.Parse(Turns, "hill.map"), Starter));
        Assert.Equal(ShardBreak.None, MapFixture.Parse(Climbing, "hill.map").ShardBreaks);

        var noShard = Assert.Throws<MapException>(() => MapFixture.Parse(Hill.Replace("kin_shard: 5,2\n", ""), "hill.map"));
        Assert.Contains("needs both kin_shard: and swallowed:", noShard.Message);
        var noKin = Assert.Throws<MapException>(() => MapFixture.Parse(Turns.Replace("swallowed: 9,2\n", ""), "hill.map"));
        Assert.Contains("needs both kin_shard: and swallowed:", noKin.Message);
        var word = Assert.Throws<MapException>(() => MapFixture.Parse(Turns.Replace("shard_breaks: turns", "shard_breaks: on"), "hill.map"));
        Assert.Contains("names 'turns', got 'on'", word.Message);
    }

    [Fact]
    public void TheKilledStillsNoLongerParses()
    {
        var stills = Assert.Throws<MapException>(() => MapFixture.Parse(Turns.Replace("shard_breaks: turns", "shard_breaks: stills"), "hill.map"));
        Assert.Contains("names 'turns', got 'stills'", stills.Message);
    }

    [Fact]
    public void WithoutTheHeaderTheBrokenShardLeavesTheClimbAndSparesTheKin()
    {
        var take = Break(Dropped(Climbing, 6));
        Assert.Null(take.Rejection);
        Assert.DoesNotContain(take.Events, e => e is FrozenIronTurned);
        Assert.False(Swallow.Turned(take.Next));
        var landed = Landings(take.Next, 2);
        Assert.Equal(new[] { 6, 9 }, landed.Select(f => f.Amount));
        Assert.All(landed, f => Assert.DoesNotContain(Kin(take.Next).Id, f.Struck));
        Assert.All(landed, f => Assert.False(f.Turned));
    }

    [Fact]
    public void AWholeShardLeavesTheClimbAndSparesTheKin()
    {
        var turns = Dropped(Turns, 6);
        Assert.False(Swallow.Turned(turns));
        var landed = Landings(turns, 2);
        Assert.Equal(6 + Stage.Step, landed[1].Amount);
        Assert.All(landed, f => Assert.DoesNotContain(Kin(turns).Id, f.Struck));
    }

    [Fact]
    public void TurnsLandsTheClimbingFrostOnTheKinToo()
    {
        var take = Break(Dropped(Turns, 3));
        var kin = Kin(take.Next);
        Assert.Equal(new FrozenIronTurned(kin.Id, Hollow.RisenHp(Stage.Hp)), Assert.Single(take.Events.OfType<FrozenIronTurned>()));
        Assert.True(Swallow.Turned(take.Next));

        var first = take.Next.Try(new EndPhase());
        var fell = Assert.Single(first.Events.OfType<FrozenIronFell>());
        Assert.Equal(3, fell.Amount);
        Assert.Contains(kin.Id, fell.Struck);
        Assert.True(fell.Turned);
        Assert.Equal(Stage.Hp - 3 + Stage.Heal, Kin(first.Next).Hp);
        Assert.Equal(3 + Stage.Step, first.Next.FrozenIron);
    }

    [Fact]
    public void TheTurnedFrostNeverTakesTheKinBelowHalfHisStage()
    {
        var floor = Hollow.RisenHp(Stage.Hp);
        var take = Break(Dropped(Turns, 30));
        var kin = Kin(take.Next);
        Assert.Equal(floor, Swallow.Floor(kin, Starter));

        var landed = take.Next.Try(new EndPhase());
        var fell = Assert.Single(landed.Events.OfType<FrozenIronFell>());
        Assert.Equal(floor, fell.HpAfter[fell.Struck.ToList().IndexOf(kin.Id)]);
        Assert.Equal(floor + Stage.Heal, Kin(landed.Next).Hp);
    }

    [Fact]
    public void BelowTheFloorTheTurnedFrostSkipsTheKinAndNeverRaisesHim()
    {
        var take = Break(Dropped(Turns, 3));
        var cut = take.Next.WithUnit(Kin(take.Next) with { Hp = 4 });
        var kin = Kin(cut);
        Assert.True(Swallow.Spared(cut, kin, Starter));

        var landed = cut.Try(new EndPhase());
        var fell = Assert.Single(landed.Events.OfType<FrozenIronFell>());
        Assert.DoesNotContain(kin.Id, fell.Struck);
        Assert.Equal(4 + Stage.Heal, Kin(landed.Next).Hp);
    }

    [Fact]
    public void ATurtleThatNeverStrikesTheKinLosesUnderTurns()
    {
        // Round 553's turtle: the shard broken early, the captain healed to full every player phase, the Kin never struck.
        var state = Break(Dropped(Turns, 0)).Next;
        var floor = Swallow.Floor(Kin(state), Starter);
        for (var phase = 0; phase < 60 && state.Outcome.Result == BattleResult.Ongoing; phase++)
        {
            if (state.Phase == Side.Player && state.Find(Captain(state).Id) is { } captain)
            {
                state = state.WithUnit(captain with { Hp = captain.MaxHp(Starter) });
            }

            state = state.Try(new EndPhase()).Next;
            Assert.True(state.Units.Any(u => u.IsBoss), "the frost alone killed the Kin");
            Assert.True(Kin(state).Hp >= floor);
        }

        Assert.Equal(BattleResult.Lost, state.Outcome.Result);
        Assert.Contains(state.Units, u => u.IsBoss);
    }

    [Fact]
    public void TheRowsSayWhatTheBreakDid()
    {
        var whole = Dropped(Turns, 6);
        var names = UnitNames.Of(whole, Starter);
        Assert.Equal("Hask stands swallowed: Frozen Iron lands for 6 on every unit but him at each enemy phase start, 3 more each time, then the Kin heals him 2", Swallow.Line(whole, Starter, names));

        var turned = Break(Dropped(Turns, 6)).Next;
        Assert.Equal("Hask stands swallowed: Frozen Iron lands for 6 on every unit, him included but never below 10 (the shard is broken) at each enemy phase start, 3 more each time, then the Kin heals him 2", Swallow.Line(turned, Starter, names));
        Assert.Equal("The shard is broken: the Kin re-takes no one, and its frost lands on it too", KinShard.Line(turned, Starter, names));

        Assert.Equal("The shard is broken: the Kin re-takes no one", KinShard.Line(Break(Dropped(Climbing, 6)).Next, Starter, names));
    }
}
