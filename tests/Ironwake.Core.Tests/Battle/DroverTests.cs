using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Content.Protocol;
using ScriptedRng = Ironwake.Core.Tests.Combat.ScriptedRng;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Rook's Drover (issue 872, DECISIONS/0217): the class never doubles, striking or countering; the drake bites
/// once after an adjacent exchange in which one of her strikes hit and both stand, for 3 while Half-grown and
/// 5 from Grown, as added damage, never a strike; from Grown a carry leaves her a Canto; at Unbroken her
/// breath's ice holds one more round.
/// </summary>
public class DroverTests
{
    private static readonly Terrain Plain = Starter.TerrainById("plain");

    // Rook: Str 8 (+1 in either class), Dex 40, Spd 12 (+3), Lck 4. Iron Lance hit 70 + 40 + 2 = 112, clamped to 100;
    // damage 9 + 6 - 2 = 13 on the mark. Attack speed 15 against the mark's 3, so the Sky Captain doubles.
    private static readonly Unit Rook = Recruit("rook", "drover", new Stats(24, 8, 0, 40, 12, 4, 5, 3, 3), "iron_lance");

    // The mark: Lck 30 so no crit lands on it; Def 2; its iron sword hits Rook 32 raw, so a roll of 50 misses.
    private static readonly Unit Mark = Recruit("mark", "cadet", new Stats(30, 6, 0, 3, 3, 30, 2, 0, 1), "iron_sword");

    private static Unit Riding(DrakeStage? stage, string classId = "drover") =>
        Rook with { ClassId = classId, Drake = stage is { } s ? new DrakeState(s, 0) : null };

    private static Combatant Fighter(Unit unit, int? hp = null) =>
        Starter.CombatantOf(unit, Starter.Weapon(unit.Inventory.Items[0].ItemId), Plain, hp ?? Starter.StatsOf(unit).Hp);

    private static CombatForecast Forecast(Unit attacker, Unit defender, int defenderHp = 30) =>
        Core.Combat.Forecast(Fighter(attacker), Fighter(defender, defenderHp), 1, RollScheme.TwoRollAverage);

    private static CombatResult Fight(Unit attacker, Unit defender, int defenderHp = 30, int roll = 50) =>
        CombatResolver.Resolve(Fighter(attacker), Fighter(defender, defenderHp), 1, new CombatContext(1, Side.Player), new ScriptedRng(roll), RollScheme.TwoRollAverage);

    [Fact]
    public void TheDroverNeverDoublesWhereTheSkyCaptainWould()
    {
        var drover = Forecast(Riding(DrakeStage.Grown), Mark);
        var captain = Forecast(Riding(DrakeStage.Grown, "skycaptain"), Mark);

        Assert.False(drover.Attacker.Doubles);
        Assert.True(drover.Attacker.NeverDoubles);
        Assert.True(captain.Attacker.Doubles);
        Assert.False(captain.Attacker.NeverDoubles);
    }

    [Fact]
    public void TheDroverNeverDoublesOnHerCounterEither()
    {
        Assert.False(Forecast(Mark, Riding(DrakeStage.Grown), 24).Defender.Doubles);
        Assert.True(Forecast(Mark, Riding(DrakeStage.Grown, "skycaptain"), 24).Defender.Doubles);
    }

    [Fact]
    public void TheBiteIsThreeHalfGrownAndFiveFromGrown()
    {
        Assert.Equal(3, Forecast(Riding(DrakeStage.HalfGrown), Mark).Attacker.Bite);
        Assert.Equal(5, Forecast(Riding(DrakeStage.Grown), Mark).Attacker.Bite);
        Assert.Equal(5, Forecast(Riding(DrakeStage.Unbroken), Mark).Attacker.Bite);
        Assert.Equal(0, Forecast(Riding(null), Mark).Attacker.Bite);
        Assert.Equal(0, Forecast(Riding(DrakeStage.Unbroken, "skycaptain"), Mark).Attacker.Bite);
    }

    [Fact]
    public void ThereIsNoBiteAtRangeTwo()
    {
        Assert.Equal(5, Core.Combat.Bite(Fighter(Riding(DrakeStage.Grown)), 1));
        Assert.Equal(0, Core.Combat.Bite(Fighter(Riding(DrakeStage.Grown)), 2));
    }

    [Fact]
    public void TheBiteLandsAfterAHitWithBothStandingAndIsNotAStrike()
    {
        var result = Fight(Riding(DrakeStage.Grown), Mark);

        Assert.Single(result.Strikes, s => s.AttackerId == "rook");
        Assert.All(result.Strikes, s => Assert.NotEqual(5, s.Damage));
        Assert.Equal(new BiteEvent("rook", "mark", 5, 30 - 13 - 5), result.Bite);
        Assert.Equal(12, result.DefenderHp);
    }

    [Fact]
    public void ThereIsNoBiteWhenEveryStrikeMisses()
    {
        var result = Fight(Riding(DrakeStage.Grown) with { Stats = Rook.Stats with { Dex = 0 } }, Mark, roll: 99);

        Assert.DoesNotContain(result.Strikes, s => s.Hit);
        Assert.Null(result.Bite);
        Assert.Equal(30, result.DefenderHp);
    }

    [Fact]
    public void ThereIsNoBiteWhenTheLanceKills()
    {
        var result = Fight(Riding(DrakeStage.Grown), Mark, defenderHp: 13);

        Assert.True(result.DefenderDied);
        Assert.Null(result.Bite);
    }

    [Fact]
    public void ABiteThatKillsIsTheCombatsKill()
    {
        var result = Fight(Riding(DrakeStage.Grown), Mark, defenderHp: 16);

        Assert.Equal(3, result.Strikes.Single(s => s.AttackerId == "rook").TargetHpAfter);
        Assert.Equal(new BiteEvent("rook", "mark", 5, 0), result.Bite);
        Assert.True(result.DefenderDied);
    }

    [Fact]
    public void TheDrakeBitesOnHerCountersToo()
    {
        var result = CombatResolver.Resolve(Fighter(Mark), Fighter(Riding(DrakeStage.HalfGrown)), 1, new CombatContext(1, Side.Enemy), new ScriptedRng(50), RollScheme.TwoRollAverage);

        Assert.Equal(new BiteEvent("rook", "mark", 3, 30 - 13 - 3), result.Bite);
        Assert.Equal(14, result.AttackerHp);
    }

    [Fact]
    public void TheForecastsLethalReadingsCountTheBite()
    {
        Assert.Equal(13 + 5, Forecast(Riding(DrakeStage.Grown), Mark).AttackerDamageLivedFor(24));
        Assert.Equal(13 + 3, Forecast(Mark, Riding(DrakeStage.HalfGrown), 24).CounterIfAllLand);
        Assert.True(Forecast(Mark with { Stats = Mark.Stats with { Hp = 16 } }, Riding(DrakeStage.Grown), 24).CounterIsLethal(16, 24));
        Assert.Equal(13, Forecast(Mark, Riding(DrakeStage.Grown, "skycaptain"), 24).CounterIfAllLand / 2);
    }

    [Fact]
    public void TheForecastLineSaysNeverDoublesAndTheBite()
    {
        var board = OnBoard(DrakeStage.Grown);
        var line = PlaySession.ForecastLine(board.Find("rook")!, board.Find("brigand-1")!, Forecast(Riding(DrakeStage.Grown), Mark));

        Assert.Contains("dmg 13 x1, never doubles, crit 0%; drake bites 5 if a strike hits and both stand, no roll", line);
        Assert.Contains("\"bite\":5", ProtocolJson.Forecast(Forecast(Riding(DrakeStage.Grown), Mark)));
        Assert.Contains("\"neverDoubles\":true", ProtocolJson.Forecast(Forecast(Riding(DrakeStage.Grown), Mark)));
        Assert.DoesNotContain("bite", ProtocolJson.Forecast(Forecast(Riding(DrakeStage.Grown, "skycaptain"), Mark)));
    }

    private const string Yard = """
        name: Yard
        size: 6x3
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ......
        ......
        ......

        units:
        P captain 0,0
        P recruit:rook 2,1
        E brigand 3,1 group:near behavior:hold
        """;

    private static BattleState OnBoard(DrakeStage stage) =>
        BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Riding(stage) with { Stats = Rook.Stats with { Dex = 10, Lck = 0 } }), Yard);

    [Fact]
    public void OnTheBoardTheCombatCarriesTheBiteAndTheLogPrintsIt()
    {
        var result = OnBoard(DrakeStage.Grown).Try(new Attack("rook", "brigand-1"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var fought = Assert.Single(result.Events.OfType<CombatFought>());
        var bite = Assert.IsType<BiteEvent>(fought.Bite);
        Assert.Equal(("rook", "brigand-1", 5), (bite.RiderId, bite.TargetId, bite.Damage));
        Assert.Equal(bite.TargetHpAfter, fought.TargetHpAfter);
        Assert.Equal(bite.TargetHpAfter, result.Next.Find("brigand-1")!.Hp);
        Assert.Contains("The drake bites: 5 (Brigand hp ", PlaySession.Describe(fought, Starter, UnitNames.Of(result.Next, Starter)));
        Assert.Contains("\"bite\":{\"rider\":\"rook\"", ProtocolJson.Event(fought));
    }

    [Fact]
    public void AGroundedDroverKeepsTheBite()
    {
        var state = OnBoard(DrakeStage.Grown);
        var grounded = state.WithUnit(state.Find("rook")! with { Grounded = 2 });
        var rook = grounded.Find("rook")!;
        var brigand = grounded.Find("brigand-1")!;

        Assert.Equal(5, Core.Combat.Forecast(rook.ToCombatant(grounded, Starter, against: brigand), brigand.Answering(grounded, Starter, rook.At, rook), 1, grounded.Scheme).Attacker.Bite);
    }

    private const string River = """
        name: River
        size: 8x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {0}

        ...~~...
        ...~~...
        ...~~...
        ...~~...
        ...~~...

        units:
        P captain 0,0
        P recruit:rook {1}
        P recruit:wren {2}
        E brigand 7,0 group:far behavior:guard
        """;

    private static BattleState OnRiver(string header, string classId, string rookAt = "1,2", string wrenAt = "1,3") =>
        BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Rook with { ClassId = classId }, Wren), string.Format(River, header, rookAt, wrenAt));

    [Fact]
    public void TheLongCarryLeavesTheDroverACantoOfWhatTheFlightLeft()
    {
        var state = OnRiver("carry: free rook", "drover");
        var mov = state.ReachOf(state.Find("rook")!, Starter).Mov;

        var rook = state.Do(new Carry("rook", "wren", new Coord(5, 2), new Coord(5, 3))).Find("rook")!;

        Assert.Equal(mov - 4, rook.Canto);
        Assert.Null(OnRiver("carry: free rook", "skycaptain").Do(new Carry("rook", "wren", new Coord(5, 2), new Coord(5, 3))).Find("rook")!.Canto);
    }

    [Fact]
    public void AfterTheLongCarryTheDroverMovesAgain()
    {
        var state = OnRiver("carry: free rook", "drover").Do(new Carry("rook", "wren", new Coord(5, 2), new Coord(5, 3)));

        var result = state.Try(new Canto("rook", new Coord(6, 2)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(new Coord(6, 2), result.Next.Find("rook")!.At);
    }

    [Fact]
    public void DeepRimeHoldsTheIceOneMoreRound()
    {
        var state = OnRiver("breath: rook", "drover", "2,2", "2,3").Do(new Breathe("rook", new Coord(3, 2)));
        Assert.Equal(1, state.Rime[0].Extra);

        // A plain breath thaws as the player's next phase ends; deep rime holds through one more round.
        state = state.Do(new EndPhase()).Do(new EndPhase()).Do(new EndPhase());
        Assert.Equal("rime", state.Map.TerrainAt(new Coord(3, 2), Starter).Id);
        Assert.Equal(0, state.Rime[0].Extra);

        state = state.Do(new EndPhase()).Do(new EndPhase());
        Assert.Equal("water", state.Map.TerrainAt(new Coord(3, 2), Starter).Id);
        Assert.Empty(state.Rime);
    }

    [Fact]
    public void TheSkyCaptainsBreathThawsOnTheUsualClock()
    {
        var state = OnRiver("breath: rook", "skycaptain", "2,2", "2,3").Do(new Breathe("rook", new Coord(3, 2)));

        Assert.Equal(0, state.Rime[0].Extra);
        Assert.Equal("water", state.Do(new EndPhase()).Do(new EndPhase()).Do(new EndPhase()).Map.TerrainAt(new Coord(3, 2), Starter).Id);
    }

    [Fact]
    public void TheBoardLineSaysTheDeepRimeRound()
    {
        var state = OnRiver("breath: rook", "drover", "2,2", "2,3").Do(new Breathe("rook", new Coord(3, 2)));

        Assert.Equal("rime: 3,2 4,2 (thaws as the next player phase ends, then holds 1 more round (deep rime))", Rime.Line(state));
    }

    [Fact]
    public void DeepRimeSurvivesTheProtocolRoundTrip()
    {
        var state = OnRiver("breath: rook", "drover", "2,2", "2,3").Do(new Breathe("rook", new Coord(3, 2)));

        var read = ProtocolJson.ReadState(ProtocolJson.State(state, Starter), Starter);

        Assert.Equal(state.Rime, read.Rime);
    }

    [Fact]
    public void TheCardNamesWhatIsLiveAtTheDrakesStage()
    {
        Assert.Equal("Drake: half-grown. Live: Drake Bite 3.", Drake.Card(Riding(DrakeStage.HalfGrown), Starter with { Campaign = Starter.Campaign with { Drake = null } }));
        Assert.EndsWith("Live: Drake Bite 5, Long Carry, Deep Rime.", Drake.Card(Riding(DrakeStage.Unbroken), Starter));
        Assert.Equal("Drake: grown.", Drake.Card(Riding(DrakeStage.Grown, "skycaptain"), Starter));
    }

    [Fact]
    public void TheClassListNamesEachAbilitysStage()
    {
        Assert.Equal(
            "the drake: Drake Bite (half-grown 3, grown 5; never doubles, live); Long Carry (from grown, live); Deep Rime (at unbroken, not yet)",
            Drake.ClassLine(Starter.Class("drover"), Starter, Riding(DrakeStage.Grown)));
        Assert.Equal(
            "the drake: Drake Bite (half-grown 3, grown 5; never doubles); Long Carry (from grown); Deep Rime (at unbroken)",
            Drake.ClassLine(Starter.Class("drover"), Starter, null));
        Assert.Null(Drake.ClassLine(Starter.Class("skycaptain"), Starter, Riding(DrakeStage.Grown)));
    }

    [Theory]
    [InlineData("\"kind\": \"bite\", \"halfGrown\": 0, \"grown\": 5", "effect.halfGrown", "must be at least 1")]
    [InlineData("\"kind\": \"bite\", \"halfGrown\": 3, \"grown\": 0", "effect.grown", "must be at least 1")]
    [InlineData("\"kind\": \"bite\", \"halfGrown\": 5, \"grown\": 3", "effect.grown", "a bite never shrinks")]
    [InlineData("\"kind\": \"bite\", \"halfGrown\": 3, \"grown\": 5, \"crit\": 5", "effect.crit", "is not read here")]
    [InlineData("\"kind\": \"deep_rime\", \"rounds\": 0", "effect.rounds", "must be at least 1")]
    [InlineData("\"kind\": \"long_carry\", \"rounds\": 1", "effect.rounds", "is not read here")]
    public void AMalformedDrakeAbilityIsRefusedAtLoad(string fields, string field, string why)
    {
        var abilities = Fixture.Abilities.Replace("\n] }", ",\n{ \"id\": \"gap\", \"name\": \"Gap\", \"text\": \"A test line.\", \"effect\": { " + fields + " } }\n] }");
        Assert.Contains("\"gap\"", abilities);

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(abilities: abilities)));
        Assert.Contains("gap", error.Message);
        Assert.Contains(field, error.Message);
        Assert.Contains(why, error.Message);
    }

    [Fact]
    public void TheDrakeAbilitiesRoundTripThroughTheSerializer()
    {
        var reloaded = ContentLoader.Parse(ContentSerializer.Write(Starter));

        Assert.Equal(new BiteEffect(3, 5), reloaded.Ability("drake_bite").Effect);
        Assert.IsType<LongCarryEffect>(reloaded.Ability("long_carry").Effect);
        Assert.Equal(new DeepRimeEffect(1), reloaded.Ability("deep_rime").Effect);
        Assert.True(reloaded.Class("drover").SingleStrike);
        Assert.False(reloaded.Class("skycaptain").SingleStrike);
    }

    [Fact]
    public void TheMeasureReadsEveryTemplateAtEachLevelAndStage()
    {
        var reading = Ironwake.Sim.DroverMeasure.Read(Starter, 10, DrakeStage.Grown, RollScheme.TwoRollAverage);

        Assert.Equal(SignatureCeiling.Targets(Starter).Count, reading.Rows.Count);
        Assert.All(reading.Rows, r => Assert.True(r.CaptainPlayer > 0 || r.DroverPlayer > 0));
        Assert.Equal(0, reading.Rows.Single(r => r.Template == "archer").DroverEnemy);
        Assert.Equal(Ironwake.Sim.DroverMeasure.Levels.Length * Ironwake.Sim.DroverMeasure.Stages.Length * (5 + reading.Rows.Count) + 1, Ironwake.Sim.DroverMeasure.Lines(Starter, RollScheme.TwoRollAverage).Count);
    }

    [Theory]
    [InlineData(10, 10, 30, true)]
    [InlineData(9, 21, 30, false)]
    [InlineData(21, 9, 30, false)]
    public void TheMeasuresBarWantsAThirdBehindAndAThirdAhead(int behind, int ahead, int templates, bool passes)
    {
        Assert.Equal(passes, Ironwake.Sim.DroverReading.Passes(behind, ahead, templates));
    }

    [Fact]
    public void TheMeasuresExpectationAddsTheBiteOnTheChanceAnyStrikeLands()
    {
        var side = new SideForecast(true, 10, 50, 50, 0, false, Bite: 5);

        var p = Core.Combat.HitProbability(50, RollScheme.TwoRollAverage);
        Assert.Equal(10 * p + 5 * p, Ironwake.Sim.DroverMeasure.Expected(side, RollScheme.TwoRollAverage), 6);
        Assert.Equal(0, Ironwake.Sim.DroverMeasure.Expected(SideForecast.None, RollScheme.TwoRollAverage));
    }
}
