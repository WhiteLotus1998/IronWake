using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Maps;

/// <summary>
/// The <c>region:</c> header (issue 916, DESIGN.md section 10): where a map lies, which picks the
/// ground plain is drawn in. Absent means the seam; an unknown word is refused naming the file,
/// the line and the field; the shipped maps carry #892's list.
/// </summary>
public class MapHeaderRegionTests
{
    private static string Regioned(string header) =>
        MapFixture.OldMillRoad.Replace("enemy_level: 1\n", "enemy_level: 1\n" + header + "\n");

    [Fact]
    public void AMapWithoutARegionLiesOnTheSeam()
    {
        Assert.Equal(MapRegion.Seam, MapFixture.Parse(MapFixture.OldMillRoad).Region);
    }

    [Theory]
    [InlineData("sallow", MapRegion.Sallow)]
    [InlineData("aldmere", MapRegion.Aldmere)]
    [InlineData("kestrow", MapRegion.Kestrow)]
    [InlineData("outland", MapRegion.Outland)]
    public void TheRegionHeaderRoundTripsInCanonicalOrder(string word, MapRegion region)
    {
        var text = Regioned("region: " + word);

        var map = MapFixture.Parse(text);

        Assert.Equal(region, map.Region);
        Assert.Equal(text, MapFormat.Write(map, MapFixture.Content));
    }

    [Fact]
    public void TheSeamIsNeverWrittenSinceItIsTheDefault()
    {
        var map = MapFixture.Parse(Regioned("region: seam"));

        Assert.Equal(MapRegion.Seam, map.Region);
        Assert.DoesNotContain("region:", MapFormat.Write(map, MapFixture.Content));
    }

    [Fact]
    public void AnUnknownRegionIsRefusedNamingTheFileAndTheField()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Regioned("region: desert"), "dunes.map"));

        Assert.Equal("dunes.map", error.File);
        Assert.Contains("region names 'desert'", error.Message);
        Assert.Contains("outland", error.Message);
    }

    [Theory]
    [InlineData("maps/saltmarsh_ford.map", MapRegion.Sallow)]
    [InlineData("maps/sallow_grange.map", MapRegion.Sallow)]
    [InlineData("maps/brackwater_cut.map", MapRegion.Sallow)]
    [InlineData("maps/harrow_weir.map", MapRegion.Aldmere)]
    [InlineData("maps/starting_alone.map", MapRegion.Seam)]
    [InlineData("maps/the_mill.map", MapRegion.Seam)]
    [InlineData("maps/the_tollgate.map", MapRegion.Seam)]
    [InlineData("keep/ironwake_raid.map", MapRegion.Seam)]
    [InlineData("maps/the_field.map", MapRegion.Seam)]
    [InlineData("keep/ironwake_keep.map", MapRegion.Seam)]
    public void TheCampaignMapsCarryTheRegionsOf892sList(string file, MapRegion region)
    {
        var path = Path.Combine(Fixture.RealContentDirectory(), file);

        Assert.Equal(region, MapFormat.Parse(Path.GetFileName(path), File.ReadAllText(path), MapFixture.Content).Region);
    }
}
