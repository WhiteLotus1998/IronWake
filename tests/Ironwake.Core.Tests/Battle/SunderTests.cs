using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Earth's Sunder (issue 1281, Lotus's #1247 rulings, DECISIONS/0307): a tome naming <c>sunder</c> on a school whose
/// rider is raise drops a raised tile through the Item action, either side's, never a map's fort; an attack with it
/// that hits a flier still standing grounds it and stuns it for its side's next phase, a boss grounded and spared
/// the stun. No shipped tome names it, so these tests use a fixture tome, <c>test_sunder</c>, with two uses, in
/// Pell's hands on the sample <c>the_tollgate_frost.map</c>, her class given earth.
/// </summary>
public class SunderTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Weapon Cinder = Shipped.Weapon("cinder");

    private static readonly GameContent Sundering = Shipped with
    {
        Weapons = Shipped.Weapons.SetItem("test_sunder", Cinder with { Id = "test_sunder", Name = "Test Sunder", School = MagicSchool.Earth, Rider = RiderKind.Sunder, Ignites = false }),
        Classes = Shipped.Classes.SetItem("adept", Shipped.Class("adept") with { Schools = ValueList<MagicSchool>.Of(MagicSchool.Fire, MagicSchool.Ice, MagicSchool.Lightning, MagicSchool.Earth) }),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private static readonly Coord TeodorAt = new(7, 11);

    private const int Uses = 2;

    /// <summary>The frost sample with Pell (6,10) holding <c>test_sunder</c> in slot 0.</summary>
    private static BattleState Board(ulong seed = 1281)
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, Sundering), Sundering, Sundering.Cast, seed);
        var pell = state.Find("pell")!;
        return state.WithUnit(pell with { Unit = pell.Unit with { Inventory = pell.Unit.Inventory.Replace(0, new ItemStack("test_sunder", Uses)) } });
    }

    /// <summary><paramref name="state"/> with earthwork raised by <paramref name="owner"/> of <paramref name="side"/> at <paramref name="at"/>.</summary>
    private static BattleState Raised(BattleState state, Coord at, string owner = "wren", Side side = Side.Player) =>
        state with
        {
            Overlays = ValueList<TileOverlay>.Of(new TileOverlay(at, "earthwork", state.Map.TerrainIdAt(at), owner, side, 1)),
            Map = state.Map.WithTerrain(at, "earthwork"),
        };

    private static ApplyResult Sunder(BattleState state, string target) =>
        Resolver.Apply(state, Sundering, new UseItem("pell", 0, target));

    private static readonly string BrigandId = Board().Units.Single(u => u.Side == Side.Enemy && u.Group == "woods" && u.Unit.ClassId == Shipped.Unit("toll_brigand").ClassId).Id;

    private static BattleUnit Brigand(BattleState state) => state.Find(BrigandId)!;

    /// <summary>The woods brigand made a sturdy skyrider (a boss when <paramref name="boss"/>), Pell two tiles below it at 6,7.</summary>
    private static BattleState Facing(ulong seed, bool flier = true, bool boss = false)
    {
        var state = Board(seed);
        var brigand = Brigand(state);
        var unit = brigand.Unit with { ClassId = flier ? "skyrider" : brigand.Unit.ClassId, Stats = brigand.Unit.Stats with { Hp = 80 } };
        return state.WithUnit(brigand with { Unit = unit, Hp = 80, IsBoss = boss }).WithUnit(state.Find("pell")! with { At = new Coord(6, 7) });
    }

    /// <summary>The first seed under 400 whose attack by Pell lands a hit on the brigand, and its board and result.</summary>
    private static (BattleState Before, ApplyResult Result) Struck(bool flier = true, bool boss = false)
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = Facing(seed, flier, boss);
            var result = Resolver.Apply(state, Sundering, new Attack("pell", Brigand(state).Id));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (result.Next.Find(Brigand(state).Id) is not null && result.Events.OfType<CombatFought>().Single().Strikes.Any(s => s.AttackerId == "pell" && s.Hit))
            {
                return (state, result);
            }
        }

        Assert.Fail("no seed under 400 gave a surviving hit");
        return default;
    }

    [Fact]
    public void ATomeMayNameSunderOnlyOnASchoolWhoseRiderIsRaise()
    {
        Assert.True(SchoolRider.Borrows(RiderKind.Sunder, RiderKind.Raise));
        Assert.False(SchoolRider.Borrows(RiderKind.Sunder, RiderKind.Burn));
        Assert.Equal(Shipped.Riders[MagicSchool.Earth] with { Kind = RiderKind.Sunder }, Sundering.RiderOf(Sundering.Weapon("test_sunder")));
        Assert.Null(Sundering.RiderOf(Cinder with { Rider = RiderKind.Sunder }));
    }

    [Fact]
    public void AnEarthTomeNamingSunderLoadsAndRoundTrips()
    {
        var files = ContentSerializer.Write(Shipped);
        files = files with
        {
            Weapons = new ContentFile(files.Weapons.Name, files.Weapons.Text.Replace("\"school\": \"fire\"", "\"school\": \"earth\", \"rider\": \"sunder\"")),
            Classes = new ContentFile(files.Classes.Name, System.Text.RegularExpressions.Regex.Replace(files.Classes.Text, "\"fire\",\\s*\"ice\",\\s*\"lightning\"\\s*\\]", "\"fire\", \"ice\", \"lightning\", \"earth\"]")),
        };
        var content = ContentLoader.Parse(files);

        Assert.Equal(RiderKind.Sunder, content.Weapon("cinder").Rider);
        Assert.True(Core.Sunder.Sunders(content, content.Weapon("cinder")));
        Assert.Equal(RiderKind.Sunder, ContentLoader.Parse(ContentSerializer.Write(content)).Weapon("cinder").Rider);
    }

    [Fact]
    public void AFireTomeNamingSunderIsRefusedAtLoad()
    {
        var files = ContentSerializer.Write(Shipped);
        files = files with { Weapons = new ContentFile(files.Weapons.Name, files.Weapons.Text.Replace("\"school\": \"fire\"", "\"school\": \"fire\", \"rider\": \"sunder\"")) };

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(files));

        Assert.Contains("cinder", error.Message);
        Assert.Contains("'sunder' but the fire school's rider is 'burn'", error.Message);
    }

    [Fact]
    public void ASchoolsOwnRiderIsNeverASunder()
    {
        var rules = """{ "wakeRadius": 4, "schools": { "earth": { "rider": { "kind": "sunder" } } } }""";

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(rules: rules)));

        Assert.Contains("schools.earth", error.Message);
        Assert.Contains("never a school's own rider", error.Message);
    }

    [Fact]
    public void SunderDropsRaisedGroundWithAUnitOnItAndSpendsAUseAndTheAction()
    {
        var state = Raised(Board(), TeodorAt);

        var result = Sunder(state, "teodor");

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(new ItemUsed("pell", "test_sunder", "teodor", Uses - 1), result.Events);
        Assert.Contains(new GroundSundered("pell", TeodorAt, "earthwork", "wren"), result.Events);
        Assert.Contains(new TerrainChanged(TeodorAt, "plain"), result.Events);
        Assert.Equal("plain", result.Next.Map.TerrainIdAt(TeodorAt));
        Assert.Empty(result.Next.Overlays);
        Assert.Equal(TeodorAt, result.Next.Find("teodor")!.At);
        var pell = result.Next.Find("pell")!;
        Assert.True(pell.Moved && pell.Acted);
        Assert.Equal(Uses - 1, pell.Unit.Inventory.Items[0].Uses);
    }

    [Fact]
    public void SunderDropsAnEnemysRaisedGroundNamedByTheTile()
    {
        var at = new Coord(6, 9);
        var state = Raised(Board(), at, "hexer-1", Side.Enemy);

        var result = Sunder(state, "6,9");

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(new GroundSundered("pell", at, "earthwork", "hexer-1"), result.Events);
        Assert.Equal(Board().Map.TerrainIdAt(at), result.Next.Map.TerrainIdAt(at));
        Assert.Empty(result.Next.Overlays);
    }

    [Fact]
    public void SunderRefusesAMapsFortAndGroundNeverRaised()
    {
        var fort = new Coord(10, 7);
        var state = Board();
        state = state.WithUnit(state.Find("pell")! with { At = new Coord(10, 9) });

        var onFort = Sunder(state, "10,7");
        var onPlain = Sunder(Board(), "teodor");

        Assert.Equal("fort", state.Map.TerrainIdAt(fort));
        Assert.False(onFort.Accepted);
        Assert.Contains("Fort at 10,7 was not raised", onFort.Rejection!.Message);
        Assert.False(onPlain.Accepted);
        Assert.Contains("drops only raised ground", onPlain.Rejection!.Message);
    }

    [Fact]
    public void SunderRefusesATileOutOfRangeOrOffTheMap()
    {
        var far = Raised(Board(), new Coord(6, 3));

        var outOfRange = Sunder(far, "6,3");
        var offMap = Sunder(Board(), "99,99");

        Assert.False(outOfRange.Accepted);
        Assert.Equal(RejectionReason.OutOfRange, outOfRange.Rejection!.Reason);
        Assert.False(offMap.Accepted);
        Assert.Equal(RejectionReason.NoSuchTarget, offMap.Rejection!.Reason);
    }

    [Fact]
    public void SunderRefusesWithNoUsesLeft()
    {
        var state = Raised(Board(), TeodorAt);
        var pell = state.Find("pell")!;
        state = state.WithUnit(pell with { Unit = pell.Unit with { Inventory = pell.Unit.Inventory.Replace(0, new ItemStack("test_sunder", 0)) } });

        var result = Sunder(state, "teodor");

        Assert.False(result.Accepted);
        Assert.Contains("no uses left", result.Rejection!.Message);
    }

    [Fact]
    public void ASunderHitGroundsAFlierAndStunsItForItsSidesNextPhase()
    {
        var (before, result) = Struck();
        var id = Brigand(before).Id;

        Assert.Contains(new UnitGrounded(id, "pell", Side.Enemy), result.Events);
        Assert.Contains(new UnitStunned(id, "pell", Side.Enemy), result.Events);
        var after = result.Next.Find(id)!;
        Assert.Equal((1, 1), (after.Grounded, after.Stun));
        Assert.False(result.Next.Find("pell")!.StunSpent);

        var enemy = Resolver.Apply(result.Next, Sundering, new EndPhase());
        Assert.Contains(new StunSkipped(id), enemy.Events);
        Assert.True(Stun.Skipping(enemy.Next.Find(id)!));
        Assert.Equal(MovementType.Infantry, Grounding.MovementOf(enemy.Next.Find(id)!, Sundering));
    }

    [Fact]
    public void ASunderHitGroundsAFlyingBossAndSparesItTheStun()
    {
        var (before, result) = Struck(boss: true);
        var id = Brigand(before).Id;

        Assert.Contains(new UnitGrounded(id, "pell", Side.Enemy), result.Events);
        Assert.DoesNotContain(result.Events, e => e is UnitStunned);
        Assert.Equal((1, 0), (result.Next.Find(id)!.Grounded, result.Next.Find(id)!.Stun));
    }

    [Fact]
    public void ASunderHitOnAUnitThatDoesNotFlyIsOnlyTheHit()
    {
        var (before, result) = Struck(flier: false);
        var after = result.Next.Find(Brigand(before).Id)!;

        Assert.DoesNotContain(result.Events, e => e is UnitGrounded or UnitStunned);
        Assert.Equal((0, 0), (after.Grounded, after.Stun));
    }

    [Fact]
    public void TheForecastPrintsBothEffectsAgainstAFlier()
    {
        var board = Facing(1);
        var pell = board.Find("pell")!;
        var brigand = Brigand(board);

        Assert.Equal(" grounds and stuns", PlaySession.Riders(Sundering, pell, brigand, null).Riders);
        Assert.Equal(" grounds (stun: bosses spared)", PlaySession.Riders(Sundering, pell, brigand with { IsBoss = true }, null).Riders);
        Assert.Equal("", PlaySession.Riders(Sundering, pell, Brigand(Facing(1, flier: false)), null).Riders);
    }

    [Fact]
    public void RecallRestoresTheDroppedGroundAndTheGroundedFlier()
    {
        var dropped = Sunder(Raised(Board(), TeodorAt), "teodor");
        var back = Resolver.Apply(dropped.Next, Sundering, new Recall(0));

        Assert.True(back.Accepted, back.Rejection?.Message);
        Assert.Equal("earthwork", back.Next.Map.TerrainIdAt(TeodorAt));
        Assert.Single(back.Next.Overlays);

        var (before, struck) = Struck();
        var restored = Resolver.Apply(struck.Next, Sundering, new Recall(0));
        Assert.True(restored.Accepted, restored.Rejection?.Message);
        Assert.Equal((0, 0), (restored.Next.Find(Brigand(before).Id)!.Grounded, restored.Next.Find(Brigand(before).Id)!.Stun));
    }

    [Fact]
    public void TheEventsPrintAndSerialize()
    {
        Assert.Equal(
            """{"type":"groundSundered","unit":"pell","at":{"x":7,"y":11},"terrain":"earthwork","owner":"wren"}""",
            ProtocolJson.Event(new GroundSundered("pell", TeodorAt, "earthwork", "wren")));
        var dropped = Sunder(Raised(Board(), TeodorAt), "teodor");
        var names = UnitNames.Of(dropped.Next, Sundering);
        Assert.Contains("Pell sunders the earthwork Wren raised at 7,11: the ground falls back", dropped.Events.Select(e => PlaySession.Describe(e, Sundering, names)));
    }
}
