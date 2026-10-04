using Ironwake.Content;
using Ironwake.Cli;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 991: when one plain strike of the attacker's first round reaches the defender's HP, the counter comes
/// only if every strike of that round misses, so the lethal-counter line names that condition and its chance,
/// read from the displayed hit: <c>100 - hit</c> for one strike, <c>(100 - hit)^2 / 100</c> for a gauntlet,
/// held inside 1 to 99. A kill line above it prints the other side of the chance. The resolver, the planners and
/// <see cref="CombatForecast.CounterIsLethal"/> are unchanged, and the swing still needs <c>!</c> (issue 975).
/// </summary>
public sealed class ConditionedLethalCounterTests
{
    private static SideForecast Side(int damage, int hit = 90, int perRound = 1) =>
        new(true, damage, hit, hit, 0, false, perRound);

    /// <summary>A defender whose counter of 11 kills an attacker at 10 HP, against <paramref name="attacker"/>.</summary>
    private static CombatForecast Against(SideForecast attacker) =>
        new(attacker, Side(11), RollScheme.TwoRollAverage);

    [Theory]
    [InlineData(4, 79, 2, 3, 4)]
    [InlineData(10, 79, 1, 10, 21)]
    [InlineData(10, 79, 2, 10, 4)]
    [InlineData(10, 50, 2, 10, 25)]
    [InlineData(10, 99, 2, 10, 1)]
    [InlineData(10, 1, 2, 10, 98)]
    public void FirstRoundMissChanceIsReadFromTheDisplayedHit(int damage, int hit, int perRound, int defenderHp, int miss)
    {
        Assert.Equal(miss, Against(Side(damage, hit, perRound)).FirstRoundMissChance(defenderHp));
    }

    [Theory]
    [InlineData(9, 79, 2, 10)]
    [InlineData(9, 79, 1, 10)]
    [InlineData(10, 0, 1, 10)]
    [InlineData(10, 100, 2, 10)]
    public void FirstRoundMissChanceIsNullUnlessOneStrikeKillsAndTheHitIsUncertain(int damage, int hit, int perRound, int defenderHp)
    {
        Assert.Null(Against(Side(damage, hit, perRound)).FirstRoundMissChance(defenderHp));
    }

    [Fact]
    public void FirstRoundMissChanceIsNullWhenTheAttackerCannotStrike()
    {
        Assert.Null(Against(SideForecast.None).FirstRoundMissChance(1));
    }

    private const string Yard = "name: Yard\nsize: 6x3\nwin: rout\nturn_limit: 10\nrecall: 3\nenemy_level: 1\n\n"
        + "......\n......\n......\n\n"
        + "units:\nP captain 0,1\nE soldier 2,1 group:a behavior:hold\n";

    /// <summary>The captain at 10 HP beside the soldier at <paramref name="soldierHp"/>, or at full HP when null.</summary>
    private static (BattleState State, BattleUnit Captain, BattleUnit Soldier) Board(int? soldierHp)
    {
        var content = Starter;
        var state = BattleState.From(MapFormat.Parse("yard.map", Yard, content), content, content.Cast, 7);
        var captain = state.Find("captain")! with { Hp = 10, At = new Coord(1, 1) };
        var soldier = state.Find("soldier-1")!;
        soldier = soldier with { Hp = soldierHp ?? soldier.Hp };
        state = state.WithUnit(captain).WithUnit(soldier);
        return (state, captain, soldier);
    }

    [Fact]
    public void AGauntletWhoseSingleStrikeKillsGetsTheSquaredChance()
    {
        var (_, captain, soldier) = Board(3);
        var forecast = Against(Side(4, hit: 79, perRound: 2));

        Assert.Equal("  Kills if any strike of the first round lands (96)", PlaySession.FirstRoundKillLine(captain, soldier, forecast, raises: false));
        Assert.Equal("  Counter: lethal to captain only if the first round misses (4 in 100) (11 against 10 hp)", PlaySession.LethalCounterLine(captain, soldier, forecast, raises: false));
    }

    [Fact]
    public void ASwordWhoseStrikeKillsGetsOneHundredLessTheHit()
    {
        var (_, captain, soldier) = Board(3);
        var forecast = Against(Side(4, hit: 79));

        Assert.Equal("  Kills if the first strike lands (79)", PlaySession.FirstRoundKillLine(captain, soldier, forecast, raises: false));
        Assert.Equal("  Counter: lethal to captain only if the first round misses (21 in 100) (11 against 10 hp)", PlaySession.LethalCounterLine(captain, soldier, forecast, raises: false));
    }

    [Fact]
    public void AnAttackerWhoNeedsBothStrikesKeepsThePlainLine()
    {
        var (_, captain, soldier) = Board(6);
        var forecast = Against(Side(4, hit: 79, perRound: 2));

        Assert.Null(PlaySession.FirstRoundKillLine(captain, soldier, forecast, raises: false));
        Assert.Equal("  Counter: lethal to captain (11 against 10 hp)", PlaySession.LethalCounterLine(captain, soldier, forecast, raises: false));
    }

    [Fact]
    public void ThePlainLineFiresWhenTheStrikeFallsOneShort()
    {
        var (_, captain, soldier) = Board(5);
        var kills = Against(Side(5, hit: 79));
        var oneShort = Against(Side(4, hit: 79));

        Assert.Contains("only if the first round misses", PlaySession.LethalCounterLine(captain, soldier, kills, raises: false));
        Assert.Equal("  Counter: lethal to captain (11 against 10 hp)", PlaySession.LethalCounterLine(captain, soldier, oneShort, raises: false));
        Assert.Null(PlaySession.FirstRoundKillLine(captain, soldier, oneShort, raises: false));
    }

    [Fact]
    public void ACounterThatCannotKillDrawsNoKillLine()
    {
        var (_, captain, soldier) = Board(3);
        var forecast = new CombatForecast(Side(4, hit: 79), Side(9), RollScheme.TwoRollAverage);

        Assert.Null(PlaySession.LethalCounterLine(captain, soldier, forecast, raises: false));
        Assert.Null(PlaySession.FirstRoundKillLine(captain, soldier, forecast, raises: false));
    }

    [Fact]
    public void TheRefusalQuotesTheConditionAndStillAsksForTheBang()
    {
        var (_, captain, soldier) = Board(3);
        var line = PlaySession.LethalCounterLine(captain, soldier, Against(Side(4, hit: 79, perRound: 2)), raises: false)!;

        Assert.Equal("counter is lethal to captain only if the first round misses (4 in 100) (11 against 10 hp); add ! to swing anyway: ", PlaySession.LethalSwingRefusal(line));
    }

    [Fact]
    public void TheForecastPrintsTheKillLineAboveTheConditionedCounter()
    {
        var (state, captain, _) = Board(1);
        captain = captain with { Hp = 1 };
        state = state.WithUnit(captain);
        var soldier = state.Find("soldier-1")!;
        var forecast = Queries.Forecast(state, Starter, captain, soldier)!;
        var miss = forecast.FirstRoundMissChance(1)!.Value;

        var lines = PlaySession.ForecastText(state, Starter, captain, soldier, forecast, captain.At, fromTile: false).Split('\n');

        Assert.StartsWith("  Kills if", lines[1]);
        Assert.EndsWith($"({100 - miss})", lines[1]);
        Assert.Equal($"  Counter: lethal to Alder Fenn only if the first round misses ({miss} in 100) ({forecast.CounterIfAllLand} against 1 hp)", lines[2]);
    }

    [Fact]
    public void TheProtocolsForecastCarriesTheMissChance()
    {
        var (state, captain, _) = Board(1);
        state = state.WithUnit(captain with { Hp = 1 });
        var conditioned = new ProtocolSession(Starter, state, new StringWriter()).Answer("""{"query":"forecast","unit":"captain","target":"soldier-1"}""");
        var forecast = Queries.Forecast(state, Starter, state.Find("captain")!, state.Find("soldier-1")!)!;
        var (plainState, _, _) = Board(null);
        var plain = new ProtocolSession(Starter, plainState, new StringWriter()).Answer("""{"query":"forecast","unit":"captain","target":"soldier-1"}""");

        Assert.Contains($"\"counterLethalIfMiss\":{forecast.FirstRoundMissChance(1)}", conditioned);
        Assert.Contains("\"counterLethalIfMiss\":null", plain);
    }
}
