using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Maps;

/// <summary>Every file under content/maps loads, is canonical, and the doc example matches DESIGN.md.</summary>
public class SampleMapsTests
{
    private static IReadOnlyList<(string Id, MapDefinition Map)> All() =>
        MapFiles.LoadAll(Fixture.RealContentDirectory(), MapFixture.Content);

    [Fact]
    public void ThreeSampleMapsLoad()
    {
        var maps = All();

        Assert.Equal(new[] { "old_mill_road", "saltmarsh_ford", "the_tollgate" }, maps.Select(m => m.Id));
        Assert.Equal(WinCondition.Seize, maps[2].Map.Win);
    }

    [Fact]
    public void EverySampleFileIsInCanonicalForm()
    {
        foreach (var (id, map) in All())
        {
            var path = Path.Combine(MapFixture.MapsDirectory, id + MapFiles.Extension);
            var onDisk = File.ReadAllText(path).Replace("\r\n", "\n");

            Assert.True(onDisk == MapFormat.Write(map, MapFixture.Content), id + ".map is not in canonical form; rewrite it with MapFormat.Write");
        }
    }

    [Fact]
    public void EverySampleMapRoundTrips()
    {
        foreach (var (_, map) in All())
        {
            Assert.Equal(map, MapFixture.Parse(MapFormat.Write(map, MapFixture.Content)));
        }
    }

    [Fact]
    public void TheSampleFileMatchesTheExampleInTheDesignDoc()
    {
        var design = File.ReadAllText(Path.Combine(Fixture.RealContentDirectory(), "..", "docs", "DESIGN.md"));
        var start = design.IndexOf("```\nname: Old Mill Road", StringComparison.Ordinal);
        Assert.True(start >= 0, "DESIGN.md section 10 no longer has the Old Mill Road example");
        var end = design.IndexOf("```", start + 3, StringComparison.Ordinal);
        var example = design[(start + 4)..end];

        Assert.Equal(All()[0].Map, MapFixture.Parse(example, "DESIGN.md"));
    }

    [Fact]
    public void AMissingMapsDirectoryIsEmptyNotAnError()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ironwake-nomaps-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Empty(MapFiles.LoadAll(dir, MapFixture.Content));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
