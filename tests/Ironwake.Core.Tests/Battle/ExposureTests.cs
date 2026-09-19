namespace Ironwake.Core.Tests.Battle;

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
