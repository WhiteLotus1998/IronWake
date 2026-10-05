using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Content.Protocol;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The Sky Captain's passives (issue 1127, DECISIONS/0262). Drake frost, Rook's alone: a Move she flew that lands
/// beside enemies strikes each for 1, never below 1, and holds each but a boss to Mov 1 with no Canto through its
/// next phase; then it rests a turn. Stoop, every other Sky Captain's: after flying 4 or more tiles, the first
/// strike deals 2 more when it hits.
/// </summary>
public class SkyCaptainTests
{
    private static readonly Unit Rook = Recruit("rook", "skycaptain", new Stats(24, 8, 0, 40, 12, 4, 5, 3, 3), "iron_lance");

    private static Unit Riding(DrakeStage? stage, string classId = "skycaptain") =>
        Rook with { ClassId = classId, Drake = stage is { } s ? new DrakeState(s, 0) : null };

    private const string Field = """
        name: Field
        size: 9x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        .........
        .........
        .........
        .........
        .........

        units:
        P captain 0,0
        P recruit:rook 0,2
        P recruit:wren {0}
        E brigand 5,2 group:near behavior:hold
        E brigand 5,4 group:near behavior:hold
        {1}
        """;

    private static BattleState OnField(Unit rook, string wrenAt = "0,4", string extra = "") =>
        BattleFixture.Start(7, ValueList<Unit>.Of(Hale, rook, Wren), string.Format(Field, wrenAt, extra));

    private static (BattleState Next, IReadOnlyList<GameEvent> Events) Moved(BattleState state, string unit, int x, int y)
    {
        var result = state.Try(new Move(unit, new Coord(x, y)));
        Assert.True(result.Accepted, result.Rejection?.Message);
        return (result.Next, result.Events);
    }

    [Fact]
    public void TheFrostFiresOnAFlownLandingBesideAnEnemy()
    {
        var state = OnField(Riding(DrakeStage.Grown));
        var hp = state.Find("brigand-1")!.Hp;

        var (next, events) = Moved(state, "rook", 4, 2);

        var frosted = Assert.Single(events.OfType<UnitFrosted>());
        Assert.Equal(new UnitFrosted("brigand-1", "rook", 1, hp - 1, true, false, Side.Enemy), frosted);
        Assert.Equal(hp - 1, next.Find("brigand-1")!.Hp);
        Assert.Equal(1, next.Find("brigand-1")!.Frosted);
        Assert.Equal(1, next.Find("rook")!.FrostTurn);
        Assert.Equal(0, next.Find("brigand-2")!.Frosted);
    }

    [Fact]
    public void TheFrostStrikesEveryEnemyOnTheFourTilesAroundTheLanding()
    {
        var (next, events) = Moved(OnField(Riding(DrakeStage.Grown)), "rook", 5, 3);

        Assert.Equal(new[] { "brigand-1", "brigand-2" }, events.OfType<UnitFrosted>().Select(f => f.UnitId));
        Assert.All(new[] { "brigand-1", "brigand-2" }, id => Assert.Equal(1, next.Find(id)!.Frosted));
    }

    [Fact]
    public void ALandingBesideNoEnemyNeitherFiresNorSpendsTheFrost()
    {
        var (next, events) = Moved(OnField(Riding(DrakeStage.Grown)), "rook", 2, 2);

        Assert.Empty(events.OfType<UnitFrosted>());
        Assert.Null(next.Find("rook")!.FrostTurn);
        Assert.True(DrakeFrost.Ready(next, Starter, next.Find("rook")!));
    }

    [Fact]
    public void StandingStillNeverFiresTheFrost()
    {
        var (next, _) = Moved(OnField(Riding(DrakeStage.Grown)), "rook", 4, 2);
        var start = next.Do(new Wait("rook")).Do(new EndPhase()).Do(new EndPhase()).Do(new EndPhase()).Do(new EndPhase());
        Assert.Equal(3, start.Turn);

        var result = start.Try(new Move("rook", new Coord(4, 2)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Empty(result.Events.OfType<UnitFrosted>());
    }

    [Fact]
    public void TheFrostNeverStrikesAnAlly()
    {
        var state = OnField(Riding(DrakeStage.Grown), wrenAt: "3,3");
        var hp = state.Find("wren")!.Hp;

        var (next, events) = Moved(state, "rook", 4, 3);

        Assert.DoesNotContain(events.OfType<UnitFrosted>(), f => f.UnitId == "wren");
        Assert.Equal(hp, next.Find("wren")!.Hp);
        Assert.Equal(0, next.Find("wren")!.Frosted);
    }

    [Fact]
    public void ABossTakesTheFrostButIsNeverHeld()
    {
        var state = OnField(Riding(DrakeStage.Grown), extra: "B brigand 6,0 group:far behavior:boss");
        var boss = state.Units.Single(u => u.IsBoss);

        var (next, events) = Moved(state, "rook", 6, 1);

        var frosted = Assert.Single(events.OfType<UnitFrosted>());
        Assert.Equal((boss.Id, false, true), (frosted.UnitId, frosted.Held, frosted.Boss));
        Assert.Equal(boss.Hp - 1, next.Find(boss.Id)!.Hp);
        Assert.Equal(0, next.Find(boss.Id)!.Frosted);
    }

    [Fact]
    public void TheFrostNeverKills()
    {
        var state = OnField(Riding(DrakeStage.Grown));
        state = state.WithUnit(state.Find("brigand-1")! with { Hp = 1 });

        var (next, events) = Moved(state, "rook", 4, 2);

        Assert.Equal(1, next.Find("brigand-1")!.Hp);
        Assert.Equal(0, Assert.Single(events.OfType<UnitFrosted>()).Damage);
        Assert.Equal(1, next.Find("brigand-1")!.Frosted);
    }

    [Fact]
    public void AHeldUnitMovesOneTileThroughItsNextPhaseAndIsFreeAfter()
    {
        var (next, _) = Moved(OnField(Riding(DrakeStage.Grown)), "rook", 4, 2);
        Assert.Equal(1, next.ReachOf(next.Find("brigand-1")!, Starter).Mov);

        var enemyPhase = next.Do(new EndPhase());
        Assert.Equal(2, enemyPhase.Find("brigand-1")!.Frosted);
        Assert.Equal(1, enemyPhase.ReachOf(enemyPhase.Find("brigand-1")!, Starter).Mov);

        var after = enemyPhase.Do(new EndPhase());
        Assert.Equal(0, after.Find("brigand-1")!.Frosted);
        Assert.Equal(Starter.Class(after.Find("brigand-1")!.Unit.ClassId).Mov, after.ReachOf(after.Find("brigand-1")!, Starter).Mov);
    }

    [Fact]
    public void AHeldUnitIsOwedNoCanto()
    {
        var lancer = Recruit("wren", "lancer", Wren.Stats, "iron_lance");
        var state = BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Rook, lancer), string.Format(Field, "0,4", ""));
        var held = state.WithUnit(state.Find("wren")! with { Frosted = 2 });

        Assert.Equal(1, held.ReachOf(held.Find("wren")!, Starter).Mov);
        Assert.Null(held.Do(new Move("wren", new Coord(1, 4))).Find("wren")!.Canto);
        Assert.Null(held.Do(new Wait("wren")).Find("wren")!.Canto);
        Assert.NotNull(state.Do(new Move("wren", new Coord(1, 4))).Find("wren")!.Canto);
    }

    [Fact]
    public void TheFrostRestsTheTurnAfterItFiresAndIsReadyTheTurnAfterThat()
    {
        var (next, _) = Moved(OnField(Riding(DrakeStage.Grown)), "rook", 4, 2);
        var turn2 = next.Do(new Wait("rook")).Do(new EndPhase()).Do(new EndPhase());
        Assert.Equal(2, turn2.Turn);
        Assert.False(DrakeFrost.Ready(turn2, Starter, turn2.Find("rook")!));
        Assert.Equal("Frost: resting (ready turn 3); on a flown landing beside enemies, each takes 1 and is held to 1 tile next phase (bosses: no hold)", DrakeFrost.CardLine(turn2, Starter, turn2.Find("rook")!));

        var (rested, restedEvents) = Moved(turn2, "rook", 4, 3);
        Assert.Empty(restedEvents.OfType<UnitFrosted>());

        var turn3 = rested.Do(new Wait("rook")).Do(new EndPhase()).Do(new EndPhase());
        Assert.StartsWith("Frost: ready;", DrakeFrost.CardLine(turn3, Starter, turn3.Find("rook")!));
        var (_, readyEvents) = Moved(turn3, "rook", 5, 3);
        Assert.NotEmpty(readyEvents.OfType<UnitFrosted>());
    }

    [Fact]
    public void AGroundedRiderWalksAndFiresNoFrost()
    {
        var state = OnField(Riding(DrakeStage.Grown));
        state = state.WithUnit(state.Find("rook")! with { Grounded = 2 });

        var (next, events) = Moved(state, "rook", 4, 2);

        Assert.Empty(events.OfType<UnitFrosted>());
        Assert.Null(next.Find("rook")!.FlewFrom);
    }

    [Fact]
    public void NoDrakeNoFrostAndTheDrakeWardenHasNeither()
    {
        Assert.Empty(Moved(OnField(Riding(null)), "rook", 4, 2).Events.OfType<UnitFrosted>());
        Assert.Empty(Moved(OnField(Riding(DrakeStage.Grown, "drover")), "rook", 4, 2).Events.OfType<UnitFrosted>());
        Assert.Null(DrakeFrost.CardLine(OnField(Riding(null)), Starter, OnField(Riding(null)).Find("rook")!));
    }

    [Fact]
    public void SheMayAttackTheUnitSheJustFrosted()
    {
        var (next, _) = Moved(OnField(Riding(DrakeStage.Grown)), "rook", 4, 2);
        var hp = next.Find("brigand-1")!.Hp;

        var result = next.Try(new Attack("rook", "brigand-1"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.True((result.Next.Find("brigand-1")?.Hp ?? 0) < hp);
    }

    [Fact]
    public void TheMovePreviewAndThreatFromPrintTheFrost()
    {
        var state = OnField(Riding(DrakeStage.Grown), extra: "B brigand 6,0 group:far behavior:boss");
        var names = UnitNames.Of(state, Starter);
        var rook = state.Find("rook")!;

        Assert.Equal("Frost: 2 enemies, 1 damage, held to 1 tile", DrakeFrost.Preview(state, Starter, rook, new Coord(5, 3), names));
        Assert.Matches(@"^Frost: 1 enemy, 1 damage, held to 1 tile \(\w[\w ]*: no hold, boss\)$", DrakeFrost.Preview(state, Starter, rook, new Coord(6, 1), names));
        Assert.Null(DrakeFrost.Preview(state, Starter, rook, new Coord(2, 2), names));

    }

    [Fact]
    public void ThreatFromPricesTheFrostedBoardAndSpendsNothing()
    {
        var state = OnField(Riding(DrakeStage.Grown));
        var rook = state.Find("rook")!;

        var board = DrakeFrost.Strike(state, Starter, rook, new Coord(4, 2), new List<GameEvent>());

        Assert.Equal(1, board.Find("brigand-1")!.Frosted);
        Assert.Equal(state.Find("brigand-1")!.Hp - 1, board.Find("brigand-1")!.Hp);
        Assert.Null(board.Find("rook")!.FrostTurn);
        Assert.Equal(new Coord(0, 2), board.Find("rook")!.At);
        Assert.NotNull(Queries.Threats(board, Starter, board.Find("rook")!, new Coord(4, 2)));
    }

    [Fact]
    public void TheEventLineSaysTheHold()
    {
        var names = UnitNames.Of(OnField(Riding(DrakeStage.Grown)), Starter);

        Assert.Equal("The drake's frost strikes Brigand 1 for 1 (hp 7); held to 1 tile, no Canto, until enemy phase ends", PlaySession.Describe(new UnitFrosted("brigand-1", "rook", 1, 7, true, false, Side.Enemy), Starter, names));
    }

    [Fact]
    public void TheSkyCaptainStoopsAfterFlyingFourTiles()
    {
        var state = OnField(Riding(null));
        var (flown, _) = Moved(state, "rook", 4, 2);
        var rook = flown.Find("rook")!;
        var target = flown.Find("brigand-1")!;

        var forecast = Core.Combat.Forecast(rook.ToCombatant(flown, Starter, against: target), target.Answering(flown, Starter, rook.At, rook), 1, flown.Scheme);

        Assert.Equal(2, forecast.Attacker.Stoop);
        Assert.Equal(0, forecast.Defender.Stoop);
        Assert.Equal(2, Queries.Forecast(state, Starter, state.Find("rook")!, state.Find("brigand-1")!, from: new Coord(4, 2))!.Attacker.Stoop);
    }

    [Fact]
    public void AShorterFlightDoesNotStoop()
    {
        var state = OnField(Riding(null));
        var near = state.WithUnit(state.Find("rook")! with { At = new Coord(1, 2) });
        var (flown, _) = Moved(near, "rook", 4, 2);

        Assert.Equal(new Coord(1, 2), flown.Find("rook")!.FlewFrom);
        Assert.Equal(0, Stoop.Bonus(Starter, flown.Find("rook")!));
        Assert.Equal(0, Queries.Forecast(near, Starter, near.Find("rook")!, near.Find("brigand-1")!, from: new Coord(4, 2))!.Attacker.Stoop);
    }

    [Fact]
    public void ARiderWithADrakeNeverStoops()
    {
        var (flown, _) = Moved(OnField(Riding(DrakeStage.Grown)), "rook", 4, 2);

        Assert.Equal(0, Stoop.Bonus(Starter, flown.Find("rook")!));
    }

    [Fact]
    public void TheStoopAddsTwoToTheFirstStrikeOnlyWhenItHits()
    {
        var plain = new SideForecast(true, 10, 100, 100, 0, true, Stoop: 2);
        var forecast = new CombatForecast(plain, SideForecast.None, RollScheme.TwoRollAverage);

        Assert.Equal(22, forecast.AttackerDamageLivedFor(30));
        Assert.Null(new CombatForecast(plain with { DisplayedHit = 50 }, SideForecast.None, RollScheme.TwoRollAverage).FirstRoundMissChance(13));
        Assert.NotNull(new CombatForecast(plain with { DisplayedHit = 50 }, SideForecast.None, RollScheme.TwoRollAverage).FirstRoundMissChance(12));
    }

    [Fact]
    public void OnTheBoardTheFirstStrikeCarriesTheStoop()
    {
        var state = OnField(Riding(null));
        var (flown, _) = Moved(state, "rook", 4, 2);
        var plain = OnField(Riding(null)).WithUnit(state.Find("rook")! with { At = new Coord(4, 2), Moved = true });

        var dived = flown.Try(new Attack("rook", "brigand-1"));
        var still = plain.Try(new Attack("rook", "brigand-1"));

        var a = Assert.Single(dived.Events.OfType<CombatFought>()).Strikes.Where(s => s.AttackerId == "rook").ToList();
        var b = Assert.Single(still.Events.OfType<CombatFought>()).Strikes.Where(s => s.AttackerId == "rook").ToList();
        Assert.True(a[0].Hit && b[0].Hit);
        Assert.Equal(b[0].Damage + 2, a[0].Damage);
        if (a.Count > 1 && b.Count > 1)
        {
            Assert.Equal(b[1].Damage, a[1].Damage);
        }
    }

    [Fact]
    public void TheForecastLineAndTheProtocolSayTheStoop()
    {
        var state = OnField(Riding(null));
        var forecast = Queries.Forecast(state, Starter, state.Find("rook")!, state.Find("brigand-1")!, from: new Coord(4, 2))!;

        Assert.Contains("; Stoop +2 on the first strike", PlaySession.ForecastLine(state.Find("rook")!, state.Find("brigand-1")!, forecast));
        Assert.Contains("\"stoop\":2", ProtocolJson.Forecast(forecast));
        Assert.Equal(2, ProtocolJson.ReadForecast(ProtocolJson.Forecast(forecast)).Attacker.Stoop);
    }

    [Fact]
    public void TheFrostHoldAndRestSurviveTheProtocolRoundTrip()
    {
        var (next, _) = Moved(OnField(Riding(DrakeStage.Grown)), "rook", 4, 2);

        var read = ProtocolJson.ReadState(ProtocolJson.State(next, Starter), Starter);

        Assert.Equal(1, read.Find("brigand-1")!.Frosted);
        Assert.Equal(1, read.Find("rook")!.FrostTurn);
        Assert.Equal(new Coord(0, 2), read.Find("rook")!.FlewFrom);
    }

    [Theory]
    [InlineData("\"kind\": \"drake_frost\", \"damage\": -1, \"rest\": 1", "effect.damage", "must not be negative")]
    [InlineData("\"kind\": \"drake_frost\", \"damage\": 1, \"rest\": -1", "effect.rest", "must not be negative")]
    [InlineData("\"kind\": \"stoop\", \"flight\": 0, \"damage\": 2", "effect.flight", "must be at least 1")]
    [InlineData("\"kind\": \"stoop\", \"flight\": 4, \"damage\": 0", "effect.damage", "must be at least 1")]
    public void AMalformedSkyCaptainAbilityIsRefusedAtLoad(string fields, string field, string why)
    {
        var abilities = Fixture.Abilities.Replace("\n] }", ",\n{ \"id\": \"gap\", \"name\": \"Gap\", \"text\": \"A test line.\", \"effect\": { " + fields + " } }\n] }");

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(abilities: abilities)));
        Assert.Contains("gap", error.Message);
        Assert.Contains(field, error.Message);
        Assert.Contains(why, error.Message);
    }

    [Fact]
    public void TheSkyCaptainAbilitiesRoundTripThroughTheSerializer()
    {
        var reloaded = ContentLoader.Parse(ContentSerializer.Write(Starter));

        Assert.Equal(new DrakeFrostEffect(1, 1), reloaded.Ability("drake_frost").Effect);
        Assert.Equal(new StoopEffect(4, 2), reloaded.Ability("stoop").Effect);
        Assert.Equal(new[] { "drake_frost", "stoop" }, reloaded.Class("skycaptain").Abilities);
    }

    [Fact]
    public void TheCardListsOnlyThePassiveTheRiderHas()
    {
        var withDrake = OnField(Riding(DrakeStage.Grown));
        var without = OnField(Riding(null));

        var rook = string.Join("\n", PlaySession.ShowLines(withDrake, Starter, withDrake.Find("rook")!));
        var edda = string.Join("\n", PlaySession.ShowLines(without, Starter, without.Find("rook")!));

        Assert.Contains("Drake Frost (", rook);
        Assert.DoesNotContain("Stoop (", rook);
        Assert.Contains("Stoop (", edda);
        Assert.DoesNotContain("Drake Frost (", edda);
    }
}
