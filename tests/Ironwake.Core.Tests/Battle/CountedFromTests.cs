using Ironwake.Content;
using Ironwake.Cli;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 1237 (round 415): <c>threat</c> keeps each line's own tile and, when the total seats the
/// line elsewhere, says where it counts it, <c>(counted from 9,4)</c>; a priced line the seating
/// drops says <c>(not counted: 8,3 taken)</c>. <see cref="Queries.CountedFrom"/> is the same walk
/// as the total, and the protocol carries it as <c>countedFrom</c>. The board is issue 253's: the
/// Bulwark trial's corridor, the captain on 5,3, both brigands striking only from 4,3.
/// </summary>
public sealed class CountedFromTests
{
    private static readonly Coord Door = new(4, 3);
    private static readonly Coord Side = new(6, 3);

    private static (BattleState State, IReadOnlyList<ThreatLine> Lines) Corridor()
    {
        var map = MapFiles.Load(Path.Combine(Ironwake.Core.Tests.Content.Fixture.RealContentDirectory(), "trials", "bulwark_trial.map"), Starter);
        var state = BattleState.From(map, Starter, Starter.Cast, 12);
        state = state.WithUnit(state.Find("captain")! with { At = new Coord(5, 3), Hp = 25 });
        return (state, Queries.Threats(state, Starter, state.Find("captain")!, new Coord(5, 3))!);
    }

    /// <summary>The corridor's lines with the second also able to strike from 6,3, so the seating moves it there.</summary>
    private static IReadOnlyList<ThreatLine> SecondCanAlsoStrikeFromTheSide(IReadOnlyList<ThreatLine> lines) =>
        new[]
        {
            lines[0] with { From = Door, Tiles = ValueList<Coord>.Of(Door) },
            lines[1] with { From = Door, Tiles = ValueList<Coord>.From(new[] { Door, Side }) },
        };

    [Fact]
    public void CountedFromSeatsTheLinesAsTheTotalDoes()
    {
        var (_, lines) = Corridor();
        var moved = SecondCanAlsoStrikeFromTheSide(lines);

        var counted = Queries.CountedFrom(moved);

        Assert.Equal(new Coord?[] { Door, Side }, counted);
        Assert.Equal(moved.Sum(l => l.IfAllLand), Queries.IfAllLand(moved));
    }

    [Fact]
    public void CountedFromLeavesADroppedLineNull()
    {
        var (_, lines) = Corridor();
        Assert.Equal(2, lines.Count);

        var counted = Queries.CountedFrom(lines);

        Assert.Single(counted, c => c == Door);
        Assert.Single(counted, c => c is null);
        var kept = lines[counted.ToList().FindIndex(c => c == Door)];
        Assert.Equal(kept.IfAllLand, Queries.IfAllLand(lines));
    }

    [Fact]
    public void CountedFromLeavesARaiseNull()
    {
        var (_, lines) = Corridor();
        var raised = new[] { lines[0] with { Raises = true }, lines[1] };

        var counted = Queries.CountedFrom(raised);

        Assert.Null(counted[0]);
        Assert.Equal(Door, counted[1]);
    }

    [Fact]
    public void ThreatNamesTheTileTheTotalCountsALineFromWhenItDiffers()
    {
        var (state, lines) = Corridor();
        var moved = SecondCanAlsoStrikeFromTheSide(lines);
        var names = UnitNames.Of(state, Starter);

        var text = PlaySession.ThreatText(state, Starter, state.Find("captain")!, new Coord(5, 3), moved, Array.Empty<SleepingThreat>());

        Assert.Contains($"  {names[moved[1].Enemy.Id]} from 4,3 (counted from 6,3) with ", text);
        Assert.Contains($"  {names[moved[0].Enemy.Id]} from 4,3 with ", text);
        Assert.DoesNotContain("not counted", text);
    }

    [Fact]
    public void ThreatSaysALineTheSeatingDropsIsNotCounted()
    {
        var (state, lines) = Corridor();
        var counted = Queries.CountedFrom(lines);
        var dropped = lines[counted.ToList().FindIndex(c => c is null)];
        var kept = lines[counted.ToList().FindIndex(c => c == Door)];
        var names = UnitNames.Of(state, Starter);

        var text = PlaySession.ThreatText(state, Starter, state.Find("captain")!, new Coord(5, 3), lines, Array.Empty<SleepingThreat>());

        Assert.Contains($"  {names[dropped.Enemy.Id]} from 4,3 (not counted: 4,3 taken) with ", text);
        Assert.Contains($"  {names[kept.Enemy.Id]} from 4,3 with ", text);
        Assert.DoesNotContain("counted from", text);
    }

    [Fact]
    public void ALineWithAFreeOwnTileIsCountedThereWhateverOrderItsTilesComeIn()
    {
        var (_, lines) = Corridor();
        var lone = new[] { lines[0] with { From = Door, Tiles = ValueList<Coord>.From(new[] { Side, Door }) } };

        Assert.Equal(new Coord?[] { Door }, Queries.CountedFrom(lone));
    }

    [Fact]
    public void ALineDisplacedOntoAnotherLinesOwnTileSwapsBack()
    {
        var (_, lines) = Corridor();
        var crossed = new[]
        {
            lines[0] with { From = Door, Tiles = ValueList<Coord>.From(new[] { Side, Door }) },
            lines[1] with { From = Side, Tiles = ValueList<Coord>.From(new[] { Door, Side }) },
        };

        Assert.Equal(new Coord?[] { Door, Side }, Queries.CountedFrom(crossed));
    }

    [Fact]
    public void ThreatAnnotatesNothingWhenEveryLineCountsWhereItReads()
    {
        var (state, lines) = Corridor();
        var one = new[] { lines[0] };

        var text = PlaySession.ThreatText(state, Starter, state.Find("captain")!, new Coord(5, 3), one, Array.Empty<SleepingThreat>());

        Assert.DoesNotContain("counted", text);
    }

    [Fact]
    public void TheProtocolCarriesEachLinesSeat()
    {
        var (state, lines) = Corridor();
        var session = new ProtocolSession(Starter, state, new StringWriter());

        var answer = session.Answer("""{"query":"threat","unit":"captain"}""");

        Assert.Contains("\"countedFrom\":{\"x\":4,\"y\":3}", answer);
        Assert.Contains("\"countedFrom\":null", answer);
        Assert.Contains("(not counted: 4,3 taken)", answer);
    }
}
