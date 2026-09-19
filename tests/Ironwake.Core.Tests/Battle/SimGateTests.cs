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

    private const string WalledOff = """
        name: Walled off
        size: 8x5
        win: survive
        turn_limit: 6
        recall: 3
        enemy_level: 1

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
        var (gate1, baseline) = Gates.Gate1(Starter, map, "walled", 30);
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
    /// only with that recruit benched. The median drop is at or below zero and gate 4 fails
    /// the cast, with no recruit labelled.
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
        var (gate1, baseline) = Gates.Gate1(Starter, map, "escape", 10);
        var result = Gates.Gate4(Starter, map, "escape", baseline);
        Assert.False(result.Passed, gate1.Line + "\n" + result.Line);
        Assert.Contains("cast not earning its deployment: benching the median recruit raises the win rate", result.Line);
        Assert.Contains("teodor: drop -1.000", result.Line);
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
}
