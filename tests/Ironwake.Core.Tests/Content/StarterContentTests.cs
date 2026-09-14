using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Core.Tests.Content;

public class StarterContentTests
{
    private static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    [Fact]
    public void StarterContentLoads()
    {
        Assert.Equal(9, Content.Terrain.Count);
        Assert.Equal(9, Content.Classes.Count);
        Assert.Equal(17, Content.Weapons.Count);
        Assert.Equal(9, Content.Units.Count);
    }

    [Fact]
    public void LoadSerializeLoadIsEqual()
    {
        var written = ContentSerializer.Write(Content);
        var reloaded = ContentLoader.Parse(written);

        Assert.Equal(Content, reloaded);
        Assert.Equal(Content.GetHashCode(), reloaded.GetHashCode());
    }

    [Fact]
    public void SerializedTextIsCanonical()
    {
        var once = ContentSerializer.Write(Content);
        var twice = ContentSerializer.Write(ContentLoader.Parse(once));

        Assert.Equal(once.Classes.Text, twice.Classes.Text);
        Assert.Equal(once.Weapons.Text, twice.Weapons.Text);
        Assert.Equal(once.Terrain.Text, twice.Terrain.Text);
        Assert.Equal(once.Units.Single().Text, twice.Units.Single().Text);
    }

    // The terrain table from DESIGN.md section 4, one row per terrain: costs are
    // infantry, cavalry, flying, armored; -1 means impassable.
    [Theory]
    [InlineData("plain", '.', 1, 1, 1, 1, 0, 0, 0, 0, false)]
    [InlineData("road", '=', 1, 1, 1, 1, 0, 0, 0, 0, false)]
    [InlineData("forest", '^', 2, 3, 1, 2, 20, 1, 0, 0, false)]
    [InlineData("hill", 'n', 2, 3, 1, 3, 10, 1, 0, 0, false)]
    [InlineData("mountain", 'M', 3, -1, 1, -1, 30, 2, 0, 0, false)]
    [InlineData("water", '~', -1, -1, 1, -1, 0, 0, 0, 0, false)]
    [InlineData("fort", 'F', 1, 1, 1, 1, 30, 2, 2, 20, true)]
    [InlineData("wall", '#', -1, -1, -1, -1, 0, 0, 0, 0, false)]
    [InlineData("throne", 'T', 1, 1, 1, 1, 30, 3, 3, 20, true)]
    public void TerrainMatchesTheDesignTable(
        string id, char glyph, int infantry, int cavalry, int flying, int armored,
        int avoid, int def, int res, int heal, bool appliesToFlyers)
    {
        var terrain = Content.TerrainById(id);

        Assert.Equal(glyph, terrain.Glyph);
        Assert.Equal(infantry < 0 ? null : infantry, terrain.MoveCost(MovementType.Infantry));
        Assert.Equal(cavalry < 0 ? null : cavalry, terrain.MoveCost(MovementType.Cavalry));
        Assert.Equal(flying < 0 ? null : flying, terrain.MoveCost(MovementType.Flying));
        Assert.Equal(armored < 0 ? null : armored, terrain.MoveCost(MovementType.Armored));
        Assert.Equal(avoid, terrain.Avoid);
        Assert.Equal(def, terrain.Def);
        Assert.Equal(res, terrain.Res);
        Assert.Equal(heal, terrain.HealPercent);
        Assert.Equal(appliesToFlyers, terrain.AppliesToFlyers);
        Assert.Same(terrain, Content.TerrainByGlyph(glyph));
    }

    [Fact]
    public void ClassesCoverEveryMovementTypeAndEveryWeaponType()
    {
        foreach (var movement in Enum.GetValues<MovementType>())
        {
            Assert.Contains(Content.Classes.Values, c => c.Movement == movement);
        }

        foreach (var weaponType in Enum.GetValues<WeaponType>())
        {
            Assert.Contains(Content.Classes.Values, c => c.CanUse(weaponType));
            Assert.True(Content.Weapons.Values.Count(w => w.Type == weaponType) >= 2, weaponType + " needs at least two weapons");
        }
    }

    [Fact]
    public void EveryEnemyTemplateCarriesAWeaponItsClassCanUse()
    {
        foreach (var unit in Content.Units.Values)
        {
            var unitClass = Content.Class(unit.ClassId);
            Assert.Contains(unit.Inventory.Items, item => unitClass.CanUse(Content.Weapon(item.ItemId).Type));
        }
    }

    [Fact]
    public void RangesFollowSectionFive()
    {
        foreach (var weapon in Content.Weapons.Values)
        {
            var (min, max) = weapon.Type switch
            {
                WeaponType.Bow => (2, 2),
                WeaponType.Reason or WeaponType.Faith => (1, 2),
                _ => (1, 1),
            };
            Assert.True(weapon.MinRange >= min && weapon.MaxRange <= max, weapon.Id + " range is outside its type's band");
        }
    }

    [Fact]
    public void LookupsByUnknownIdNameTheId()
    {
        var e = Assert.Throws<KeyNotFoundException>(() => Content.Weapon("no_such_weapon"));

        Assert.Contains("no_such_weapon", e.Message);
        Assert.Null(Content.TerrainByGlyph('?'));
    }

    [Fact]
    public void ExampleMapUnitsFromTheDesignDocExist()
    {
        Assert.NotNull(Content.Unit("soldier"));
        Assert.NotNull(Content.Unit("archer"));
        Assert.NotNull(Content.Unit("brigand"));
        Assert.NotNull(Content.Unit("bandit_leader"));
    }
}
