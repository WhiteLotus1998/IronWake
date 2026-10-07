using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Dark, the fifth school, and its drain rider (issue 1283, DECISIONS/0307): a hit from a tome naming
/// drain heals its caster, after the combat, by the HP its hits took off the target, up to max HP. No
/// shipped class reaches dark and no shipped tome names drain, so these tests give the Adept dark and
/// a fixture tome, <c>test_ledger</c>, to Pell on the sample <c>the_tollgate_frost.map</c>.
/// </summary>
public class DrainTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Weapon Cinder = Shipped.Weapon("cinder");

    private static readonly GameContent Drains = Shipped with
    {
        Weapons = Shipped.Weapons.SetItem("test_ledger", Cinder with { Id = "test_ledger", Name = "Test Ledger", School = MagicSchool.Dark, Rider = RiderKind.Drain, Ignites = false }),
        Classes = Shipped.Classes.SetItem("adept", Shipped.Class("adept") with { Schools = Shipped.Class("adept").Schools.Add(MagicSchool.Dark) }),
    };

    private static readonly GameContent Plain = Drains with
    {
        Weapons = Drains.Weapons.SetItem("test_ledger", Drains.Weapon("test_ledger") with { Rider = null }),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private static int PellMax => Drains.StatsOf(BattleState.From(MapFiles.Load(SamplePath, Drains), Drains, Drains.Cast, 1).Find("pell")!.Unit).Hp;

    /// <summary>Pell holding the ledger at <paramref name="hp"/>, two tiles below the woods brigand at 6,5, out of its axe's reach.</summary>
    private static BattleState Facing(ulong seed, int hp)
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, Drains), Drains, Drains.Cast, seed);
        var pell = state.Find("pell")!;
        var unit = pell.Unit with { Inventory = pell.Unit.Inventory.Replace(0, new ItemStack("test_ledger", Cinder.Durability)) };
        return state.WithUnit(pell with { Unit = unit, At = new Coord(6, 7), Hp = hp });
    }

    private static BattleUnit Brigand(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Group == "woods" && u.Unit.ClassId == Shipped.Unit("toll_brigand").ClassId);

    /// <summary>The first seed under 400 whose attack by Pell at <paramref name="hp"/> passes <paramref name="keep"/>, the board before it and its result.</summary>
    private static (BattleState Before, ApplyResult Result) Attacked(int hp, Func<BattleState, CombatFought, bool> keep, GameContent? content = null, Func<BattleState, BattleState>? edit = null)
    {
        content ??= Drains;
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = Facing(seed, hp);
            state = edit?.Invoke(state) ?? state;
            var result = Resolver.Apply(state, content, new Attack("pell", Brigand(state).Id));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (keep(state, result.Events.OfType<CombatFought>().Single()))
            {
                return (state, result);
            }
        }

        throw new InvalidOperationException("no seed under 400 gave the combat asked for");
    }

    private static bool PellHit(CombatFought fought) => fought.Strikes.Any(s => s.AttackerId == "pell" && s.Hit);

    [Fact]
    public void DarkIsASchoolWhoseShippedRiderIsTheDrainAndNoShippedTomeOrPlayerClassUsesIt()
    {
        Assert.Equal(new SchoolRider(RiderKind.Drain, 0, 0), Shipped.Riders[MagicSchool.Dark]);
        Assert.Equal("dark", MagicSchool.Dark.Label());
        Assert.Equal("drain", SchoolRider.Label(RiderKind.Drain));
        Assert.DoesNotContain(Shipped.Weapons.Values, w => w.School == MagicSchool.Dark || w.Rider == RiderKind.Drain);
        Assert.DoesNotContain(Shipped.Classes.Values, c => c.Reaches(MagicSchool.Dark) && !c.Enemy);
    }

    [Fact]
    public void AHitDrainsTheHpItTookOffTheTarget()
    {
        var (before, result) = Attacked(5, (_, f) => PellHit(f) && f.AttackerHpAfter == 5);
        var brigand = Brigand(before);
        var fought = result.Events.OfType<CombatFought>().Single();
        var taken = brigand.Hp - fought.TargetHpAfter;

        var drained = Assert.Single(result.Events.OfType<UnitDrained>());
        Assert.Equal(new UnitDrained("pell", brigand.Id, Math.Min(taken, PellMax - 5), Math.Min(PellMax, 5 + taken)), drained);
        Assert.Equal(drained.HpAfter, result.Next.Find("pell")!.Hp);
        Assert.True(taken > 0);
    }

    [Fact]
    public void TheDrainNeverHealsPastMaxHp()
    {
        var (_, full) = Attacked(PellMax, (_, f) => PellHit(f) && f.AttackerHpAfter == PellMax);
        Assert.DoesNotContain(full.Events, e => e is UnitDrained);
        Assert.Equal(PellMax, full.Next.Find("pell")!.Hp);

        var (_, nearly) = Attacked(PellMax - 1, (_, f) => PellHit(f) && f.AttackerHpAfter == PellMax - 1);
        Assert.Equal(1, Assert.Single(nearly.Events.OfType<UnitDrained>()).Amount);
        Assert.Equal(PellMax, nearly.Next.Find("pell")!.Hp);
    }

    [Fact]
    public void AMissDrainsNothing()
    {
        var (_, result) = Attacked(5, (_, f) => !PellHit(f));

        Assert.DoesNotContain(result.Events, e => e is UnitDrained);
    }

    [Fact]
    public void ATomeThatDoesNotNameTheDrainNeverDrains()
    {
        var (_, result) = Attacked(5, (_, f) => PellHit(f), Plain);

        Assert.DoesNotContain(result.Events, e => e is UnitDrained);
    }

    [Fact]
    public void AKillDrainsOnlyTheHpTheTargetHadLeft()
    {
        var (before, result) = Attacked(1, (s, f) => f.TargetHpAfter == 0, edit: s => s.WithUnit(Brigand(s) with { Hp = 2 }));

        Assert.Null(result.Next.Find(Brigand(before).Id));
        Assert.Equal(new UnitDrained("pell", Brigand(before).Id, 2, 3), Assert.Single(result.Events.OfType<UnitDrained>()));
    }

    [Theory]
    [InlineData(20, 8, 16)]
    [InlineData(12, 8, 12)]
    [InlineData(5, 8, 5)]
    [InlineData(0, 8, 0)]
    public void TakenCountsEachHitsFallInHpAndNothingPastTheLast(int hpBefore, int damage, int expected)
    {
        var strikes = ValueList<StrikeEvent>.Of(
            new StrikeEvent(0, "pell", "brigand-1", true, false, damage, Math.Max(0, hpBefore - damage)),
            new StrikeEvent(1, "brigand-1", "pell", true, false, 3, 10),
            new StrikeEvent(2, "pell", "brigand-1", true, false, damage, Math.Max(0, hpBefore - 2 * damage)));

        Assert.Equal(expected, Drain.Taken(strikes, "pell", "brigand-1", hpBefore));
        Assert.Equal(3, Drain.Taken(strikes, "brigand-1", "pell", 13));
    }

    [Fact]
    public void AMissedStrikeAddsNothingToTaken()
    {
        var strikes = ValueList<StrikeEvent>.Of(
            new StrikeEvent(0, "pell", "brigand-1", false, false, 8, 20),
            new StrikeEvent(1, "pell", "brigand-1", true, false, 8, 12));

        Assert.Equal(8, Drain.Taken(strikes, "pell", "brigand-1", 20));
    }

    [Fact]
    public void ACounterByADrainTomeDrains()
    {
        var state = Facing(1, 5);
        var pell = state.Find("pell")!;
        var brigand = Brigand(state);
        var strikes = ValueList<StrikeEvent>.Of(
            new StrikeEvent(0, brigand.Id, "pell", false, false, 4, 5),
            new StrikeEvent(1, "pell", brigand.Id, true, false, 6, brigand.Hp - 6));
        var events = new List<GameEvent>();

        var after = Drain.AfterCombat(state, Drains, brigand.Id, brigand.EquippedWeapon(Drains), "pell", Drains.Weapon("test_ledger"), strikes, events, brigand, pell);

        Assert.Equal(new UnitDrained("pell", brigand.Id, 6, 11), Assert.Single(events));
        Assert.Equal(11, after.Find("pell")!.Hp);
    }

    [Fact]
    public void ADrainerTheCombatKilledHealsNothing()
    {
        var state = Facing(1, 5);
        var pell = state.Find("pell")!;
        var brigand = Brigand(state);
        var strikes = ValueList<StrikeEvent>.Of(new StrikeEvent(0, "pell", brigand.Id, true, false, 6, brigand.Hp - 6));
        var events = new List<GameEvent>();

        var after = Drain.AfterCombat(state.WithoutUnit("pell"), Drains, "pell", Drains.Weapon("test_ledger"), brigand.Id, null, strikes, events, pell, brigand);

        Assert.Empty(events);
        Assert.Null(after.Find("pell"));
    }

    [Fact]
    public void ALearnedDarkDrainsOnlyPastItsGate()
    {
        var learned = Drains with { Classes = Shipped.Classes };
        var pell = Facing(1, 5).Find("pell")!;
        var brigand = Brigand(Facing(1, 5));
        var mag = learned.StatsOf(pell.Unit).Mag;
        var res = learned.StatsOf(brigand.Unit).Res;
        var held = learned with { Riders = learned.Riders.SetItem(MagicSchool.Dark, new SchoolRider(RiderKind.Drain, 0, 0) { Gate = mag - res }) };
        var open = learned with { Riders = learned.Riders.SetItem(MagicSchool.Dark, new SchoolRider(RiderKind.Drain, 0, 0) { Gate = mag - res - 1 }) };
        Func<BattleState, BattleState> learn = s => s.WithUnit(s.Find("pell")! with { Unit = s.Find("pell")!.Unit with { Learned = ValueList<MagicSchool>.Of(MagicSchool.Dark) } });

        var (_, refused) = Attacked(5, (_, f) => PellHit(f) && f.AttackerHpAfter == 5, held, learn);
        var (_, fired) = Attacked(5, (_, f) => PellHit(f) && f.AttackerHpAfter == 5, open, learn);

        Assert.DoesNotContain(refused.Events, e => e is UnitDrained);
        Assert.Single(fired.Events.OfType<UnitDrained>());
        Assert.StartsWith(" no drain: ", PlaySession.Riders(held, learn(Facing(1, 5)).Find("pell")!, brigand, null).Riders);
    }

    [Fact]
    public void ARecallTakesTheDrainedHpBack()
    {
        var (_, result) = Attacked(5, (_, f) => PellHit(f) && f.AttackerHpAfter == 5);
        Assert.True(result.Next.Find("pell")!.Hp > 5);

        var back = Resolver.Apply(result.Next, Drains, new Recall(0));

        Assert.True(back.Accepted, back.Rejection?.Message);
        Assert.Equal(5, back.Next.Find("pell")!.Hp);
    }

    [Fact]
    public void TheForecastSaysWhatTheStrikesDrainIfAllLandCritsAside()
    {
        var state = Facing(1, 5);
        var (pell, target) = (state.Find("pell")!, Brigand(state));
        var forecast = Core.Combat.Forecast(
            Drains.CombatantOf(pell.Unit, pell.EquippedWeapon(Drains), state.Map.TerrainAt(pell.At, Drains), pell.Hp),
            Drains.CombatantOf(target.Unit, target.EquippedWeapon(Drains), state.Map.TerrainAt(target.At, Drains), target.Hp),
            2,
            state.Scheme);
        var most = Drain.MostDrained(forecast.Attacker, target);

        Assert.Equal(($" drains up to {most}", ""), PlaySession.Riders(Drains, pell, target, null, forecast));
        Assert.Equal(("", ""), PlaySession.Riders(Plain, pell, target, null, forecast));
        Assert.Equal(Math.Min(target.Hp, forecast.Attacker.StrikeCount * forecast.Attacker.Damage), most);
        Assert.Equal(1, Drain.MostDrained(forecast.Attacker, target with { Hp = 1 }));
        Assert.Equal(0, Drain.MostDrained(SideForecast.None, target));
    }

    [Fact]
    public void ThePlannerPricesTheHealOnlyUpToTheHpTheDrainerWouldBeMissing()
    {
        var wounded = Facing(1, 5);
        var full = Facing(1, PellMax);
        var pell = wounded.Find("pell")!;
        var brigand = Brigand(wounded);

        var drains = EnemyAi.Score(wounded, Drains, pell, pell.At, brigand);
        var plain = EnemyAi.Score(wounded, Plain, pell, pell.At, brigand);
        var atFull = EnemyAi.Score(full, Drains, full.Find("pell")!, pell.At, brigand) - EnemyAi.Score(full, Plain, full.Find("pell")!, pell.At, brigand);

        Assert.True(drains > plain, $"{drains} against {plain}");
        Assert.InRange(atFull, 0, drains - plain - 1);
    }

    [Fact]
    public void ADrainRiderRoundTripsThroughTheSerializerAndTakesNoAmount()
    {
        var again = ContentLoader.Parse(ContentSerializer.Write(Shipped));
        Assert.Equal(Shipped.Riders, again.Riders);

        var files = Fixture.Files(rules: """{ "wakeRadius": 4, "schools": { "dark": { "rider": { "kind": "drain", "amount": 2 } } } }""");
        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(files));
        Assert.Contains("schools.dark", error.Message);
        Assert.Contains("rider.amount", error.Message);
        Assert.Contains("is not a field of a drain rider", error.Message);
    }
}
