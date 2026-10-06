using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 82's experiment, the keep you build (DESIGN section 13.5): a fixed menu of terrain edits,
/// each with a price and a placement set in <c>campaign.json</c>, applied to the keep's map under
/// <c>content/keep</c>. An edited keep is an ordinary map, and no edit on the menu can close a front (issue 1149: the
/// keep is the finale, so its breaches are the map's <c>fronts:</c>).
/// </summary>
public class KeepTests
{
    private static GameContent Content => MapFixture.Content;

    private static KeepMenu Menu => Content.Campaign.Keep;

    private static MapDefinition Base =>
        MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "keep", Menu.MapId + ".map"), Content);

    private static KeepEdit Edit(string id) => Menu.Edit(id)!;

    [Fact]
    public void TheShippedMenuSellsAWallAndADitchEachPricedAndPlaced()
    {
        Assert.Equal("ironwake_keep", Menu.MapId);
        Assert.Equal(new[] { ("wall", "wall", 400), ("ditch", "water", 300) }, Menu.Edits.Select(e => (e.Id, e.TerrainId, e.Price)));
        Assert.All(Menu.Edits, e => Assert.NotEmpty(e.At));
        Assert.All(Menu.Edits.SelectMany(e => e.At), at => Assert.True(Base.Contains(at), at.ToString()));
    }

    [Fact]
    public void AnEditChangesOneTilesTerrainAndNothingElse()
    {
        var edited = Keep.Apply(Base, Edit("wall"), new Coord(10, 2));

        Assert.Equal("wall", edited.TerrainIdAt(new Coord(10, 2)));
        Assert.Equal(Base with { TerrainIds = edited.TerrainIds }, edited);
        Assert.Equal(1, Base.TerrainIds.Zip(edited.TerrainIds).Count(p => p.First != p.Second));
    }

    [Fact]
    public void AnEditOffItsPlacementSetOnItsOwnTerrainOrOnAUnitIsRefused()
    {
        var walled = Keep.Apply(Base, Edit("wall"), new Coord(10, 2));
        var onUnit = Edit("ditch") with { At = ValueList<Coord>.Of(new Coord(13, 5)) };

        Assert.Equal("Rebuild a wall goes only on 10,2 10,9, not 10,1", Keep.Refusal(Base, Edit("wall"), new Coord(10, 1)));
        Assert.Equal("10,2 is already wall", Keep.Refusal(walled, Edit("wall"), new Coord(10, 2)));
        Assert.Equal("13,5 holds a unit at the start", Keep.Refusal(Base, onUnit, new Coord(13, 5)));
        Assert.Null(Keep.Refusal(Base, Edit("wall"), new Coord(10, 9)));
        var e = Assert.Throws<InvalidOperationException>(() => Keep.Apply(Base, Edit("wall"), new Coord(10, 1)));
        Assert.StartsWith("wall at 10,1: ", e.Message);
    }

    [Fact]
    public void AnEditedKeepIsWrittenCanonicallyAndParsesBackEqual()
    {
        var edited = Menu.Edits.SelectMany(e => e.At.Select(at => (e, at))).Aggregate(Base, (map, x) => Keep.Apply(map, x.e, x.at));

        var text = MapFormat.Write(edited, Content);
        var parsed = MapFormat.Parse("ironwake_keep.map", text, Content);

        Assert.Equal(edited, parsed);
        Assert.Equal(text, MapFormat.Write(parsed, Content));
    }

    /// <summary>
    /// Issue 1149: with every edit on the menu made at once, each of the finale's three fronts still
    /// has a tile a foot unit can walk to from the west edge and on into the keep, so the purse
    /// narrows a front and never shuts one.
    /// </summary>
    [Fact]
    public void EveryEditOnTheMenuAtOnceLeavesEveryFrontOpenToFoot()
    {
        var edited = Menu.Edits.SelectMany(e => e.At.Select(at => (e, at))).Aggregate(Base, (map, x) => Keep.Apply(map, x.e, x.at));

        Assert.Equal(new[] { "north", "gate", "south" }, Base.Fronts.Select(f => f.Name));
        foreach (var front in edited.Fronts)
        {
            var open = front.Tiles.Where(at => edited.TerrainAt(at, Content).IsPassable(MovementType.Infantry)).ToList();
            Assert.NotEmpty(open);
            Assert.Contains(open, at => FootReaches(edited, new Coord(0, at.Y), at) && FootReaches(edited, at, new Coord(13, 5)));
        }
    }

    /// <summary>
    /// Issue 1149: every edit on the menu stands on a front's tile or within two tiles outside one,
    /// so what the purse buys hardens a front and nothing else.
    /// </summary>
    [Fact]
    public void EveryEditOnTheMenuSitsOnAFront()
    {
        var fronts = Base.Fronts.SelectMany(f => f.Tiles).ToList();

        Assert.All(Menu.Edits.SelectMany(e => e.At), at => Assert.Contains(fronts, f => at.X <= f.X && f.DistanceTo(at) <= 2));
    }

    /// <summary>
    /// Issue 1149: the campaign keep seats the finale whole. Its objective, clock, enemies, events,
    /// sworn groups, pair rule, hunter and bond are the measured finale sample's
    /// (<c>docs/samples/ironwake_keep_finale.map</c>), and its fronts are the sample's by name with
    /// every sample front tile kept; only the north and south breaches are a tile wider, so a
    /// rebuilt wall narrows them instead of shutting them, and the clock runs one turn longer
    /// (the lever 0151 named, which carries the depleted company over gate 1).
    /// </summary>
    [Fact]
    public void TheCampaignKeepSeatsTheFinaleWhole()
    {
        var sample = MapFiles.Load(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "ironwake_keep_finale.map"), Content);

        Assert.Equal(WinCondition.DefeatBoss, Base.Win);
        Assert.Equal((sample.Win, sample.TurnLimit + 1), (Base.Win, Base.TurnLimit));
        Assert.Equal(sample.Events, Base.Events);
        Assert.Equal(sample.Placements.OfType<EnemyPlacement>(), Base.Placements.OfType<EnemyPlacement>());
        Assert.Equal(sample.Oathbound, Base.Oathbound);
        Assert.Equal(sample.PairRuleGroups, Base.PairRuleGroups);
        Assert.Equal(sample.Hunter, Base.Hunter);
        Assert.Equal(sample.Holds, Base.Holds);
        Assert.Equal(sample.Fronts.Select(f => f.Name), Base.Fronts.Select(f => f.Name));
        Assert.All(sample.Fronts.Zip(Base.Fronts), p => Assert.Subset(p.Second.Tiles.ToHashSet(), p.First.Tiles.ToHashSet()));
        Assert.Equal(12, Base.Placements.OfType<PlayerPlacement>().Count());
    }

    /// <summary>
    /// Issue 1204, lever 2: each spawn a front's fall fires stands on the tile just inside that
    /// front, next to one of its tiles the menu's wall never covers, so a unit that blocks the wave
    /// stands where the breachers strike. Before the lever the inside spawns were a tile off the
    /// breach, and a unit parked on one blocked the wave for free.
    /// </summary>
    [Fact]
    public void AFallenFrontsSpawnStandsJustInsideThatFront()
    {
        var walls = Menu.Edits.SelectMany(e => e.At).ToHashSet();
        var falls = Base.Events.Where(e => e.Trigger is FallsTrigger).ToList();

        Assert.Equal(new[] { "north", "gate", "gate", "south" }, falls.Select(e => ((FallsTrigger)e.Trigger).Front));
        Assert.All(falls, e =>
        {
            var at = ((SpawnEnemy)e.Action).Placement.At;
            var front = Base.Fronts.Single(f => f.Name == ((FallsTrigger)e.Trigger).Front);
            Assert.True(front.Tiles.Any(t => !walls.Contains(t) && t.DistanceTo(at) == 1 && at.X > t.X), $"{e.Name} at {at}");
        });
        Assert.Equal(new[] { new Coord(11, 1), new Coord(11, 5), new Coord(11, 6), new Coord(11, 10) }, falls.Select(e => ((SpawnEnemy)e.Action).Placement.At));
    }

    /// <summary>
    /// Issue 1204, lever 3 (DECISIONS/0286): on <c>docs/samples/ironwake_keep_hask_holds.map</c> Hask
    /// arrives Aggressive on a one-tile post at his spawn tile, so he strikes anything in his Move plus
    /// his range and walks back (0279's <c>holds:</c>). The sample is the campaign keep with only that
    /// changed; the campaign keep keeps him a Boss, since the lever took the depleted roster under 60.
    /// </summary>
    [Fact]
    public void TheHaskHoldsSampleIsTheKeepWithHaskOnAHeldPost()
    {
        var sample = MapFiles.Load(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "ironwake_keep_hask_holds.map"), Content);
        static EnemyPlacement Lord(MapDefinition map) => map.Events.Select(e => e.Action).OfType<SpawnEnemy>().Single(s => s.Placement.TemplateId == "hask").Placement;

        Assert.Equal((Behavior.Boss, true), (Lord(Base).Behavior, Lord(Base).IsBoss));
        Assert.Null(Base.Holds);
        Assert.Equal((Behavior.Aggressive, true), (Lord(sample).Behavior, Lord(sample).IsBoss));
        Assert.Equal(new HeldGround("lord", Lord(sample).At, Lord(sample).At), sample.Holds);
        Assert.Equal(
            Base with { Name = sample.Name, Events = Base.Events },
            sample with { Holds = null, Events = Base.Events });
        Assert.Equal(Base.Events.Where(e => e.Name != "lord"), sample.Events.Where(e => e.Name != "lord"));
    }

    [Fact]
    public void TheKeepMenuIsRefusedOnAnUnknownTerrainANonPositivePriceOrABadTile()
    {
        static ContentException Fails(string edit) => Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files() with
        {
            Campaign = new ContentFile(ContentFiles.CampaignName, $$"""{ "startingPurse": 0, "certificationPrice": 0, "maps": [ { "map": "one", "reward": 0, "stock": [] } ], "keep": { "map": "keep", "edits": [ {{edit}} ] } }"""),
        }));

        var terrain = Fails("""{ "id": "moat", "name": "Moat", "terrain": "lava", "price": 1, "at": ["1,1"] }""");
        var price = Fails("""{ "id": "moat", "name": "Moat", "terrain": "plain", "price": 0, "at": ["1,1"] }""");
        var tile = Fails("""{ "id": "moat", "name": "Moat", "terrain": "plain", "price": 1, "at": ["one"] }""");
        var none = Fails("""{ "id": "moat", "name": "Moat", "terrain": "plain", "price": 1, "at": [] }""");

        Assert.Equal((ContentFiles.CampaignName, "keep.moat", "terrain"), (terrain.File, terrain.Entry, terrain.Field));
        Assert.Equal("price", price.Field);
        Assert.Equal("at", tile.Field);
        Assert.Equal("at", none.Field);
    }

    [Fact]
    public void TheKeepMenuRoundTripsThroughTheSerializer()
    {
        var files = ContentSerializer.Write(Content);

        Assert.Equal(Content.Campaign, ContentLoader.Parse(files).Campaign);
    }

    /// <summary>Whether an infantry unit could walk from <paramref name="from"/> to <paramref name="to"/> over passable terrain, ignoring units.</summary>
    private static bool FootReaches(MapDefinition map, Coord from, Coord to)
    {
        var seen = new HashSet<Coord> { from };
        var queue = new Queue<Coord>(new[] { from });
        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            if (at == to)
            {
                return true;
            }

            foreach (var next in at.Neighbors().Where(n => map.Contains(n) && map.TerrainAt(n, Content).IsPassable(MovementType.Infantry) && seen.Add(n)))
            {
                queue.Enqueue(next);
            }
        }

        return false;
    }
}
