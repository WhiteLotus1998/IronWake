using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 69's Breakers, DESIGN section 5: one ability per weapon type, +20 hit and +20
/// avoid against that type, on the strike and on the counter, in the forecast and the
/// resolver through the one hit chance they share. The yard's brigand carries an iron axe.
/// </summary>
public class BreakerTests
{
    private static readonly Unit Breaker = Hale with { Abilities = ValueList<string>.Of("axebreaker") };

    private static BattleState Beside(Unit captain, ulong seed = 7) =>
        BattleState.From(YardMap, Starter, ValueList<Unit>.Of(captain, Wren), seed).Do(new Move("hale", new Coord(2, 1)));

    private static CombatForecast Forecast(BattleState state, string attacker, string target) =>
        Queries.Forecast(state, Starter, state.Find(attacker)!, state.Find(target)!)!;

    [Theory]
    [InlineData(WeaponType.Sword, "swordbreaker")]
    [InlineData(WeaponType.Lance, "lancebreaker")]
    [InlineData(WeaponType.Axe, "axebreaker")]
    [InlineData(WeaponType.Bow, "bowbreaker")]
    [InlineData(WeaponType.Reason, "reasonbreaker")]
    [InlineData(WeaponType.Faith, "faithbreaker")]
    public void ContentShipsABreakerForEveryWeaponTypeAtTwentyHitAndTwentyAvoid(WeaponType type, string id)
    {
        Assert.Equal(new CombatModifierEffect(new OpponentCondition(type, null), 20, 20, 0, 0), Starter.Ability(id).Effect);
    }

    [Fact]
    public void EveryWeaponTypeHasExactlyOneBreaker()
    {
        var breakers = Starter.Abilities.Values
            .Select(a => a.Effect)
            .OfType<CombatModifierEffect>()
            .Where(m => m.Against.Weapon is not null && m.Hit == 20 && m.Avoid == 20)
            .Select(m => m.Against.Weapon!.Value)
            .ToList();

        Assert.Equal(Enum.GetValues<WeaponType>().OrderBy(t => t), breakers.OrderBy(t => t));
    }

    [Fact]
    public void ABreakerAddsTwentyHitAndTwentyAvoidOnTheStrike()
    {
        var plain = Forecast(Beside(Hale), "hale", "brigand-1");
        var broken = Forecast(Beside(Breaker), "hale", "brigand-1");

        Assert.Equal(Math.Clamp(plain.Attacker.HitChance + 20, 0, 100), broken.Attacker.HitChance);
        Assert.Equal(Math.Clamp(plain.Defender.HitChance - 20, 0, 100), broken.Defender.HitChance);
        Assert.Equal(plain.Attacker.Damage, broken.Attacker.Damage);
        Assert.Equal(plain.Attacker.CritChance, broken.Attacker.CritChance);
    }

    [Fact]
    public void ABreakerAddsTwentyHitAndTwentyAvoidOnTheCounter()
    {
        var plain = Beside(Hale) with { Phase = Side.Enemy };
        var broken = Beside(Breaker) with { Phase = Side.Enemy };

        var before = Forecast(plain, "brigand-1", "hale");
        var after = Forecast(broken, "brigand-1", "hale");

        Assert.True(after.Defender.Strikes);
        Assert.Equal(Math.Clamp(before.Defender.HitChance + 20, 0, 100), after.Defender.HitChance);
        Assert.Equal(Math.Clamp(before.Attacker.HitChance - 20, 0, 100), after.Attacker.HitChance);
    }

    [Fact]
    public void ABreakerDoesNothingAgainstAnotherWeaponType()
    {
        var lances = Hale with { Abilities = ValueList<string>.Of("lancebreaker") };

        Assert.Equal(Forecast(Beside(Hale), "hale", "brigand-1"), Forecast(Beside(lances), "hale", "brigand-1"));
    }

    [Fact]
    public void ABreakerResolvesAtTheHitItsForecastPrinted()
    {
        const int seeds = 600;
        var forecast = Forecast(Beside(Breaker), "hale", "brigand-1");
        var expected = Ironwake.Core.Combat.HitProbability(forecast.Attacker.HitChance, forecast.Scheme);
        var hits = 0;
        for (var seed = 1UL; seed <= seeds; seed++)
        {
            var result = Beside(Breaker, seed).Try(new Attack("hale", "brigand-1"));
            Assert.True(result.Accepted, result.Rejection?.Message);
            hits += result.Events.OfType<CombatFought>().Single().Strikes.First(s => s.AttackerId == "hale").Hit ? 1 : 0;
        }

        var error = Math.Sqrt(expected * (1 - expected) / seeds);
        Assert.InRange((double)hits / seeds, expected - 3 * error - 1e-9, expected + 3 * error + 1e-9);
    }
}
