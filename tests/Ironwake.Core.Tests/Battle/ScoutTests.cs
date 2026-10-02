using System.Text.Json;
using Ironwake.Cli;
using Ironwake.Content.Protocol;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Rook's Scout in battle (issue 706 slice 2): High Watch sees 2 tiles further than the company on a
/// dusk map, and Headcount makes <c>threat</c> price a sleeping group with a member within 6 tiles of
/// her, never in the total. A Skyrider in her place does neither.
/// </summary>
public class ScoutTests
{
    private static Unit Rook(string classId) => Recruit("rook", classId, new Stats(22, 8, 0, 9, 12, 6, 4, 5, 2), "iron_lance");

    private static BattleState Night(string classId) =>
        Start(7, ValueList<Unit>.Of(Hale, Rook(classId)), """
            name: Night
            size: 8x4
            win: rout
            turn_limit: 10
            recall: 3
            enemy_level: 1
            dusk: 1

            ........
            ........
            ........
            ........

            units:
            P captain 0,0
            P recruit:rook 0,3
            E soldier 3,3 group:a behavior:hold

            """);

    /// <summary>The captain 5 tiles from a sleeping soldier, inside its reach and outside the wake radius, and Rook <paramref name="rookX"/> along the top row: 6 tiles from the soldier at 8, 7 at 7.</summary>
    private static BattleState Camp(string classId, int rookX = 8) =>
        Start(7, ValueList<Unit>.Of(Hale, Rook(classId)), $"""
            name: Camp
            size: 12x4
            win: rout
            turn_limit: 10
            recall: 3
            enemy_level: 1

            ............
            ............
            ............
            ............

            units:
            P captain 6,3
            P recruit:rook {rookX},0
            E soldier 11,3 group:y behavior:guard

            """);

    [Fact]
    public void HighWatchSeesTwoTilesFurtherAtDusk()
    {
        var scout = Night("scout");
        var rider = Night("skyrider");

        Assert.Equal(2, scout.Find("rook")!.ExtraSight);
        Assert.Equal(0, rider.Find("rook")!.ExtraSight);
        Assert.True(Dusk.Seen(scout, scout.Find("soldier-1")!));
        Assert.False(Dusk.Seen(rider, rider.Find("soldier-1")!));
    }

    [Fact]
    public void TheDuskLineSaysHowFarTheScoutSees()
    {
        Assert.Contains("dusk: sight 1; it gets no darker; rook sees 3; no side strikes", Dusk.Line(Night("scout")));
        Assert.DoesNotContain("sees", Dusk.Line(Night("skyrider")));
    }

    [Fact]
    public void HighWatchSurvivesTheProtocolRoundTrip()
    {
        var state = Night("scout");

        var read = ProtocolJson.ReadState(ProtocolJson.State(state, Starter), Starter);

        Assert.Equal(2, read.Find("rook")!.ExtraSight);
    }

    [Fact]
    public void HeadcountPricesASleepingGroupWithinSixAsTheWokenBoardWould()
    {
        var state = Camp("scout");
        var hale = state.Find("hale")!;

        var group = Queries.SleepingThreats(state, Starter, hale, hale.At)!.Single();
        var woken = Queries.Threats(state.Wake("y"), Starter, hale, hale.At)!.Single();

        Assert.Equal("rook", group.CountedBy?.Id);
        Assert.Equal(woken.IfAllLand, group.Priced.Single().IfAllLand);
        Assert.Equal(woken.Forecast, group.Priced.Single().Forecast);
        Assert.Empty(Queries.Threats(state, Starter, hale, hale.At)!);
    }

    [Fact]
    public void ASkyriderNamesTheSameGroupWithoutNumbers()
    {
        var state = Camp("skyrider");
        var hale = state.Find("hale")!;

        var group = Queries.SleepingThreats(state, Starter, hale, hale.At)!.Single();

        Assert.Null(group.CountedBy);
        Assert.Empty(group.Priced);
    }

    [Fact]
    public void HeadcountLeavesAGroupPastSixTilesUnpriced()
    {
        var state = Camp("scout", rookX: 7);
        var hale = state.Find("hale")!;

        var group = Queries.SleepingThreats(state, Starter, hale, hale.At)!.Single();

        Assert.Null(group.CountedBy);
    }

    [Fact]
    public void HeadcountCountsFromTheTileTheScoutIsAskedAbout()
    {
        var state = Camp("scout", rookX: 7);
        var rook = state.Find("rook")!;

        Assert.Equal("rook", Queries.SleepingThreats(state, Starter, rook, new Coord(8, 1))!.Single().CountedBy?.Id);
    }

    [Fact]
    public void ThreatPrintsTheCountedGroupOutsideTheTotal()
    {
        var state = Camp("scout");
        var hale = state.Find("hale")!;
        var asleep = Queries.SleepingThreats(state, Starter, hale, hale.At)!;
        var priced = asleep.Single().Priced.Single();

        var text = Ironwake.Cli.PlaySession.ThreatText(state, Starter, hale, hale.At, Queries.Threats(state, Starter, hale, hale.At)!, asleep);

        Assert.Contains("no enemy can strike", text);
        Assert.Contains("\n  The y group is asleep; rook counts it, if woken:\n    Soldier at 11,3 from ", text);
        Assert.Contains($"\n    If woken and all land: {priced.IfAllLand} against {hale.Hp} hp (asleep, not in the total)\n", text);
    }

    [Fact]
    public void TheProtocolsThreatCarriesTheCountedGroupApartFromTheTotal()
    {
        var state = Camp("scout");
        var session = new ProtocolSession(Starter, state, new StringWriter());

        using var answer = JsonDocument.Parse(session.Answer("""{"query":"threat","unit":"hale"}"""));
        var group = answer.RootElement.GetProperty("asleep")[0];

        Assert.Equal(0, answer.RootElement.GetProperty("ifAllLand").GetInt32());
        Assert.Equal("rook", group.GetProperty("countedBy").GetString());
        Assert.Equal("soldier-1", group.GetProperty("priced")[0].GetProperty("enemy").GetString());
        Assert.Equal(Queries.SleepingThreats(state, Starter, state.Find("hale")!, state.Find("hale")!.At)!.Single().Priced.Single().IfAllLand, group.GetProperty("ifWokenAllLand").GetInt32());
    }

    [Fact]
    public void TheProtocolsThreatLeavesAnUncountedGroupWithoutNumbers()
    {
        var session = new ProtocolSession(Starter, Camp("skyrider"), new StringWriter());

        using var answer = JsonDocument.Parse(session.Answer("""{"query":"threat","unit":"hale"}"""));
        var group = answer.RootElement.GetProperty("asleep")[0];

        Assert.False(group.TryGetProperty("countedBy", out _));
        Assert.False(group.TryGetProperty("priced", out _));
    }
}
