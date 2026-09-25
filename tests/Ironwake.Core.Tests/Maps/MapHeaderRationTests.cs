using Ironwake.Content;

namespace Ironwake.Core.Tests.Maps;

/// <summary>
/// The <c>ration:</c> header (issue 160, DESIGN.md section 10; DECISIONS/0039): each deployed
/// player unit's stack of a rationed item is clamped to the ration's uses when the map starts.
/// A clamp and never a set, player units only, and every malformed entry refused naming the line.
/// </summary>
public class MapHeaderRationTests
{
    private static string Rationed(string header) =>
        MapFixture.OldMillRoad.Replace("enemy_level: 1\n", "enemy_level: 1\n" + header + "\n");

    private static int UsesOf(Unit unit, string itemId) =>
        unit.Inventory.Items.Where(i => i.ItemId == itemId).Select(i => i.Uses).DefaultIfEmpty(0).Single();

    [Fact]
    public void TheRationHeaderRoundTripsInCanonicalOrder()
    {
        var text = Rationed("ration: field_dressing:1");

        var map = MapFixture.Parse(text);

        Assert.Equal(ValueList<ItemStack>.Of(new ItemStack("field_dressing", 1)), map.Rations);
        Assert.Equal(text, MapFormat.Write(map, MapFixture.Content));
        Assert.Empty(MapFixture.Parse(MapFixture.OldMillRoad).Rations);
    }

    [Fact]
    public void ARationCapsEveryDeployedPlayerUnitsStackAtTheMapsStart()
    {
        var map = MapFixture.Parse(Rationed("ration: field_dressing:1"));

        var state = BattleState.From(map, MapFixture.Content, MapFixture.Content.Cast, 1);

        Assert.Equal(1, UsesOf(state.Find("captain")!.Unit, "field_dressing"));
        Assert.Equal(1, UsesOf(state.Find("wren")!.Unit, "field_dressing"));
        Assert.Equal(3, UsesOf(MapFixture.Content.Cast[0], "field_dressing"));
    }

    [Fact]
    public void AMapWithoutARationIssuesWhatTheUnitCarries()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);

        var state = BattleState.From(map, MapFixture.Content, MapFixture.Content.Cast, 1);

        Assert.Equal(3, UsesOf(state.Find("captain")!.Unit, "field_dressing"));
    }

    [Fact]
    public void ARationIsAClampAndNeverRaisesAStack()
    {
        var map = MapFixture.Parse(Rationed("ration: field_dressing:2"));
        var carried = MapFixture.Content.Cast[0];
        var low = carried with { Inventory = carried.Inventory.Replace(1, new ItemStack("field_dressing", 1)) };

        Assert.Equal(1, UsesOf(map.Rationed(low), "field_dressing"));
        Assert.Equal(2, UsesOf(map.Rationed(carried), "field_dressing"));
        Assert.Equal(carried.Inventory.Items[0], map.Rationed(carried).Inventory.Items[0]);
    }

    [Fact]
    public void ARationLeavesEnemiesAsTheirTemplatesCarry()
    {
        var map = MapFixture.Parse(Rationed("ration: field_dressing:1").Replace("E soldier 9,1", "E brannock 9,1"));

        var state = BattleState.From(map, MapFixture.Content, MapFixture.Content.Cast, 1);

        Assert.Equal(1, UsesOf(state.Find("captain")!.Unit, "field_dressing"));
        Assert.Equal(3, UsesOf(state.Find("brannock-1")!.Unit, "field_dressing"));
    }

    [Theory]
    [InlineData("ration: field_dressing", "ration entry 'field_dressing' must be <item>:<uses>")]
    [InlineData("ration: field_dressing:", "ration entry 'field_dressing:' must be <item>:<uses>")]
    [InlineData("ration: :1", "ration entry ':1' must be <item>:<uses>")]
    [InlineData("ration: bandage:1", "ration names 'bandage', which is not an item in items.json")]
    [InlineData("ration: field_dressing:0", "ration for 'field_dressing' must be 1..3 uses, got '0'")]
    [InlineData("ration: field_dressing:4", "ration for 'field_dressing' must be 1..3 uses, got '4'")]
    [InlineData("ration: field_dressing:-1", "ration for 'field_dressing' must be 1..3 uses, got '-1'")]
    [InlineData("ration: field_dressing:1 field_dressing:2", "ration names 'field_dressing' twice")]
    public void ABadRationIsRefusedNamingTheProblemAndTheLine(string header, string message)
    {
        var ex = Assert.Throws<MapException>(() => MapFixture.Parse(Rationed(header), "rationed.map"));

        Assert.Contains(message, ex.Message);
        Assert.StartsWith("rationed.map, line 7: ", ex.Message);
    }
}
