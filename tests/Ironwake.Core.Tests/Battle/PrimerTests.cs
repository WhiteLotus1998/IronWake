using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Schools 5 (issue 1246, DECISIONS/0301): a primer (<c>teaches</c> in items.json) is read at camp and
/// adds a school to a Lore unit's own <see cref="Unit.Learned"/>; a learned school's rider fires only on
/// the caster's Mag above the target's Res plus the rider's gate, and the forecast prints the reading; a
/// grimoire's <c>minMag</c> gates who wields it; a map's <c>drops:</c> sends a dying enemy's tomes to the
/// wagon. No shipped content uses any of it, so these tests use fixtures: <c>test_primer_fire</c>, and
/// <c>test_grimoire_mag8</c> (Cinder with fire's burn and Mag 8), with the Adept reaching ice and
/// lightning only, so Pell must learn fire.
/// </summary>
public class PrimerTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Weapon Cinder = Shipped.Weapon("cinder");

    private static readonly GameContent Learning = Shipped with
    {
        Items = Shipped.Items
            .SetItem("test_primer_fire", new Item("test_primer_fire", "Test Primer", 0, 1) { Teaches = MagicSchool.Fire, Description = "A test line." }),
        Weapons = Shipped.Weapons
            .SetItem("cinder", Cinder with { Rider = RiderKind.Burn })
            .SetItem("test_grimoire_mag8", Cinder with { Id = "test_grimoire_mag8", Name = "Test Grimoire", Rider = RiderKind.Burn, MinMag = 8 }),
        Classes = Shipped.Classes.SetItem("adept", Shipped.Class("adept") with { Schools = ValueList<MagicSchool>.Of(MagicSchool.Ice, MagicSchool.Lightning) }),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private static Unit Pell(GameContent content) => content.Cast.Single(u => u.Id == "pell");

    private static CampaignRecord WithPrimer(int slot = 0, GameContent? content = null)
    {
        content ??= Learning;
        var record = CampaignRecord.Start(content, 1246);
        var pell = Pell(content) with { Inventory = Pell(content).Inventory.Replace(slot, new ItemStack("test_primer_fire", 1)) };
        return record with { Roster = ValueList<Unit>.From(record.Roster.Where(u => u.Id != "pell").Append(pell)) };
    }

    /// <summary>Pell two tiles below the woods brigand, Cinder in front, her Mag set to <paramref name="mag"/>, fire learned when <paramref name="learned"/>.</summary>
    private static BattleState Facing(GameContent content, int mag, bool learned = true, ulong seed = 1246)
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, content), content, content.Cast, seed);
        var pell = state.Find("pell")!;
        var unitClass = content.Class(pell.Unit.ClassId);
        var unit = pell.Unit with
        {
            Stats = pell.Unit.Stats with { Mag = mag - unitClass.Modifiers.Mag },
            Learned = learned ? ValueList<MagicSchool>.Of(MagicSchool.Fire) : ValueList<MagicSchool>.Empty,
        };
        return state.WithUnit(pell with { At = new Coord(6, 7), Unit = unit });
    }

    private static BattleUnit Brigand(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Group == "woods" && u.Unit.ClassId == Shipped.Unit("toll_brigand").ClassId);

    private static int Res(GameContent content, BattleState state) => content.StatsOf(Brigand(state).Unit).Res;

    /// <summary>The first seed on which Pell's Cinder lands on the brigand and leaves it standing.</summary>
    private static (BattleState Before, ApplyResult Result) Struck(GameContent content, int mag)
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = Facing(content, mag, seed: seed);
            var id = Brigand(state).Id;
            var result = Resolver.Apply(state, content, new Attack("pell", id));
            if (result.Next.Find(id) is { } struck && struck.Hp < Brigand(state).Hp)
            {
                return (state, result);
            }
        }

        throw new InvalidOperationException("no seed under 400 gave a surviving hit");
    }

    [Fact]
    public void APrimerLoadsWithTeachesAndRoundTripsThroughTheSerializer()
    {
        const string items = """
            { "items": [ { "id": "primer", "name": "Primer", "teaches": "ice", "uses": 1, "description": "A test line." } ] }
            """;
        var content = ContentLoader.Parse(Fixture.Files(items: items));
        var again = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.Equal(MagicSchool.Ice, content.Item("primer").Teaches);
        Assert.Equal(0, content.Item("primer").Heals);
        Assert.Equal(content.Item("primer"), again.Item("primer"));
    }

    [Theory]
    [InlineData("\"teaches\": \"earth\", \"uses\": 1", "teaches", "earth is never taught")]
    [InlineData("\"teaches\": \"fire\", \"heals\": 5, \"uses\": 1", "teaches", "heals nothing")]
    [InlineData("\"teaches\": \"fire\", \"uses\": 2", "uses", "read once")]
    [InlineData("\"teaches\": \"sand\", \"uses\": 1", "teaches", "'sand'")]
    public void ABadPrimerIsRefusedAtLoadNamingFileEntryAndField(string fields, string field, string why)
    {
        var items = "{ \"items\": [ { \"id\": \"primer\", \"name\": \"Primer\", " + fields + ", \"description\": \"A test line.\" } ] }";

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(items: items)));

        Assert.Contains("items.json", error.Message);
        Assert.Contains("primer", error.Message);
        Assert.Equal(field, error.Field);
        Assert.Contains(why, error.Message);
    }

    [Fact]
    public void NoShippedItemTeachesAndNoShippedTomeIsGatedOnMag()
    {
        Assert.All(Shipped.Items.Values, i => Assert.Null(i.Teaches));
        Assert.All(Shipped.Weapons.Values, w => Assert.Null(w.MinMag));
        Assert.All(Shipped.Riders.Values, r => Assert.Equal(0, r.Gate));
    }

    [Fact]
    public void ReadingAPrimerAtCampTeachesItsSchoolAndSpendsIt()
    {
        var result = WithPrimer().Read("pell", 0, Learning);

        Assert.True(result.Accepted, result.Text);
        var pell = result.Record.Find("pell")!;
        Assert.Equal(new[] { MagicSchool.Fire }, pell.Learned);
        Assert.DoesNotContain(pell.Inventory.Items, s => s.ItemId == "test_primer_fire");
        Assert.Equal("pell reads Test Primer and learns the fire school; its rider fires only on Mag over the target's Res", result.Text);
        Assert.True(pell.CanWield(Learning.Weapon("cinder"), Learning.Class("adept")));
        Assert.False(Pell(Learning).CanWield(Learning.Weapon("cinder"), Learning.Class("adept")));
    }

    [Fact]
    public void APrimerIsRefusedOnAClassThatDoesNotWieldLore()
    {
        var record = CampaignRecord.Start(Learning, 1246);
        var captain = record.Roster[0];
        record = record with { Roster = record.Roster.SetItem(0, captain with { Inventory = captain.Inventory.Replace(0, new ItemStack("test_primer_fire", 1)) }) };

        var result = record.Read(captain.Id, 0, Learning);

        Assert.False(result.Accepted);
        Assert.Contains("does not wield Lore", result.Text);
    }

    [Fact]
    public void APrimerIsRefusedForASchoolTheUnitAlreadyReaches()
    {
        var reaching = Learning with { Classes = Shipped.Classes };

        var result = WithPrimer(content: reaching).Read("pell", 0, reaching);

        Assert.False(result.Accepted);
        Assert.Equal("pell already reaches the fire school", result.Text);
    }

    [Fact]
    public void OnlyAPrimerIsRead()
    {
        var result = WithPrimer(0).Read("pell", 1, Learning);

        Assert.False(result.Accepted);
        Assert.Contains("is not a primer", result.Text);
    }

    [Fact]
    public void APrimerIsRefusedInBattle()
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, Learning), Learning, Learning.Cast, 1246);
        var pell = state.Find("pell")!;
        state = state.WithUnit(pell with { Hp = 1, Unit = pell.Unit with { Inventory = pell.Unit.Inventory.Replace(0, new ItemStack("test_primer_fire", 1)) } });

        var result = Resolver.Apply(state, Learning, new UseItem("pell", 0));

        Assert.False(result.Accepted);
        Assert.Contains("read at camp, not in battle", result.Rejection!.Message);
        Assert.DoesNotContain(Resolver.Legal(state, Learning), c => c is UseItem { UnitId: "pell", Slot: 0 });
    }

    [Fact]
    public void LearnedSchoolsRoundTripThroughTheSaveAndAnOldSaveReadsNone()
    {
        var learned = WithPrimer().Read("pell", 0, Learning).Record;
        var json = ProtocolJson.Campaign(learned);

        Assert.Contains("\"learned\":[\"fire\"]", json);
        Assert.Equal(new[] { MagicSchool.Fire }, ProtocolJson.ReadCampaign(json, Learning).Find("pell")!.Learned);
        Assert.Empty(ProtocolJson.ReadCampaign(ProtocolJson.Campaign(WithPrimer()), Learning).Find("pell")!.Learned);
        Assert.DoesNotContain("learned", ProtocolJson.Campaign(WithPrimer()));
    }

    [Fact]
    public void ASaveWithAWordThatIsNotASchoolIsRefused()
    {
        var json = ProtocolJson.Campaign(WithPrimer().Read("pell", 0, Learning).Record).Replace("\"learned\":[\"fire\"]", "\"learned\":[\"sand\"]");

        var error = Assert.Throws<ProtocolException>(() => ProtocolJson.ReadCampaign(json, Learning));

        Assert.Contains("'sand'", error.Message);
    }

    [Fact]
    public void ALearnedRiderFiresWhenMagIsAboveRes()
    {
        var res = Res(Learning, Facing(Learning, 0));
        var (before, result) = Struck(Learning, res + 1);

        Assert.Contains(result.Events, e => e is UnitIgnited);
        Assert.True(result.Next.Find(Brigand(before).Id)!.BurnPhases > 0);
    }

    [Fact]
    public void ALearnedRiderDoesNotFireWhenMagIsNotAboveRes()
    {
        var res = Res(Learning, Facing(Learning, 0));
        var (before, result) = Struck(Learning, res);

        Assert.DoesNotContain(result.Events, e => e is UnitIgnited);
        Assert.Equal(0, result.Next.Find(Brigand(before).Id)!.BurnPhases);
    }

    [Fact]
    public void AClassThatReachesTheSchoolIsNeverGated()
    {
        var reaching = Learning with { Classes = Shipped.Classes };
        var res = Res(reaching, Facing(reaching, 0, learned: false));

        var (_, result) = Struck(reaching, res);

        Assert.Contains(result.Events, e => e is UnitIgnited);
        Assert.Null(LearnedGate.Read(reaching, Facing(reaching, res, learned: false).Find("pell"), reaching.Weapon("cinder"), Brigand(Facing(reaching, res))));
    }

    [Fact]
    public void TheGateAddsTheRidersMarginAndANullGateIsNoGate()
    {
        var res = Res(Learning, Facing(Learning, 0));
        var margined = Learning with { Riders = Learning.Riders.SetItem(MagicSchool.Fire, Learning.Riders[MagicSchool.Fire] with { Gate = 2 }) };
        var open = Learning with { Riders = Learning.Riders.SetItem(MagicSchool.Fire, Learning.Riders[MagicSchool.Fire] with { Gate = null }) };
        var state = Facing(Learning, res + 2);

        var reading = LearnedGate.Read(margined, state.Find("pell"), margined.Weapon("cinder"), Brigand(state))!;
        Assert.False(reading.Passes);
        Assert.Equal($"Mag {res + 2}, Res {res}+2", reading.Text);
        Assert.True(LearnedGate.Read(margined, Facing(Learning, res + 3).Find("pell"), margined.Weapon("cinder"), Brigand(state))!.Passes);
        Assert.Null(LearnedGate.Read(open, Facing(Learning, 0).Find("pell"), open.Weapon("cinder"), Brigand(state)));
        Assert.True(LearnedGate.Fires(open, Facing(Learning, 0).Find("pell"), open.Weapon("cinder"), Brigand(state)));
    }

    [Fact]
    public void TheForecastPrintsTheGateEitherWay()
    {
        var res = Res(Learning, Facing(Learning, 0));
        var over = Facing(Learning, res + 2);
        var under = Facing(Learning, res);

        Assert.Equal($" burn {Math.Max(1, 2 - res / 2)} (1 stack, 2 phases): Mag {res + 2} over Res {res}", PlaySession.Riders(Learning, over.Find("pell")!, Brigand(over), null).Riders);
        Assert.Equal($" no burn: Mag {res}, Res {res}", PlaySession.Riders(Learning, under.Find("pell")!, Brigand(under), null).Riders);
    }

    [Fact]
    public void TheEnemyPlannerPricesNoBurnTheGateHoldsBack()
    {
        var res = Res(Learning, Facing(Learning, 0));
        var under = Facing(Learning, res);
        var over = Facing(Learning, res + 1);
        var pellUnder = under.Find("pell")!;
        var pellOver = over.Find("pell")!;

        var held = EnemyAi.Score(under, Learning, pellUnder, pellUnder.At, Brigand(under));
        var fired = EnemyAi.Score(over, Learning, pellOver, pellOver.At, Brigand(over));
        var plain = EnemyAi.Score(under, Learning with { Weapons = Learning.Weapons.SetItem("cinder", Cinder) }, pellUnder, pellUnder.At, Brigand(under));

        Assert.Equal(plain, held);
        Assert.True(fired > held);
    }

    [Fact]
    public void AGrimoireBelowItsMagIsRefusedNamingTheGate()
    {
        var state = Facing(Learning, 7);
        var pell = state.Find("pell")!;
        state = state.WithUnit(pell with { Unit = pell.Unit with { Inventory = pell.Unit.Inventory.Replace(0, new ItemStack("test_grimoire_mag8", Cinder.Durability)) } });

        var result = Resolver.Apply(state, Learning, new Attack("pell", Brigand(state).Id, Slot: 0));

        Assert.False(result.Accepted);
        Assert.EndsWith("needs Mag 8; pell has 7", result.Rejection!.Message);
        Assert.False(state.Find("pell")!.Unit.CanWield(Learning.Weapon("test_grimoire_mag8"), Learning.Class("adept")));
        Assert.True(Facing(Learning, 8).Find("pell")!.Unit.CanWield(Learning.Weapon("test_grimoire_mag8"), Learning.Class("adept")));
    }

    [Fact]
    public void TheGrimoiresCardNamesItsMagGate()
    {
        Assert.Contains(" E, fire school, needs Mag 8. Acc", ItemCard.Text(Learning, "test_grimoire_mag8"));
        Assert.StartsWith("Test Primer. Teaches the fire school to a Lore class, read at camp.", ItemCard.Text(Learning, "test_primer_fire"));
    }

    [Theory]
    [InlineData("sword", 3, "only a Lore (reason) tome")]
    [InlineData("reason", 0, "at least 1")]
    public void ABadMinMagIsRefusedAtLoad(string type, int minMag, string why)
    {
        var weapons = "{ \"weapons\": [ { \"id\": \"grim\", \"name\": \"Grim\", \"type\": \"" + type + "\", \"minMag\": " + minMag
            + ", \"mt\": 3, \"hit\": 90, \"crit\": 0, \"wt\": 1, \"minRange\": 1, \"maxRange\": 1, \"durability\": 8, \"description\": \"A test line.\", \"rank\": \"E\" } ] }";

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(weapons: weapons)));

        Assert.Equal("minMag", error.Field);
        Assert.Contains(why, error.Message);
    }

    [Fact]
    public void MinMagRoundTripsThroughTheSerializer()
    {
        var files = ContentSerializer.Write(Shipped);
        var weapons = files.Weapons.Text.Replace("\"id\": \"cinder\",", "\"id\": \"cinder\", \"minMag\": 4,");
        var content = ContentLoader.Parse(files with { Weapons = new ContentFile(files.Weapons.Name, weapons) });
        var again = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.Equal(4, content.Weapon("cinder").MinMag);
        Assert.Equal(4, again.Weapon("cinder").MinMag);
    }

    [Fact]
    public void AnEnemyTemplateBelowItsGrimoiresMagIsAContentError()
    {
        var files = ContentSerializer.Write(Shipped);
        var weapons = files.Weapons.Text.Replace("\"id\": \"cinder\",", "\"id\": \"cinder\", \"minMag\": 99,");

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(files with { Weapons = new ContentFile(files.Weapons.Name, weapons) }));

        Assert.Contains("hexer", error.Message);
        Assert.Contains("needs Mag 99", error.Message);
    }

    [Fact]
    public void ReadIsRefusedForAUnitNotOnTheRosterOrAnEmptySlot()
    {
        Assert.Equal("no unit 'nobody' on the roster", WithPrimer().Read("nobody", 0, Learning).Text);
        Assert.Equal("pell has no item in slot 9", WithPrimer().Read("pell", 8, Learning).Text);
    }

    private static string StartingAlone(string header) =>
        File.ReadAllText(Path.Combine(Fixture.RealContentDirectory(), "maps", "starting_alone.map")).Replace("enemy_level: 1\n", "enemy_level: 1\n" + header);

    [Fact]
    public void ADropsEnemyThatDiesSendsItsTomesToTheWagon()
    {
        var map = MapFormat.Parse("drops.map", StartingAlone("drops: 8,2\n"), Shipped);
        Assert.Equal(new[] { new Coord(8, 2) }, map.Drops);
        Assert.Equal(map, MapFormat.Parse("again.map", MapFormat.Write(map, Shipped), Shipped));

        for (ulong seed = 1; seed < 200; seed++)
        {
            var state = BattleState.From(map, Shipped, Shipped.Cast, seed);
            var hexer = state.Units.Single(u => u.Unit.ClassId == "adept" && u.Side == Side.Enemy);
            state = state.WithUnit(hexer with { Hp = 1 }).WithUnit(state.Find("captain")! with { At = new Coord(8, 3) });
            var result = Resolver.Apply(state, Shipped, new Attack("captain", hexer.Id));
            if (result.Next.Find(hexer.Id) is null)
            {
                Assert.Contains(new TomeDropped(hexer.Id, ValueList<string>.Of("cinder")), result.Events);
                Assert.Equal(new[] { "cinder" }, result.Next.Wagon);
                return;
            }
        }

        throw new InvalidOperationException("no seed under 200 killed the hexer");
    }

    [Fact]
    public void AnEnemyOffTheDropsTilesDropsNothing()
    {
        var map = MapFormat.Parse("plain.map", StartingAlone(""), Shipped);
        var state = BattleState.From(map, Shipped, Shipped.Cast, 1246);
        var hexer = state.Units.Single(u => u.Unit.ClassId == "adept" && u.Side == Side.Enemy);
        var events = new List<GameEvent>();

        Assert.Same(state, TomeDrop.After(state, hexer, Shipped, events));
        Assert.Empty(events);
    }

    [Theory]
    [InlineData("drops: 3,3\n", "no E line places an enemy")]
    [InlineData("drops: 11,6\n", "carries no Lore tome")]
    [InlineData("drops: 8,2 8,2\n", "twice")]
    [InlineData("drops: 8\n", "needs the tiles")]
    public void ABadDropsHeaderIsRefusedNamingTheLine(string header, string why)
    {
        var error = Assert.Throws<MapException>(() => MapFormat.Parse("drops.map", StartingAlone(header), Shipped));

        Assert.Contains(why, error.Message);
    }
}
