using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The focused chair (issue 1157, round 389): a kill the heuristic plans for another unit goes to the fed
/// unit, Teodor, under one of two guards. Guarded keeps the even chair's rule; paying lets him take it at
/// up to 15 points less kill chance and one more enemy in reach, never onto a forecast death. Striking
/// (issue 1167, round 392) adds a strike on any target another unit is planned to attack, kill or not.
/// </summary>
public class FocusedPlayerTests
{
    /// <summary>A 6x4 yard: Hale at 0,1, Teodor at 0,2, a brigand at 3,1 both reach on turn 1.</summary>
    private const string Lone = """
        name: Lone
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
        P recruit:teodor 0,2
        E brigand 3,1 group:yard behavior:aggressive

        """;

    private const string Two = """
        name: Two
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
        P recruit:teodor 0,2
        E brigand 3,1 group:yard behavior:aggressive
        E soldier 4,2 group:yard behavior:aggressive

        """;

    /// <summary>A planned tile's exposure no tile on these yards exceeds.</summary>
    private const int Open = 10;

    /// <summary>Teodor with Hale's stats, so the two strike alike.</summary>
    private static readonly Unit Twin = Recruit(FocusedPlayer.Fed, Hale.Stats, "iron_sword");

    private static BattleState Board(Unit hale, Unit teodor, string map = Lone)
    {
        var state = Start(roster: ValueList<Unit>.Of(hale, teodor), map: map);
        var brigand = state.UnitsOf(Side.Enemy).First(e => e.At == new Coord(3, 1));
        return state.WithUnit(brigand with { Hp = 1 });
    }

    private static BattleUnit Brigand(BattleState state) => state.UnitsOf(Side.Enemy).Single(e => e.At == new Coord(3, 1));

    private static IReadOnlyList<Reach> EnemyReach(BattleState state) => state.UnitsOf(Side.Enemy).Select(e => state.ReachOf(e, Starter)).ToList();

    private static IReadOnlyList<Command> HalesPlan(BattleState state) => HeuristicPlayer.PlanUnit(state, Starter, state.Find("hale")!);

    private static string? Striker(IReadOnlyList<Command> plan) => plan[^1] is Attack attack ? attack.UnitId : null;

    /// <summary>Teodor's chance to kill the brigand from 2,1, which every tile beside it on the open yard shares.</summary>
    private static double TeodorsChance(BattleState state) =>
        HeuristicPlayer.KillProbability(state, Starter, state.Find(FocusedPlayer.Fed)!, new Coord(2, 1), Brigand(state));

    [Theory]
    [InlineData(FocusedPlayer.Guard.Guarded)]
    [InlineData(FocusedPlayer.Guard.Paying)]
    public void AKillGoesToTheFedUnitWhateverHisLevel(FocusedPlayer.Guard guard)
    {
        var state = Board(Hale, Twin with { Level = 5 });
        var plan = HalesPlan(state);
        Assert.Equal("hale", Striker(plan));

        var handed = FocusedPlayer.Hand(state, Starter, plan, guard);

        Assert.Equal(FocusedPlayer.Fed, Striker(handed));
        Assert.True(Resolver.Apply(state, Starter, handed[0]).Accepted);
    }

    [Fact]
    public void TheGuardedLineKeepsTheKillAtALowerKillChance()
    {
        var clumsy = Twin with { Stats = Twin.Stats with { Dex = 0, Lck = 0 } };
        var state = Board(Hale, clumsy);
        var plan = HalesPlan(state);

        Assert.Same(plan, FocusedPlayer.Hand(state, Starter, plan, FocusedPlayer.Guard.Guarded));
    }

    [Fact]
    public void ALowerKillChanceIsClassifiedAsTheChanceClause()
    {
        var state = Board(Hale, Twin);
        var teodor = state.Find(FocusedPlayer.Fed)!;

        var why = FocusedPlayer.Classify(state, Starter, teodor, Brigand(state), TeodorsChance(state) + 0.01, int.MaxValue, EnemyReach(state), refuseDeath: false);

        Assert.Equal(FocusedPlayer.Refusal.Chance, why);
    }

    [Fact]
    public void APayingGapOfFifteenPointsIsTaken()
    {
        var state = Board(Hale, Twin);

        Assert.NotNull(FocusedPlayer.Paying(state, Starter, state.Find(FocusedPlayer.Fed)!, Brigand(state), TeodorsChance(state) + 0.15, Open, EnemyReach(state)));
    }

    [Fact]
    public void APayingGapOfSixteenPointsIsRefused()
    {
        var state = Board(Hale, Twin);

        Assert.Null(FocusedPlayer.Paying(state, Starter, state.Find(FocusedPlayer.Fed)!, Brigand(state), TeodorsChance(state) + 0.16, Open, EnemyReach(state)));
    }

    [Fact]
    public void OneMoreEnemyInReachIsTakenAndTwoAreRefused()
    {
        var state = Board(Hale, Twin);
        var teodor = state.Find(FocusedPlayer.Fed)!;
        var reach = EnemyReach(state);

        // Every tile beside the brigand is in its own reach: one enemy. A planned tile with none is +1, with -1 is +2.
        Assert.NotNull(FocusedPlayer.Paying(state, Starter, teodor, Brigand(state), 0, 0, reach));
        Assert.Null(FocusedPlayer.Paying(state, Starter, teodor, Brigand(state), 0, -1, reach));
        Assert.Equal(FocusedPlayer.Refusal.Exposure, FocusedPlayer.Classify(state, Starter, teodor, Brigand(state), 0, 0, reach, refuseDeath: true));
    }

    [Fact]
    public void APayingKillOntoAForecastDeathIsRefused()
    {
        var state = Board(Hale, Twin, Two);
        state = state.WithUnit(state.Find(FocusedPlayer.Fed)! with { Hp = 1 });
        var teodor = state.Find(FocusedPlayer.Fed)!;
        var reach = EnemyReach(state);

        Assert.NotNull(EvenPlayer.KillFrom(state, Starter, teodor, Brigand(state), 0, int.MaxValue, reach));
        Assert.Null(FocusedPlayer.Paying(state, Starter, teodor, Brigand(state), 0, Open, reach));
        Assert.Equal(FocusedPlayer.Refusal.ForecastDeath, FocusedPlayer.Classify(state, Starter, teodor, Brigand(state), 0, int.MaxValue, reach, refuseDeath: true));
    }

    [Fact]
    public void TheFedUnitsOwnPlanIsNeverHanded()
    {
        var state = Board(Hale, Twin);
        var plan = HeuristicPlayer.PlanUnit(state, Starter, state.Find(FocusedPlayer.Fed)!);
        Assert.Equal(FocusedPlayer.Fed, Striker(plan));

        Assert.Same(plan, FocusedPlayer.Hand(state, Starter, plan, FocusedPlayer.Guard.Paying));
    }

    [Fact]
    public void AnAttackThatDoesNotKillOnAHitIsNeverHanded()
    {
        var state = Board(Hale, Twin);
        state = state.WithUnit(Brigand(state) with { Hp = Brigand(state).Unit.Stats.Hp });
        var plan = HalesPlan(state);
        Assert.Equal("hale", Striker(plan));

        Assert.Same(plan, FocusedPlayer.Hand(state, Starter, plan, FocusedPlayer.Guard.Paying));
    }

    private static BattleState Whole(BattleState state) => state.WithUnit(Brigand(state) with { Hp = Brigand(state).Unit.Stats.Hp });

    [Fact]
    public void TheStrikingLineStrikesAPlannedTargetItCannotKill()
    {
        var state = Whole(Board(Hale, Twin));
        var plan = HalesPlan(state);
        Assert.Equal("hale", Striker(plan));
        Assert.False(HeuristicPlayer.KillsOnHit(state, Starter, state.Find(FocusedPlayer.Fed)!, new Coord(2, 1), Brigand(state)));

        var strike = FocusedPlayer.Strike(state, Starter, plan);

        Assert.NotNull(strike);
        Assert.Equal(FocusedPlayer.Fed, Striker(strike));
        Assert.Equal(Brigand(state).Id, ((Attack)strike[^1]).TargetId);
        Assert.True(Resolver.Apply(state, Starter, strike[0]).Accepted);
    }

    [Fact]
    public void TheStrikingPlayerCountsTheStrikeAndTheCombat()
    {
        var state = Whole(Board(Hale, Twin));
        var player = new FocusedPlayer(FocusedPlayer.Guard.Striking);

        var commands = player.Next(state, Starter);

        Assert.Equal(FocusedPlayer.Fed, Striker(commands));
        Assert.Equal(1, player.Combats);
        Assert.Equal(HalesPlan(state)[^1] is Attack { UnitId: "hale" } ? 1 : 0, player.Strikes);
    }

    [Fact]
    public void ThePayingPlayerNeverHandsAStrike()
    {
        var state = Whole(Board(Hale, Twin));
        var player = new FocusedPlayer(FocusedPlayer.Guard.Paying);

        player.Next(state, Starter);

        Assert.Equal(0, player.Strikes);
    }

    [Fact]
    public void AStrikeOntoAForecastDeathIsRefused()
    {
        var state = Whole(Board(Hale, Twin, Two));
        state = state.WithUnit(state.Find(FocusedPlayer.Fed)! with { Hp = 1 });
        var teodor = state.Find(FocusedPlayer.Fed)!;

        Assert.Null(FocusedPlayer.StrikeFrom(state, Starter, teodor, Brigand(state), Open, EnemyReach(state)));
    }

    [Fact]
    public void AStrikeTakesOneMoreEnemyInReachAndRefusesTwo()
    {
        var state = Whole(Board(Hale, Twin));
        var teodor = state.Find(FocusedPlayer.Fed)!;
        var reach = EnemyReach(state);

        // Every tile beside the brigand is in its own reach: one enemy.
        Assert.NotNull(FocusedPlayer.StrikeFrom(state, Starter, teodor, Brigand(state), 1, reach));
        Assert.Null(FocusedPlayer.StrikeFrom(state, Starter, teodor, Brigand(state), 0, reach));
    }

    [Fact]
    public void AStrikeThatCannotLandIsRefused()
    {
        var state = Whole(Board(Hale, Twin));
        var brigand = Brigand(state);
        state = state.WithUnit(brigand with { Unit = brigand.Unit with { Stats = brigand.Unit.Stats with { Lck = 250 } } });
        var teodor = state.Find(FocusedPlayer.Fed)!;

        // The refusal is the landing clause: the tile beside the brigand forecasts no death for him.
        Assert.True(Exposure.Of(state, Starter, teodor, new Coord(2, 1), Brigand(state), teodor.EquippedSlot(Starter)).NoCrit < teodor.Hp);
        Assert.Null(FocusedPlayer.StrikeFrom(state, Starter, teodor, Brigand(state), Open, EnemyReach(state)));
    }

    [Fact]
    public void TheFedUnitsOwnPlanIsNeverStruck()
    {
        var state = Whole(Board(Hale, Twin));
        var plan = HeuristicPlayer.PlanUnit(state, Starter, state.Find(FocusedPlayer.Fed)!);

        Assert.Null(FocusedPlayer.Strike(state, Starter, plan));
    }
}
