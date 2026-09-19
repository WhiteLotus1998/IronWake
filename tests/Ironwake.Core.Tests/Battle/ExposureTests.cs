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

    [Fact]
    public void ASleepingGuardIsCountedByItsReachFromWhereItStandsAndAnAggressiveUnitByItsMove()
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
            P recruit:wren 0,2
            E brigand 3,1 group:yard behavior:guard
            E soldier 3,2 group:yard behavior:aggressive

            """;
        var state = Start(map: map);
        var hale = state.Find("hale")!;

        var beside = Exposure.Of(state, Starter, hale, new Coord(2, 1));
        var apart = Exposure.Of(state, Starter, hale, new Coord(0, 0));

        var brigandStrike = Core.Combat.Forecast(state.Find("brigand-1")!.ToCombatant(state.Map, Starter), (hale with { At = new Coord(2, 1) }).ToCombatant(state.Map, Starter), 1, state.Scheme).Attacker;
        Assert.True(beside.NoCrit > brigandStrike.Damage * Strikes(brigandStrike), "the aggressive soldier reaches the tile beside the brigand");
        Assert.True(apart.NoCrit > 0, "the aggressive soldier reaches 0,0 within its move");
        var soldierStrike = Core.Combat.Forecast(state.Find("soldier-1")!.ToCombatant(state.Map, Starter), (hale with { At = new Coord(0, 0) }).ToCombatant(state.Map, Starter), 1, state.Scheme).Attacker;
        Assert.Equal(soldierStrike.Damage * Strikes(soldierStrike), apart.NoCrit);
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
    /// HP set <paramref name="hpShort"/> above the forecast's damage from the tile beside it.
    /// </summary>
    private static (BattleState State, BattleUnit Hale, BattleUnit Brigand) CertainKillBoard(int hpShort, int rawHit = 100)
    {
        for (var dex = 0; dex <= 80; dex++)
        {
            var sharp = Recruit("hale", new Stats(22, 8, 0, dex, 8, 6, 5, 2, 9), "iron_sword");
            var state = Start(roster: ValueList<Unit>.Of(sharp, Wren));
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
