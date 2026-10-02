using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The pair rule (issue 692, slice 4; DECISIONS/0150): on a map whose <c>pair_rule:</c> header names
/// an enemy group, an enemy of that group strikes a unit with an ally orthogonally beside it once,
/// never doubling and never critting, striking or answering; against a unit alone, for a group the
/// header does not name, or on a map without the header it keeps its double and its crit.
/// </summary>
public class PairRuleTests
{
    private static string Field(string header, string units) =>
        $"""
        name: Field
        size: 7x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {header}

        .......
        .......
        .......
        .......
        .......

        units:
        {units}
        """.Replace("\n\n\n", "\n\n");

    private const string Paired = """
        P captain 1,2
        P recruit:wren 1,3
        E finale_lord 2,2 group:lord behavior:aggressive
        E brigand 6,0 group:field behavior:hold

        """;

    private const string Alone = """
        P captain 1,2
        P recruit:wren 5,4
        E finale_lord 2,2 group:lord behavior:aggressive
        E brigand 6,0 group:field behavior:hold

        """;

    private static BattleState Start(string units, string header = "pair_rule: lord") =>
        BattleFixture.Start(map: Field(header, units));

    private static CombatForecast LordStrikes(BattleState state) =>
        Queries.Forecast(state, Starter, state.Find("finale_lord-1")!, state.Find("hale")!, state.Find("finale_lord-1")!.At)!;

    private static CombatForecast HaleStrikes(BattleState state) =>
        Queries.Forecast(state, Starter, state.Find("hale")!, state.Find("finale_lord-1")!, state.Find("hale")!.At)!;

    [Fact]
    public void ABoundEnemyNeverCritsAUnitWithAnAllyBesideIt()
    {
        var alone = LordStrikes(Start(Alone));
        var paired = LordStrikes(Start(Paired));

        Assert.True(alone.Attacker.CritChance > 0, $"alone the lord's crit is {alone.Attacker.CritChance}, so the rule has nothing to bar");
        Assert.Equal(0, paired.Attacker.CritChance);
        Assert.Equal(alone.Attacker.Damage, paired.Attacker.Damage);
        Assert.Equal(alone.Attacker.HitChance, paired.Attacker.HitChance);
    }

    [Fact]
    public void ABoundEnemyNeverDoublesAUnitWithAnAllyBesideIt()
    {
        var slow = Hale with { Id = "hale", Stats = Hale.Stats with { Spd = 1 } };
        var roster = ValueList<Unit>.Of(slow, Wren);
        var alone = LordStrikes(BattleFixture.Start(roster: roster, map: Field("pair_rule: lord", Alone)));
        var paired = LordStrikes(BattleFixture.Start(roster: roster, map: Field("pair_rule: lord", Paired)));

        Assert.True(alone.Attacker.Doubles, "alone the lord does not double a speed-1 captain, so the rule has nothing to hold");
        Assert.False(paired.Attacker.Doubles);
    }

    [Fact]
    public void ThePairRuleHoldsTheBoundEnemysCounterToo()
    {
        var alone = HaleStrikes(Start(Alone));
        var paired = HaleStrikes(Start(Paired));

        Assert.True(alone.Defender.CritChance > 0);
        Assert.Equal(0, paired.Defender.CritChance);
    }

    [Fact]
    public void AnAllyDiagonallyBesideIsNoPair()
    {
        var diagonal = Start(Paired.Replace("P recruit:wren 1,3", "P recruit:wren 0,3"));

        Assert.True(LordStrikes(diagonal).Attacker.CritChance > 0);
    }

    [Fact]
    public void AGroupTheHeaderDoesNotNameKeepsItsCrit()
    {
        var unnamed = Start(Paired, header: "pair_rule: field");
        var none = Start(Paired, header: "");

        Assert.Equal(LordStrikes(Start(Alone)).Attacker.CritChance, LordStrikes(unnamed).Attacker.CritChance);
        Assert.Equal(LordStrikes(Start(Alone)).Attacker.CritChance, LordStrikes(none).Attacker.CritChance);
    }

    [Fact]
    public void ThreatPricesTheBarredCrit()
    {
        var state = Start(Paired);
        var line = Assert.Single(Queries.Threats(state, Starter, state.Find("hale")!, state.Find("hale")!.At)!, l => l.Enemy.Id == "finale_lord-1");

        Assert.Equal(0, line.Forecast.Attacker.CritChance);
    }

    [Fact]
    public void ThePairRuleHeaderRoundTripsAndNamesItsGroups()
    {
        var map = MapFixture.Parse(Field("pair_rule: lord, field", Paired));

        Assert.Equal(new[] { "lord", "field" }, map.PairRuleGroups);
        Assert.Contains("pair_rule: lord, field\n", MapFormat.Write(map, Starter));
        Assert.Equal(map, MapFixture.Parse(MapFormat.Write(map, Starter)));
    }

    [Theory]
    [InlineData("pair_rule: van", "pair_rule: no enemy is in group 'van'")]
    [InlineData("pair_rule: lord, lord", "pair_rule: group 'lord' is listed twice")]
    public void APairRuleHeaderNamingNoGroupOrAGroupTwiceIsRefused(string header, string message)
    {
        var error = Assert.ThrowsAny<Exception>(() => MapFixture.Parse(Field(header, Paired)));

        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void TheBoardAndHelpPrintThePairRuleAndWhoItBinds()
    {
        var state = Start(Paired);
        var board = MapRenderer.Render(state, Starter);

        Assert.Contains("Pair rule: an enemy of the lord group strikes a unit with an ally beside it once, never doubling, never critting.", board);
        Assert.Contains("pair rule binds: Sworn Lord", board);
        Assert.Contains(PairRule.Rule(state.Map), Objective.Rules(state, Starter));
        Assert.DoesNotContain("Pair rule", MapRenderer.Render(Start(Paired, header: ""), Starter));
    }
}
