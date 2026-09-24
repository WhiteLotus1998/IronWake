namespace Ironwake.Core.Tests.Battle;

using Ironwake.Content;
using Ironwake.Sim;

using static BattleFixture;

/// <summary>
/// The exposure query behind issue 12's captain veto (Design Table, seventh round): one
/// sum over the whole cycle, the counter on the unit's own attack plus every enemy whose
/// reach-plus-range covers the tile, at the forecast's own numbers, returned twice: with
/// no crit landing and with every strike a crit.
/// </summary>
public class ExposureTests
{
    private static int Strikes(SideForecast side) => side.Doubles ? 2 : 1;

    [Fact]
    public void TheSumIsTheCounterPlusEveryEnemyThatCanReachTheTile()
    {
        var state = Start();
        var hale = state.Find("hale")!;
        var brigand = state.Find("brigand-1")!;
        var soldier = state.Find("soldier-1")!;
        var tile = new Coord(2, 1);

        var sum = Exposure.Of(state, Starter, hale, tile, brigand);

        var moved = state.WithUnit(hale with { At = tile });
        var own = Queries.Forecast(moved, Starter, moved.Find("hale")!, brigand)!;
        var counter = own.Defender.Damage * Strikes(own.Defender);
        var fromBrigand = Core.Combat.Forecast(brigand.ToCombatant(state.Map, Starter), moved.Find("hale")!.ToCombatant(state.Map, Starter), 1, state.Scheme).Attacker;
        var fromSoldier = Core.Combat.Forecast(soldier.ToCombatant(state.Map, Starter), moved.Find("hale")!.ToCombatant(state.Map, Starter), 1, state.Scheme).Attacker;
        var tileSum = fromBrigand.Damage * Strikes(fromBrigand) + fromSoldier.Damage * Strikes(fromSoldier);

        Assert.Equal(counter, sum.Counter);
        Assert.Equal(counter + tileSum, sum.NoCrit);
        Assert.Equal((counter + tileSum) * Core.Combat.CritMultiplier, sum.WithCrit);
        Assert.True(sum.NoCrit > 0);
    }

    [Fact]
    public void APlanWithNoAttackHasACounterOfZero()
    {
        var state = Start();
        var sum = Exposure.Of(state, Starter, state.Find("hale")!, new Coord(2, 1));
        Assert.Equal(0, sum.Counter);
        Assert.True(sum.NoCrit > 0);
    }

    [Fact]
    public void ATileNoEnemyCanReachIsSafe()
    {
        var state = Start(map: Yard.Replace("behavior:aggressive", "behavior:hold"));
        Assert.Equal(new ExposureSum(0, 0, 0), Exposure.Of(state, Starter, state.Find("hale")!, new Coord(0, 0)));
        Assert.True(Exposure.Of(state, Starter, state.Find("hale")!, new Coord(2, 1)).NoCrit > 0, "a holding brigand still strikes the tile beside it");
    }

    /// <summary>A 12x6 field with one sleeping Guard brigand at 9,1 and both player units outside every radius of it.</summary>
    private const string Watch = """
        name: Watch
        size: 12x6
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ............
        ............
        ............
        ............
        ............
        ............

        units:
        P captain 0,1
        P recruit:wren 0,5
        E brigand 9,1 group:watch behavior:guard

        """;

    private static SideForecast StrikeOn(BattleState state, string enemyId, Coord from, BattleUnit unit, Coord tile)
    {
        var enemy = state.Find(enemyId)!;
        var striker = new Combatant(enemy.Unit, Starter.Class(enemy.Unit.ClassId), enemy.EquippedWeapon(Starter)!, state.Map.TerrainAt(from, Starter), enemy.Hp, 0, false);
        return Core.Combat.Forecast(striker, (unit with { At = tile }).ToCombatant(state.Map, Starter), from.DistanceTo(tile), state.Scheme).Attacker;
    }

    /// <summary>
    /// Issue 128: a sleeping Guard group the move itself wakes is counted by its move. The
    /// tile at exactly the wake radius from the brigand wakes it, and the sum is the
    /// brigand's strike from the tile beside the captain, three tiles into its move.
    /// </summary>
    [Fact]
    public void ASleepingGroupTheMoveWakesByProximityIsCountedByItsMove()
    {
        var state = Start(map: Watch);
        var hale = state.Find("hale")!;
        var tile = new Coord(5, 1);
        Assert.Equal(Starter.WakeRadius, tile.DistanceTo(state.Find("brigand-1")!.At));

        var sum = Exposure.Of(state, Starter, hale, tile);

        var strike = StrikeOn(state, "brigand-1", new Coord(6, 1), hale, tile);
        Assert.True(Exposure.Board(state, Starter, hale, tile).IsAwake("watch"));
        Assert.Equal(new ExposureSum(0, strike.Damage * Strikes(strike), strike.Damage * Core.Combat.CritMultiplier * Strikes(strike)), sum);
        Assert.True(sum.NoCrit > 0);
    }

    /// <summary>
    /// Issue 128, falsified the other way: a group the command does not wake is counted
    /// from where it stands. One tile past the wake radius with no fight, the brigand
    /// stays asleep and its range 1 reaches nothing, though awake it would reach the tile.
    /// </summary>
    [Fact]
    public void ASleepingGroupTheMoveDoesNotWakeIsCountedFromWhereItStands()
    {
        var state = Start(map: Watch);
        var hale = state.Find("hale")!;
        var tile = new Coord(4, 1);
        Assert.Equal(Starter.WakeRadius + 1, tile.DistanceTo(state.Find("brigand-1")!.At));

        Assert.False(Exposure.Board(state, Starter, hale, tile).IsAwake("watch"));
        Assert.Equal(new ExposureSum(0, 0, 0), Exposure.Of(state, Starter, hale, tile));
        Assert.True(Exposure.Of(state.Wake("watch"), Starter, hale, tile).NoCrit > 0, "awake, the brigand reaches the tile");
    }

    /// <summary>
    /// Issue 128: when the plan attacks, the fight's two tiles are noisy and a group within
    /// the noise radius of either wakes, even when neither player unit is within the wake
    /// radius of it. The brigand at 4,4 is five from the captain's tile and four from the
    /// soldier he attacks, so the attack alone wakes it and it is counted by its move;
    /// the same tile with no attack leaves it asleep.
    /// </summary>
    [Fact]
    public void ASleepingGroupTheFightsNoiseWakesIsCountedByItsMove()
    {
        const string map = """
            name: Noise
            size: 10x6
            win: rout
            turn_limit: 10
            recall: 3
            enemy_level: 1

            ..........
            ..........
            ..........
            ..........
            ..........
            ..........

            units:
            P captain 0,1
            P recruit:wren 0,5
            E soldier 3,1 group:road behavior:aggressive
            E brigand 4,4 group:watch behavior:guard

            """;
        var state = Start(map: map);
        var hale = state.Find("hale")!;
        var soldier = state.Find("soldier-1")!;
        var brigand = state.Find("brigand-1")!;
        var tile = new Coord(2, 1);
        Assert.True(tile.DistanceTo(brigand.At) > Starter.WakeRadius);
        Assert.True(state.Find("wren")!.At.DistanceTo(brigand.At) > Starter.WakeRadius);
        Assert.True(soldier.At.DistanceTo(brigand.At) <= Starter.NoiseRadius);

        var quiet = Exposure.Of(state, Starter, hale, tile);
        var loud = Exposure.Of(state, Starter, hale, tile, soldier);
        var awake = Exposure.Of(state.Wake("watch"), Starter, hale, tile);

        Assert.False(Exposure.Board(state, Starter, hale, tile).IsAwake("watch"));
        Assert.True(Exposure.Board(state, Starter, hale, tile, soldier).IsAwake("watch"));
        Assert.True(loud.Counter > 0);
        Assert.True(quiet.NoCrit < awake.NoCrit, "asleep, the brigand does not reach the tile");
        Assert.Equal(awake.NoCrit, loud.NoCrit - loud.Counter);
    }

    /// <summary>
    /// Issue 128: a certain kill on a grouped enemy wakes that enemy's sleeping group by
    /// death, even though issue 117 drops the dead target itself from the board. The
    /// group's other member stands outside both radii of every player unit and of both
    /// fight tiles, so death is the only cause that can have fired, and the same board
    /// with no death listed wakes nothing; a kill one damage short leaves the target standing.
    /// </summary>
    [Fact]
    public void ACertainKillWakesTheTargetsGroupByDeathAtAnyDistance()
    {
        const string map = """
            name: Far
            size: 14x4
            win: rout
            turn_limit: 10
            recall: 3
            enemy_level: 1

            ..............
            ..............
            ..............
            ..............

            units:
            P captain 0,1
            P recruit:wren 0,2
            E brigand 3,1 group:watch behavior:guard
            E soldier 13,3 group:watch behavior:guard

            """;
        var tile = new Coord(2, 1);
        var (state, hale, brigand) = CertainKillBoard(hpShort: 0, map: map);
        var far = state.Find("soldier-1")!.At;
        Assert.True(far.DistanceTo(tile) > Starter.NoiseRadius && far.DistanceTo(brigand.At) > Starter.NoiseRadius);
        Assert.True(far.DistanceTo(state.Find("wren")!.At) > Starter.WakeRadius);

        var board = Exposure.Board(state, Starter, hale, tile, brigand);

        Assert.Null(board.Find("brigand-1"));
        Assert.True(board.IsAwake("watch"));

        var noisy = new[] { tile, brigand.At };
        Assert.Empty(WakeCheck.Run(state, board, Starter, noisy, Array.Empty<string>()));
        Assert.Equal(new[] { new GroupWoke("watch", WakeCause.Death) }, WakeCheck.Run(state, board, Starter, noisy, new[] { "watch" }));

        var (shortState, shortHale, shortBrigand) = CertainKillBoard(hpShort: 1, map: map);
        Assert.NotNull(Exposure.Board(shortState, Starter, shortHale, tile, shortBrigand).Find("brigand-1"));
    }

    /// <summary>
    /// DECISIONS/0025, the scope of the rule: an ally's body in a one-tile crossing is a
    /// blocker on the board as it stands, and the enemy behind it is priced at zero even
    /// though the ally may die inside the cycle and open the crossing. Deliberate: an
    /// ally's death has the enemy's choice inside it and is never this command's
    /// certainty, so the veto is a worst case over the standing board, not a guarantee
    /// over the cycle. Saltmarsh Ford seed 24 under two rolls, turn 3, is the shape.
    /// </summary>
    [Fact]
    public void AnAllyInAOneTileCrossingBlocksTheEnemyBehindItInTheSum()
    {
        const string map = """
            name: Crossing
            size: 5x5
            win: rout
            turn_limit: 10
            recall: 3
            enemy_level: 1

            .....
            .....
            ~~=~~
            .....
            .....

            units:
            P captain 2,4
            P recruit:wren 2,2
            E soldier 2,0 group:bank behavior:aggressive

            """;
        var state = Start(map: map);
        var hale = state.Find("hale")!;
        var tile = new Coord(2, 3);

        Assert.Equal(new ExposureSum(0, 0, 0), Exposure.Of(state, Starter, hale, tile));
        Assert.True(Exposure.Of(state.WithoutUnit("wren"), Starter, hale, tile).NoCrit > 0, "with the crossing open the soldier strikes from the ford");
    }

    /// <summary>
    /// Issue 117: a target the named attack kills with certainty (raw hit 100, first
    /// strike's damage at least its HP) contributes neither its counter nor its
    /// enemy-phase term. The captain's Dex is raised until the raw hit clamps at 100 and
    /// the brigand's HP is set to the forecast's damage; the sum at the tile beside it is
    /// then the soldier's strike alone.
    /// </summary>
    [Fact]
    public void ATargetTheAttackKillsWithCertaintyContributesNothingToEitherSum()
    {
        var (state, hale, brigand) = CertainKillBoard(hpShort: 0);
        var tile = new Coord(2, 1);
        Assert.Equal(100, Queries.Forecast(state.WithUnit(hale with { At = tile }), Starter, hale with { At = tile }, brigand)!.Attacker.HitChance);

        var sum = Exposure.Of(state, Starter, hale, tile, brigand);

        var soldier = Core.Combat.Forecast(state.Find("soldier-1")!.ToCombatant(state.Map, Starter), (hale with { At = tile }).ToCombatant(state.Map, Starter), 1, state.Scheme).Attacker;
        Assert.Equal(0, sum.Counter);
        Assert.Equal(soldier.Damage * Strikes(soldier), sum.NoCrit);
        Assert.Equal(soldier.Damage * Core.Combat.CritMultiplier * Strikes(soldier), sum.WithCrit);
        Assert.True(Exposure.Of(state, Starter, hale, tile).NoCrit > sum.NoCrit, "the same tile with no attack named still counts the brigand");
    }

    /// <summary>
    /// Issue 117, falsified the other way: certainty is the raw hit, not the displayed one.
    /// A raw 99 prints as 100 under two-roll averaging and the target still counts.
    /// </summary>
    [Fact]
    public void ARawNinetyNineThatPrintsAsOneHundredStillCounts()
    {
        var (state, hale, brigand) = CertainKillBoard(hpShort: 0, rawHit: 99);
        var tile = new Coord(2, 1);
        var forecast = Queries.Forecast(state.WithUnit(hale with { At = tile }), Starter, hale with { At = tile }, brigand)!.Attacker;
        Assert.Equal(RollScheme.TwoRollAverage, state.Scheme);
        Assert.Equal(99, forecast.HitChance);
        Assert.Equal(100, forecast.DisplayedHit);
        Assert.True(forecast.Damage >= brigand.Hp);

        var sum = Exposure.Of(state, Starter, hale, tile, brigand);

        var full = Exposure.Of(state.WithUnit(brigand with { Hp = brigand.Unit.Stats.Hp }), Starter, hale, tile, brigand with { Hp = brigand.Unit.Stats.Hp });
        Assert.True(sum.Counter > 0);
        Assert.Equal(full, sum);
    }

    /// <summary>Issue 117, falsified the other way: damage one short of the target's HP is not a certain kill.</summary>
    [Fact]
    public void DamageOneShortOfTheTargetsHpStillCounts()
    {
        var (state, hale, brigand) = CertainKillBoard(hpShort: 1);
        var tile = new Coord(2, 1);
        var forecast = Queries.Forecast(state.WithUnit(hale with { At = tile }), Starter, hale with { At = tile }, brigand)!.Attacker;
        Assert.Equal(100, forecast.HitChance);
        Assert.Equal(brigand.Hp - 1, forecast.Damage);

        var sum = Exposure.Of(state, Starter, hale, tile, brigand);

        var full = Exposure.Of(state.WithUnit(brigand with { Hp = brigand.Unit.Stats.Hp }), Starter, hale, tile, brigand with { Hp = brigand.Unit.Stats.Hp });
        Assert.True(sum.Counter > 0);
        Assert.Equal(full, sum);
    }

    /// <summary>
    /// Issue 117: the veto reads the narrowed sum. The captain's HP is set one above the
    /// worst narrowed sum over the brigand's attack tiles and at or below every old sum,
    /// so under the old rule every attack was refused and under the new one the plan is
    /// the certain kill.
    /// </summary>
    [Fact]
    public void TheCaptainTakesACertainKillTheOldSumWouldHaveRefused()
    {
        var (state, hale, brigand) = CertainKillBoard(hpShort: 0);
        var attackTiles = new[] { new Coord(2, 1), new Coord(3, 0), new Coord(4, 1) };
        var narrowed = attackTiles.Max(t => Exposure.Of(state, Starter, hale, t, brigand).NoCrit);
        var hp = narrowed + 1;
        var full = state.WithUnit(brigand with { Hp = brigand.Unit.Stats.Hp });
        Assert.All(attackTiles, t => Assert.True(Exposure.Of(full, Starter, hale, t, brigand with { Hp = brigand.Unit.Stats.Hp }).NoCrit >= hp, "the old sum refused " + t));

        var lowHale = hale with { Hp = hp };
        var plan = HeuristicPlayer.PlanUnit(state.WithUnit(lowHale), Starter, lowHale);

        var attack = Assert.IsType<Attack>(plan[^1]);
        Assert.Equal("brigand-1", attack.TargetId);
    }

    /// <summary>
    /// Issue 117's board: on Old Mill Road seed 2 the heuristic's captain stood two tiles
    /// from a boss at 8 HP for twelve turns, a kill at raw 100 for 9 refused because the
    /// sum counted the boss's counter and enemy-phase strike. The game now ends in a win.
    /// </summary>
    [Fact]
    public void OldMillRoadSeedTwoEndsInAWinNotAStall()
    {
        var file = "old_mill_road" + MapFiles.Extension;
        var map = Maps.MapFixture.Parse(File.ReadAllText(Path.Combine(Maps.MapFixture.MapsDirectory, file)), file);

        var game = Runner.Play(Starter, map, 2, new HeuristicPlayer());

        Assert.True(game.Won, game.Result + " at turn " + game.Turns);
        Assert.True(game.Turns < map.TurnLimit, "won at turn " + game.Turns);
    }

    /// <summary>
    /// The yard with the captain's Dex raised until his raw hit against the brigand is
    /// <paramref name="rawHit"/> (searched, since Hit is monotone in Dex) and the brigand's
    /// HP set <paramref name="hpShort"/> above the forecast's damage from the tile beside it,
    /// on the yard or on <paramref name="map"/>, whose brigand must stand at 3,1.
    /// </summary>
    private static (BattleState State, BattleUnit Hale, BattleUnit Brigand) CertainKillBoard(int hpShort, int rawHit = 100, string? map = null)
    {
        for (var dex = 0; dex <= 80; dex++)
        {
            var sharp = Recruit("hale", new Stats(22, 8, 0, dex, 8, 6, 5, 2, 9), "iron_sword");
            var state = Start(roster: ValueList<Unit>.Of(sharp, Wren), map: map);
            var hale = state.Find("hale")! with { At = new Coord(2, 1) };
            var brigand = state.Find("brigand-1")!;
            var forecast = Queries.Forecast(state.WithUnit(hale), Starter, hale, brigand)!.Attacker;
            if (forecast.HitChance != rawHit)
            {
                continue;
            }

            Assert.True(forecast.Damage > 0);
            var hurt = brigand with { Hp = forecast.Damage + hpShort };
            var board = state.WithUnit(hurt);
            return (board, board.Find("hale")!, hurt);
        }

        throw new Xunit.Sdk.XunitException("no Dex gives a raw hit of " + rawHit);
    }

    /// <summary>
    /// Issue 126: a strike whose raw hit against the unit is 0 after the clamp is the
    /// same zero-probability branch as issue 117's certain kill, read from the other side.
    /// The only enemy on the board cannot land on the captain, so the counter on the named
    /// attack and the enemy-phase term both read 0, with and without the attack named.
    /// </summary>
    [Fact]
    public void AnEnemyStrikeAtRawHitZeroContributesNothingToEitherSum()
    {
        var (state, hale, brigand) = UntouchableBoard(rawHit: 0);
        var tile = new Coord(2, 1);
        var forecast = Queries.Forecast(state.WithUnit(hale with { At = tile }), Starter, hale with { At = tile }, brigand)!;
        Assert.True(forecast.Defender.Strikes, "the brigand counters at range 1");
        Assert.Equal(0, forecast.Defender.HitChance);
        Assert.True(forecast.Defender.Damage > 0, "the strike would hurt if it landed");

        Assert.Equal(new ExposureSum(0, 0, 0), Exposure.Of(state, Starter, hale, tile, brigand));
        Assert.Equal(new ExposureSum(0, 0, 0), Exposure.Of(state, Starter, hale, tile));
    }

    /// <summary>Issue 126, falsified the other way: raw 1 is a strike that can land, and both terms count in full.</summary>
    [Fact]
    public void AnEnemyStrikeAtRawHitOneCountsInFull()
    {
        var (state, hale, brigand) = UntouchableBoard(rawHit: 1);
        var tile = new Coord(2, 1);
        var forecast = Queries.Forecast(state.WithUnit(hale with { At = tile }), Starter, hale with { At = tile }, brigand)!;
        Assert.Equal(1, forecast.Defender.HitChance);
        var counter = forecast.Defender.Damage * Strikes(forecast.Defender);
        Assert.True(counter > 0);

        var sum = Exposure.Of(state, Starter, hale, tile, brigand);

        var strike = Core.Combat.Forecast(brigand.ToCombatant(state.Map, Starter), (hale with { At = tile }).ToCombatant(state.Map, Starter), 1, state.Scheme).Attacker;
        Assert.Equal(1, strike.HitChance);
        Assert.Equal(counter, sum.Counter);
        Assert.Equal(counter + strike.Damage * Strikes(strike), sum.NoCrit);
        Assert.Equal((counter + strike.Damage * Strikes(strike)) * Core.Combat.CritMultiplier, sum.WithCrit);
    }

    /// <summary>
    /// The yard with the soldier removed and the captain's Spd raised until the brigand's
    /// raw hit against him on the tile beside it is <paramref name="rawHit"/> (searched,
    /// since Avoid is monotone in Spd). Lck stays at Hale's own, so the captain still hits
    /// the brigand and the counter is a real strike that cannot land.
    /// </summary>
    private static (BattleState State, BattleUnit Hale, BattleUnit Brigand) UntouchableBoard(int rawHit)
    {
        var map = Yard.Replace("E soldier 3,2 group:yard behavior:aggressive\n", string.Empty);
        for (var spd = 8; spd <= 120; spd++)
        {
            var quick = Recruit("hale", new Stats(22, 8, 0, 7, spd, 6, 5, 2, 9), "iron_sword");
            var state = Start(roster: ValueList<Unit>.Of(quick, Wren), map: map);
            Assert.Null(state.Find("soldier-1"));
            var hale = state.Find("hale")! with { At = new Coord(2, 1) };
            var brigand = state.Find("brigand-1")!;
            var forecast = Queries.Forecast(state.WithUnit(hale), Starter, hale, brigand)!;
            if (forecast.Defender.HitChance != rawHit)
            {
                continue;
            }

            return (state, state.Find("hale")!, brigand);
        }

        throw new Xunit.Sdk.XunitException("no Spd gives a raw hit of " + rawHit + " against the captain");
    }

    [Fact]
    public void BenchingLeavesTheNamedSlotEmptyAndShiftsNobody()
    {
        var map = Maps.MapFixture.Parse(Maps.MapFixture.OldMillRoad, "old_mill_road.map");
        var roster = ValueList<Unit>.Of(Hale, Wren, Ivo);

        var benched = BattleState.From(map, Starter, roster, 7, benched: ValueList<string>.Of("wren"));

        Assert.Null(benched.Find("wren"));
        Assert.Null(benched.Find("ivo"));
        Assert.Null(benched.UnitAt(new Coord(2, 8)));
        Assert.Single(benched.UnitsOf(Side.Player));
    }

    [Fact]
    public void BenchingABareSlotRecruitLeavesItsSlotEmpty()
    {
        const string map = """
            name: Yard
            size: 6x4
            win: rout
            turn_limit: 10
            recall: 3
            enemy_level: 1

            ......
            ......
            ......
            ......

            units:
            P captain 0,1
            P recruit 0,2
            P recruit 0,3
            E brigand 3,1 group:yard behavior:aggressive

            """;
        var roster = ValueList<Unit>.Of(Hale, Wren, Ivo);
        var full = Start(map: map, roster: roster);
        Assert.Equal(new Coord(0, 2), full.Find("wren")!.At);
        Assert.Equal(new Coord(0, 3), full.Find("ivo")!.At);

        var benched = BattleState.From(Maps.MapFixture.Parse(map), Starter, roster, 7, benched: ValueList<string>.Of("wren"));

        Assert.Null(benched.Find("wren"));
        Assert.Null(benched.UnitAt(new Coord(0, 2)));
        Assert.Equal(new Coord(0, 3), benched.Find("ivo")!.At);
    }

    [Fact]
    public void TheCaptainCannotBeBenched()
    {
        var ex = Assert.Throws<ArgumentException>(() => BattleState.From(YardMap, Starter, Roster, 7, benched: ValueList<string>.Of("hale")));
        Assert.Contains("captain", ex.Message);
    }
}
