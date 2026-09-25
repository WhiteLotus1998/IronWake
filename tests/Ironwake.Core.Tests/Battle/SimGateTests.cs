using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Battle;

using static BattleFixture;

/// <summary>
/// Issue 12's acceptance for gates 1 to 5 as functions: a one-shot enemy at deployment
/// fails gate 3 and the waiver prints instead of failing; a walled-off recruit fails gate
/// 4; a recruit within its own margin of the threshold passes; the heuristic player's
/// captain refuses a lethal tile and takes a survivable one.
/// </summary>
public class SimGateTests
{
    private const string OneShot = """
        name: Cheap shots
        size: 6x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 30

        ......
        ......
        ......
        ......

        units:
        P captain 0,0
        P recruit 5,3
        E brigand 4,3 group:yard behavior:aggressive
        E soldier 5,2 group:yard behavior:aggressive

        """;

    [Fact]
    public void GateThreeFailsWhenTheEnemyPhaseKillsFromDeployment()
    {
        var result = Gates.Gate3(Starter, MapFixture.Parse(OneShot), "cheap");
        Assert.False(result.Passed, result.Line);
        Assert.Contains("killed from deployment: FAILED", result.Line);
    }

    [Fact]
    public void GateThreePrintsTheWaiverInsteadOfSkippingSilently()
    {
        var waived = OneShot.Replace("enemy_level: 30", "enemy_level: 30\ncheap_shots: allowed");
        var result = Gates.Gate3(Starter, MapFixture.Parse(waived), "cheap");
        Assert.True(result.Passed, result.Line);
        Assert.Contains("waived by cheap_shots: allowed", result.Line);
        Assert.Contains("1 player units killed", result.Line);
    }

    /// <summary>
    /// Teodor starts inside the walls and can reach nothing. The brigands are level 3 and the
    /// test reads 60 seeds so that, at section 5's kept odds (DECISIONS/0028), benching Wren
    /// costs enough games for the median to clear its margin and name Teodor.
    /// </summary>
    private const string WalledOff = """
        name: Walled off
        size: 8x5
        win: survive
        turn_limit: 6
        recall: 3
        enemy_level: 3

        ........
        ..###...
        ..#.#...
        ..###...
        ........

        units:
        P captain 0,0
        P recruit 0,1
        P recruit 3,2
        E brigand 7,4 group:yard behavior:aggressive
        E brigand 7,3 group:yard behavior:aggressive
        E brigand 6,4 group:yard behavior:aggressive

        """;

    [Fact]
    public void GateFourFailsARecruitDeployedOutOfReachOfEverything()
    {
        var map = MapFixture.Parse(WalledOff);
        var (gate1, baseline) = Gates.Gate1(Starter, map, "walled", 60);
        var result = Gates.Gate4(Starter, map, "walled", baseline);
        Assert.False(result.Passed, gate1.Line + "\n" + result.Line);
        Assert.Contains("teodor: drop 0.000 se 0.000", result.Line);
        Assert.Contains("teodor: drop 0.000 se 0.000 own [atk 0 dmg 0 heal 0 abs 0]", result.Line);
        Assert.Contains("DEAD WEIGHT", result.Line);
        Assert.DoesNotContain("wren: drop 0.000 se 0.000", result.Line);
        Assert.Contains("captain captain: baseline [atk", result.Line);
    }

    /// <summary>
    /// Issue 105: the row compares the rest of the cast against itself across the bench. On the
    /// walled map wren stands beside the captain and absorbs; with wren benched the captain
    /// fights alone, deals more damage, and absorbs more, so the rest-of-cast mix differs
    /// between the two arms in the direction the row is meant to show: fewer bodies fought
    /// more. Damage is the number read, not attacks: since issue 117 the captain takes a
    /// certain kill in both arms and the attack counts meet.
    /// </summary>
    [Fact]
    public void GateFourPrintsTheRestOfTheCastOnBothSidesOfTheBench()
    {
        var map = MapFixture.Parse(WalledOff);
        var (_, baseline) = Gates.Gate1(Starter, map, "walled", 10);
        var result = Gates.Gate4(Starter, map, "walled", baseline);
        var wren = result.Line.Split('\n').Single(l => l.StartsWith("  wren:", StringComparison.Ordinal));
        var restBaseline = Between(wren, "rest baseline [", "]");
        var restBenched = Between(wren, "rest benched [", "]");
        Assert.NotEqual(restBaseline, restBenched);
        Assert.True(Damage(restBenched) > Damage(restBaseline), wren);
        Assert.True(Absorbed(restBenched) > Absorbed(restBaseline), wren);

        static string Between(string line, string open, string close)
        {
            var start = line.IndexOf(open, StringComparison.Ordinal) + open.Length;
            return line[start..line.IndexOf(close, start, StringComparison.Ordinal)];
        }

        static int Damage(string mix) => int.Parse(mix.Split(' ')[3], System.Globalization.CultureInfo.InvariantCulture);

        static int Absorbed(string mix) => int.Parse(mix.Split(' ')[7], System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Issue 105: an Escape map wins only when every living unit stands on an exit, and the
    /// second recruit starts nine tiles from it with a two-turn limit, so the party wins
    /// only with that recruit benched. The median drop is confidently negative and gate 4
    /// fails the cast on the benching line, with no recruit labelled. Twenty seeds, since the
    /// median of two rows is about -0.5 and the margin (issue 115) is twice the benched recruit's
    /// standard error, 1 / sqrt(seeds): at ten seeds it would read as a ceiling instead. Under
    /// section 5 as kept (DECISIONS/0028) the far recruit falls to the soldier in 2 of 20
    /// baseline games, which wins the escape, so the row reads -0.900 rather than -1.000.
    /// </summary>
    private const string OneBodyTooMany = """
        name: One body too many
        size: 10x3
        win: escape
        turn_limit: 2
        recall: 3
        enemy_level: 1
        exit: 0,0 0,1 0,2

        ..........
        ..........
        ..........

        units:
        P captain 1,1
        P recruit 2,1
        P recruit 9,1
        E soldier 9,0 group:far behavior:hold

        """;

    [Fact]
    public void GateFourFailsTheCastWhenBenchingTheMedianRecruitRaisesTheWinRate()
    {
        var map = MapFixture.Parse(OneBodyTooMany);
        var (gate1, baseline) = Gates.Gate1(Starter, map, "escape", 20);
        var result = Gates.Gate4(Starter, map, "escape", baseline);
        Assert.False(result.Passed, gate1.Line + "\n" + result.Line);
        Assert.Contains("cast not earning its deployment: benching the median recruit raises the win rate", result.Line);
        Assert.DoesNotContain("changes no outcomes", result.Line);
        Assert.Contains("teodor: drop -0.900", result.Line);
        Assert.DoesNotContain("DEAD WEIGHT", result.Line);
    }

    [Fact]
    public void JudgeNamesNobodyWhenTheMedianDropIsAtOrBelowZero()
    {
        var rows = new[]
        {
            new Gates.AblationRow("a", -0.65, 0.13, ActionMix.Zero, ActionMix.Zero, ActionMix.Zero),
            new Gates.AblationRow("b", -0.70, 0.14, ActionMix.Zero, ActionMix.Zero, ActionMix.Zero),
            new Gates.AblationRow("c", -0.05, 0.08, ActionMix.Zero, ActionMix.Zero, ActionMix.Zero),
            new Gates.AblationRow("d", -0.22, 0.09, ActionMix.Zero, ActionMix.Zero, ActionMix.Zero),
        };
        Assert.True(Gates.CastFails(rows));
        Assert.Empty(Gates.Judge(rows));

        var zero = new[] { new Gates.AblationRow("a", 0.0, 0.0, ActionMix.Zero, ActionMix.Zero, ActionMix.Zero) };
        Assert.True(Gates.CastFails(zero));
        Assert.Empty(Gates.Judge(zero));
    }

    /// <summary>
    /// Issue 115: the verdict fails unless the median is above zero by twice its row's
    /// standard error. The Tollgate's rows at 08f00f6 under both schemes fail on the ceiling
    /// line rather than flipping on the sign of noise; a cast of clear drops passes; a median
    /// row inside its own margin fails on the ceiling line; a confidently negative median fails
    /// on the benching line.
    /// </summary>
    [Fact]
    public void TheCastVerdictNeedsASignificantlyPositiveMedian()
    {
        var tollgateTwoRolls = new[]
        {
            Row("ottilie", -0.005, 0.005),
            Row("pell", 0.440, 0.047),
            Row("teodor", -0.005, 0.005),
            Row("wren", -0.005, 0.005),
        };
        var tollgateOneRoll = new[]
        {
            Row("ottilie", -0.005, 0.005),
            Row("pell", 0.170, 0.030),
            Row("teodor", 0.025, 0.013),
            Row("wren", 0.005, 0.009),
        };
        Assert.Equal(Gates.CastVerdictKind.NoOutcomesChange, Gates.CastVerdict(tollgateTwoRolls));
        Assert.Equal(Gates.CastVerdictKind.NoOutcomesChange, Gates.CastVerdict(tollgateOneRoll));
        Assert.Equal(0.013, Gates.MedianError(tollgateOneRoll), 6);
        Assert.True(Gates.CastFails(tollgateOneRoll));
        Assert.Empty(Gates.Judge(tollgateOneRoll));

        var clear = new[] { Row("a", 0.6, 0.05), Row("b", 0.6, 0.05), Row("c", 0.6, 0.05) };
        Assert.Equal(Gates.CastVerdictKind.Passes, Gates.CastVerdict(clear));
        Assert.False(Gates.CastFails(clear));

        var ceiling = new[] { Row("a", 0.300, 0.040), Row("b", 0.010, 0.008), Row("c", -0.020, 0.010) };
        Assert.Equal(Gates.CastVerdictKind.NoOutcomesChange, Gates.CastVerdict(ceiling));

        var benching = new[] { Row("a", -0.65, 0.13), Row("b", -0.70, 0.14), Row("c", -0.05, 0.08), Row("d", -0.22, 0.09) };
        Assert.Equal(Gates.CastVerdictKind.BenchingRaisesWins, Gates.CastVerdict(benching));
        Assert.Equal(Gates.CastVerdictKind.Passes, Gates.CastVerdict(Array.Empty<Gates.AblationRow>()));

        static Gates.AblationRow Row(string id, double drop, double se) => new(id, drop, se, ActionMix.Zero, ActionMix.Zero, ActionMix.Zero);
    }

    [Fact]
    public void ARecruitWithinItsOwnMarginOfTheThresholdPasses()
    {
        var rows = new[]
        {
            new Gates.AblationRow("a", 0.40, 0.03, ActionMix.Zero, ActionMix.Zero, ActionMix.Zero),
            new Gates.AblationRow("b", 0.40, 0.03, ActionMix.Zero, ActionMix.Zero, ActionMix.Zero),
            new Gates.AblationRow("c", 0.15, 0.03, ActionMix.Zero, ActionMix.Zero, ActionMix.Zero),
        };
        Assert.Empty(Gates.Judge(rows));

        var failing = rows.Append(new Gates.AblationRow("d", 0.05, 0.03, ActionMix.Zero, ActionMix.Zero, ActionMix.Zero)).ToList();
        Assert.Equal(new[] { "d" }, Gates.Judge(failing));
    }

    [Fact]
    public void TheCaptainRefusesATileWhereTheNoCritSumReachesHisHpAndTakesOneThatPasses()
    {
        const string map = """
            name: Veto
            size: 7x3
            win: rout
            turn_limit: 10
            recall: 3
            enemy_level: 12

            .......
            .......
            .......

            units:
            P captain 0,1
            E brigand 4,1 group:yard behavior:hold
            E soldier 3,0 group:yard behavior:hold
            E soldier 3,2 group:yard behavior:hold

            """;
        var state = Start(map: map, roster: ValueList<Unit>.Of(Hale));
        var hale = state.Find("hale")!;
        var plan = HeuristicPlayer.PlanUnit(state, Starter, hale);
        var move = Assert.IsType<Move>(plan[0]);
        var sum = Exposure.Of(state, Starter, hale, move.To, plan.Count > 1 && plan[1] is Attack a ? state.Find(a.TargetId) : null);
        Assert.True(sum.NoCrit < hale.Hp, $"{move.To}: no-crit sum {sum.NoCrit} against {hale.Hp} HP");
        Assert.True(Exposure.Of(state, Starter, hale, new Coord(3, 1)).NoCrit >= hale.Hp, "the tile beside all three is lethal, so the test is a real refusal");
        Assert.NotEqual(new Coord(3, 1), move.To);
    }

    /// <summary>
    /// Issue 141: a <c>protect:</c> map where the protected recruit's only attack tile,
    /// 4,1 beside the brigand, sits between two walls, so the soldiers behind the brigand
    /// cannot reach it and the brigand's own strikes are the whole sum, lethal on the
    /// enemy phase alone. The captain stands out of everyone's reach and plays no part.
    /// </summary>
    private const string Ward = """
        name: Ward
        size: 7x3
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 20
        protect: wren

        ....#..
        .......
        ....#..

        units:
        P captain 0,1
        P recruit:wren 2,1
        E brigand 5,1 group:yard behavior:hold
        E soldier 6,0 group:yard behavior:hold
        E soldier 6,2 group:yard behavior:hold

        """;

    /// <summary>
    /// Issue 141: the veto covers every unit whose death loses the map. On the Ward the
    /// protected recruit refuses her one attack tile because the no-crit sum there reaches
    /// her HP, and ends somewhere the sum does not; on the same board without the header
    /// she is an ordinary recruit and takes the attack.
    /// </summary>
    [Fact]
    public void TheProtectedRecruitRefusesALethalAttackTileAndAnUnprotectedOneTakesIt()
    {
        var attackTile = new Coord(4, 1);
        var guarded = Start(map: Ward);
        var wren = guarded.Find("wren")!;
        var brigand = guarded.Find("brigand-1")!;
        Assert.True(HeuristicPlayer.LosesTheMap(guarded, wren));
        Assert.True(HeuristicPlayer.LosesTheMap(guarded, guarded.UnitsOf(Side.Player).Single(u => u.IsCaptain)), "the captain is always covered");
        Assert.True(guarded.ReachOf(wren, Starter).CanEnd(attackTile), "the attack tile is in reach, so the refusal is real");
        Assert.True(Exposure.Of(guarded, Starter, wren, attackTile, brigand).NoCrit >= wren.Hp, "the sum at the attack tile reaches her HP, so the test is a real refusal");
        Assert.True(Exposure.Of(guarded, Starter, wren, attackTile).NoCrit >= wren.Hp, "standing there without attacking is lethal too, so the approach refuses the tile as well");

        var refused = HeuristicPlayer.PlanUnit(guarded, Starter, wren);
        Assert.DoesNotContain(refused, c => c is Attack);
        var ends = refused[0] is Move m ? m.To : wren.At;
        Assert.NotEqual(attackTile, ends);
        Assert.True(Exposure.Of(guarded, Starter, wren, ends).NoCrit < wren.Hp, $"{ends}: the tile she ends on passes the veto");

        var open = Start(map: Ward.Replace("protect: wren\n", ""));
        var unprotected = open.Find("wren")!;
        Assert.False(HeuristicPlayer.LosesTheMap(open, unprotected));
        var taken = HeuristicPlayer.PlanUnit(open, Starter, unprotected);
        Assert.Equal(attackTile, Assert.IsType<Move>(taken[0]).To);
        Assert.Equal("brigand-1", Assert.IsType<Attack>(taken[1]).TargetId);
    }

    /// <summary>Issue 141: the protected recruit's bench is refused by the core, like the captain's, with the same message shape.</summary>
    [Fact]
    public void TheProtectedRecruitCannotBeBenched()
    {
        var map = MapFixture.Parse(Ward);
        var ex = Assert.Throws<ArgumentException>(() => BattleState.From(map, Starter, Starter.Cast, 7, benched: ValueList<string>.Of("wren")));
        Assert.Equal("the protected recruit 'wren' cannot be benched (Parameter 'benched')", ex.Message);
        Assert.NotNull(BattleState.From(map, Starter, Starter.Cast, 7, benched: ValueList<string>.Of("teodor")).Find("wren"));
    }

    /// <summary>
    /// Issue 141: gate 4 neither benches nor judges the protected recruit. Her mix prints as
    /// data on a line beside the captain's, she is not in the recruit count or the median,
    /// and a bare recruit on the same map is benched and judged as ever.
    /// </summary>
    [Fact]
    public void GateFourPrintsTheProtectedRecruitUnjudgedBesideTheCaptain()
    {
        var map = MapFixture.Parse(Ward.Replace("P recruit:wren 2,1", "P recruit:wren 2,1\n        P recruit 0,0"));
        var (_, baseline) = Gates.Gate1(Starter, map, "ward", 5);
        var result = Gates.Gate4(Starter, map, "ward", baseline);
        var lines = result.Line.Split('\n');
        Assert.Contains("gate 4 no dead weight: ward, 1 recruits x 5 seeds", lines[0]);
        Assert.Contains(lines, l => l.StartsWith("  teodor: drop ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("  captain captain: baseline [atk", StringComparison.Ordinal) && l.EndsWith("never benched, not judged", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("  protected wren: baseline [atk", StringComparison.Ordinal) && l.EndsWith("never benched, not judged", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.StartsWith("  wren: drop ", StringComparison.Ordinal));
    }

    private const string Veto = """
        name: Veto
        size: 7x3
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 12

        .......
        .......
        .......

        units:
        P captain 0,1
        E brigand 4,1 group:yard behavior:hold
        E soldier 3,0 group:yard behavior:hold
        E soldier 3,2 group:yard behavior:hold

        """;

    /// <summary>
    /// Issue 153: the refused-kill column floors at four decimals, so only a kill that cannot
    /// fail prints 1.0000. A doubled raw-99 first-strike kill on the Yard succeeds unless both
    /// strikes miss, one minus a hundred-millionth, which rounding put on the certainty line
    /// and the floor prints as 0.9999; the same board at raw 100 is exactly one and prints
    /// 1.0000. Raw 98 and a four-decimal value print as themselves.
    /// </summary>
    [Fact]
    public void TheRefusedKillColumnFloorsSoOnlyACertainKillPrintsAsOne()
    {
        var (nearly, sharp, brigand) = ExposureTests.CertainKillBoard(hpShort: 0, rawHit: 99);
        var (certain, sharper, target) = ExposureTests.CertainKillBoard(hpShort: 0, rawHit: 100);
        var tile = new Coord(2, 1);
        Assert.Equal(RollScheme.TwoRollAverage, nearly.Scheme);
        Assert.True(Queries.Forecast(nearly, Starter, sharp, brigand, tile)!.Attacker.Doubles, "the captain doubles the brigand, so the second strike is a second chance");

        var double99 = HeuristicPlayer.KillProbability(nearly, Starter, sharp, tile, brigand);
        var double100 = HeuristicPlayer.KillProbability(certain, Starter, sharper, tile, target);

        Assert.True(double99 < 1 && double99 > 0.9999, "under one and inside the last digit: the case that rounded onto the line");
        Assert.Equal(1.0, double100, 12);
        Assert.Equal("0.9999", Gates.FormatKill(double99));
        Assert.Equal("1.0000", Gates.FormatKill(double100));
        Assert.Equal("0.9994", Gates.FormatKill(0.9994));
        Assert.Equal("0.7191", Gates.FormatKill(0.71915));
        Assert.Equal("0.0000", Gates.FormatKill(0));
        Assert.Equal("refused kill p50 0.9999 over 1", Gates.RefusedKill(new[] { new GameResult(BattleResult.Lost, 6, new Dictionary<string, ActionMix>(), LossCause.Timeout, 2, double99) }));
        Assert.Equal("refused kill p50 1.0000 over 1", Gates.RefusedKill(new[] { new GameResult(BattleResult.Lost, 6, new Dictionary<string, ActionMix>(), LossCause.Timeout, 2, double100) }));
    }

    /// <summary>
    /// Issue 125: the refused kill probability is the named attack's own chance to kill over
    /// its strikes at the forecast's numbers under the game's scheme. On the Veto board with
    /// the brigand at 1 HP, the captain's only attack on him is from 3,1, which the worst case
    /// refuses; any landing strike kills, so the chance is the hit probability, or one minus
    /// the chance both strikes miss when he doubles, and it differs between the two schemes.
    /// </summary>
    [Theory]
    [InlineData(RollScheme.TwoRollAverage)]
    [InlineData(RollScheme.OneRoll)]
    public void TheRefusedKillProbabilityIsTheNamedAttacksChanceToKillUnderTheScheme(RollScheme scheme)
    {
        var start = BattleState.From(MapFixture.Parse(Veto), Starter, ValueList<Unit>.Of(Hale), 7, scheme);
        var brigand = start.Find("brigand-1")! with { Hp = 1 };
        var state = start.WithUnit(brigand);
        var hale = state.Find("hale")!;
        var tile = new Coord(3, 1);
        Assert.True(Exposure.Of(state, Starter, hale, tile, brigand).NoCrit >= hale.Hp, "the attack on the brigand is refused, so the test is a real refusal");

        var side = Ironwake.Core.Combat.Forecast((hale with { At = tile }).ToCombatant(state.Map, Starter), brigand.ToCombatant(state.Map, Starter), 1, scheme).Attacker;
        Assert.True(side.HitChance < 100, "a raw 100 would be a certain kill and leave the sum, so the hit is under it");
        var hit = Ironwake.Core.Combat.HitProbability(side.HitChance, scheme);
        var expected = side.Doubles ? 1 - (1 - hit) * (1 - hit) : hit;

        Assert.Equal(expected, HeuristicPlayer.KillProbability(state, Starter, hale, tile, brigand), 12);
        var player = new HeuristicPlayer();
        player.Next(state, Starter);
        Assert.Equal(expected, player.HighestRefusedKill!.Value, 12);
        Assert.Equal(Ironwake.Core.Combat.HitProbability(side.HitChance, scheme), Ironwake.Core.Combat.HitProbability(side.HitChance, scheme));
    }

    /// <summary>
    /// Issue 125: a strike that does not kill on its own can kill with its crit or with the
    /// second strike, and the probability enumerates those paths at the forecast's numbers.
    /// The brigand at full HP on the Veto board is not killed by one plain hit from the captain.
    /// </summary>
    [Fact]
    public void TheKillProbabilityCountsTheCritAndTheSecondStrike()
    {
        var state = BattleState.From(MapFixture.Parse(Veto), Starter, ValueList<Unit>.Of(Hale), 7);
        var hale = state.Find("hale")!;
        var brigand = state.Find("brigand-1")!;
        var tile = new Coord(3, 1);
        var side = Ironwake.Core.Combat.Forecast((hale with { At = tile }).ToCombatant(state.Map, Starter), brigand.ToCombatant(state.Map, Starter), 1, state.Scheme).Attacker;
        Assert.True(side.Damage < brigand.Hp, "one plain hit does not kill, so the crit and the double are the only ways");
        var hit = Ironwake.Core.Combat.HitProbability(side.HitChance, state.Scheme);
        var crit = side.CritChance / 100.0;
        var expected = 0.0;
        var outcomes = new[] { (1 - hit, 0), (hit * (1 - crit), side.Damage), (hit * crit, side.Damage * Ironwake.Core.Combat.CritMultiplier) };
        foreach (var (p1, d1) in outcomes)
        {
            if (!side.Doubles)
            {
                expected += d1 >= brigand.Hp ? p1 : 0;
                continue;
            }

            foreach (var (p2, d2) in outcomes)
            {
                expected += d1 + d2 >= brigand.Hp ? p1 * p2 : 0;
            }
        }

        Assert.Equal(expected, HeuristicPlayer.KillProbability(state, Starter, hale, tile, brigand), 12);
        Assert.InRange(expected, 0.0, 1.0);
    }

    /// <summary>
    /// Issue 147: a kill that needs the second strike is weighted by the chance the attacker
    /// survives the counter thrown between the strikes, at the defender's own hit and crit.
    /// On the Yard the level 1 brigand at 20 HP takes 11 from the captain's iron sword, so
    /// the kill needs both strikes; his iron axe counters for 8, so a captain at 8 HP dies to
    /// any landing counter before the second strike. The number is the closed form of that
    /// board, and it is under the count the named attack alone would give (issue 125).
    /// </summary>
    [Theory]
    [InlineData(RollScheme.TwoRollAverage)]
    [InlineData(RollScheme.OneRoll)]
    public void TheRefusedKillCountsTheCounterBetweenTheStrikesWhenTheKillNeedsTheSecond(RollScheme scheme)
    {
        var start = BattleState.From(BattleFixture.YardMap, Starter, BattleFixture.Roster, 7, scheme);
        var hale = start.Find("hale")! with { Hp = 8 };
        var state = start.WithUnit(hale);
        var brigand = state.Find("brigand-1")!;
        var tile = new Coord(2, 1);
        var forecast = Ironwake.Core.Combat.Forecast((hale with { At = tile }).ToCombatant(state.Map, Starter), brigand.ToCombatant(state.Map, Starter), 1, scheme);
        var side = forecast.Attacker;
        var back = forecast.Defender;
        Assert.True(side.Doubles, "the captain doubles the brigand, so the second strike exists");
        Assert.True(side.Damage < brigand.Hp && 2 * side.Damage >= brigand.Hp, "one plain hit does not kill and two do, so the kill needs the second strike");
        Assert.True(back.Strikes && back.Damage >= hale.Hp && back.Damage * Ironwake.Core.Combat.CritMultiplier >= hale.Hp, "the counter kills the captain on any landing strike");

        var strike = Outcomes(side, scheme);
        var counterKills = Outcomes(back, scheme).Where(o => o.Damage >= hale.Hp).Sum(o => o.P);
        Assert.InRange(counterKills, 0.4, 1.0);
        var expected = 0.0;
        var alone = 0.0;
        foreach (var (p1, d1) in strike)
        {
            if (d1 >= brigand.Hp)
            {
                expected += p1;
                alone += p1;
                continue;
            }

            foreach (var (p2, d2) in strike)
            {
                if (d1 + d2 >= brigand.Hp)
                {
                    expected += p1 * (1 - counterKills) * p2;
                    alone += p1 * p2;
                }
            }
        }

        var actual = HeuristicPlayer.KillProbability(state, Starter, hale, tile, brigand);
        Assert.Equal(expected, actual, 12);
        Assert.True(actual < alone - 0.2, "the counter's share is taken off the second strike's paths");
        Assert.True(Exposure.Of(state, Starter, hale, tile, brigand).NoCrit >= hale.Hp, "the attack is refused, so the number reaches the row");
        var player = new HeuristicPlayer();
        player.Next(state, Starter);
        Assert.Equal(expected, player.HighestRefusedKill!.Value, 12);
    }

    /// <summary>
    /// Issue 147: the counter weighs only the paths through the second strike. A first
    /// strike that kills when it lands is unchanged on that path; only the path where it
    /// misses and the second strike kills carries the counter, so the brigand at 1 HP on the
    /// Veto board, for a captain fast enough to double it, moves from the named attack's own chance by exactly the miss, the
    /// counter's kill, and the second hit, and by nothing when the captain stands at full
    /// HP, where the counter kills nobody. A defender that cannot strike back, an archer at
    /// sword range against a captain fast enough to double it, weighs the second strike at
    /// one, so the number is the plain count over the strikes however low the captain's HP.
    /// </summary>
    [Theory]
    [InlineData(RollScheme.TwoRollAverage)]
    [InlineData(RollScheme.OneRoll)]
    public void OnlyThePathsThroughTheSecondStrikeCarryTheCounter(RollScheme scheme)
    {
        var swift = Hale with { Stats = Hale.Stats with { Spd = Hale.Stats.Spd + 8 } };
        var start = BattleState.From(MapFixture.Parse(Veto), Starter, ValueList<Unit>.Of(swift), 7, scheme);
        var brigand = start.Find("brigand-1")! with { Hp = 1 };
        var tile = new Coord(3, 1);
        var full = start.WithUnit(brigand);
        var hale = full.Find("hale")!;
        var low = full.WithUnit(hale with { Hp = 1 });
        var forecast = Ironwake.Core.Combat.Forecast((hale with { At = tile }).ToCombatant(full.Map, Starter), brigand.ToCombatant(full.Map, Starter), 1, scheme);
        var side = forecast.Attacker;
        var back = forecast.Defender;
        Assert.True(side.Doubles && side.HitChance < 100, "the miss-then-kill path exists and has a chance");
        Assert.True(back.Strikes && back.Damage >= 1 && back.Damage < hale.Hp, "the counter kills a captain at 1 HP and not one at full");
        var hit = Ironwake.Core.Combat.HitProbability(side.HitChance, scheme);
        var counterKills = Outcomes(back, scheme).Where(o => o.Damage >= 1).Sum(o => o.P);
        var alone = 1 - (1 - hit) * (1 - hit);

        Assert.Equal(alone, HeuristicPlayer.KillProbability(full, Starter, hale, tile, brigand), 12);
        Assert.Equal(alone - (1 - hit) * counterKills * hit, HeuristicPlayer.KillProbability(low, Starter, low.Find("hale")!, tile, brigand), 12);

        var unanswered = BattleState.From(MapFixture.Parse(Unanswered), Starter, ValueList<Unit>.Of(swift), 7, scheme);
        var archer = unanswered.Find("archer-1")!;
        var weak = unanswered.WithUnit(unanswered.Find("hale")! with { Hp = 1 });
        var attacker = weak.Find("hale")!;
        var against = Ironwake.Core.Combat.Forecast((attacker with { At = tile }).ToCombatant(weak.Map, Starter), archer.ToCombatant(weak.Map, Starter), 1, scheme);
        Assert.False(against.Defender.Strikes, "a bow does not answer at range 1");
        Assert.True(against.Attacker.Doubles && against.Attacker.Damage < archer.Hp, "the kill needs the second strike, the case the counter would weigh");
        var strike = Outcomes(against.Attacker, scheme);
        var expected = 0.0;
        foreach (var (p1, d1) in strike)
        {
            foreach (var (p2, d2) in strike)
            {
                expected += d1 + d2 >= archer.Hp ? p1 * p2 : 0;
            }
        }

        Assert.Equal(expected, HeuristicPlayer.KillProbability(weak, Starter, attacker, tile, archer), 12);
    }

    private static (double P, int Damage)[] Outcomes(SideForecast side, RollScheme scheme)
    {
        var hit = Ironwake.Core.Combat.HitProbability(side.HitChance, scheme);
        var crit = side.CritChance / 100.0;
        return new[] { (1 - hit, 0), (hit * (1 - crit), side.Damage), (hit * crit, side.Damage * Ironwake.Core.Combat.CritMultiplier) };
    }

    private const string Unanswered = """
        name: Unanswered
        size: 7x3
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        .......
        .......
        .......

        units:
        P captain 0,1
        E archer 4,1 group:yard behavior:hold

        """;

    /// <summary>
    /// Issue 125: gate 1 prints the refused-kill median over its timeout losses with its
    /// count, and a dash when no timeout had a refusal. On the Ward every seed is a timeout
    /// in which both covered units refused the one lethal attack on the brigand, whose full
    /// HP only a crit path kills, so the median is a small probability over every seed; on
    /// the Sealed map nothing was ever refused, so the row prints a dash and the result
    /// carries null, as does a game the random player fought.
    /// </summary>
    [Fact]
    public void GateOnePrintsTheRefusedKillMedianOverTimeoutsAndADashWhenNothingWasRefused()
    {
        var (ward, wardGames) = Gates.Gate1(Starter, MapFixture.Parse(Ward), "ward", 3);
        Assert.Contains("losses 3 timeout 0 captain 0 protected, quiet tail ", ward.Line);
        Assert.Contains(", refused kill p50 0.", ward.Line);
        Assert.Contains(" over 3, two-roll average: FAILED", ward.Line);
        Assert.All(wardGames, g => Assert.InRange(g.RefusedKill!.Value, 0.0, 1.0));
        Assert.All(wardGames, g => Assert.True(g.RefusedKill!.Value > 0, "the crit path is a way to kill, so the refused chance is above zero"));

        var (_, sealedGames) = Gates.Gate1(Starter, MapFixture.Parse(Sealed), "sealed", 3);
        Assert.All(sealedGames, g => Assert.Null(g.RefusedKill));
        Assert.Equal("refused kill -", Gates.RefusedKill(sealedGames));
        Assert.Null(Runner.Play(Starter, MapFixture.Parse(Sealed), 1, new RandomLegalPlayer(1)).RefusedKill);
    }

    /// <summary>
    /// Issue 125: gate 4's rows print the benched arm's losses by cause. On the Ambush with a
    /// recruit beside the captain, benching her leaves him alone among four brigands and every
    /// arm game is a captain death, counted under captain in her row.
    /// </summary>
    [Fact]
    public void GateFourPrintsTheBenchedArmsLossesByCause()
    {
        var map = MapFixture.Parse(Ambush.Replace("P captain 1,1", "P captain 1,1\n        P recruit 1,0"));
        var (_, baseline) = Gates.Gate1(Starter, map, "ambush", 5);
        var result = Gates.Gate4(Starter, map, "ambush", baseline);
        var wren = result.Line.Split('\n').Single(l => l.StartsWith("  wren:", StringComparison.Ordinal));
        Assert.Contains("benched losses 0 timeout 5 captain 0 protected, refused kill -", wren);
    }

    /// <summary>
    /// A Seize map where the recruit stands nearer the throne than the captain and nothing
    /// is in reach to fight on turn 1: the approach walks both toward the throne, and the
    /// nearest tile to the throne is the throne.
    /// </summary>
    private const string Gatehouse = """
        name: Gatehouse
        size: 9x3
        win: seize
        turn_limit: 6
        recall: 3
        enemy_level: 1

        .........
        ....T....
        .........

        units:
        P captain 0,1
        P recruit 2,1
        E soldier 8,1 group:door behavior:hold

        """;

    /// <summary>Issue 111: only the captain can seize, so a recruit never ends a move on the throne, and the captain does.</summary>
    [Fact]
    public void ARecruitStopsShortOfTheThroneAndTheCaptainTakesIt()
    {
        var state = Start(map: Gatehouse);
        var throne = new Coord(4, 1);

        var wren = state.Find("wren")!;
        Assert.True(state.ReachOf(wren, Starter).CanEnd(throne), "the throne is in the recruit's reach, so the test is a real refusal");
        var plan = HeuristicPlayer.PlanUnit(state, Starter, wren);
        var move = Assert.IsType<Move>(plan[0]);
        Assert.NotEqual(throne, move.To);
        Assert.Equal(1, move.To.DistanceTo(throne));

        var applied = Resolver.Apply(state, Starter, move).Next;
        applied = Resolver.Apply(applied, Starter, new Wait("wren")).Next;
        applied = Resolver.Apply(applied, Starter, new Move("hale", new Coord(3, 0))).Next;
        applied = Resolver.Apply(applied, Starter, new Wait("hale")).Next;
        applied = Resolver.Apply(applied, Starter, new EndPhase()).Next;
        applied = EnemyAi.Plan(applied, Starter).Aggregate(applied, (s, c) => Resolver.Apply(s, Starter, c).Next);
        var hale = applied.Find("hale")!;
        var captainPlan = HeuristicPlayer.PlanUnit(applied, Starter, hale);
        Assert.Equal(throne, Assert.IsType<Move>(captainPlan[0]).To);

        var game = Runner.Play(Starter, MapFixture.Parse(Gatehouse, "gatehouse.map"), 1, new HeuristicPlayer());
        Assert.True(game.Won, "the heuristic seizes within the limit: " + game.Result + " at turn " + game.Turns);
        Assert.True(game.Turns <= 3, "seized at turn " + game.Turns);
    }

    /// <summary>Issue 111: a recruit that already stands on the throne steps off it.</summary>
    [Fact]
    public void ARecruitStandingOnTheThroneStepsOff()
    {
        var map = Gatehouse.Replace("P recruit 2,1", "P recruit 4,1");
        var state = Start(map: map);
        var wren = state.Find("wren")!;
        Assert.True(state.Map.IsThrone(wren.At));

        var plan = HeuristicPlayer.PlanUnit(state, Starter, wren);
        var move = Assert.IsType<Move>(plan[0]);
        Assert.False(state.Map.IsThrone(move.To));
    }

    /// <summary>Issue 114: a Rout map whose only enemy holds behind a wall nobody can cross, so every seed runs out the clock without a combat.</summary>
    private const string Sealed = """
        name: Sealed
        size: 6x3
        win: rout
        turn_limit: 3
        recall: 3
        enemy_level: 1

        ...#..
        ...#..
        ...#..

        units:
        P captain 0,1
        E brigand 5,1 group:far behavior:hold

        """;

    /// <summary>Issue 114: the captain alone in the middle of four aggressive level-20 brigands, so every seed is a captain death on turn 1.</summary>
    private const string Ambush = """
        name: Ambush
        size: 3x3
        win: rout
        turn_limit: 5
        recall: 3
        enemy_level: 20

        ...
        ...
        ...

        units:
        P captain 1,1
        E brigand 0,0 group:ring behavior:aggressive
        E brigand 2,0 group:ring behavior:aggressive
        E brigand 0,2 group:ring behavior:aggressive
        E brigand 2,2 group:ring behavior:aggressive

        """;

    /// <summary>Issue 114: the protected recruit among the same four brigands and the captain walled off from her, so every seed is a protected death.</summary>
    private const string Hostage = """
        name: Hostage
        size: 7x3
        win: rout
        turn_limit: 5
        recall: 3
        enemy_level: 20
        protect: wren

        ...#...
        ...#...
        ...#...

        units:
        P captain 0,1
        P recruit:wren 5,1
        E brigand 4,0 group:ring behavior:aggressive
        E brigand 6,0 group:ring behavior:aggressive
        E brigand 4,2 group:ring behavior:aggressive
        E brigand 6,2 group:ring behavior:aggressive

        """;

    /// <summary>
    /// Issue 114: gate 1's row counts the losses by cause in section 7's order and prints the
    /// mean quiet tail of the timeouts, the turns from the last combat to the limit. With no
    /// combat at all the tail is the whole limit; with no timeouts it is a dash.
    /// </summary>
    [Fact]
    public void GateOnePrintsTheLossesByCauseAndTheQuietTail()
    {
        var (sealedRow, sealedGames) = Gates.Gate1(Starter, MapFixture.Parse(Sealed), "sealed", 10);
        Assert.Contains("heuristic wins 0/10 (0 %), no wins, losses 10 timeout 0 captain 0 protected, quiet tail 3.0, refused kill -, two-roll average: FAILED", sealedRow.Line);
        Assert.All(sealedGames, g => Assert.Equal(LossCause.Timeout, g.Cause));
        Assert.All(sealedGames, g => Assert.Equal(0, g.LastCombatTurn));

        var (ambushRow, ambushGames) = Gates.Gate1(Starter, MapFixture.Parse(Ambush), "ambush", 10);
        Assert.Contains("losses 0 timeout 10 captain 0 protected, quiet tail -, refused kill -, two-roll average: FAILED", ambushRow.Line);
        Assert.All(ambushGames, g => Assert.Equal(LossCause.Captain, g.Cause));

        var (hostageRow, hostageGames) = Gates.Gate1(Starter, MapFixture.Parse(Hostage), "hostage", 10);
        Assert.Contains("losses 0 timeout 0 captain 10 protected, quiet tail -, refused kill -, two-roll average: FAILED", hostageRow.Line);
        Assert.All(hostageGames, g => Assert.Equal(LossCause.Protected, g.Cause));

        var yard = Runner.Play(Starter, YardMap, 7, new HeuristicPlayer());
        Assert.Equal(LossCause.None, yard.Cause);
        Assert.True(yard.LastCombatTurn > 0, "the yard fights");
        Assert.True(yard.LastCombatTurn <= yard.Turns);
    }

    /// <summary>Issue 114: the tail counts from the last combat, not from the start, so a timeout after a fight reads shorter than the limit.</summary>
    [Fact]
    public void TheQuietTailCountsFromTheLastCombat()
    {
        var games = new[]
        {
            new GameResult(BattleResult.Lost, 6, new Dictionary<string, ActionMix>(), LossCause.Timeout, 2),
            new GameResult(BattleResult.Lost, 6, new Dictionary<string, ActionMix>(), LossCause.Timeout, 5),
            new GameResult(BattleResult.Lost, 3, new Dictionary<string, ActionMix>(), LossCause.Captain, 3),
            new GameResult(BattleResult.Won, 4, new Dictionary<string, ActionMix>(), LossCause.None, 4),
        };
        var map = MapFixture.Parse(Sealed) with { TurnLimit = 5 };
        Assert.Equal("losses 2 timeout 1 captain 0 protected, quiet tail 1.5", Gates.Losses(games, map));
        Assert.Equal("losses 0 timeout 1 captain 0 protected, quiet tail -", Gates.Losses(games.Skip(2).ToList(), map));
    }

    [Fact]
    public void GateOneAndTwoRunOnTheYard()
    {
        var (gate1, games) = Gates.Gate1(Starter, YardMap, "yard", 10);
        Assert.Equal(10, games.Count);
        Assert.Contains("gate 1 beatable: yard, heuristic wins", gate1.Line);
        var gate2 = Gates.Gate2(Starter, YardMap, "yard", 10);
        Assert.Contains("gate 2 decisions matter: yard, random wins", gate2.Line);
    }

    /// <summary>
    /// Issue 107: the Sim fights under either roll scheme. The default is the two-roll
    /// average, so an unspecified game equals a two-roll one on every seed; the keyed rng
    /// hands both schemes the same numbers and <c>Combat.Lands</c> reads them differently,
    /// so over ten seeds at least one game comes out different under one roll.
    /// </summary>
    [Fact]
    public void TheSimPlaysUnderEitherRollSchemeAndDefaultsToTwoRolls()
    {
        var differed = false;
        for (var seed = 1UL; seed <= 10; seed++)
        {
            var unspecified = Runner.Play(Starter, YardMap, seed, new HeuristicPlayer());
            var two = Runner.Play(Starter, YardMap, seed, new HeuristicPlayer(), scheme: RollScheme.TwoRollAverage);
            var one = Runner.Play(Starter, YardMap, seed, new HeuristicPlayer(), scheme: RollScheme.OneRoll);
            Assert.Equal(Line(two), Line(unspecified));
            differed |= Line(one) != Line(two);
        }

        Assert.True(differed, "ten seeds under one roll never differed from two rolls");
        Assert.Contains(", one roll:", Gates.Gate1(Starter, YardMap, "yard", 3, RollScheme.OneRoll).Gate.Line);
        Assert.Contains(", two-roll average:", Gates.Gate1(Starter, YardMap, "yard", 3).Gate.Line);

        static string Line(GameResult game) => $"{game.Result} {game.Turns} {string.Join(" ", game.Mix.OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => m.Key + "=" + m.Value))}";
    }

    /// <summary>
    /// Issue 66: gate 5 runs with abilities on both sides. Its fighters draw both effect
    /// kinds, and a stream of them still reads exactly the damage the forecast printed.
    /// </summary>
    [Fact]
    public void GateFiveFightersCarryAbilitiesOfBothKinds()
    {
        var random = new Random(5);
        var drawn = Enumerable.Range(0, 50).SelectMany(_ => Gates.DrawnAbilities(Starter, random)).ToList();

        Assert.Contains(drawn, a => a.Trigger == AbilityTrigger.Passive);
        Assert.Contains(drawn, a => a.Trigger == AbilityTrigger.OnCombat);

        var tally = new Gates.ForecastTally();
        Gates.ForecastStream(Starter, tally, 500);
        Assert.Equal(500, tally.Combats);
        Assert.Equal(0, tally.WrongDamage);
    }

    /// <summary>Issue 70: gate 5's stream carries drawn gauntlets on both sides, and every combat's strikes match the forecast's count.</summary>
    [Fact]
    public void GateFiveFightsGauntletsAndCountsEveryStrike()
    {
        var tally = new Gates.ForecastTally();
        Gates.ForecastStream(Starter, tally, 2000);

        Assert.Equal(0, tally.WrongStrikeCounts);
        Assert.InRange(tally.GauntletCombats, 600, 1200);
        Assert.True(tally.Result(2000).Passed, tally.Result(2000).Line);
        var gauntlet = Gates.DrawnGauntlet(new Random(3));
        Assert.Equal(WeaponType.Gauntlet, gauntlet.Type);
        Assert.Equal((1, 1), (gauntlet.MinRange, gauntlet.MaxRange));
    }

    /// <summary>The strike-count check fires: a forecast that shows one strike for a two-strike round fails gate 5.</summary>
    [Fact]
    public void GateFiveFailsAForecastThatHidesAStrike()
    {
        var side = new SideForecast(true, 3, 50, 50, 0, false, StrikesPerRound: 2);
        var forecast = new CombatForecast(side, SideForecast.None, RollScheme.OneRoll);
        var strikes = ValueList<StrikeEvent>.Of(
            new StrikeEvent(0, "a", "b", false, false, 0, 20),
            new StrikeEvent(1, "a", "b", false, false, 0, 20));
        var tally = new Gates.ForecastTally();

        tally.Count(forecast, new CombatFought("a", "b", 1, Side.Player, strikes, 20, 20));
        Assert.Equal(0, tally.WrongStrikeCounts);
        tally.Count(forecast with { Attacker = side with { StrikesPerRound = 1 } }, new CombatFought("a", "b", 1, Side.Player, strikes, 20, 20));
        Assert.Equal(1, tally.WrongStrikeCounts);
        Assert.False(tally.Result(1).Passed);
        Assert.Contains("1 combats with a strike count the forecast did not show", tally.Result(1).Line);
    }
}
