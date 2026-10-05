using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The Sim's approach for a recruit the veto does not cover (issue 1044): section 8's approach,
/// unless its tile is one where the cycle's no-crit sum reaches the recruit's HP; then the
/// captain's approach key, lethal tiles last, so a fast recruit stops short of a group it would
/// meet alone.
/// </summary>
public class HeuristicApproachTests
{
    /// <summary>A 16x3 field: Hale the captain at 0,0, Wren at 0,1, a pack of four soldiers at column <paramref name="column"/>.</summary>
    private static string Field(int column) => $"""
        name: Field
        size: 16x3
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ................
        ................
        ................

        units:
        P captain 0,0
        P recruit 0,1
        E soldier {column},0 group:pack behavior:aggressive
        E soldier {column},1 group:pack behavior:aggressive
        E soldier {column},2 group:pack behavior:aggressive
        E soldier {column + 1},1 group:pack behavior:aggressive

        """;

    private static (BattleState State, BattleUnit Wren, Coord? Blind) Probe(int column)
    {
        var state = Start(map: Field(column));
        var wren = state.Find("wren")!;
        var enemies = state.UnitsOf(Side.Enemy).ToList();
        var blind = EnemyAi.Approach(state, Starter, wren, wren.EquippedWeapon(Starter)!, state.ReachOf(wren, Starter), enemies, enemies.Select(e => state.ReachOf(e, Starter)).ToList());
        return (state, wren, blind);
    }

    private static Coord EndOf(IReadOnlyList<Command> plan, BattleUnit unit) => plan.OfType<Move>().Select(m => m.To).DefaultIfEmpty(unit.At).Single();

    private static bool Lethal(BattleState state, BattleUnit unit, Coord tile) => Exposure.Of(state, Starter, unit, tile).NoCrit >= unit.Hp;

    [Fact]
    public void ARecruitWhoseApproachTileIsLethalStopsShortOnTheNearestTileThatIsNot()
    {
        var (state, wren, blind) = Probe(8);
        Assert.Equal(new Coord(4, 1), blind);
        Assert.True(Lethal(state, wren, blind!.Value));

        var end = EndOf(HeuristicPlayer.PlanUnit(state, Starter, wren, out _), wren);

        Assert.Equal(new Coord(3, 1), end);
        Assert.False(Lethal(state, wren, end));
    }

    [Fact]
    public void ARecruitWhoseApproachTileIsNotLethalTakesSectionEightsApproach()
    {
        var (state, wren, blind) = Probe(9);
        Assert.False(Lethal(state, wren, blind!.Value));

        Assert.Equal(blind, EndOf(HeuristicPlayer.PlanUnit(state, Starter, wren, out _), wren));
    }
}
