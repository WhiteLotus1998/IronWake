using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The pincer (DESIGN.md 13.13, experiment): on a <c>pincer: on</c> map a unit struck from an
/// orthogonally adjacent tile, while a unit of the striker's side stands directly behind it, is
/// hit at <see cref="Pincer.Hit"/> more. Counters are pinned the same way; a ranged strike, a
/// friend behind, or a map without the header pins nothing.
/// </summary>
public class PincerTests
{
    private static string Field(bool pincer, string units) =>
        $"""
        name: Field
        size: 7x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(pincer ? "pincer: on" : "")}

        .......
        .......
        .......
        .......
        .......

        units:
        {units}
        """.Replace("\n\n\n", "\n\n");

    private const string Pinned = """
        P captain 1,2
        P recruit:wren 3,2
        E brigand 2,2 group:field behavior:aggressive

        """;

    private const string Alone = """
        P captain 1,2
        P recruit:wren 6,4
        E brigand 2,2 group:field behavior:aggressive

        """;

    private static BattleState Start(string units, bool pincer = true) =>
        BattleFixture.Start(map: Field(pincer, units));

    private static CombatForecast Forecast(BattleState state, string unit, string target, Coord? from = null)
    {
        var striker = state.Find(unit)!;
        return Queries.Forecast(state, Starter, striker, state.Find(target)!, from ?? striker.At)!;
    }

    [Fact]
    public void AUnitStruckFromBesideWithAFoeOfTheStrikerDirectlyBehindIsHitAtFifteenMore()
    {
        var pinned = Forecast(Start(Pinned), "hale", "brigand-1");
        var alone = Forecast(Start(Alone), "hale", "brigand-1");

        Assert.Equal(15, Pincer.Hit);
        Assert.True(alone.Attacker.HitChance <= 100 - Pincer.Hit, $"unpinned hit {alone.Attacker.HitChance} leaves no room to see the pincer");
        Assert.Equal(Math.Min(100, alone.Attacker.HitChance + Pincer.Hit), pinned.Attacker.HitChance);
        Assert.Equal(alone.Defender.HitChance, pinned.Defender.HitChance);
    }

    [Fact]
    public void WithoutTheHeaderNothingIsPinned()
    {
        var off = Forecast(Start(Pinned, pincer: false), "hale", "brigand-1");
        var alone = Forecast(Start(Alone, pincer: false), "hale", "brigand-1");

        Assert.Equal(alone.Attacker.HitChance, off.Attacker.HitChance);
        Assert.Null(Pincer.PinnedBy(Start(Pinned, pincer: false), Start(Pinned, pincer: false).Find("hale")!, Start(Pinned, pincer: false).Find("brigand-1")!));
    }

    [Fact]
    public void AFriendOfTheTargetBehindItDoesNotPin()
    {
        var state = Start("""
            P captain 1,2
            E brigand 2,2 group:field behavior:aggressive
            E soldier 3,2 group:field behavior:aggressive

            """);

        Assert.Null(Pincer.PinnedBy(state, state.Find("hale")!, state.Find("brigand-1")!));
    }

    [Fact]
    public void AStrikeFromTheSideOrAtRangeTwoDoesNotPin()
    {
        var state = Start(Pinned);
        var hale = state.Find("hale")!;
        var brigand = state.Find("brigand-1")!;

        Assert.NotNull(Pincer.PinnedBy(state, hale, brigand));
        Assert.Null(Pincer.PinnedBy(state, hale with { At = new Coord(2, 1) }, brigand));
        Assert.Null(Pincer.PinnedBy(state, hale with { At = new Coord(0, 2) }, brigand));
    }

    [Fact]
    public void ACounterIsPinnedWhenTheAttackerStandsBetweenTwoFoes()
    {
        var state = Start("""
            P captain 1,2
            E brigand 2,2 group:field behavior:aggressive
            E soldier 0,2 group:field behavior:aggressive

            """);
        var between = Forecast(state, "hale", "brigand-1");
        var clear = Forecast(Start("""
            P captain 1,2
            E brigand 2,2 group:field behavior:aggressive
            E soldier 0,0 group:field behavior:aggressive

            """), "hale", "brigand-1");

        Assert.True(clear.Defender.HitChance <= 100 - Pincer.Hit, $"unpinned counter hit {clear.Defender.HitChance} leaves no room to see the pincer");
        Assert.Equal(clear.Defender.HitChance + Pincer.Hit, between.Defender.HitChance);
        Assert.Equal(clear.Attacker.HitChance, between.Attacker.HitChance);
    }

    [Fact]
    public void AForecastFromATileReadsThePincerAtThatTile()
    {
        var state = Start("""
            P captain 1,4
            P recruit:wren 3,2
            E brigand 2,2 group:field behavior:aggressive

            """);

        var there = Forecast(state, "hale", "brigand-1", new Coord(1, 2));
        var side = Forecast(state, "hale", "brigand-1", new Coord(2, 3));

        Assert.Equal(Math.Min(100, side.Attacker.HitChance + Pincer.Hit), there.Attacker.HitChance);
    }

    [Fact]
    public void ThePlannerScoresAPinningStrikeAboveTheSameStrikeUnpinned()
    {
        var state = Start("""
            P captain 1,2
            E brigand 2,2 group:field behavior:aggressive
            E soldier 5,0 group:field behavior:aggressive

            """);
        var soldier = state.Find("soldier-1")!;
        var hale = state.Find("hale")!;

        var pinning = EnemyAi.Score(state, Starter, soldier, new Coord(0, 2), hale);
        var open = EnemyAi.Score(state, Starter, soldier, new Coord(1, 1), hale);

        Assert.True(pinning > open, $"pinning {pinning} should outscore open {open}");
    }

    [Fact]
    public void TheResolverStrikesWithThePinnedCombatant()
    {
        var state = Start(Pinned);
        var hale = state.Find("hale")!;
        var brigand = state.Find("brigand-1")!;
        var me = hale.ToCombatant(state, Starter, against: brigand);
        var expected = CombatResolver.Resolve(me, brigand.Answering(state, Starter, hale.At, hale), 1, new CombatContext(state.Turn, state.Phase), new KeyedRng(state.Seed), state.Scheme);

        var result = state.Try(new Attack("hale", "brigand-1"));

        Assert.Equal(Pincer.Hit, me.HitModifier);
        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(expected.Strikes, result.Events.OfType<CombatFought>().Single().Strikes);
    }

    [Fact]
    public void TheForecastPrintsOneLinePerPinnedSide()
    {
        var state = Start(Pinned);
        var hale = state.Find("hale")!;
        var brigand = state.Find("brigand-1")!;

        var lines = PlaySession.PincerLines(state, hale, brigand).ToList();

        Assert.Equal(new[] { "  pincer: brigand-1 pinned by wren: hale hit +15" }, lines);
        Assert.Empty(PlaySession.PincerLines(Start(Alone), Start(Alone).Find("hale")!, Start(Alone).Find("brigand-1")!));
    }

    [Fact]
    public void ThePincerHeaderRoundTripsAndTheSampleCarriesIt()
    {
        var map = MapFixture.Parse(Field(true, Pinned), "field.map");
        Assert.True(map.PincerEnabled);
        Assert.Contains("pincer: on\n", MapFormat.Write(map, Starter));
        Assert.Contains(MapRenderer.PincerLegend, MapRenderer.Render(map, Starter));

        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var path = Path.Combine(repo, "docs", "samples", "sallow_grange_pincer.map");
        var sample = MapFiles.Load(path, MapFixture.Content);
        Assert.True(sample.PincerEnabled);
        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), MapFormat.Write(sample, Starter));
    }

    /// <summary>
    /// Issue 429: the Brackwater sample that judges the anvil arm is the shipped map in
    /// daylight with the pincer on. Its only differences from <c>brackwater_cut.map</c> are the
    /// added <c>pincer: on</c> line and the dropped <c>dusk</c> line; the exit rule stays 0074's.
    /// </summary>
    [Fact]
    public void TheBrackwaterPincerSampleIsTheShippedMapInDaylightWithOnlyThePincerAdded()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var shippedPath = Path.Combine(repo, "content", "maps", "brackwater_cut.map");
        var samplePath = Path.Combine(repo, "docs", "samples", "brackwater_cut_pincer.map");
        var shipped = File.ReadAllText(shippedPath).Replace("\r\n", "\n").Split('\n');
        var sampleText = File.ReadAllText(samplePath).Replace("\r\n", "\n");
        var sampleLines = sampleText.Split('\n');

        Assert.Equal(new[] { "pincer: on" }, sampleLines.Except(shipped).ToArray());
        Assert.Equal(new[] { "dusk: 5" }, shipped.Except(sampleLines).ToArray());

        var sample = MapFiles.Load(samplePath, MapFixture.Content);
        var original = MapFiles.Load(shippedPath, MapFixture.Content);
        Assert.True(sample.PincerEnabled);
        Assert.Null(sample.Dusk);
        Assert.Equal(original.ExitAfterMove, sample.ExitAfterMove);
        Assert.Equal(sampleText, MapFormat.Write(sample, Starter));
    }
}
