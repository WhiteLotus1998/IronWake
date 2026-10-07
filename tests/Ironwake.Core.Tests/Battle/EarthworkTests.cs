using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Earth's raise rider (issue 1245, DECISIONS/0300): a tome naming it is cast through the Item action on
/// an ally in range and lays earthwork (a fort's cover, no heal) on the ally's tile as a timed overlay until
/// the caster's next phase ends. One per caster; never on a fort, gate, water, wall, mountain, fire, planks
/// or ice; held by whoever stands on it. No shipped tome names the rider, so these tests use a fixture tome,
/// <c>test_cairn</c>, in Pell's hands on the sample <c>the_tollgate_frost.map</c>, her class given earth.
/// </summary>
public class EarthworkTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Weapon Cinder = Shipped.Weapon("cinder");

    private static readonly GameContent Raising = Shipped with
    {
        Weapons = Shipped.Weapons.SetItem("test_cairn", Cinder with { Id = "test_cairn", Name = "Test Cairn", School = MagicSchool.Earth, Rider = RiderKind.Raise, Ignites = false }),
        Classes = Shipped.Classes.SetItem("adept", Shipped.Class("adept") with { Schools = ValueList<MagicSchool>.Of(MagicSchool.Fire, MagicSchool.Ice, MagicSchool.Lightning, MagicSchool.Earth) }),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private static readonly Coord TeodorAt = new(7, 11);

    /// <summary>The frost sample with Pell (6,10) holding <c>test_cairn</c> in slot 0; Teodor stands at 7,11, two tiles off.</summary>
    private static BattleState Board(GameContent? content = null)
    {
        content ??= Raising;
        var state = BattleState.From(MapFiles.Load(SamplePath, content), content, content.Cast, 1245);
        var pell = state.Find("pell")!;
        return state.WithUnit(pell with { Unit = pell.Unit with { Inventory = pell.Unit.Inventory.Replace(0, new ItemStack("test_cairn", Cinder.Durability)) } });
    }

    private static ApplyResult Raise(BattleState state, string target, GameContent? content = null) =>
        Resolver.Apply(state, content ?? Raising, new UseItem("pell", 0, target));

    private static BattleState Fresh(BattleState state) => state.WithUnit(state.Find("pell")! with { Moved = false, Acted = false });

    [Fact]
    public void EarthsRiderShipsAsRaiseEarthworkAndNoShippedTomeNamesIt()
    {
        Assert.Equal(new SchoolRider(RiderKind.Raise, 0, 0) { Terrain = "earthwork" }, Shipped.Riders[MagicSchool.Earth]);
        Assert.All(Shipped.Weapons.Values, w => Assert.Null(w.Rider));
        var earthwork = Shipped.TerrainById("earthwork");
        var fort = Shipped.TerrainById("fort");
        Assert.Equal((fort.Avoid, fort.Def, fort.Res, fort.AppliesToFlyers), (earthwork.Avoid, earthwork.Def, earthwork.Res, earthwork.AppliesToFlyers));
        Assert.Equal((0, 0), (earthwork.HealPercent, earthwork.BurnPercent));
    }

    [Fact]
    public void ARaiseCastLaysEarthworkUnderTheAllyAndSpendsAUseAndTheAction()
    {
        var result = Raise(Board(), "teodor");

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(new ItemUsed("pell", "test_cairn", "teodor", Cinder.Durability - 1), result.Events);
        Assert.Contains(new GroundRaised("pell", "teodor", TeodorAt, "earthwork"), result.Events);
        Assert.Contains(new TerrainChanged(TeodorAt, "earthwork"), result.Events);
        Assert.Equal("earthwork", result.Next.Map.TerrainIdAt(TeodorAt));
        Assert.Equal(new TileOverlay(TeodorAt, "earthwork", "plain", "pell", Side.Player, 1), Assert.Single(result.Next.Overlays));
        var pell = result.Next.Find("pell")!;
        Assert.True(pell.Moved && pell.Acted);
        Assert.Equal(Cinder.Durability - 1, pell.Unit.Inventory.Items[0].Uses);
    }

    [Fact]
    public void EarthworkLastsUntilTheCastersNextPhaseEndsThenGivesTheGroundBack()
    {
        var raised = Raise(Board(), "teodor").Next;

        var enemy = Resolver.Apply(raised, Raising, new EndPhase()).Next;
        Assert.Equal("earthwork", enemy.Map.TerrainIdAt(TeodorAt));
        var player = Resolver.Apply(enemy, Raising, new EndPhase()).Next;
        Assert.Equal("earthwork", player.Map.TerrainIdAt(TeodorAt));
        Assert.Equal(2, Assert.Single(player.Overlays).Clock);

        var fallen = Resolver.Apply(player, Raising, new EndPhase());
        Assert.Contains(new TerrainChanged(TeodorAt, "plain"), fallen.Events);
        Assert.Equal("plain", fallen.Next.Map.TerrainIdAt(TeodorAt));
        Assert.Empty(fallen.Next.Overlays);
    }

    [Fact]
    public void EarthworkGivesWhoeverStandsOnItAFortsCover()
    {
        var raised = Raise(Board(), "teodor").Next;
        var teodor = raised.Find("teodor")!;
        var movement = Raising.Class(teodor.Unit.ClassId).Movement;

        Assert.Equal(2, raised.Map.TerrainAt(TeodorAt, Raising).DefFor(movement));
        Assert.Equal(15, raised.Map.TerrainAt(TeodorAt, Raising).AvoidFor(movement));
        Assert.Equal(0, Board().Map.TerrainAt(TeodorAt, Raising).DefFor(movement));
    }

    [Fact]
    public void ASecondCastTakesTheFirstOneUp()
    {
        var first = Raise(Board(), "teodor").Next;
        var second = Raise(Fresh(first), "wren");

        Assert.True(second.Accepted, second.Rejection?.Message);
        Assert.Contains(new TerrainChanged(TeodorAt, "plain"), second.Events);
        Assert.Equal("plain", second.Next.Map.TerrainIdAt(TeodorAt));
        Assert.Equal(second.Next.Find("wren")!.At, Assert.Single(second.Next.Overlays).At);
    }

    [Fact]
    public void AnotherCastersEarthworkIsRefused()
    {
        var state = Board();
        state = state with { Overlays = ValueList<TileOverlay>.Of(new TileOverlay(TeodorAt, "earthwork", "plain", "wren", Side.Player, 1)), Map = state.Map.WithTerrain(TeodorAt, "earthwork") };

        var result = Raise(state, "teodor");

        Assert.False(result.Accepted);
        Assert.Contains("already raised by wren", result.Rejection!.Message);
    }

    [Theory]
    [InlineData(10, 7, 10, 9, "Fort")]
    [InlineData(1, 3, 1, 5, "Mountain")]
    [InlineData(6, 8, 6, 10, "Water")]
    public void EarthworkNeverRisesOnAFortAMountainOrWater(int x, int y, int px, int py, string name)
    {
        var state = Board();
        state = state.WithUnit(state.Find("teodor")! with { At = new Coord(x, y) }).WithUnit(state.Find("pell")! with { At = new Coord(px, py) });

        var result = Raise(state, "teodor");

        Assert.False(result.Accepted);
        Assert.Contains($"{name} at {x},{y} is not", result.Rejection!.Message);
        Assert.Empty(result.Next.Overlays);
    }

    [Fact]
    public void ARaiseIsRefusedOnAFoeOutOfRangeWithNoTargetOrOutsideTheCastersSchools()
    {
        var state = Board();
        var foe = state.Units.First(u => u.Side == Side.Enemy);

        Assert.Equal(RejectionReason.NotAnAlly, Raise(state.WithUnit(foe with { At = new Coord(6, 9) }), foe.Id).Rejection!.Reason);
        Assert.Equal(RejectionReason.OutOfRange, Raise(state.WithUnit(state.Find("wren")! with { At = new Coord(0, 11) }), "wren").Rejection!.Reason);
        Assert.Equal(RejectionReason.NoTarget, Resolver.Apply(state, Raising, new UseItem("pell", 0)).Rejection!.Reason);
        var unreached = Raising with { Classes = Shipped.Classes };
        Assert.Contains("needs the earth school", Raise(Board(unreached), "teodor", unreached).Rejection!.Message);
    }

    [Fact]
    public void ATomeOfEarthWithoutTheRiderNamedRaisesNothing()
    {
        var plain = Raising with { Weapons = Raising.Weapons.SetItem("test_cairn", Raising.Weapon("test_cairn") with { Rider = null }) };

        var result = Raise(Board(plain), "teodor", plain);

        Assert.False(result.Accepted);
        Assert.Empty(result.Next.Overlays);
    }

    [Fact]
    public void AFallenCastersEarthworkLastsOutItsClock()
    {
        var raised = Raise(Board(), "teodor").Next.WithoutUnit("pell");

        var player = Resolver.Apply(Resolver.Apply(raised, Raising, new EndPhase()).Next, Raising, new EndPhase()).Next;

        Assert.Equal("earthwork", player.Map.TerrainIdAt(TeodorAt));
        Assert.Equal("pell", Assert.Single(player.Overlays).OwnerId);
    }

    [Fact]
    public void TheBoardAndTheTerrainCardSayWhoRaisedItAndWhenItFalls()
    {
        var raised = Raise(Board(), "teodor").Next;

        Assert.Equal("earthwork ([): 7,11 raised by pell, falls as the next player phase ends", Earthwork.Line(raised, Raising));
        Assert.Contains("earthwork ([): 7,11 raised by pell", MapRenderer.Render(raised, Raising));
        var card = TerrainCard.Text(raised, Raising, "earthwork");
        Assert.Contains("7,11 raised by pell, falls as the next player phase ends.", card);
        Assert.Contains("+2 Def, +2 Res", card);
        Assert.Null(Earthwork.Line(Board(), Raising));
    }

    [Fact]
    public void OverlaysRoundTripThroughTheProtocolState()
    {
        var raised = Raise(Board(), "teodor").Next;

        var back = ProtocolJson.ReadState(ProtocolJson.State(raised, Raising), Raising);

        Assert.Equal(raised.Overlays, back.Overlays);
        Assert.Equal("earthwork", back.Map.TerrainIdAt(TeodorAt));
        Assert.Contains("overlays 7,11/earthwork/plain/pell/Player/1", raised.Canonical());
    }

    private const string TwoTerrains = """
        { "terrain": [
          { "id": "plain", "name": "Plain", "glyph": ".", "cost": { "infantry": 1, "cavalry": 1, "flying": 1, "armored": 1 } },
          { "id": "water", "name": "Water", "glyph": "~", "cost": { "infantry": null, "cavalry": null, "flying": 1, "armored": null } }
        ] }
        """;

    [Theory]
    [InlineData("\"kind\": \"raise\" }", "terrain", "is required")]
    [InlineData("\"kind\": \"raise\", \"terrain\": \"rampart\" }", "rider.terrain", "unknown terrain 'rampart'")]
    [InlineData("\"kind\": \"raise\", \"terrain\": \"water\" }", "rider.terrain", "every unit can stand on")]
    [InlineData("\"kind\": \"raise\", \"terrain\": \"plain\", \"amount\": 1 }", "rider.amount", "is not a field of a raise rider")]
    public void ABadRaiseRiderIsRefusedAtLoadNamingFileEntryAndField(string rider, string field, string why)
    {
        var rules = "{ \"wakeRadius\": 4, \"schools\": { \"earth\": { \"rider\": { " + rider + " } } }";

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(terrain: TwoTerrains, rules: rules)));

        Assert.Contains(ContentFiles.RulesName, error.Message);
        Assert.Contains("schools.earth", error.Message);
        Assert.Contains(field, error.Message);
        Assert.Contains(why, error.Message);
    }

    [Fact]
    public void ARaiseRiderRoundTripsThroughTheSerializer()
    {
        var content = ContentLoader.Parse(Fixture.Files(rules: "{ \"wakeRadius\": 4, \"schools\": { \"earth\": { \"rider\": { \"kind\": \"raise\", \"terrain\": \"plain\" } } } }"));
        var again = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.Equal("plain", again.Riders[MagicSchool.Earth].Terrain);
        Assert.Equal(content.Riders, again.Riders);
    }
}
