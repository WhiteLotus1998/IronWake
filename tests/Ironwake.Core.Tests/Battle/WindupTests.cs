using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The windup (DESIGN.md 13.16, experiment; issue 441's acceptance): on a <c>windup: on</c> map
/// an attack with a windup weapon raises a blow over the target's tile instead of fighting; the
/// blow lands at the wielder's side's next phase start on whoever stands there, a certain hit for
/// a normal hit's damage, falls on an empty tile, and is broken by a hit on the wielder from within its weapon's range.
/// </summary>
public class WindupTests
{
    private static readonly Coord Door = new(2, 2);

    private static string Field(bool windup, string units, int height = 5) =>
        $"""
        name: Field
        size: 7x{height}
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(windup ? "windup: on" : "")}

        {string.Join("\n", Enumerable.Repeat(".......", height))}

        units:
        {units}
        """.Replace("\n\n\n", "\n\n");

    private const string Duel = """
        P captain 2,2
        P recruit:wren 0,0
        E toll_mauler 3,2 group:field behavior:aggressive
        E brigand 6,4 group:far behavior:hold

        """;

    private static BattleState Start(bool windup, string units = Duel, ulong seed = 7) =>
        BattleFixture.Start(seed, ValueList<Unit>.Of(Hale, Wren), Field(windup, units));

    /// <summary>The enemy phase after the player ends turn 1, the mauler's attack on hale applied.</summary>
    private static ApplyResult Raise(bool windup = true, ulong seed = 7) =>
        Start(windup, seed: seed).Do(new EndPhase()).Try(new Attack("toll_mauler-1", "hale"));

    [Fact]
    public void AMaulAttackRaisesABlowOverTheTargetsTileAndFightsNothing()
    {
        var result = Raise();

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Empty(result.Events.OfType<CombatFought>());
        Assert.Contains(new BlowRaised("toll_mauler-1", "hale", Door), result.Events);
        var mauler = result.Next.Find("toll_mauler-1")!;
        Assert.Equal(Door, mauler.WindupAt);
        Assert.True(mauler.Acted);
        Assert.Equal(Start(true).Find("hale")!.Hp, result.Next.Find("hale")!.Hp);
        Assert.Equal(20, mauler.Unit.Inventory.Items[0].Uses);
    }

    [Fact]
    public void WithoutTheHeaderTheMaulFightsAsAnyWeapon()
    {
        var result = Raise(windup: false);

        Assert.Single(result.Events.OfType<CombatFought>());
        Assert.Empty(result.Events.OfType<BlowRaised>());
        Assert.Null(result.Next.Find("toll_mauler-1")!.WindupAt);
        Assert.True(Starter.Weapon("post_maul").Windup);
        Assert.False(Starter.Weapon("iron_axe").Windup);
    }

    [Fact]
    public void TheBlowLandsOnTheUnitStandingThereAtTheWieldersNextPhaseStartForANormalHitsDamage()
    {
        var raised = Raise().Next;
        var playerPhase = raised.Do(new EndPhase());
        var hale = playerPhase.Find("hale")!;
        var expected = Windup.Damage(playerPhase, Starter, playerPhase.Find("toll_mauler-1")!, hale);
        Assert.Equal(hale.Hp, raised.Find("hale")!.Hp);

        var result = playerPhase.Do(new Wait("hale")).Try(new EndPhase());

        Assert.True(expected > 0);
        Assert.Contains(new BlowLanded("toll_mauler-1", "hale", Door, expected, hale.Hp - expected), result.Events);
        Assert.Equal(hale.Hp - expected, result.Next.Find("hale")!.Hp);
        Assert.Null(result.Next.Find("toll_mauler-1")!.WindupAt);
    }

    [Fact]
    public void TheBlowFallsOnAnEmptyTileAndHarmsNobody()
    {
        var playerPhase = Raise().Next.Do(new EndPhase());
        var hp = playerPhase.Find("hale")!.Hp;

        var result = playerPhase.Do(new Move("hale", new Coord(1, 1))).Do(new Wait("hale")).Try(new EndPhase());

        Assert.Contains(new BlowFell("toll_mauler-1", Door), result.Events);
        Assert.Empty(result.Events.OfType<BlowLanded>());
        Assert.Equal(hp, result.Next.Find("hale")!.Hp);
        Assert.Null(result.Next.Find("toll_mauler-1")!.WindupAt);
    }

    [Fact]
    public void TheBlowLandsOnAnEnemyStandingUnderIt()
    {
        var raised = Raise().Next;
        var underBlow = raised
            .WithUnit(raised.Find("brigand-1")! with { At = new Coord(1, 2) })
            .WithUnit(raised.Find("hale")! with { At = new Coord(0, 4) })
            .WithUnit(raised.Find("toll_mauler-1")! with { WindupAt = new Coord(1, 2) });

        var result = underBlow.Do(new EndPhase()).Try(new EndPhase());

        var landed = Assert.Single(result.Events.OfType<BlowLanded>());
        Assert.Equal("brigand-1", landed.TargetId);
        Assert.True(landed.Damage > 0);
    }

    [Fact]
    public void AnAdjacentHitOnTheWielderBreaksTheBlowAndAMissDoesNot()
    {
        var broken = 0;
        var kept = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var playerPhase = Raise(seed: seed).Next.Do(new EndPhase());
            playerPhase = playerPhase with { Map = playerPhase.Map.WithTerrain(new Coord(3, 2), "forest") };
            var result = playerPhase.Try(new Attack("hale", "toll_mauler-1"));
            Assert.True(result.Accepted, result.Rejection?.Message);
            var hit = result.Events.OfType<CombatFought>().Single().Strikes.Any(s => s.TargetId == "toll_mauler-1" && s.Hit);
            var mauler = result.Next.Find("toll_mauler-1");
            if (mauler is null)
            {
                continue;
            }

            if (hit)
            {
                broken++;
                Assert.Null(mauler.WindupAt);
                Assert.Contains(new BlowBroken("toll_mauler-1", Door), result.Events);
            }
            else
            {
                kept++;
                Assert.Equal(Door, mauler.WindupAt);
                Assert.Empty(result.Events.OfType<BlowBroken>());
            }
        }

        Assert.True(broken > 0 && kept > 0, $"broken {broken}, kept {kept}");
    }

    private static readonly Unit Pell = Recruit("pell", "adept", new Stats(18, 1, 7, 6, 6, 3, 2, 4, 2), "cinder");

    private const string Perch = """
        P captain 2,2
        P recruit:pell 3,4
        E toll_mauler 3,2 group:field behavior:aggressive
        E brigand 6,4 group:far behavior:hold

        """;

    private static BattleState PerchPhase(ulong seed) =>
        BattleFixture.Start(seed, ValueList<Unit>.Of(Hale, Pell), Field(true, Perch))
            .Do(new EndPhase()).Do(new Attack("toll_mauler-1", "hale")).Do(new EndPhase());

    [Fact]
    public void AHitFromOutsideTheWieldersReachDoesNotBreakTheBlow()
    {
        var hits = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var result = PerchPhase(seed).Try(new Attack("pell", "toll_mauler-1"));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (!result.Events.OfType<CombatFought>().Single().Strikes.Any(s => s.AttackerId == "pell" && s.Hit) || result.Next.Find("toll_mauler-1") is null)
            {
                continue;
            }

            hits++;
            Assert.Equal(Door, result.Next.Find("toll_mauler-1")!.WindupAt);
            Assert.Empty(result.Events.OfType<BlowBroken>());
        }

        Assert.True(hits > 0);
    }

    [Fact]
    public void TheForecastSaysWhetherAHitBreaksTheBlowAndWhatItLandsOnTheStriker()
    {
        var state = PerchPhase(7);
        var mauler = state.Find("toll_mauler-1")!;
        var pell = state.Find("pell")!;
        var hale = state.Find("hale")!;

        Assert.Equal(
            new[] { "  windup: a hit from 3,4 does not break toll_mauler-1's blow over 2,2 (outside toll_mauler-1's reach)" },
            Ironwake.Cli.PlaySession.WindupLines(state, Starter, pell, mauler, null).ToList());
        Assert.Equal(
            new[]
            {
                "  windup: a hit on toll_mauler-1 breaks toll_mauler-1's blow over 2,2",
                $"  windup: toll_mauler-1's blow lands on 2,2 at toll_mauler-1's next phase start: {Windup.Damage(state, Starter, mauler, hale)} to hale, sure",
            },
            Ironwake.Cli.PlaySession.WindupLines(state, Starter, hale, mauler, null).ToList());
    }

    [Fact]
    public void TheWindupLinesNameTheWielderAsAReaderSeesThemWhenGivenNames()
    {
        var state = PerchPhase(7);
        var names = UnitNames.Of(state, Starter);
        var mauler = state.Find("toll_mauler-1")!;

        var lines = Ironwake.Cli.PlaySession.WindupLines(state, Starter, state.Find("hale")!, mauler, null, names).ToList();

        Assert.StartsWith($"  windup: a hit on {names["toll_mauler-1"]} breaks {names["toll_mauler-1"]}'s blow over 2,2", lines[0]);
        Assert.All(lines, l => Assert.DoesNotContain("toll_mauler-1", l));
    }

    [Fact]
    public void TheBlowCanKill()
    {
        var playerPhase = Raise().Next.Do(new EndPhase()).Do(new Wait("hale"));
        var dying = playerPhase.WithUnit(playerPhase.Find("hale")! with { Hp = 1 });

        var result = dying.Try(new EndPhase());

        Assert.Contains(new UnitDied("hale", Side.Player, Door), result.Events);
        Assert.Null(result.Next.Find("hale"));
        Assert.True(result.Next.Outcome.IsOver);
    }

    [Fact]
    public void ARecallRestoresARaisedBlow()
    {
        var playerPhase = Raise().Next.Do(new EndPhase());
        var landed = playerPhase.Do(new Wait("hale")).Do(new EndPhase());
        Assert.Null(landed.Find("toll_mauler-1")!.WindupAt);

        var back = landed.Do(new Recall(playerPhase.History.Count));

        Assert.Equal(Door, back.Find("toll_mauler-1")!.WindupAt);
    }

    [Fact]
    public void TheProtocolCarriesARaisedBlow()
    {
        var raised = Raise().Next;

        var json = ProtocolJson.State(raised, Starter);
        var read = ProtocolJson.ReadState(json, Starter);

        Assert.Contains("\"windupAt\":{\"x\":2,\"y\":2}", json);
        Assert.Equal(Door, read.Find("toll_mauler-1")!.WindupAt);
        Assert.DoesNotContain("windupAt", ProtocolJson.State(Start(true), Starter));
    }

    [Fact]
    public void AnEnemyDoesNotEndItsMoveUnderARaisedBlow()
    {
        const string units = """
            P captain 3,9
            P recruit:wren 6,9
            E brigand 3,2 group:field behavior:aggressive
            E toll_mauler 6,0 group:post behavior:hold

            """;
        Coord EndOf(Coord? mark)
        {
            var enemyPhase = BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Wren), Field(true, units, 10)).Do(new EndPhase());
            if (mark is not null)
            {
                enemyPhase = enemyPhase.WithUnit(enemyPhase.Find("toll_mauler-1")! with { WindupAt = mark });
            }

            return EnemyAi.PlanUnit(enemyPhase, Starter, enemyPhase.Find("brigand-1")!).OfType<Move>().Single().To;
        }

        var unmarked = EndOf(null);
        Assert.NotEqual(unmarked, EndOf(unmarked));
    }

    [Fact]
    public void TheBoardAndTheForecastSayWhatABlowWillDo()
    {
        var raised = Raise().Next;
        var playerPhase = raised.Do(new EndPhase());

        var landing = Windup.Damage(playerPhase, Starter, playerPhase.Find("toll_mauler-1")!, playerPhase.Find("hale")!);
        Assert.Equal($"blows: toll_mauler-1 over 2,2 (hale {landing}, sure)", MapRenderer.BlowLine(playerPhase, Starter));
        Assert.Null(MapRenderer.BlowLine(Start(true), Starter));
        var board = MapRenderer.Render(playerPhase, Starter);
        Assert.Contains(MapRenderer.WindupLegend, board);
        Assert.Contains($"under a blow from toll_mauler-1 ({landing}, sure)", board);
        Assert.Contains("winding up over 2,2", board);

        var hale = playerPhase.Find("hale")!;
        var mauler = playerPhase.Find("toll_mauler-1")!;
        Assert.Equal(
            new[]
            {
                "  windup: a hit on toll_mauler-1 breaks toll_mauler-1's blow over 2,2",
                $"  windup: toll_mauler-1's blow lands on 2,2 at toll_mauler-1's next phase start: {landing} to hale, sure",
            },
            Ironwake.Cli.PlaySession.WindupLines(playerPhase, Starter, hale, mauler, null).ToList());

        var enemyPhase = Start(true).Do(new EndPhase());
        var damage = Windup.Damage(enemyPhase, Starter, enemyPhase.Find("toll_mauler-1")!, enemyPhase.Find("hale")!);
        Assert.Equal(
            new[] { $"  windup: no combat now; toll_mauler-1 raises a blow over 2,2, landing at toll_mauler-1's next phase start on whoever stands there (hale: {damage}, sure) unless a hit from within its reach breaks it" },
            Ironwake.Cli.PlaySession.WindupLines(enemyPhase, Starter, enemyPhase.Find("toll_mauler-1")!, enemyPhase.Find("hale")!, null).ToList());
        Assert.Empty(Ironwake.Cli.PlaySession.WindupLines(Start(false).Do(new EndPhase()), Starter, mauler, hale, null));
    }

    private static string ThreatOn(BattleState state, string unitId)
    {
        var unit = state.Find(unitId)!;
        var lines = Queries.Threats(state, Starter, unit, unit.At)!;
        return Ironwake.Cli.PlaySession.ThreatText(state, Starter, unit, unit.At, lines, Queries.SleepingThreats(state, Starter, unit, unit.At)!);
    }

    [Fact]
    public void ARaiseThisPhaseIsPrintedButAddsNothingToTheThreatTotal()
    {
        var state = Start(true);
        var hale = state.Find("hale")!;
        var lines = Queries.Threats(state, Starter, hale, Door)!;
        var enemyPhase = state.Do(new EndPhase());
        var damage = Windup.Damage(enemyPhase, Starter, enemyPhase.Find("toll_mauler-1")!, enemyPhase.Find("hale")!);

        var raise = Assert.Single(lines);
        Assert.True(raise.Raises);
        Assert.Equal(0, raise.IfAllLand);
        Assert.Equal(0, Queries.IfAllLand(lines));
        var text = ThreatOn(state, "hale");
        Assert.Contains($"    Windup: no strike; Toll Mauler raises over 2,2, lands next enemy phase for {damage}, sure, unless a hit from within its reach breaks it (not in the total)\n", text);
        Assert.EndsWith($"  If all land: 0 against {hale.Hp} hp", text);

        var plain = Start(false);
        var strike = Assert.Single(Queries.Threats(plain, Starter, plain.Find("hale")!, Door)!);
        Assert.False(strike.Raises);
        Assert.Equal(damage, strike.Forecast.Attacker.Damage);
        Assert.True(Queries.IfAllLand(new[] { strike }) > 0);
        Assert.DoesNotContain("windup", ThreatOn(plain, "hale"));
    }

    [Fact]
    public void TheLegendSaysOnlyAHitFromWithinTheWieldersReachBreaksABlow()
    {
        Assert.EndsWith("a hit from within the wielder's reach breaks it", MapRenderer.WindupLegend);
        Assert.DoesNotContain("a hit on the wielder breaks it", MapRenderer.WindupLegend);
    }

    [Fact]
    public void ARaisePrintsNoHitChanceInTheThreatRowOrTheForecast()
    {
        var state = Start(true);
        var text = ThreatOn(state, "hale");
        var enemyPhase = state.Do(new EndPhase());
        var mauler = enemyPhase.Find("toll_mauler-1")!;
        var hale = enemyPhase.Find("hale")!;
        var damage = Windup.Damage(enemyPhase, Starter, mauler, hale);

        Assert.Contains($"  Toll Mauler from 3,2 with Post Maul (slot 1): dmg {damage} hit -- crit --; counter: none\n", text);
        Assert.DoesNotContain("%", text);

        var forecast = Queries.Forecast(enemyPhase, Starter, mauler, hale, mauler.At, null)!;
        var lines = Ironwake.Cli.PlaySession.ForecastText(enemyPhase, Starter, mauler, hale, forecast, mauler.At, false).Split('\n');
        Assert.Equal($"Forecast Toll Mauler -> hale: dmg {damage} hit -- crit --; counter: none", lines[0]);
        Assert.DoesNotContain(lines, l => l.Contains('%'));

        var plain = Start(false);
        Assert.Contains("%", ThreatOn(plain, "hale"));
    }

    [Fact]
    public void ABlowAlreadyRaisedOverTheTileIsInTheThreatTotal()
    {
        var playerPhase = Raise().Next.Do(new EndPhase());
        var hale = playerPhase.Find("hale")!;
        var landing = Windup.Damage(playerPhase, Starter, playerPhase.Find("toll_mauler-1")!, hale);
        var lines = Queries.Threats(playerPhase, Starter, hale, Door)!;

        var blow = Queries.RaisedBlowOn(playerPhase, Starter, hale, Door);
        Assert.Equal(new RaisedBlow(playerPhase.Find("toll_mauler-1")!, Door, landing), blow);
        Assert.Equal(0, Queries.IfAllLand(lines));
        Assert.Equal(landing, Queries.IfAllLand(lines, blow));
        var text = ThreatOn(playerPhase, "hale");
        Assert.StartsWith($"Threat on hale at 2,2 (Plain):\n  Toll Mauler's raised blow lands here at the enemy phase start: {landing}, sure, unless a hit from within its reach breaks it\n", text);
        Assert.EndsWith($"  If all land: {landing} against {hale.Hp} hp", text);

        Assert.Null(Queries.RaisedBlowOn(playerPhase, Starter, hale, new Coord(1, 2)));
        Assert.Null(Queries.RaisedBlowOn(Start(false), Starter, hale, Door));
    }

    [Fact]
    public void ABlowThatKillsIsStillPricedWhenNoStrikeFollows()
    {
        var playerPhase = Raise().Next.Do(new EndPhase());
        var dying = playerPhase.WithUnit(playerPhase.Find("hale")! with { Hp = 1 });
        var landing = Windup.Damage(dying, Starter, dying.Find("toll_mauler-1")!, dying.Find("hale")!);

        Assert.Empty(Queries.Threats(dying, Starter, dying.Find("hale")!, Door)!);
        var text = ThreatOn(dying, "hale");
        Assert.DoesNotContain("no enemy can strike it", text);
        Assert.EndsWith($"  If all land: {landing} against 1 hp", text);
    }

    [Fact]
    public void TheProtocolsThreatTotalCountsARaisedBlowAndNotARaise()
    {
        var playerPhase = Raise().Next.Do(new EndPhase());
        var hale = playerPhase.Find("hale")!;
        var landing = Windup.Damage(playerPhase, Starter, playerPhase.Find("toll_mauler-1")!, hale);

        var raised = new Ironwake.Cli.ProtocolSession(Starter, playerPhase, TextWriter.Null).Answer("""{"query":"threat","unit":"hale"}""");
        var fresh = new Ironwake.Cli.ProtocolSession(Starter, Start(true), TextWriter.Null).Answer("""{"query":"threat","unit":"hale"}""");

        Assert.Contains($"\"blow\":{{\"wielder\":\"toll_mauler-1\",\"damage\":{landing}}},\"ifAllLand\":{landing},", raised);
        Assert.Contains("\"ifAllLand\":0,\"raises\":true,", raised);
        Assert.Contains("],\"ifAllLand\":0,", fresh);
        Assert.DoesNotContain("\"blow\"", fresh);
    }

    [Fact]
    public void TheHeaderRoundTripsThroughTheMapFormat()
    {
        var map = MapFixture.Parse(Field(true, Duel), "field.map");

        Assert.True(map.WindupEnabled);
        Assert.Contains("windup: on\n", MapFormat.Write(map, Starter));
        Assert.False(MapFixture.Parse(Field(false, Duel), "field.map").WindupEnabled);
    }

    [Fact]
    public void TheSampleIsTheShippedTollgateWithTheHeaderAndTheMaulerForTheWarden()
    {
        var root = RepoRoot();
        var shipped = File.ReadAllText(Path.Combine(root, "content", "maps", "the_tollgate.map"));
        var sample = File.ReadAllText(Path.Combine(root, "docs", "samples", "the_tollgate_windup.map"));

        Assert.DoesNotContain("windup", shipped);
        Assert.Equal(shipped, sample.Replace("windup: on\n", "").Replace("E toll_mauler 6,2 ", "E toll_warden 6,2 "));
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
