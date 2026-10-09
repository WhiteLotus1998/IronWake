using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 1489, Grit's one reshape (Table rounds 559 to 561): <c>grit_gain: hits</c>, where every unit starts at 1
/// Grit and only a hit taken raises it; and the capped expected-damage rule both sides declare a form by, where a
/// form is declared only when its expected damage, each outcome capped at the target's HP, beats the plain strike's.
/// </summary>
public class GritHitsAndFormRuleTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static MapDefinition HitsMap => YardMap with { FormsEnabled = true, GritFromHitsOnly = true };

    private static BattleState Begin(MapDefinition map) =>
        BattleState.From(map, Starter, ValueList<Unit>.Of(Hale, Wren), 7);

    private static CombatForecast Strike(int damage, int hit, int crit = 0, bool doubles = false) =>
        new(new SideForecast(true, damage, hit, hit, crit, doubles), SideForecast.None, RollScheme.TwoRollAverage);

    [Theory]
    [InlineData(12, 45, 0, false, 18, 5.4)]
    [InlineData(20, 25, 0, false, 18, 4.5)]
    [InlineData(10, 50, 0, true, 15, 8.75)]
    [InlineData(10, 50, 10, false, 20, 5.5)]
    [InlineData(12, 100, 0, false, 6, 6.0)]
    public void ExpectedDamageIsCappedAtTheTargetsHpOutcomeByOutcomeCountingDoublesAndCrits(int damage, int hit, int crit, bool doubles, int targetHp, double expected)
    {
        Assert.Equal(expected, FormChoice.CappedExpected(Strike(damage, hit, crit, doubles), targetHp, 99), 6);
    }

    [Fact]
    public void UnderGritGainHitsEveryUnitOfEitherSideStartsAtOne()
    {
        var state = Begin(HitsMap);

        Assert.All(state.Units, u => Assert.Equal(1, u.Grit));
    }

    [Fact]
    public void UnderGritGainHitsAPhaseStartGainsNoGrit()
    {
        var state = Begin(HitsMap);
        for (var i = 0; i < 6; i++)
        {
            state = Resolver.Apply(state, Starter, new EndPhase()).Next;
        }

        Assert.All(state.Units, u => Assert.Equal(1, u.Grit));
    }

    [Fact]
    public void UnderGritGainHitsAHitTakenStillGainsOneGrit()
    {
        var state = Resolver.Apply(Begin(HitsMap), Starter, new Move("hale", new Coord(2, 1))).Next;

        var result = Resolver.Apply(state, Starter, new Attack("hale", "brigand-1"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var strikes = result.Events.OfType<CombatFought>().Single().Strikes;
        Assert.Equal(Math.Min(Grit.Cap, 1 + strikes.Count(s => s.TargetId == "hale" && s.Hit)), result.Next.Find("hale")!.Grit);
    }

    [Fact]
    public void AGritGainHitsMapPrintsItsOwnLegend()
    {
        var board = Ironwake.Core.MapRenderer.Render(Begin(HitsMap), Starter);

        Assert.Contains(Grit.HitsLegend, board);
        Assert.DoesNotContain(Grit.Legend + "\n", board);
    }

    [Fact]
    public void TheGritGainAndFormRuleHeadersRoundTrip()
    {
        var text = MapFormat.Write(MapFixture.Parse(MapFixture.OldMillRoad) with { FormsEnabled = true, GritFromHitsOnly = true, FormRuleLethal = true }, MapFixture.Content);

        Assert.Contains("grit_gain: hits\n", text);
        Assert.Contains("form_rule: lethal\n", text);
        var map = MapFixture.Parse(text);
        Assert.True(map.GritFromHitsOnly);
        Assert.True(map.FormRuleLethal);
        Assert.Equal(text, MapFormat.Write(map, MapFixture.Content));
    }

    [Fact]
    public void AGritGainHeaderOtherThanHitsIsRefused()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(MapFixture.Replacing("enemy_level: 1", "enemy_level: 1\nforms: on\ngrit_gain: kills")));

        Assert.Contains("grit_gain may only be 'hits'", error.Message);
    }

    [Fact]
    public void AGritGainHeaderWithoutFormsIsRefused()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(MapFixture.Replacing("enemy_level: 1", "enemy_level: 1\ngrit_gain: hits")));

        Assert.Contains("grit_gain: hits needs forms: on", error.Message);
    }

    /// <summary>
    /// Code's 1461 Tollgate board (<c>docs/samples/the_tollgate_forms_1461.map</c>, seed 1461), the turn-7 swing at its
    /// opening levels: the Bandit Leader on 7,2 with 3 Grit and the Toll Axe, Pell on the gate at 6,3 at full HP
    /// (17 on turn 7, a level later). Cleave kills on its hit and the plain strike does not, so 0400's lethal rule swung a 25 percent Cleave; the plain strike's capped expected damage is higher, so the capped rule swings plain.
    /// </summary>
    [Fact]
    public void TheBanditLeaderSwingsPlainAtPellUnderTheCappedRule()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var lethalMap = MapFiles.Load(Path.Combine(root, "docs", "samples", "the_tollgate_forms_1461.map"), Shipped);
        var capped = Board(lethalMap with { FormRuleLethal = false });
        var lethal = Board(lethalMap);
        var leader = capped.Find("bandit_leader-1")!;
        var pell = capped.Find("pell")!;
        var slot = leader.Unit.Inventory.Items.ToList().FindIndex(i => i.ItemId == "toll_axe");

        var plain = Queries.Forecast(capped, Shipped, leader, pell, leader.At, slot)!;
        var cleave = Queries.Forecast(capped, Shipped, leader, pell, leader.At, slot, "cleave")!;

        Assert.True(plain.Attacker.Damage < pell.Hp);
        Assert.True(cleave.Attacker.Damage >= pell.Hp);
        Assert.True(FormChoice.CappedExpected(plain, pell.Hp, leader.Hp) > FormChoice.CappedExpected(cleave, pell.Hp, leader.Hp));
        Assert.Equal("cleave", EnemyAi.Form(lethal, Shipped, leader, leader.At, pell, slot));
        Assert.Null(EnemyAi.Form(capped, Shipped, leader, leader.At, pell, slot));
        Assert.Empty(FormChoice.Offers(capped, Shipped, leader, leader.At, pell, slot));

        static BattleState Board(MapDefinition map)
        {
            var state = BattleState.From(map, Shipped, Shipped.Cast, 1461);
            state = state.WithUnit(state.Find("pell")! with { At = new Coord(6, 3) });
            return state.WithUnit(state.Find("bandit_leader-1")! with { Grit = 3 });
        }
    }

    [Fact]
    public void ATargetAlreadyInThePlainStrikesKillRangeGetsThePlainStrike()
    {
        var state = Resolver.Apply(Begin(YardMap with { FormsEnabled = true }), Starter, new Move("hale", new Coord(2, 1))).Next;
        state = Resolver.Apply(state, Starter, new EndPhase()).Next;
        var brigand = state.Find("brigand-1")!;
        var plain = Queries.Forecast(state, Starter, brigand, state.Find("hale")!)!;
        state = state.WithUnit(state.Find("hale")! with { Hp = plain.Attacker.Damage });
        state = state.WithUnit(brigand with { Grit = 3 });
        brigand = state.Find("brigand-1")!;

        Assert.Empty(FormChoice.Offers(state, Starter, brigand, brigand.At, state.Find("hale")!, 0));
        Assert.All(EnemyAi.PlanUnit(state, Starter, brigand).OfType<Attack>(), a => Assert.Null(a.Art));
    }
}
