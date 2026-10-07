using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The enemy's earth casts (issue 1286, DECISIONS/0316): an earth-shaper lays Rampart under an ally that stays where
/// it stands and a player unit can strike, in place of any strike that is not a kill; an armor caster dons its armor
/// only in a phase it has no strike, when a player unit can strike the tile it ends on. No shipped class or tome
/// casts either, so these tests make the woods archer of <c>the_tollgate_frost.map</c> an Adept given earth, holding
/// Cinder and a fixture tome, <c>test_rampart</c> or <c>test_earth_armor</c>, in the enemy phase after Pell, holding
/// Cinder, steps to 6,7, two tiles from the woods brigand and three from the archer.
/// </summary>
public class EnemyEarthTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Weapon Cinder = Shipped.Weapon("cinder");

    private static readonly GameContent Earthen = Shipped with
    {
        Weapons = Shipped.Weapons
            .SetItem("test_rampart", Cinder with { Id = "test_rampart", Name = "Test Rampart", School = MagicSchool.Earth, Rider = RiderKind.Raise, Ignites = false, MinRange = 1, MaxRange = 2 })
            .SetItem("test_earth_armor", Cinder with { Id = "test_earth_armor", Name = "Test Earth Armor", School = MagicSchool.Earth, Rider = RiderKind.Armor, Armor = new ArmorSpell(10, 2, 2), Ignites = false }),
        Classes = Shipped.Classes.SetItem("adept", Shipped.Class("adept") with { Schools = Shipped.Class("adept").Schools.Add(MagicSchool.Earth) }),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private static readonly Coord Woods = new(6, 5);

    private static readonly Coord Archer = new(5, 5);

    /// <summary>
    /// The enemy phase with Pell at 6,7 and the woods archer an Adept holding Cinder and <paramref name="tome"/> in
    /// slot 1; the caster's id and the woods brigand's.
    /// </summary>
    private static (BattleState State, string Caster, string Brigand) EnemyPhase(string tome)
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, Earthen), Earthen, Earthen.Cast, 1286);
        var pell = state.Find("pell")!;
        state = state.WithUnit(pell with { At = new Coord(6, 7), Unit = pell.Unit with { Inventory = pell.Unit.Inventory.Replace(0, new ItemStack("cinder", Cinder.Durability)) } });
        var ended = Resolver.Apply(state, Earthen, new EndPhase());
        Assert.True(ended.Accepted, ended.Rejection?.Message);
        var next = ended.Next;
        var archer = next.UnitAt(Archer)!;
        var caster = archer with
        {
            Unit = archer.Unit with
            {
                ClassId = "adept",
                Stats = archer.Unit.Stats with { Mag = 12 },
                Inventory = Inventory.Empty.Add(new ItemStack("cinder", Cinder.Durability)).Add(new ItemStack(tome, 2)),
            },
        };
        return (next.WithUnit(caster), archer.Id, next.UnitAt(Woods)!.Id);
    }

    private static IReadOnlyList<Command> Plan(BattleState state, string caster) =>
        EnemyAi.PlanUnit(state, Earthen, state.Find(caster)!);

    private static BattleState Run(BattleState state, IEnumerable<Command> commands)
    {
        foreach (var command in commands)
        {
            var result = Resolver.Apply(state, Earthen, command);
            Assert.True(result.Accepted, result.Rejection?.Message);
            state = result.Next;
        }

        return state;
    }

    [Fact]
    public void AnEnemyEarthShaperLaysRampartUnderAnAllyAPlayerUnitCanStrike()
    {
        var (state, caster, brigand) = EnemyPhase("test_rampart");

        var plan = Plan(state, caster);

        Assert.Equal(new Command[] { new UseItem(caster, 1, brigand) }, plan);
        var next = Run(state, plan);
        Assert.Equal("earthwork", next.Map.TerrainIdAt(Woods));
        var overlay = Earthwork.At(next, Woods)!;
        Assert.Equal((caster, Side.Enemy), (overlay.OwnerId, overlay.Side));
    }

    [Fact]
    public void AnEnemyEarthShaperLaysRampartInPlaceOfAStrikeThatDoesNotKill()
    {
        var (state, caster, brigand) = EnemyPhase("test_rampart");
        state = state.WithUnit(state.Find("pell")! with { At = new Coord(5, 7) });

        var plan = Plan(state, caster);

        Assert.Equal(new Command[] { new UseItem(caster, 1, brigand) }, plan);
    }

    [Fact]
    public void AKillBeatsARampart()
    {
        var (state, caster, _) = EnemyPhase("test_rampart");
        state = state.WithUnit(state.Find("pell")! with { At = new Coord(5, 7), Hp = 1 });

        var plan = Plan(state, caster);

        Assert.IsType<Attack>(plan[^1]);
    }

    [Fact]
    public void AnAllyThatMayStillMoveGetsNoRampartUntilItHasMoved()
    {
        var (state, caster, brigand) = EnemyPhase("test_rampart");
        var unit = state.Find(brigand)!;
        state = state.WithUnit(unit with { Behavior = Behavior.Aggressive });

        var before = Plan(state, caster);
        var after = Plan(state.WithUnit(state.Find(brigand)! with { Moved = true }), caster);

        Assert.DoesNotContain(before, c => c is UseItem);
        Assert.Equal(new Command[] { new UseItem(caster, 1, brigand) }, after);
    }

    [Fact]
    public void AnAllyOffOpenGroundGetsNoRampart()
    {
        var (state, caster, _) = EnemyPhase("test_rampart");
        state = state with { Map = state.Map.WithTerrain(Woods, "fort") };

        var plan = Plan(state, caster);

        Assert.DoesNotContain(plan, c => c is UseItem);
    }

    [Fact]
    public void AnEnemyEarthShaperRefreshesItsOwnRampartAndNeverTakesAnothersTile()
    {
        var (state, caster, brigand) = EnemyPhase("test_rampart");
        var laid = Run(state, Plan(state, caster));
        var own = laid.WithUnit(laid.Find(caster)! with { Moved = false, Acted = false });
        var others = own with { Overlays = ValueList<TileOverlay>.From(own.Overlays.Select(o => o with { OwnerId = "someone-else" })) };

        Assert.Equal(new Command[] { new UseItem(caster, 1, brigand) }, Plan(own, caster));
        Assert.DoesNotContain(Plan(others, caster), c => c is UseItem);
    }

    [Fact]
    public void AnEnemyWithNoRampartTomeNeverLaysOne()
    {
        var (state, caster, _) = EnemyPhase("test_rampart");
        var unit = state.Find(caster)!;
        state = state.WithUnit(unit with { Unit = unit.Unit with { Inventory = Inventory.Empty.Add(new ItemStack("cinder", Cinder.Durability)) } });

        Assert.DoesNotContain(Plan(state, caster), c => c is UseItem);
    }

    [Fact]
    public void AnEnemyWithNoStrikeDonsItsArmorWhenAPlayerUnitCanStrikeIt()
    {
        var (state, caster, _) = EnemyPhase("test_earth_armor");

        var plan = Plan(state, caster);

        Assert.Equal(new Command[] { new UseItem(caster, 1, null) }, plan);
        var worn = Run(state, plan).Find(caster)!.Armor!;
        Assert.Equal(("test_earth_armor", 10, 2), (worn.SpellId, worn.Def, worn.Mov));
    }

    [Fact]
    public void AnEnemyWithAStrikeStrikesAndNeverDonsInstead()
    {
        var (state, caster, _) = EnemyPhase("test_earth_armor");
        state = state.WithUnit(state.Find("pell")! with { At = new Coord(5, 7) });

        var plan = Plan(state, caster);

        Assert.IsType<Attack>(plan[^1]);
    }

    [Fact]
    public void AnEnemyNeverDonsArmorItWearsOrWhereNoPlayerUnitCanStrikeIt()
    {
        var (state, caster, _) = EnemyPhase("test_earth_armor");
        var unit = state.Find(caster)!;
        var wearing = state.WithUnit(unit with { Armor = new ArmorMark("test_earth_armor", 10, 2, 1) });

        Assert.DoesNotContain(Plan(wearing, caster), c => c is UseItem);
        Assert.Null(EnemyAi.Don(state, Earthen, unit, new Coord(0, 4)));
        Assert.NotNull(EnemyAi.Don(state, Earthen, unit, unit.At));
    }

    [Fact]
    public void TheWholeEnemyPhasePlansTheRampartLegallyAndItStandsThroughThePlayerPhase()
    {
        var (state, caster, brigand) = EnemyPhase("test_rampart");

        var plan = EnemyAi.Plan(state, Earthen);

        Assert.Contains(new UseItem(caster, 1, brigand), plan);
        var next = Run(state, plan);
        Assert.Equal(Side.Player, next.Phase);
        Assert.Equal("earthwork", next.Map.TerrainIdAt(next.Find(brigand)!.At));
    }
}
