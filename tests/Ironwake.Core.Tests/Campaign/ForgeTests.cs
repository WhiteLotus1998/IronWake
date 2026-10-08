using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 647, the forge and Refine (DESIGN section 13.20): a keep room opened once the smith is met,
/// where a weapon is raised a step at a time for one material and a fee; the rare material the
/// campaign pays is exactly what its issued signatures need; and the camp warns on leaving when an
/// equipped weapon is low.
/// </summary>
public class ForgeTests
{
    private static readonly GameContent Shipped = MapFixture.Content;

    private static readonly Weapon Vow = Shipped.Weapon("iron_sword") with { Id = "test_vow", Name = "Test Vow", Price = null, BoundTo = "maud" };

    private static readonly Weapon Charm = Shipped.Weapon("iron_sword") with { Id = "test_charm", Name = "Test Charm", Price = null, BoundTo = "brannock" };

    /// <summary>The shipped content with Maud's Test Vow paid by her quest 2 in place of the Psalter (with its three rare) and a side character's Test Charm.</summary>
    private static readonly GameContent Content = Shipped with
    {
        Weapons = Shipped.Weapons.Add(Vow.Id, Vow).Add(Charm.Id, Charm),
        Campaign = Shipped.Campaign with
        {
            Quests = ValueList<CampaignQuest>.From(Shipped.Campaign.Quests.Select(q => q.Id == "maud_2" ? q with { Pays = "test_vow", Rare = 3 } : q)),
        },
    };

    private static int IndexOf(string id) => Content.Campaign.Maps.Select(m => m.MapId).ToList().IndexOf(id);

    /// <summary>A record after the Tollgate is won, the forge built, the captain's pack <paramref name="pack"/>, stores and purse as given.</summary>
    private static CampaignRecord AtTheForge(int common = 5, int rare = 5, int purse = 1000, params ItemStack[] pack)
    {
        var record = CampaignRecord.Start(Content, 647) with
        {
            MapIndex = IndexOf("the_tollgate") + 1,
            Rooms = ValueList<string>.Of("forge"),
            CommonMaterial = common,
            RareMaterial = rare,
            Purse = purse,
        };
        return Give(record, "captain", pack.Length > 0 ? pack : new[] { new ItemStack("iron_sword", 40) });
    }

    private static CampaignRecord Give(CampaignRecord record, string unitId, params ItemStack[] pack) =>
        record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == unitId ? u with { Inventory = new Inventory(ValueList<ItemStack>.From(pack)) } : u)) };

    [Fact]
    public void TheForgeOpensOnlyOnceTheSmithsMapIsWon()
    {
        var before = CampaignRecord.Start(Content, 1) with { MapIndex = IndexOf("the_tollgate"), Purse = 1000 };
        var after = before with { MapIndex = IndexOf("the_tollgate") + 1 };

        var refused = before.BuildRoom("forge", Content);
        var built = after.BuildRoom("forge", Content);

        Assert.False(refused.Accepted);
        Assert.Equal("Forge opens once the_tollgate is won", refused.Text);
        Assert.True(built.Accepted);
        Assert.Equal("Forge built for 600, the purse holds 400; refine <unit> <slot> mt|hit works here", built.Text);
        Assert.True(built.Record.ForgeBuilt(Content));
        Assert.Equal(after.Beds(Content), built.Record.Beds(Content));
    }

    [Fact]
    public void RefineAddsMtOrHitNeverWeight()
    {
        var record = AtTheForge();
        var sword = Content.Weapon("iron_sword");

        var mt = record.Refine("captain", 0, "mt", Content);
        var hit = mt.Record.Refine("captain", 0, "hit", Content);

        Assert.True(mt.Accepted);
        Assert.Equal($"Refine Iron Sword: Iron Sword +1, Acc {sword.Hit}, Power {sword.Mt + 1}, for 1 common material and 100; the purse holds 900", mt.Text);
        Assert.True(hit.Accepted);
        var stack = hit.Record.Find("captain")!.Inventory.Items[0];
        Assert.Equal((2, 1, 5), (stack.Refines, stack.RefineMt, stack.RefineHit));
        var shaped = Forge.Shape(sword, stack);
        Assert.Equal((sword.Mt + 1, sword.Hit + 5, sword.Wt, sword.Crit), (shaped.Mt, shaped.Hit, shaped.Wt, shaped.Crit));
        Assert.Equal((3, 5, 800), (hit.Record.CommonMaterial, hit.Record.RareMaterial, hit.Record.Purse));
    }

    [Fact]
    public void TheForgeDutyWorksOneStepForNoGoldAndPaysTheMaterial()
    {
        var record = AtTheForge();
        var sword = Content.Weapon("iron_sword");

        var worked = record.ForgeDuty("captain", 0, "mt", Content);

        Assert.True(worked.Accepted, worked.Text);
        Assert.Equal($"Alder Fenn works the forge: Iron Sword: Iron Sword +1, Acc {sword.Hit}, Power {sword.Mt + 1}, for 1 common material and no gold; the purse holds 1000", worked.Text);
        Assert.Equal((4, 1000, Duty.Forge), (worked.Record.CommonMaterial, worked.Record.Purse, worked.Record.DutyOf("captain")));
        Assert.Equal(1, worked.Record.Find("captain")!.Inventory.Items[0].Refines);
    }

    [Fact]
    public void TheForgeDutyIsOneStepAUnitACamp()
    {
        var worked = AtTheForge().ForgeDuty("captain", 0, "mt", Content).Record;

        var again = worked.ForgeDuty("captain", 0, "hit", Content);
        var paid = worked.Refine("captain", 0, "hit", Content);

        Assert.False(again.Accepted);
        Assert.Equal("Alder Fenn took the forge duty at this camp already", again.Text);
        Assert.True(paid.Accepted);
        Assert.Equal(900, paid.Record.Purse);
    }

    [Fact]
    public void TheForgeDutyIsRefusedWhereRefineIsAndForAUnitWithAnotherDuty()
    {
        var bare = CampaignRecord.Start(Content, 647) with { MapIndex = IndexOf("the_tollgate") + 1 };

        Assert.Equal("the keep has no forge; build it first", bare.ForgeDuty("captain", 0, "mt", Content).Text);
        Assert.StartsWith("Refining Iron Sword takes 1 common material", AtTheForge(common: 0).ForgeDuty("captain", 0, "mt", Content).Text);
        Assert.True(AtTheForge(purse: 0).ForgeDuty("captain", 0, "mt", Content).Accepted);
        Assert.Equal("Alder Fenn took the rest duty at this camp; one duty a unit", AtTheForge().AssignDuty("captain", "rest").Record.ForgeDuty("captain", 0, "mt", Content).Text);
        var none = AtTheForge().ForgeDuty("captain", 0, "mt", Content).Record;
        Assert.Equal(none.Duties, none.ForgeDuty("captain", 0, "mt", Content).Record.Duties);
    }

    [Fact]
    public void ARefinedWeaponStrikesWithItsSteps()
    {
        var stack = new ItemStack("iron_sword", 40) { Refines = 2, RefineMt = 2 };
        var unit = Content.Unit("captain") with { Inventory = new Inventory(ValueList<ItemStack>.Of(stack)) };
        var holder = new BattleUnit(unit, Side.Player, default, 20, false, false);

        Assert.Equal(Content.Weapon("iron_sword").Mt + 2, holder.EquippedWeapon(Content)!.Mt);
        Assert.Equal(Content.Weapon("iron_sword").Mt + 2, holder.UsableWeaponAt(Content, 0)!.Mt);
    }

    [Fact]
    public void AShopWeaponTakesTwoStepsOnCommon()
    {
        var twice = AtTheForge(pack: new ItemStack("iron_sword", 40) { Refines = 2, RefineMt = 2 });

        var third = twice.Refine("captain", 0, "hit", Content);

        Assert.False(third.Accepted);
        Assert.Equal("Iron Sword +2 is Refined 2 of 2", third.Text);
    }

    [Fact]
    public void AMainLineSignatureTakesThreeStepsOnRare()
    {
        var record = AtTheForge(common: 0, rare: 3, pack: new ItemStack("test_vow", Vow.Durability) { Refines = 2, RefineMt = 2 });

        var third = record.Refine("captain", 0, "mt", Content);
        var fourth = third.Record.Refine("captain", 0, "mt", Content);

        Assert.True(third.Accepted);
        Assert.Contains("for 1 frozen iron", third.Text);
        Assert.Equal(2, third.Record.RareMaterial);
        Assert.Equal("Test Vow +3 is Refined 3 of 3", fourth.Text);
    }

    [Fact]
    public void ASideCharactersSignatureRefinesOnCommon()
    {
        Assert.Equal(Material.Common, Forge.MaterialFor(Charm, Content).Material);
        Assert.Equal(Material.Rare, Forge.MaterialFor(Vow, Content).Material);
        Assert.Equal(Material.Common, Forge.MaterialFor(Content.Weapon("iron_lance"), Content).Material);
    }

    [Fact]
    public void KinsbaneAHealingSpellAndAnUnsoldWeaponNeverRefine()
    {
        var record = AtTheForge(pack: new[] { new ItemStack("kinsbane", 20), new ItemStack("salve", 8) });

        Assert.Equal("Kinsbane is never Refined; it grows on what it is fed", record.Refine("captain", 0, "mt", Content).Text);
        Assert.Equal("Salve is a healing spell; the forge has nothing to raise", record.Refine("captain", 1, "hit", Content).Text);
        Assert.Equal("Rusty Thing is no shop's weapon; the smith will not work it", Forge.MaterialFor(Content.Weapon("iron_sword") with { Name = "Rusty Thing", Price = null }, Content).Refusal);
    }

    /// <summary>Issue 702: obsidian is glass, never Refined and never repaired, and the smith says why in one line.</summary>
    [Fact]
    public void AGlassWeaponIsNeverRefinedOrRepairedAndTheSmithSaysWhy()
    {
        var record = AtTheForge(pack: new ItemStack("obsidian_sword", 4));
        var line = $"the smith: \"{Weapon.GlassRefusal}\"";

        Assert.Equal("the smith: \"You don't mend glass. You buy another.\"", line);
        var refine = record.Refine("captain", 0, "mt", Content);
        Assert.False(refine.Accepted);
        Assert.Equal(line, refine.Text);
        var repair = record.Repair("captain", 0, Content);
        Assert.False(repair.Accepted);
        Assert.Equal(line, repair.Text);
        Assert.Null(CampaignRules.RepairPricePerUse(Content.Weapon("obsidian_sword")));
        Assert.True(record.Repair("captain", 0, Content with { Weapons = Content.Weapons.SetItem("obsidian_sword", Content.Weapon("obsidian_sword") with { Glass = false }) }).Accepted);
    }

    /// <summary>Issue 702: the rare material reads frozen iron on screen; the id stays.</summary>
    [Fact]
    public void TheRareMaterialIsNamedFrozenIron()
    {
        Assert.Equal("frozen iron", Forge.Label(Material.Rare));
        Assert.Equal("common material", Forge.Label(Material.Common));
    }

    [Fact]
    public void TheSmithRefusesARustedHeirloomAndWorksItOnlyWoken()
    {
        var lance = Content.Weapon("family_lance");
        var last = lance.Heirloom!.Turns.Count;
        var rusted = AtTheForge(pack: new ItemStack("family_lance", 40));
        var sound = AtTheForge(pack: new ItemStack("family_lance", 40) { Stage = last - 1 });
        var woken = AtTheForge(pack: new ItemStack("family_lance", 40) { Stage = last });

        Assert.Equal($"the smith: \"{Heirloom.SmithRefusal}\"", rusted.Refine("captain", 0, "mt", Content).Text);
        Assert.Equal("Family Lance is not yet woken; the smith works it only then", sound.Refine("captain", 0, "mt", Content).Text);
        var refined = woken.Refine("captain", 0, "mt", Content);
        Assert.True(refined.Accepted);
        var stack = refined.Record.Find("captain")!.Inventory.Items[0];
        Assert.Equal(Heirloom.Last(lance).Mt + 1, Forge.Shape(Heirloom.Shape(lance, stack), stack).Mt);
    }

    [Fact]
    public void RefineIsRefusedWithoutAForgeShortStoresOrPurseAndOffTheSteps()
    {
        var forge = AtTheForge();

        Assert.Equal("the keep has no forge; build it first", (forge with { Rooms = ValueList<string>.Empty }).Refine("captain", 0, "mt", Content).Text);
        Assert.Equal("'wt' is not a step; Refine adds mt or hit, never weight", forge.Refine("captain", 0, "wt", Content).Text);
        Assert.Equal("Refining Iron Sword takes 1 common material and the stores hold none", AtTheForge(common: 0).Refine("captain", 0, "mt", Content).Text);
        Assert.Equal("Refining Iron Sword costs 100 and the purse holds 99", AtTheForge(purse: 99).Refine("captain", 0, "mt", Content).Text);
        Assert.Equal("captain has no item in slot 2", forge.Refine("captain", 1, "mt", Content).Text);
        Assert.Equal("'nobody' is not on the roster", forge.Refine("nobody", 0, "mt", Content).Text);
        Assert.Equal("Field Dressing is not a weapon; nothing to Refine", AtTheForge(pack: new ItemStack("field_dressing", 3)).Refine("captain", 0, "mt", Content).Text);
    }

    [Fact]
    public void RefineAndTheStoresRoundTripThroughTheRecord()
    {
        var record = AtTheForge(common: 2, rare: 1).Refine("captain", 0, "hit", Content).Record;

        var read = ProtocolJson.ReadCampaign(ProtocolJson.Campaign(record), Content);

        Assert.Equal((1, 1), (read.CommonMaterial, read.RareMaterial));
        Assert.Equal(record.Find("captain")!.Inventory.Items[0], read.Find("captain")!.Inventory.Items[0]);
    }

    [Fact]
    public void ThePackPrintsTheSteps()
    {
        var record = AtTheForge(pack: new ItemStack("iron_sword", 40) { Refines = 2, RefineMt = 1, RefineHit = 5 });

        Assert.Contains("1: Iron Sword +2 40/", CampaignSession.UnitLines(record, Content, record.Find("captain")!, detail: false)[0]);
    }

    [Fact]
    public void TheShippedCampaignIssuesThePsalterTheCommonplaceAndTheTallyAndNamesTheLanceAndPaysRareForTheThreeThatRefine()
    {
        Assert.Equal(new[] { "maud_psalter", "pell_commonplace", "ottilie_tally" }, Shipped.Campaign.Quests.Select(q => q.Pays).OfType<string>());
        Assert.Equal(new[] { "family_lance" }, Shipped.Campaign.Quests.Select(q => q.Names).OfType<string>());
        Assert.Equal(9, Forge.RareNeeded(Shipped));
        Assert.Equal(9, Forge.RarePaid(Shipped));
        Assert.Equal(3, Shipped.Campaign.Quest("teodor_2")!.Rare);
        Assert.Equal(3, Shipped.Campaign.Quest("pell_2")!.Rare);
        Assert.Equal(3, Shipped.Campaign.Quest("ottilie_2")!.Rare);
        Assert.Null(Forge.RareRefusal(Shipped));
        Assert.Equal(new ForgeRules(1, 5, 100, 2, 3), Shipped.Campaign.Forge);
    }

    [Fact]
    public void TheValidatorHoldsRareMaterialToExactlyEnough()
    {
        Assert.Null(Forge.RareRefusal(Content));
        var reloaded = ContentLoader.Parse(ContentSerializer.Write(Content));
        Assert.Equal(3, reloaded.Campaign.Quest("maud_2")!.Rare);
        Assert.Equal(Shipped.Campaign.Forge, reloaded.Campaign.Forge);
        Assert.Equal(Shipped.Campaign.Keep.Room("forge"), reloaded.Campaign.Keep.Room("forge"));
    }

    [Theory]
    [InlineData(2, "pays 11 frozen iron and the signatures the campaign issues need 12 to Refine fully; 1 short")]
    [InlineData(4, "pays 13 frozen iron and the signatures the campaign issues need 12; 1 over, which nothing can spend")]
    public void TheValidatorFiresOnOneShortAndOneOver(int rare, string problem)
    {
        var quests = Content.Campaign.Quests.Select(q => q.Id == "maud_2" ? q with { Rare = rare } : q);
        var content = Content with { Campaign = Content.Campaign with { Quests = ValueList<CampaignQuest>.From(quests) } };

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(ContentSerializer.Write(content)));

        Assert.Equal((ContentFiles.CampaignName, "quests", "rare"), (e.File, e.Entry, e.Field));
        Assert.Contains(problem, e.Message);
    }

    [Fact]
    public void AForgeRoomNeedsTheForgeNumbers()
    {
        var content = Shipped with { Campaign = Shipped.Campaign with { Forge = ForgeRules.None } };

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(ContentSerializer.Write(content)));

        Assert.Equal("keep.rooms.forge.forge", e.Field);
    }

    [Fact]
    public void TheLeaveWarningFiresAtFourUsesAndIsSilentAtFive()
    {
        var four = AtTheForge(pack: new ItemStack("iron_sword", 4));
        var five = AtTheForge(pack: new ItemStack("iron_sword", 5));

        Assert.Equal(new[] { "low: Alder Fenn's Iron Sword has 4 uses" }, CampaignSession.LowLines(four, Content).Where(l => l.Contains("Alder")));
        Assert.DoesNotContain(CampaignSession.LowLines(five, Content), l => l.Contains("Alder"));
        Assert.DoesNotContain(CampaignSession.LowLines(four with { Benched = ValueList<string>.Of("captain") }, Content), l => l.Contains("Alder"));
    }
}

/// <summary>
/// Code's journaled forge play (issue 647, seed 701): the wounded Tollgate run, then at the Harrow
/// Weir camp the forge, one step each for Teodor's lance and the captain's sword, and a bunk room,
/// leaving 300 of 1500 for the next map.
/// </summary>
[Collection("console")]
public class ForgeTranscriptTests
{
    [Fact]
    public void TheJournaledForgePlayReplaysToItsTranscript()
    {
        var transcripts = Path.Combine(Directory.GetParent(Ironwake.Core.Tests.Content.Fixture.RealContentDirectory())!.FullName, "docs", "transcripts");
        var script = Path.Combine(transcripts, "2026-10-01-campaign-647-forge.script");
        var args = new[] { "campaign", "--from", "the_tollgate", "--seed", "701", "--permadeath", "off", "--script", script, "--strict", "--content", Ironwake.Core.Tests.Content.Fixture.CurveFreeContentDirectory() };

        var output = ConsoleCapture.Run(() => Ironwake.Cli.Program.Main(args));

        Assert.Contains("Maud wins maud_1; the stores take 2 common material; fell and came back wounded: Wren\n", output);
        Assert.Contains("Refine Iron Lance: Iron Lance +1, Acc 70, Power 7, for 1 common material and 100; the purse holds 800\n", output);
        Assert.Contains("  Teodor: Pikeman L2, EXP 54; 1: Iron Lance +1 38/40", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }
}
