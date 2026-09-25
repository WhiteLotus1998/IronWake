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
    /// Issue 197: the Tollgate's woods group screens the hill. It holds its forest, so it
    /// never walks onto plain, and the toll brigand at 6,5 reaches 6,4 and every tile within 2
    /// of itself, so the hill is struck while it lives and a strike on it from range 2 is
    /// answered. A brigand with an axe of range 1, or a woods group that guards, fails this.
    /// </summary>
    [Fact]
    public void TheTollgateWoodsScreenHoldsTheHillAndAnswersRangeTwo()
    {
        var map = All().Single(m => m.Id == "the_tollgate").Map;
        var content = MapFixture.Content;
        var woods = map.Placements.OfType<EnemyPlacement>().Where(e => e.Group == "woods").ToList();
        Assert.Equal(2, woods.Count);
        Assert.All(woods, e => Assert.Equal(Behavior.Hold, e.Behavior));

        var screen = Assert.Single(woods, e => e.TemplateId == "toll_brigand");
        Assert.Equal(new Coord(6, 5), screen.At);
        var reach = content.Unit(screen.TemplateId).Inventory.Items
            .Where(item => content.Weapons.ContainsKey(item.ItemId))
            .Select(item => content.Weapon(item.ItemId))
            .ToList();
        for (var distance = 1; distance <= 2; distance++)
        {
            Assert.Contains(reach, w => w.MinRange <= distance && distance <= w.MaxRange);
        }

        Assert.Equal(1, new Coord(6, 4).DistanceTo(screen.At));
    }

    /// <summary>
    /// Issue 208: the Tollgate's woods archer is no free kill. Every passable tile beside it
    /// lies within 1 or 2 of the toll brigand, so a melee strike on the archer eats the thrown
    /// axe; and some passable tile at range 2 of the archer lies outside the brigand's band,
    /// so a bow can still reach it and the group is not a wall. The archer at 5,6 fails the
    /// first half: 4,6 and 5,7 stand at distance 3 from the brigand.
    /// </summary>
    [Fact]
    public void TheTollgateWoodsArcherIsReachedInMeleeOnlyInsideTheBrigandsBand()
    {
        var map = All().Single(m => m.Id == "the_tollgate").Map;
        var content = MapFixture.Content;
        var woods = map.Placements.OfType<EnemyPlacement>().Where(e => e.Group == "woods").ToList();
        var brigand = Assert.Single(woods, e => e.TemplateId == "toll_brigand").At;
        var archer = Assert.Single(woods, e => e.TemplateId == "archer").At;

        bool Open(Coord tile) =>
            tile.X >= 0 && tile.Y >= 0 && tile.X < map.Width && tile.Y < map.Height
            && tile != brigand && map.TerrainAt(tile, content).IsPassable(MovementType.Infantry);

        var neighbours = new[] { new Coord(archer.X + 1, archer.Y), new Coord(archer.X - 1, archer.Y), new Coord(archer.X, archer.Y + 1), new Coord(archer.X, archer.Y - 1) }
            .Where(Open)
            .ToList();
        Assert.NotEmpty(neighbours);
        Assert.All(neighbours, tile => Assert.InRange(tile.DistanceTo(brigand), 1, 2));

        var bowTiles = new List<Coord>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var tile = new Coord(x, y);
                if (Open(tile) && tile.DistanceTo(archer) == 2 && tile.DistanceTo(brigand) > 2)
                {
                    bowTiles.Add(tile);
                }
            }
        }

        Assert.NotEmpty(bowTiles);
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
