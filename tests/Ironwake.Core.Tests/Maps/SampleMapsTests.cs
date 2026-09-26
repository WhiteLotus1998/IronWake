using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Maps;

/// <summary>Every file under content/maps loads, is canonical, and the doc example matches DESIGN.md.</summary>
public class SampleMapsTests
{
    private static IReadOnlyList<(string Id, MapDefinition Map)> All() =>
        MapFiles.LoadAll(Fixture.RealContentDirectory(), MapFixture.Content);

    [Fact]
    public void TheSixShippedMapsLoad()
    {
        var maps = All();

        Assert.Equal(new[] { "brackwater_cut", "harrow_weir", "old_mill_road", "sallow_grange", "saltmarsh_ford", "the_tollgate" }, maps.Select(m => m.Id));
        Assert.Equal(WinCondition.Seize, maps[5].Map.Win);
        Assert.Equal(WinCondition.Seize, maps[3].Map.Win);
        Assert.Equal(WinCondition.DefeatBoss, maps[1].Map.Win);
        Assert.Equal(WinCondition.Escape, maps[0].Map.Win);
    }

    /// <summary>
    /// Issue 78: Harrow Weir announces its reinforcements. Every event is a spawn on a turn
    /// trigger at the start of an enemy phase, the header says <c>announce: on</c>, and the two
    /// north waves share the road's edge tile 7,0, so one body standing there spends both.
    /// </summary>
    [Fact]
    public void HarrowWeirAnnouncesTurnSpawnsAndTheNorthWavesShareOneTile()
    {
        var map = All().Single(m => m.Id == "harrow_weir").Map;

        Assert.True(map.Announced);
        Assert.Equal(new[] { "north1", "west1", "north2" }, map.Events.Select(e => e.Name));
        Assert.All(map.Events, e => Assert.Equal(Side.Enemy, Assert.IsType<TurnTrigger>(e.Trigger).Phase));
        var tiles = map.Events.ToDictionary(e => e.Name, e => Assert.IsType<SpawnEnemy>(e.Action).Placement.At);
        Assert.Equal(new Coord(7, 0), tiles["north1"]);
        Assert.Equal(new Coord(7, 0), tiles["north2"]);
        Assert.Equal(new Coord(0, 11), tiles["west1"]);
    }

    /// <summary>
    /// Issue 256: both north waves matter, so north2 arrives on enemy phase 5 beside the west
    /// wave, and all five recruit slots are named, Keziah in Wren's place, so roster order no
    /// longer decides who fights the Def 8 shieldbearer.
    /// </summary>
    [Fact]
    public void HarrowWeirSendsBothLateWavesOnPhaseFiveAndNamesKeziahNotWren()
    {
        var map = All().Single(m => m.Id == "harrow_weir").Map;

        var turns = map.Events.ToDictionary(e => e.Name, e => ((TurnTrigger)e.Trigger).Turn);
        Assert.Equal(3, turns["north1"]);
        Assert.Equal(5, turns["west1"]);
        Assert.Equal(5, turns["north2"]);
        var recruits = map.Placements.OfType<PlayerPlacement>().Where(p => p.Slot != PlayerSlot.Captain).ToList();
        Assert.Equal(new[] { "teodor", "ottilie", "pell", "dunstan", "keziah" }, recruits.Select(p => p.RecruitId));
    }

    /// <summary>
    /// Issue 78 and section 9's authoring constraint: the weir's bridge is a one-tile corridor
    /// (water on both sides of 10,6 and 11,6) and the shieldbearer holds its east end, so the
    /// armored wall is absolute and the other way over is the ford on rows 10 and 11.
    /// </summary>
    [Fact]
    public void HarrowWeirBridgeIsOneTileWideAndTheShieldbearerHoldsIt()
    {
        var map = All().Single(m => m.Id == "harrow_weir").Map;

        foreach (var x in new[] { 10, 11 })
        {
            Assert.Equal("road", map.TerrainIdAt(new Coord(x, 6)));
            Assert.Equal("water", map.TerrainIdAt(new Coord(x, 5)));
            Assert.Equal("water", map.TerrainIdAt(new Coord(x, 7)));
            Assert.All(new[] { 10, 11 }, y => Assert.Equal("plain", map.TerrainIdAt(new Coord(x, y))));
        }

        var wall = Assert.Single(map.Placements.OfType<EnemyPlacement>(), e => e.At == new Coord(11, 6));
        Assert.Equal("shieldbearer", wall.TemplateId);
        Assert.Equal(Behavior.Hold, wall.Behavior);
    }

    /// <summary>
    /// The thirty-sixth round: maps 4 to 8 each owe one enemy that fights with gauntlets and one
    /// at Def 7 or more, read as the map fields them (class and level included). Harrow Weir's
    /// brawler and its boss carry gauntlets and its bridge shieldbearer stands at Def 7 or more;
    /// Sallow Grange's field brawler carries gauntlets and its north gate shieldbearer is the wall;
    /// Brackwater Cut's bank fields both, a gauntlet brawler and a shieldbearer before the exits.
    /// </summary>
    [Theory]
    [InlineData("brackwater_cut")]
    [InlineData("harrow_weir")]
    [InlineData("sallow_grange")]
    public void MapsFromFourFieldAGauntletEnemyAndOneAtDefSevenOrMore(string id)
    {
        var map = All().Single(m => m.Id == id).Map;
        var content = MapFixture.Content;
        var enemies = map.Placements.OfType<EnemyPlacement>().Select(e => (e.TemplateId, Unit: map.EnemyUnit(e, content))).ToList();

        Assert.Contains(enemies, e => e.Unit.Inventory.Items.Any(i => content.Weapons.TryGetValue(i.ItemId, out var w) && w.Type == WeaponType.Gauntlet));
        Assert.Contains(enemies, e => content.StatsOf(e.Unit).Def >= 7);
    }

    /// <summary>
    /// Issue 80: the corridor is one tile. Column 11 of Brackwater Cut is wall or water on every
    /// row but the gap at 11,3, so every ground unit crosses there, and the water at both ends is
    /// the flyer's only other way through, since a wall blocks flyers too (section 4).
    /// </summary>
    [Fact]
    public void BrackwaterCutCrossesColumnElevenOnlyAtTheGapOrOverWater()
    {
        var map = All().Single(m => m.Id == "brackwater_cut").Map;

        for (var y = 0; y < map.Height; y++)
        {
            var terrain = map.TerrainIdAt(new Coord(11, y));
            if (y == 3)
            {
                Assert.Equal("plain", terrain);
            }
            else if (y is 0 or 10 or 11)
            {
                Assert.Equal("water", terrain);
            }
            else if (y == 1)
            {
                Assert.Equal("fort", terrain);
            }
            else
            {
                Assert.Equal("wall", terrain);
            }
        }

        foreach (var flank in new[] { new Coord(10, 1), new Coord(12, 1), new Coord(10, 10), new Coord(12, 10) })
        {
            Assert.Equal("water", map.TerrainIdAt(flank));
        }
    }

    /// <summary>
    /// Issue 80: a bow the blocker cannot answer. A Hold archer stands on the fort island at 11,1,
    /// water on three sides and wall on the fourth, exactly two from the gap, so a unit holding
    /// 11,3 is under a range-2 bow; only a flyer reaches it in melee.
    /// </summary>
    [Fact]
    public void BrackwaterCutIslandArcherCoversTheGapFromWhereOnlyAFlyerReachesIt()
    {
        var map = All().Single(m => m.Id == "brackwater_cut").Map;

        var archer = Assert.Single(map.Placements.OfType<EnemyPlacement>(), e => e.At == new Coord(11, 1));
        Assert.Equal("archer", archer.TemplateId);
        Assert.Equal(Behavior.Hold, archer.Behavior);
        Assert.Equal(2, archer.At.DistanceTo(new Coord(11, 3)));
        Assert.Equal("water", map.TerrainIdAt(new Coord(11, 0)));
        Assert.Equal("wall", map.TerrainIdAt(new Coord(11, 2)));
    }

    /// <summary>
    /// Issue 80: an Escape with a clock on both sides. Six exits on the east edge for five
    /// deployed units, every pursuer aggressive and west of the wall, and a sleeping Guard bank
    /// east of it standing in front of the exits, so the party clears the front while the gap
    /// holds the rear. No <c>cheap_shots</c> waiver.
    /// </summary>
    [Fact]
    public void BrackwaterCutPursuersComeFromTheWestAndABankSleepsBeforeTheExits()
    {
        var map = All().Single(m => m.Id == "brackwater_cut").Map;
        var enemies = map.Placements.OfType<EnemyPlacement>().ToList();

        Assert.Equal(WinCondition.Escape, map.Win);
        Assert.False(map.CheapShotsAllowed);
        Assert.Equal(6, map.Exits.Count);
        Assert.All(map.Exits, e => Assert.Equal(map.Width - 1, e.X));
        Assert.Equal(5, map.Placements.Count(p => p is not EnemyPlacement));
        Assert.All(enemies.Where(e => e.Group == "chase"), e => { Assert.Equal(Behavior.Aggressive, e.Behavior); Assert.True(e.At.X < 11); });
        var bank = enemies.Where(e => e.Group == "bank").ToList();
        Assert.Equal(3, bank.Count);
        Assert.All(bank, e => { Assert.Equal(Behavior.Guard, e.Behavior); Assert.True(e.At.X > 11); });
    }

    private static IReadOnlyList<Coord> FieldGroup(MapDefinition map) =>
        map.Placements.OfType<EnemyPlacement>().Where(e => e.Group == "field").Select(e => e.At).ToList();

    private static bool Within(IReadOnlyList<Coord> members, Coord tile, int radius) =>
        members.Any(m => m.DistanceTo(tile) <= radius);

    /// <summary>
    /// Issue 79: the sleeping group sits on open ground with room on two sides, since a group in a
    /// corridor can only be woken, never dashed past (DIALOGUE.md, third round). The field group is
    /// three Guard units whose wake diamond reaches neither the top row nor the bottom row, so a
    /// unit can pass it to the north or to the south without stopping inside it.
    /// </summary>
    [Fact]
    public void SallowGrangeFieldGroupSleepsWithRoomToPassOnBothSides()
    {
        var map = All().Single(m => m.Id == "sallow_grange").Map;
        var field = FieldGroup(map);
        var radius = MapFixture.Content.WakeRadius;

        Assert.Equal(3, field.Count);
        Assert.All(map.Placements.OfType<EnemyPlacement>().Where(e => e.Group == "field"), e => Assert.Equal(Behavior.Guard, e.Behavior));
        for (var x = 0; x < map.Width; x++)
        {
            Assert.False(Within(field, new Coord(x, 0), radius), $"row 0 at x {x} wakes the field");
            Assert.False(Within(field, new Coord(x, map.Height - 1), radius), $"row {map.Height - 1} at x {x} wakes the field");
        }
    }

    /// <summary>
    /// Issue 79: the throne is reachable two ways. The west gate (11,5 and 11,6) is the short way
    /// and lies inside the field group's wake radius; the north gate (12,2) is the long way, held
    /// by a Hold shieldbearer whose only outside neighbour, 12,1, is beyond the field's noise
    /// radius, so the lock can be broken in melee without waking the field. A caster at 10,2 is
    /// inside the noise radius: the long way has a wrong tile too.
    /// </summary>
    [Fact]
    public void SallowGrangeWestGateWakesTheFieldAndTheNorthGateCanBeBrokenQuietly()
    {
        var map = All().Single(m => m.Id == "sallow_grange").Map;
        var content = MapFixture.Content;
        var field = FieldGroup(map);

        foreach (var gate in new[] { new Coord(11, 5), new Coord(11, 6) })
        {
            Assert.Equal("plain", map.TerrainIdAt(gate));
            Assert.True(Within(field, gate, content.WakeRadius));
        }

        var lockUnit = Assert.Single(map.Placements.OfType<EnemyPlacement>(), e => e.At == new Coord(12, 2));
        Assert.Equal("shieldbearer", lockUnit.TemplateId);
        Assert.Equal(Behavior.Hold, lockUnit.Behavior);
        Assert.Equal("wall", map.TerrainIdAt(new Coord(11, 2)));
        Assert.Equal("wall", map.TerrainIdAt(new Coord(13, 2)));
        Assert.False(Within(field, new Coord(12, 1), content.NoiseRadius));
        Assert.False(Within(field, new Coord(12, 2), content.NoiseRadius));
        Assert.True(Within(field, new Coord(10, 2), content.NoiseRadius));
    }

    /// <summary>
    /// Issue 259: the Reeve is a guard boss alone in his group, beside the throne on the side the
    /// west gate faces, and an enemy holds the fort on the short way. The one tile inside the
    /// walls a captain could seize from in one move without ending within the wake radius of
    /// him is 17,3, which only the north gate reaches: the short way cannot seize past a
    /// sleeping Reeve, and the long way's quiet seize is its prize.
    /// </summary>
    [Fact]
    public void SallowGrangeHasAGuardBossBesideTheThroneAndAnEnemyOnTheFort()
    {
        var map = All().Single(m => m.Id == "sallow_grange").Map;
        var content = MapFixture.Content;
        var throne = new Coord(16, 6);

        Assert.Equal(WinCondition.Seize, map.Win);
        var boss = Assert.Single(map.Placements.OfType<EnemyPlacement>(), e => e.IsBoss);
        Assert.Equal(Behavior.Guard, boss.Behavior);
        Assert.Single(map.Placements.OfType<EnemyPlacement>(), e => e.Group == boss.Group);
        Assert.Equal(1, boss.At.DistanceTo(throne));
        Assert.Equal("throne", map.TerrainIdAt(throne));
        var captainMov = content.Class("cadet").Mov;
        var quiet = Enumerable.Range(3, 6)
            .SelectMany(y => Enumerable.Range(12, 6).Select(x => new Coord(x, y)))
            .Where(c => map.TerrainIdAt(c) != "wall" && c.DistanceTo(throne) <= captainMov && c.DistanceTo(boss.At) > content.WakeRadius);
        Assert.Equal(new[] { new Coord(17, 3) }, quiet);
        var fort = Assert.Single(map.Placements.OfType<EnemyPlacement>(), e => map.TerrainIdAt(e.At) == "fort");
        Assert.Equal(Behavior.Hold, fort.Behavior);
    }

    /// <summary>
    /// Issue 275: the long way is quiet on the enemy phase too. A captain on the broken north
    /// gate at 12,2 has a stop that is within one move of both 12,2 and 17,3, more than the wake
    /// radius from the Reeve, and outside the strike range of every enemy but the lock it has
    /// just broken, so nothing fires on the lane and no combat wakes the hall. With the hexer
    /// at 13,4 the only such stop, 13,3, is one tile from its Cinder.
    /// </summary>
    [Fact]
    public void SallowGrangeNorthLaneHasAStopNoEnemyCanStrike()
    {
        var map = All().Single(m => m.Id == "sallow_grange").Map;
        var content = MapFixture.Content;
        var gate = new Coord(12, 2);
        var stage = new Coord(17, 3);
        var mov = content.Class("cadet").Mov;
        var boss = Assert.Single(map.Placements.OfType<EnemyPlacement>(), e => e.IsBoss);
        var strikers = map.Placements.OfType<EnemyPlacement>().Where(e => e.At != gate).ToList();

        var stops = Enumerable.Range(0, map.Height)
            .SelectMany(y => Enumerable.Range(0, map.Width).Select(x => new Coord(x, y)))
            .Where(c => map.TerrainIdAt(c) != "wall" && c.DistanceTo(gate) <= mov && c.DistanceTo(stage) <= mov)
            .Where(c => c.DistanceTo(boss.At) > content.WakeRadius)
            .Where(c => strikers.All(e => c.DistanceTo(e.At) > MaxRange(content, e.TemplateId)))
            .ToList();

        Assert.Contains(new Coord(13, 3), stops);
        Assert.True(stage.DistanceTo(boss.At) > content.WakeRadius);
        Assert.True(strikers.All(e => stage.DistanceTo(e.At) > MaxRange(content, e.TemplateId)));
    }

    /// <summary>
    /// Issue 275: the hexer moved off the north lane still prices the short way. From 13,7 its
    /// Cinder reaches 12,6, the yard tile beside the west gap's south half, so a unit stepping
    /// through the mouth to fight the Reeve is in its range. A hexer at 13,8, three from 12,6,
    /// fails this.
    /// </summary>
    [Fact]
    public void SallowGrangeHexerCoversATileBesideTheWestGap()
    {
        var map = All().Single(m => m.Id == "sallow_grange").Map;
        var content = MapFixture.Content;
        var hexer = Assert.Single(map.Placements.OfType<EnemyPlacement>(), e => e.TemplateId == "hexer");
        Assert.Equal(Behavior.Hold, hexer.Behavior);
        var beside = new[] { new Coord(10, 5), new Coord(10, 6), new Coord(12, 5), new Coord(12, 6) };

        Assert.Contains(beside, c => c.DistanceTo(hexer.At) <= MaxRange(content, hexer.TemplateId));
    }

    private static int MaxRange(GameContent content, string templateId) =>
        content.Unit(templateId).Inventory.Items
            .Where(item => content.Weapons.ContainsKey(item.ItemId))
            .Select(item => content.Weapon(item.ItemId).MaxRange)
            .DefaultIfEmpty(0)
            .Max();

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

        Assert.Equal(All().Single(m => m.Id == "old_mill_road").Map, MapFixture.Parse(example, "DESIGN.md"));
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
