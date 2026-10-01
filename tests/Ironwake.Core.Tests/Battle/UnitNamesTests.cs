using Ironwake.Cli;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Names, not ids, on screen (issue 609): narration prints each unit's display name, an enemy
/// numbered only when another on the map shares its name, numbered in file order so a spawn
/// never renumbers anyone mid-battle; every narration line is in sentence case.
/// </summary>
public class UnitNamesTests
{
    /// <summary>One brigand, two archers on the board and a third archer by spawn event.</summary>
    private const string Field = """
        name: Field
        size: 8x3
        win: rout
        turn_limit: 5
        recall: 3
        enemy_level: 1

        ........
        ........
        ........

        units:
        P captain 0,1
        P recruit:wren 0,2
        E brigand 7,0 group:a behavior:hold
        E archer 7,1 group:a behavior:hold
        E archer 7,2 group:a behavior:hold

        events:
        late turn 3 enemy spawn archer 6,0 group:b behavior:hold

        """;

    [Fact]
    public void AnEnemyWithAUniqueNameReadsWithoutANumber()
    {
        var names = UnitNames.Of(Start(map: Field), Starter);

        Assert.Equal("Brigand", names["brigand-1"]);
    }

    [Fact]
    public void EnemiesSharingANameAreNumberedInFileOrderSpawnsIncluded()
    {
        var names = UnitNames.Of(Start(map: Field), Starter);

        Assert.Equal("Archer 1", names["archer-1"]);
        Assert.Equal("Archer 2", names["archer-2"]);
        Assert.Equal("Archer 3", names["archer-3"]);
    }

    [Fact]
    public void APlayerUnitReadsAsItsOwnName()
    {
        var state = Start(map: Field);
        var names = UnitNames.Of(state, Starter);

        Assert.All(state.UnitsOf(Side.Player), u => Assert.Equal(u.Unit.Name, names[u.Id]));
    }

    [Fact]
    public void AFallenPlayerUnitStillReadsAsItsName()
    {
        var state = Start(map: Field).Do(new Wait("wren"));
        var wren = state.Find("wren")!;
        var after = state.WithoutUnit("wren");

        Assert.Equal(wren.Unit.Name, UnitNames.Of(after, Starter)["wren"]);
    }

    [Fact]
    public void AnIdTheBattleNeverHeldReadsAsItself()
    {
        Assert.Equal("stranger-9", UnitNames.Of(Start(map: Field), Starter)["stranger-9"]);
        Assert.Equal("archer-1", UnitNames.None["archer-1"]);
    }

    [Fact]
    public void NarrationPrintsNamesNeverIds()
    {
        var state = Start(map: Field);
        var line = PlaySession.Describe(new UnitMoved("archer-2", new Coord(7, 2), new Coord(6, 2), ValueList<Coord>.From(new[] { new Coord(6, 2) })), Starter, UnitNames.Of(state, Starter));

        Assert.Equal("Archer 2 moves 7,2 -> 6,2", line);
        Assert.DoesNotContain("archer-2", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("wren waits", "Wren waits")]
    [InlineData("-- enemy phase, turn 1 --", "-- Enemy phase, turn 1 --")]
    [InlineData("  4,1 becomes Road", "  4,1 becomes Road")]
    [InlineData("a attacks b\n  b misses a", "A attacks b\n  B misses a")]
    [InlineData("Already upper", "Already upper")]
    [InlineData("", "")]
    public void EveryNarrationLineStartsUpperCaseUnlessItOpensWithANumber(string text, string expected)
    {
        Assert.Equal(expected, UnitNames.Sentence(text));
    }
}
