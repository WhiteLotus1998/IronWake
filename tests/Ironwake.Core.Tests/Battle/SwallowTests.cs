using Ironwake.Content;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Hask's second stage (issue 1385, Lotus's rework, Table round 487; numbers provisional on #1247), on the pitched
/// Iron Warden (<c>hask_warden</c>) alone. Stage 1's bar reaching 0 is the swallow: he stays on his tile on a fresh
/// bar of 20 with Def and Res +3 (issue 1395; 24 in round 502, 40 before), and from his next phase start Frozen Iron lands on every unit on the board, his
/// side too but never him (Lotus, 2026-10-09), flat past Def and Res, 0 and then 3 more each of his phases (issue 1395, round 518; 2 and 2 before), uncapped; it lands at his side's phase
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

    /// <summary>The Warden's first Frozen Iron landing and its step (Table round 518).</summary>
    private static int Dose => Starter.Unit("hask_warden").Swallow!.Dose;

    private static int Step => Starter.Unit("hask_warden").Swallow!.Step;

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
        Assert.Equal((0, 3), (stage.Dose, stage.Step));
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
            Assert.Equal(Dose, result.Next.FrozenIron);
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
        var state = Swallowed(Start()) with { Phase = Side.Player, FrozenIron = 2 };
        var before = state.Units.ToDictionary(u => u.Id, u => u.Hp);

        var result = state.Try(new EndPhase());

        var fell = result.Events.OfType<FrozenIronFell>().Single();
        Assert.Equal(2, fell.Amount);
        Assert.Equal(before.Keys.Where(id => id != Hask(state).Id).Order(StringComparer.Ordinal), fell.Struck.Order(StringComparer.Ordinal));
        Assert.Equal(fell.Struck.Select(id => before[id] - 2), fell.HpAfter);
        Assert.All(result.Next.Units.Where(u => !u.Swallowed), u => Assert.Equal(before[u.Id] - 2, u.Hp));
        Assert.Equal(2 + Step, result.Next.FrozenIron);
    }

    [Fact]
    public void FrozenIronClimbsByItsStepWithNoCap()
    {
        var state = Swallowed(Start()) with { FrozenIron = 10 };

        var once = state.Try(new EndPhase());
        var between = once.Next.Try(new EndPhase());
        var twice = between.Next.Try(new EndPhase());

        Assert.Equal(10, once.Events.OfType<FrozenIronFell>().Single().Amount);
        Assert.Equal(Side.Enemy, twice.Next.Phase);
        Assert.Equal(10 + Step, twice.Events.OfType<FrozenIronFell>().Single().Amount);
        Assert.Equal(10 + 2 * Step, twice.Next.FrozenIron);
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
        Assert.True(Swallow.Spared(state, Hask(state), Starter));
        Assert.False(Swallow.Spared(Start(), Hask(Start()), Starter));
        Assert.Equal(3, Hask(result.Next).Hp);
        Assert.Equal(soldier.Hp - 6, result.Next.Find(soldier.Id)!.Hp);
    }

    [Fact]
    public void ALateStageHoldsTheFirstLandingOneOfHisPhaseStarts()
    {
        var state = Taken(Start()) with { Phase = Side.Player };
        state = state.WithUnit(Hask(state) with { Hp = 18 });
        Assert.True(state.FrozenIronHeld);
        Assert.Equal(Dose, state.FrozenIron);
        var before = state.Units.ToDictionary(u => u.Id, u => u.Hp);

        var held = state.Try(new EndPhase());

        Assert.Equal(Side.Enemy, held.Next.Phase);
        Assert.DoesNotContain(held.Events, e => e is FrozenIronFell);
        Assert.Contains(new KinHealed(Hask(state).Id, 2, 20), held.Events);
        Assert.All(held.Next.Units.Where(u => !u.Swallowed), u => Assert.Equal(before[u.Id], u.Hp));
        Assert.False(held.Next.FrozenIronHeld);
        Assert.Equal(Dose, held.Next.FrozenIron);

        var between = held.Next.Try(new EndPhase());
        var lands = between.Next.Try(new EndPhase());

        Assert.DoesNotContain(between.Events, e => e is FrozenIronFell);
        Assert.Equal(Dose, lands.Events.OfType<FrozenIronFell>().Single().Amount);
        Assert.Equal(Dose + Step, lands.Next.FrozenIron);
    }

    [Fact]
    public void AStageThatIsNotLateHoldsNoLanding()
    {
        var start = Start();
        start = start.WithUnit(Hask(start) with { Kin = Hask(start).Kin! with { Late = false } });

        var state = Taken(start) with { Phase = Side.Player };
        var result = state.Try(new EndPhase());

        Assert.False(state.FrozenIronHeld);
        Assert.Equal(Dose, result.Events.OfType<FrozenIronFell>().Single().Amount);
    }

    [Fact]
    public void TheStagesDoseAndStepSetTheClimbAndADoseOfZeroStillLands()
    {
        var start = Start();
        start = start.WithUnit(Hask(start) with { Kin = Hask(start).Kin! with { Late = false, Dose = 0, Step = 3 } });
        var state = Taken(start) with { Phase = Side.Player };
        var before = state.Units.ToDictionary(u => u.Id, u => u.Hp);
        Assert.Equal(0, state.FrozenIron);

        var first = state.Try(new EndPhase());

        Assert.Equal(0, first.Events.OfType<FrozenIronFell>().Single().Amount);
        Assert.All(first.Next.Units.Where(u => !u.Swallowed), u => Assert.Equal(before[u.Id], u.Hp));
        Assert.Equal(3, first.Next.FrozenIron);

        var second = first.Next.Try(new EndPhase()).Next.Try(new EndPhase());

        Assert.Equal(3, second.Events.OfType<FrozenIronFell>().Single().Amount);
        Assert.Equal(6, second.Next.FrozenIron);
    }

    [Fact]
    public void AHeldDoseOfZeroRoundTripsThroughTheProtocol()
    {
        var start = Start();
        start = start.WithUnit(Hask(start) with { Kin = Hask(start).Kin! with { Dose = 0, Step = 4 } });
        var taken = Taken(start);

        var read = Ironwake.Content.Protocol.ProtocolJson.ReadState(Ironwake.Content.Protocol.ProtocolJson.State(taken, Starter), Starter);

        Assert.Equal((0, true), (read.FrozenIron, read.FrozenIronHeld));
        Assert.Equal(Hask(taken).Kin, Hask(read).Kin);
    }

    [Fact]
    public void AHeldLandingIsNotCountedInTheExposureSum()
    {
        var held = Taken(Start()) with { Phase = Side.Player };

        Assert.Equal(0, Swallow.NextLanding(held, Side.Player));
        Assert.Equal(Dose, Swallow.NextLanding(held with { FrozenIronHeld = false }, Side.Player));
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
        var state = Swallowed(Start()) with { FrozenIron = 2 };
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
            "stage 2 race: won 2, player phases to the kill 3: 1, 4: 1; landings before it 3: 1, 4: 1; clock deaths 0 in 1, 1 in 1, 2+ in 0 (gate: 2+ in at most 10 %: ok); lost 2, the dose's first kill at landing 5: 1, none: 1",
            Ironwake.Sim.FinaleRun.RaceLine(games));
        Assert.Null(Ironwake.Sim.FinaleRun.RaceLine(new List<Ironwake.Sim.GameResult> { new(BattleResult.Won, 9, mix) }));
    }

    [Fact]
    public void TheClockDeathGateFailsPastATenthOfWonGamesWithTwoDoseDeaths()
    {
        var mix = new Dictionary<string, Ironwake.Sim.ActionMix>();
        var games = Enumerable.Range(0, 9).Select(_ => new Ironwake.Sim.GameResult(BattleResult.Won, 9, mix) { Stage = new Ironwake.Sim.StageTwo(6, 0) { PlayerPhases = 3, Landings = 3 } }).ToList();
        games.Add(new(BattleResult.Won, 9, mix) { Stage = new Ironwake.Sim.StageTwo(6, 2) { PlayerPhases = 3, Landings = 3 } });

        Assert.Contains("(gate: 2+ in at most 10 %: ok)", Ironwake.Sim.FinaleRun.RaceLine(games));
        games.Add(new(BattleResult.Won, 9, mix) { Stage = new Ironwake.Sim.StageTwo(6, 2) { PlayerPhases = 3, Landings = 3 } });
        Assert.Contains("(gate: 2+ in at most 10 %: FAILED)", Ironwake.Sim.FinaleRun.RaceLine(games));
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
    public void TheStageReadPlacesTheCompanyAtTheSwallowAndKeepsTheDamageByUnit()
    {
        var after = Swallowed(Start());
        var hask = Hask(after);
        var wren = after.Units.Single(u => u.Unit.Id == "wren");
        var captain = Captain(after);

        var stage = Ironwake.Sim.StageTwo.After(null, after, after, new GameEvent[] { new ShardSwallowed(hask.Id, hask.At, 20) }, Starter);
        Assert.Equal(new Ironwake.Sim.StageTwo.Placed(captain.At.DistanceTo(hask.At), true), stage!.AtSwallow[captain.Id]);
        Assert.Equal(after.UnitsOf(Side.Player).Count(), stage.AtSwallow.Count);

        var diagonal = after.WithUnit(wren with { At = new Coord(5, 3) });
        var struck = diagonal.WithUnit(Hask(diagonal) with { Hp = 16 }).WithUnit(wren with { At = new Coord(5, 3), Acted = true, Moved = true });
        stage = Ironwake.Sim.StageTwo.After(stage, diagonal, struck, Array.Empty<GameEvent>(), Starter);
        var beside = struck.WithUnit(Captain(struck) with { At = new Coord(3, 2) });
        var hit = beside.WithUnit(Hask(beside) with { Hp = 9 }).WithUnit(Captain(beside) with { Acted = true, Moved = true });
        stage = Ironwake.Sim.StageTwo.After(stage, beside, hit, Array.Empty<GameEvent>(), Starter);

        Assert.Equal(new Ironwake.Sim.StageTwo.Dealt(0, 4), stage!.DamageBy[wren.Id]);
        Assert.Equal(new Ironwake.Sim.StageTwo.Dealt(7, 0), stage.DamageBy[captain.Id]);
        Assert.Empty(Ironwake.Sim.StageTwo.After(null, after, after, new GameEvent[] { new ShardSwallowed(hask.Id, hask.At, 20) })!.AtSwallow);
    }

    [Fact]
    public void TheCaptainReadOpensEachStageTwoPhaseWithHisOffersAndFilesWhereHeEnded()
    {
        var after = Swallowed(Start());
        var hask = Hask(after);
        var captain = Captain(after);
        var measured = after.WithUnit(captain with { Unit = captain.Unit with { Abilities = ValueList<string>.Of("full_measure") } });
        var stage = Ironwake.Sim.StageTwo.After(null, measured, measured, new GameEvent[] { new ShardSwallowed(hask.Id, hask.At, 20) }, Starter);
        stage = Ironwake.Sim.StageTwo.After(stage, measured with { Phase = Side.Enemy }, measured, new GameEvent[] { new PhaseBegan(Side.Player, 5) }, Starter);

        Assert.Equal(2, stage!.Captain.Phases);
        Assert.Equal((2, 2), (stage.Captain.Offer, stage.Captain.ArtOffer));

        var waited = measured.WithUnit(Captain(measured) with { Acted = true });
        stage = Ironwake.Sim.StageTwo.After(stage, measured, waited, Array.Empty<GameEvent>(), Starter);
        Assert.Equal(1, stage!.Captain.EndedInReach["stood"]);
        Assert.Equal(0, stage.Captain.EndedClear);

        var unread = Ironwake.Sim.StageTwo.After(stage, measured, waited, Array.Empty<GameEvent>(), Starter);
        Assert.Equal(1, unread!.Captain.EndedInReach["stood"]);
    }

    [Fact]
    public void TheCaptainReadFilesAStrikeOnHimAndAMarkCarriedThroughTheSwallow()
    {
        var start = Start();
        var marked = start.WithUnit(Hask(start) with { Mark = MagicSchool.Lightning });
        var after = Swallowed(marked);
        var hask = Hask(after);
        var captain = Captain(after);
        var stage = Ironwake.Sim.StageTwo.After(null, after, after, new GameEvent[] { new ShardSwallowed(hask.Id, hask.At, 20) }, Starter);
        Assert.Equal(1, stage!.Captain.MarkedAtSwallow);

        var struck = after.WithUnit(captain with { Acted = true, Moved = true });
        var fought = new CombatFought(captain.Id, hask.Id, 5, Side.Player, ValueList<StrikeEvent>.Empty, captain.Hp, hask.Hp);
        stage = Ironwake.Sim.StageTwo.After(stage, after, struck, new GameEvent[] { fought, new MarkCashed(hask.Id, captain.Id, MagicSchool.Lightning) }, Starter);

        Assert.Equal((1, 0, 1), (stage!.Captain.StruckHim, stage.Captain.StruckHimArt, stage.Captain.MarksCashed));
        Assert.Equal(1, stage.Captain.EndedInReach["struck him"]);
    }

    [Fact]
    public void TheLeaderLineSumsTheCaptainReadOverTheGamesThatReachedTheStage()
    {
        var mix = new Dictionary<string, Ironwake.Sim.ActionMix>();
        var read = new Ironwake.Sim.StageTwo.CaptainRead
        {
            Phases = 3, Offer = 2, PlainLethal = 1, ArtOffer = 2, ArtLethal = 1, ArtLethalHit = System.Collections.Immutable.ImmutableList.Create(99),
            StruckHim = 1, EndedInReach = System.Collections.Immutable.ImmutableDictionary<string, int>.Empty.Add("moved", 2), EndedClear = 1, MarkedAtSwallow = 1,
        };
        var lost = new Ironwake.Sim.GameResult(BattleResult.Lost, 12, mix, LossCause.Captain) { Stage = new Ironwake.Sim.StageTwo(4, 1) { Captain = read } };
        Assert.Equal(
            "stage 2 captain: player phases 3 (resting 0); a strike on him on offer in 2, lethal if all land in 1; an art on offer in 2, lethal on its hit in 1 (from a tile the veto allows 0, median best hit 99 %; in 1 of the 1 games lost in the stage); he struck him in 1 (with an art 0), another in 0; ended in his reach 2 (after moved 2), clear 1; marks on him: at the swallow in 1 of 1, laid in the stage 0, cashed 0",
            Ironwake.Sim.FinaleRun.LeaderLine(new[] { lost }));
        Assert.Null(Ironwake.Sim.FinaleRun.LeaderLine(new[] { new Ironwake.Sim.GameResult(BattleResult.Won, 5, mix) }));
    }

    [Fact]
    public void TheStageOneReadSplitsAnUnarmedHealersFallsByHerLastPhaseAndWhoStruckHer()
    {
        var start = Start();
        var ivo = start.Units.Single(u => u.Unit.Id == "ivo");
        var state = start.WithUnit(ivo with { Unit = Recruit("ivo", "chaplain", new Stats(19, 0, 6, 5, 7, 4, 4, 6, 4), "salve") });
        var healer = state.Find(ivo.Id)!;
        var soldier = state.Units.Single(u => u.Unit.ClassId != "iron_warden" && u.Side == Side.Enemy);
        Assert.True(Ironwake.Sim.HeuristicPlayer.Healer(Starter, healer));

        var stage = Ironwake.Sim.StageOne.After(null, state, state, new GameEvent[] { new PhaseBegan(Side.Player, 1) }, Starter);
        stage = Ironwake.Sim.StageOne.After(stage, state, state, Array.Empty<GameEvent>(), Starter, new Move(healer.Id, healer.At));
        stage = Ironwake.Sim.StageOne.After(stage, state, state, Array.Empty<GameEvent>(), Starter, new EndPhase());
        var enemy = state with { Phase = Side.Enemy };
        var fell = enemy.WithoutUnit(healer.Id);
        stage = Ironwake.Sim.StageOne.After(stage, enemy, fell, new GameEvent[] { new CombatFought(soldier.Id, healer.Id, 1, Side.Enemy, ValueList<StrikeEvent>.Empty, 20, 0) }, Starter);
        stage = Ironwake.Sim.StageOne.After(stage, fell, fell, Array.Empty<GameEvent>(), Starter);

        Assert.Equal(1, stage!.HealerFalls);
        Assert.Equal(1, stage.HealerFallsBy["walk, on the board"]);

        var waited = Ironwake.Sim.StageOne.After(null, state, state, new GameEvent[] { new PhaseBegan(Side.Player, 1) }, Starter);
        waited = Ironwake.Sim.StageOne.After(waited, state, state, Array.Empty<GameEvent>(), Starter, new EndPhase());
        waited = Ironwake.Sim.StageOne.After(waited, enemy, fell, new GameEvent[] { new CombatFought("stranger", healer.Id, 1, Side.Enemy, ValueList<StrikeEvent>.Empty, 20, 0) }, Starter);
        Assert.Equal(1, waited!.HealerFallsBy["wait, arrived after"]);
    }

    [Fact]
    public void TheStageOneCaptainFallIsSplitByTheVetoOnHisEndTileThenByACritOrAnArrival()
    {
        var state = Start();
        var captain = Captain(state);
        var hask = Hask(state);
        var passed = Exposure.Of(state, Starter, captain, captain.At).NoCrit < captain.Hp;
        var verdict = passed ? "passed" : Ironwake.Sim.CaptainPlan.Read(state, Starter).AnyPass ? "failed" : "cornered";
        var closed = Ironwake.Sim.StageOne.After(null, state, state, new GameEvent[] { new PhaseBegan(Side.Player, 1) }, Starter);
        closed = Ironwake.Sim.StageOne.After(closed, state, state, Array.Empty<GameEvent>(), Starter, new EndPhase());
        Assert.Null(closed!.CaptainFall);

        var enemy = state with { Phase = Side.Enemy };
        var fell = enemy.WithUnit(captain with { Hp = 0 });
        var crit = ValueList<StrikeEvent>.Of(new StrikeEvent(0, hask.Id, captain.Id, true, true, 40, 0));
        var critted = Ironwake.Sim.StageOne.After(closed, enemy, fell, new GameEvent[] { new CombatFought(hask.Id, captain.Id, 1, Side.Enemy, crit, hask.Hp, 0) }, Starter);
        Assert.Equal($"{verdict}, crit", critted!.CaptainFall);
        Assert.Equal(new[] { "captain" }, critted.Fallen);

        var arrived = Ironwake.Sim.StageOne.After(closed, enemy, fell, new GameEvent[] { new CombatFought("stranger", captain.Id, 1, Side.Enemy, ValueList<StrikeEvent>.Empty, 20, 0) }, Starter);
        Assert.Equal($"{verdict}, arrival", arrived!.CaptainFall);

        var mine = Ironwake.Sim.StageOne.After(closed, state, state.WithUnit(captain with { Hp = 0 }), new GameEvent[] { new CombatFought(captain.Id, hask.Id, 1, Side.Player, ValueList<StrikeEvent>.Empty, 0, hask.Hp) }, Starter);
        Assert.Equal("his own phase", mine!.CaptainFall);
    }

    [Fact]
    public void TheVetoSplitAndFirstFallLinesCountTheStageOneFalls()
    {
        var mix = new Dictionary<string, Ironwake.Sim.ActionMix>();
        Ironwake.Sim.GameResult Lost(string? fall, params string[] fallen) =>
            new(BattleResult.Lost, 12, mix, LossCause.Timeout) { StageOne = new Ironwake.Sim.StageOne("h", 3) { CaptainFall = fall, Fallen = System.Collections.Immutable.ImmutableList.CreateRange(fallen) } };
        var games = new[]
        {
            Lost("passed, crit", "healer", "captain"),
            Lost("failed, plain", "other", "captain"),
            Lost("cornered, line", "captain"),
            Lost(null, "healer"),
            Lost(null),
        };
        Assert.Equal(
            "stage 1 captain falls 3, by the veto on his end tile: passed 1 (plain 0, crit 1, line 0, arrival 0); cornered 1 (plain 0, crit 0, line 1, arrival 0); failed 1 (plain 1, crit 0, line 0, arrival 0); unread 0 (plain 0, crit 0, line 0, arrival 0); his own phase 0",
            Ironwake.Sim.FinaleRun.VetoSplitLine(games));
        Assert.Equal(
            "stage 1 first fall, in the 5 games lost before the swallow: an unarmed healer 2, the captain 1, another 1, none 1; the captain's stage-1 falls after a healer's in the same game 1 of 3",
            Ironwake.Sim.FinaleRun.FirstFallLine(games));
        Assert.Null(Ironwake.Sim.FinaleRun.VetoSplitLine(new[] { Lost(null) }));
        Assert.Null(Ironwake.Sim.FinaleRun.FirstFallLine(new[] { new Ironwake.Sim.GameResult(BattleResult.Won, 5, mix) }));
    }

    [Fact]
    public void TheStageOneBaitReadCountsHisOwnPhaseStrikesByHowFarOffHisStartTile()
    {
        var state = Start();
        var hask = Hask(state);
        var captain = Captain(state);
        var stage = Ironwake.Sim.StageOne.After(null, state, state, Array.Empty<GameEvent>(), Starter);
        Assert.Equal(hask.At, stage!.Home);

        var enemy = state with { Phase = Side.Enemy };
        var stepped = enemy.WithUnit(hask with { At = new Coord(hask.At.X + 2, hask.At.Y) });
        var lance = new CombatFought(hask.Id, captain.Id, 3, Side.Enemy, ValueList<StrikeEvent>.Empty, hask.Hp, captain.Hp);
        var line = new LineStruck(hask.Id, new Coord(hask.At.X + 2, hask.At.Y), ValueList<Coord>.Empty, ValueList<string>.Of(captain.Id));
        var missed = new LineStruck(hask.Id, hask.At, ValueList<Coord>.Empty, ValueList<string>.Empty);
        var countered = new CombatFought(captain.Id, hask.Id, 3, Side.Enemy, ValueList<StrikeEvent>.Empty, captain.Hp, hask.Hp);
        stage = Ironwake.Sim.StageOne.After(stage, enemy, stepped, new GameEvent[] { lance, line, missed, countered }, Starter);

        Assert.Equal(new[] { ("lance", 2), ("line", 2) }, stage!.BossStrikes);

        var mix = new Dictionary<string, Ironwake.Sim.ActionMix>();
        var lost = new Ironwake.Sim.GameResult(BattleResult.Lost, 12, mix, LossCause.Timeout) { StageOne = stage };
        var quiet = new Ironwake.Sim.GameResult(BattleResult.Won, 9, mix) { StageOne = new Ironwake.Sim.StageOne("h", 3) { SwallowTurn = 7 } };
        Assert.Equal(
            "stage 1 bait: every game struck in 1 of 2, strikes 2 (lance 1, line 1), tiles off his start 2: 2; lost before the swallow struck in 1 of 1, strikes 2 (lance 1, line 1), tiles off his start 2: 2",
            Ironwake.Sim.FinaleRun.BaitLine(new[] { lost, quiet }));
        Assert.Null(Ironwake.Sim.FinaleRun.BaitLine(new[] { new Ironwake.Sim.GameResult(BattleResult.Won, 5, mix) }));
    }

    [Fact]
    public void TheHealerFallLineSumsTheSplitAndTheShapeLineNamesWhoDealtTheStageTwoDamage()
    {
        var mix = new Dictionary<string, Ironwake.Sim.ActionMix>();
        var falls = System.Collections.Immutable.ImmutableDictionary<string, int>.Empty.Add("heal, on the board", 3).Add("wait, arrived after", 1).Add("walk, on the board", 1);
        var fallen = new Ironwake.Sim.GameResult(BattleResult.Lost, 12, mix, LossCause.Timeout) { StageOne = new Ironwake.Sim.StageOne("h", 3) { HealerFalls = 5, HealerFallsBy = falls } };
        Assert.Equal(
            "unarmed healer falls in stage 1 5, by her last closed phase: after a heal 3, after a walk 1, after a wait 1, in her own phase 0; struck by a unit on the board as it closed 4, by one that arrived after 1, unread 0 (heal, on the board 3, wait, arrived after 1, walk, on the board 1)",
            Ironwake.Sim.FinaleRun.HealerFallLine(new[] { fallen }));
        Assert.Null(Ironwake.Sim.FinaleRun.HealerFallLine(new[] { new Ironwake.Sim.GameResult(BattleResult.Won, 5, mix) }));

        var placed = System.Collections.Immutable.ImmutableDictionary<string, Ironwake.Sim.StageTwo.Placed>.Empty
            .Add("captain", new Ironwake.Sim.StageTwo.Placed(1, true)).Add("pell", new Ironwake.Sim.StageTwo.Placed(3, false)).Add("maud", new Ironwake.Sim.StageTwo.Placed(5, false));
        var dealt = System.Collections.Immutable.ImmutableDictionary<string, Ironwake.Sim.StageTwo.Dealt>.Empty
            .Add("pell", new Ironwake.Sim.StageTwo.Dealt(4, 10)).Add("captain", new Ironwake.Sim.StageTwo.Dealt(6, 0));
        var reached = new Ironwake.Sim.GameResult(BattleResult.Won, 9, mix) { Stage = new Ironwake.Sim.StageTwo(4, 0) { AtSwallow = placed, DamageBy = dealt } };
        Assert.Equal(
            "stage 2 shape: at the swallow, median distance to him 3, farthest 5, on tiles he reaches 1; by unit: pell 3 tiles in 1, dealt 4 in reach 10 beyond, captain 1 tiles in 1, dealt 6 in reach 0 beyond, maud 5 tiles in 1, dealt 0 in reach 0 beyond",
            Ironwake.Sim.FinaleRun.ShapeLine(new[] { reached }));
        Assert.Null(Ironwake.Sim.FinaleRun.ShapeLine(new[] { new Ironwake.Sim.GameResult(BattleResult.Won, 5, mix) }));
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
    public void TheStageOneReadSplitsThePhasesThatStruckNoOne()
    {
        var state = Start();
        var hask = Hask(state);
        var captain = Captain(state);
        var wren = state.Units.Single(u => u.Unit.Id == "wren");
        var ivo = state.Units.Single(u => u.Unit.Id == "ivo");
        var far = state.WithUnit(wren with { At = new Coord(0, 4), Moved = true }).WithUnit(ivo with { Moved = true });

        var stage = Ironwake.Sim.StageOne.After(null, state, state, new GameEvent[] { new PhaseBegan(Side.Player, 1) }, Starter);
        stage = Ironwake.Sim.StageOne.After(stage, state, state, new GameEvent[] { new CombatFought(captain.Id, hask.Id, 1, Side.Player, ValueList<StrikeEvent>.Empty, 20, 30) }, Starter, new Attack(captain.Id, hask.Id));
        stage = Ironwake.Sim.StageOne.After(stage, far, far, Array.Empty<GameEvent>(), Starter, new UseItem(wren.Id, 0, wren.Id));
        stage = Ironwake.Sim.StageOne.After(stage, far, far, Array.Empty<GameEvent>(), Starter, new Wait(ivo.Id));
        stage = Ironwake.Sim.StageOne.After(stage, far, far, Array.Empty<GameEvent>(), Starter, new EndPhase());
        Assert.Equal((1, 0, 0, 1), (stage!.Healed, stage.Refused, stage.MovedOnly, stage.Idle));

        var done = far.WithUnit(ivo with { Acted = true });
        stage = Ironwake.Sim.StageOne.After(stage, state, state, new GameEvent[] { new PhaseBegan(Side.Player, 2) }, Starter);
        stage = Ironwake.Sim.StageOne.After(stage, state, state, Array.Empty<GameEvent>(), Starter, new Wait(captain.Id));
        stage = Ironwake.Sim.StageOne.After(stage, far, far, Array.Empty<GameEvent>(), Starter, new Move(wren.Id, new Coord(0, 4)));
        stage = Ironwake.Sim.StageOne.After(stage, done, done, Array.Empty<GameEvent>(), Starter, new EndPhase());

        Assert.Equal((1, 1, 1, 2), (stage!.Healed, stage.Refused, stage.MovedOnly, stage.Idle));
        Assert.Equal(6, stage.UnitPhases);
    }

    [Fact]
    public void TheStageOneReadSplitsTheIdleByWhatTheUnitSaw()
    {
        var state = Start();
        var hask = Hask(state);
        var captain = Captain(state);
        var wren = state.Units.Single(u => u.Unit.Id == "wren");
        var ivo = state.Units.Single(u => u.Unit.Id == "ivo");
        var held = state.WithUnit(ivo with { Moved = true });
        var gone = held.WithoutUnit(wren.Id);
        var distance = state.UnitsOf(Side.Enemy).Min(e => ivo.At.DistanceTo(e.At));

        var stage = Ironwake.Sim.StageOne.After(null, state, state, new GameEvent[] { new PhaseBegan(Side.Player, 1) }, Starter);
        stage = Ironwake.Sim.StageOne.After(stage, state, state, new GameEvent[] { new CombatFought(captain.Id, hask.Id, 1, Side.Player, ValueList<StrikeEvent>.Empty, 20, 30) }, Starter, new Attack(captain.Id, hask.Id));
        stage = Ironwake.Sim.StageOne.After(stage, held, held, Array.Empty<GameEvent>(), Starter, new Wait(ivo.Id));
        stage = Ironwake.Sim.StageOne.After(stage, gone, gone, Array.Empty<GameEvent>(), Starter, new EndPhase());

        Assert.Equal((2, 1, 0, 1, 1), (stage!.Idle, stage.IdleFell, stage.IdleUnseen, stage.IdleHeld, stage.IdleUnread));
        var ivoHeld = stage.HeldBy[ivo.Id];
        Assert.Equal(new[] { distance }, ivoHeld.Distances);
        Assert.Equal(0, ivoHeld.CouldClose);
    }

    [Fact]
    public void ALookNamesTheNearestEnemySeenAndWhetherTheUnitCouldClose()
    {
        var state = Start();
        var captain = Captain(state);
        var ivo = state.Units.Single(u => u.Unit.Id == "ivo");
        var empty = state.UnitsOf(Side.Enemy).Aggregate(state, (s, e) => s.WithoutUnit(e.Id));

        Assert.Equal("captain", Ironwake.Sim.StageOne.LookAt(state, Starter, captain).Kind);
        Assert.False(Ironwake.Sim.StageOne.LookAt(state, Starter, ivo with { Moved = true }).CanClose);
        Assert.Equal(state.UnitsOf(Side.Enemy).Min(e => ivo.At.DistanceTo(e.At)), Ironwake.Sim.StageOne.LookAt(state, Starter, ivo).Nearest);
        var blind = Ironwake.Sim.StageOne.LookAt(empty, Starter, ivo);
        Assert.Null(blind.Nearest);
        Assert.False(blind.Offer);
    }

    [Fact]
    public void TheIdleLineSplitsTheIdleAndNamesTheHeldByUnit()
    {
        var mix = new Dictionary<string, Ironwake.Sim.ActionMix>();
        var held = System.Collections.Immutable.ImmutableDictionary<string, Ironwake.Sim.StageOne.Held>.Empty
            .Add("tamsin", new Ironwake.Sim.StageOne.Held("healer", System.Collections.Immutable.ImmutableList.Create(13, 12, 14), 3))
            .Add("pell", new Ironwake.Sim.StageOne.Held("armed", System.Collections.Immutable.ImmutableList.Create(8), 0));
        var game = new Ironwake.Sim.GameResult(BattleResult.Lost, 12, mix, LossCause.Timeout)
        {
            StageOne = new Ironwake.Sim.StageOne("h", 3) { Idle = 6, IdleFell = 1, IdleUnseen = 1, IdleHeld = 4, IdleUnread = 2, HealerFalls = 1, HeldBy = held },
        };

        Assert.Equal("stage 1 idle 6: fell before acting 1, no enemy seen 1, an enemy seen out of reach 4 (could close 3; an unarmed healer's 3, the walk finding no safe tile that closes); unread 2; unarmed healers fallen in stage 1 1; out of reach by unit: tamsin 3 (healer, median 13 tiles, could close 3), pell 1 (armed, median 8 tiles, could close 0)", Ironwake.Sim.FinaleRun.IdleLine(new[] { game }));
        Assert.Null(Ironwake.Sim.FinaleRun.IdleLine(new[] { new Ironwake.Sim.GameResult(BattleResult.Won, 5, mix) }));
    }

    [Fact]
    public void AStrikeIsOnOfferOnlyToAUnitThatCanStillReachAnEnemyItSees()
    {
        var state = Start();
        var captain = Captain(state);
        var ivo = state.Units.Single(u => u.Unit.Id == "ivo");

        Assert.True(Ironwake.Sim.StageOne.StrikeOnOffer(state, Starter, captain));
        Assert.False(Ironwake.Sim.StageOne.StrikeOnOffer(state, Starter, captain with { Acted = true }));
        Assert.False(Ironwake.Sim.StageOne.StrikeOnOffer(state, Starter, ivo with { Moved = true }));
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
            StageOne = new Ironwake.Sim.StageOne("h", 3) { SwallowTurn = 7, FirstBlowTurn = 4, PlayerPhases = 5, UnitPhases = 50, OnHim = 20, OnOthers = 10, BossMaxHp = 44, Healed = 4, Refused = 10, MovedOnly = 6, Idle = 5 },
        };
        var stalled = new Ironwake.Sim.GameResult(BattleResult.Lost, 12, mix, LossCause.Timeout)
        {
            StageOne = new Ironwake.Sim.StageOne("h", 3) { FirstBlowTurn = 8, PlayerPhases = 10, UnitPhases = 50, OnHim = 3, OnOthers = 12, BossHp = 12, BossMaxHp = 44, StandingEnd = 2, Refused = 20, MovedOnly = 10, Idle = 5 },
        };
        var games = new[] { swallowed, stalled };

        Assert.Equal("stage 1: on the board median turn 3 in 2 of 2; swallowed in 1, median turn 7 (earliest 7, latest 7); his first blow taken median turn 4 in 2; unit-phases 100: on him 23 (23 %), on the others 22 (22 %), the rest 55 (55 %: an item 4 (4 %), refused 30 (30 %), move only 16 (16 %), idle 10 (10 %))", Ironwake.Sim.FinaleRun.PaceLine(games));
        Assert.Equal("stage 1 timeouts: 1; at the limit, median boss HP 12 of 44, 2 standing; median actions a player phase on him 0.3, on the others 1.2, over 10 player phases; unit-phases 50: on him 3 (6 %), on the others 12 (24 %), the rest 35 (70 %: an item 0 (0 %), refused 20 (40 %), move only 10 (20 %), idle 5 (10 %))", Ironwake.Sim.FinaleRun.PaceStallLine(games));
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
        var state = Swallowed(Start()) with { FrozenIron = 2 };
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
        Assert.Contains("turn 11 (the limit ends nothing now)", MapRenderer.Render(result.Next, Starter));
        var his = result.Next.Try(new EndPhase());
        Assert.Contains(new PhaseBegan(Side.Enemy, 11), his.Events);
        Assert.Contains(his.Events, e => e is FrozenIronFell);
    }

    [Fact]
    public void TheStatusLineSaysTheLimitEndsNothingFromTheSwallowOn()
    {
        var state = Start() with { Turn = 7 };

        Assert.Contains("turn 7 of 10", MapRenderer.Render(state, Starter));
        var swallowed = Swallowed(state);
        Assert.Contains("turn 7 (the limit ends nothing now)", MapRenderer.Render(swallowed, Starter));
        Assert.DoesNotContain("of 10", MapRenderer.Render(swallowed, Starter));
    }

    [Fact]
    public void TheSwallowKeepsEveryStatusOnHimSoAMarkRidesOntoTheFreshBar()
    {
        var state = Start();
        var marked = state.WithUnit(Hask(state) with { Mark = MagicSchool.Lightning });

        Assert.Equal(MagicSchool.Lightning, Hask(Taken(marked)).Mark);
        Assert.True(Hask(Taken(marked)).Swallowed);
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
        Assert.Contains(lines, l => l.Contains("Frozen Iron lands for 0, 3 more each time"));
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
    public void ANegativeDoseIsRefusedNamingTheField()
    {
        var units = Ironwake.Core.Tests.Content.Fixture.Units.Replace(
            "\"inventory\": [ { \"item\": \"iron_sword\" } ] }",
            "\"inventory\": [ { \"item\": \"iron_sword\" } ], \"swallow\": { \"hp\": 20, \"def\": 3, \"res\": 3, \"heal\": 2, \"dose\": -1 } }");

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Ironwake.Core.Tests.Content.Fixture.Files(units: units)));

        Assert.Equal(("recruit", "swallow.dose"), (e.Entry, e.Field));
    }

    [Fact]
    public void ASwallowRoundTripsThroughTheContentWriter()
    {
        var units = Ironwake.Core.Tests.Content.Fixture.Units.Replace(
            "\"inventory\": [ { \"item\": \"iron_sword\" } ] }",
            "\"inventory\": [ { \"item\": \"iron_sword\" } ], \"swallow\": { \"hp\": 40, \"def\": 3, \"res\": 3, \"heal\": 6, \"late\": true, \"race\": true, \"dose\": 0, \"step\": 3 } }");
        var content = ContentLoader.Parse(Ironwake.Core.Tests.Content.Fixture.Files(units: units));

        Assert.Equal(new KinStage(40, 3, 3, 6, Late: true, Race: true, Dose: 0, Step: 3), content.Unit("recruit").Swallow);
        Assert.Equal(content, ContentLoader.Parse(ContentSerializer.Write(content)));
    }
}
