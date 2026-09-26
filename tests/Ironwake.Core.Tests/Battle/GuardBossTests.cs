using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// A boss that can sleep (issue 259, DECISIONS/0055): a <c>B</c> line with
/// <c>behavior:guard</c> holds its tile as Boss does until its group wakes, then acts
/// as Aggressive, and may leave its tile and end on a throne.
/// </summary>
public class GuardBossTests
{
    /// <summary>A 10x3 hall: Hale at 2,1, a throne at 5,1, and a guard boss alone in group hall at 8,1, six from Hale.</summary>
    private const string Hall = """
        name: Hall
        size: 10x3
        win: seize
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ..........
        .....T....
        ..........

        units:
        P captain 2,1
        P recruit:wren 0,2
        B grange_reeve 8,1 group:hall behavior:guard

        """;

    private static BattleState Start() => BattleFixture.Start(map: Hall);

    [Fact]
    public void ASleepingGuardBossHoldsItsTile()
    {
        var state = Start().Do(new EndPhase());
        var reeve = state.Find("grange_reeve-1")!;

        Assert.True(reeve.IsBoss);
        Assert.False(state.IsAwake("hall"));
        Assert.Equal(Behavior.Hold, state.EffectiveBehavior(reeve, Starter));
        Assert.Equal(new Command[] { new Wait("grange_reeve-1") }, EnemyAi.PlanUnit(state, Starter, reeve));
    }

    [Fact]
    public void AWokenGuardBossActsAsAggressive()
    {
        var state = Start().Wake("hall").Do(new EndPhase());
        var reeve = state.Find("grange_reeve-1")!;

        Assert.Equal(Behavior.Aggressive, state.EffectiveBehavior(reeve, Starter));
        Assert.IsType<Move>(EnemyAi.PlanUnit(state, Starter, reeve)[0]);
    }

    [Fact]
    public void AWokenGuardBossMayLeaveItsTileAndEndOnTheThrone()
    {
        var state = Start().Wake("hall").Do(new EndPhase());
        var result = state.Try(new Move("grange_reeve-1", new Coord(5, 1)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(new Coord(5, 1), result.Next.Find("grange_reeve-1")!.At);
        Assert.Equal(MapDefinition.ThroneTerrainId, result.Next.Map.TerrainIdAt(new Coord(5, 1)));
    }

    [Fact]
    public void AGuardBossStillCountsAsTheBoss()
    {
        var state = Start();

        Assert.Single(state.UnitsOf(Side.Enemy), u => u.IsBoss);
    }

    [Fact]
    public void TheConsoleNamesAGuardBossAsABossThatSleepsAndWakes()
    {
        var asleep = MapRenderer.Render(Start(), Starter);
        var awake = MapRenderer.Render(Start().Wake("hall"), Starter);
        var placements = MapRenderer.Render(Start().Map, Starter);

        Assert.Contains("group hall, boss, asleep", asleep);
        Assert.Contains("group hall, boss, awake", awake);
        Assert.Contains("group hall, boss, guard", placements);
    }
}
