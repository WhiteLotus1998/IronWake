using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 69's shipped masteries, DECISIONS/0047: every starter class names one ability from
/// issue 66's effect set and the combats that earn it, 12 for a class that fights and 8 for
/// the chaplain, whose heals are not combats. The yard's brigand is infantry with an iron axe.
/// </summary>
public class ShippedMasteryTests
{
    private static BattleState Beside(Unit captain) =>
        BattleState.From(YardMap, Starter, ValueList<Unit>.Of(captain, Wren), 7).Do(new Move("hale", new Coord(2, 1)));

    private static CombatForecast Forecast(BattleState state) =>
        Queries.Forecast(state, Starter, state.Find("hale")!, state.Find("brigand-1")!)!;

    private static Unit Knowing(string ability) => Hale with { Abilities = ValueList<string>.Of(ability) };

    [Theory]
    [InlineData("cadet", "fundamentals", 12)]
    [InlineData("pikeman", "horsebane", 12)]
    [InlineData("reaver", "bloodrush", 12)]
    [InlineData("bowman", "deadeye", 12)]
    [InlineData("adept", "deep_study", 12)]
    [InlineData("chaplain", "grace", 8)]
    [InlineData("outrider", "lancebreaker", 12)]
    [InlineData("skyrider", "bowbreaker", 12)]
    [InlineData("bulwark", "reasonbreaker", 12)]
    public void EveryStarterClassMastersOneAbilityAtItsRequirement(string classId, string ability, int points)
    {
        var unitClass = Starter.Class(classId);

        Assert.Equal(ability, unitClass.Mastery);
        Assert.Equal(points, unitClass.MasteryPoints);
        Assert.Equal(ability, Starter.Ability(ability).Id);
    }

    [Fact]
    public void NoTwoStarterClassesMasterTheSameAbility()
    {
        var masteries = Starter.Classes.Values.Select(c => c.Mastery).ToList();

        Assert.Equal(9, masteries.Count);
        Assert.DoesNotContain(null, masteries);
        Assert.Equal(masteries.Count, masteries.Distinct().Count());
    }

    [Fact]
    public void FundamentalsAddsOneStrSpdAndDef()
    {
        var plain = Starter.StatsOf(Hale);
        var drilled = Starter.StatsOf(Knowing("fundamentals"));

        Assert.Equal(Stats.Zero.With(Stat.Str, 1).With(Stat.Spd, 1).With(Stat.Def, 1), drilled - plain);
    }

    [Fact]
    public void DeepStudyAddsTwoMag()
    {
        Assert.Equal(Stats.Zero.With(Stat.Mag, 2), Starter.StatsOf(Knowing("deep_study")) - Starter.StatsOf(Hale));
    }

    [Fact]
    public void BloodrushTradesTenAvoidForFifteenCrit()
    {
        var plain = Forecast(Beside(Hale));
        var rushed = Forecast(Beside(Knowing("bloodrush")));

        Assert.Equal(Math.Clamp(plain.Attacker.CritChance + 15, 0, 100), rushed.Attacker.CritChance);
        Assert.Equal(Math.Clamp(plain.Defender.HitChance + 10, 0, 100), rushed.Defender.HitChance);
        Assert.Equal(plain.Attacker.HitChance, rushed.Attacker.HitChance);
    }

    [Fact]
    public void DeadeyeAddsTenHitAndTenCrit()
    {
        var plain = Forecast(Beside(Hale));
        var aimed = Forecast(Beside(Knowing("deadeye")));

        Assert.Equal(Math.Clamp(plain.Attacker.HitChance + 10, 0, 100), aimed.Attacker.HitChance);
        Assert.Equal(Math.Clamp(plain.Attacker.CritChance + 10, 0, 100), aimed.Attacker.CritChance);
        Assert.Equal(plain.Defender.HitChance, aimed.Defender.HitChance);
    }

    [Fact]
    public void GraceAddsTenAvoidAndTwentyCritAvoid()
    {
        Assert.Equal(new CombatModifierEffect(OpponentCondition.Any, 0, 10, 0, 20), Starter.Ability("grace").Effect);

        var plain = Forecast(Beside(Hale));
        var graced = Forecast(Beside(Knowing("grace")));

        Assert.Equal(Math.Clamp(plain.Defender.HitChance - 10, 0, 100), graced.Defender.HitChance);
        Assert.Equal(Math.Clamp(plain.Defender.CritChance - 20, 0, 100), graced.Defender.CritChance);
    }

    [Fact]
    public void HorsebaneReadsOnlyAgainstCavalry()
    {
        Assert.Equal(new CombatModifierEffect(new OpponentCondition(null, MovementType.Cavalry), 20, 0, 10, 0), Starter.Ability("horsebane").Effect);
        Assert.Equal(Forecast(Beside(Hale)), Forecast(Beside(Knowing("horsebane"))));
    }
}
