using Ironwake.Content;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Hask's second stage (issue 1385, Lotus's rework, Table round 487; numbers provisional on #1247), on the pitched
/// Iron Warden (<c>hask_warden</c>) alone. Stage 1's bar reaching 0 is the swallow: he stays on his tile on a fresh
/// bar of 40 with Def and Res +3, and from his next phase start Frozen Iron lands on every unit on the board, his
/// side and him too, flat past Def and Res, 2 and then 2 more each of his phases to 10; it lands at his side's phase
/// start alone (issue 1395). The Kin heals him 6 at his phase start, after the Frozen Iron. His fall in stage 2 drains the cold and leaves the shard on his tile.
/// </summary>
public class SwallowTests
{
    private const string Hall = """
        name: Hall
        size: 10x5
        win: defeat_boss
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
        E soldier 7,2 group:lord behavior:hold
        """;

    private static BattleState Start(ulong seed = 1385) =>
        BattleFixture.Start(seed, ValueList<Unit>.Of(Hale, Wren, Ivo), Hall);

    private static BattleUnit Captain(BattleState state) => state.Units.Single(u => u.IsCaptain);

    private static BattleUnit Hask(BattleState state) => state.Units.Single(u => u.Unit.ClassId == "iron_warden");

    private static BattleState Swallowed(BattleState state) => Swallow.Take(state.WithUnit(Hask(state) with { Hp = 0 }), Starter, Hask(state).Id, new List<GameEvent>());

    [Fact]
    public void TheWardenCarriesTheSecondStagesNumbers()
    {
        var stage = Starter.Unit("hask_warden").Swallow!;

        Assert.Equal((40, 3, 3, 6), (stage.Hp, stage.Def, stage.Res, stage.Heal));
        Assert.Contains("pommel is empty", stage.Description);
        Assert.Null(Starter.Unit("hask").Swallow);
        Assert.Equal(stage, Hask(Start()).Kin);
    }

    [Fact]
    public void AHitTakingStageOneToZeroSwallowsInsteadOfKilling()
    {
        var swallowedOnce = false;
        for (ulong seed = 1; seed <= 40 && !swallowedOnce; seed++)
        {
            var state = Start(seed);
            state = state.WithUnit(Hask(state) with { Hp = 1 });
            var result = state.Try(new Attack(Captain(state).Id, Hask(state).Id));
            if (!result.Events.OfType<CombatFought>().Single().Strikes.Any(s => s.Hit && s.AttackerId == Captain(state).Id))
            {
                continue;
            }

            swallowedOnce = true;
            var hask = Hask(result.Next);
            Assert.Contains(new ShardSwallowed(hask.Id, new Coord(4, 2), 40), result.Events);
            Assert.DoesNotContain(result.Events, e => e is UnitDied d && d.UnitId == hask.Id);
            Assert.True(hask.Swallowed);
            Assert.Equal(40, hask.MaxHp(Starter));
            Assert.Equal(Starter.StatsOf(Starter.Unit("hask_warden")).Def + 3, Starter.StatsOf(hask.Unit).Def);
            Assert.Equal(Starter.StatsOf(Starter.Unit("hask_warden")).Res + 3, Starter.StatsOf(hask.Unit).Res);
            Assert.Contains("pommel is empty", hask.Unit.Description);
            Assert.Equal(Swallow.FirstDose, result.Next.FrozenIron);
            Assert.False(result.Next.Outcome.IsOver);
        }

        Assert.True(swallowedOnce);
    }

    [Fact]
    public void TheSwallowingHitPaysNoKillExp()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var state = Start(seed);
            state = state.WithUnit(Hask(state) with { Hp = 1 });
            var result = state.Try(new Attack(Captain(state).Id, Hask(state).Id));
            if (result.Events.OfType<ShardSwallowed>().Any())
            {
                var earned = result.Events.OfType<ExpGained>().Where(g => g.UnitId == Captain(state).Id).Sum(g => g.Amount);
                var landed = Experience.ForCombat(Captain(state).Unit.Level, Hask(state).Unit.Level, true, false, true);
                Assert.Equal(landed, earned);
                return;
            }
        }

        Assert.Fail("no seed landed the swallowing hit");
    }

    [Fact]
    public void FrozenIronLandsOnEveryUnitAtHisNextPhaseStartPastDefAndRes()
    {
        var state = Swallowed(Start()) with { Phase = Side.Player };
        var before = state.Units.ToDictionary(u => u.Id, u => u.Hp);

        var result = state.Try(new EndPhase());

        var fell = result.Events.OfType<FrozenIronFell>().Single();
        Assert.Equal(2, fell.Amount);
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), fell.Struck.Order(StringComparer.Ordinal));
        Assert.Equal(fell.Struck.Select(id => before[id] - 2), fell.HpAfter);
        Assert.All(result.Next.Units.Where(u => !u.Swallowed), u => Assert.Equal(before[u.Id] - 2, u.Hp));
        Assert.Equal(4, result.Next.FrozenIron);
    }

    [Fact]
    public void FrozenIronClimbsByTwoAndStopsAtTen()
    {
        var state = Swallowed(Start()) with { FrozenIron = 8 };

        var once = state.Try(new EndPhase());
        var between = once.Next.Try(new EndPhase());
        var twice = between.Next.Try(new EndPhase());

        Assert.Equal(8, once.Events.OfType<FrozenIronFell>().Single().Amount);
        Assert.Equal(Side.Enemy, twice.Next.Phase);
        Assert.Equal(10, twice.Events.OfType<FrozenIronFell>().Single().Amount);
        Assert.Equal(Swallow.MostDose, twice.Next.FrozenIron);
    }

    [Fact]
    public void NoFrozenIronFallsBeforeTheSwallow()
    {
        var result = Start().Try(new EndPhase());

        Assert.DoesNotContain(result.Events, e => e is FrozenIronFell or KinHealed);
    }

    [Fact]
    public void FrozenIronCanKill()
    {
        var state = Swallowed(Start());
        var wren = state.Units.Single(u => u.Unit.Id == "wren");
        state = state.WithUnit(wren with { Hp = 2 });

        var result = state.Try(new EndPhase());

        Assert.Contains(new UnitDied(wren.Id, Side.Player, wren.At), result.Events);
        Assert.Null(result.Next.Find(wren.Id));
    }

    [Fact]
    public void TheKinHealsHimAtHisPhaseStartAfterTheFrozenIron()
    {
        var state = Swallowed(Start()) with { Phase = Side.Player };
        state = state.WithUnit(Hask(state) with { Hp = 20 });

        var result = state.Try(new EndPhase());

        Assert.Equal(Side.Enemy, result.Next.Phase);
        var events = result.Events.ToList();
        Assert.True(events.FindIndex(e => e is FrozenIronFell) < events.FindIndex(e => e is KinHealed));
        Assert.Contains(new KinHealed(Hask(state).Id, 6, 24), result.Events);
        Assert.Equal(24, Hask(result.Next).Hp);
    }

    [Fact]
    public void NeitherFrozenIronNorTheKinLandsOnTheOtherSidesPhaseStart()
    {
        var state = Swallowed(Start()) with { Phase = Side.Enemy, FrozenIron = 6 };
        state = state.WithUnit(Hask(state) with { Hp = 20 });
        var before = state.Units.ToDictionary(u => u.Id, u => u.Hp);

        var result = state.Try(new EndPhase());

        Assert.Equal(Side.Player, result.Next.Phase);
        Assert.DoesNotContain(result.Events, e => e is FrozenIronFell or KinHealed);
        Assert.All(result.Next.Units, u => Assert.Equal(before[u.Id], u.Hp));
        Assert.Equal(6, result.Next.FrozenIron);
    }

    [Fact]
    public void TheSimsStageReadRecordsTheCompanyTheSwallowFound()
    {
        var before = Start();
        var after = Swallowed(before);
        after = after.WithUnit(Captain(after) with { Hp = 9 });

        var stage = Ironwake.Sim.StageTwo.After(null, before, after, new GameEvent[] { new ShardSwallowed(Hask(after).Id, Hask(after).At, 40) });

        Assert.Equal(new Ironwake.Sim.StageTwo(0, 0, 3, 9), stage);
    }

    [Fact]
    public void HisFallInStageTwoDrainsTheColdAndLeavesTheShardOnHisTile()
    {
        var state = Swallowed(Start());
        state = state.WithUnit(Hask(state) with { Hp = 2 });

        var result = state.Try(new EndPhase());

        var events = result.Events.ToList();
        var died = events.FindIndex(e => e is UnitDied d && d.UnitId == Hask(state).Id);
        Assert.True(died >= 0);
        Assert.Equal(new ColdDrained(Hask(state).Id, new Coord(4, 2)), events[died + 1]);
        Assert.Equal(BattleResult.Won, result.Next.Outcome.Result);
    }

    [Fact]
    public void ADeathBeforeTheSwallowDrainsNoCold()
    {
        var state = Swallowed(Start());
        var wren = state.Units.Single(u => u.Unit.Id == "wren");
        state = state.WithUnit(wren with { Hp = 2 });

        var result = state.Try(new EndPhase());

        Assert.DoesNotContain(result.Events, e => e is ColdDrained);
    }

    [Fact]
    public void RecallRestoresTheFirstStage()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var state = Start(seed);
            state = state.WithUnit(Hask(state) with { Hp = 1 });
            var result = state.Try(new Attack(Captain(state).Id, Hask(state).Id));
            if (result.Events.OfType<ShardSwallowed>().Any())
            {
                var recalled = result.Next.Try(new Recall(result.Next.History.Count - 1));
                Assert.Null(recalled.Rejection);
                Assert.False(Hask(recalled.Next).Swallowed);
                Assert.Equal(0, recalled.Next.FrozenIron);
                return;
            }
        }

        Assert.Fail("no seed landed the swallowing hit");
    }

    [Fact]
    public void TheSecondStageRoundTripsThroughTheProtocol()
    {
        var state = Swallowed(Start());

        var read = Ironwake.Content.Protocol.ProtocolJson.ReadState(Ironwake.Content.Protocol.ProtocolJson.State(state, Starter), Starter);

        Assert.True(Hask(read).Swallowed);
        Assert.Equal(Hask(state).Kin, Hask(read).Kin);
        Assert.Equal(state.FrozenIron, read.FrozenIron);
    }

    [Fact]
    public void TheCardSaysWhatTheStageDoes()
    {
        var state = Start();
        Assert.Contains(Ironwake.Cli.PlaySession.ShowLines(state, Starter, Hask(state)), l => l.Contains("At 0 HP he swallows the shard"));

        var swallowed = Swallowed(state);
        var lines = Ironwake.Cli.PlaySession.ShowLines(swallowed, Starter, Hask(swallowed));
        Assert.Contains(lines, l => l.Contains("pommel is empty"));
        Assert.Contains(lines, l => l.Contains("Frozen Iron lands for 2"));
    }

    [Fact]
    public void ASwallowWithNoHpIsRefusedNamingTheField()
    {
        var units = Ironwake.Core.Tests.Content.Fixture.Units.Replace(
            "\"inventory\": [ { \"item\": \"iron_sword\" } ] }",
            "\"inventory\": [ { \"item\": \"iron_sword\" } ], \"swallow\": { \"hp\": 0, \"def\": 3, \"res\": 3, \"heal\": 6 } }");

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Ironwake.Core.Tests.Content.Fixture.Files(units: units)));

        Assert.Equal(("recruit", "swallow.hp"), (e.Entry, e.Field));
    }

    [Fact]
    public void ASwallowRoundTripsThroughTheContentWriter()
    {
        var units = Ironwake.Core.Tests.Content.Fixture.Units.Replace(
            "\"inventory\": [ { \"item\": \"iron_sword\" } ] }",
            "\"inventory\": [ { \"item\": \"iron_sword\" } ], \"swallow\": { \"hp\": 40, \"def\": 3, \"res\": 3, \"heal\": 6 } }");
        var content = ContentLoader.Parse(Ironwake.Core.Tests.Content.Fixture.Files(units: units));

        Assert.Equal(new KinStage(40, 3, 3, 6), content.Unit("recruit").Swallow);
        Assert.Equal(content, ContentLoader.Parse(ContentSerializer.Write(content)));
    }
}
