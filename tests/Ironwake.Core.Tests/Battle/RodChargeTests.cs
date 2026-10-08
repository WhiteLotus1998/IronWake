using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Lightning Rod's price and pay (issue 1329 slice 2, Lotus's round-3 rulings, DECISIONS/0322 and 0327; <see cref="LightningRod"/>):
/// a caught spell deals the holder x0.5, and a holder standing after a catch is charged, so his next lightning cast deals x1.25
/// and spends the charge, hit or miss. Both multiply with the mark on final damage, rounded down once. The board is
/// <see cref="LightningRodTests"/>'s: Pell on 6,6 with a fixture lightning tome beside the woods brigand at 6,5, the woods
/// archer holding the rod on 5,5.
/// </summary>
public class RodChargeTests
{
    private static readonly GameContent Real = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Weapon Gust = ContentLoader.Load(Fixture.GustContentDirectory()).Weapon("gust") with { EffectiveAgainst = ValueList<MovementType>.Empty };

    private static readonly GameContent Shipped = Real with
    {
        Weapons = Real.Weapons
            .SetItem("test_bolt", Real.Weapon("cinder") with { Id = "test_bolt", Name = "Test Bolt", School = MagicSchool.Lightning, Ignites = false })
            .SetItem("test_storm", Gust with { Id = "test_storm", Name = "Test Storm", Area = 1, Marks = true }),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private static readonly Coord BrigandAt = new(6, 5);

    /// <summary>Pell on 6,6 holding <paramref name="tome"/> in slot 0 and the storm in slot 1; the woods archer holding the rod on 5,5.</summary>
    private static BattleState Board(string tome = "test_bolt", ulong seed = 1329, MagicSchool? pellCharge = null, MagicSchool? archerCharge = null)
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, Shipped), Shipped, Shipped.Cast, seed);
        var pell = state.Find("pell")!;
        var inventory = new Inventory(ValueList<ItemStack>.From([new ItemStack(tome, Shipped.Weapon(tome).Durability), new ItemStack("test_storm", 6)]));
        state = state.WithUnit(pell with { Unit = pell.Unit with { Inventory = inventory }, At = new Coord(6, 6), RodCharge = pellCharge });
        var archer = Archer(state);
        return state.WithUnit(archer with { Unit = archer.Unit with { Abilities = archer.Unit.Abilities.Add("lightning_rod") }, RodCharge = archerCharge });
    }

    private static BattleUnit Archer(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Group == "woods" && u.Unit.ClassId == Shipped.Unit("archer").ClassId);

    private static BattleUnit Brigand(BattleState state) => state.Units.Single(u => u.At == BrigandAt);

    /// <summary>The first seed under 400 whose board and command pass <paramref name="keep"/>.</summary>
    private static (BattleState Before, ApplyResult Result) Seeded(Func<ulong, BattleState> board, Func<BattleState, Command> command, Func<BattleState, ApplyResult, bool> keep)
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = board(seed);
            var result = Resolver.Apply(state, Shipped, command(state));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (keep(state, result))
            {
                return (state, result);
            }
        }

        throw new InvalidOperationException("no seed under 400 gave the result asked for");
    }

    private static int Unscaled(CombatForecast forecast) => forecast.Attacker.Unscaled ?? forecast.Attacker.Damage;

    [Fact]
    public void ACaughtSpellDealsTheHolderHalfOnFinalDamage()
    {
        var state = Board();
        var pell = state.Find("pell")!;

        var caught = Queries.Forecast(state, Shipped, pell, Brigand(state))!;
        var direct = Queries.Forecast(state, Shipped, pell, Archer(state))!;

        Assert.Equal(Archer(state).Id, caught.CaughtBy);
        Assert.Equal(LightningRod.Half, caught.Attacker.Scale);
        Assert.Equal(direct.Attacker.Damage / 2, caught.Attacker.Damage);
        Assert.Equal(direct.Attacker.Damage * Ironwake.Core.Combat.CritMultiplier / 2, caught.Attacker.CritDamage);
        Assert.True(direct.Attacker.Damage > caught.Attacker.Damage);
    }

    [Fact]
    public void TheHoldersCounterOnACaughtSpellIsPlain()
    {
        var state = Board();
        var pell = state.Find("pell")!;

        var caught = Queries.Forecast(state, Shipped, pell, Brigand(state))!;
        var direct = Queries.Forecast(state, Shipped, pell, Archer(state))!;

        Assert.Equal(direct.Defender, caught.Defender);
        Assert.Equal(DamageScale.One, caught.Defender.Scale);
    }

    [Fact]
    public void TheResolverDealsTheHalfTheForecastShows()
    {
        var (before, result) = Seeded(seed => Board(seed: seed), s => new Attack("pell", Brigand(s).Id), (s, r) => r.Events.OfType<CombatFought>().Single().Strikes.Any(x => x.AttackerId == "pell" && x.Hit && !x.Crit));
        var forecast = Queries.Forecast(before, Shipped, before.Find("pell")!, Brigand(before))!;

        var hit = result.Events.OfType<CombatFought>().Single().Strikes.First(x => x.AttackerId == "pell" && x.Hit && !x.Crit);
        Assert.Equal(forecast.Attacker.Damage, hit.Damage);
        Assert.Equal(Archer(before).Id, hit.TargetId);
    }

    [Fact]
    public void AHolderStandingAfterACatchIsCharged()
    {
        var (before, result) = Seeded(seed => Board(seed: seed), s => new Attack("pell", Brigand(s).Id), (s, r) => r.Next.Find(Archer(s).Id) is not null);

        Assert.Contains(new RodCharged(Archer(before).Id, MagicSchool.Lightning), result.Events);
        Assert.Equal(MagicSchool.Lightning, result.Next.Find(Archer(before).Id)!.RodCharge);
    }

    [Fact]
    public void TwoCatchesGiveOneCharge()
    {
        var (before, result) = Seeded(seed => Board(seed: seed, archerCharge: MagicSchool.Lightning), s => new Attack("pell", Brigand(s).Id), (s, r) => r.Next.Find(Archer(s).Id) is not null);

        Assert.DoesNotContain(result.Events, e => e is RodCharged);
        Assert.Equal(MagicSchool.Lightning, result.Next.Find(Archer(before).Id)!.RodCharge);
        Assert.Equal(LightningRod.Half, Queries.Forecast(before, Shipped, before.Find("pell")!, Brigand(before))!.Attacker.Scale);
    }

    [Fact]
    public void TheChargeAddsAQuarterToEveryStrikeOfTheNextLightningCast()
    {
        var plain = Board();
        var charged = Board(pellCharge: MagicSchool.Lightning);

        var before = Queries.Forecast(plain, Shipped, plain.Find("pell")!, Archer(plain))!;
        var after = Queries.Forecast(charged, Shipped, charged.Find("pell")!, Archer(charged))!;

        Assert.Equal(LightningRod.Boost, after.Attacker.Scale);
        Assert.Equal(before.Attacker.Damage * 5 / 4, after.Attacker.Damage);
        Assert.Equal(before.Attacker.Damage * Ironwake.Core.Combat.CritMultiplier * 5 / 4, after.Attacker.CritDamage);
        Assert.Equal(before.Defender, after.Defender);
    }

    [Fact]
    public void TheChargeIsSpentByTheNextLightningCastOnAMiss()
    {
        var (before, result) = Seeded(seed => Board(seed: seed, pellCharge: MagicSchool.Lightning), s => new Attack("pell", Archer(s).Id), (s, r) => r.Events.OfType<CombatFought>().Single().Strikes.Where(x => x.AttackerId == "pell").All(x => !x.Hit) && r.Next.Find("pell") is not null);

        Assert.Contains(new RodChargeSpent("pell", MagicSchool.Lightning), result.Events);
        Assert.Null(result.Next.Find("pell")!.RodCharge);
    }

    [Fact]
    public void TheChargeIsSpentByTheNextLightningCastOnAHit()
    {
        var (_, result) = Seeded(seed => Board(seed: seed, pellCharge: MagicSchool.Lightning), s => new Attack("pell", Archer(s).Id), (s, r) => r.Events.OfType<CombatFought>().Single().Strikes.Any(x => x.AttackerId == "pell" && x.Hit) && r.Next.Find("pell") is not null);

        Assert.Contains(new RodChargeSpent("pell", MagicSchool.Lightning), result.Events);
        Assert.Null(result.Next.Find("pell")!.RodCharge);
    }

    [Fact]
    public void AnAreaCastReadsAndSpendsTheCharge()
    {
        var state = Board(pellCharge: MagicSchool.Lightning);
        var plain = Board();
        var at = BrigandAt;

        var boosted = AreaCast.Forecast(state, Shipped, state.Find("pell")!, Shipped.Weapon("test_storm"), Brigand(state), at).Attacker;
        var unboosted = AreaCast.Forecast(plain, Shipped, plain.Find("pell")!, Shipped.Weapon("test_storm"), Brigand(plain), at).Attacker;
        var result = Resolver.Apply(state, Shipped, new UseItem("pell", 1, "6,5"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(LightningRod.Boost, boosted.Scale);
        Assert.Equal(unboosted.Damage * 5 / 4, boosted.Damage);
        Assert.Single(result.Events.OfType<RodChargeSpent>());
        Assert.Null(result.Next.Find("pell")!.RodCharge);
    }

    [Fact]
    public void AFireCastNeitherReadsNorSpendsALightningCharge()
    {
        var state = Board(tome: "cinder", pellCharge: MagicSchool.Lightning);

        var forecast = Queries.Forecast(state, Shipped, state.Find("pell")!, Brigand(state))!;
        var result = Resolver.Apply(state, Shipped, new Attack("pell", Brigand(state).Id));

        Assert.Equal(DamageScale.One, forecast.Attacker.Scale);
        Assert.DoesNotContain(result.Events, e => e is RodChargeSpent);
        if (result.Next.Find("pell") is { } pell)
        {
            Assert.Equal(MagicSchool.Lightning, pell.RodCharge);
        }
    }

    [Fact]
    public void ACounterNeitherReadsNorSpendsTheCharge()
    {
        var state = Board(pellCharge: MagicSchool.Lightning);
        var pell = state.Find("pell")!;

        Assert.Equal(MagicSchool.Lightning, pell.ToCombatant(state, Shipped, against: Brigand(state)).Charged);
        Assert.Null(pell.ToCombatant(state, Shipped, countering: true, against: Brigand(state)).Charged);
        Assert.Null(pell.Answering(state, Shipped, Brigand(state).At, Brigand(state)).Charged);
    }

    [Fact]
    public void TheChargeEndsWithTheMap()
    {
        var fresh = BattleState.From(MapFiles.Load(SamplePath, Shipped), Shipped, Shipped.Cast, 1329);

        Assert.All(fresh.Units, u => Assert.Null(u.RodCharge));
    }

    [Theory]
    [InlineData(10, 5, 4, 3, 2, 18)]
    [InlineData(9, 1, 2, 3, 2, 6)]
    [InlineData(7, 5, 4, 1, 1, 8)]
    [InlineData(7, 1, 2, 1, 1, 3)]
    [InlineData(5, 5, 8, 3, 2, 4)]
    public void TheRodsMultiplesAndTheMarkMultiplyOnFinalDamageRoundedDownOnce(int damage, int numerator, int denominator, int markNumerator, int markDenominator, int dealt)
    {
        Assert.Equal(dealt, new DamageScale(numerator, denominator).Times(new DamageScale(markNumerator, markDenominator)).Of(damage));
    }

    [Fact]
    public void AChargedHitOnAMarkedUnitDealsOnePointEightSevenFive()
    {
        var side = new SideForecast(true, LightningRod.Boost.Of(10), 100, 100, 0, false, CashesMark: true) { Unscaled = 10, Scale = LightningRod.Boost };

        Assert.Equal(12, side.Damage);
        Assert.Equal(18, side.MarkedDamage);
        Assert.Equal(56, side.MarkedCritDamage);
        Assert.Equal(37, side.CritDamage);
    }

    [Fact]
    public void AMarkedHolderCatchingASpellTakesThreeQuarters()
    {
        var side = new SideForecast(true, LightningRod.Half.Of(9), 100, 100, 0, false, CashesMark: true) { Unscaled = 9, Scale = LightningRod.Half };

        Assert.Equal(4, side.Damage);
        Assert.Equal(6, side.MarkedDamage);
        Assert.Equal(20, side.MarkedCritDamage);
    }

    [Fact]
    public void TheCardAndTheForecastShowTheRod()
    {
        var state = Board(pellCharge: MagicSchool.Lightning);
        var pell = state.Find("pell")!;

        Assert.Equal("charged: next lightning x1.25", LightningRod.CardLine(pell));
        Assert.Null(LightningRod.CardLine(Archer(state)));
        Assert.Equal(" (charged x1.25)", LightningRod.ForecastText(new SideForecast(true, 5, 100, 100, 0, false) { Scale = LightningRod.Boost }));
        Assert.Equal(" (caught x0.5)", LightningRod.ForecastText(new SideForecast(true, 5, 100, 100, 0, false) { Scale = LightningRod.Half }));
        Assert.Equal(" (charged x1.25, caught x0.5)", LightningRod.ForecastText(new SideForecast(true, 5, 100, 100, 0, false) { Scale = LightningRod.Boost.Times(LightningRod.Half) }));
        Assert.Equal("", LightningRod.ForecastText(new SideForecast(true, 5, 100, 100, 0, false)));
        var caught = Queries.Forecast(state, Shipped, pell, Brigand(state))!;
        Assert.Contains("(charged x1.25, caught x0.5)", PlaySession.ForecastText(state, Shipped, pell, Brigand(state), caught, pell.At, false));
    }

    [Fact]
    public void TheChargeAndAScaledForecastReadBackEqualFromTheProtocol()
    {
        var state = Board(pellCharge: MagicSchool.Lightning) with { History = ValueList<BattleState>.Empty };
        var json = ProtocolJson.State(state, Shipped);
        var forecast = Queries.Forecast(state, Shipped, state.Find("pell")!, Brigand(state))!;
        var forecastJson = ProtocolJson.Forecast(forecast);

        Assert.Contains("\"rodCharge\":\"lightning\"", json);
        Assert.Equal(state, ProtocolJson.ReadState(json, Shipped));
        Assert.Throws<ProtocolException>(() => ProtocolJson.ReadState(json.Replace("\"rodCharge\":\"lightning\"", "\"rodCharge\":\"thunder\""), Shipped));
        Assert.Contains("\"scaleNumerator\":5", forecastJson);
        var read = ProtocolJson.ReadForecast(forecastJson);
        Assert.Equal(forecast.Attacker.CritDamage, read.Attacker.CritDamage);
        Assert.Equal(forecast.Attacker.Scale, read.Attacker.Scale);
    }

    [Fact]
    public void ThePlannerPricesTheHalfAndTheCharge()
    {
        var state = Board();
        var pell = state.Find("pell")!;

        Assert.NotEqual(EnemyAi.Score(state, Shipped, pell, pell.At, Archer(state)), EnemyAi.Score(state, Shipped, pell, pell.At, Brigand(state)));
        var charged = Board(pellCharge: MagicSchool.Lightning);
        Assert.NotEqual(EnemyAi.Score(state, Shipped, pell, pell.At, Archer(state)), EnemyAi.Score(charged, Shipped, charged.Find("pell")!, pell.At, Archer(charged)));
    }
}
