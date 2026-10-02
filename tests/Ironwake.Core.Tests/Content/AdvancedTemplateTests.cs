using Ironwake.Content;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The second tier's enemies (issue 704, slice 3): one template per advanced form, each its base
/// template's numbers in the form, so what changes on the board is the form's modifiers and verb.
/// They load, scale to a map's enemy level and field like any other template; the sample
/// <c>docs/samples/sallow_grange_advanced.map</c> fields four. The Sim's level table counts a unit
/// ready for the tier by the form's own requirements.
/// </summary>
public class AdvancedTemplateTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    [Theory]
    [InlineData("veteran", "halberdier", "soldier")]
    [InlineData("marauder", "berserker", "brigand")]
    [InlineData("longbowman", "marksman", "archer")]
    [InlineData("lector", "scholar", "hexer")]
    [InlineData("deacon", "warden", "acolyte")]
    [InlineData("heavy_rider", "lancer", "rider")]
    [InlineData("wing_captain", "skycaptain", "wingrider")]
    [InlineData("sentry", "sentinel", "shieldbearer")]
    public void EachAdvancedFormHasATemplateWithItsBaseTemplatesNumbers(string id, string form, string basis)
    {
        var template = Shipped.Unit(id);
        var under = Shipped.Unit(basis);

        Assert.Equal(form, template.ClassId);
        Assert.Equal(Shipped.Class(form).Advances!.Id, under.ClassId);
        Assert.Equal(under.Level, template.Level);
        Assert.Equal(under.Stats, template.Stats);
        Assert.Equal(under.Growths, template.Growths);
        Assert.All(template.Inventory.Items, s => Assert.True(template.CanWield(Shipped.Weapon(s.ItemId), Shipped.Class(form))));
        Assert.True(Shipped.StatsOf(template).Hp >= Shipped.StatsOf(under).Hp);
    }

    /// <summary>A unique class (issue 706) is one cast member's other door, never fielded by the enemy, so it has no template.</summary>
    [Fact]
    public void EveryAdvancedFormHasExactlyOneTemplate()
    {
        var cast = Shipped.Cast.Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var form in Shipped.Classes.Values.Where(c => c.Advances is not null && !c.Captain && c.Unique is null))
        {
            Assert.Single(Shipped.Units.Values, u => !cast.Contains(u.Id) && u.ClassId == form.Id);
        }
    }

    [Fact]
    public void AnAdvancedTemplateScalesToTheMapsEnemyLevelLikeAnyOther()
    {
        var map = MapFiles.Load(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "sallow_grange_advanced.map"), Shipped);
        var placement = map.Placements.OfType<EnemyPlacement>().Single(p => p.TemplateId == "longbowman");

        var fielded = map.EnemyUnit(placement, Shipped);

        Assert.Equal(map.EnemyLevel, fielded.Level);
        Assert.Equal(Shipped.Unit("longbowman").ScaledTo(map.EnemyLevel, Shipped.Class("marksman")), fielded);
        var state = BattleState.From(map, Shipped, Shipped.Cast, 704);
        Assert.Equal("marksman", state.UnitsOf(Side.Enemy).Single(u => u.At == placement.At).Unit.ClassId);
        Assert.Equal(3, Shipped.WeaponOf(fielded, Shipped.Weapon("steel_bow")).MaxRange);
    }

    [Fact]
    public void TheLevelTableCountsAUnitReadyOnlyAtTheFormsLevelAndRank()
    {
        var levy = Shipped.Cast[1];
        var trained = levy with { Level = 7, Skill = WeaponSkill.Zero.With(WeaponType.Lance, 80) };

        Assert.True(LevelRun.Ready(trained, Shipped));
        Assert.False(LevelRun.Ready(trained with { Level = 6 }, Shipped));
        Assert.False(LevelRun.Ready(trained with { Skill = WeaponSkill.Zero.With(WeaponType.Lance, 79) }, Shipped));
    }
}
