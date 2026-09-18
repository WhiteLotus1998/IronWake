namespace Ironwake.Core.Tests.Maps;

/// <summary>
/// Enemy templates scale on effective growth, unit plus class (DESIGN.md section 3,
/// DECISIONS/0005 as amended by issue 84), checked against the starter content.
/// </summary>
public class EnemyScalingTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static Unit Raised(string templateId, int level)
    {
        var template = Content.Unit(templateId);
        return template.AtLevel(level, Content.Class(template.ClassId));
    }

    /// <summary>
    /// Every enemy template raised to level 5, one row per template: the level-1 templates
    /// gain four levels, the level-3 bandit leader two.
    /// </summary>
    [Theory]
    [InlineData("soldier", 21, 7, 0, 5, 6, 2, 4, 1, 2)]
    [InlineData("brigand", 22, 8, 0, 4, 4, 1, 2, 0, 1)]
    [InlineData("archer", 18, 6, 0, 8, 6, 2, 2, 1, 2)]
    [InlineData("hexer", 17, 1, 7, 6, 5, 2, 1, 5, 2)]
    [InlineData("acolyte", 17, 1, 5, 5, 5, 4, 1, 6, 3)]
    [InlineData("rider", 21, 7, 0, 5, 6, 2, 4, 1, 2)]
    [InlineData("wingrider", 18, 6, 0, 6, 8, 3, 2, 4, 2)]
    [InlineData("shieldbearer", 23, 7, 0, 3, 3, 1, 7, 0, 2)]
    [InlineData("bandit_leader", 25, 9, 0, 4, 5, 3, 4, 1, 4)]
    public void LevelUpGrowthIsUnitGrowthPlusTheClassModifier(
        string templateId, int hp, int str, int mag, int dex, int spd, int lck, int def, int res, int cha)
    {
        var raised = Raised(templateId, 5);

        Assert.Equal(5, raised.Level);
        Assert.Equal(new Stats(hp, str, mag, dex, spd, lck, def, res, cha), raised.Stats);
    }

    /// <summary>
    /// Issue 84's table: over four levels gained, what the class modifier adds beyond the
    /// unit's own growth. The sign matters (the reaver trades Spd for Str), and a row with
    /// no difference is a row too.
    /// </summary>
    [Theory]
    [InlineData("soldier", "Hp +1")]
    [InlineData("brigand", "Str +1 Spd -1")]
    [InlineData("archer", "Dex +1")]
    [InlineData("hexer", "Mag +1")]
    [InlineData("acolyte", "")]
    [InlineData("rider", "Hp +1")]
    [InlineData("wingrider", "Spd +1")]
    [InlineData("shieldbearer", "Def +1")]
    [InlineData("bandit_leader", "Str +1 Spd -1")]
    public void TheClassModifierChangesTheTemplateByTheIssueTableOverFourLevels(string templateId, string expected)
    {
        var template = Content.Unit(templateId);
        var byOwnGrowth = template.Stats.Map((stat, value) => value + template.Growths.Get(stat) * 4 / 100);

        var difference = Raised(templateId, template.Level + 4).Stats - byOwnGrowth;

        var actual = string.Join(" ", Stats.All
            .Where(stat => difference.Get(stat) != 0)
            .Select(stat => $"{stat} {difference.Get(stat):+#;-#}"));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NoStarterTemplateHasAnEffectiveGrowthOutsideZeroToOneHundred()
    {
        foreach (var template in Content.Units.Values)
        {
            var growths = template.EffectiveGrowths(Content.Class(template.ClassId));
            foreach (var stat in Stats.All)
            {
                Assert.InRange(growths.Get(stat), 0, 100);
            }
        }
    }

    [Fact]
    public void EnemyUnitScalesOnTheClassTheTemplateNames()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad) with { EnemyLevel = 5 };
        var brigand = map.Placements.OfType<EnemyPlacement>().Single(e => e.TemplateId == "brigand");

        Assert.Equal(Raised("brigand", 5), map.EnemyUnit(brigand, Content));
    }
}
