using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The <c>seize_hold: 1</c> header (issue 1274): on a Seize map that carries it, the step onto the
/// seize tile wins nothing; the captain must still stand there when the next player phase begins,
/// so the tile is held through one enemy phase, and a hold through the last turn's enemy phase still wins.
/// </summary>
public class SeizeHoldTests
{
    private static readonly Coord Throne = new(12, 1);

    private static string Hall(string win = "seize", string? header = "seize_hold: 1", int turnLimit = 10, string captainAt = "9,1") => $"""
        name: Hall
        size: 16x3
        win: {win}{(header is null ? "" : "\n" + header)}
        turn_limit: {turnLimit}
        recall: 3
        enemy_level: 1

        ................
        ............T...
        ................

        units:
        P captain {captainAt}
        E brigand 0,0 group:far behavior:hold

        """;

    private static BattleState Stepped(string map) => Start(map: map).Do(new Move("hale", Throne));

    [Fact]
    public void OnAHeldSeizeMapTheStepOntoTheSeizeTileDoesNotWin()
    {
        var state = Stepped(Hall());

        Assert.True(state.Map.IsThrone(state.Find("hale")!.At));
        Assert.False(state.Outcome.IsOver);
    }

    [Fact]
    public void OnAPlainSeizeMapTheStepOntoTheSeizeTileWins()
    {
        Assert.Equal(BattleResult.Won, Stepped(Hall(header: null)).Outcome.Result);
    }

    [Fact]
    public void TheCaptainStillOnAHeldSeizeTileWhenThePlayerPhaseBeginsWins()
    {
        var ended = Stepped(Hall()).Do(new EndPhase());
        Assert.False(ended.Outcome.IsOver);

        var held = ended.Do(new EndPhase());

        Assert.Equal((2, Side.Player), (held.Turn, held.Phase));
        Assert.Equal(BattleResult.Won, held.Outcome.Result);
    }

    [Fact]
    public void AHoldThroughTheLastTurnsEnemyPhaseWinsRatherThanTimingOut()
    {
        var held = Stepped(Hall(turnLimit: 1)).Do(new EndPhase()).Do(new EndPhase());

        Assert.Equal(2, held.Turn);
        Assert.Equal(BattleResult.Won, held.Outcome.Result);
    }

    [Fact]
    public void ACaptainBesideAHeldSeizeTileWhenThePlayerPhaseBeginsHoldsNothing()
    {
        var beside = Start(map: Hall()).Do(new Move("hale", new Coord(11, 1))).Do(new EndPhase()).Do(new EndPhase());

        Assert.True(beside.AtPlayerPhaseStart);
        Assert.False(beside.Outcome.IsOver);
    }

    /// <summary>The guard on turn 1: no enemy phase came before it, so a captain placed on the tile has held nothing.</summary>
    [Fact]
    public void ACaptainPlacedOnAHeldSeizeTileHasHeldNothingOnTurnOne()
    {
        var state = Start(map: Hall(captainAt: "12,1"));

        Assert.False(state.AtPlayerPhaseStart);
        Assert.False(state.Outcome.IsOver);
        Assert.Equal(BattleResult.Won, state.Do(new EndPhase()).Do(new EndPhase()).Outcome.Result);
    }

    [Fact]
    public void TheObjectiveAndRulesOfAHeldSeizeMapNameTheHold()
    {
        var state = Start(map: Hall());

        Assert.Equal("Get the captain to the gate and hold it through an enemy phase by the end of turn 10. Captain hale must survive.", Objective.Line(state, Starter));
        Assert.Equal("The captain, hale (A), must still stand on the gate at 12,1 when a player phase begins: step on, then hold it through the enemy phase. A step on turn 10 held through its enemy phase wins. Only the captain seizes.", Assert.Single(Objective.Rules(state, Starter)));
    }

    [Fact]
    public void TheCaptainsStepOntoAHeldSeizeTileNoticesWhatMustHold()
    {
        var before = Start(map: Hall());
        var move = new Move("hale", Throne);
        var after = before.Do(move);

        Assert.Equal("hale stands on the gate. The map is won if hale still stands there when turn 1's enemy phase ends.", Assert.Single(Objective.Notices(before, after, Starter, move)));
        Assert.Empty(Objective.Notices(Start(map: Hall(header: null)), Start(map: Hall(header: null)).Do(new Move("hale", new Coord(11, 1))), Starter, new Move("hale", new Coord(11, 1))));
    }

    [Fact]
    public void TheSeizeHoldHeaderRoundTripsAndIsAbsentWhenOff()
    {
        var held = MapFixture.Parse(Hall(), "hall.map");

        Assert.True(held.SeizeHold);
        Assert.Contains("seize_hold: 1\n", MapFormat.Write(held, Starter), StringComparison.Ordinal);
        Assert.Equal(held, MapFixture.Parse(MapFormat.Write(held, Starter), "hall.map"));
        Assert.False(MapFixture.Parse(Hall(header: null), "hall.map").SeizeHold);
        Assert.DoesNotContain("seize_hold", MapFormat.Write(MapFixture.Parse(Hall(header: null), "hall.map"), Starter), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("rout", "seize_hold: 1", "seize_hold: needs win: seize")]
    [InlineData("seize", "seize_hold: 2", "must be 1")]
    [InlineData("seize", "seize_hold: on", "must be 1")]
    public void ABadSeizeHoldHeaderIsRefusedNamingTheFileAndLine(string win, string header, string problem)
    {
        var ex = Assert.Throws<MapException>(() => MapFixture.Parse(Hall(win, header), "hall.map"));

        Assert.StartsWith("hall.map, line 4: ", ex.Message, StringComparison.Ordinal);
        Assert.Contains(problem, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>The First Shrine is the one map that holds its seize tile (issue 1274): Maud holds the altar.</summary>
    [Fact]
    public void TheFirstShrinesAltarIsHeldThroughAnEnemyPhase()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "quests", "the_first_shrine.map"), content);

        Assert.True(map.SeizeHold);
    }
}
