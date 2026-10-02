using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Issue 702, the weapon ladder: iron at E, steel at D, the specialty weapons at C, obsidian at B,
/// story and enemy weapons kept at E; obsidian is glass, sold from the raid on.
/// </summary>
public class WeaponLadderTests
{
    private static readonly GameContent Shipped = MapFixture.Content;

    [Theory]
    [InlineData("iron_sword", WeaponRank.E)]
    [InlineData("iron_lance", WeaponRank.E)]
    [InlineData("iron_axe", WeaponRank.E)]
    [InlineData("hatchet", WeaponRank.E)]
    [InlineData("iron_bow", WeaponRank.E)]
    [InlineData("iron_gauntlets", WeaponRank.E)]
    [InlineData("cinder", WeaponRank.E)]
    [InlineData("gust", WeaponRank.E)]
    [InlineData("salve", WeaponRank.E)]
    [InlineData("steel_sword", WeaponRank.D)]
    [InlineData("steel_lance", WeaponRank.D)]
    [InlineData("steel_axe", WeaponRank.D)]
    [InlineData("steel_bow", WeaponRank.D)]
    [InlineData("steel_gauntlets", WeaponRank.D)]
    [InlineData("bolt", WeaponRank.D)]
    [InlineData("radiance", WeaponRank.D)]
    [InlineData("ridgeblade", WeaponRank.C)]
    [InlineData("longpike", WeaponRank.C)]
    [InlineData("beacon", WeaponRank.C)]
    [InlineData("obsidian_sword", WeaponRank.B)]
    [InlineData("obsidian_spear", WeaponRank.B)]
    [InlineData("obsidian_axe", WeaponRank.B)]
    [InlineData("obsidian_bow", WeaponRank.B)]
    [InlineData("toll_spear", WeaponRank.E)]
    [InlineData("toll_axe", WeaponRank.E)]
    [InlineData("post_maul", WeaponRank.E)]
    [InlineData("kinsbane", WeaponRank.E)]
    [InlineData("family_lance", WeaponRank.E)]
    public void EveryWeaponSitsOnItsRungOfTheLadder(string id, WeaponRank rank)
    {
        Assert.Equal(rank, Shipped.Weapon(id).Rank);
    }

    [Fact]
    public void TheLadderNamesEveryShippedWeapon()
    {
        var named = typeof(WeaponLadderTests).GetMethod(nameof(EveryWeaponSitsOnItsRungOfTheLadder))!
            .GetCustomAttributes(typeof(InlineDataAttribute), false).Cast<InlineDataAttribute>()
            .Select(a => (string)a.GetData(null!).First()[0]).OrderBy(id => id, StringComparer.Ordinal);

        Assert.Equal(Shipped.Weapons.Keys.OrderBy(id => id, StringComparer.Ordinal), named);
    }

    [Theory]
    [InlineData("obsidian_sword", "steel_sword", "iron_sword")]
    [InlineData("obsidian_spear", "steel_lance", "iron_lance")]
    [InlineData("obsidian_axe", "steel_axe", "iron_axe")]
    [InlineData("obsidian_bow", "steel_bow", "iron_bow")]
    public void ObsidianIsSteelsPowerPlusTwoAtIronsWeightGlassWithEightUses(string id, string steelId, string ironId)
    {
        var obsidian = Shipped.Weapon(id);
        var steel = Shipped.Weapon(steelId);

        Assert.Equal(steel.Type, obsidian.Type);
        Assert.Equal(steel.Mt + 2, obsidian.Mt);
        Assert.Equal(steel.Hit, obsidian.Hit);
        Assert.Equal(25, obsidian.Crit);
        Assert.Equal(Shipped.Weapon(ironId).Wt, obsidian.Wt);
        Assert.Equal(8, obsidian.Durability);
        Assert.Equal(steel.Price * 2, obsidian.Price);
        Assert.Equal(steel.EffectiveAgainst, obsidian.EffectiveAgainst);
        Assert.True(obsidian.Glass);
    }

    [Fact]
    public void OnlyObsidianIsGlass()
    {
        Assert.Equal(
            new[] { "obsidian_axe", "obsidian_bow", "obsidian_spear", "obsidian_sword" },
            Shipped.Weapons.Values.Where(w => w.Glass).Select(w => w.Id).OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public void ObsidianIsStockedOnlyOnTheScreensAfterTheRaid()
    {
        var maps = Shipped.Campaign.Maps;
        var raid = maps.Select(m => m.MapId).ToList().IndexOf("ironwake_raid");

        Assert.True(raid >= 0);
        for (var i = 0; i < maps.Count; i++)
        {
            var glass = maps[i].Stock.Where(id => Shipped.Weapons.TryGetValue(id, out var w) && w.Glass).ToList();
            Assert.Equal(i > raid ? 4 : 0, glass.Count);
        }
    }

    [Fact]
    public void EveryTemplateCarryingAWeaponAboveEDeclaresItsRank()
    {
        var cast = Shipped.Cast.Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        var raised = Shipped.Units.Values
            .Where(u => !cast.Contains(u.Id))
            .Where(u => u.Inventory.Items.Any(s => Shipped.Weapons.TryGetValue(s.ItemId, out var w) && w.Rank > WeaponRank.E))
            .Select(u => u.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.Equal(new[] { "acolyte", "bandit_leader", "deacon", "finale_lord", "ford_chief", "grange_reeve", "heavy_rider", "lector", "longbowman", "marauder", "postern_keeper", "sentry", "veteran", "wing_captain" }, raised);
        Assert.All(raised, id => Assert.All(Shipped.Units[id].Inventory.Items, s => Assert.True(Shipped.Units[id].CanWield(Shipped.Weapon(s.ItemId), Shipped.Class(Shipped.Units[id].ClassId)))));
    }

    [Fact]
    public void MaudStartsAtFaithDSoHerRadianceIsHers()
    {
        var maud = Shipped.Cast.Single(u => u.Id == "maud");

        Assert.Equal(WeaponRank.D, maud.Skill.Rank(WeaponType.Faith));
        Assert.True(maud.CanWield(Shipped.Weapon("radiance"), Shipped.Class(maud.ClassId)));
    }
}
