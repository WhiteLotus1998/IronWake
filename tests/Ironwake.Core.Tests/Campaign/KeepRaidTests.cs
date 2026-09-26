using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 288, the keep attacked twice (DECISIONS/0059 decisions 7 and 8): a raid on the bare keep
/// mid-campaign from the finale's own spawn tiles, the menu open only after it, the edits carried
/// in the campaign record, the finale fought on the record's keep, and every placement described
/// in rules terms derived from content.
/// </summary>
public class KeepRaidTests
{
    private static GameContent Content => MapFixture.Content;

    private static KeepMenu Menu => Content.Campaign.Keep;

    private static MapDefinition Load(string id) =>
        MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), Content, id), Content);

    private static MapDefinition Bare => Load(Menu.MapId);

    private static int IndexOf(string id) => Content.Campaign.Maps.Select(m => m.MapId).ToList().IndexOf(id);

    /// <summary>A record whose next map is the one after the raid, with <paramref name="purse"/> in hand.</summary>
    private static CampaignRecord AfterRaid(int purse = 1000) =>
        CampaignRecord.Start(Content, 288) with { MapIndex = IndexOf(Menu.RaidId) + 1, Purse = purse };

    [Fact]
    public void TheRaidIsFoughtAfterHarrowWeirAndTheKeepIsTheLastMap()
    {
        Assert.Equal("ironwake_raid", Menu.RaidId);
        Assert.Equal(IndexOf("harrow_weir") + 1, IndexOf(Menu.RaidId));
        Assert.Equal(Content.Campaign.Maps.Count - 1, IndexOf(Menu.MapId));
        Assert.True(Menu.IsKeepMap("ironwake_raid"));
        Assert.True(Menu.IsKeepMap("ironwake_keep"));
        Assert.False(Menu.IsKeepMap("harrow_weir"));
    }

    /// <summary>
    /// The raid shows the finale's leak only if it is the finale's keep: the same grid tile for
    /// tile, the same player slots, and every enemy it brings arriving on a tile the finale's waves
    /// spawn on, with a smaller force than the finale's.
    /// </summary>
    [Fact]
    public void TheRaidIsTheBareKeepWithASmallerForceFromTheFinalesOwnSpawnTiles()
    {
        var raid = Load(Menu.RaidId);
        var keep = Bare;
        static IEnumerable<Coord> Spawns(MapDefinition map) => map.Events.Select(e => e.Action).OfType<SpawnEnemy>().Select(s => s.Placement.At);

        Assert.Equal((keep.Width, keep.Height), (raid.Width, raid.Height));
        Assert.Equal(keep.TerrainIds, raid.TerrainIds);
        Assert.Equal(keep.Placements.OfType<PlayerPlacement>(), raid.Placements.OfType<PlayerPlacement>());
        Assert.NotEmpty(Spawns(raid));
        Assert.All(Spawns(raid), at => Assert.Contains(at, Spawns(keep)));
        Assert.All(raid.Placements.OfType<EnemyPlacement>(), e => Assert.Contains(keep.Placements.OfType<EnemyPlacement>(), k => k.At == e.At));
        var force = (MapDefinition m) => m.Placements.OfType<EnemyPlacement>().Count() + Spawns(m).Count();
        Assert.True(force(raid) < force(keep), $"the raid brings {force(raid)} and the finale {force(keep)}");
    }

    /// <summary>A Rout is won when no enemy is alive, so a wave still to come could be skipped by an early win; every raid wave lands by enemy phase 2.</summary>
    [Fact]
    public void EveryRaidWaveLandsBeforeTheRaidCanBeRouted()
    {
        var raid = Load(Menu.RaidId);

        Assert.Equal(WinCondition.Rout, raid.Win);
        Assert.All(raid.Events, e => Assert.True(Assert.IsType<TurnTrigger>(e.Trigger) is { Phase: Side.Enemy, Turn: <= 2 }, e.Name));
    }

    [Fact]
    public void TheRaidIsWrittenCanonically()
    {
        var path = MapFiles.CampaignPath(Fixture.RealContentDirectory(), Content, Menu.RaidId);

        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), MapFormat.Write(Load(Menu.RaidId), Content));
    }

    [Fact]
    public void TheKeepsMenuIsRefusedBeforeTheRaidAndOpenAfterIt()
    {
        var start = CampaignRecord.Start(Content, 288) with { Purse = 5000 };
        var atRaid = start with { MapIndex = IndexOf(Menu.RaidId) };
        const string closed = "the keep's menu opens after the raid on it (ironwake_raid, map 5) is fought";

        Assert.Equal(closed, start.KeepMenuRefusal(Content));
        Assert.Equal(closed, atRaid.KeepMenuRefusal(Content));
        var refused = atRaid.Build("wall", new Coord(10, 3), Bare, Content);
        Assert.False(refused.Accepted);
        Assert.Equal(closed, refused.Text);
        Assert.Same(atRaid, refused.Record);
        Assert.Null(AfterRaid().KeepMenuRefusal(Content));
        Assert.Null((AfterRaid() with { MapIndex = IndexOf(Menu.MapId) }).KeepMenuRefusal(Content));
        Assert.Equal("the campaign is finished", (AfterRaid() with { MapIndex = Content.Campaign.Maps.Count }).KeepMenuRefusal(Content));
    }

    [Fact]
    public void AContentWithoutARaidNeverOpensTheMenu()
    {
        var content = Content with { Campaign = Content.Campaign with { Keep = Menu with { RaidId = "" } } };

        Assert.Equal("the campaign has no keep to build", (CampaignRecord.Start(content, 1) with { MapIndex = 7 }).KeepMenuRefusal(content));
    }

    [Fact]
    public void BuildingPaysTheEditsPriceRecordsItAndTheKeepMapMakesIt()
    {
        var result = AfterRaid(1000).Build("wall", new Coord(10, 3), Bare, Content);

        Assert.True(result.Accepted, result.Text);
        Assert.Equal(600, result.Record.Purse);
        Assert.Equal(ValueList<KeepWork>.Of(new KeepWork("wall", new Coord(10, 3))), result.Record.Keep);
        Assert.Equal("built for 400, the purse holds 600: wall 10,3: Wall; no unit can stand on it; the gap 10,3 to 10,4 narrows from 2 tiles to 1 (10,4)", result.Text);
        Assert.Equal(Keep.Apply(Bare, Menu.Edit("wall")!, new Coord(10, 3)), result.Record.KeepMap(Bare, Content));
        Assert.Equal(Bare, AfterRaid().KeepMap(Bare, Content));
    }

    [Fact]
    public void BuildingIsRefusedOffTheMenuOffItsPlacementsTwiceOrOnAShortPurse()
    {
        var walled = AfterRaid(1000).Build("wall", new Coord(10, 3), Bare, Content).Record;

        Assert.Equal("the keep's menu has no 'fort'; it sells wall, ditch", AfterRaid().Build("fort", new Coord(11, 4), Bare, Content).Text);
        Assert.Equal("Rebuild a wall goes only on 10,3 10,8, not 10,4", AfterRaid().Build("wall", new Coord(10, 4), Bare, Content).Text);
        Assert.Equal("10,3 is already wall", walled.Build("wall", new Coord(10, 3), Bare, Content).Text);
        Assert.Equal("Dig a ditch costs 300 and the purse holds 200", AfterRaid(200).Build("ditch", new Coord(9, 5), Bare, Content).Text);
        Assert.False(AfterRaid(200).Build("ditch", new Coord(9, 5), Bare, Content).Accepted);
    }

    /// <summary>The line comes from the terrain and the placement: the shipped menu's four lines, read on the bare keep.</summary>
    [Fact]
    public void EachPlacementSaysWhatItDoesInRulesTerms()
    {
        var lines = Menu.Edits.SelectMany(e => e.At.Select(at => Keep.Describe(Bare, e, at, Content)));

        Assert.Equal(
            new[]
            {
                "wall 10,3: Wall; no unit can stand on it; the gap 10,3 to 10,4 narrows from 2 tiles to 1 (10,4)",
                "wall 10,8: Wall; no unit can stand on it; the gap 10,7 to 10,8 narrows from 2 tiles to 1 (10,7)",
                "ditch 9,5: Water; infantry, cavalry and armored units cannot stand on it; flying can",
                "ditch 9,6: Water; infantry, cavalry and armored units cannot stand on it; flying can",
            },
            lines);
    }

    /// <summary>A changed terrain changes the line: the words are the content's, never authored hint text.</summary>
    [Fact]
    public void APlacementLineChangesWithItsTerrain()
    {
        var water = Content.TerrainById("water");
        var shallow = Content with
        {
            Terrain = Content.Terrain.SetItem("water", water with
            {
                Name = "Shallows",
                MoveCosts = ValueList<int?>.Of(2, null, 1, null),
                Avoid = 10,
            }),
        };

        Assert.Equal(
            "ditch 9,5: Shallows; cavalry and armored units cannot stand on it; infantry and flying can; a unit on it gets avoid 10",
            Keep.Describe(Bare, Menu.Edit("ditch")!, new Coord(9, 5), shallow));
    }

    /// <summary>The finale is fought on the keep the record holds: its map is the content's keep with the record's edits made, written canonically.</summary>
    [Fact]
    public void TheRecordsKeepIsWrittenCanonicallyAndTheFinaleBeginsOnIt()
    {
        var record = AfterRaid(1000).Build("wall", new Coord(10, 8), Bare, Content).Record.Build("ditch", new Coord(9, 5), Bare, Content).Record;
        var keep = record.KeepMap(Bare, Content);

        var text = MapFormat.Write(keep, Content);
        Assert.Equal(keep, MapFormat.Parse("ironwake_keep.map", text, Content));
        Assert.Equal("wall", keep.TerrainIdAt(new Coord(10, 8)));
        Assert.Equal("water", keep.TerrainIdAt(new Coord(9, 5)));
        var battle = (record with { MapIndex = IndexOf(Menu.MapId) }).Begin(keep, Content);
        Assert.Equal("wall", battle.Map.TerrainIdAt(new Coord(10, 8)));
    }

    [Fact]
    public void ARecordWithAnEditedKeepRoundTripsThroughTheProtocol()
    {
        var record = AfterRaid(1000).Build("wall", new Coord(10, 3), Bare, Content).Record.Build("ditch", new Coord(9, 6), Bare, Content).Record;

        var json = ProtocolJson.Campaign(record);
        var back = ProtocolJson.ReadCampaign(json, Content);

        Assert.Contains("\"keep\":[{\"edit\":\"wall\",\"at\":{\"x\":10,\"y\":3}},{\"edit\":\"ditch\",\"at\":{\"x\":9,\"y\":6}}]", json);
        Assert.Equal(record, back);
        Assert.Equal(json, ProtocolJson.Campaign(back));
    }

    [Fact]
    public void ARecordWrittenBeforeTheKeepReadsAsNothingBuiltAndAnUnknownEditIsRefused()
    {
        var json = ProtocolJson.Campaign(AfterRaid());
        var old = json.Replace(",\"keep\":[]", "");
        var unknown = json.Replace("\"keep\":[]", "\"keep\":[{\"edit\":\"moat\",\"at\":{\"x\":1,\"y\":1}}]");

        Assert.NotEqual(json, old);
        Assert.Empty(ProtocolJson.ReadCampaign(old, Content).Keep);
        var error = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCampaign(unknown, Content));
        Assert.Equal("field 'keep': 'moat' is not on the keep's menu", error.Message);
    }

    [Fact]
    public void ARaidListedAfterItsKeepOrNamedAsTheKeepIsRefused()
    {
        static ContentException Fails(string maps, string raid) => Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files() with
        {
            Campaign = new ContentFile(ContentFiles.CampaignName, $$"""{ "startingPurse": 0, "certificationPrice": 0, "maps": [ {{maps}} ], "keep": { "map": "keep", "raid": "{{raid}}", "edits": [] } }"""),
        }));

        const string keepFirst = """{ "map": "keep", "reward": 0, "stock": [] }, { "map": "raid", "reward": 0, "stock": [] }""";
        var late = Fails(keepFirst, "raid");
        var same = Fails("""{ "map": "keep", "reward": 0, "stock": [] }""", "keep");

        Assert.Equal((ContentFiles.CampaignName, "keep.raid"), (late.File, late.Field));
        Assert.Equal("raid", same.Field);
    }
}
