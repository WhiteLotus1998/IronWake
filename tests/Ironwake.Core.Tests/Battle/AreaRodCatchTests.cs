using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Lightning Rod catches an area cast (issue 1400, Lotus's ruling, DECISIONS/0351; <see cref="AreaCast.Catcher"/>): when a storm's
/// area takes in an ally of a holder within the rod's radius, the holder aside, and the tome reaches the holder from the caster's
/// tile, the whole storm strikes the holder alone, once, at x0.5, and charges him if he stands. A catch marks no one. The board is
/// <see cref="RodChargeTests"/>'s: Pell on 6,6 with a fixture lightning tome and an area-1 marking storm, the woods brigand on 6,5,
/// the woods archer holding the rod on 5,5.
/// </summary>
public class AreaRodCatchTests
{
    private static readonly GameContent Real = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Weapon Gust = ContentLoader.Load(Fixture.GustContentDirectory()).Weapon("gust") with { EffectiveAgainst = ValueList<MovementType>.Empty };

    private static readonly GameContent Shipped = Real with
    {
        Weapons = Real.Weapons
            .SetItem("test_bolt", Real.Weapon("cinder") with { Id = "test_bolt", Name = "Test Bolt", School = MagicSchool.Lightning, Ignites = false, Marks = true })
            .SetItem("test_storm", Gust with { Id = "test_storm", Name = "Test Storm", Area = 1, Marks = true }),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private static readonly Coord BrigandAt = new(6, 5);

    /// <summary>Pell on <paramref name="pellAt"/> (6,6 by default) holding the bolt in slot 0 and the storm in slot 1; the archer holding the rod.</summary>
    private static BattleState Board(ulong seed = 1400, Coord? pellAt = null, Func<BattleState, BattleState>? edit = null, bool rod = true)
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, Shipped), Shipped, Shipped.Cast, seed);
        var pell = state.Find("pell")!;
        var inventory = new Inventory(ValueList<ItemStack>.From([new ItemStack("test_bolt", 8), new ItemStack("test_storm", 6)]));
        state = state.WithUnit(pell with { Unit = pell.Unit with { Inventory = inventory }, At = pellAt ?? new Coord(6, 6) });
        state = rod ? WithRod(state, Archer(state)) : state;
        return edit?.Invoke(state) ?? state;
    }

    private static BattleState WithRod(BattleState state, BattleUnit unit) =>
        state.WithUnit(unit with { Unit = unit.Unit with { Abilities = unit.Unit.Abilities.Add("lightning_rod") } });

    private static BattleUnit Archer(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Group == "woods" && u.Unit.ClassId == Shipped.Unit("archer").ClassId);

    private static BattleUnit Brigand(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Group == "woods" && u.Id != Archer(state).Id);

    private static Command Storm(string at) => new UseItem("pell", 1, at);

    private static ApplyResult Cast(BattleState state, string at)
    {
        var result = Resolver.Apply(state, Shipped, Storm(at));
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result;
    }

    /// <summary>The first seed under 400 whose board and cast pass <paramref name="keep"/>.</summary>
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

    private static bool HolderHitAndStands(BattleState before, ApplyResult result) =>
        result.Events.OfType<CombatFought>().Single().Strikes.Any(s => s.Hit) && result.Next.Find(Archer(before).Id) is not null;

    [Fact]
    public void ACaughtStormStrikesTheHolderAloneOnce()
    {
        var state = Board();

        var result = Cast(state, "6,5");

        var fought = Assert.Single(result.Events.OfType<CombatFought>());
        Assert.Equal(Archer(state).Id, fought.TargetId);
        Assert.Single(fought.Strikes);
        Assert.Equal(new[] { Archer(state).Id }, result.Events.OfType<AreaCastAt>().Single().Struck.ToArray());
        Assert.Contains(new RodCaught(Archer(state).Id, Brigand(state).Id, "pell"), result.Events);
        Assert.Equal(Brigand(state).Hp, result.Next.Find(Brigand(state).Id)!.Hp);
    }

    [Fact]
    public void ACaughtStormDealsTheHolderHalfOnFinalDamage()
    {
        var state = Board();
        var pell = state.Find("pell")!;
        var storm = Shipped.Weapon("test_storm");

        var caught = AreaCast.CaughtForecast(state, Shipped, pell, storm, Archer(state)).Attacker;
        var plain = AreaCast.Forecast(state, Shipped, pell, storm, Archer(state), Archer(state).At).Attacker;
        var (before, result) = Seeded(seed => Board(seed), _ => Storm("6,5"), (s, r) => r.Events.OfType<CombatFought>().Single().Strikes.Any(x => x.Hit && !x.Crit));

        Assert.Equal(LightningRod.Half, caught.Scale);
        Assert.Equal(plain.Damage / 2, caught.Damage);
        Assert.Equal(caught.Damage, result.Events.OfType<CombatFought>().Single().Strikes.Single().Damage);
        Assert.Equal(Archer(before).Id, result.Events.OfType<CombatFought>().Single().TargetId);
    }

    [Fact]
    public void ACaughtStormDrawsNoCounter()
    {
        var result = Cast(Board(), "6,5");

        Assert.All(result.Events.OfType<CombatFought>().Single().Strikes, s => Assert.Equal("pell", s.AttackerId));
    }

    [Fact]
    public void AHolderStandingAfterACaughtStormIsCharged()
    {
        var (before, result) = Seeded(seed => Board(seed), _ => Storm("6,5"), (s, r) => r.Next.Find(Archer(s).Id) is not null);

        Assert.Contains(new RodCharged(Archer(before).Id, MagicSchool.Lightning), result.Events);
        Assert.Equal(MagicSchool.Lightning, result.Next.Find(Archer(before).Id)!.RodCharge);
    }

    [Fact]
    public void ACaughtMarkingStormMarksNoOne()
    {
        var (before, result) = Seeded(seed => Board(seed), _ => Storm("6,5"), HolderHitAndStands);

        Assert.DoesNotContain(result.Events, e => e is UnitMarked);
        Assert.Null(result.Next.Find(Archer(before).Id)!.Mark);
        Assert.Null(result.Next.Find(Brigand(before).Id)!.Mark);
    }

    [Fact]
    public void ACaughtMarkingBoltMarksNoOne()
    {
        var (before, result) = Seeded(seed => Board(seed), s => new Attack("pell", Brigand(s).Id, 0), (s, r) =>
            r.Events.OfType<CombatFought>().Single().Strikes.Any(x => x.AttackerId == "pell" && x.Hit) && r.Next.Find(Archer(s).Id) is not null);

        Assert.Contains(result.Events, e => e is RodCaught);
        Assert.DoesNotContain(result.Events, e => e is UnitMarked um && um.ByUnitId == "pell");
        Assert.Null(result.Next.Find(Archer(before).Id)!.Mark);
    }

    [Fact]
    public void AnUncaughtMarkingBoltStillMarks()
    {
        var (before, result) = Seeded(seed => Board(seed), s => new Attack("pell", Archer(s).Id, 0), (s, r) =>
            r.Events.OfType<CombatFought>().Single().Strikes.Any(x => x.AttackerId == "pell" && x.Hit) && r.Next.Find(Archer(s).Id) is not null);

        Assert.DoesNotContain(result.Events, e => e is RodCaught);
        Assert.Equal(MagicSchool.Lightning, result.Next.Find(Archer(before).Id)!.Mark);
    }

    [Fact]
    public void ACaughtStormCashesAMarkTheHolderCarries()
    {
        static BattleState MarkedHolder(ulong seed) => Board(seed, edit: s => s.WithUnit(Archer(s) with { Mark = MagicSchool.Lightning }));
        var state = MarkedHolder(1400);
        var caught = AreaCast.CaughtForecast(state, Shipped, state.Find("pell")!, Shipped.Weapon("test_storm"), Archer(state)).Attacker;
        var (before, result) = Seeded(MarkedHolder, _ => Storm("6,5"), HolderHitAndStands);

        Assert.True(caught.CashesMark);
        Assert.Contains(new MarkCashed(Archer(before).Id, "pell", MagicSchool.Lightning), result.Events);
        Assert.DoesNotContain(result.Events, e => e is UnitMarked);
        Assert.Null(result.Next.Find(Archer(before).Id)!.Mark);
    }

    [Fact]
    public void AHolderBesideTheAreaCatchesAStormOnAnAllyWithinItsRadius()
    {
        var state = Board();

        var result = Cast(state, "7,5");

        Assert.Equal(new[] { Archer(state).Id }, result.Events.OfType<AreaCastAt>().Single().Struck.ToArray());
        Assert.Equal(Brigand(state).Hp, result.Next.Find(Brigand(state).Id)!.Hp);
    }

    [Fact]
    public void AStormOnAnAllyBeyondTheRodsRadiusIsNotCaught()
    {
        var state = Board(pellAt: new Coord(7, 5), edit: s => s.WithUnit(Brigand(s) with { At = new Coord(8, 5) }));

        var result = Cast(state, "8,5");

        Assert.DoesNotContain(result.Events, e => e is RodCaught);
        Assert.Equal(new[] { Brigand(state).Id }, result.Events.OfType<AreaCastAt>().Single().Struck.ToArray());
    }

    [Fact]
    public void AStormTheTomeCannotCarryToTheHolderLandsAsCast()
    {
        var state = Board(pellAt: new Coord(6, 7));

        var result = Cast(state, "6,5");

        Assert.DoesNotContain(result.Events, e => e is RodCaught);
        Assert.Equal(2, result.Events.OfType<CombatFought>().Count());
        Assert.DoesNotContain(result.Events, e => e is RodCharged);
    }

    [Fact]
    public void AStunnedHolderCatchesNoStorm()
    {
        var state = Board(edit: s => s.WithUnit(Archer(s) with { Stun = 1 }));

        var result = Cast(state, "6,5");

        Assert.DoesNotContain(result.Events, e => e is RodCaught);
        Assert.Equal(2, result.Events.OfType<CombatFought>().Count());
    }

    [Fact]
    public void AStormOnTheHolderAloneIsNotCaughtAndStrikesHimPlain()
    {
        var state = Board(pellAt: new Coord(5, 6));

        var result = Cast(state, "5,4");

        Assert.DoesNotContain(result.Events, e => e is RodCaught or RodCharged);
        var fought = Assert.Single(result.Events.OfType<CombatFought>());
        Assert.Equal(Archer(state).Id, fought.TargetId);
        Assert.Equal(DamageScale.One, AreaCast.Forecast(state, Shipped, state.Find("pell")!, Shipped.Weapon("test_storm"), Archer(state), new Coord(5, 4)).Attacker.Scale);
    }

    [Theory]
    [InlineData("6,5", "brigand")]
    [InlineData("5,5", "archer")]
    public void OfTwoHoldersTheNearerToTheAimedTileCatches(string at, string catcher)
    {
        var state = Board(edit: s => WithRod(s, Brigand(s)));
        var expected = catcher == "brigand" ? Brigand(state).Id : Archer(state).Id;

        var result = Cast(state, at);

        Assert.Equal(expected, result.Events.OfType<RodCaught>().Single().UnitId);
        Assert.Equal(expected, result.Events.OfType<CombatFought>().Single().TargetId);
    }

    [Fact]
    public void ThePreviewNamesTheHolderAndHisOneCaughtStrike()
    {
        var state = Board();
        var holder = Archer(state);

        var line = AreaCast.Preview(state, Shipped, state.Find("pell")!, Shipped.Weapon("test_storm"), BrigandAt);

        Assert.Contains($"(Lightning Rod: strikes {holder.Id})", line);
        Assert.Contains("(caught x0.5)", line);
        Assert.DoesNotContain(Brigand(state).Id, line);
        Assert.DoesNotContain("marks", line);
    }

    [Fact]
    public void ThePlannerPricesACaughtStormAsOneHalvedStrikeOnTheHolder()
    {
        var state = Board();
        var pell = state.Find("pell")!;
        var storm = Shipped.Weapon("test_storm");
        var holder = Archer(state);

        var (score, kills) = AreaCast.Price(state, Shipped, pell, storm, BrigandAt, [Brigand(state), holder], holder);

        var side = AreaCast.CaughtForecast(state, Shipped, pell, storm, holder).Attacker;
        var expected = EnemyAi.NoCounterBonus + (side.Damage >= holder.Hp ? EnemyAi.KillBonus : 0)
            + Math.Min(holder.Hp, side.Damage) * Ironwake.Core.Combat.HitProbability(side.HitChance, state.Scheme);
        Assert.Equal(expected, score, 6);
        Assert.Equal(side.Damage >= holder.Hp, kills);
    }

    [Fact]
    public void ThePlannerCastsAStormARodWouldCatchOnlyForTheKill()
    {
        var state = Board();
        var bare = Board(rod: false);
        var storm = Shipped.Weapon("test_storm");
        var pell = state.Find("pell")!;
        var enemies = state.Units.Where(u => u.Side == Side.Enemy).ToList();

        var caught = AreaCast.Best(state, Shipped, pell, [pell.At], enemies);
        var free = AreaCast.Best(bare, Shipped, bare.Find("pell")!, [pell.At], bare.Units.Where(u => u.Side == Side.Enemy).ToList());

        Assert.NotNull(free);
        Assert.Equal(2, free.Struck.Count);
        if (caught is not null)
        {
            var holder = Assert.Single(caught.Struck);
            Assert.True(AreaCast.Price(state, Shipped, pell, storm, caught.At, caught.Struck, holder).Kills);
        }
    }
}
