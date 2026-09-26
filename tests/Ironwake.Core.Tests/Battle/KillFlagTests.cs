using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 315: a kill that needs the second round is a kill only if the defender's plain
/// counter, thrown between the rounds, cannot kill the attacker first. Section 8's kill
/// flag and <c>threat</c>'s "if all land" read the one predicate,
/// <see cref="CombatForecast.AttackerStrikesLivedFor"/>. A gauntlet's round is two strikes
/// with nothing between them, so its first round is both (issue 70).
/// </summary>
public sealed class KillFlagTests
{
    private static SideForecast Side(int damage, bool doubles, int perRound = 1) =>
        new(true, damage, 90, 90, 0, doubles, perRound);

    [Theory]
    [InlineData(9, true, 1, true, 13, 1, 11, 1)]
    [InlineData(9, true, 1, true, 13, 1, 14, 2)]
    [InlineData(9, true, 1, true, 10, 1, 11, 2)]
    [InlineData(9, true, 1, false, 0, 1, 11, 2)]
    [InlineData(9, false, 1, true, 13, 1, 11, 1)]
    [InlineData(9, false, 2, true, 13, 1, 11, 2)]
    [InlineData(9, true, 2, true, 13, 1, 11, 2)]
    [InlineData(9, true, 2, true, 10, 1, 11, 4)]
    [InlineData(9, true, 1, true, 6, 2, 12, 1)]
    [InlineData(9, true, 1, true, 5, 2, 12, 2)]
    public void TheAttackerCountsOnlyTheStrikesItLivesToMake(int damage, bool doubles, int perRound, bool counters, int counterDamage, int counterPerRound, int attackerHp, int expected)
    {
        var defender = counters ? Side(counterDamage, false, counterPerRound) : SideForecast.None;
        var forecast = new CombatForecast(Side(damage, doubles, perRound), defender, RollScheme.TwoRollAverage);

        Assert.Equal(expected, forecast.AttackerStrikesLivedFor(attackerHp));
        Assert.Equal(damage * expected, forecast.AttackerDamageLivedFor(attackerHp));
    }

    private const string Board = """
        name: Yard
        size: 8x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ........
        ........
        ........
        ........

        units:
        P captain 0,1
        P recruit:armed 3,0
        P recruit:unarmed 3,2
        E soldier 7,3 group:g behavior:hold

        """;

    private static readonly Stats Fast = new(11, 8, 0, 10, 14, 0, 0, 0, 3);
    private static readonly Stats Slow = new(16, 12, 0, 12, 2, 0, 3, 0, 3);

    private static (BattleState State, BattleUnit Fast) Setup(int fastHp)
    {
        var roster = ValueList<Unit>.Of(
            Recruit("fast", Fast with { Hp = fastHp }, "iron_sword"),
            Recruit("armed", Slow, "iron_axe"),
            Recruit("unarmed", Slow));
        var state = Start(map: Board, roster: roster);
        return (state, state.Find("fast")!);
    }

    private static CombatForecast Against(BattleState state, BattleUnit attacker, string target) =>
        Core.Combat.Forecast(
            (attacker with { At = new Coord(3, 1) }).ToCombatant(state.Map, Starter),
            state.Find(target)!.Answering(state, Starter, new Coord(3, 1)),
            1,
            state.Scheme);

    [Fact]
    public void ADoubleWhoseSecondStrikeIsNeededIsNoKillWhenTheCounterBetweenKillsTheAttacker()
    {
        var (state, fast) = Setup(11);
        var forecast = Against(state, fast, "armed");
        Assert.True(forecast.Attacker.Doubles, "the attacker doubles");
        Assert.True(forecast.Attacker.Damage < 16 && forecast.Attacker.Damage * 2 >= 16, $"only both strikes kill: {forecast.Attacker.Damage}");
        Assert.True(forecast.Defender.Strikes && forecast.Defender.Damage >= 11, $"the counter kills: {forecast.Defender.Damage}");

        var score = EnemyAi.Score(state, Starter, fast, new Coord(3, 1), state.Find("armed")!);

        Assert.True(score < EnemyAi.KillBonus, $"a double the attacker does not live to finish priced as a kill: {score}");
    }

    [Fact]
    public void ADoubleThatNeedsItsSecondStrikeIsAKillWhenTheDefenderCannotCounter()
    {
        var (state, fast) = Setup(11);
        var forecast = Against(state, fast, "unarmed");
        Assert.False(forecast.Defender.Strikes);
        Assert.True(forecast.Attacker.Damage < 16 && forecast.Attacker.Damage * 2 >= 16);

        Assert.True(EnemyAi.Score(state, Starter, fast, new Coord(3, 1), state.Find("unarmed")!) >= EnemyAi.KillBonus);
    }

    [Fact]
    public void ADoubleThatNeedsItsSecondStrikeIsAKillWhenTheCounterDoesNotKill()
    {
        var (state, fast) = Setup(30);
        var forecast = Against(state, fast, "armed");
        Assert.True(forecast.Attacker.Doubles);
        Assert.True(forecast.Defender.Strikes && forecast.Defender.Damage < 30);

        Assert.True(EnemyAi.Score(state, Starter, fast, new Coord(3, 1), state.Find("armed")!) >= EnemyAi.KillBonus);
    }

    [Fact]
    public void AFirstStrikeKillIsAKillWhateverTheCounter()
    {
        var (state, fast) = Setup(11);
        var wounded = state.WithUnit(state.Find("armed")! with { Hp = 5 });
        var forecast = Against(wounded, fast, "armed");
        Assert.True(forecast.Attacker.Damage >= 5 && forecast.Defender.Damage >= 11);

        Assert.True(EnemyAi.Score(wounded, Starter, fast, new Coord(3, 1), wounded.Find("armed")!) >= EnemyAi.KillBonus);
    }

    [Fact]
    public void ThreatsIfAllLandStopsAtTheStrikesTheEnemyLivesToMake()
    {
        var (state, fast) = Setup(11);
        var forecast = Against(state, fast, "armed");
        var enemy = fast with { At = new Coord(3, 1) };

        var line = new ThreatLine(enemy, enemy.At, 0, Starter.Weapons["iron_sword"], forecast);
        var survives = new ThreatLine(enemy with { Hp = forecast.Defender.Damage + 1 }, enemy.At, 0, Starter.Weapons["iron_sword"], forecast);

        Assert.Equal(forecast.Attacker.Damage, line.IfAllLand);
        Assert.Equal(forecast.Attacker.Damage * 2, survives.IfAllLand);
        Assert.Equal(forecast.Attacker.Damage, Queries.IfAllLand(new[] { line }));
    }
}
