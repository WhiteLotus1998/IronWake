using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The Sim's heuristic takes Canto (issue 262): after a unit's Attack, Item or Wait, when a
/// Canto is owed, it rides to the tile in its Canto reach with the lowest no-crit
/// <see cref="Exposure"/> sum, stays when its own tile is already that low, refuses a tile
/// the veto refuses for a unit whose death loses the map, and never leaves an Escape exit
/// for a tile that is not one.
/// </summary>
public class HeuristicCantoTests
{
    /// <summary>A 12x4 field: Hale at 0,1, a rider at 6,2 beside a holding soldier at 7,2, a forest at 7,1 on the soldier's shoulder.</summary>
    private const string Field = """
        name: Field
        size: 12x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ............
        .......^....
        ............
        ............

        units:
        P captain 0,1
        P recruit:rider 6,2
        E soldier 7,2 group:g behavior:hold

        """;

    private static readonly Unit Rider = Recruit("rider", "outrider", new Stats(40, 9, 0, 9, 9, 5, 9, 2, 3), "iron_lance");

    private static BattleState Start(string map = Field) =>
        BattleState.From(MapFixture.Parse(map, "field.map"), Starter, ValueList<Unit>.Of(Hale, Rider), 7);

    private static BattleState Do(BattleState state, Command command)
    {
        var result = Resolver.Apply(state, Starter, command);
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result.Next;
    }

    /// <summary>The rider has waited where it stands at <paramref name="hp"/> HP and is owed its full Canto.</summary>
    private static (BattleState State, BattleUnit Rider) Waited(string map = Field, int hp = 1)
    {
        var state = Start(map);
        state = state.WithUnit(state.Find("rider")! with { Hp = hp });
        state = Do(state, new Wait("rider"));
        var rider = state.Find("rider")!;
        Assert.NotNull(state.CantoReachOf(rider, Starter));
        return (state, rider);
    }

    [Fact]
    public void ACantoThatLeavesALethalTileIsTaken()
    {
        var (state, rider) = Waited();
        Assert.True(Exposure.Of(state, Starter, rider, rider.At).NoCrit >= rider.Hp, "the rider's own tile is lethal");

        var canto = HeuristicPlayer.PlanCanto(state, Starter, rider);

        Assert.NotEqual(rider.At, canto.To);
        Assert.Equal(0, Exposure.Of(state, Starter, rider, canto.To).NoCrit);
    }

    [Fact]
    public void TheHeuristicTakesAnOwedCantoBeforeAnyOtherUnitActs()
    {
        var (state, rider) = Waited();

        var next = new HeuristicPlayer().Next(state, Starter);

        Assert.Equal(new Command[] { HeuristicPlayer.PlanCanto(state, Starter, rider) }, next);
        var after = Do(state, next[0]);
        Assert.Null(after.CantoReachOf(after.Find("rider")!, Starter));
    }

    /// <summary>
    /// The forest at 7,1 is the best tile by avoid and is inside the Canto reach, but the
    /// soldier reaches it for more than the rider's HP, so the veto refuses it for a rider
    /// the map's <c>protect:</c> header names.
    /// </summary>
    [Fact]
    public void ACantoThatWouldEnterALethalTileIsRefused()
    {
        var (state, rider) = Waited(Field.Replace("enemy_level: 1\n", "enemy_level: 1\nprotect: rider\n"));
        Assert.True(HeuristicPlayer.LosesTheMap(state, rider));
        var forest = new Coord(7, 1);
        Assert.True(state.CantoReachOf(rider, Starter)!.CanEnd(forest));
        Assert.True(Exposure.Of(state, Starter, rider, forest).NoCrit >= rider.Hp);

        var canto = HeuristicPlayer.PlanCanto(state, Starter, rider);

        Assert.NotEqual(forest, canto.To);
        Assert.True(Exposure.Of(state, Starter, rider, canto.To).NoCrit < rider.Hp);
    }

    [Fact]
    public void StayIsChosenWhenNothingIsSafer()
    {
        var (state, rider) = Waited(Field.Replace("P recruit:rider 6,2", "P recruit:rider 2,2"));
        Assert.Equal(0, Exposure.Of(state, Starter, rider, rider.At).NoCrit);

        Assert.Equal(new Canto("rider", rider.At), HeuristicPlayer.PlanCanto(state, Starter, rider));
    }

    /// <summary>
    /// The rider stands on the only exit in its Canto reach, beside the soldier, a lethal
    /// tile, and every safer tile in reach is off an exit, so it stays: leaving would undo
    /// the escape.
    /// </summary>
    [Fact]
    public void ACantoNeverLeavesAnEscapeExitForATileThatIsNotOne()
    {
        var escape = Field.Replace("win: rout", "win: escape").Replace("enemy_level: 1\n", "enemy_level: 1\nexit: 0,0 6,2\n");
        var (state, rider) = Waited(escape);
        Assert.True(Exposure.Of(state, Starter, rider, rider.At).NoCrit >= rider.Hp);

        Assert.Equal(new Canto("rider", rider.At), HeuristicPlayer.PlanCanto(state, Starter, rider));
    }
}
