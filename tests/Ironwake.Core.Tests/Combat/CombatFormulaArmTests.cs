using Ironwake.Core.Tests.Battle;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Combat;

using static CombatFixture;

/// <summary>
/// Issue 158's formula arms, worked on the fixture's numbers. Wren (Str 7, Spd 8, Lck 5,
/// Dex 6, iron sword wt 5 hit 90) on plain against the brigand (Str 7, Spd 4, Lck 1, Dex 3,
/// heavy axe wt 8 hit 70) in a forest (avoid 20). Standard: burdens 4 and 7, attack speeds
/// 4 and -3, avoids 6 and 17, hits 98 and 73. Full Str: burdens 0 and 1, attack speeds 8
/// and 3, avoids 10 and 23. Speed twice: avoids 18 and 26.
/// </summary>
public class CombatFormulaArmTests
{
    [Theory]
    [InlineData(CombatFormula.Standard, 4, 4, 81, 67)]
    [InlineData(CombatFormula.FullStrBurden, 0, 8, 75, 63)]
    [InlineData(CombatFormula.FullStrBurdenSpeedTwice, 0, 8, 72, 55)]
    public void BurdenAvoidAndDoublingFollowTheFormula(CombatFormula formula, int wrenBurden, int wrenSpeed, int wrenHits, int brigandHits)
    {
        var wren = WrenOnPlain();
        var brigand = BrigandInForest();

        Assert.Equal(wrenBurden, Core.Combat.Burden(wren, formula));
        Assert.Equal(wrenSpeed, Core.Combat.AttackSpeed(wren, formula));
        Assert.Equal(wrenHits, Core.Combat.HitChance(wren, brigand, formula));
        Assert.Equal(brigandHits, Core.Combat.HitChance(brigand, wren, formula));
        Assert.True(Core.Combat.Doubles(wren, brigand, formula));
        Assert.False(Core.Combat.Doubles(brigand, wren, formula));

        var forecast = Core.Combat.Forecast(wren, brigand, 1, RollScheme.OneRoll, formula);
        Assert.Equal(wrenHits, forecast.Attacker.HitChance);
        Assert.Equal(brigandHits, forecast.Defender.HitChance);
        Assert.True(forecast.Attacker.Doubles);
    }

    [Fact]
    public void MagicAvoidIgnoresTheFormula()
    {
        var hexer = HexerOnPlain();
        var wren = WrenOnPlain();
        foreach (var formula in Enum.GetValues<CombatFormula>())
        {
            Assert.Equal(Core.Combat.HitChance(hexer, wren, CombatFormula.Standard), Core.Combat.HitChance(hexer, wren, formula));
        }
    }

    private const string Duel = """
        name: Duel
        size: 3x1
        win: rout
        turn_limit: 10
        recall: 0
        enemy_level: 1

        ...

        units:
        P captain 0,0
        E brigand 1,0 group:yard behavior:aggressive

        """;

    /// <summary>
    /// The state's formula reaches the query the player reads and the resolver the game
    /// plays. On the duel map the captain's raw hit on the brigand is 100 under the standard
    /// formula and under 100 under arm 2, so over a hundred one-roll games the standard
    /// formula never misses and arm 2 does.
    /// </summary>
    [Fact]
    public void TheStatesFormulaReachesTheForecastAndTheResolver()
    {
        var map = MapFixture.Parse(Duel);
        var misses = new Dictionary<CombatFormula, int>();
        foreach (var formula in new[] { CombatFormula.Standard, CombatFormula.FullStrBurdenSpeedTwice })
        {
            misses[formula] = 0;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var state = BattleState.From(map, BattleFixture.Starter, BattleFixture.Roster, seed, RollScheme.OneRoll, formula: formula);
                var captain = state.UnitsOf(Side.Player).Single();
                var brigand = state.UnitsOf(Side.Enemy).Single();
                var query = Queries.Forecast(state, BattleFixture.Starter, captain, brigand)!;
                var direct = Core.Combat.Forecast(captain.ToCombatant(map, BattleFixture.Starter), brigand.ToCombatant(map, BattleFixture.Starter), 1, RollScheme.OneRoll, formula);
                Assert.Equal(direct, query);

                var result = Resolver.Apply(state, BattleFixture.Starter, new Attack(captain.Id, brigand.Id));
                Assert.True(result.Accepted);
                var fought = result.Events.OfType<CombatFought>().Single();
                misses[formula] += fought.Strikes.Count(s => s.AttackerId == captain.Id && !s.Hit);
            }
        }

        Assert.Equal(0, misses[CombatFormula.Standard]);
        Assert.True(misses[CombatFormula.FullStrBurdenSpeedTwice] > 0);
    }
}
