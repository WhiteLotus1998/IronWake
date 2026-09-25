using Ironwake.Cli;
using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 70 on the board: a gauntlet spends one use a combat, section 8's scorer prices a
/// gauntlet's strikes the way it prices a sword's, and the console shows every strike.
/// The gauntlet here is test content, the iron sword's numbers under the gauntlet type,
/// and the cadet is widened to wield it; no shipped unit carries one yet.
/// </summary>
public class GauntletBattleTests
{
    private static readonly Weapon TestGauntlet = Starter.Weapon("iron_sword") with { Id = "test_gauntlet", Name = "Test Gauntlet", Type = WeaponType.Gauntlet };

    private static readonly GameContent Fists = Starter with
    {
        Weapons = Starter.Weapons.Add(TestGauntlet.Id, TestGauntlet),
        Classes = Starter.Classes.SetItem("cadet", Starter.Class("cadet") with { Weapons = Starter.Class("cadet").Weapons.Add(WeaponType.Gauntlet) }),
    };

    private static BattleState Beside(string weapon)
    {
        var hale = Hale with { Inventory = Inventory.Empty.Add(new ItemStack(weapon, 40)) };
        var state = BattleState.From(YardMap, Fists, ValueList<Unit>.Of(hale, Wren), 7);
        return Do(state, new Move("hale", new Coord(2, 1)));
    }

    private static BattleState Do(BattleState state, Command command)
    {
        var result = Resolver.Apply(state, Fists, command);
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result.Next;
    }

    [Fact]
    public void AGauntletSpendsOneUseForTheCombatHoweverManyStrikesItMade()
    {
        var before = Beside("test_gauntlet");
        var forecast = Queries.Forecast(before, Fists, before.Find("hale")!, before.Find("brigand-1")!)!;
        Assert.Equal(2, forecast.Attacker.StrikesPerRound);

        var result = Resolver.Apply(before, Fists, new Attack("hale", "brigand-1"));

        var fought = Assert.Single(result.Events.OfType<CombatFought>());
        Assert.True(fought.Strikes.Count(s => s.AttackerId == "hale") >= 2);
        Assert.Equal(39, result.Next.Find("hale")!.Unit.Inventory.Items[0].Uses);
    }

    [Fact]
    public void ASwordStillSpendsOneUseAStrike()
    {
        var result = Resolver.Apply(Beside("iron_sword"), Fists, new Attack("hale", "brigand-1"));

        var made = result.Events.OfType<CombatFought>().Single().Strikes.Count(s => s.AttackerId == "hale");
        Assert.Equal(40 - made, result.Next.Find("hale")!.Unit.Inventory.Items[0].Uses);
    }

    [Fact]
    public void SectionEightScoresAGauntletAndASwordOfEqualLevelTheSameWay()
    {
        foreach (var weapon in new[] { "test_gauntlet", "iron_sword" })
        {
            var state = Beside(weapon);
            var hale = state.Find("hale")!;
            var brigand = state.Find("brigand-1")!;
            var forecast = Queries.Forecast(state, Fists, hale, brigand)!;

            Assert.Equal(Priced(forecast, state.Scheme, hale.Hp, brigand.Hp), EnemyAi.Score(state, Fists, hale, hale.At, brigand), 9);
        }

        var gauntlet = Queries.Forecast(Beside("test_gauntlet"), Fists, Beside("test_gauntlet").Find("hale")!, Beside("test_gauntlet").Find("brigand-1")!)!;
        var sword = Queries.Forecast(Beside("iron_sword"), Fists, Beside("iron_sword").Find("hale")!, Beside("iron_sword").Find("brigand-1")!)!;
        Assert.Equal(2 * sword.Attacker.StrikeCount, gauntlet.Attacker.StrikeCount);
    }

    [Fact]
    public void TheForecastLineAndThreatTotalShowEveryStrike()
    {
        var state = Beside("test_gauntlet");
        var hale = state.Find("hale")!;
        var brigand = state.Find("brigand-1")!;
        var forecast = Queries.Forecast(state, Fists, hale, brigand)!;

        Assert.Contains($"dmg {forecast.Attacker.Damage} x{forecast.Attacker.StrikeCount} hit", PlaySession.ForecastLine(hale, brigand, forecast));
        Assert.Equal(forecast.Attacker.Damage * forecast.Attacker.StrikeCount, new ThreatLine(hale, hale.At, 0, TestGauntlet, forecast).IfAllLand);
    }

    [Fact]
    public void TheSimsKillChanceCountsBothStrikesOfTheFirstRound()
    {
        var state = Beside("test_gauntlet");
        var hale = state.Find("hale")!;
        var brigand = state.Find("brigand-1")!;
        var forecast = Queries.Forecast(state, Fists, hale, brigand)!;
        var low = state.WithUnit(brigand with { Hp = forecast.Attacker.Damage + 1 });

        var chance = HeuristicPlayer.KillProbability(low, Fists, hale, hale.At, low.Find("brigand-1")!);

        // One plain hit leaves 1 hp and a crit kills; a second landed strike always kills.
        var hit = Core.Combat.HitProbability(forecast.Attacker.HitChance, state.Scheme);
        var crit = forecast.Attacker.CritChance / 100.0;
        var firstKills = hit * crit;
        var secondKills = hit * (1 - crit) * hit;
        Assert.True(chance >= firstKills + secondKills - 1e-9, $"kill chance {chance} below the first round's {firstKills + secondKills}");
    }

    private static double Priced(CombatForecast forecast, RollScheme scheme, int attackerHp, int targetHp)
    {
        var side = forecast.Attacker;
        var score = (side.Damage * side.StrikeCount >= targetHp ? EnemyAi.KillBonus : 0)
            + Math.Min(targetHp, side.Damage * (1 + 2 * side.CritChance / 100.0) * side.StrikeCount) * Core.Combat.HitProbability(side.HitChance, scheme);
        if (!forecast.Defender.Strikes)
        {
            return score + EnemyAi.NoCounterBonus;
        }

        var counter = forecast.Defender;
        return score - Math.Min(attackerHp, counter.Damage * (1 + 2 * counter.CritChance / 100.0) * counter.StrikeCount) * Core.Combat.HitProbability(counter.HitChance, scheme) * EnemyAi.CounterWeight;
    }
}
