using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The Sim captain's stage-2 plan (issue 1441, Table round 535; DECISIONS/0380): he declares a per-map art (Full Measure)
/// when it kills on its one hit a swallowed boss whose fall ends the map, the veto's sum pricing only the miss unless the hit is at least
/// <see cref="HeuristicPlayer.FinisherHit"/>; and with a swallowed boss standing, a walk that would end in the boss's
/// reach ends on the nearest tile clear of it instead.
/// </summary>
public class CaptainFinisherTests
{
    private static readonly Unit Captain = Hale with { Abilities = ValueList<string>.Of("full_measure") };

    private static string Hall(string boss) => $"""
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
        P recruit:wren 0,0
        P recruit:ivo 0,4
        B {boss} 5,2 group:lord behavior:boss
        E soldier 9,4 group:far behavior:hold
        """;

    private static BattleState Board(string boss = "brigand") => Start(1441, ValueList<Unit>.Of(Captain, Wren, Ivo), Hall(boss));

    private static BattleUnit CaptainOf(BattleState state) => state.Units.Single(u => u.IsCaptain);

    private static BattleUnit BossOf(BattleState state) => state.Units.Single(u => u.IsBoss);

    private static BattleState WithBossHp(BattleState state, int hp) => state.WithUnit(BossOf(state) with { Hp = hp });

    private static BattleState WithCaptainHp(BattleState state, int hp) => state.WithUnit(CaptainOf(state) with { Hp = hp });

    /// <summary>The boss with avoid enough that the art's hit falls under <see cref="HeuristicPlayer.FinisherHit"/>, and no strength, so a whole captain survives his miss.</summary>
    private static BattleState Evasive(BattleState state)
    {
        var boss = BossOf(state);
        return state.WithUnit(boss with { Unit = boss.Unit with { Stats = boss.Unit.Stats with { Lck = 70, Str = 0 } } });
    }

    private static IReadOnlyList<Command> Plan(BattleState state) => HeuristicPlayer.PlanUnit(state, Starter, CaptainOf(state), out _);

    /// <summary>The Hall with Hask swallowed: stage 2, his fall ends the map.</summary>
    private static BattleState StageTwo()
    {
        var board = Board("hask_warden");
        return Swallow.Take(board.WithUnit(BossOf(board) with { Hp = 0 }), Starter, BossOf(board).Id, new List<GameEvent>());
    }

    [Fact]
    public void TheCaptainDeclaresFullMeasureWhenItKillsASwallowedBossOnItsHit()
    {
        var state = WithBossHp(StageTwo(), 1);

        var attack = Assert.Single(Plan(state).OfType<Attack>());

        Assert.True(HeuristicPlayer.EndsTheMap(state, BossOf(state)));
        Assert.Equal(("full_measure", BossOf(state).Id), (attack.Art, attack.TargetId));
    }

    [Fact]
    public void TheFinisherWaitsOnTheTableForABossWithNoStage()
    {
        var state = WithBossHp(Board(), 3);

        Assert.True(HeuristicPlayer.EndsTheMap(state, BossOf(state)));
        Assert.DoesNotContain(Plan(state), c => c is Attack { Art: not null });
    }

    [Fact]
    public void TheFinisherIsNeverDeclaredOnABossWhoseFallIsTheSwallow()
    {
        var state = WithBossHp(Board("hask_warden"), 1);
        Assert.True(Swallow.Takes(BossOf(state)));

        Assert.False(HeuristicPlayer.EndsTheMap(state, BossOf(state)));
        Assert.DoesNotContain(Plan(state), c => c is Attack { Art: not null });
    }

    [Fact]
    public void TheVetoRefusesAFinisherUnderTheHitBarWhenAMissLeavesHimOnALethalTile()
    {
        var state = WithCaptainHp(Evasive(WithBossHp(StageTwo(), 1)), 1);
        var captain = CaptainOf(state);
        var tiles = state.ReachOf(captain, Starter).Destinations.ToList();
        Assert.All(
            tiles.Select(t => Queries.Forecast(state, Starter, captain, BossOf(state), t, null, "full_measure")).OfType<CombatForecast>(),
            f => Assert.True(f.Attacker.HitChance < HeuristicPlayer.FinisherHit));

        Assert.Null(HeuristicPlayer.Finisher(state, Starter, captain, tiles, state.UnitsOf(Side.Enemy).ToList()));
        Assert.NotNull(HeuristicPlayer.Finisher(WithCaptainHp(state, 22), Starter, captain with { Hp = 22 }, tiles, state.UnitsOf(Side.Enemy).ToList()));
    }

    [Fact]
    public void AMissedFullMeasureIsPricedOverBothEnemyPhasesItLeavesHimStanding()
    {
        var state = WithCaptainHp(Evasive(WithBossHp(StageTwo() with { FrozenIronHeld = false }, 1)), 12);
        var captain = CaptainOf(state);
        var boss = BossOf(state);
        var tiles = state.ReachOf(captain, Starter).Destinations.ToList();
        var lethal = tiles.Where(t => Queries.Forecast(state, Starter, captain, boss, t, null, "full_measure") is { } f && f.Attacker.FirstHit(false) >= boss.Hp).ToList();
        Assert.NotEmpty(lethal);
        Assert.Contains(lethal, t => HeuristicPlayer.MissPrice(state, Starter, captain, t, boss, 0, costsNextPhase: false) < captain.Hp);
        Assert.All(lethal, t => Assert.True(HeuristicPlayer.MissPrice(state, Starter, captain, t, boss, 0, costsNextPhase: true) >= captain.Hp));

        Assert.Null(HeuristicPlayer.Finisher(state, Starter, captain, tiles, state.UnitsOf(Side.Enemy).ToList()));
    }

    [Fact]
    public void AtTheHitBarTheFinisherIsDeclaredOnALethalTile()
    {
        var state = WithCaptainHp(WithBossHp(StageTwo(), 1), 1);
        var captain = CaptainOf(state);
        var tiles = state.ReachOf(captain, Starter).Destinations.ToList();

        var finish = HeuristicPlayer.Finisher(state, Starter, captain, tiles, state.UnitsOf(Side.Enemy).ToList());

        Assert.NotNull(finish);
        Assert.True(finish!.Hit >= HeuristicPlayer.FinisherHit);
        Assert.True(Exposure.Of(state, Starter, captain, finish.Tile, BossOf(state), finish.Slot).NoCrit >= captain.Hp);
    }

    [Fact]
    public void InStageTwoTheCaptainEndsClearOfTheSwallowedBossesReach()
    {
        var state = StageTwo();
        var captain = CaptainOf(state);
        var boss = BossOf(state);
        var tiles = state.ReachOf(captain, Starter).Destinations.ToList();
        Assert.True(HeuristicPlayer.InBossReach(state, Starter, boss, captain.At));

        var clear = HeuristicPlayer.Clear(state, Starter, captain, tiles, state.UnitsOf(Side.Enemy).ToList(), captain.At);

        Assert.NotNull(clear);
        Assert.False(HeuristicPlayer.InBossReach(state, Starter, boss, clear!.Value));
    }

    [Fact]
    public void BeforeTheSwallowTheCaptainsWalkIsUnchanged()
    {
        var state = Board("hask_warden");
        var captain = CaptainOf(state);
        var tiles = state.ReachOf(captain, Starter).Destinations.ToList();

        Assert.Null(HeuristicPlayer.Clear(state, Starter, captain, tiles, state.UnitsOf(Side.Enemy).ToList(), captain.At));
    }
}
