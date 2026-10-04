using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 957 (rounds 327, 328): the wind's coming turn is printed before it lands. During the
/// turn before a wind turn, <see cref="WakeCheck.Run"/> on the board as it stands under the next
/// turn's wind names who would wake whom, on the board, at <c>end</c> beside the lethal, and in
/// <c>threat from</c>. The turn lands as the player phase begins, and <c>wind:</c> with
/// <c>dusk:</c> is refused.
/// </summary>
[Collection("console")]
public class WindWarningTests
{
    private static string Field(string header) =>
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
        P captain 0,0
        P recruit:wren 0,6
        E soldier 9,3 group:watch behavior:guard
        E brigand 15,0 group:far behavior:guard

        """.Replace("\n\n\n", "\n\n");

    /// <summary>The field on <paramref name="turn"/> under <c>wind: west; turn 3 east</c>, Hale on <paramref name="hale"/>: 3,3 is quiet upwind of the watch and wakes it downwind.</summary>
    private static BattleState OnTurn(int turn, Coord hale)
    {
        var state = Start(map: Field("wind: west; turn 3 east")) with { Turn = turn };
        return state.WithUnit(state.Find("hale")! with { At = hale });
    }

    private static readonly Coord Quiet = new(3, 3);

    [Fact]
    public void TheTurnBeforeTheWindTurnsNamesWhoTheTurnWakes()
    {
        var state = OnTurn(2, Quiet);

        var coming = Wind.Coming(state, Starter)!;

        Assert.Equal(3, coming.Turn);
        Assert.Equal(WindDirection.East, coming.To);
        var wake = Assert.Single(coming.Wakes);
        Assert.Equal("watch", wake.Group);
        Assert.Equal(new[] { "hale" }, wake.Wakers.Select(u => u.Id));
        Assert.Equal("the wind turns east at turn 3: hale at 3,3 wakes the watch group", Wind.ComingLine(state, Starter, UnitNames.Of(state, Starter), quiet: false));
    }

    [Fact]
    public void OnlyTheTurnBeforeTheWindTurnsWarns()
    {
        Assert.Null(Wind.Coming(OnTurn(1, Quiet), Starter));
        Assert.Null(Wind.Coming(OnTurn(3, Quiet), Starter));
        Assert.Null(Wind.Coming(Start(map: Field("")), Starter));
    }

    [Fact]
    public void AnEndedBattleHasNoComingTurn()
    {
        var state = OnTurn(2, Quiet);
        var won = state with { Units = ValueList<BattleUnit>.From(state.UnitsOf(Side.Player)) };

        Assert.True(won.Outcome.IsOver);
        Assert.Null(Wind.Coming(won, Starter));
        Assert.Null(Wind.StopWakesOnTurn(won, Starter, won.Find("hale")!, new Coord(3, 2)));
    }

    [Fact]
    public void ATurnThatWakesNobodySaysSoOnTheBoardAndNotAtEnd()
    {
        var state = OnTurn(2, new Coord(0, 0));
        var names = UnitNames.Of(state, Starter);

        Assert.Empty(Wind.Coming(state, Starter)!.Wakes);
        Assert.Null(Wind.ComingLine(state, Starter, names, quiet: false));
        Assert.Equal("the wind turns east at turn 3: it wakes nobody where your units stand", Wind.ComingLine(state, Starter, names, quiet: true));
    }

    [Fact]
    public void TheBoardPrintsTheWarningUnderTheWindLine()
    {
        var board = MapRenderer.Render(OnTurn(2, Quiet), Starter);

        Assert.Contains("turns east on turn 3\nthe wind turns east at turn 3: hale at 3,3 wakes the watch group\n", board);
        Assert.DoesNotContain("the wind turns", MapRenderer.Render(OnTurn(1, Quiet), Starter));
    }

    [Fact]
    public void ACalledGroupRidesTheWarning()
    {
        var state = Start(map: Field("wind: west; turn 3 east\nwake_links: watch>far")) with { Turn = 2 };
        state = state.WithUnit(state.Find("hale")! with { At = Quiet });

        Assert.Equal("the wind turns east at turn 3: hale at 3,3 wakes the watch group; the watch group calls the far group", Wind.ComingLine(state, Starter, UnitNames.Of(state, Starter), quiet: false));
    }

    [Fact]
    public void ThreatFromNamesAStopQuietNowThatTheTurnMakesLoud()
    {
        var state = OnTurn(2, new Coord(5, 1));
        var hale = state.Find("hale")!;

        var turn = Wind.StopWakesOnTurn(state, Starter, hale, Quiet)!.Value;

        Assert.Equal(new WindShift(3, WindDirection.East), turn.Shift);
        Assert.Equal(new[] { "watch" }, turn.Groups);
        var text = PlaySession.ThreatText(state, Starter, hale, Quiet, Queries.Threats(state, Starter, hale, Quiet)!, Queries.SleepingThreats(state, Starter, hale, Quiet)!, wakes: Queries.StopWakes(state, Starter, hale, Quiet));
        Assert.Contains("  Quiet now; once the wind turns east on turn 3, stopping here wakes: the watch group", text);
    }

    [Fact]
    public void ThreatFromLeavesOutAStopThatWakesTheGroupNow()
    {
        var state = OnTurn(2, new Coord(5, 1));
        var hale = state.Find("hale")!;

        Assert.Contains(Queries.StopWakes(state, Starter, hale, new Coord(8, 1))!, w => w.Group == "watch");
        Assert.Empty(Wind.StopWakesOnTurn(state, Starter, hale, new Coord(8, 1))!.Value.Groups);
        Assert.Null(Wind.StopWakesOnTurn(OnTurn(1, new Coord(5, 1)), Starter, hale, Quiet));
    }

    [Fact]
    public void AGroupTheWindTurnWakesIsAwakeAtThePlayerPhaseStartAndHasNotActed()
    {
        var state = OnTurn(2, Quiet);
        var enemyPhase = state.Do(new EndPhase());
        Assert.False(enemyPhase.IsAwake("watch"));

        var result = Resolver.Apply(enemyPhase, Starter, new EndPhase());

        Assert.True(result.Accepted);
        Assert.Equal(Side.Player, result.Next.Phase);
        Assert.Equal(3, result.Next.Turn);
        Assert.True(result.Next.IsAwake("watch"));
        Assert.Equal(new Coord(9, 3), result.Next.Find("soldier-1")!.At);
        var began = result.Events.ToList().FindIndex(e => e is PhaseBegan { Side: Side.Player, Turn: 3 });
        var woke = result.Events.ToList().FindIndex(e => e is GroupWoke { Group: "watch", Cause: WakeCause.Proximity });
        Assert.True(began >= 0 && woke > began, "the watch wakes after the player phase of turn 3 begins");
    }

    [Fact]
    public void WindWithDuskIsRefusedNamingTheFileAndTheField()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Field("dusk: 4\nwind: east"), "windy_dusk.map"));

        Assert.Contains("windy_dusk.map", error.Message);
        Assert.Contains("wind: cannot ride with dusk:", error.Message);
    }

    /// <summary>Code's 1280 play of the wind sample to the end of turn 3, then on turn 4 nobody steps clear: <c>threat</c> and <c>end</c> say who the turn-5 east wind wakes, and the field group wakes on turn 5's player phase.</summary>
    [Fact]
    public void TheSampleWarnsAtEndOnTheTurnBeforeTheWindTurns()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var played = File.ReadAllLines(Path.Combine(repo, "docs", "transcripts", "2026-10-04-sallow_grange_wind-1280.script")).Take(35);
        var path = Path.Combine(Path.GetTempPath(), "ironwake-wind-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllLines(path, played.Concat(new[] { "threat wren from 2,4", "end" }));
        try
        {
            var output = ConsoleCapture.Run(() => Ironwake.Cli.Program.Main(new[] { "play", Path.Combine(repo, "docs", "samples", "sallow_grange_wind.map"), "--seed", "1280", "--script", path, "--content", Fixture.RealContentDirectory() }));

            Assert.Contains("  Quiet now; once the wind turns east on turn 5, stopping here wakes: the field group", output);
            var end = output.LastIndexOf("> end", StringComparison.Ordinal);
            var warning = output.IndexOf("The wind turns east at turn 5: Ansgar at 8,3, Teodor at 6,3 and Wren at 2,4 wake the field group", end, StringComparison.Ordinal);
            Assert.True(warning > end, output[end..]);
            Assert.Contains("field group wakes", output[warning..]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
