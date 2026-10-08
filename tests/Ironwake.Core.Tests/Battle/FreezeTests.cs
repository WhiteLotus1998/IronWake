using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Still Water's second use, the freeze (issue 1330, DECISIONS/0328): a hit (not a miss, not a kill) from a tome naming
/// <c>freeze</c>, borrowing ice's chill rider, on a unit standing on a Water tile or orthogonally beside one, freezes it:
/// Mov 0 through its side's next phase on the chill's clock, a boss Mov 1. Off water the hit is only a hit. Light's
/// cleanse clears it. No shipped tome names it until Lotus signs #1247, so these tests give Pell a fixture tome,
/// <c>test_still_water</c>, on the sample <c>the_tollgate_frost.map</c>, against the woods brigand at 6,5, with Water laid
/// where each test needs it.
/// </summary>
public class FreezeTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Weapon Cinder = Shipped.Weapon("cinder");

    private static readonly GameContent Freezes = Shipped with
    {
        Weapons = Shipped.Weapons
            .SetItem("test_still_water", Cinder with { Id = "test_still_water", Name = "Test Still Water", School = MagicSchool.Ice, Rider = RiderKind.Freeze, Ignites = false }),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private static readonly Coord BrigandAt = new(6, 5);

    /// <summary>Pell holding the fixture tome two tiles below the woods brigand, with Water laid on <paramref name="water"/>.</summary>
    private static BattleState Facing(ulong seed, params Coord[] water)
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, Freezes), Freezes, Freezes.Cast, seed);
        var pell = state.Find("pell")!;
        var unit = pell.Unit with { Inventory = pell.Unit.Inventory.Replace(0, new ItemStack("test_still_water", Cinder.Durability)) };
        state = state.WithUnit(pell with { Unit = unit, At = new Coord(6, 7) });
        foreach (var at in water)
        {
            state = state with { Map = state.Map.WithTerrain(at, Freeze.WaterTerrainId) };
        }

        return state;
    }

    /// <summary>The brigand made a skyrider, since only a flier stands on Water.</summary>
    private static BattleState Flier(BattleState state)
    {
        var unit = Brigand(state).Unit with { ClassId = "skyrider" };
        return state.WithUnit(Brigand(state) with { Unit = unit, Hp = Freezes.StatsOf(unit).Hp });
    }

    private static BattleUnit Brigand(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Group == "woods" && u.At == BrigandAt);

    /// <summary>The first seed under 400 whose attack by Pell is a hit the brigand survives.</summary>
    private static (BattleState Before, ApplyResult Result) SurvivingHit(Func<BattleState, BattleState>? edit = null, params Coord[] water)
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = Facing(seed, water);
            state = edit?.Invoke(state) ?? state;
            var result = Resolver.Apply(state, Freezes, new Attack("pell", Brigand(state).Id));
            Assert.True(result.Accepted, result.Rejection?.Message);
            var hit = result.Events.OfType<CombatFought>().Single().Strikes.Any(s => s.AttackerId == "pell" && s.Hit);
            if (hit && result.Next.Find(Brigand(state).Id) is not null)
            {
                return (state, result);
            }
        }

        throw new InvalidOperationException("no seed under 400 gave a surviving hit");
    }

    [Fact]
    public void NoShippedTomeNamesTheFreezeAndItIsNeverASchoolsOwnRider()
    {
        Assert.DoesNotContain(Shipped.Weapons.Values, w => w.Rider == RiderKind.Freeze);
        Assert.DoesNotContain(Shipped.Riders.Values, r => r.Kind == RiderKind.Freeze);
        Assert.Equal(RiderKind.Freeze, Freezes.RiderOf(Freezes.Weapon("test_still_water"))!.Kind);
        Assert.Equal("freeze", SchoolRider.Label(RiderKind.Freeze));
    }

    [Fact]
    public void AHitOnAFlierStandingOnWaterFreezesIt()
    {
        var (before, result) = SurvivingHit(Flier, BrigandAt);
        var brigand = result.Next.Find(Brigand(before).Id)!;

        Assert.Equal(new UnitFrozen(brigand.Id, "pell", Side.Enemy, false), Assert.Single(result.Events.OfType<UnitFrozen>()));
        Assert.Equal(1, brigand.Frozen);
        Assert.Equal(0, result.Next.ReachOf(brigand, Freezes).Mov);
    }

    [Fact]
    public void AHitOnAUnitOrthogonallyBesideWaterFreezesIt()
    {
        var (before, result) = SurvivingHit(water: new Coord(7, 5));

        Assert.Equal(1, result.Next.Find(Brigand(before).Id)!.Frozen);
        Assert.Single(result.Events.OfType<UnitFrozen>());
    }

    [Fact]
    public void WaterOnlyDiagonalOrAwayFreezesNothingTheHitIsOnlyAHit()
    {
        var (before, diagonal) = SurvivingHit(water: new Coord(7, 4));
        Assert.Equal(0, diagonal.Next.Find(Brigand(before).Id)!.Frozen);
        Assert.DoesNotContain(diagonal.Events, e => e is UnitFrozen);

        var (_, dry) = SurvivingHit();
        Assert.DoesNotContain(dry.Events, e => e is UnitFrozen);
        Assert.False(Freeze.ByWater(Facing(1), BrigandAt));
        Assert.False(Freeze.ByWater(Facing(1, new Coord(7, 4)), BrigandAt));
        Assert.True(Freeze.ByWater(Facing(1, new Coord(6, 4)), BrigandAt));
    }

    [Fact]
    public void AMissOrAKillFreezesNothing()
    {
        var missed = false;
        var killed = false;
        for (ulong seed = 1; seed < 400 && !(missed && killed); seed++)
        {
            var state = Facing(seed, BrigandAt);
            var miss = Resolver.Apply(state, Freezes, new Attack("pell", Brigand(state).Id));
            if (!miss.Events.OfType<CombatFought>().Single().Strikes.Any(s => s.AttackerId == "pell" && s.Hit))
            {
                Assert.DoesNotContain(miss.Events, e => e is UnitFrozen);
                missed = true;
            }

            var weak = state.WithUnit(Brigand(state) with { Hp = 1 });
            var kill = Resolver.Apply(weak, Freezes, new Attack("pell", Brigand(weak).Id));
            if (kill.Next.Find(Brigand(weak).Id) is null)
            {
                Assert.DoesNotContain(kill.Events, e => e is UnitFrozen);
                killed = true;
            }
        }

        Assert.True(missed && killed);
    }

    [Fact]
    public void AFrozenBossKeepsMovOne()
    {
        var (before, result) = SurvivingHit(s => s.WithUnit(Brigand(s) with { IsBoss = true }), new Coord(7, 5));
        var boss = result.Next.Find(Brigand(before).Id)!;

        Assert.Equal(new UnitFrozen(boss.Id, "pell", Side.Enemy, true), Assert.Single(result.Events.OfType<UnitFrozen>()));
        Assert.Equal(Freeze.BossMov, result.Next.ReachOf(boss, Freezes).Mov);
        Assert.Equal(0, Freeze.Mov(5, boss with { IsBoss = false }));
        Assert.Equal(1, Freeze.Mov(5, boss));
        Assert.Equal(5, Freeze.Mov(5, boss with { Frozen = 0 }));
    }

    [Fact]
    public void TheFreezeLastsThroughItsSidesNextPhaseAndClearsWhenItEnds()
    {
        var (before, result) = SurvivingHit(water: new Coord(7, 5));
        var id = Brigand(before).Id;

        var enemyPhase = Resolver.Apply(result.Next, Freezes, new EndPhase()).Next;
        Assert.Equal(Side.Enemy, enemyPhase.Phase);
        Assert.Equal(2, enemyPhase.Find(id)!.Frozen);
        Assert.Equal(0, enemyPhase.ReachOf(enemyPhase.Find(id)!, Freezes).Mov);

        var after = Resolver.Apply(enemyPhase, Freezes, new EndPhase()).Next;
        Assert.Equal(0, after.Find(id)!.Frozen);
        Assert.True(after.ReachOf(after.Find(id)!, Freezes).Mov > 0);
    }

    [Fact]
    public void LightsCleanseClearsAFreeze()
    {
        var frozen = Brigand(Facing(1)) with { Frozen = 1 };
        Assert.True(Cleanse.Afflicted(frozen));

        var events = new List<GameEvent>();
        var cleared = Cleanse.Clear(frozen, "mira", events);

        Assert.Equal(0, cleared.Frozen);
        Assert.Equal(new UnitCleansed(frozen.Id, "mira", false, false, false, false, Frozen: true), Assert.Single(events));
        Assert.EndsWith("the freeze cleared", PlaySession.Describe(Assert.Single(events), Freezes, UnitNames.None));
    }

    [Fact]
    public void TheForecastTheCardAndTheLinesSayFreezeNeverRoot()
    {
        var wet = Facing(1, BrigandAt);
        var dry = Facing(1);
        var pell = wet.Find("pell")!;
        var brigand = Brigand(wet);

        var lines = new[]
        {
            PlaySession.Riders(Freezes, pell, brigand, null, null, wet).Riders,
            PlaySession.Riders(Freezes, pell, brigand, null, null, dry).Riders,
            PlaySession.Riders(Freezes, pell, brigand with { IsBoss = true }, null, null, wet).Riders,
            Freeze.CardLine(brigand with { Frozen = 1 })!,
            Freeze.CardLine(brigand with { Frozen = 2 })!,
            Freeze.CardLine(brigand with { Frozen = 1, IsBoss = true })!,
            PlaySession.Describe(new UnitFrozen(brigand.Id, "pell", Side.Enemy, false), Freezes, UnitNames.None),
            PlaySession.Describe(new UnitFrozen(brigand.Id, "pell", Side.Enemy, true, true), Freezes, UnitNames.None),
        };

        Assert.Equal(
            [
                " freezes",
                " no water: no freeze",
                " freezes: boss Mov 1",
                "frozen: cannot move next phase",
                "frozen: cannot move this phase",
                "frozen: Mov 1 next phase",
                "Toll_brigand-1 is frozen: cannot move until enemy phase ends",
                "Toll_brigand-1 is frozen: Mov 1 until the next enemy phase ends",
            ],
            lines);
        Assert.Null(Freeze.CardLine(brigand));
        Assert.DoesNotContain(lines, l => l.Contains("root", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheFreezeIsBoardStateTheProtocolCarries()
    {
        var state = Facing(1);
        state = state.WithUnit(Brigand(state) with { Frozen = 2 });
        var again = Brigand(ProtocolJson.ReadState(ProtocolJson.State(state, Freezes), Freezes));

        Assert.Equal(2, again.Frozen);
    }

    [Fact]
    public void AFreezeAsASchoolsOwnRiderIsRefusedAtLoadNamingFileEntryAndField()
    {
        var rules = """{ "wakeRadius": 4, "schools": { "ice": { "rider": { "kind": "freeze" } } } }""";
        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(rules: rules)));

        Assert.Contains(ContentFiles.RulesName, error.Message);
        Assert.Contains("schools.ice", error.Message);
        Assert.Contains("rider.kind", error.Message);
        Assert.Contains("a freeze is named by a tome on a school whose rider is chill", error.Message);
    }
}
