using Ironwake.Content;
using Ironwake.Cli;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 539: the forecast says when the counter kills the attacker, on the deterministic reading
/// <c>threat</c>'s "if all land" uses: the counter's plain damage over every strike it can make
/// against the attacker's HP, no crit counted, and never when the attacker's first round is
/// certain to kill first. The protocol's forecast answer carries the same verdict as
/// <c>counterLethal</c>.
/// </summary>
public sealed class LethalCounterTests
{
    private static SideForecast Side(int damage, bool doubles = false, int perRound = 1, int hit = 90) =>
        new(true, damage, hit, hit, 0, doubles, perRound);

    [Theory]
    [InlineData(17, false, 1, 17, true)]
    [InlineData(16, false, 1, 17, false)]
    [InlineData(9, true, 1, 17, true)]
    [InlineData(8, true, 1, 17, false)]
    [InlineData(5, false, 2, 10, true)]
    [InlineData(5, true, 2, 20, true)]
    [InlineData(5, true, 2, 21, false)]
    public void ForecastFlagsLethalCounter(int counterDamage, bool counterDoubles, int counterPerRound, int attackerHp, bool lethal)
    {
        var forecast = new CombatForecast(Side(6), Side(counterDamage, counterDoubles, counterPerRound), RollScheme.TwoRollAverage);

        Assert.Equal(lethal, forecast.CounterIsLethal(attackerHp, defenderHp: 30));
    }

    [Fact]
    public void AKillThatNeedsACritIsNotFlagged()
    {
        var counter = new SideForecast(true, 6, 90, 90, 30, false);
        var forecast = new CombatForecast(Side(6), counter, RollScheme.TwoRollAverage);

        Assert.False(forecast.CounterIsLethal(attackerHp: 17, defenderHp: 30));
    }

    [Fact]
    public void NoCounterIsNeverLethal()
    {
        var forecast = new CombatForecast(Side(6), SideForecast.None, RollScheme.TwoRollAverage);

        Assert.False(forecast.CounterIsLethal(attackerHp: 1, defenderHp: 30));
        Assert.Equal(0, forecast.CounterIfAllLand);
    }

    [Fact]
    public void ACertainFirstRoundKillDrawsNoLethalFlag()
    {
        var sure = new CombatForecast(Side(12, hit: 100), Side(20), RollScheme.TwoRollAverage);
        var unsure = new CombatForecast(Side(12, hit: 99), Side(20), RollScheme.TwoRollAverage);
        var short1 = new CombatForecast(Side(11, hit: 100), Side(20), RollScheme.TwoRollAverage);

        Assert.False(sure.CounterIsLethal(attackerHp: 17, defenderHp: 12));
        Assert.True(unsure.CounterIsLethal(attackerHp: 17, defenderHp: 12));
        Assert.True(short1.CounterIsLethal(attackerHp: 17, defenderHp: 12));
    }

    private const string Yard = "name: Yard\nsize: 6x3\nwin: rout\nturn_limit: 10\nrecall: 3\nenemy_level: 1\n\n"
        + "......\n......\n......\n\n"
        + "units:\nP captain 0,1\nE soldier 2,1 group:a behavior:hold\n";

    /// <summary>The captain beside the soldier, at <paramref name="captainHp"/> or at full HP when null.</summary>
    private static BattleState Board(int? captainHp)
    {
        var content = Starter;
        var state = BattleState.From(MapFormat.Parse("yard.map", Yard, content), content, content.Cast, 7);
        var captain = state.Find("captain")!;
        return state.WithUnit(captain with { Hp = captainHp ?? captain.Hp, At = new Coord(1, 1) });
    }

    [Fact]
    public void TheForecastPrintsTheLethalCounterLineUnderTheForecastLine()
    {
        var state = Board(1);
        var captain = state.Find("captain")!;
        var soldier = state.Find("soldier-1")!;
        var forecast = Queries.Forecast(state, Starter, captain, soldier)!;

        var lines = PlaySession.ForecastText(state, Starter, captain, soldier, forecast, captain.At, fromTile: false).Split('\n');

        Assert.Equal($"  Counter: lethal to Alder Fenn ({forecast.CounterIfAllLand} against 1 hp)", lines[1]);
    }

    [Fact]
    public void ACounterThatCannotKillPrintsNoLine()
    {
        var state = Board(null);
        var captain = state.Find("captain")!;
        var soldier = state.Find("soldier-1")!;
        var forecast = Queries.Forecast(state, Starter, captain, soldier)!;

        Assert.DoesNotContain("lethal", PlaySession.ForecastText(state, Starter, captain, soldier, forecast, captain.At, fromTile: false));
    }

    [Fact]
    public void TheProtocolsForecastCarriesCounterLethal()
    {
        var lethal = new ProtocolSession(Starter, Board(1), new StringWriter()).Answer("""{"query":"forecast","unit":"captain","target":"soldier-1"}""");
        var safe = new ProtocolSession(Starter, Board(null), new StringWriter()).Answer("""{"query":"forecast","unit":"captain","target":"soldier-1"}""");

        Assert.Contains("\"counterLethal\":true", lethal);
        Assert.Contains("\"counterLethal\":false", safe);
    }
}
