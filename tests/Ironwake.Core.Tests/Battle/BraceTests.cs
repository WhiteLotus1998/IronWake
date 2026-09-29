using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Brace (DESIGN.md 13.14, experiment; issue 425's acceptance): on a <c>brace: on</c> map a
/// unit that waits on the tile it began its phase on is struck at <see cref="Brace.Hit"/> less
/// hit until its side's next phase begins. A unit that moved or was shoved first, a sleeping
/// Guard, or a map without the header braces nothing; a shove or a Canto off the tile after
/// the Wait takes the brace off.
/// </summary>
public class BraceTests
{
    private static string Field(bool brace, string units) =>
        $"""
        name: Field
        size: 7x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(brace ? "brace: on" : "")}

        .......
        .......
        .......
        .......
        .......

        units:
        {units}
        """.Replace("\n\n\n", "\n\n");

    private const string Line = """
        P captain 1,2
        P recruit:wren 5,4
        E brigand 2,2 group:field behavior:aggressive

        """;

    private static BattleState Start(bool brace = true) =>
        BattleFixture.Start(map: Field(brace, Line));

    private static BattleState Apply(BattleState state, params Command[] commands)
    {
        foreach (var command in commands)
        {
            var result = state.Try(command);
            Assert.True(result.Accepted, result.Rejection?.Message);
            state = result.Next;
        }

        return state;
    }

    private static CombatForecast Forecast(BattleState state, string unit, string target)
    {
        var striker = state.Find(unit)!;
        return Queries.Forecast(state, Starter, striker, state.Find(target)!, striker.At)!;
    }

    /// <summary>The enemy phase begun with the captain as it waited: the brigand strikes it where it stands.</summary>
    private static BattleState EnemyPhaseAfter(BattleState state, params Command[] commands) =>
        Apply(state, commands.Append(new EndPhase()).ToArray());

    [Fact]
    public void AUnitThatWaitsWithoutMovingIsStruckAtFifteenLess()
    {
        var braced = EnemyPhaseAfter(Start(), new Wait("hale"), new Wait("wren"));
        var open = EnemyPhaseAfter(Start(brace: false), new Wait("hale"), new Wait("wren"));

        var hit = Forecast(braced, "brigand-1", "hale");
        var plain = Forecast(open, "brigand-1", "hale");

        Assert.Equal(15, Brace.Hit);
        Assert.True(braced.Find("hale")!.Braced);
        Assert.True(plain.Attacker.HitChance >= Brace.Hit, $"unbraced hit {plain.Attacker.HitChance} leaves no room to see the brace");
        Assert.Equal(Math.Max(0, plain.Attacker.HitChance - Brace.Hit), hit.Attacker.HitChance);
        Assert.Equal(plain.Defender.HitChance, hit.Defender.HitChance);
    }

    [Fact]
    public void AUnitThatMovedBeforeItWaitsDoesNotBrace()
    {
        var state = Apply(Start(), new Move("hale", new Coord(1, 1)), new Wait("hale"));

        Assert.False(state.Find("hale")!.Braced);
    }

    [Fact]
    public void ASleepingGuardDoesNotBraceAndAnAwakeOneDoes()
    {
        var map = Field(true, """
            P captain 0,0
            E brigand 6,4 group:far behavior:guard
            E soldier 6,0 group:post behavior:hold

            """);
        var state = Apply(BattleFixture.Start(map: map), new EndPhase());
        Assert.False(state.IsAwake("far"));

        state = Apply(state, new Wait("brigand-1"), new Wait("soldier-1"));

        Assert.False(state.Find("brigand-1")!.Braced);
        Assert.True(state.Find("soldier-1")!.Braced);

        var awake = state with { AwakeGroups = state.AwakeGroups.Add("far") };
        Assert.True(Brace.BracesOnWait(awake, awake.Find("brigand-1")! with { Moved = false }));
    }

    [Fact]
    public void AUnitShovedBeforeItWaitsDoesNotBrace()
    {
        var map = Field(true, """
            P captain 1,2
            P recruit:wren 2,2
            E brigand 6,4 group:field behavior:aggressive

            """).Replace("brace: on", "brace: on\nshove: on");
        var state = Apply(BattleFixture.Start(map: map), new Shove("hale", "wren"), new Wait("wren"));

        Assert.False(state.Find("wren")!.Braced);
    }

    [Fact]
    public void AGuardWokenMidPhaseAfterItWaitedAsleepIsStruckAtFullHit()
    {
        var map = Field(true, """
            P captain 1,4
            P recruit:wren 1,0
            E brigand 6,4 group:camp behavior:guard
            E soldier 6,0 group:camp behavior:guard

            """);
        var state = Apply(BattleFixture.Start(map: map), new Wait("hale"), new Wait("wren"), new EndPhase());
        state = Apply(state, new Wait("brigand-1"), new Wait("soldier-1"), new EndPhase());
        Assert.False(state.IsAwake("camp"));
        Assert.False(state.Find("brigand-1")!.Braced);

        state = Apply(state, new Move("wren", new Coord(5, 0)), new Attack("wren", "soldier-1"));
        Assert.True(state.IsAwake("camp"));

        state = Apply(state, new Move("hale", new Coord(5, 4)));
        var brigand = state.Find("brigand-1")!;
        Assert.False(brigand.Braced);
        Assert.Equal(0, state.Find("hale")!.ToCombatant(state, Starter, against: brigand).HitModifier);
    }

    [Fact]
    public void AnEnemyWithTwoOtherwiseEqualTargetsStrikesTheUnbracedOne()
    {
        var map = Field(true, """
            P captain 6,4
            P recruit:wren 1,2
            E soldier 3,0 group:field behavior:aggressive

            """);
        var start = BattleFixture.Start(map: map);
        var wren = start.Find("wren")!;
        var twin = wren with { Unit = wren.Unit with { Id = "twin" }, At = new Coord(5, 2) };
        start = Apply(start with { Units = start.Units.Add(twin) }, new EndPhase());

        foreach (var (braced, open) in new[] { ("wren", "twin"), ("twin", "wren") })
        {
            var state = start.WithUnit(start.Find(braced)! with { Braced = true });
            var plan = EnemyAi.PlanUnit(state, Starter, state.Find("soldier-1")!);

            Assert.Equal(open, plan.OfType<Attack>().Single().TargetId);
        }
    }

    [Fact]
    public void ARecallRestoresTheBrace()
    {
        var state = Apply(Start(), new Wait("hale"));
        var index = state.History.Count;
        state = Apply(state, new EndPhase(), new EndPhase());
        Assert.False(state.Find("hale")!.Braced);

        state = Apply(state, new Recall(index));

        Assert.True(state.Find("hale")!.Braced);
    }

    [Fact]
    public void EveryShippedMapLeavesTheHeaderOffAndItsWaitsBraceNobody()
    {
        var files = Directory.GetFiles(MapFixture.MapsDirectory, "*.map", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            Assert.False(MapFiles.Load(file, MapFixture.Content).BraceEnabled, file);
        }

        var result = Start(brace: false).Try(new Wait("hale"));
        Assert.Equal("""{"type":"unitWaited","unit":"hale"}""", ProtocolJson.Event(Assert.Single(result.Events)));
    }

    [Fact]
    public void WithoutTheHeaderNobodyBraces()
    {
        var state = Apply(Start(brace: false), new Wait("hale"));

        Assert.False(state.Find("hale")!.Braced);
        Assert.Equal(0, Brace.HitAgainst(state.Find("hale")));
    }

    [Fact]
    public void TheBraceEndsWhenTheUnitsSideBeginsItsNextPhase()
    {
        var enemy = EnemyPhaseAfter(Start(), new Wait("hale"));
        Assert.True(enemy.Find("hale")!.Braced);

        var player = Apply(enemy, new EndPhase());

        Assert.False(player.Find("hale")!.Braced);
    }

    [Fact]
    public void AShoveTakesTheBraceOff()
    {
        var map = Field(true, """
            P captain 1,2
            P recruit:wren 2,2
            E brigand 6,4 group:field behavior:aggressive

            """).Replace("brace: on", "brace: on\nshove: on");
        var state = Apply(BattleFixture.Start(map: map), new Wait("wren"));
        Assert.True(state.Find("wren")!.Braced);

        state = Apply(state, new Shove("hale", "wren"));

        Assert.False(state.Find("wren")!.Braced);
    }

    private static readonly Unit Rider = Recruit("rider", "outrider", new Stats(40, 9, 0, 9, 9, 5, 9, 2, 3), "iron_lance");

    private static BattleState RiderStart() => BattleFixture.Start(roster: ValueList<Unit>.Of(Hale, Rider), map: Field(true, """
        P captain 1,2
        P recruit:rider 2,2
        E brigand 6,4 group:field behavior:aggressive

        """));

    [Fact]
    public void ACantoOffTheTileTakesTheBraceOff()
    {
        var state = Apply(RiderStart(), new Wait("rider"));
        Assert.True(state.Find("rider")!.Braced);

        state = Apply(state, new Canto("rider", new Coord(2, 3)));

        Assert.Equal(new Coord(2, 3), state.Find("rider")!.At);
        Assert.False(state.Find("rider")!.Braced);
    }

    [Fact]
    public void ACantoThatStaysKeepsTheBrace()
    {
        var state = Apply(RiderStart(), new Wait("rider"), new Canto("rider", new Coord(2, 2)));

        Assert.True(state.Find("rider")!.Braced);
    }

    [Fact]
    public void ThePlannerScoresAStrikeOnABracedUnitBelowTheSameStrikeUnbraced()
    {
        var state = Apply(Start(), new Wait("hale"));
        var brigand = state.Find("brigand-1")!;
        var hale = state.Find("hale")!;

        var braced = EnemyAi.Score(state, Starter, brigand, brigand.At, hale);
        var open = EnemyAi.Score(state, Starter, brigand, brigand.At, hale with { Braced = false });

        Assert.True(braced < open, $"braced {braced} should score below open {open}");
    }

    [Fact]
    public void TheWaitSaysItBracesAndTheForecastPrintsTheBrace()
    {
        var result = Start().Try(new Wait("hale"));
        var state = result.Next;

        Assert.Equal(new UnitWaited("hale", Braced: true), Assert.Single(result.Events));
        Assert.Equal(new[] { "  brace: hale braced: brigand-1 hit -15" }, PlaySession.BraceLines(state.Find("brigand-1")!, state.Find("hale")!).ToList());
        Assert.Empty(PlaySession.BraceLines(Start().Find("brigand-1")!, Start().Find("hale")!));
    }

    [Fact]
    public void ThreatOnTheUnitsOwnTilePricesTheBraceItWouldTake()
    {
        var state = Start();
        var hale = state.Find("hale")!;

        var text = PlaySession.BracedThreat(state, Starter, hale, hale.At);
        var plain = Queries.Threats(state, Starter, hale, hale.At)!.Single();

        Assert.NotNull(text);
        Assert.StartsWith("if hale waits here it braces (hit -15):\n", text);
        var after = state.WithUnit(hale with { Braced = true });
        var braced = Queries.Threats(after, Starter, after.Find("hale")!, hale.At)!.Single();
        Assert.Equal(plain.Forecast.Attacker.HitChance - Brace.Hit, braced.Forecast.Attacker.HitChance);
        Assert.EndsWith(PlaySession.ThreatText(after, Starter, after.Find("hale")!, hale.At, new[] { braced }, Queries.SleepingThreats(after, Starter, after.Find("hale")!, hale.At)!, Queries.Unseeing(after, Starter, after.Find("hale")!, hale.At)), text);
        Assert.Null(PlaySession.BracedThreat(state, Starter, hale, new Coord(1, 1)));
        Assert.Null(PlaySession.BracedThreat(Start(brace: false), Starter, hale, hale.At));

        var quiet = BattleFixture.Start(map: Field(true, """
            P captain 0,0
            E soldier 6,4 group:post behavior:hold

            """));
        Assert.Null(PlaySession.BracedThreat(quiet, Starter, quiet.Find("hale")!, new Coord(0, 0)));
    }

    [Fact]
    public void TheBracedFlagRoundTripsThroughTheProtocol()
    {
        var state = Apply(Start(), new Wait("hale"));

        var json = ProtocolJson.State(state, Starter);
        var back = ProtocolJson.ReadState(json, Starter);

        Assert.Contains("\"braced\":true", json);
        Assert.True(back.Find("hale")!.Braced);
        Assert.Equal("""{"type":"unitWaited","unit":"hale","braced":true}""", ProtocolJson.Event(new UnitWaited("hale", true)));
    }

    [Fact]
    public void TheBraceHeaderRoundTripsAndTheSampleCarriesIt()
    {
        var map = MapFixture.Parse(Field(true, Line), "field.map");
        Assert.True(map.BraceEnabled);
        Assert.Contains("brace: on\n", MapFormat.Write(map, Starter));
        Assert.Contains(MapRenderer.BraceLegend, MapRenderer.Render(map, Starter));

        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var path = Path.Combine(repo, "docs", "samples", "harrow_weir_brace.map");
        var sample = MapFiles.Load(path, MapFixture.Content);
        Assert.True(sample.BraceEnabled);
        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), MapFormat.Write(sample, Starter));
    }

    /// <summary>
    /// Issue 430: 13.14's keep round sample is the shipped Saltmarsh Ford with only
    /// <c>brace: on</c> added, so a play of it reads against the plain map's re-rate (#131).
    /// </summary>
    [Fact]
    public void TheSaltmarshBraceSampleIsTheShippedMapWithOnlyTheBraceHeaderAdded()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var shippedPath = Path.Combine(repo, "content", "maps", "saltmarsh_ford.map");
        var samplePath = Path.Combine(repo, "docs", "samples", "saltmarsh_ford_brace.map");
        var shipped = File.ReadAllText(shippedPath).Replace("\r\n", "\n").Split('\n').ToList();
        var sampleText = File.ReadAllText(samplePath).Replace("\r\n", "\n");
        var sampleLines = sampleText.Split('\n').ToList();

        var at = sampleLines.IndexOf("brace: on");
        Assert.True(at >= 0);
        sampleLines.RemoveAt(at);
        Assert.Equal(shipped, sampleLines);

        var sample = MapFiles.Load(samplePath, MapFixture.Content);
        var original = MapFiles.Load(shippedPath, MapFixture.Content);
        Assert.True(sample.BraceEnabled);
        Assert.False(original.BraceEnabled);
        Assert.Equal(original with { BraceEnabled = true }, sample);
        Assert.Equal(sampleText, MapFormat.Write(sample, Starter));
    }
}
