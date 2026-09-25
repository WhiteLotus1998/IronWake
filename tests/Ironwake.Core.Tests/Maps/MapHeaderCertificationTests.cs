using Ironwake.Content;

namespace Ironwake.Core.Tests.Maps;

/// <summary>
/// The <c>certification:</c> header (issue 73, DESIGN.md section 13.6): a one-unit puzzle map
/// whose candidate plays in the class the map grants, with the map's loadout at full uses.
/// Written back canonically, and every bad value refused naming the line.
/// </summary>
public class MapHeaderCertificationTests
{
    private const string Trial = """
        name: Trial
        size: 5x3
        win: survive
        turn_limit: 1
        recall: 0
        enemy_level: 1
        certification: bulwark iron_lance field_dressing

        .....
        .....
        .....

        units:
        P captain 0,1
        E brigand 4,1 group:g behavior:aggressive

        """;

    private static string With(string header) =>
        Trial.Replace("certification: bulwark iron_lance field_dressing", header);

    [Fact]
    public void TheCertificationHeaderRoundTripsInCanonicalOrder()
    {
        var map = MapFixture.Parse(Trial);

        Assert.Equal(new CertificationTrial("bulwark", ValueList<string>.From(new[] { "iron_lance", "field_dressing" })), map.Certification);
        Assert.Equal(Trial, MapFormat.Write(map, MapFixture.Content));
        Assert.Null(MapFixture.Parse(MapFixture.OldMillRoad).Certification);
    }

    [Fact]
    public void ACertificationCandidatePlaysInTheTrialsClassWithOnlyItsLoadoutAtFullUses()
    {
        var map = MapFixture.Parse(Trial);
        var carried = MapFixture.Content.Cast[0];

        var state = BattleState.From(map, MapFixture.Content, MapFixture.Content.Cast, 1);
        var candidate = state.Find("captain")!;

        Assert.Equal("cadet", carried.ClassId);
        Assert.Equal("bulwark", candidate.Unit.ClassId);
        Assert.Equal(new[] { new ItemStack("iron_lance", 40), new ItemStack("field_dressing", 3) }, candidate.Unit.Inventory.Items);
        Assert.Equal(carried.Stats, candidate.Unit.Stats);
        Assert.Equal(carried.Stats.Hp + 3, candidate.Hp);
    }

    [Fact]
    public void AnOrdinaryMapFieldsAUnitAsItIs()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);
        var carried = MapFixture.Content.Cast[0];

        Assert.Same(carried, map.Trial(carried, MapFixture.Content));
    }

    [Theory]
    [InlineData("certification: bulwark", "certification must be '<class> <item> [<item> ...]'")]
    [InlineData("certification: squire iron_lance", "certification names class 'squire', which is not in classes.json")]
    [InlineData("certification: bulwark iron_spoon", "certification loadout names 'iron_spoon', which is not a weapon in weapons.json or an item in items.json")]
    [InlineData("certification: bulwark iron_bow", "certification loadout names 'iron_bow', a bow, which a Bulwark cannot wield")]
    [InlineData("certification: bulwark iron_lance iron_lance iron_lance iron_lance iron_lance iron_lance", "certification loadout holds at most 5 items, got 6")]
    public void ABadCertificationHeaderIsRefusedNamingTheLine(string header, string problem)
    {
        var ex = Assert.Throws<MapException>(() => MapFixture.Parse(With(header), "trial.map"));

        Assert.StartsWith("trial.map, line 7: " + problem, ex.Message);
    }

    [Fact]
    public void ACertificationMapWithASecondPlayerSlotIsRefused()
    {
        var ex = Assert.Throws<MapException>(() => MapFixture.Parse(Trial.Replace("P captain 0,1\n", "P captain 0,1\nP recruit 0,2\n"), "trial.map"));

        Assert.Contains("a certification map has exactly one player slot, the candidate's 'P captain', got 2", ex.Message);
    }
}
