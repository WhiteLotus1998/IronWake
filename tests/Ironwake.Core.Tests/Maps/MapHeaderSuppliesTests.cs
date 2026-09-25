using Ironwake.Content;

namespace Ironwake.Core.Tests.Maps;

/// <summary>
/// The <c>supplies:</c> header (issue 160, DESIGN.md section 10; DECISIONS/0039): every
/// consumable stack a deployed player unit carries is capped at N uses when the map starts.
/// A cap and never a set, consumables only, player units only, and a bad value refused
/// naming the line.
/// </summary>
public class MapHeaderSuppliesTests
{
    private static string Supplied(string header) =>
        MapFixture.OldMillRoad.Replace("enemy_level: 1\n", "enemy_level: 1\n" + header + "\n");

    private static int UsesOf(Unit unit, string itemId) =>
        unit.Inventory.Items.Where(i => i.ItemId == itemId).Select(i => i.Uses).DefaultIfEmpty(0).Single();

    [Fact]
    public void TheSuppliesHeaderRoundTripsInCanonicalOrder()
    {
        var text = Supplied("supplies: 1");

        var map = MapFixture.Parse(text);

        Assert.Equal(1, map.Supplies);
        Assert.Equal(text, MapFormat.Write(map, MapFixture.Content));
        Assert.Null(MapFixture.Parse(MapFixture.OldMillRoad).Supplies);
    }

    [Fact]
    public void SuppliesCapEveryDeployedPlayerUnitsConsumablesAtTheMapsStart()
    {
        var map = MapFixture.Parse(Supplied("supplies: 1"));

        var state = BattleState.From(map, MapFixture.Content, MapFixture.Content.Cast, 1);

        Assert.Equal(1, UsesOf(state.Find("captain")!.Unit, "field_dressing"));
        Assert.Equal(1, UsesOf(state.Find("wren")!.Unit, "field_dressing"));
        Assert.Equal(3, UsesOf(MapFixture.Content.Cast[0], "field_dressing"));
    }

    [Fact]
    public void AMapWithoutSuppliesIssuesWhatTheUnitCarries()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);

        var state = BattleState.From(map, MapFixture.Content, MapFixture.Content.Cast, 1);

        Assert.Equal(3, UsesOf(state.Find("captain")!.Unit, "field_dressing"));
    }

    [Fact]
    public void SuppliesAreACapAndNeverRaiseAStack()
    {
        var map = MapFixture.Parse(Supplied("supplies: 2"));
        var carried = MapFixture.Content.Cast[0];
        var low = carried with { Inventory = carried.Inventory.Replace(1, new ItemStack("field_dressing", 1)) };

        Assert.Equal(1, UsesOf(map.Supplied(low, MapFixture.Content), "field_dressing"));
        Assert.Equal(2, UsesOf(map.Supplied(carried, MapFixture.Content), "field_dressing"));
    }

    [Fact]
    public void SuppliesLeaveWeaponsAlone()
    {
        var map = MapFixture.Parse(Supplied("supplies: 1"));
        var carried = MapFixture.Content.Cast[0];

        Assert.Equal(carried.Inventory.Items[0], map.Supplied(carried, MapFixture.Content).Inventory.Items[0]);
        Assert.True(carried.Inventory.Items[0].Uses > 1);
    }

    [Fact]
    public void SuppliesLeaveEnemiesAsTheirTemplatesCarry()
    {
        var map = MapFixture.Parse(Supplied("supplies: 1").Replace("E soldier 9,1", "E brannock 9,1"));

        var state = BattleState.From(map, MapFixture.Content, MapFixture.Content.Cast, 1);

        Assert.Equal(1, UsesOf(state.Find("captain")!.Unit, "field_dressing"));
        Assert.Equal(3, UsesOf(state.Find("brannock-1")!.Unit, "field_dressing"));
    }

    [Theory]
    [InlineData("supplies: 0")]
    [InlineData("supplies: 100")]
    [InlineData("supplies: -1")]
    [InlineData("supplies: one")]
    public void ABadSuppliesValueIsRefusedNamingTheLine(string header)
    {
        var ex = Assert.Throws<MapException>(() => MapFixture.Parse(Supplied(header), "supplied.map"));

        Assert.StartsWith("supplied.map, line 7: supplies must be an integer 1..99, got '", ex.Message);
    }
}
