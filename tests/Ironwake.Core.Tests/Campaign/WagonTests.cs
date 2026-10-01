using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// The wagon (issue 679, DESIGN section 10): what a chest sends past a full pack is collected onto
/// the campaign record only if the map is won, main map or side map; a lost map drops it; the camp's
/// Keep panel lists it numbered, and <c>take &lt;unit&gt; &lt;n&gt;</c> moves an entry into a free slot at full uses.
/// </summary>
public class WagonTests
{
    private static readonly GameContent Content = MapFixture.WithoutLadder(MapFixture.Content);

    private static readonly ValueList<string> Found = ValueList<string>.Of("iron_bow", "field_dressing");

    private static MapDefinition Map(string id) => MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), Content, id), Content);

    private static MapDefinition SideMap(string id) =>
        MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), MapFiles.QuestsDirectory, id + MapFiles.Extension), Content);

    /// <summary>The battle <paramref name="record"/> begins, won by removing every enemy, with <see cref="Found"/> in its wagon.</summary>
    private static BattleState WonWithWagon(CampaignRecord record)
    {
        var map = Map(record.NextMap(Content).MapId);
        var opening = record.Begin(map, Content);
        var throne = Enumerable.Range(0, map.Width * map.Height).Select(i => new Coord(i % map.Width, i / map.Width)).FirstOrDefault(map.IsThrone);
        var units = opening.UnitsOf(Side.Player).Select(u => u.IsCaptain && map.Win == WinCondition.Seize ? u with { At = throne } : u);
        return opening with { Units = ValueList<BattleUnit>.From(units), Turn = 4, History = ValueList<BattleState>.Of(opening), Wagon = Found };
    }

    /// <summary>Maud's side map decided past its limit, with <see cref="Found"/> in its wagon and the player units <paramref name="keep"/> keeps.</summary>
    private static BattleState QuestWithWagon(CampaignRecord record, Func<BattleUnit, bool> keep)
    {
        var opening = record.BeginQuest(SideMap("the_lazar_house"), "maud_1", "wren", Content);
        var units = opening.UnitsOf(Side.Player).Where(keep);
        return opening with { Units = ValueList<BattleUnit>.From(units), Turn = opening.Map.TurnLimit + 1, History = ValueList<BattleState>.Of(opening), Wagon = Found };
    }

    [Fact]
    public void AWonMapCollectsItsWagonOntoTheRecordAfterWhatWasThere()
    {
        var record = CampaignRecord.Start(Content, 5) with { Wagon = ValueList<string>.Of("steel_sword") };
        var end = WonWithWagon(record);
        Assert.Equal(BattleResult.Won, end.Outcome.Result);

        var after = record.AfterBattle(end, Content);

        Assert.Equal(ValueList<string>.Of("steel_sword", "iron_bow", "field_dressing"), after.Wagon);
    }

    [Fact]
    public void AWonSideMapCollectsItsWagonAndALostOneDropsIt()
    {
        var record = CampaignRecord.StartAt(Content, 701, "the_tollgate");

        var won = record.AfterQuest(QuestWithWagon(record, _ => true), "maud_1", Content).Record;
        var lost = record.AfterQuest(QuestWithWagon(record, u => u.Id != "maud"), "maud_1", Content).Record;

        Assert.Equal(Found, won.Wagon);
        Assert.Empty(lost.Wagon);
    }

    [Fact]
    public void TakeMovesAWagonEntryIntoAFreeSlotAtFullUses()
    {
        var record = CampaignRecord.Start(Content, 5) with { Wagon = Found };
        var before = record.Find("wren")!.Inventory.Count;

        var result = record.TakeFromWagon("wren", 0, Content);

        Assert.True(result.Accepted, result.Text);
        Assert.Equal("wren takes Iron Bow from the wagon", result.Text);
        Assert.Equal(new ItemStack("iron_bow", Content.Weapon("iron_bow").Durability), result.Record.Find("wren")!.Inventory.Items[before]);
        Assert.Equal(ValueList<string>.Of("field_dressing"), result.Record.Wagon);
    }

    [Theory]
    [InlineData("wren", 2, "the wagon has no entry 3; it holds 2")]
    [InlineData("nobody", 0, "no unit 'nobody' on the roster")]
    public void TakeIsRefusedNamingWhy(string unitId, int index, string message)
    {
        var record = CampaignRecord.Start(Content, 5) with { Wagon = Found };

        var result = record.TakeFromWagon(unitId, index, Content);

        Assert.False(result.Accepted);
        Assert.Equal(message, result.Text);
        Assert.Equal(record, result.Record);
    }

    [Fact]
    public void TakeIsRefusedFromAnEmptyWagonAndIntoAFullPack()
    {
        var record = CampaignRecord.Start(Content, 5);
        Assert.Equal("the wagon is empty", record.TakeFromWagon("wren", 0, Content).Text);

        var wren = record.Find("wren")!;
        var full = wren with { Inventory = Enumerable.Range(wren.Inventory.Count, Inventory.Capacity - wren.Inventory.Count).Aggregate(wren.Inventory, (inv, _) => inv.Add(new ItemStack("field_dressing", 3))) };
        var packed = record with { Roster = ValueList<Unit>.From(record.Roster.Select(u => u.Id == "wren" ? full : u)), Wagon = Found };
        Assert.Equal("wren has no free slot", packed.TakeFromWagon("wren", 0, Content).Text);
    }

    [Fact]
    public void TheKeepPanelListsTheWagonNumberedAndOnlyWhenItHoldsSomething()
    {
        var record = CampaignRecord.Start(Content, 5);

        Assert.Null(CampaignSession.WagonLine(record, Content));
        Assert.Equal("Wagon: 1 Iron Bow, 2 Field Dressing (take <unit> <n>)", CampaignSession.WagonLine(record with { Wagon = Found }, Content));
    }

    [Fact]
    public void TheRecordCarriesTheWagonOnlyWhenItHoldsSomething()
    {
        var record = CampaignRecord.Start(Content, 5);
        Assert.DoesNotContain("\"wagon\"", ProtocolJson.Campaign(record));

        var json = ProtocolJson.Campaign(record with { Wagon = Found });

        Assert.Contains("\"wagon\":[\"iron_bow\",\"field_dressing\"]", json);
        Assert.Equal(Found, ProtocolJson.ReadCampaign(json, Content).Wagon);
    }

    [Fact]
    public void ARecordWhoseWagonNamesNoItemIsRefused()
    {
        var json = ProtocolJson.Campaign(CampaignRecord.Start(Content, 5) with { Wagon = ValueList<string>.Of("iron_bow") }).Replace("\"iron_bow\"]", "\"gold_crown\"]");

        var error = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCampaign(json, Content));
        Assert.Contains("field 'wagon' names 'gold_crown'", error.Message);
    }
}
