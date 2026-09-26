using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// The campaign record and the between-map screen (issue 74, DESIGN section 9): the purse, the
/// shop, repair, certification with the seal, the bench as a deployment choice, permadeath and
/// supplies across a battle, and the seed each map plays on. The shipped content's campaign, with
/// the class ladder removed so the seal's rules are read apart from it (ClassLadderTests).
/// </summary>
public class CampaignRecordTests
{
    private static readonly GameContent Content = MapFixture.WithoutLadder(MapFixture.Content);

    private static MapDefinition Map(string id) => MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), Content, id), Content);

    private static CampaignRecord Start(ulong seed = 5) => CampaignRecord.Start(Content, seed);

    private static CampaignRecord WithPurse(int purse) => Start() with { Purse = purse };

    /// <summary>A record whose next map is <paramref name="index"/> (0-based) in campaign order.</summary>
    private static CampaignRecord AtMap(int index) => Start() with { MapIndex = index };

    /// <summary>
    /// The battle <paramref name="record"/> begins, won by removing every enemy and, on a Seize map,
    /// standing the captain on the throne, or on an Escape map, every player unit on its own exit,
    /// or on a Survive map, the turn past the limit,
    /// with the opening kept as history.
    /// </summary>
    private static BattleState Won(CampaignRecord record, Func<BattleUnit, BattleUnit?>? player = null)
    {
        var map = Map(record.NextMap(Content).MapId);
        var opening = record.Begin(map, Content);
        var throne = Enumerable.Range(0, map.Width * map.Height).Select(i => new Coord(i % map.Width, i / map.Width)).FirstOrDefault(map.IsThrone);
        var units = opening.Units.Where(u => u.Side == Side.Player)
            .Select(u => u.IsCaptain && map.Win == WinCondition.Seize ? u with { At = throne } : u)
            .Select(u => player is null ? u : player(u)).OfType<BattleUnit>();
        return map.Win == WinCondition.Escape
            ? opening with { Units = ValueList<BattleUnit>.Empty, Escaped = ValueList<BattleUnit>.From(units.OrderBy(u => u.IsCaptain)), Turn = 4, History = ValueList<BattleState>.Of(opening) }
            : opening with { Units = ValueList<BattleUnit>.From(units), Turn = map.Win == WinCondition.Survive ? map.TurnLimit + 1 : 4, History = ValueList<BattleState>.Of(opening) };
    }

    [Fact]
    public void ANewCampaignStartsWithTheCastTheStartingPurseAndTheFirstMap()
    {
        var record = Start(9);

        Assert.Equal(Content.Cast, record.Roster);
        Assert.Equal(Content.Campaign.StartingPurse, record.Purse);
        Assert.Equal("old_mill_road", record.NextMap(Content).MapId);
        Assert.Equal("normal", record.Difficulty);
        Assert.Empty(record.Fallen);
    }

    [Fact]
    public void ThePurseRefusesAPurchaseItCannotAffordNamingThePriceAndTheBalance()
    {
        var result = WithPurse(399).Buy("iron_sword", "teodor", Content);

        Assert.False(result.Accepted);
        Assert.Equal("Iron Sword costs 400 and the purse holds 399", result.Text);
        Assert.Equal(399, result.Record.Purse);
    }

    [Fact]
    public void APurchaseSpendsThePriceAndAddsAFullStackToTheUnit()
    {
        var result = WithPurse(1000).Buy("field_dressing", "pell", Content);

        Assert.True(result.Accepted);
        Assert.Equal(850, result.Record.Purse);
        Assert.Equal(new ItemStack("field_dressing", 3), result.Record.Find("pell")!.Inventory.Items[^1]);
        Assert.Equal("pell buys Field Dressing for 150; the purse holds 850", result.Text);
    }

    [Fact]
    public void TheShopSellsOnlyTheNextMapsStock()
    {
        var result = WithPurse(5000).Buy("steel_sword", "wren", Content);

        Assert.False(result.Accepted);
        Assert.StartsWith("the shop does not stock 'steel_sword'; it sells iron_sword", result.Text);
        Assert.True(AtMap(2).Buy("steel_sword", "wren", Content) is { Accepted: false, Text: "Steel Sword costs 900 and the purse holds 500" });
    }

    [Fact]
    public void APurchaseIsRefusedIntoAFullInventory()
    {
        var full = new Inventory(ValueList<ItemStack>.From(Enumerable.Repeat(new ItemStack("iron_sword", 40), Inventory.Capacity)));
        var record = WithPurse(1000);
        record = record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == "wren" ? u with { Inventory = full } : u)) };

        Assert.Equal("wren carries 5 items already", record.Buy("iron_sword", "wren", Content).Text);
    }

    [Fact]
    public void RepairCostsThePricePerUseForEveryMissingUse()
    {
        var record = WithPurse(1000);
        var worn = record.Find("wren")! with { Inventory = new Inventory(ValueList<ItemStack>.Of(new ItemStack("iron_sword", 0))) };
        record = record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == "wren" ? worn : u)) };

        var result = record.Repair("wren", 0, Content);

        Assert.Equal(10, CampaignRules.RepairPricePerUse(Content.Weapon("iron_sword")));
        Assert.True(result.Accepted);
        Assert.Equal(600, result.Record.Purse);
        Assert.Equal(40, result.Record.Find("wren")!.Inventory.Items[0].Uses);
        Assert.Equal("wren's Iron Sword repaired from 0 to 40 uses for 400; the purse holds 600", result.Text);
    }

    [Fact]
    public void RepairIsRefusedForASpellAWeaponAtFullUsesAWeaponWithoutAPriceAndAShortPurse()
    {
        var record = WithPurse(5);
        var tollman = record.Find("teodor")! with { Inventory = new Inventory(ValueList<ItemStack>.Of(new ItemStack("toll_spear", 3), new ItemStack("iron_lance", 38))) };
        record = record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == "teodor" ? tollman : u)) };

        Assert.Equal("Cinder is a spell; its uses refresh every map", record.Repair("pell", 0, Content).Text);
        Assert.Equal("Iron Sword is at full uses (40)", record.Repair("wren", 0, Content).Text);
        Assert.Equal("Toll Spear cannot be repaired: no shop has ever sold one", record.Repair("teodor", 0, Content).Text);
        Assert.Equal("repairing Iron Lance costs 20 (2 uses at 10) and the purse holds 5", record.Repair("teodor", 1, Content).Text);
        Assert.Equal("Field Dressing is not a weapon; nothing to repair", record.Repair("wren", 1, Content).Text);
        Assert.Equal("wren has no slot 3; it carries 2", record.Repair("wren", 2, Content).Text);
        Assert.Null(CampaignRules.RepairPricePerUse(Content.Weapon("toll_axe")));
    }

    [Fact]
    public void CertificationPaysTheSealAndChangesTheClass()
    {
        var result = WithPurse(600).Certify("brannock", "reaver", Content);

        Assert.True(result.Accepted);
        Assert.Equal("reaver", result.Record.Find("brannock")!.ClassId);
        Assert.Equal(100, result.Record.Purse);
    }

    [Fact]
    public void CertificationIsRefusedNamingTheRequirementOrTheSealPrice()
    {
        Assert.Equal("a seal costs 500 and the purse holds 499", WithPurse(499).Certify("brannock", "reaver", Content).Text);
        Assert.Equal("brannock cannot certify as Cadet: brannock is already a Cadet", WithPurse(600).Certify("brannock", "cadet", Content).Text);
        Assert.Equal("no class 'knight'", WithPurse(600).Certify("brannock", "knight", Content).Text);
    }

    [Fact]
    public void BenchingARecruitLetsTheNextInRosterOrderFillItsBareSlot()
    {
        var saltmarsh = Map("saltmarsh_ford");
        var record = AtMap(1);

        var benched = record.Bench("ottilie", saltmarsh);

        Assert.Equal(new[] { "captain", "wren", "teodor", "ottilie" }, record.Deployment(saltmarsh, Content));
        Assert.True(benched.Accepted);
        Assert.Equal(new[] { "captain", "wren", "teodor", "pell" }, benched.Record.Deployment(saltmarsh, Content));
        Assert.Equal(new[] { "captain", "wren", "teodor", "ottilie" }, benched.Record.Unbench("ottilie").Record.Deployment(saltmarsh, Content));
    }

    [Fact]
    public void TheCaptainANamedRecruitAndABenchedUnitCannotBeBenched()
    {
        var mill = Map("old_mill_road");
        var record = Start();

        Assert.Equal("captain is the captain and leads every map", record.Bench("captain", mill).Text);
        Assert.Equal("Old Mill Road places wren by name at 2,8", record.Bench("wren", mill).Text);
        Assert.Equal("teodor is already benched", record.Bench("teodor", mill).Record.Bench("teodor", mill).Text);
        Assert.Equal("nobody is not benched", record.Unbench("nobody").Text);
    }

    [Fact]
    public void AProtectedRecruitCannotBeBenched()
    {
        var protecting = Map("the_tollgate") with { ProtectId = "wren" };

        Assert.Equal("The Tollgate must protect wren, who cannot be benched", AtMap(2).Bench("wren", protecting).Text);
    }

    [Fact]
    public void EachMapPlaysOnTheCampaignSeedPlusItsIndex()
    {
        Assert.Equal(5UL, Start(5).BattleSeed);
        Assert.Equal(7UL, (Start(5) with { MapIndex = 2 }).BattleSeed);
        Assert.Equal(7UL, AtMap(2).Begin(Map("the_tollgate"), Content).Seed);
    }

    [Fact]
    public void AWonBattlePaysTheRewardAndCarriesTheSurvivorsAsTheBattleLeftThem()
    {
        var record = Start();
        var end = Won(record, u => u.Id == "wren"
            ? u with { Unit = u.Unit with { Exp = 40, Inventory = new Inventory(ValueList<ItemStack>.Of(new ItemStack("iron_sword", 31), new ItemStack("field_dressing", 1))) } }
            : u);

        var after = record.AfterBattle(end, Content);

        Assert.Equal(1, after.MapIndex);
        Assert.Equal(record.Purse + 600, after.Purse);
        Assert.Equal(40, after.Find("wren")!.Exp);
        Assert.Equal(31, after.Find("wren")!.Inventory.Items[0].Uses);
        Assert.Equal(record.Find("teodor"), after.Find("teodor"));
        Assert.Equal(record.Roster.Select(u => u.Id), after.Roster.Select(u => u.Id));
    }

    [Fact]
    public void ADeployedUnitMissingFromTheWonBoardHasFallenAndLeavesTheRoster()
    {
        var record = Start();

        var after = record.AfterBattle(Won(record, u => u.Id == "wren" ? null : u), Content);

        Assert.Null(after.Find("wren"));
        Assert.Equal(ValueList<string>.Of("wren"), after.Fallen);
        Assert.Equal(Content.Cast.Count - 1, after.Roster.Count);
    }

    [Fact]
    public void ANamedSlotWhoseRecruitHasFallenStaysEmpty()
    {
        var record = AtMap(2) with
        {
            Roster = ValueList<Unit>.From(Content.Cast.Where(u => u.Id != "wren")),
            Fallen = ValueList<string>.Of("wren"),
        };

        var state = record.Begin(Map("the_tollgate"), Content);

        Assert.Null(state.Find("wren"));
        Assert.Equal(new[] { "captain", "teodor", "pell" }, state.UnitsOf(Side.Player).OrderBy(u => u.PlacementIndex).Select(u => u.Id));
    }

    [Fact]
    public void TheUsesASuppliesCapHeldBackAreReturnedAfterTheBattle()
    {
        var record = Start();
        var opening = record.Begin(Map("old_mill_road"), Content);
        Assert.Equal(1, opening.Find("wren")!.Unit.Inventory.Items[1].Uses);

        var spent = Won(record, u => u.Id == "wren"
            ? u with { Unit = u.Unit with { Inventory = new Inventory(ValueList<ItemStack>.Of(u.Unit.Inventory.Items[0])) } }
            : u);
        var kept = Won(record);

        Assert.Equal(new ItemStack("field_dressing", 2), record.AfterBattle(spent, Content).Find("wren")!.Inventory.Items[1]);
        Assert.Equal(new ItemStack("field_dressing", 3), record.AfterBattle(kept, Content).Find("wren")!.Inventory.Items[1]);
    }

    [Fact]
    public void OnlyAWonBattleContinuesTheCampaign()
    {
        var record = Start();
        var opening = record.Begin(Map("old_mill_road"), Content);

        var error = Assert.Throws<InvalidOperationException>(() => record.AfterBattle(opening, Content));

        Assert.Equal("only a won battle continues the campaign; a lost one ends it", error.Message);
    }

    /// <summary>Issue 269: on an Escape map a unit the captain's exit leaves behind has fallen, the answer death gets.</summary>
    [Fact]
    public void AUnitLeftBehindOnAnEscapeMapHasFallen()
    {
        var last = AtMap(Content.Campaign.Maps.Select(m => m.MapId).ToList().IndexOf("brackwater_cut"));
        Assert.Equal(WinCondition.Escape, Map(last.NextMap(Content).MapId).Win);
        var won = Won(last);
        var left = won.Escaped.First(u => !u.IsCaptain);
        var end = won with { Escaped = ValueList<BattleUnit>.From(won.Escaped.Where(u => u.Id != left.Id)), Units = ValueList<BattleUnit>.Of(left) };
        Assert.Equal(BattleResult.Won, end.Outcome.Result);

        var after = last.AfterBattle(end, Content);

        Assert.Contains(left.Id, after.Fallen);
        Assert.DoesNotContain(after.Roster, u => u.Id == left.Id);
    }

    [Fact]
    public void ACampaignIsFinishedAfterItsLastMap()
    {
        var last = AtMap(Content.Campaign.Maps.Count - 1);

        var after = last.AfterBattle(Won(last), Content);

        Assert.True(after.IsFinished(Content));
        Assert.Throws<InvalidOperationException>(() => after.NextMap(Content));
    }
}
