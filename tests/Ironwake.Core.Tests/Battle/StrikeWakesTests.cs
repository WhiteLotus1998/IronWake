using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 1290: a priced enemy strike in <c>threat</c> whose noise reaches a sleeping group names that
/// group under its line, in the forecast's words (issue 1106), and the tiles that are heard. Unpriced:
/// the total stays one wave deep (DECISIONS/0281). The protocol's threat line carries <c>wakes</c>.
/// </summary>
public class StrikeWakesTests
{
    private static string Field(int yardX, string header = "") =>
        $"""
        name: Field
        size: 16x7
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {header}

        ................
        ................
        ................
        ................
        ................
        ................
        ................

        units:
        P captain 2,3
        E brigand 1,3 group:band behavior:hold
        E soldier {yardX},3 group:yard behavior:guard

        """.Replace("\n\n\n", "\n\n");

    private static BattleState Start(int yardX, string header = "") => BattleFixture.Start(map: Field(yardX, header));

    private static BattleUnit Captain(BattleState state) => state.Find("hale")!;

    private static ThreatLine Line(BattleState state) => Assert.Single(Queries.Threats(state, Starter, Captain(state), Captain(state).At)!);

    private static string Text(BattleState state)
    {
        var captain = Captain(state);
        var tile = captain.At;
        return Ironwake.Cli.PlaySession.ThreatText(state, Starter, captain, tile, Queries.Threats(state, Starter, captain, tile)!, Queries.SleepingThreats(state, Starter, captain, tile)!);
    }

    [Fact]
    public void APricedEnemyStrikeWhoseNoiseReachesASleepingGroupNamesItInThreat()
    {
        // The strike is fought on 1,3 and 2,3; the yard on 8,3 is 6 from 2,3 and 7 from 1,3.
        var state = Start(8);

        var line = Line(state);

        Assert.Equal(new Coord(1, 3), line.From);
        Assert.Equal(new[] { ("yard", (string?)null, "2,3") }, line.Wakes.Select(w => (w.Group, w.CalledBy, string.Join(" ", w.HeardFrom))));
        Assert.Contains("\n    Its strike here wakes: the yard group (noise, heard from 2,3); not in the total\n", Text(state));
    }

    [Fact]
    public void AStrikeOutOfEarshotOfTheGroupNamesNoWake()
    {
        var state = Start(9);

        Assert.Empty(Line(state).Wakes);
        Assert.DoesNotContain("strike here wakes", Text(state));
    }

    [Fact]
    public void TheWokenGroupIsNotInTheTotal()
    {
        Assert.Equal(Queries.IfAllLand(Queries.Threats(Start(9), Starter, Captain(Start(9)), new Coord(2, 3))!), Queries.IfAllLand(Queries.Threats(Start(8), Starter, Captain(Start(8)), new Coord(2, 3))!));
    }

    [Fact]
    public void AWindupRaiseFightsNothingSoItWakesNothing()
    {
        BattleState Mauler(bool windup) => BattleFixture.Start(map: Field(8, windup ? "windup: on" : "").Replace("E brigand 1,3", "E toll_mauler 1,3"));

        Assert.True(Line(Mauler(true)).Raises);
        Assert.Empty(Line(Mauler(true)).Wakes);
        Assert.Equal(new[] { "yard" }, Line(Mauler(false)).Wakes.Select(w => w.Group));
    }

    [Fact]
    public void AGroupTheStrikeWakesCallsItsLinkedGroup()
    {
        var state = BattleFixture.Start(map: Field(8, "wake_links: yard>far").Replace("E soldier 8,3 group:yard behavior:guard", "E soldier 8,3 group:yard behavior:guard\nE soldier 15,6 group:far behavior:guard"));

        Assert.Contains("\n    Its strike here wakes: the yard group (noise, heard from 2,3), the far group (called by the yard group); not in the total\n", Text(state));
    }

    [Fact]
    public void TheEnemyPhaseWakesWhatThreatNamed()
    {
        Assert.Equal(new[] { "yard" }, Line(Start(8)).Wakes.Select(w => w.Group));
        Assert.Contains(EnemyPhaseWakes(Start(8)), w => w.Group == "yard" && w.Cause == WakeCause.Noise);
        Assert.Empty(Line(Start(9)).Wakes);
        Assert.DoesNotContain(EnemyPhaseWakes(Start(9)), w => w.Group == "yard");
    }

    /// <summary>The groups woken while the enemy phase after <paramref name="state"/>'s player phase is played out by its planner.</summary>
    private static IReadOnlyList<GroupWoke> EnemyPhaseWakes(BattleState state)
    {
        var step = Resolver.Apply(state, Starter, new EndPhase());
        var events = step.Events.ToList();
        var board = step.Next;
        foreach (var command in EnemyAi.Plan(board, Starter))
        {
            step = Resolver.Apply(board, Starter, command);
            events.AddRange(step.Events);
            board = step.Next;
        }

        return events.OfType<GroupWoke>().ToList();
    }

    [Fact]
    public void TheProtocolsThreatLineCarriesTheWakes()
    {
        var loud = new Ironwake.Cli.ProtocolSession(Starter, Start(8), TextWriter.Null).Answer("{\"query\":\"threat\",\"unit\":\"hale\"}");
        var quiet = new Ironwake.Cli.ProtocolSession(Starter, Start(9), TextWriter.Null).Answer("{\"query\":\"threat\",\"unit\":\"hale\"}");

        Assert.Contains("\"wakes\":[{\"group\":\"yard\",\"cause\":\"noise\",\"heardFrom\":[{\"x\":2,\"y\":3}]}]", loud);
        Assert.Contains("\"wakes\":[]", quiet);
    }
}
