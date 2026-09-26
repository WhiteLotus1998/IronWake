using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 82's experiment, the keep you build (DESIGN section 13.5): a fixed menu of terrain edits,
/// each with a price and a placement set in <c>campaign.json</c>, applied to the keep's map under
/// <c>content/keep</c>. An edited keep is an ordinary map, and no edit on the menu can close a breach.
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
        var edited = Keep.Apply(Base, Edit("wall"), new Coord(10, 3));

        Assert.Equal("wall", edited.TerrainIdAt(new Coord(10, 3)));
        Assert.Equal(Base with { TerrainIds = edited.TerrainIds }, edited);
        Assert.Equal(1, Base.TerrainIds.Zip(edited.TerrainIds).Count(p => p.First != p.Second));
    }

    [Fact]
    public void AnEditOffItsPlacementSetOnItsOwnTerrainOrOnAUnitIsRefused()
    {
        var walled = Keep.Apply(Base, Edit("wall"), new Coord(10, 3));
        var onUnit = Edit("ditch") with { At = ValueList<Coord>.Of(new Coord(13, 5)) };

        Assert.Equal("Rebuild a wall goes only on 10,3 10,8, not 10,4", Keep.Refusal(Base, Edit("wall"), new Coord(10, 4)));
        Assert.Equal("10,3 is already wall", Keep.Refusal(walled, Edit("wall"), new Coord(10, 3)));
        Assert.Equal("13,5 holds a unit at the start", Keep.Refusal(Base, onUnit, new Coord(13, 5)));
        Assert.Null(Keep.Refusal(Base, Edit("wall"), new Coord(10, 8)));
        var e = Assert.Throws<InvalidOperationException>(() => Keep.Apply(Base, Edit("wall"), new Coord(10, 4)));
        Assert.StartsWith("wall at 10,4: ", e.Message);
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

    [Fact]
    public void EveryEditOnTheMenuAtOnceLeavesBothBreachesOpenToFoot()
    {
        var edited = Menu.Edits.SelectMany(e => e.At.Select(at => (e, at))).Aggregate(Base, (map, x) => Keep.Apply(map, x.e, x.at));

        foreach (var breach in new[] { new[] { new Coord(10, 3), new Coord(10, 4) }, new[] { new Coord(10, 7), new Coord(10, 8) } })
        {
            var open = breach.Where(at => edited.TerrainAt(at, Content).IsPassable(MovementType.Infantry)).ToList();
            Assert.NotEmpty(open);
            Assert.Contains(open, at => FootReaches(edited, new Coord(0, at.Y), at) && FootReaches(edited, at, new Coord(13, 5)));
        }
    }

    /// <summary>
    /// Issue 287: the waves run to the clock. Every wave arrives on an enemy phase, there are
    /// waves on enemy phases 5 and 6, and the last is the heaviest, so the keep peaks on its
    /// final enemy phases instead of ending with a free turn.
    /// </summary>
    [Fact]
    public void TheWavesRunThroughEnemyPhasesFiveAndSixWithTheHeaviestLast()
    {
        var turns = Base.Events.Select(e => Assert.IsType<TurnTrigger>(e.Trigger)).ToList();
        Assert.All(turns, t => Assert.Equal(Side.Enemy, t.Phase));
        Assert.All(Base.Events, e => Assert.IsType<SpawnEnemy>(e.Action));

        var sizes = turns.GroupBy(t => t.Turn).ToDictionary(g => g.Key, g => g.Count());
        Assert.Contains(5, sizes.Keys);
        Assert.Equal(6, sizes.Keys.Max());
        Assert.All(sizes.Where(kv => kv.Key != 6), kv => Assert.True(kv.Value < sizes[6], $"the wave on enemy phase {kv.Key} is as heavy as the last"));
        Assert.True(sizes[6] > Base.Placements.OfType<EnemyPlacement>().Count() / 2, "the last wave is lighter than half the van");
    }

    /// <summary>
    /// Issue 287: the first wave reaches the breaches on the first enemy phase, so the opening is
    /// not a free turn. Each breach has a van member whose foot path to one of its tiles fits in
    /// that member's movement, over the bare keep and with every edit on the menu made.
    /// </summary>
    [Fact]
    public void TheVanReachesBothBreachesOnTheFirstEnemyPhase()
    {
        var edited = Menu.Edits.SelectMany(e => e.At.Select(at => (e, at))).Aggregate(Base, (map, x) => Keep.Apply(map, x.e, x.at));
        var van = Base.Placements.OfType<EnemyPlacement>().Where(e => e.Group == "van").ToList();
        Assert.NotEmpty(van);

        foreach (var map in new[] { Base, edited })
        {
            foreach (var breach in new[] { new[] { new Coord(10, 3), new Coord(10, 4) }, new[] { new Coord(10, 7), new Coord(10, 8) } })
            {
                var open = breach.Where(at => map.TerrainAt(at, Content).IsPassable(MovementType.Infantry)).ToList();
                Assert.Contains(van, e => open.Any(at => FootSteps(map, e.At, at) <= Mov(e)));
            }
        }
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

    /// <summary>An enemy template's class movement.</summary>
    private static int Mov(EnemyPlacement e) => Content.Class(Content.Unit(e.TemplateId).ClassId).Mov;

    /// <summary>The fewest steps an infantry unit takes from <paramref name="from"/> to <paramref name="to"/> over passable terrain, ignoring units and move costs above one; int.MaxValue when it cannot.</summary>
    private static int FootSteps(MapDefinition map, Coord from, Coord to)
    {
        var steps = new Dictionary<Coord, int> { [from] = 0 };
        var queue = new Queue<Coord>(new[] { from });
        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            if (at == to)
            {
                return steps[at];
            }

            foreach (var next in at.Neighbors().Where(n => map.Contains(n) && map.TerrainAt(n, Content).IsPassable(MovementType.Infantry) && !steps.ContainsKey(n)))
            {
                steps[next] = steps[at] + 1;
                queue.Enqueue(next);
            }
        }

        return int.MaxValue;
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
