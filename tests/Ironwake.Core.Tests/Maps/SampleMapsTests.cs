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

    /// <summary>
    /// Issue 181: the Tollgate's keep has no free tile. Every open tile outside the keep
    /// walls that stands within 2 of a keep enemy is a tile that enemy's weapons reach, so a
    /// range-2 unit striking the keep from outside is always answered. The door warden's
    /// thrown spear is what answers 6,4; a soldier with a lance of range 1 would not.
    /// </summary>
    [Fact]
    public void TheTollgateKeepAnswersEveryTileThatCanStrikeIt()
    {
        var map = All().Single(m => m.Id == "the_tollgate").Map;
        var content = MapFixture.Content;
        var keep = map.Placements.OfType<EnemyPlacement>().Where(e => e.Group == "keep").ToList();
        Assert.Equal(3, keep.Count);
        Assert.Contains(keep, e => e.TemplateId == "toll_warden" && e.At == new Coord(6, 2));

        var checkedTiles = 0;
        for (var y = 3; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var tile = new Coord(x, y);
                if (!map.TerrainAt(tile, content).IsPassable(MovementType.Infantry))
                {
                    continue;
                }

                foreach (var enemy in keep)
                {
                    var distance = tile.DistanceTo(enemy.At);
                    if (distance > 2)
                    {
                        continue;
                    }

                    checkedTiles++;
                    var answers = content.Unit(enemy.TemplateId).Inventory.Items
                        .Where(item => content.Weapons.ContainsKey(item.ItemId))
                        .Select(item => content.Weapon(item.ItemId))
                        .Any(w => w.MinRange <= distance && distance <= w.MaxRange);
                    Assert.True(answers, $"{enemy.TemplateId} at {enemy.At} cannot answer a strike from {tile}");
                }
            }
        }

        Assert.True(checkedTiles >= 3, "the keep is no longer reachable from outside");
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
