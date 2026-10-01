using Ironwake.Content;
using Ironwake.Cli;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 558 (rounds 158 and 159): before a player phase ends, <c>end</c> names every player
/// unit whose <c>threat</c> total reaches its HP, then ends the phase anyway. The names come
/// from <see cref="Queries.Lethal"/>, which reads the same lines and the same seated sum as
/// <c>threat</c>, so the two cannot disagree.
/// </summary>
public sealed class EndLethalTests
{
    /// <summary>Hale at 0,1 and Wren at 0,3; <paramref name="enemies"/> fill the units list; <paramref name="header"/> an extra header line.</summary>
    private static string Field(string enemies, string header = "") =>
        $"""
        name: Field
        size: 8x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {header}

        ........
        ........
        ........
        ........

        units:
        P captain 0,1
        P recruit:wren 0,3
        {enemies}

        """.Replace("\n\n\n", "\n\n");

    private static BattleState OneSoldier() => Start(map: Field("E soldier 4,1 group:y behavior:aggressive"));

    private static int ThreatTotal(BattleState state, string unitId)
    {
        var unit = state.Find(unitId)!;
        return Queries.IfAllLand(Queries.Threats(state, Starter, unit, unit.At)!, Queries.RaisedBlowOn(state, Starter, unit, unit.At));
    }

    private static BattleState WithHp(BattleState state, string unitId, int hp) => state.WithUnit(state.Find(unitId)! with { Hp = hp });

    [Fact]
    public void AUnitWhoseThreatTotalEqualsItsHpIsLethal()
    {
        var state = OneSoldier();
        var total = ThreatTotal(state, "hale");
        Assert.True(total > 0);

        var lethal = Assert.Single(Queries.Lethal(WithHp(state, "hale", total), Starter));

        Assert.Equal("hale", lethal.Unit.Id);
        Assert.Equal(total, lethal.Total);
        Assert.Equal(new[] { "soldier-1" }, lethal.Strikers.Select(s => s.Enemy.Id));
        Assert.Equal($"Lethal if all land: hale (Soldier for {total}, against {total} hp)", PlaySession.LethalLine(lethal, UnitNames.Of(state, Starter)));
    }

    [Fact]
    public void AUnitOneHpAboveItsThreatTotalIsNotLethal()
    {
        var state = OneSoldier();
        var total = ThreatTotal(state, "hale");

        Assert.DoesNotContain(Queries.Lethal(WithHp(state, "hale", total + 1), Starter), l => l.Unit.Id == "hale");
    }

    /// <summary>Issue 253's board: from 5,3 in the Bulwark trial's corridor both brigands can strike only from 4,3, so one of them is counted and named.</summary>
    [Fact]
    public void TwoEnemiesWithOneWayInAreCountedOnce()
    {
        var map = MapFiles.Load(Path.Combine(Ironwake.Core.Tests.Content.Fixture.RealContentDirectory(), "trials", "bulwark_trial.map"), Starter);
        var state = BattleState.From(map, Starter, Starter.Cast, 12);
        state = state.WithUnit(state.Find("captain")! with { At = new Coord(5, 3), Hp = 1 });
        var lines = Queries.Threats(state, Starter, state.Find("captain")!, new Coord(5, 3))!;
        Assert.Equal(2, lines.Count);

        var lethal = Queries.Lethal(state, Starter).Single(l => l.Unit.Id == "captain");

        Assert.Single(lethal.Strikers);
        Assert.Equal(Queries.IfAllLand(lines), lethal.Total);
        Assert.Equal(lines.Max(l => l.IfAllLand), lethal.Strikers[0].Damage);
    }

    /// <summary>
    /// At dusk (sight 1) soldier-1 beside Hale is seen and soldier-2 three tiles off is not; the
    /// player's reading leaves the unseen one out, as <c>threat</c> does, and the omniscient one keeps it.
    /// </summary>
    [Fact]
    public void AStrikerThePlayerCannotSeeAtDuskIsLeftOut()
    {
        var state = Start(map: Field("E soldier 1,1 group:y behavior:aggressive\nE soldier 3,0 group:y behavior:aggressive", "dusk: 1"));
        state = WithHp(state, "hale", 1);
        Assert.False(Dusk.Seen(state, state.Find("soldier-2")!));

        var seen = Queries.Lethal(state, Starter).Single(l => l.Unit.Id == "hale");
        var all = Queries.Lethal(state, Starter, playerView: false).Single(l => l.Unit.Id == "hale");

        Assert.DoesNotContain(seen.Strikers, s => s.Enemy.Id == "soldier-2");
        Assert.Contains(all.Strikers, s => s.Enemy.Id == "soldier-2");
    }

    [Fact]
    public void NothingIsLethalOnTheEnemyPhase()
    {
        var state = WithHp(OneSoldier(), "hale", 1);
        Assert.NotEmpty(Queries.Lethal(state, Starter));

        Assert.Empty(Queries.Lethal(state.Do(new EndPhase()), Starter));
    }

    [Fact]
    public void NothingIsLethalWhenTheMapIsOver()
    {
        var state = WithHp(OneSoldier(), "hale", 1).WithoutUnit("soldier-1");
        Assert.True(state.Outcome.IsOver);

        Assert.Empty(Queries.Lethal(state, Starter));
    }

    [Fact]
    public void LethalUnitsAreNamedInDeploymentOrder()
    {
        var state = Start(map: Field("E soldier 2,1 group:y behavior:aggressive\nE soldier 2,3 group:y behavior:aggressive"));
        state = WithHp(WithHp(state, "hale", 1), "wren", 1);

        Assert.Equal(new[] { "hale", "wren" }, Queries.Lethal(state, Starter).Select(l => l.Unit.Id));
    }

    [Fact]
    public void TheProtocolsEndAnswerCarriesLethal()
    {
        var state = WithHp(OneSoldier(), "hale", 1);
        var safe = OneSoldier();

        var answer = new ProtocolSession(Starter, state, new StringWriter()).Answer("""{"type":"end"}""");
        var none = new ProtocolSession(Starter, safe, new StringWriter()).Answer("""{"type":"end"}""");

        Assert.Contains("\"lethal\":[{\"unit\":\"hale\",\"total\":", answer);
        Assert.Contains("\"strikers\":[{\"enemy\":\"soldier-1\",\"damage\":", answer);
        Assert.Contains("\"lethal\":[]", none);
    }

    /// <summary>
    /// The console prints the line after the <c>end</c> it answers and before the phase ends, and
    /// the phase ends anyway: Chat's cold Saltmarsh play, seed 541, turn 7, replayed byte for byte
    /// by <c>ChatsColdPlayReplaysOnTheBracedSaltmarshToItsTranscript</c>.
    /// </summary>
    [Fact]
    public void TheConsolePrintsTheLineBeforeThePhaseEnds()
    {
        var repo = Directory.GetParent(Ironwake.Core.Tests.Content.Fixture.RealContentDirectory())!.FullName;
        var transcript = File.ReadAllText(Path.Combine(repo, "docs", "transcripts", "2026-09-29-saltmarsh_ford-541.txt")).ReplaceLineEndings("\n");

        Assert.Contains("> end\nLethal if all land: Wren (Brigand for 11, against 11 hp)\n-- Player phase ends, turn 7 --\n-- Enemy phase, turn 7 --\n", transcript);
    }
}
