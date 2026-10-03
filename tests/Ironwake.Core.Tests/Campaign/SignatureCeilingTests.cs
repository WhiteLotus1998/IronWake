using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// The Sim's ceiling on signature items (issue 635, slice 3; DESIGN section 14): at most 15 percent
/// over the best shop weapon of the item's type and rank in damage per combat, and each signature
/// art loses to the item's plain attack somewhere. No item ships yet (the story gate, #656), so
/// these tests add placeholder entries bound to Wren.
/// </summary>
public class SignatureCeilingTests
{
    private static readonly GameContent Shipped = MapFixture.Content;

    private static Weapon Bound(string id, Weapon source) => source with { Id = id, Name = id, Price = null, BoundTo = "wren" };

    private static GameContent With(Weapon item, params Ability[] arts) => Shipped with
    {
        Weapons = Shipped.Weapons.Add(item.Id, item),
        Abilities = arts.Aggregate(Shipped.Abilities, (all, art) => all.Add(art.Id, art)),
    };

    private static Ability Art(string id, string item, int mt, int hit) =>
        new(id, id, "Placeholder signature art.", new CombatArtEffect(WeaponType.Sword, WeaponRank.E, 1, mt, hit, 0, 0, 0) { Item = item });

    private static CeilingReading Read(GameContent content, string id) =>
        SignatureCeiling.Read(content, content.Weapon(id), RollScheme.TwoRollAverage);

    [Fact]
    public void TheShopIsTheStockedWeaponsOfTheItemsTypeAndRank()
    {
        var item = Bound("test_vow", Shipped.Weapon("iron_sword"));

        var shop = SignatureCeiling.Shop(With(item), item).Select(w => w.Id).ToList();

        Assert.Equal(new[] { "iron_sword", "steel_sword" }, shop);
    }

    [Fact]
    public void ThePricedWeaponsAreTheShopWhenNoCampaignMapStocksTheRank()
    {
        var item = Bound("test_vow", Shipped.Weapon("iron_sword"));
        var content = With(item) with { Campaign = Shipped.Campaign with { Maps = ValueList<CampaignMap>.Empty } };

        var shop = SignatureCeiling.Shop(content, item).Select(w => w.Id).ToList();

        Assert.Equal(new[] { "iron_sword", "steel_sword" }, shop);
    }

    [Fact]
    public void ARankEItemIsHeldToTheShopUpToRankDAndNoHigher()
    {
        var item = Bound("test_vow", Shipped.Weapon("iron_sword"));

        var shop = SignatureCeiling.Shop(With(item), item).Select(w => (w.Id, w.Rank)).ToList();

        Assert.Equal(WeaponRank.D, SignatureCeiling.FloorRank);
        Assert.Contains(("steel_sword", WeaponRank.D), shop);
        Assert.DoesNotContain(shop, w => w.Rank > WeaponRank.D);
    }

    [Fact]
    public void AnItemAboveTheFloorIsHeldToItsOwnRankAlone()
    {
        var item = Bound("test_vow", Shipped.Weapon("ridgeblade"));
        var content = With(item) with { Campaign = Shipped.Campaign with { Maps = ValueList<CampaignMap>.Empty } };

        var shop = SignatureCeiling.Shop(content, item).Select(w => w.Id).ToList();

        Assert.Equal(new[] { "ridgeblade" }, shop);
    }

    [Fact]
    public void TheTargetsAreEveryUnitOutsideTheCastWithAWeaponThatStrikes()
    {
        var targets = SignatureCeiling.Targets(Shipped);

        Assert.NotEmpty(targets);
        Assert.DoesNotContain(targets, t => Shipped.Cast.Any(c => c.Id == t.Unit.Id));
        Assert.All(targets, t => Assert.False(t.Weapon.Heals));
    }

    [Fact]
    public void AnItemThatCopiesTheBestShopWeaponReadsOneAndPasses()
    {
        var plain = Read(With(Bound("test_vow", Shipped.Weapon("iron_sword"))), "test_vow");
        var best = plain.Comparator!;

        var copy = Read(With(Bound("test_copy", best)), "test_copy");

        Assert.Equal(1.0, copy.Ratio, 6);
        Assert.True(copy.Passed);
        Assert.Equal(best.Id, copy.Comparator!.Id);
    }

    [Fact]
    public void AnItemMoreThanFifteenPercentOverTheBestShopWeaponFailsTheCeiling()
    {
        var best = Read(With(Bound("test_vow", Shipped.Weapon("iron_sword"))), "test_vow").Comparator!;
        var item = Bound("test_vow", best with { Mt = best.Mt + 6 });

        var reading = Read(With(item), "test_vow");

        Assert.True(reading.Ratio > SignatureCeiling.MaxRatio);
        Assert.False(reading.Passed);
        Assert.Equal($"test_vow deals {reading.Ratio:F2} of {best.Id}, over the ceiling of 1.15", reading.Failure);
    }

    [Fact]
    public void AnItemWithNoShopWeaponOfItsRankFailsNamingTheRank()
    {
        var item = Bound("test_vow", Shipped.Weapon("iron_sword") with { Rank = WeaponRank.A });

        var reading = Read(With(item), "test_vow");

        Assert.Null(reading.Comparator);
        Assert.Equal("the shop sells no sword of rank A to hold test_vow to", reading.Failure);
    }

    [Fact]
    public void AnItemBoundToNobodyInTheCastFails()
    {
        var item = Shipped.Weapon("iron_sword") with { Id = "test_vow", Price = null, BoundTo = "nobody" };

        var reading = Read(With(item), "test_vow");

        Assert.Equal("test_vow is bound to no cast member", reading.Failure);
    }

    [Fact]
    public void AnItemItsOwnerCannotWieldFails()
    {
        var item = Bound("test_tome", Shipped.Weapon("cinder"));

        var reading = Read(With(item), "test_tome");

        Assert.False(reading.Passed);
        Assert.StartsWith("wren cannot wield test_tome", reading.Failure);
    }

    [Fact]
    public void ASignatureArtThatNeverLosesToThePlainAttackFails()
    {
        var item = Bound("test_vow", Shipped.Weapon("iron_sword"));

        var reading = Read(With(item, Art("test_oath", "test_vow", mt: 2, hit: 0)), "test_vow");

        Assert.Equal(0, Assert.Single(reading.Arts).LosesTo);
        Assert.Equal("test_oath never loses to test_vow's plain attack", reading.Failure);
    }

    [Fact]
    public void ASignatureArtThatLosesToThePlainAttackSomewherePasses()
    {
        var item = Bound("test_vow", Shipped.Weapon("iron_sword"));

        var reading = Read(With(item, Art("test_oath", "test_vow", mt: 1, hit: -50)), "test_vow");

        var art = Assert.Single(reading.Arts);
        Assert.True(art.LosesTo > 0);
        Assert.True(reading.Passed);
    }

    [Fact]
    public void DamagePerCombatIsHitTimesDamageTimesTheCritWeightTimesTheStrikes()
    {
        var wren = Shipped.Cast.Single(u => u.Id == "wren");
        var sword = Shipped.Weapon("iron_sword");
        var target = SignatureCeiling.Targets(Shipped)[0];
        var plain = Shipped.Terrain[SignatureCeiling.TerrainId];
        var side = Ironwake.Core.Combat.Forecast(
            Shipped.CombatantOf(wren, sword, plain, Shipped.StatsOf(wren).Hp),
            Shipped.CombatantOf(target.Unit, target.Weapon, plain, Shipped.StatsOf(target.Unit).Hp),
            1,
            RollScheme.OneRoll).Attacker;

        var damage = SignatureCeiling.DamagePerCombat(Shipped, wren, sword, target, RollScheme.OneRoll);

        Assert.Equal(side.Damage * (side.HitChance / 100.0) * (1 + 2 * side.CritChance / 100.0) * side.StrikeCount, damage, 9);
    }

    [Fact]
    public void TheSmokeRowPassesWhenNoSignatureItemShips()
    {
        var row = Ironwake.Sim.Program.CeilingGate(Ironwake.Content.ContentLoader.Parse(Ironwake.Core.Tests.Content.Fixture.Files()));

        Assert.True(row.Passed);
        Assert.Equal("signature ceiling: no signature item ships: ok", row.Line);
    }

    [Fact]
    public void TheSmokeRowPassesOnTheShippedHeirloomAtItsLastStageAndEveryPaidSignature()
    {
        var row = Ironwake.Sim.Program.CeilingGate(Shipped);

        Assert.True(row.Passed);
        Assert.Equal("signature ceiling: 4 items, highest 1.14 of 1.15: ok", row.Line);
    }

    [Fact]
    public void TheSmokeRowFailsOnAnItemOverTheCeiling()
    {
        var best = Read(With(Bound("test_vow", Shipped.Weapon("iron_sword"))), "test_vow").Comparator!;
        var content = With(Bound("test_vow", best with { Mt = best.Mt + 6 }));

        var row = Ironwake.Sim.Program.CeilingGate(content);

        Assert.False(row.Passed);
        Assert.StartsWith("signature ceiling: test_vow deals ", row.Line);
        Assert.EndsWith(": FAILED", row.Line);
    }
    [Fact]
    public void AHealingItemIsHeldToTheBestStockedHealAtOrBelowItsRank()
    {
        var psalter = Shipped.Weapon("maud_psalter");

        Assert.Equal(new[] { "salve" }, SignatureCeiling.HealShop(Shipped, psalter).Select(w => w.Id));
        Assert.Contains("beacon", SignatureCeiling.HealShop(Shipped, psalter with { Rank = WeaponRank.C }).Select(w => w.Id));
    }

    [Fact]
    public void TheHealArmReadsHpPerCastTimesUsesAgainstTheComparator()
    {
        var maud = Shipped.Cast.Single(u => u.Id == "maud");
        var reading = Read(Shipped, "maud_psalter");

        var perCast = Ironwake.Core.Combat.Heal(Shipped.CombatantOf(maud, Shipped.Weapon("salve"), Shipped.Terrain["plain"], Shipped.StatsOf(maud).Hp), Shipped.Weapon("salve"));
        Assert.Equal(perCast * 8.0, SignatureCeiling.Restored(Shipped, maud, Shipped.Weapon("salve")));
        Assert.Equal(perCast * 4.0, SignatureCeiling.Restored(Shipped, maud, Shipped.Weapon("maud_psalter")));
        Assert.Equal("salve", reading.Comparator!.Id);
        Assert.Equal(0.5, reading.Ratio, 3);
        Assert.True(reading.Passed);
    }

    [Fact]
    public void AHealingItemOverTheCeilingFails()
    {
        var content = Shipped with { Weapons = Shipped.Weapons.SetItem("maud_psalter", Shipped.Weapon("maud_psalter") with { Durability = 10 }) };

        var reading = Read(content, "maud_psalter");

        Assert.False(reading.Passed);
        Assert.Contains("restores 1.25 of salve, over the ceiling", reading.Failure);
    }

    [Fact]
    public void AHealingItemWithNoShopHealFailsNamingTheRank()
    {
        var content = Shipped with { Weapons = Shipped.Weapons.SetItem("maud_psalter", Shipped.Weapon("maud_psalter") with { Type = WeaponType.Reason }) };
        content = content with { Abilities = content.Abilities.Remove("unasked") };

        var reading = Read(content, "maud_psalter");

        Assert.False(reading.Passed);
    }

    [Fact]
    public void AHealArtPassesByConstruction()
    {
        var reading = Read(Shipped, "maud_psalter");

        var art = Assert.Single(reading.Arts);
        Assert.Equal("unasked", art.ArtId);
        Assert.True(art.ByConstruction && art.Passed);
    }
}
