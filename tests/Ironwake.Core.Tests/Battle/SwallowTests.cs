using Ironwake.Content;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Hask's second stage (issue 1385, Lotus's rework, Table round 487; numbers provisional on #1247), on the pitched
/// Iron Warden (<c>hask_warden</c>) alone. Stage 1's bar reaching 0 is the swallow: he stays on his tile on a fresh
/// bar of 20 with Def and Res +3 (issue 1395; 24 in round 502, 40 before), and from his next phase start Frozen Iron lands on every unit on the board, his
/// side too but never him (Lotus, 2026-10-09), flat past Def and Res, 2 and then 2 more each of his phases, uncapped; it lands at his side's phase
/// start alone (issue 1395). The Kin heals him 2 at his phase start (issue 1395; 4 in round 502, 6 before), after the Frozen Iron. His fall in stage 2 drains the cold and leaves the shard on his tile.
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

    private static BattleState Taken(BattleState state) => Swallow.Take(state.WithUnit(Hask(state) with { Hp = 0 }), Starter, Hask(state).Id, new List<GameEvent>());

    /// <summary>Swallowed with the clock running: the late stage's held first landing already lifted.</summary>
    private static BattleState Swallowed(BattleState state) => Taken(state) with { FrozenIronHeld = false };

    [Fact]
    public void TheWardenCarriesTheSecondStagesNumbers()
    {
        var stage = Starter.Unit("hask_warden").Swallow!;

        Assert.Equal((20, 3, 3, 2), (stage.Hp, stage.Def, stage.Res, stage.Heal));
        Assert.True(stage.Rooted);
        Assert.True(stage.Late);
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
            Assert.Contains(new ShardSwallowed(hask.Id, new Coord(4, 2), 20), result.Events);
            Assert.DoesNotContain(result.Events, e => e is UnitDied d && d.UnitId == hask.Id);
            Assert.True(hask.Swallowed);
            Assert.Equal(20, hask.MaxHp(Starter));
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
        Assert.Equal(before.Keys.Where(id => id != Hask(state).Id).Order(StringComparer.Ordinal), fell.Struck.Order(StringComparer.Ordinal));
        Assert.Equal(fell.Struck.Select(id => before[id] - 2), fell.HpAfter);
        Assert.All(result.Next.Units.Where(u => !u.Swallowed), u => Assert.Equal(before[u.Id] - 2, u.Hp));
        Assert.Equal(4, result.Next.FrozenIron);
    }

    [Fact]
    public void FrozenIronClimbsByTwoWithNoCap()
    {
        var state = Swallowed(Start()) with { FrozenIron = 10 };

        var once = state.Try(new EndPhase());
        var between = once.Next.Try(new EndPhase());
        var twice = between.Next.Try(new EndPhase());

        Assert.Equal(10, once.Events.OfType<FrozenIronFell>().Single().Amount);
        Assert.Equal(Side.Enemy, twice.Next.Phase);
        Assert.Equal(12, twice.Events.OfType<FrozenIronFell>().Single().Amount);
        Assert.Equal(14, twice.Next.FrozenIron);
    }

    [Fact]
    public void FrozenIronSparesHimAloneAndHitsHisOwnSide()
    {
        var state = Swallowed(Start()) with { Phase = Side.Player, FrozenIron = 6 };
        state = state.WithUnit(Hask(state) with { Hp = 1 });
        var soldier = state.UnitsOf(Side.Enemy).Single(u => u.Id != Hask(state).Id);

        var result = state.Try(new EndPhase());

        var fell = result.Events.OfType<FrozenIronFell>().Single();
        Assert.DoesNotContain(Hask(state).Id, fell.Struck);
        Assert.Contains(soldier.Id, fell.Struck);
        Assert.True(Swallow.Spared(Hask(state)));
        Assert.False(Swallow.Spared(Hask(Start())));
        Assert.Equal(3, Hask(result.Next).Hp);
        Assert.Equal(soldier.Hp - 6, result.Next.Find(soldier.Id)!.Hp);
    }

    [Fact]
    public void ALateStageHoldsTheFirstLandingOneOfHisPhaseStarts()
    {
        var state = Taken(Start()) with { Phase = Side.Player };
        state = state.WithUnit(Hask(state) with { Hp = 18 });
        Assert.True(state.FrozenIronHeld);
        Assert.Equal(Swallow.FirstDose, state.FrozenIron);
        var before = state.Units.ToDictionary(u => u.Id, u => u.Hp);

        var held = state.Try(new EndPhase());

        Assert.Equal(Side.Enemy, held.Next.Phase);
        Assert.DoesNotContain(held.Events, e => e is FrozenIronFell);
        Assert.Contains(new KinHealed(Hask(state).Id, 2, 20), held.Events);
        Assert.All(held.Next.Units.Where(u => !u.Swallowed), u => Assert.Equal(before[u.Id], u.Hp));
        Assert.False(held.Next.FrozenIronHeld);
        Assert.Equal(Swallow.FirstDose, held.Next.FrozenIron);

        var between = held.Next.Try(new EndPhase());
        var lands = between.Next.Try(new EndPhase());

        Assert.DoesNotContain(between.Events, e => e is FrozenIronFell);
        Assert.Equal(Swallow.FirstDose, lands.Events.OfType<FrozenIronFell>().Single().Amount);
        Assert.Equal(Swallow.FirstDose + Swallow.DoseStep, lands.Next.FrozenIron);
    }

    [Fact]
    public void AStageThatIsNotLateHoldsNoLanding()
    {
        var start = Start();
        start = start.WithUnit(Hask(start) with { Kin = Hask(start).Kin! with { Late = false } });

        var state = Taken(start) with { Phase = Side.Player };
        var result = state.Try(new EndPhase());

        Assert.False(state.FrozenIronHeld);
        Assert.Equal(Swallow.FirstDose, result.Events.OfType<FrozenIronFell>().Single().Amount);
    }

    [Fact]
    public void AHeldLandingIsNotCountedInTheExposureSum()
    {
        var held = Taken(Start()) with { Phase = Side.Player };

        Assert.Equal(0, Swallow.NextLanding(held, Side.Player));
        Assert.Equal(Swallow.FirstDose, Swallow.NextLanding(held with { FrozenIronHeld = false }, Side.Player));
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
        state = state.WithUnit(Hask(state) with { Hp = 18 });

        var result = state.Try(new EndPhase());

        Assert.Equal(Side.Enemy, result.Next.Phase);
        var events = result.Events.ToList();
        Assert.True(events.FindIndex(e => e is FrozenIronFell) < events.FindIndex(e => e is KinHealed));
        Assert.Contains(new KinHealed(Hask(state).Id, 2, 20), result.Events);
        Assert.Equal(20, Hask(result.Next).Hp);
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
    public void TheExposureSumCountsTheFrozenIronStillToLand()
    {
        var state = Swallowed(Start()) with { Phase = Side.Player, FrozenIron = 6 };
        var cold = state with { FrozenIron = 0 };
        var captain = Captain(state);

        Assert.Equal(6, Swallow.NextLanding(state, Side.Player));
        Assert.Equal(Exposure.Of(cold, Starter, captain, captain.At).NoCrit + 6, Exposure.Of(state, Starter, captain, captain.At).NoCrit);
        Assert.Equal(Exposure.Of(cold, Starter, captain, captain.At).WithCrit + 6, Exposure.Of(state, Starter, captain, captain.At).WithCrit);
    }

    [Fact]
    public void NoFrozenIronIsCountedBeforeTheSwallowOrForHisOwnSide()
    {
        var unswallowed = Start() with { FrozenIron = 6 };
        var swallowed = Swallowed(Start()) with { FrozenIron = 6 };

        Assert.Equal(0, Swallow.NextLanding(unswallowed, Side.Player));
        Assert.Equal(0, Swallow.NextLanding(swallowed, Side.Enemy));
        Assert.Equal(0, Swallow.NextLanding(swallowed with { FrozenIron = 0 }, Side.Player));
    }

    [Fact]
    public void TheSimsStageReadFollowsBlowsOnTheBossInPlayerPhasesToTheEnd()
    {
        var after = Swallowed(Start());
        var hask = Hask(after);
        var captain = Captain(after);
        var stage = Ironwake.Sim.StageTwo.After(null, after, after, new GameEvent[] { new ShardSwallowed(hask.Id, hask.At, 20) });
        stage = Ironwake.Sim.StageTwo.After(stage, after, after, new GameEvent[] { new PhaseBegan(Side.Player, 5) });
        var fought = new CombatFought(captain.Id, hask.Id, 5, Side.Player, ValueList<StrikeEvent>.Empty, captain.Hp, 20);
        var answered = new CombatFought(hask.Id, captain.Id, 5, Side.Enemy, ValueList<StrikeEvent>.Empty, 20, captain.Hp);
        var hurt = after.WithUnit(hask with { Hp = 20 });
        stage = Ironwake.Sim.StageTwo.After(stage, after, hurt, new GameEvent[] { fought, answered, new PhaseBegan(Side.Enemy, 5) });

        Assert.Equal((2, 1, 1, 20, 3), (stage!.Phases, stage.PlayerPhases, stage.Blows, stage.BossHp, stage.StandingEnd));
    }

    [Fact]
    public void TheSimsStageReadCountsTheLandingsAndTheOneWhoseDoseFirstKilled()
    {
        var after = Swallowed(Start());
        var hask = Hask(after);
        var wren = after.Units.Single(u => u.Unit.Id == "wren");
        var stage = Ironwake.Sim.StageTwo.After(null, after, after, new GameEvent[] { new ShardSwallowed(hask.Id, hask.At, 20) });
        stage = Ironwake.Sim.StageTwo.After(stage, after, after, new GameEvent[] { new FrozenIronFell(2, ValueList<string>.Of(wren.Id), ValueList<int>.Of(5)) });
        Assert.Equal((1, (int?)null), (stage!.Landings, stage.FirstClockKill));

        stage = Ironwake.Sim.StageTwo.After(stage, after, after, new GameEvent[] { new FrozenIronFell(4, ValueList<string>.Of(wren.Id), ValueList<int>.Of(0)) });
        stage = Ironwake.Sim.StageTwo.After(stage, after, after, new GameEvent[] { new FrozenIronFell(6, ValueList<string>.Of(wren.Id), ValueList<int>.Of(0)) });

        Assert.Equal((3, (int?)2, 2), (stage!.Landings, stage.FirstClockKill, stage.ClockDeaths));
    }

    [Fact]
    public void TheFinaleRaceLineCountsKillsByPhaseAndDoseDeathsByLanding()
    {
        var mix = new Dictionary<string, Ironwake.Sim.ActionMix>();
        var games = new List<Ironwake.Sim.GameResult>
        {
            new(BattleResult.Won, 9, mix) { Stage = new Ironwake.Sim.StageTwo(6, 0) { PlayerPhases = 3, Landings = 3 } },
            new(BattleResult.Won, 10, mix) { Stage = new Ironwake.Sim.StageTwo(8, 1) { PlayerPhases = 4, Landings = 4, FirstClockKill = 4 } },
            new(BattleResult.Lost, 12, mix, LossCause.Timeout) { Stage = new Ironwake.Sim.StageTwo(10, 3) { PlayerPhases = 5, Landings = 5, FirstClockKill = 5 } },
            new(BattleResult.Lost, 12, mix, LossCause.Timeout) { Stage = new Ironwake.Sim.StageTwo(2, 0) { PlayerPhases = 1, Landings = 1 } },
        };

        Assert.Equal(
            "stage 2 race: won 2, player phases to the kill 3: 1, 4: 1; landings before it 3: 1, 4: 1; clock deaths 0 in 1, 1 in 1, 2+ in 0; lost 2, the dose's first kill at landing 5: 1, none: 1",
            Ironwake.Sim.FinaleRun.RaceLine(games));
        Assert.Null(Ironwake.Sim.FinaleRun.RaceLine(new List<Ironwake.Sim.GameResult> { new(BattleResult.Won, 9, mix) }));
    }

    [Theory]
    [InlineData(false, null, null, "cornered")]
    [InlineData(true, null, null, "own phase")]
    [InlineData(true, false, false, "lethal tile")]
    [InlineData(true, true, false, "read safe, crit-lethal")]
    [InlineData(true, true, true, "read safe")]
    public void TheCaptainsLastPlanNamesCorneredApartFromThePlannersFault(bool anyPass, bool? endSafe, bool? endCritSafe, string name)
    {
        Assert.Equal(name, Ironwake.Sim.CaptainPlan.Name(new Ironwake.Sim.CaptainPlan(5, anyPass, endSafe, endCritSafe), Start()));
        Assert.Equal("unread", Ironwake.Sim.CaptainPlan.Name(null, Start()));
    }

    [Fact]
    public void ACaptainTheComingFrozenIronKillsAnywhereIsCornered()
    {
        var after = Swallowed(Start()) with { FrozenIron = 4 };
        var doomed = after.WithUnit(Captain(after) with { Hp = 4 });
        var hale = after.WithUnit(Captain(after) with { Hp = Captain(after).MaxHp(Starter) });

        Assert.False(Ironwake.Sim.CaptainPlan.Read(doomed, Starter).AnyPass);
        Assert.True(Ironwake.Sim.CaptainPlan.Read(hale, Starter).AnyPass);
        Assert.False(Ironwake.Sim.CaptainPlan.Read(doomed, Starter).Ended(doomed, Starter).EndSafe);
    }

    [Fact]
    public void TheCaptainsPlanReadBeginsWithHisFirstCommandOfThePhase()
    {
        var state = Start();
        var moved = state.Try(new Move(Captain(state).Id, new Coord(3, 3))).Next;
        var waited = state.Try(new Wait(Captain(state).Id)).Next;

        Assert.True(Ironwake.Sim.CaptainPlan.Begins(state, moved));
        Assert.True(Ironwake.Sim.CaptainPlan.Begins(state, waited));
        Assert.False(Ironwake.Sim.CaptainPlan.Begins(moved, moved.Try(new Wait(Captain(moved).Id)).Next));
    }

    [Fact]
    public void TheFinaleReadSplitsThePostSwallowFallsByTheCaptainsLastPlan()
    {
        var mix = new Dictionary<string, Ironwake.Sim.ActionMix>();
        Ironwake.Sim.GameResult Fell(string killer, string? plan) => new(BattleResult.Lost, 11, mix, LossCause.Captain) { CaptainKiller = killer, CaptainFellInStageTwo = plan is not null, CaptainPlanRead = plan };
        var games = new[] { Fell("Hask", "cornered"), Fell("frozen iron", "cornered"), Fell("Hask", "cornered"), Fell("Hask", "read safe"), Fell("Hexer", null) };

        Assert.Equal("after the swallow, by his last plan: cornered 3 (Hask 2, frozen iron 1); read safe 1 (Hask 1)", Ironwake.Sim.FinaleRun.PlanLine(games));
        Assert.Null(Ironwake.Sim.FinaleRun.PlanLine(new[] { Fell("Hexer", null) }));
    }

    [Fact]
    public void TheFinaleReadSplitsThePreSwallowFallsByWhereTheHuntStood()
    {
        var mix = new Dictionary<string, Ironwake.Sim.ActionMix>();
        Ironwake.Sim.GameResult Fell(string killer, string front, bool line = false, bool after = false) =>
            new(BattleResult.Lost, 6, mix, LossCause.Captain) { CaptainKiller = killer, CaptainFront = after ? null : front, CaptainFellInStageTwo = after, CaptainByLine = line };
        var games = new[] { Fell("Sworn Hunter", "hunted, alone"), Fell("Hask", "not hunted, behind the fronts", line: true), Fell("Hask", "hunted, alone"), Fell("Hask", "", after: true) };

        Assert.Equal("before the swallow, by the hunt: hunted, alone 2 (Hask 1, Sworn Hunter 1); not hunted, behind the fronts 1 (Hask 1); down a line 1", Ironwake.Sim.FinaleRun.FrontLine(games));
        Assert.Null(Ironwake.Sim.FinaleRun.FrontLine(new[] { Fell("Hask", "", after: true) }));
    }

    [Fact]
    public void TheStageReadSplitsDamageOnHimByWhetherHeReachesTheStrikersTile()
    {
        var after = Swallowed(Start());
        var hask = Hask(after);
        var wren = after.Units.Single(u => u.Unit.Id == "wren");

        Assert.True(Ironwake.Sim.StageTwo.Reaches(after, Starter, hask, new Coord(3, 2)));
        Assert.True(Ironwake.Sim.StageTwo.Reaches(after, Starter, hask, new Coord(4, 4)));
        Assert.False(Ironwake.Sim.StageTwo.Reaches(after, Starter, hask, new Coord(5, 3)));
        Assert.False(Ironwake.Sim.StageTwo.Reaches(after, Starter, hask, new Coord(6, 4)));

        var stage = Ironwake.Sim.StageTwo.After(null, after, after, new GameEvent[] { new ShardSwallowed(hask.Id, hask.At, 20) });
        var diagonal = after.WithUnit(wren with { At = new Coord(5, 3) });
        var struck = diagonal.WithUnit(Hask(diagonal) with { Hp = 16 }).WithUnit(wren with { At = new Coord(5, 3), Acted = true, Moved = true });
        stage = Ironwake.Sim.StageTwo.After(stage, diagonal, struck, Array.Empty<GameEvent>(), Starter);
        var beside = struck.WithUnit(Captain(struck) with { At = new Coord(3, 2) });
        var hit = beside.WithUnit(Hask(beside) with { Hp = 9 }).WithUnit(Captain(beside) with { Acted = true, Moved = true });
        stage = Ironwake.Sim.StageTwo.After(stage, beside, hit, Array.Empty<GameEvent>(), Starter);

        Assert.Equal((7, 4), (stage!.DamageInReach, stage.DamageBeyond));
        Assert.Equal((0, 0), (Ironwake.Sim.StageTwo.After(stage with { DamageInReach = 0, DamageBeyond = 0 }, beside, hit, Array.Empty<GameEvent>())!.DamageInReach, 0));
    }

    [Fact]
    public void TheStageOneReadSplitsTheCompanysActionsByWhetherTheyStruckHim()
    {
        var state = Start();
        var hask = Hask(state);
        var captain = Captain(state);
        var wren = state.Units.Single(u => u.Unit.Id == "wren");
        var soldier = state.Units.Single(u => u.Unit.ClassId != "iron_warden" && u.Side == Side.Enemy);

        var stage = Ironwake.Sim.StageOne.After(null, state, state, new GameEvent[] { new PhaseBegan(Side.Player, 1) }, Starter);
        Assert.Equal((hask.Id, 1, 1, 3), (stage!.Boss, stage.Arrived, stage.PlayerPhases, stage.UnitPhases));

        stage = Ironwake.Sim.StageOne.After(stage, state, state, new GameEvent[] { new CombatFought(captain.Id, hask.Id, 1, Side.Player, ValueList<StrikeEvent>.Empty, 20, 30) }, Starter);
        stage = Ironwake.Sim.StageOne.After(stage, state, state, new GameEvent[] { new CombatFought(wren.Id, soldier.Id, 1, Side.Player, ValueList<StrikeEvent>.Empty, 20, 10) }, Starter);
        stage = Ironwake.Sim.StageOne.After(stage, state, state, new GameEvent[] { new AreaCastAt(wren.Id, "spark_storm", hask.At, ValueList<string>.Of(hask.Id, soldier.Id), 2) }, Starter);
        stage = Ironwake.Sim.StageOne.After(stage, state, state, new GameEvent[] { new CombatFought(hask.Id, captain.Id, 1, Side.Enemy, ValueList<StrikeEvent>.Empty, 30, 15) }, Starter);

        Assert.Equal((2, 1, 1), (stage!.OnHim, stage.OnOthers, stage.FirstBlowTurn));
        Assert.Null(stage.SwallowTurn);
    }

    [Fact]
    public void TheStageOneReadStopsAtTheSwallow()
    {
        var state = Start();
        var hask = Hask(state);
        var captain = Captain(state);
        var after = Swallowed(state);

        var stage = Ironwake.Sim.StageOne.After(null, state, state, Array.Empty<GameEvent>(), Starter);
        stage = Ironwake.Sim.StageOne.After(stage, state, after, new GameEvent[] { new CombatFought(captain.Id, hask.Id, 1, Side.Player, ValueList<StrikeEvent>.Empty, 20, 0), new ShardSwallowed(hask.Id, hask.At, 20) }, Starter);
        var held = Ironwake.Sim.StageOne.After(stage, after, after, new GameEvent[] { new PhaseBegan(Side.Player, 2), new CombatFought(captain.Id, hask.Id, 2, Side.Player, ValueList<StrikeEvent>.Empty, 20, 10) }, Starter);

        Assert.Equal((1, 1, 0), (stage!.SwallowTurn, stage.OnHim, stage.BossHp));
        Assert.Equal(stage, held);
        Assert.Null(Ironwake.Sim.StageOne.After(null, after, after, Array.Empty<GameEvent>(), Starter));
    }

    [Fact]
    public void TheFinalePaceReadNamesTheSwallowTurnAndTheStageOneStall()
    {
        var mix = new Dictionary<string, Ironwake.Sim.ActionMix>();
        var swallowed = new Ironwake.Sim.GameResult(BattleResult.Won, 10, mix)
        {
            Stage = new Ironwake.Sim.StageTwo(3, 0),
            StageOne = new Ironwake.Sim.StageOne("h", 3) { SwallowTurn = 7, FirstBlowTurn = 4, PlayerPhases = 5, UnitPhases = 50, OnHim = 20, OnOthers = 10, BossMaxHp = 44 },
        };
        var stalled = new Ironwake.Sim.GameResult(BattleResult.Lost, 12, mix, LossCause.Timeout)
        {
            StageOne = new Ironwake.Sim.StageOne("h", 3) { FirstBlowTurn = 8, PlayerPhases = 10, UnitPhases = 50, OnHim = 3, OnOthers = 12, BossHp = 12, BossMaxHp = 44, StandingEnd = 2 },
        };
        var games = new[] { swallowed, stalled };

        Assert.Equal("stage 1: on the board median turn 3 in 2 of 2; swallowed in 1, median turn 7 (earliest 7, latest 7); his first blow taken median turn 4 in 2; unit-phases 100: on him 23 (23 %), on the others 22 (22 %), the rest 55 (55 %)", Ironwake.Sim.FinaleRun.PaceLine(games));
        Assert.Equal("stage 1 timeouts: 1; at the limit, median boss HP 12 of 44, 2 standing; median actions a player phase on him 0.3, on the others 1.2, over 10 player phases; unit-phases 50: on him 3 (6 %), on the others 12 (24 %), the rest 35 (70 %)", Ironwake.Sim.FinaleRun.PaceStallLine(games));
        Assert.Null(Ironwake.Sim.FinaleRun.PaceStallLine(new[] { swallowed }));
        Assert.Null(Ironwake.Sim.FinaleRun.PaceLine(new[] { new Ironwake.Sim.GameResult(BattleResult.Won, 5, mix) }));
    }

    [Fact]
    public void TheFinaleReadNamesTheStallAndWhatKilledTheCaptain()
    {
        var mix = new Dictionary<string, Ironwake.Sim.ActionMix>();
        var stalled = new Ironwake.Sim.GameResult(BattleResult.Lost, 12, mix, LossCause.Timeout) { Stage = new Ironwake.Sim.StageTwo(4, 0) { PlayerPhases = 2, Blows = 3, BossHp = 10, StandingEnd = 4 } };
        var struck = new Ironwake.Sim.GameResult(BattleResult.Lost, 11, mix, LossCause.Captain) { Stage = new Ironwake.Sim.StageTwo(3, 0), CaptainKiller = "Hask", CaptainFellInStageTwo = true };
        var early = new Ironwake.Sim.GameResult(BattleResult.Lost, 6, mix, LossCause.Captain) { CaptainKiller = "Hexer" };
        var games = new[] { stalled, struck, early };

        Assert.Equal("stage 2 timeouts: 1; at the limit, median boss HP 10, 4 standing; median blows on him a player phase 1.5 over 2 player phases", Ironwake.Sim.FinaleRun.StallLine(games));
        Assert.Equal("captain falls: 1 before the swallow (Hexer 1), 1 after (Hask 1)", Ironwake.Sim.FinaleRun.CaptainLine(games));
        Assert.Null(Ironwake.Sim.FinaleRun.StallLine(new[] { struck, early }));
        Assert.Null(Ironwake.Sim.FinaleRun.CaptainLine(new[] { stalled }));
    }

    [Fact]
    public void TheCaptainsKillerIsTheOtherSideOfTheCombatOrTheFrozenIron()
    {
        var state = Swallowed(Start());
        var hask = Hask(state);
        var captain = Captain(state);

        Assert.Equal("Hask", Ironwake.Sim.Gates.CaptainKillerOf(state, new GameEvent[] { new CombatFought(hask.Id, captain.Id, 3, Side.Enemy, ValueList<StrikeEvent>.Empty, 20, 0) }));
        Assert.Equal("Hask (countering)", Ironwake.Sim.Gates.CaptainKillerOf(state, new GameEvent[] { new CombatFought(captain.Id, hask.Id, 3, Side.Player, ValueList<StrikeEvent>.Empty, 0, 20) }));
        Assert.Equal("frozen iron", Ironwake.Sim.Gates.CaptainKillerOf(state, new GameEvent[] { new FrozenIronFell(4, ValueList<string>.Of(captain.Id), ValueList<int>.Of(0)) }));
        Assert.Null(Ironwake.Sim.Gates.CaptainKillerOf(state, new GameEvent[] { new CombatFought(hask.Id, captain.Id, 3, Side.Enemy, ValueList<StrikeEvent>.Empty, 20, 1) }));
    }

    [Fact]
    public void TheSimsStageReadRecordsTheCompanyTheSwallowFound()
    {
        var before = Start();
        var after = Swallowed(before);
        after = after.WithUnit(Captain(after) with { Hp = 9 });

        var stage = Ironwake.Sim.StageTwo.After(null, before, after, new GameEvent[] { new ShardSwallowed(Hask(after).Id, Hask(after).At, 40) });

        Assert.Equal(new Ironwake.Sim.StageTwo(0, 0, 3, 9) { Boss = Hask(after).Id, BossHp = 20, StandingEnd = 3 }, stage);
    }

    [Fact]
    public void HisFallInStageTwoDrainsTheColdAndLeavesTheShardOnHisTile()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var state = Swallowed(Start(seed));
            state = state.WithUnit(Hask(state) with { Hp = 1 });

            var result = state.Try(new Attack(Captain(state).Id, Hask(state).Id));

            var events = result.Events.ToList();
            var died = events.FindIndex(e => e is UnitDied d && d.UnitId == Hask(state).Id);
            if (died >= 0)
            {
                Assert.Equal(new ColdDrained(Hask(state).Id, new Coord(4, 2)), events[died + 1]);
                Assert.Equal(BattleResult.Won, result.Next.Outcome.Result);
                return;
            }
        }

        Assert.Fail("no seed landed the killing hit");
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
    public void ARootedStageHoldsTheTileHeSwallowedOn()
    {
        var start = Start();
        var roaming = start.WithUnit(Hask(start) with { Behavior = Behavior.Aggressive });
        var rooted = Swallowed(roaming);
        var unrooted = Swallowed(roaming.WithUnit(Hask(roaming) with { Kin = Hask(roaming).Kin! with { Rooted = false } }));

        Assert.Equal(Behavior.Hold, Hask(rooted).Behavior);
        Assert.Equal(Behavior.Aggressive, Hask(unrooted).Behavior);
        var far = rooted.WithUnit(Captain(rooted) with { At = new Coord(0, 4) }) with { Phase = Side.Enemy };
        Assert.DoesNotContain(EnemyAi.Plan(far, Starter), c => c is Move m && m.UnitId == Hask(far).Id);
        var chased = unrooted.WithUnit(Captain(unrooted) with { At = new Coord(0, 4) }) with { Phase = Side.Enemy };
        Assert.Contains(EnemyAi.Plan(chased, Starter), c => c is Move m && m.UnitId == Hask(chased).Id);
    }

    [Fact]
    public void ARaceStageEndsNoMapAtTheTurnLimitWhileHeStandsSwallowed()
    {
        var past = Swallowed(Start()) with { Turn = 11 };
        var walking = past.WithUnit(Hask(past) with { Kin = Hask(past).Kin! with { Race = false } });

        Assert.True(Hask(past).Kin!.Race);
        Assert.True(past.Racing);
        Assert.False(past.Outcome.IsOver);
        Assert.False(walking.Racing);
        Assert.Equal(LossCause.Timeout, walking.Outcome.Cause);
    }

    [Fact]
    public void TheTurnLimitStillEndsTheMapBeforeTheSwallow()
    {
        var past = Start() with { Turn = 11 };

        Assert.False(past.Racing);
        Assert.Equal(LossCause.Timeout, past.Outcome.Cause);
    }

    [Fact]
    public void ARacingBoardRunsItsPhasesPastTheLimit()
    {
        var last = Swallowed(Start()) with { Turn = 10, Phase = Side.Enemy };

        var result = last.Try(new EndPhase());

        Assert.Null(result.Rejection);
        Assert.Contains(new PhaseBegan(Side.Player, 11), result.Events);
        Assert.False(result.Next.Outcome.IsOver);
        Assert.Contains("turn 11, past the limit: the race", MapRenderer.Render(result.Next, Starter));
        var his = result.Next.Try(new EndPhase());
        Assert.Contains(new PhaseBegan(Side.Enemy, 11), his.Events);
        Assert.Contains(his.Events, e => e is FrozenIronFell);
    }

    [Fact]
    public void TheCardSaysTheRaceHasNoTurnLimit()
    {
        var state = Start();
        Assert.Contains(Ironwake.Cli.PlaySession.ShowLines(state, Starter, Hask(state)), l => l.Contains("from then the turn limit ends nothing"));
        var swallowed = Swallowed(state);
        Assert.Contains(Ironwake.Cli.PlaySession.ShowLines(swallowed, Starter, Hask(swallowed)), l => l.Contains("no turn limit while he stands"));
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
        Assert.False(read.FrozenIronHeld);
        Assert.True(Ironwake.Content.Protocol.ProtocolJson.ReadState(Ironwake.Content.Protocol.ProtocolJson.State(Taken(Start()), Starter), Starter).FrozenIronHeld);
    }

    [Fact]
    public void TheCardSaysWhatTheStageDoes()
    {
        var state = Start();
        Assert.Contains(Ironwake.Cli.PlaySession.ShowLines(state, Starter, Hask(state)), l => l.Contains("At 0 HP he swallows the shard") && l.Contains("phase starts from the second"));
        var taken = Taken(state);
        Assert.Contains(Ironwake.Cli.PlaySession.ShowLines(taken, Starter, Hask(taken)), l => l.Contains("at his side's phase start after next"));

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
            "\"inventory\": [ { \"item\": \"iron_sword\" } ], \"swallow\": { \"hp\": 40, \"def\": 3, \"res\": 3, \"heal\": 6, \"late\": true, \"race\": true } }");
        var content = ContentLoader.Parse(Ironwake.Core.Tests.Content.Fixture.Files(units: units));

        Assert.Equal(new KinStage(40, 3, 3, 6, Late: true, Race: true), content.Unit("recruit").Swallow);
        Assert.Equal(content, ContentLoader.Parse(ContentSerializer.Write(content)));
    }
}
