using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Wildfire (DESIGN.md 13.15, experiment; issue 435's acceptance): on a <c>wildfire: on</c>
/// map a hit from an igniting weapon on a unit in forest sets the tile alight after the combat,
/// fire burns a unit standing in it at its side's phase start (never below 1), and at each
/// player phase start fire burns out to plain and lights the forest orthogonally beside it.
/// </summary>
public class WildfireTests
{
    private static readonly Unit Pell = Recruit("pell", "adept", new Stats(18, 1, 7, 6, 6, 3, 2, 4, 2), "cinder");

    private static string Field(bool wildfire, string grid, string units) =>
        $"""
        name: Field
        size: 7x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(wildfire ? "wildfire: on" : "")}

        {grid}

        units:
        {units}
        """.Replace("\n\n\n", "\n\n");

    private const string Wood = """
        .......
        .......
        ...^...
        .......
        .......
        """;

    private static BattleState Start(bool wildfire, string grid, string units, ulong seed = 7) =>
        BattleFixture.Start(seed, ValueList<Unit>.Of(Hale, Pell), Field(wildfire, grid, units));

    private const string Duel = """
        P captain 0,0
        P recruit:pell 2,2
        E brigand 3,2 group:field behavior:guard

        """;

    /// <summary>The attack's events and the state after it, for each of forty seeds.</summary>
    private static IEnumerable<(BattleState After, bool Hit)> Attacks(bool wildfire, string attacker, string grid, string units)
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var result = Resolver.Apply(Start(wildfire, grid, units, seed), Starter, new Attack(attacker, "brigand-1"));
            Assert.True(result.Accepted, result.Rejection?.Message);
            var fought = result.Events.OfType<CombatFought>().Single();
            yield return (result.Next, fought.Strikes.Any(s => s.AttackerId == attacker && s.Hit));
        }
    }

    [Fact]
    public void ACinderHitOnAUnitInForestSetsTheTileAlightAndAMissDoesNot()
    {
        var games = Attacks(true, "pell", Wood, Duel).ToList();

        Assert.Contains(games, g => g.Hit);
        Assert.Contains(games, g => !g.Hit);
        Assert.All(games, g => Assert.Equal(g.Hit ? Wildfire.FireTerrainId : Wildfire.ForestTerrainId, g.After.Map.TerrainIdAt(new Coord(3, 2))));
    }

    [Fact]
    public void WithoutTheHeaderACinderHitSetsNothingAlight()
    {
        var games = Attacks(false, "pell", Wood, Duel).ToList();

        Assert.Contains(games, g => g.Hit);
        Assert.All(games, g => Assert.Equal(Wildfire.ForestTerrainId, g.After.Map.TerrainIdAt(new Coord(3, 2))));
    }

    [Fact]
    public void AWeaponThatDoesNotIgniteSetsNothingAlight()
    {
        const string units = """
            P captain 2,2
            P recruit:pell 0,0
            E brigand 3,2 group:field behavior:guard

            """;
        var games = Attacks(true, "hale", Wood, units).ToList();

        Assert.False(Starter.Weapon("iron_sword").Ignites);
        Assert.True(Starter.Weapon("cinder").Ignites);
        Assert.Contains(games, g => g.Hit);
        Assert.All(games, g => Assert.Equal(Wildfire.ForestTerrainId, g.After.Map.TerrainIdAt(new Coord(3, 2))));
    }

    [Fact]
    public void ACinderHitOnPlainSetsNothingAlight()
    {
        const string plain = """
            .......
            .......
            .......
            .......
            .......
            """;
        var games = Attacks(true, "pell", plain, Duel).ToList();

        Assert.Contains(games, g => g.Hit);
        Assert.All(games, g => Assert.DoesNotContain(Wildfire.FireTerrainId, g.After.Map.TerrainIds));
    }

    private const string Front = """
        .^^....
        .^%^...
        ..^....
        .......
        .......
        """;

    private const string Bystanders = """
        P captain 2,4
        P recruit:pell 6,4
        E brigand 6,0 group:field behavior:guard

        """;

    [Fact]
    public void AtThePlayerPhaseStartFireBurnsOutAndLightsTheForestBesideItButNotDiagonally()
    {
        var enemyPhase = Start(true, Front, Bystanders).Do(new EndPhase());

        Assert.Equal(Wildfire.FireTerrainId, enemyPhase.Map.TerrainIdAt(new Coord(2, 1)));
        Assert.Equal(Wildfire.ForestTerrainId, enemyPhase.Map.TerrainIdAt(new Coord(2, 0)));

        var result = Resolver.Apply(enemyPhase, Starter, new EndPhase());
        var next = result.Next;

        Assert.Equal(Wildfire.BurntTerrainId, next.Map.TerrainIdAt(new Coord(2, 1)));
        Assert.Equal(Wildfire.FireTerrainId, next.Map.TerrainIdAt(new Coord(2, 0)));
        Assert.Equal(Wildfire.FireTerrainId, next.Map.TerrainIdAt(new Coord(1, 1)));
        Assert.Equal(Wildfire.FireTerrainId, next.Map.TerrainIdAt(new Coord(3, 1)));
        Assert.Equal(Wildfire.FireTerrainId, next.Map.TerrainIdAt(new Coord(2, 2)));
        Assert.Equal(Wildfire.ForestTerrainId, next.Map.TerrainIdAt(new Coord(1, 0)));
        Assert.Equal(
            new[] { new Coord(2, 0), new Coord(1, 1), new Coord(2, 1), new Coord(3, 1), new Coord(2, 2) },
            result.Events.OfType<TerrainChanged>().Select(t => t.At));
    }

    [Fact]
    public void WithoutTheHeaderFireNeverSpreads()
    {
        var next = Start(false, Front, Bystanders).Do(new EndPhase()).Do(new EndPhase());

        Assert.Equal(Wildfire.FireTerrainId, next.Map.TerrainIdAt(new Coord(2, 1)));
        Assert.Equal(Wildfire.ForestTerrainId, next.Map.TerrainIdAt(new Coord(2, 0)));
    }

    [Fact]
    public void AUnitOnFireAtItsPhaseStartLosesTwentyPercentOfItsMaxHp()
    {
        const string units = """
            P captain 6,4
            P recruit:pell 5,4
            E brigand 2,1 group:field behavior:guard

            """;
        var start = Start(true, Front, units);
        var brigand = start.Find("brigand-1")!;
        var max = brigand.MaxHp(Starter);

        var result = Resolver.Apply(start, Starter, new EndPhase());

        Assert.Equal(20, Starter.TerrainById(Wildfire.FireTerrainId).BurnPercent);
        Assert.Equal(brigand.Hp - max * 20 / 100, result.Next.Find("brigand-1")!.Hp);
        Assert.Contains(new UnitBurned("brigand-1", max * 20 / 100, brigand.Hp - max * 20 / 100), result.Events);
    }

    [Fact]
    public void ATileTheSpreadLightsHarmsNoPlayerUnitAtThatPhaseStart()
    {
        const string units = """
            P captain 2,0
            P recruit:pell 6,4
            E brigand 6,0 group:field behavior:guard

            """;
        var start = Start(true, Front, units);
        var hale = start.Find("hale")!;

        var caught = start.Do(new EndPhase()).Do(new EndPhase());

        Assert.Equal(Wildfire.FireTerrainId, caught.Map.TerrainIdAt(new Coord(2, 0)));
        Assert.Equal(hale.Hp, caught.Find("hale")!.Hp);

        var result = Resolver.Apply(caught.Do(new EndPhase()), Starter, new EndPhase());

        Assert.Equal(hale.Hp - hale.MaxHp(Starter) * 20 / 100, result.Next.Find("hale")!.Hp);
        Assert.Equal(Wildfire.BurntTerrainId, result.Next.Map.TerrainIdAt(new Coord(2, 0)));
    }

    [Fact]
    public void APlayerUnitOnFireAtItsPhaseStartBurnsBeforeTheTileBurnsOut()
    {
        const string units = """
            P captain 2,1
            P recruit:pell 6,4
            E brigand 6,0 group:field behavior:guard

            """;
        var start = Start(true, Front, units);
        var hale = start.Find("hale")!;

        var result = Resolver.Apply(start.Do(new EndPhase()), Starter, new EndPhase());

        Assert.Equal(hale.Hp - hale.MaxHp(Starter) * 20 / 100, result.Next.Find("hale")!.Hp);
        Assert.Equal(Wildfire.BurntTerrainId, result.Next.Map.TerrainIdAt(new Coord(2, 1)));
        var burned = result.Events.ToList().FindIndex(e => e is UnitBurned);
        var burntOut = result.Events.ToList().FindIndex(e => e is TerrainChanged t && t.At == new Coord(2, 1));
        Assert.True(burned >= 0 && burned < burntOut, "the burn comes before the burn-out");
    }

    private const string Approach = """
        .......
        .......
        .......
        .......
        .......
        """;

    private static Coord EndTile(bool wildfire, string grid, string units)
    {
        var state = Start(wildfire, grid, units);
        var enemyPhase = state.Do(new EndPhase());
        var plan = EnemyAi.PlanUnit(enemyPhase, Starter, enemyPhase.Find("brigand-1")!);
        return plan.OfType<Move>().Single().To;
    }

    [Fact]
    public void AnEnemyWithTwoOtherwiseEqualStrikesTakesTheOneNotOnFire()
    {
        const string grid = """
            .......
            .......
            ...%...
            .......
            .......
            """;
        const string units = """
            P captain 3,3
            P recruit:pell 6,4
            E brigand 3,0 group:field behavior:aggressive

            """;

        Assert.Equal(new Coord(3, 2), EndTile(false, grid, units));
        Assert.NotEqual(new Coord(3, 2), EndTile(true, grid, units));
    }

    [Fact]
    public void AnEnemyWithTwoOtherwiseEqualStrikesTakesTheOneTheNextFrontMisses()
    {
        const string grid = """
            .......
            .......
            .......
            .%^.^..
            .......
            """;
        const string units = """
            P captain 3,3
            P recruit:pell 6,4
            E brigand 3,1 group:field behavior:aggressive

            """;

        Assert.Equal(new Coord(2, 3), EndTile(false, grid, units));
        Assert.Equal(new Coord(4, 3), EndTile(true, grid, units));
    }

    [Fact]
    public void AnApproachingEnemyStopsOutOfTheFire()
    {
        const string grid = """
            .......
            .......
            .......
            .......
            .......
            ...%...
            .......
            .......
            .......
            .......
            """;
        const string units = """
            P captain 3,9
            P recruit:pell 6,9
            E brigand 3,2 group:field behavior:aggressive

            """;
        var map = $"""
            name: Field
            size: 7x10
            win: rout
            turn_limit: 10
            recall: 3
            enemy_level: 1
            wildfire: on

            {grid}

            units:
            {units}
            """;
        var plain = map.Replace("wildfire: on\n", "");
        Coord EndOf(string text)
        {
            var enemyPhase = BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Pell), text).Do(new EndPhase());
            return EnemyAi.PlanUnit(enemyPhase, Starter, enemyPhase.Find("brigand-1")!).OfType<Move>().Single().To;
        }

        Assert.Equal(new Coord(3, 5), EndOf(plain));
        Assert.NotEqual(new Coord(3, 5), EndOf(map));
    }

    [Fact]
    public void FireNeverTakesAUnitBelowOne()
    {
        const string units = """
            P captain 6,4
            P recruit:pell 5,4
            E brigand 2,1 group:field behavior:guard

            """;
        var start = Start(true, Front, units);
        start = start.WithUnit(start.Find("brigand-1")! with { Hp = 1 });

        var result = Resolver.Apply(start, Starter, new EndPhase());

        Assert.Equal(1, result.Next.Find("brigand-1")!.Hp);
        Assert.Empty(result.Events.OfType<UnitBurned>());
    }

    [Fact]
    public void TheProtocolCarriesTheFireTheFrontAndWhoIsBurning()
    {
        const string units = """
            P captain 2,1
            P recruit:pell 6,4
            E brigand 6,0 group:field behavior:guard

            """;
        var state = Start(true, Front, units);

        var board = Ironwake.Content.Protocol.ProtocolJson.BoardState(state, Starter);
        var full = Ironwake.Content.Protocol.ProtocolJson.BoardState(Start(false, Front, units), Starter);

        Assert.Contains("\"nextFront\":[{\"x\":2,\"y\":0},{\"x\":1,\"y\":1},{\"x\":3,\"y\":1},{\"x\":2,\"y\":2}]", board);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(board, "\"burning\":true"));
        Assert.DoesNotContain("nextFront", full);
    }

    [Fact]
    public void TheBoardMarksTheNextFrontAndTheForecastSaysIgnites()
    {
        var state = Start(true, Front, Bystanders);

        Assert.Equal("fire: burning 2,1; next front 2,0 1,1 3,1 2,2", MapRenderer.FireLine(state.Map));
        Assert.Null(MapRenderer.FireLine(Start(true, Wood, Duel).Map));

        var duel = Start(true, Wood, Duel);
        var pell = duel.Find("pell")!;
        var lines = Ironwake.Cli.PlaySession.IgniteLines(duel, Starter, pell, duel.Find("brigand-1")!, pell.At, null, counters: true).ToList();
        Assert.Equal(new[] { "  wildfire: pell ignites 3,2 on a hit" }, lines);
        Assert.Empty(Ironwake.Cli.PlaySession.IgniteLines(Start(false, Wood, Duel), Starter, pell, duel.Find("brigand-1")!, pell.At, null, counters: true));
    }

    [Fact]
    public void ARecallRestoresTheForestTheFireTook()
    {
        var hit = Attacks(true, "pell", Wood, Duel).First(g => g.Hit).After;
        Assert.Equal(Wildfire.FireTerrainId, hit.Map.TerrainIdAt(new Coord(3, 2)));

        var back = hit.Do(new Recall(0));

        Assert.Equal(Wildfire.ForestTerrainId, back.Map.TerrainIdAt(new Coord(3, 2)));
    }

    [Fact]
    public void TheHeaderRoundTripsThroughTheMapFormat()
    {
        var map = MapFixture.Parse(Field(true, Wood, Duel), "field.map");

        Assert.True(map.WildfireEnabled);
        Assert.Contains("wildfire: on\n", MapFormat.Write(map, Starter));
        Assert.False(MapFixture.Parse(Field(false, Wood, Duel), "field.map").WildfireEnabled);
    }

    [Fact]
    public void TheShippedTollgateCarriesNoWildfireAndTheSampleCarriesOnlyTheHeader()
    {
        var root = RepoRoot();
        var shipped = File.ReadAllText(Path.Combine(root, "content", "maps", "the_tollgate.map"));
        var sample = File.ReadAllText(Path.Combine(root, "docs", "samples", "the_tollgate_wildfire.map"));

        Assert.DoesNotContain("wildfire", shipped);
        Assert.Equal(shipped, sample.Replace("wildfire: on\n", ""));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ironwake.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
