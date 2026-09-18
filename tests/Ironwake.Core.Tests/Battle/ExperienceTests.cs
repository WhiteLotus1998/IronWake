using Ironwake.Core.Tests.Combat;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>DESIGN.md section 6: the EXP formula with the doc's numbers, keyed level-ups, the cap, and who earns (issue 8).</summary>
public class ExperienceTests
{
    [Theory]
    [InlineData(-10, 1, 6)]
    [InlineData(0, 10, 30)]
    [InlineData(10, 30, 80)]
    [InlineData(-4, 2, 10)]
    [InlineData(5, 20, 55)]
    public void TheExpFormulaAtLevelDifferences(int diff, int strikeOnly, int strikeAndKill)
    {
        const int unitLevel = 15;
        Assert.Equal(strikeOnly, Experience.ForCombat(unitLevel, unitLevel + diff, landed: true, killed: false, boss: false));
        Assert.Equal(strikeAndKill, Experience.ForCombat(unitLevel, unitLevel + diff, landed: true, killed: true, boss: false));
        Assert.Equal(strikeAndKill + 20, Experience.ForCombat(unitLevel, unitLevel + diff, landed: true, killed: true, boss: true));
        Assert.Equal(0, Experience.ForCombat(unitLevel, unitLevel + diff, landed: false, killed: false, boss: false));
    }

    [Fact]
    public void ALevelUpRollsEachStatAgainstItsEffectiveGrowthUnderTheGrowthKey()
    {
        var cadet = Starter.Class("cadet");
        var unit = Hale with { Growths = new Stats(80, 50, 5, 40, 40, 30, 30, 20, 20) };
        var rng = new ScriptedRng(defaultRoll: 35)
            .Set(RollKey.Growth("hale", 2, Stat.Hp), 79)
            .Set(RollKey.Growth("hale", 2, Stat.Mag), 4)
            .Set(RollKey.Growth("hale", 2, Stat.Def), 30);

        var (next, gains) = unit.LevelUp(cadet, rng);

        Assert.Equal(2, next.Level);
        Assert.Equal(new Stats(1, 1, 1, 1, 1, 0, 0, 0, 0), gains);
        Assert.Equal(unit.Stats + gains, next.Stats);
        Assert.Equal(9, rng.Asked.Count);
        Assert.All(rng.Asked, key => Assert.StartsWith("growth/hale/2/", key));
    }

    [Fact]
    public void GainingExpCarriesTheRemainderAndCanCrossTwoLevels()
    {
        var cadet = Starter.Class("cadet");
        var rng = new ScriptedRng(defaultRoll: 99);

        var one = (Hale with { Exp = 90 }).GainExp(25, cadet, rng);
        Assert.Equal(2, one.Unit.Level);
        Assert.Equal(15, one.Unit.Exp);
        Assert.Equal(new[] { 2 }, one.LevelUps.Select(l => l.NewLevel));

        var two = (Hale with { Exp = 95 }).GainExp(120, cadet, rng);
        Assert.Equal(3, two.Unit.Level);
        Assert.Equal(15, two.Unit.Exp);
        Assert.Equal(new[] { 2, 3 }, two.LevelUps.Select(l => l.NewLevel));
    }

    [Fact]
    public void TheLevelCapStopsExpAndDiscardsTheRemainder()
    {
        var cadet = Starter.Class("cadet");
        var rng = new ScriptedRng(defaultRoll: 99);

        var capped = (Hale with { Level = 30, Exp = 40 }).GainExp(50, cadet, rng);
        Assert.Equal(Hale.Stats, capped.Unit.Stats);
        Assert.Equal(40, capped.Unit.Exp);
        Assert.Empty(capped.LevelUps);
        Assert.Empty(rng.Asked);

        var reaching = (Hale with { Level = 29, Exp = 99 }).GainExp(70, cadet, rng);
        Assert.Equal(30, reaching.Unit.Level);
        Assert.Equal(0, reaching.Unit.Exp);
        Assert.Single(reaching.LevelUps);
        Assert.Throws<InvalidOperationException>(() => reaching.Unit.LevelUp(cadet, rng));
    }

    [Fact]
    public void APlayerUnitEarnsExpFromItsBestOutcomeAndAnEnemyEarnsNothing()
    {
        var state = Start().WithUnit(Start().Find("brigand-1")! with { Hp = 5 });
        var result = state.Do(new Move("hale", new Coord(2, 1))).Try(new Attack("hale", "brigand-1"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var gained = Assert.Single(result.Events.OfType<ExpGained>());
        Assert.Equal("hale", gained.UnitId);
        Assert.Equal(30, gained.Amount);
        Assert.Equal(30, gained.ExpAfter);
        Assert.Equal(30, result.Next.Find("hale")!.Unit.Exp);
        Assert.Contains(result.Events, e => e is UnitDied { UnitId: "brigand-1" });
        Assert.True(result.Events.ToList().FindIndex(e => e is ExpGained) < result.Events.ToList().FindIndex(e => e is UnitDied));
        Assert.Contains("unit hale Player 2,1 hp 22 moved acted class cadet level 1 exp 30", result.Next.Canonical());
    }

    [Fact]
    public void TheDefenderEarnsExpForItsCounterAndNothingWhenNothingLands()
    {
        var state = Start().Do(new Move("hale", new Coord(2, 1))).Do(new Wait("hale")).Do(new Wait("wren")).Do(new EndPhase());
        var result = state.Try(new Attack("brigand-1", "hale"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var fought = result.Events.OfType<CombatFought>().Single();
        var landed = fought.Strikes.Any(s => s.AttackerId == "hale" && s.Hit);
        var gained = result.Events.OfType<ExpGained>().SingleOrDefault();
        Assert.Equal(landed, gained is not null);
        if (gained is not null)
        {
            Assert.Equal("hale", gained.UnitId);
            Assert.Equal(Experience.ForCombat(1, 1, landed: true, killed: fought.AttackerHpAfter == 0, boss: false), gained.Amount);
        }

        Assert.Equal(0, result.Next.Find("brigand-1")!.Unit.Exp);
    }

    [Fact]
    public void ALevelUpInBattleRaisesCurrentHpByTheHpGain()
    {
        var hale = Hale with { Exp = 99, Growths = new Stats(100, 0, 0, 0, 0, 0, 0, 0, 0) };
        var state = Start(roster: ValueList<Unit>.Of(hale, Wren));
        state = state.WithUnit(state.Find("hale")! with { Hp = 10 }).WithUnit(state.Find("brigand-1")! with { Hp = 1 });
        var result = state.Do(new Move("hale", new Coord(2, 1))).Try(new Attack("hale", "brigand-1"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var levelUp = Assert.Single(result.Events.OfType<LeveledUp>());
        Assert.Equal(2, levelUp.NewLevel);
        Assert.Equal(1, levelUp.Gains.Hp);
        var after = result.Next.Find("hale")!;
        Assert.Equal(2, after.Unit.Level);
        Assert.Equal(23, after.Unit.Stats.Hp);
        Assert.Equal(11, after.Hp);
    }
}
