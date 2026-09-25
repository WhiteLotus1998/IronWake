using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Rapport and Rivalry (DESIGN.md 13.1, issue 16): behind a map's <c>rivalry:</c> header,
/// two recruits of different regions standing adjacent fight with the named arm's
/// modifiers, rapport accrues between adjacent recruits at the end of each player phase
/// at a rate read from Cha, and a rival pair whose rapport reaches the threshold is rivals
/// no longer. Without the header nothing changes.
/// </summary>
public class RivalryTests
{
    /// <summary>
    /// A 6x4 yard. Wren (Aldmere) at 0,1 and Ivo (Sallow) at 0,2 are adjacent; the captain
    /// Hale (Crown) at 0,0 is adjacent to Wren. The brigand at 3,1 is two tiles from 1,1.
    /// </summary>
    private const string Camp = """
        name: Camp
        size: 6x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        rivalry: symmetric

        ......
        ......
        ......
        ......

        units:
        P captain 0,0
        P recruit:wren 0,1
        P recruit:ivo 0,2
        E brigand 3,1 group:yard behavior:aggressive

        """;

    private static ValueList<Unit> Cohort(string ivoRegion = "sallow") => ValueList<Unit>.Of(
        Hale with { Region = "crown" },
        Wren with { Region = "aldmere" },
        Ivo with { Region = ivoRegion });

    private static BattleState Begin(string arm = "symmetric", string ivoRegion = "sallow", string map = Camp) =>
        Start(roster: Cohort(ivoRegion), map: map.Replace("rivalry: symmetric", "rivalry: " + arm));

    private static BattleState NoHeader() => Start(roster: Cohort(), map: Camp.Replace("rivalry: symmetric\n", ""));

    private static RivalryArm ArmNamed(string id) => Starter.Rivalry.Arm(id)!;

    [Fact]
    public void RecruitsOfDifferentRegionsStandingAdjacentAreRivals()
    {
        var state = Begin();

        Assert.Equal(new[] { "ivo" }, Rivalry.AdjacentRivals(state, Starter, state.Find("wren")!).Select(u => u.Id));
        Assert.Equal(new[] { "wren" }, Rivalry.AdjacentRivals(state, Starter, state.Find("ivo")!).Select(u => u.Id));
    }

    [Fact]
    public void TheCaptainIsNobodysRival()
    {
        var state = Begin();

        Assert.Empty(Rivalry.AdjacentRivals(state, Starter, state.Find("hale")!));
        Assert.False(Rivalry.AreRivals(state, Starter, state.Find("hale")!, state.Find("wren")!));
    }

    [Fact]
    public void RecruitsOfOneRegionAreNotRivals()
    {
        var state = Begin(ivoRegion: "aldmere");

        Assert.Empty(Rivalry.AdjacentRivals(state, Starter, state.Find("wren")!));
    }

    [Fact]
    public void ARivalOutOfReachOfOneTileDoesNotCount()
    {
        var state = Begin();
        state = state.WithUnit(state.Find("ivo")! with { At = new Coord(0, 3) });

        Assert.Empty(Rivalry.AdjacentRivals(state, Starter, state.Find("wren")!));
    }

    [Fact]
    public void WithoutTheHeaderThereAreNoRivalsAndNoModifiers()
    {
        var state = NoHeader();

        Assert.Empty(Rivalry.AdjacentRivals(state, Starter, state.Find("wren")!));
        Assert.Equal((0, 0, 0), Rivalry.Modifiers(state, Starter, state.Find("wren")!, countering: true));
    }

    [Theory]
    [InlineData("written", false, -5, 10, 0)]
    [InlineData("written", true, -5, 10, 0)]
    [InlineData("symmetric", false, -5, 10, -10)]
    [InlineData("symmetric", true, -5, 10, -10)]
    [InlineData("counter", false, 0, 0, 0)]
    [InlineData("counter", true, -5, 10, 0)]
    public void EachArmAppliesItsModifiersBesideARival(string arm, bool countering, int hit, int crit, int critAvoid)
    {
        var state = Begin(arm);

        Assert.Equal((hit, crit, critAvoid), Rivalry.Modifiers(state, Starter, state.Find("wren")!, countering));
    }

    [Fact]
    public void TheArmsAreContent()
    {
        Assert.Equal(new RivalryArm("written", -5, 10, 0, false), ArmNamed("written"));
        Assert.Equal(new RivalryArm("symmetric", -5, 10, -10, false), ArmNamed("symmetric"));
        Assert.Equal(new RivalryArm("counter", -5, 10, 0, true), ArmNamed("counter"));
    }

    [Fact]
    public void TheCombatantCarriesTheModifiersIntoHitCritAndCritAvoid()
    {
        var state = Begin();
        var wren = state.Find("wren")!;

        var plain = wren.ToCombatant(state.Map, Starter);
        var rival = wren.ToCombatant(state, Starter);

        Assert.Equal(Core.Combat.Hit(plain) - 5, Core.Combat.Hit(rival));
        Assert.Equal(Core.Combat.Crit(plain) + 10, Core.Combat.Crit(rival));
        Assert.Equal(Core.Combat.CritAvoid(plain) - 10, Core.Combat.CritAvoid(rival));
    }

    [Fact]
    public void TheForecastFromATileBesideARivalCarriesTheArm()
    {
        var state = Begin();
        var wren = state.Find("wren")!;
        var brigand = state.Find("brigand-1")!;
        var besideIvo = new Coord(1, 2);
        var alone = new Coord(2, 0);

        var near = Queries.Forecast(state, Starter, wren with { At = besideIvo }, brigand with { At = new Coord(2, 2) }, besideIvo)!;
        var far = Queries.Forecast(state, Starter, wren with { At = alone }, brigand with { At = new Coord(2, 1) }, alone)!;

        Assert.Equal(far.Attacker.HitChance - 5, near.Attacker.HitChance);
        Assert.Equal(far.Attacker.CritChance + 10, near.Attacker.CritChance);
    }

    [Fact]
    public void TheEnemyPricesTheLoweredCritAvoidOfAUnitBesideARival()
    {
        var state = Begin().Do(new EndPhase());
        var wren = state.Find("wren")!;
        var brigand = state.Find("brigand-1")! with { At = new Coord(1, 1) };
        state = state.WithUnit(brigand);

        var forecast = Queries.Forecast(state, Starter, brigand, wren, brigand.At)!;
        var brigandSide = brigand.ToCombatant(state.Map, Starter);

        Assert.Equal(Math.Clamp(Core.Combat.Crit(brigandSide) - (wren.ToCombatant(state.Map, Starter).Stats.Lck - 10), 0, 100), forecast.Attacker.CritChance);
        Assert.True(forecast.Defender.Strikes);
        Assert.Equal(Math.Clamp(Core.Combat.Crit(wren.ToCombatant(state.Map, Starter)) + 10 - Core.Combat.CritAvoid(brigandSide), 0, 100), forecast.Defender.CritChance);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(7, 3)]
    [InlineData(8, 4)]
    [InlineData(20, 4)]
    public void TheRapportRateIsTheStepTableByCha(int cha, int rate)
    {
        Assert.Equal(rate, Starter.Rivalry.RateFor(cha));
    }

    [Fact]
    public void RapportAccruesBetweenAdjacentRecruitsAtTheEndOfThePlayerPhase()
    {
        var state = Begin();
        var amount = Rivalry.RateOf(state.Find("ivo")!, Starter) + Rivalry.RateOf(state.Find("wren")!, Starter);

        var result = Resolver.Apply(state, Starter, new EndPhase());

        Assert.Equal(new RapportGained("ivo", "wren", amount, amount), result.Events[0]);
        Assert.Equal(amount, Rivalry.PointsOf(result.Next, "wren", "ivo"));
        Assert.Equal(ValueList<Rapport>.Of(new Rapport("ivo", "wren", amount)), result.Next.Rapport);
    }

    [Fact]
    public void RapportDoesNotAccrueAtTheEndOfTheEnemyPhaseOrApart()
    {
        var state = Begin().Do(new EndPhase());
        var afterPlayer = state.Rapport;

        state = state.Do(new EndPhase());
        Assert.Equal(afterPlayer, state.Rapport);

        state = state.Do(new Move("ivo", new Coord(0, 3))).Do(new EndPhase());
        Assert.Equal(afterPlayer, state.Rapport);
    }

    /// <summary>
    /// A 12x4 field for the threatened-phase rule (issue 209): the same three at the west
    /// edge, and the brigand at <c>{0}</c> with <c>{1}</c>. At 11,3 a holding brigand
    /// strikes nothing near the pair; at 5,1 it stands five tiles from Wren, outside the
    /// wake radius of 4, and within an aggressive move of her.
    /// </summary>
    private static BattleState Field(string at, string behavior) => Start(roster: Cohort(), map: $"""
        name: Field
        size: 12x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        rivalry: symmetric

        ............
        ............
        ............
        ............

        units:
        P captain 0,0
        P recruit:wren 0,1
        P recruit:ivo 0,2
        E brigand {at} group:field behavior:{behavior}

        """);

    [Fact]
    public void RapportDoesNotAccrueOnAPhaseNoEnemyCanStrikeThePair()
    {
        var state = Field("11,3", "hold");
        Assert.False(Threat.IsThreatened(state, Starter, state.Find("wren")!));

        var result = Resolver.Apply(state, Starter, new EndPhase());

        Assert.DoesNotContain(result.Events, e => e is RapportGained);
        Assert.Empty(result.Next.Rapport);
    }

    [Fact]
    public void RapportAccruesOnAPhaseAnAwakeEnemyCanStrikeOneOfThePair()
    {
        var state = Field("5,1", "aggressive");
        Assert.True(Threat.IsThreatened(state, Starter, state.Find("wren")!));
        var amount = Rivalry.RateOf(state.Find("ivo")!, Starter) + Rivalry.RateOf(state.Find("wren")!, Starter);

        var result = Resolver.Apply(state, Starter, new EndPhase());

        Assert.Contains(new RapportGained("ivo", "wren", amount, amount), result.Events);
    }

    [Fact]
    public void ASleepingGroupThreatensNothingSoRapportDoesNotAccrue()
    {
        var state = Field("5,1", "guard");
        Assert.False(state.IsAwake("field"));
        Assert.Empty(Threat.StruckByUnit(state, Starter, state.Find("brigand-1")!));

        var result = Resolver.Apply(state, Starter, new EndPhase());

        Assert.DoesNotContain(result.Events, e => e is RapportGained);
    }

    [Fact]
    public void AHoldingEnemyThreatensOnlyFromItsOwnTile()
    {
        var state = Field("11,3", "hold");
        var struck = Threat.StruckByUnit(state, Starter, state.Find("brigand-1")!);

        Assert.Contains(new Coord(10, 3), struck);
        Assert.DoesNotContain(new Coord(8, 3), struck);
    }

    [Theory]
    [InlineData("11,3", "hold")]
    [InlineData("5,1", "guard")]
    [InlineData("5,1", "aggressive")]
    public void AccrualAndTheExposureLineReadTheSamePhase(string at, string behavior)
    {
        var state = Field(at, behavior);

        var exposed = Rivalry.Exposed(state, Starter).Select(u => u.Id).ToList();
        var gained = Resolver.Apply(state, Starter, new EndPhase()).Events.OfType<RapportGained>().ToList();

        Assert.Equal(behavior == "aggressive", exposed.Count > 0);
        Assert.Equal(exposed.Count > 0, gained.Count > 0);
        Assert.All(exposed, id => Assert.Contains(gained, g => g.A == id || g.B == id));
    }

    [Fact]
    public void RapportDoesNotAccrueWithoutTheHeader()
    {
        var result = Resolver.Apply(NoHeader(), Starter, new EndPhase());

        Assert.DoesNotContain(result.Events, e => e is RapportGained);
        Assert.Empty(result.Next.Rapport);
    }

    [Fact]
    public void ARivalPairReachingTheThresholdIsRivalsNoLonger()
    {
        var state = Begin();
        state = state with { Rapport = ValueList<Rapport>.Of(new Rapport("ivo", "wren", Starter.Rivalry.OverwriteAt - 1)) };
        Assert.Single(Rivalry.AdjacentRivals(state, Starter, state.Find("wren")!));

        var result = Resolver.Apply(state, Starter, new EndPhase());

        Assert.Contains(new RivalryEnded("ivo", "wren"), result.Events);
        Assert.Empty(Rivalry.AdjacentRivals(result.Next, Starter, result.Next.Find("wren")!));
        Assert.Equal((0, 0, 0), Rivalry.Modifiers(result.Next, Starter, result.Next.Find("wren")!, countering: true));
    }

    [Fact]
    public void APairAlreadyPastTheThresholdDoesNotEndTwice()
    {
        var state = Begin();
        state = state with { Rapport = ValueList<Rapport>.Of(new Rapport("ivo", "wren", Starter.Rivalry.OverwriteAt)) };

        var result = Resolver.Apply(state, Starter, new EndPhase());

        Assert.DoesNotContain(result.Events, e => e is RivalryEnded);
    }

    [Fact]
    public void ARecallRestoresRapportWithTheBoard()
    {
        var state = Begin().Do(new EndPhase()).Do(new EndPhase());
        Assert.NotEmpty(state.Rapport);

        state = state.Do(new Recall(0));

        Assert.Empty(state.Rapport);
    }

    [Fact]
    public void TheCanonicalStateCarriesRapportOnlyOnARivalryMap()
    {
        var state = Begin().Do(new EndPhase());

        Assert.Contains("rapport ivo+wren=", state.Canonical());
        Assert.DoesNotContain("rapport", NoHeader().Do(new EndPhase()).Canonical());
    }

    [Fact]
    public void TheRivalryHeaderParsesAndWritesBack()
    {
        var map = MapFixture.Parse(Camp);

        Assert.Equal("symmetric", map.RivalryArm);
        Assert.Equal(Camp.Replace("\r\n", "\n"), MapFormat.Write(map, Starter));
        Assert.Null(MapFixture.Parse(Yard).RivalryArm);
    }

    [Fact]
    public void TheRivalryHeaderRefusesAnArmTheContentLacks()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Camp.Replace("rivalry: symmetric", "rivalry: loud")));

        Assert.Contains("rivalry names arm 'loud'", error.Message);
        Assert.Contains("counter, symmetric, written", error.Message);
    }

    [Fact]
    public void TheRivalrySampleIsSaltmarshFordWithTheHeader()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var text = File.ReadAllText(Path.Combine(repo, "docs", "samples", "saltmarsh_ford_rivalry.map")).Replace("\r\n", "\n");
        var shipped = File.ReadAllText(Path.Combine(repo, "content", "maps", "saltmarsh_ford.map")).Replace("\r\n", "\n");
        var map = MapFixture.Parse(text, "saltmarsh_ford_rivalry.map");

        Assert.Equal(text, MapFormat.Write(map, Starter));
        Assert.Equal(MapFixture.Parse(shipped, "saltmarsh_ford.map") with { Name = map.Name, RivalryArm = "symmetric" }, map);
    }

    [Fact]
    public void TheSampleDeploysTwoRivalPairsThroughOttilie()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var map = MapFixture.Parse(File.ReadAllText(Path.Combine(repo, "docs", "samples", "saltmarsh_ford_rivalry.map")), "saltmarsh_ford_rivalry.map");
        var state = BattleState.From(map, Starter, Starter.Cast, 1);
        var recruits = state.UnitsOf(Side.Player).Where(Rivalry.IsRecruit).ToList();

        var pairs = recruits.SelectMany(a => recruits.Where(b => string.CompareOrdinal(a.Id, b.Id) < 0 && Rivalry.AreRivals(state, Starter, a, b)).Select(b => a.Id + "+" + b.Id));

        Assert.Equal(new[] { "ottilie+wren", "ottilie+teodor" }.OrderBy(s => s, StringComparer.Ordinal), pairs.OrderBy(s => s, StringComparer.Ordinal));
    }
}

/// <summary>The rivalry block of rules.json validates on load, each guard naming file, entry, and field (issue 16).</summary>
public class RivalryContentTests
{
    private const string Arms = "\"arms\": { \"a\": { \"hit\": -5, \"crit\": 10, \"critAvoid\": 0 } }";
    private const string Rates = "\"rapportRate\": [ { \"cha\": 0, \"rate\": 1 }, { \"cha\": 3, \"rate\": 2 } ]";

    private static ContentException Fails(string rivalry) =>
        Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(rules: "{ \"wakeRadius\": 4, \"rivalry\": { " + rivalry + " } }")));

    [Fact]
    public void AWellFormedBlockLoads()
    {
        var content = ContentLoader.Parse(Fixture.Files(rules: "{ \"wakeRadius\": 4, \"rivalry\": { " + Arms + ", " + Rates + ", \"overwriteAt\": 9 } }"));

        Assert.Equal(new RivalryArm("a", -5, 10, 0, false), content.Rivalry.Arm("a"));
        Assert.Equal(9, content.Rivalry.OverwriteAt);
        Assert.Equal(2, content.Rivalry.RateFor(4));
    }

    [Fact]
    public void ContentWithoutABlockHasNoArms()
    {
        Assert.Equal(RivalryRules.None, ContentLoader.Parse(Fixture.Files()).Rivalry);
    }

    [Fact]
    public void ABlockWithNoArmsIsRefused()
    {
        var e = Fails("\"arms\": { }, " + Rates + ", \"overwriteAt\": 9");

        Assert.Equal("rivalry.arms", e.Field);
    }

    [Fact]
    public void AnArmMissingAFieldIsRefusedByName()
    {
        var e = Fails("\"arms\": { \"a\": { \"hit\": -5, \"crit\": 10 } }, " + Rates + ", \"overwriteAt\": 9");

        Assert.Equal("rivalry.arms.a", e.Entry);
        Assert.Equal("critAvoid", e.Field);
    }

    [Fact]
    public void TheFirstRateStepMustBeAtChaZero()
    {
        var e = Fails(Arms + ", \"rapportRate\": [ { \"cha\": 1, \"rate\": 1 } ], \"overwriteAt\": 9");

        Assert.Equal("rivalry.rapportRate[0]", e.Entry);
        Assert.Equal("cha", e.Field);
    }

    [Fact]
    public void RateStepsMustRiseInCha()
    {
        var e = Fails(Arms + ", \"rapportRate\": [ { \"cha\": 0, \"rate\": 1 }, { \"cha\": 0, \"rate\": 2 } ], \"overwriteAt\": 9");

        Assert.Equal("rivalry.rapportRate[1]", e.Entry);
        Assert.Contains("must rise", e.Message);
    }

    [Fact]
    public void ANegativeRateIsRefused()
    {
        var e = Fails(Arms + ", \"rapportRate\": [ { \"cha\": 0, \"rate\": -1 } ], \"overwriteAt\": 9");

        Assert.Equal("rate", e.Field);
    }

    [Fact]
    public void AnEmptyRateTableIsRefused()
    {
        var e = Fails(Arms + ", \"rapportRate\": [ ], \"overwriteAt\": 9");

        Assert.Equal("rivalry.rapportRate", e.Field);
    }

    [Fact]
    public void AnOverwriteThresholdBelowOneIsRefused()
    {
        var e = Fails(Arms + ", " + Rates + ", \"overwriteAt\": 0");

        Assert.Equal("rivalry.overwriteAt", e.Field);
    }
}
