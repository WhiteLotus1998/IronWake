namespace Ironwake.Core.Tests.Maps;

public class MapDefinitionTests
{
    private static readonly MapDefinition Map = MapFixture.Parse(MapFixture.OldMillRoad);

    private static EnemyPlacement Enemy(MapDefinition map, string templateId) =>
        map.Placements.OfType<EnemyPlacement>().Single(e => e.TemplateId == templateId);

    [Fact]
    public void EnemyUnitRaisesATemplateBelowTheMapsEnemyLevelStatsIncluded()
    {
        var raised = Map with { EnemyLevel = 5 };

        var soldier = raised.EnemyUnit(Enemy(raised, "soldier"), MapFixture.Content);

        Assert.Equal(5, soldier.Level);
        Assert.Equal(new Stats(21, 7, 0, 5, 6, 2, 4, 1, 2), soldier.Stats);
        Assert.Equal(MapFixture.Content.Unit("soldier").Growths, soldier.Growths);
    }

    [Fact]
    public void EnemyUnitLeavesATemplateAtOrAboveTheMapsEnemyLevelAlone()
    {
        var raised = Map with { EnemyLevel = 3 };
        var template = MapFixture.Content.Unit("bandit_leader");

        Assert.Equal(template, raised.EnemyUnit(Enemy(raised, "bandit_leader"), MapFixture.Content));
        Assert.Equal(template, Map.EnemyUnit(Enemy(Map, "bandit_leader"), MapFixture.Content));
    }

    [Fact]
    public void EnemyUnitAtTheDefaultLevelIsTheTemplate()
    {
        Assert.Equal(1, Map.EnemyLevel);
        Assert.Equal(MapFixture.Content.Unit("brigand"), Map.EnemyUnit(Enemy(Map, "brigand"), MapFixture.Content));
    }
}
