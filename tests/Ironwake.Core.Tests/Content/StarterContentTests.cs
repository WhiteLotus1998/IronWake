using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Core.Tests.Content;

public class StarterContentTests
{
    private static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    /// <summary>Every weapon type, gauntlets included since issue 70's content line: the reaver wields them and Keziah carries a pair.</summary>
    private static readonly WeaponType[] ShippedWeaponTypes = Enum.GetValues<WeaponType>();

    [Fact]
    public void StarterContentLoads()
    {
        Assert.Equal(9, Content.Terrain.Count);
        Assert.Equal(9, Content.Classes.Count);
        Assert.Equal(21, Content.Weapons.Count);
        Assert.Equal(25, Content.Units.Count);
        Assert.Equal(11, Content.Cast.Count);
    }

    /// <summary>DESIGN.md section 9: a captain and ten recruits, three or four per region, every weapon type and movement type covered, hooks between cast members (DECISIONS/0022).</summary>
    [Fact]
    public void TheCastCoversSectionNine()
    {
        var cast = Content.Cast;
        Assert.Equal("captain", cast[0].Id);
        Assert.True(Content.Class(cast[0].ClassId).CanUse(WeaponType.Sword));
        Assert.All(cast.Skip(1), u => Assert.True(cast[0].Stats.Cha > u.Stats.Cha, u.Id + " out-charms the captain"));

        var recruits = cast.Skip(1).ToList();
        Assert.Equal(10, recruits.Count);
        var regions = recruits.GroupBy(u => u.Region).ToDictionary(g => g.Key!, g => g.Count());
        Assert.Equal(3, regions.Count);
        Assert.All(regions.Values, n => Assert.InRange(n, 3, 4));
        Assert.DoesNotContain(cast[0].Region, regions.Keys);

        var classes = cast.Select(u => Content.Class(u.ClassId)).ToList();
        foreach (var type in ShippedWeaponTypes)
        {
            Assert.Contains(classes, c => c.CanUse(type));
        }

        foreach (var movement in Enum.GetValues<MovementType>())
        {
            Assert.Contains(classes, c => c.Movement == movement);
        }

        var ids = cast.Select(u => u.Id).ToHashSet();
        foreach (var recruit in recruits)
        {
            Assert.Equal(2, recruit.Hooks.Count);
            Assert.All(recruit.Hooks, h => Assert.True(ids.Contains(h) && h != recruit.Id, recruit.Id + " hooks " + h));
            Assert.False(string.IsNullOrWhiteSpace(recruit.Personality));
            Assert.NotEqual(recruit.Name, recruit.Id);
        }

        Assert.Empty(cast[0].Hooks);
        Assert.All(cast, u => Assert.All(u.Inventory.Items, s => Assert.True(
            !Content.Weapons.TryGetValue(s.ItemId, out var w) || Content.Class(u.ClassId).CanUse(w.Type), u.Id + " cannot use " + s.ItemId)));
    }

    /// <summary>Design Table, seventh round: no Reason or Faith unit ships with one casting option.</summary>
    [Fact]
    public void EveryCasterCarriesTwoSpells()
    {
        var casters = Content.Cast.Where(u => Content.Class(u.ClassId).Weapons.All(t => t.IsMagic())).ToList();
        Assert.NotEmpty(casters);
        foreach (var caster in casters)
        {
            var spells = caster.Inventory.Items.Count(s => Content.Weapons.ContainsKey(s.ItemId));
            Assert.True(spells >= 2, caster.Id + " carries " + spells + " spells");
        }
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
        Assert.Equal(2, once.Units.Count);
        Assert.Equal(once.Units.Select(u => u.Name), twice.Units.Select(u => u.Name));
        Assert.Equal(once.Units.Select(u => u.Text), twice.Units.Select(u => u.Text));
        Assert.Contains(once.Units, u => u.Name == ContentFiles.CastName);
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
    [InlineData("fort", 'F', 1, 1, 1, 1, 15, 2, 2, 20, true)]
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

    // The iron tier's hit from DESIGN.md section 5 as kept by issue 158 (DECISIONS/0028):
    // 15 under the numbers it shipped with, so raw hits on open ground sit near 60 to 80.
    [Theory]
    [InlineData("iron_sword", 75)]
    [InlineData("iron_lance", 70)]
    [InlineData("iron_axe", 65)]
    [InlineData("iron_bow", 70)]
    public void TheIronTierHitIsTheKeptNumbers(string id, int hit)
    {
        Assert.Equal(hit, Content.Weapons[id].Hit);
    }

    // Issue 70's lean: low Mt, high Hit, low Wt, and no crit, since four strikes at any
    // crit fish for the boss (the dial the issue names is crit, not the strike count).
    [Theory]
    [InlineData("iron_gauntlets", 2, 85, 0, 2, 40)]
    [InlineData("steel_gauntlets", 4, 80, 0, 5, 30)]
    public void GauntletsAreLowMightHighHitLowWeightAndNoCrit(string id, int mt, int hit, int crit, int wt, int durability)
    {
        var weapon = Content.Weapons[id];

        Assert.Equal(WeaponType.Gauntlet, weapon.Type);
        Assert.Equal((mt, hit, crit, wt, durability), (weapon.Mt, weapon.Hit, weapon.Crit, weapon.Wt, weapon.Durability));
        Assert.Equal((1, 1), (weapon.MinRange, weapon.MaxRange));
    }

    /// <summary>
    /// Issue 70: two iron-gauntlet strikes out-damage one iron-sword strike only while the
    /// target's Def is under the wielder's Str less one, so gauntlets lose to armour.
    /// </summary>
    [Theory]
    [InlineData(7, 3, true)]
    [InlineData(7, 5, true)]
    [InlineData(7, 6, false)]
    [InlineData(7, 8, false)]
    [InlineData(5, 3, true)]
    [InlineData(5, 4, false)]
    public void TwoIronGauntletStrikesBeatOneIronSwordStrikeOnlyBelowStrLessOneDef(int str, int def, bool gauntletsAhead)
    {
        var fists = 2 * Math.Max(0, str + Content.Weapons["iron_gauntlets"].Mt - def);
        var sword = Math.Max(0, str + Content.Weapons["iron_sword"].Mt - def);

        Assert.Equal(gauntletsAhead, fists > sword);
    }

    /// <summary>Issue 70: the reaver wields gauntlets beside the axe, and Keziah, the harbour brawler, carries both.</summary>
    [Fact]
    public void KeziahCarriesAnAxeAndGauntlets()
    {
        Assert.True(Content.Class("reaver").CanUse(WeaponType.Gauntlet));
        Assert.Equal(new[] { "iron_axe", "iron_gauntlets" }, Content.Cast.Single(u => u.Id == "keziah").Inventory.Items.Select(s => s.ItemId));
    }

    [Fact]
    public void ClassesCoverEveryMovementTypeAndEveryWeaponType()
    {
        foreach (var movement in Enum.GetValues<MovementType>())
        {
            Assert.Contains(Content.Classes.Values, c => c.Movement == movement);
        }

        foreach (var weaponType in ShippedWeaponTypes)
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
                _ => (1, 2),
            };
            Assert.True(weapon.MinRange >= min && weapon.MaxRange <= max, weapon.Id + " range is outside its type's band");
        }
    }

    /// <summary>Section 5: a melee weapon that reaches 2 is thrown and enemy-only in v1, so the player's melee stays at 1.</summary>
    [Fact]
    public void AThrownMeleeWeaponIsCarriedByNoCastMember()
    {
        var thrown = Content.Weapons.Values
            .Where(w => w.Type is WeaponType.Sword or WeaponType.Lance or WeaponType.Axe && w.MaxRange > 1)
            .Select(w => w.Id)
            .ToList();
        Assert.Equal(new[] { "toll_axe", "toll_spear" }, thrown.Order());
        foreach (var unit in Content.Cast)
        {
            Assert.DoesNotContain(unit.Inventory.Items, item => thrown.Contains(item.ItemId));
        }

        Assert.Contains(Content.Unit("bandit_leader").Inventory.Items, item => item.ItemId == "toll_axe");
        Assert.Contains(Content.Unit("toll_warden").Inventory.Items, item => item.ItemId == "toll_spear");
        Assert.Contains(Content.Unit("toll_brigand").Inventory.Items, item => item.ItemId == "toll_axe");
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
