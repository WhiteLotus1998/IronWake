using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Dark's curse (issue 1328, DECISIONS/0322, 0325): a hit (not a miss, not a kill) from a tome naming dark's curse rider
/// blinds the target and lays a tick, <c>max(1, amount - Res / 2)</c> at each of its side's next phase starts, never
/// below 1 HP, that heals the caster by what it took. One a unit, refreshed for a new caster, beside burn and outside
/// its cap; light's cleanse clears it. No shipped school carries it until Lotus signs its numbers, so these tests give
/// dark a fixture curse (3 for two phases, blind 30), the Adept dark, and a fixture tome, <c>test_curse</c>, to Pell on
/// the sample <c>the_tollgate_frost.map</c>, against the woods brigand.
/// </summary>
public class CurseTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Weapon Cinder = Shipped.Weapon("cinder");

    private static readonly SchoolRider DarkCurse = new(RiderKind.Curse, 3, 2) { Blind = 30 };

    private static readonly GameContent Curses = Shipped with
    {
        Weapons = Shipped.Weapons
            .SetItem("test_curse", Cinder with { Id = "test_curse", Name = "Test Curse", School = MagicSchool.Dark, Rider = RiderKind.Curse, Ignites = false })
            .SetItem("test_ledger", Cinder with { Id = "test_ledger", Name = "Test Ledger", School = MagicSchool.Dark, Rider = RiderKind.Drain, Ignites = false })
            .SetItem("cinder", Cinder with { Rider = RiderKind.Burn }),
        Classes = Shipped.Classes.SetItem("adept", Shipped.Class("adept") with { Schools = Shipped.Class("adept").Schools.Add(MagicSchool.Dark) }),
        Riders = Shipped.Riders.SetItem(MagicSchool.Dark, DarkCurse),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private const string RiderRules = """
        { "wakeRadius": 4, "schools": { "dark": { "rider": { "kind": "curse", "amount": 3, "phases": 2, "blind": 30 } } } }
        """;

    private static ContentFiles RulesFiles(Func<string, string> edit) => Fixture.Files(rules: edit(RiderRules));

    /// <summary>Pell holding <paramref name="tome"/> two tiles below the woods brigand at 6,5, out of its axe's reach.</summary>
    private static BattleState Facing(ulong seed = 1328, string tome = "test_curse")
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, Curses), Curses, Curses.Cast, seed);
        var pell = state.Find("pell")!;
        var unit = pell.Unit with { Inventory = pell.Unit.Inventory.Replace(0, new ItemStack(tome, Cinder.Durability)) };
        return state.WithUnit(pell with { Unit = unit, At = new Coord(6, 7) });
    }

    private static BattleUnit Brigand(BattleState state) => state.Units.Single(u => u.Side == Side.Enemy && u.Group == "woods" && u.Unit.ClassId == Shipped.Unit("toll_brigand").ClassId);

    private static int Res(BattleUnit unit) => Curses.StatsOf(unit.Unit).Res;

    private static int Max(BattleUnit unit) => Curses.StatsOf(unit.Unit).Hp;

    /// <summary>The first seed under 400 whose attack by Pell passes <paramref name="keep"/>, the board before it and its result.</summary>
    private static (BattleState Before, ApplyResult Result) Attacked(Func<BattleState, ApplyResult, bool> keep, Func<BattleState, BattleState>? edit = null, string tome = "test_curse")
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = Facing(seed, tome);
            state = edit?.Invoke(state) ?? state;
            var result = Resolver.Apply(state, Curses, new Attack("pell", Brigand(state).Id));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (keep(state, result))
            {
                return (state, result);
            }
        }

        throw new InvalidOperationException("no seed under 400 gave the combat asked for");
    }

    private static bool PellHit(ApplyResult result) =>
        result.Events.OfType<CombatFought>().Single().Strikes.Any(s => s.AttackerId == "pell" && s.Hit);

    private static bool SurvivingHit(BattleState before, ApplyResult result) =>
        PellHit(result) && result.Next.Find(Brigand(before).Id) is not null;

    /// <summary>The board with the brigand cursed by <paramref name="caster"/> at <paramref name="hp"/>, the player phase about to end.</summary>
    private static (BattleState State, BattleUnit Brigand) Cursed(int hp, string caster = "pell", int phases = 2)
    {
        var state = Facing();
        var brigand = Brigand(state) with { Hp = hp, Curse = 3, CurseBlind = 30, CursePhases = phases, CursedBy = caster };
        return (state.WithUnit(brigand), brigand);
    }

    [Fact]
    public void NoShippedSchoolOrTomeCarriesTheCurse()
    {
        Assert.DoesNotContain(Shipped.Riders.Values, r => r.Kind == RiderKind.Curse);
        Assert.DoesNotContain(Shipped.Weapons.Values, w => w.Rider == RiderKind.Curse);
        Assert.Equal("curse", SchoolRider.Label(RiderKind.Curse));
    }

    [Fact]
    public void ASurvivingHitCursesTheTarget()
    {
        var (before, result) = Attacked(SurvivingHit);
        var brigand = result.Next.Find(Brigand(before).Id)!;
        var tick = Math.Max(1, 3 - Res(brigand) / 2);

        Assert.Equal(new UnitCursed(brigand.Id, "pell", tick, 2, 30), Assert.Single(result.Events.OfType<UnitCursed>()));
        Assert.Equal((3, 30, 2, "pell"), (brigand.Curse, brigand.CurseBlind, brigand.CursePhases, brigand.CursedBy));
    }

    [Fact]
    public void AMissOrAKillLaysNoCurse()
    {
        var (_, missed) = Attacked((_, r) => !PellHit(r));
        Assert.DoesNotContain(missed.Events, e => e is UnitCursed);

        var (before, killed) = Attacked((s, r) => r.Next.Find(Brigand(s).Id) is null, s => s.WithUnit(Brigand(s) with { Hp = 1 }));
        Assert.Null(killed.Next.Find(Brigand(before).Id));
        Assert.DoesNotContain(killed.Events, e => e is UnitCursed);
    }

    [Fact]
    public void AHollowIsNeverTheCasterItsHitsLayNoCurse()
    {
        var (_, result) = Attacked(SurvivingHit, s => s.WithUnit(s.Find("pell")! with { Hollow = new HollowMark("wren", "pell", 2) }));

        Assert.DoesNotContain(result.Events, e => e is UnitCursed);
    }

    [Fact]
    public void TheCurseTicksAtEachOfItsSidesNextTwoPhaseStartsHealingTheCasterThenClears()
    {
        var (state, brigand) = Cursed(15);
        state = state.WithUnit(state.Find("pell")! with { Hp = 5 });
        var tick = Math.Max(1, 3 - Res(brigand) / 2);

        var enemy = Resolver.Apply(state, Curses, new EndPhase());
        Assert.Contains(new CurseTicked(brigand.Id, tick, 15 - tick, "pell", tick, 5 + tick), enemy.Events);
        Assert.Equal((15 - tick, 1), (enemy.Next.Find(brigand.Id)!.Hp, enemy.Next.Find(brigand.Id)!.CursePhases));
        Assert.Equal(5 + tick, enemy.Next.Find("pell")!.Hp);

        var player = Resolver.Apply(enemy.Next, Curses, new EndPhase());
        Assert.DoesNotContain(player.Events, e => e is CurseTicked);

        var second = Resolver.Apply(player.Next, Curses, new EndPhase());
        Assert.Contains(new CurseTicked(brigand.Id, tick, 15 - 2 * tick, "pell", tick, 5 + 2 * tick), second.Events);
        var after = second.Next.Find(brigand.Id)!;
        Assert.Equal((0, 0, 0, null), (after.Curse, after.CurseBlind, after.CursePhases, after.CursedBy));

        var third = Resolver.Apply(Resolver.Apply(second.Next, Curses, new EndPhase()).Next, Curses, new EndPhase());
        Assert.DoesNotContain(third.Events, e => e is CurseTicked);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 1)]
    public void ATickNeverTakesAUnitBelowOneHpAndAtOneHpTakesAndHealsNothing(int hp, int after)
    {
        var (state, brigand) = Cursed(hp);
        state = state.WithUnit(state.Find("pell")! with { Hp = 5 });
        var result = Resolver.Apply(state, Curses, new EndPhase());

        Assert.Equal(after, result.Next.Find(brigand.Id)!.Hp);
        Assert.Equal(5 + hp - after, result.Next.Find("pell")!.Hp);
        Assert.Equal(hp > 1, result.Events.Any(e => e is CurseTicked));
        Assert.Equal(1, result.Next.Find(brigand.Id)!.CursePhases);
    }

    [Fact]
    public void TheHealStopsAtTheCastersMaxHp()
    {
        var (state, brigand) = Cursed(15);
        var pell = state.Find("pell")!;
        state = state.WithUnit(pell with { Hp = Max(pell) - 1 });
        var tick = Math.Max(1, 3 - Res(brigand) / 2);

        var result = Resolver.Apply(state, Curses, new EndPhase());

        Assert.Contains(new CurseTicked(brigand.Id, tick, 15 - tick, "pell", Math.Min(1, tick), Max(pell)), result.Events);
        Assert.Equal(Max(pell), result.Next.Find("pell")!.Hp);
    }

    [Fact]
    public void ADeadCastersCurseStillTicksAndHealsNobody()
    {
        var (state, brigand) = Cursed(15);
        state = state.WithoutUnit("pell");
        var tick = Math.Max(1, 3 - Res(brigand) / 2);

        var result = Resolver.Apply(state, Curses, new EndPhase());

        Assert.Contains(new CurseTicked(brigand.Id, tick, 15 - tick, null, 0, 0), result.Events);
        Assert.Equal(15 - tick, result.Next.Find(brigand.Id)!.Hp);
        Assert.EndsWith("its caster is gone, and it heals no one", PlaySession.Describe(new CurseTicked(brigand.Id, tick, 15 - tick, null, 0, 0), Curses, UnitNames.None));
    }

    [Fact]
    public void ASecondCurseRefreshesTheCountAndPaysTheNewCasterNeverStacking()
    {
        var (before, result) = Attacked(SurvivingHit, s => s.WithUnit(Brigand(s) with { Curse = 3, CurseBlind = 30, CursePhases = 1, CursedBy = "wren" }));
        var brigand = result.Next.Find(Brigand(before).Id)!;

        Assert.Equal((3, 30, 2, "pell"), (brigand.Curse, brigand.CurseBlind, brigand.CursePhases, brigand.CursedBy));
        Assert.Equal(Math.Max(1, 3 - Res(brigand) / 2), Curse.Tick(Curses, brigand));
    }

    [Fact]
    public void ACurseSitsBesideFourBurnStacksOutsideTheCapAndBothTick()
    {
        var (state, brigand) = Cursed(15);
        state = state.WithUnit(brigand with { Burn = 2, BurnStacks = 4, BurnPhases = 2 });
        var burn = Math.Max(1, 8 - Res(brigand) / 2);
        var tick = Math.Max(1, 3 - Res(brigand) / 2);

        var result = Resolver.Apply(state, Curses, new EndPhase());

        Assert.Contains(new UnitBurned(brigand.Id, burn, 15 - burn), result.Events);
        Assert.Equal((15 - burn - tick, tick), Assert.Single(result.Events.OfType<CurseTicked>()) is var t ? (t.HpAfter, t.Amount) : default);
        Assert.Equal(4, result.Next.Find(brigand.Id)!.BurnStacks);
        Assert.Equal(1, result.Next.Find(brigand.Id)!.CursePhases);

        var (stacked, again) = Attacked(SurvivingHit, s => s.WithUnit(Brigand(s) with { Burn = 2, BurnStacks = 4, BurnPhases = 2 }));
        var after = again.Next.Find(Brigand(stacked).Id)!;
        Assert.Equal((4, 2), (after.BurnStacks, after.CursePhases));
    }

    [Fact]
    public void BlindCutsTheCursedUnitsHitOnItsStrikeAndItsCounter()
    {
        var state = Facing();
        var brigand = Brigand(state);
        var cursed = brigand with { Curse = 3, CurseBlind = 30, CursePhases = 2, CursedBy = "pell" };
        var pell = state.Find("pell")!;

        Assert.Equal(-30, Curse.HitOf(cursed));
        Assert.Equal(0, Curse.HitOf(brigand));
        Assert.Equal(0, Curse.HitOf(cursed with { CursePhases = 0 }));
        Assert.Equal(30, Ironwake.Core.Combat.Hit(brigand.ToCombatant(state, Curses, against: pell)) - Ironwake.Core.Combat.Hit(cursed.ToCombatant(state.WithUnit(cursed), Curses, against: pell)));
        Assert.Equal(30, Ironwake.Core.Combat.Hit(brigand.ToCombatant(state, Curses, countering: true, against: pell)) - Ironwake.Core.Combat.Hit(cursed.ToCombatant(state.WithUnit(cursed), Curses, countering: true, against: pell)));
        Assert.Equal(-30, Brace.StrikeHit(state.WithUnit(cursed), cursed, pell));
    }

    [Fact]
    public void LightsCleanseClearsACurse()
    {
        var (_, brigand) = Cursed(15);
        Assert.True(Cleanse.Afflicted(brigand));
        Assert.False(Cleanse.Afflicted(brigand with { CursePhases = 0 }));

        var events = new List<GameEvent>();
        var cleared = Cleanse.Clear(brigand, "mira", events);

        Assert.Equal((0, 0, 0, null), (cleared.Curse, cleared.CurseBlind, cleared.CursePhases, cleared.CursedBy));
        Assert.Equal(new UnitCleansed(brigand.Id, "mira", false, false, false, false, Curse: true), Assert.Single(events));
        Assert.EndsWith("the curse cleared", PlaySession.Describe(Assert.Single(events), Curses, UnitNames.None));
        var staff = Shipped.Weapons.Values.First(w => w.Heals) with { Id = "test_cleanse", Cleanses = true };
        Assert.Contains("cleanses burn, chill, stun, curse and freeze", ItemCard.Text(Curses with { Weapons = Curses.Weapons.SetItem("test_cleanse", staff) }, "test_cleanse"));
    }

    [Fact]
    public void ADrainOrHollowTomeBorrowsACurseSchool()
    {
        Assert.Equal(RiderKind.Drain, Curses.RiderOf(Curses.Weapon("test_ledger"))!.Kind);
        Assert.True(SchoolRider.Borrows(RiderKind.Hollow, RiderKind.Curse));
        Assert.False(SchoolRider.Borrows(RiderKind.Curse, RiderKind.Drain));

        var (_, result) = Attacked((_, r) => PellHit(r), s => s.WithUnit(s.Find("pell")! with { Hp = 5 }), tome: "test_ledger");
        Assert.Contains(result.Events, e => e is UnitDrained);
        Assert.DoesNotContain(result.Events, e => e is UnitCursed);
    }

    [Fact]
    public void TheForecastTheCardAndTheLinesNameTheCurse()
    {
        var state = Facing();
        Assert.Equal(" curses", PlaySession.Riders(Curses, state.Find("pell")!, Brigand(state), null).Riders);

        var (_, brigand) = Cursed(15);
        var tick = Math.Max(1, 3 - Res(brigand) / 2);
        Assert.Equal($"cursed: -30 Hit, {tick} a phase to pell (2 phases)", Curse.CardLine(Curses, brigand, UnitNames.None));
        Assert.Null(Curse.CardLine(Curses, Brigand(state), UnitNames.None));

        Assert.Equal("Brigand-1 is cursed by pell: Hit -30, and 2 hp to pell at the start of each of its side's next two phases", PlaySession.Describe(new UnitCursed("brigand-1", "pell", 2, 2, 30), Curses, UnitNames.None));
        Assert.Equal("The curse takes 2 from brigand-1 (hp 9); pell heals 2 (hp 7)", PlaySession.Describe(new CurseTicked("brigand-1", 2, 9, "pell", 2, 7), Curses, UnitNames.None));
    }

    [Fact]
    public void TheCurseIsBoardStateTheProtocolCarries()
    {
        var (_, brigand) = Cursed(15);
        var state = Facing().WithUnit(brigand);
        var again = Brigand(ProtocolJson.ReadState(ProtocolJson.State(state, Curses), Curses));

        Assert.Equal((3, 30, 2, "pell"), (again.Curse, again.CurseBlind, again.CursePhases, again.CursedBy));
    }

    [Fact]
    public void ACurseRiderRoundTripsThroughTheSerializer()
    {
        var content = ContentLoader.Parse(RulesFiles(r => r));
        var again = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.Equal(DarkCurse, content.Riders[MagicSchool.Dark]);
        Assert.Equal(content.Riders, again.Riders);
    }

    [Theory]
    [InlineData("\"dark\"", "\"fire\"", "schools.fire", "rider.kind", "a curse is dark's rider alone")]
    [InlineData(", \"blind\": 30", "", "schools.dark", "blind", "is required")]
    [InlineData("\"amount\": 3", "\"amount\": 0", "schools.dark", "rider.amount", "must be at least 1")]
    [InlineData("\"phases\": 2", "\"phases\": 0", "schools.dark", "rider.phases", "must be at least 1")]
    [InlineData("\"blind\": 30", "\"blind\": -1", "schools.dark", "rider.blind", "must be at least 0")]
    [InlineData("\"blind\": 30", "\"blind\": 30, \"cap\": 4", "schools.dark", "rider.cap", "is not a field of a curse rider")]
    public void ABadCurseRiderIsRefusedAtLoadNamingFileEntryAndField(string from, string to, string entry, string field, string why)
    {
        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(RulesFiles(r => r.Replace(from, to))));

        Assert.Contains(ContentFiles.RulesName, error.Message);
        Assert.Contains(entry, error.Message);
        Assert.Contains(field, error.Message);
        Assert.Contains(why, error.Message);
    }
}
