using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The hungering weapon (DESIGN.md 13.23, experiment, issue 645): the drain at its carrier's
/// phase start after an unfed one, never below 1; the starved form when the drain would reach 1
/// (half Mt, uses held at 1); the feed on a kill (+10 HP to max, uses restored, +1 Mt per three
/// kills to +5); the hit that eases the starved form (+5 HP); the uses floor; and the cap, where
/// it wakes and neither drains, starves nor heals. Played on the spike's sample,
/// <c>docs/samples/the_gleaning_kinsbane.map</c>, where Keziah carries it.
/// </summary>
public class KinsbaneTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_gleaning_kinsbane.map");

    private static BattleState Placed(ulong seed = 645) =>
        BattleState.From(MapFiles.Load(SamplePath, Shipped), Shipped, Shipped.Cast, seed);

    private static BattleUnit Keziah(BattleState state) => state.Find("keziah")!;

    private static BattleState WithScythe(BattleState state, int hp, int fed = 0, bool starved = false, int uses = 20, bool hasFed = false)
    {
        var keziah = Keziah(state);
        var stack = keziah.Unit.Inventory.Items[0] with { Fed = fed, Starved = starved, Uses = uses };
        return state.WithUnit(keziah with { Hp = hp, HasFed = hasFed, Unit = keziah.Unit with { Inventory = keziah.Unit.Inventory.Replace(0, stack) } });
    }

    private static ItemStack Scythe(BattleState state) => Keziah(state).Unit.Inventory.Items[0];

    private static (BattleState State, List<GameEvent> Events) PhaseStart(BattleState state, int turn = 2)
    {
        var events = new List<GameEvent>();
        var next = Kinsbane.AtPhaseStart(state with { Turn = turn }, Shipped, Side.Player, events);
        return (next, events);
    }

    [Fact]
    public void TheKinsbaneHeaderPutsTheScytheInFrontOfTheBearersPackAtFullUses()
    {
        var keziah = Keziah(Placed());
        var weapon = Shipped.Weapon(Kinsbane.ItemId);

        Assert.True(weapon.Hungers);
        Assert.Equal(new ItemStack(Kinsbane.ItemId, weapon.Durability), keziah.Unit.Inventory.Items[0]);
        Assert.Equal(Kinsbane.ItemId, keziah.EquippedWeapon(Shipped)!.Id);
        Assert.Equal(Shipped.Cast.Single(u => u.Id == "keziah").Inventory.Count + 1, keziah.Unit.Inventory.Count);
        Assert.DoesNotContain(Placed().Units.Where(u => u.Id != "keziah"), u => u.Unit.Inventory.Items.Any(s => s.ItemId == Kinsbane.ItemId));
    }

    [Fact]
    public void TheKinsbaneHeaderRoundTripsAndTheSampleIsCanonical()
    {
        var sample = MapFiles.Load(SamplePath, Shipped);

        Assert.Equal("keziah", sample.KinsbaneBearer);
        Assert.Equal(File.ReadAllText(SamplePath).Replace("\r\n", "\n"), MapFormat.Write(sample, Shipped));
    }

    [Fact]
    public void AKinsbaneHeaderNamingNoPlacedRecruitIsRefused()
    {
        var text = File.ReadAllText(SamplePath).Replace("kinsbane: keziah", "kinsbane: rook");

        var error = Assert.Throws<MapException>(() => MapFormat.Parse("gleaning.map", text, Shipped));
        Assert.Contains("kinsbane names 'rook'", error.Message);
    }

    [Theory]
    [InlineData("\"price\": 100, ")]
    [InlineData("\"type\": \"reason\", ")]
    public void AHungeringWeaponWithAPriceOrMagicIsRefusedByTheValidator(string extra)
    {
        var line = """{ "id": "scythe", "name": "Scythe", "type": "axe", "mt": 9, "hit": 70, "crit": 0, "wt": 10, "minRange": 1, "maxRange": 1, "durability": 20, "rank": "E", "hungers": true, "description": "A test line." }""";
        line = extra.StartsWith("\"type\"") ? line.Replace("\"type\": \"axe\", ", extra) : line.Replace("\"rank\"", extra + "\"rank\"");
        var weapons = Fixture.Weapons.Replace("\n] }", ",\n" + line + "\n] }");
        Assert.Contains("scythe", weapons);

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(weapons: weapons)));
        Assert.Contains("hungers", error.Message);
    }

    [Theory]
    [InlineData(23, 18, false)]
    [InlineData(7, 2, false)]
    [InlineData(6, 1, true)]
    [InlineData(3, 1, true)]
    [InlineData(1, 1, true)]
    public void TheDrainTakesFiveAndStarvesTheWeaponWhenItWouldReachOne(int hp, int after, bool starved)
    {
        var (next, events) = PhaseStart(WithScythe(Placed(), hp));

        Assert.Equal(after, Keziah(next).Hp);
        Assert.Equal(new HungerDrained("keziah", Kinsbane.ItemId, hp - after, after, starved), Assert.Single(events));
        Assert.Equal(starved, Scythe(next).Starved);
        Assert.Equal(starved ? 1 : 20, Scythe(next).Uses);
    }

    [Fact]
    public void TheDrainNeverTakesAStarvedCarrierBelowOneAndSaysNothingMore()
    {
        var (next, events) = PhaseStart(WithScythe(Placed(), 1, starved: true, uses: 1));

        Assert.Equal(1, Keziah(next).Hp);
        Assert.Empty(events);
    }

    [Fact]
    public void TheDrainSkipsTheSidesFirstPhase()
    {
        var (next, events) = PhaseStart(WithScythe(Placed(), 23), turn: 1);

        Assert.Equal(23, Keziah(next).Hp);
        Assert.Empty(events);
    }

    [Fact]
    public void AFeedSinceTheLastPhaseStartSparesTheDrainAndTheFlagClears()
    {
        var (next, events) = PhaseStart(WithScythe(Placed(), 23, hasFed: true));

        Assert.Equal(23, Keziah(next).Hp);
        Assert.Empty(events);
        Assert.False(Keziah(next).HasFed);
    }

    [Fact]
    public void TheDrainReachesTheCarrierEvenWithTheScytheUnequipped()
    {
        var state = WithScythe(Placed(), 23);
        state = state.WithUnit(Keziah(state).WithSlotInFront(1));

        var (next, _) = PhaseStart(state);

        Assert.Equal(18, Keziah(next).Hp);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(8, 2)]
    [InlineData(14, 4)]
    [InlineData(15, 5)]
    [InlineData(40, 5)]
    public void TheScytheGrowsOneMtPerThreeKillsToFive(int fed, int bonus)
    {
        Assert.Equal(bonus, Kinsbane.MtBonus(fed));
        Assert.Equal(Shipped.Weapon(Kinsbane.ItemId).Mt + bonus, Keziah(WithScythe(Placed(), 23, fed: fed)).EquippedWeapon(Shipped)!.Mt);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(2, 0, false)]
    [InlineData(3, 1, true)]
    [InlineData(4, 1, false)]
    [InlineData(6, 2, true)]
    [InlineData(14, 4, false)]
    [InlineData(15, 5, true)]
    [InlineData(16, 5, false)]
    public void TheBladeGrowsAToothWithEachMtStepToFive(int fed, int teeth, bool grew)
    {
        Assert.Equal(teeth, Kinsbane.Teeth(fed));
        Assert.Equal(grew, Kinsbane.ToothGrew(fed));
    }

    [Fact]
    public void TheSimsPlayerTakesTheHuntWithAHungeringWeaponOverABetterScoringOne()
    {
        var start = WithScythe(Placed(), 23, starved: true, uses: 1);
        var state = start.WithUnit(Keziah(start) with { At = new Coord(6, 7) });
        var keziah = Keziah(state);
        var brigand = state.Find("brigand-1")!;
        var axe = keziah.Unit.Inventory.Items.ToList().FindIndex(s => s.ItemId == "iron_axe");
        var from = new Coord(6, 6);

        Assert.True(EnemyAi.Score(state, Shipped, keziah.WithSlotInFront(axe), from, brigand) > EnemyAi.Score(state, Shipped, keziah, from, brigand));

        var attack = Assert.IsType<Attack>(Ironwake.Sim.HeuristicPlayer.PlanUnit(state, Shipped, keziah)[^1]);

        Assert.Null(attack.Slot);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(14, true)]
    [InlineData(15, false)]
    public void AWokenWeaponIsNoLongerHunted(int fed, bool hunts)
    {
        var keziah = Keziah(WithScythe(Placed(), 23, fed: fed));
        var axe = keziah.Unit.Inventory.Items.ToList().FindIndex(s => s.ItemId == "iron_axe");

        Assert.Equal(hunts, Ironwake.Sim.HeuristicPlayer.Hunts(keziah, 0, keziah.EquippedWeapon(Shipped)!));
        Assert.False(Ironwake.Sim.HeuristicPlayer.Hunts(keziah, axe, Shipped.Weapon("iron_axe")));
    }

    [Fact]
    public void TheFeedLineNamesTheToothOnlyWhenOneGrows()
    {
        var grew = new HungerFed("keziah", Kinsbane.ItemId, 6, 10, 20, 2, false);
        var same = new HungerFed("keziah", Kinsbane.ItemId, 7, 10, 20, 2, false);

        Assert.Contains("a tooth grows (teeth 2/5)", PlaySession.Describe(grew, Shipped, UnitNames.None), StringComparison.Ordinal);
        Assert.DoesNotContain("tooth", PlaySession.Describe(same, Shipped, UnitNames.None), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(3, 5)]
    [InlineData(15, 7)]
    public void TheStarvedFormHalvesMtRoundedDown(int fed, int mt)
    {
        Assert.Equal(mt, Keziah(WithScythe(Placed(), 1, fed: fed, starved: true, uses: 1)).EquippedWeapon(Shipped)!.Mt);
    }

    [Theory]
    [InlineData(6, 16)]
    [InlineData(20, 23)]
    public void AKillFeedsTheScytheHealsTenToMaxAndRestoresItsUses(int hp, int after)
    {
        var events = new List<GameEvent>();
        var fed = Kinsbane.AfterCombat(Keziah(WithScythe(Placed(), hp, fed: 2, uses: 7)), Shipped, ValueList<StrikeEvent>.Empty, killed: true, events);

        Assert.Equal(after, fed.Hp);
        Assert.True(fed.HasFed);
        Assert.Equal(new ItemStack(Kinsbane.ItemId, 20) { Fed = 3 }, fed.Unit.Inventory.Items[0]);
        Assert.Equal(new HungerFed("keziah", Kinsbane.ItemId, 3, after - hp, after, 1, false), Assert.Single(events));
    }

    [Fact]
    public void AKillEndsTheStarvedForm()
    {
        var events = new List<GameEvent>();
        var fed = Kinsbane.AfterCombat(Keziah(WithScythe(Placed(), 1, starved: true, uses: 1)), Shipped, ValueList<StrikeEvent>.Empty, killed: true, events);

        Assert.Equal(11, fed.Hp);
        Assert.False(fed.Unit.Inventory.Items[0].Starved);
        Assert.Equal(20, fed.Unit.Inventory.Items[0].Uses);
    }

    [Fact]
    public void AStarvedHitThatKillsNothingEasesItForFive()
    {
        var events = new List<GameEvent>();
        var hit = ValueList<StrikeEvent>.Of(new StrikeEvent(0, "keziah", "archer-1", true, false, 4, 13));

        var eased = Kinsbane.AfterCombat(Keziah(WithScythe(Placed(), 1, starved: true, uses: 1)), Shipped, hit, killed: false, events);

        Assert.Equal(6, eased.Hp);
        Assert.False(eased.Unit.Inventory.Items[0].Starved);
        Assert.Equal(20, eased.Unit.Inventory.Items[0].Uses);
        Assert.False(eased.HasFed);
        Assert.Equal(new HungerEased("keziah", Kinsbane.ItemId, 5, 6), Assert.Single(events));
    }

    [Fact]
    public void AStarvedMissChangesNothing()
    {
        var events = new List<GameEvent>();
        var miss = ValueList<StrikeEvent>.Of(new StrikeEvent(0, "keziah", "archer-1", false, false, 0, 17));
        var starved = Keziah(WithScythe(Placed(), 1, starved: true, uses: 1));

        Assert.Equal(starved, Kinsbane.AfterCombat(starved, Shipped, miss, killed: false, events));
        Assert.Empty(events);
    }

    [Fact]
    public void AnUnstarvedHitThatKillsNothingChangesNothing()
    {
        var events = new List<GameEvent>();
        var hit = ValueList<StrikeEvent>.Of(new StrikeEvent(0, "keziah", "archer-1", true, false, 11, 6));
        var keziah = Keziah(WithScythe(Placed(), 10));

        Assert.Equal(keziah, Kinsbane.AfterCombat(keziah, Shipped, hit, killed: false, events));
        Assert.Empty(events);
    }

    [Fact]
    public void TheKillThatReachesTheCapWakesItAndItNeitherHealsNorDrainsAfter()
    {
        var events = new List<GameEvent>();
        var woke = Kinsbane.AfterCombat(Keziah(WithScythe(Placed(), 10, fed: 14)), Shipped, ValueList<StrikeEvent>.Empty, killed: true, events);
        Assert.Equal(new HungerFed("keziah", Kinsbane.ItemId, 15, 10, 20, 5, true), Assert.Single(events));

        events.Clear();
        var after = Kinsbane.AfterCombat(woke with { Hp = 10 }, Shipped, ValueList<StrikeEvent>.Empty, killed: true, events);
        Assert.Equal(new HungerFed("keziah", Kinsbane.ItemId, 16, 0, 10, 5, false), Assert.Single(events));
        Assert.Equal(10, after.Hp);

        var (next, drained) = PhaseStart(WithScythe(Placed(), 3, fed: 15));
        Assert.Equal(3, Keziah(next).Hp);
        Assert.Empty(drained);
        Assert.Null(Kinsbane.Coming(Keziah(next), Shipped));
    }

    [Fact]
    public void AStrikeWithTheScytheNeverSpendsItBelowOne()
    {
        var state = WithScythe(Placed(), 23, uses: 1);
        var brigand = state.Find("brigand-1")!;
        state = state.WithUnit(Keziah(state) with { At = new Coord(brigand.At.X - 1, brigand.At.Y) });

        var result = Resolver.Apply(state, Shipped, new Attack("keziah", "brigand-1"));

        Assert.True(result.Accepted);
        Assert.Equal(1, Scythe(result.Next).Uses);
        Assert.DoesNotContain(result.Events, e => e is WeaponBroke);
    }

    [Fact]
    public void AKillThroughTheResolverFeedsTheScythe()
    {
        for (ulong seed = 1; seed < 64; seed++)
        {
            var state = WithScythe(Placed(seed), 23);
            var brigand = state.Find("brigand-1")!;
            state = state.WithUnit(brigand with { Hp = 1 }).WithUnit(Keziah(state) with { At = new Coord(brigand.At.X - 1, brigand.At.Y) });

            var result = Resolver.Apply(state, Shipped, new Attack("keziah", "brigand-1"));
            if (result.Events.OfType<UnitDied>().Any())
            {
                Assert.Equal(1, Scythe(result.Next).Fed);
                Assert.True(Keziah(result.Next).HasFed);
                Assert.Contains(new HungerFed("keziah", Kinsbane.ItemId, 1, 0, 23, 0, false), result.Events);
                return;
            }
        }

        Assert.Fail("no seed under 64 lands a hit on a brigand at 1 hp");
    }

    [Fact]
    public void ThePlayerPhaseStartOfTurnTwoDrainsAnUnfedCarrier()
    {
        var state = WithScythe(Placed(), 23);
        state = state.WithUnit(Keziah(state) with { At = new Coord(1, 8) });

        var result = Resolver.Apply(state, Shipped, new EndPhase());
        var events = result.Events.ToList();
        while (result.Next.Phase == Side.Enemy)
        {
            result = Resolver.Apply(result.Next, Shipped, new EndPhase());
            events.AddRange(result.Events);
        }

        var drain = Assert.Single(events.OfType<HungerDrained>());
        Assert.Equal("keziah", drain.UnitId);
        Assert.Equal(Keziah(result.Next).Hp, drain.HpAfter);
    }

    [Fact]
    public void RecallRestoresTheScytheWithTheBoard()
    {
        var state = WithScythe(Placed(), 23, fed: 2);
        state = state.WithUnit(Keziah(state) with { At = new Coord(1, 8) });
        var result = Resolver.Apply(state, Shipped, new EndPhase());
        while (result.Next.Phase == Side.Enemy)
        {
            result = Resolver.Apply(result.Next, Shipped, new EndPhase());
        }

        Assert.Equal(18, Keziah(result.Next).Hp);
        var back = Resolver.Apply(result.Next, Shipped, new Recall(0)).Next;
        Assert.Equal(23, Keziah(back).Hp);
        Assert.Equal(Scythe(state), Scythe(back));
    }

    [Fact]
    public void TheProtocolCarriesTheFeedCountTheStarvedFormAndTheFlag()
    {
        var state = WithScythe(Placed(), 1, fed: 4, starved: true, uses: 1, hasFed: true);

        var back = Ironwake.Content.Protocol.ProtocolJson.ReadState(Ironwake.Content.Protocol.ProtocolJson.State(state, Shipped), Shipped);

        Assert.Equal(Keziah(state), Keziah(back));
    }

    [Fact]
    public void TheForecastPrintsTheFeedAndTheStarvedHit()
    {
        var state = WithScythe(Placed(), 1, starved: true, uses: 1);
        var lines = PlaySession.HungerLines(Shipped, Keziah(state), state.Find("brigand-1")!, null, true).ToList();

        Assert.Equal(new[] { "  kill: keziah +10 HP, to max 23 (Kinsbane feeds, fed 1)", "  hit: keziah +5 HP, the starved form ends" }, lines);
        Assert.Empty(PlaySession.HungerLines(Shipped, Keziah(state), state.Find("brigand-1")!, 1, true));
        Assert.Empty(PlaySession.HungerLines(Shipped, Keziah(WithScythe(Placed(), 20, fed: 15)), state.Find("brigand-1")!, null, true));
    }

    [Fact]
    public void TheForecastPrintsACounterKillOnlyWhenTheCarrierCounters()
    {
        var state = WithScythe(Placed(), 20);
        var brigand = state.Find("brigand-1")!;

        Assert.Single(PlaySession.HungerLines(Shipped, brigand, Keziah(state), null, true));
        Assert.Empty(PlaySession.HungerLines(Shipped, brigand, Keziah(state), null, false));
    }

    [Fact]
    public void TheUnitCardPrintsTheCountTheGrowthAndTheStateComing()
    {
        Assert.Equal("Kinsbane: fed 4, teeth 1/5. Power +1. Hungry: -5 HP at the next phase start.", Kinsbane.Card(Keziah(WithScythe(Placed(), 23, fed: 4)), Shipped));
        Assert.Equal("Kinsbane: fed 4, teeth 1/5. Power +1. Hungry: -5 HP at the next phase start, and it starves.", Kinsbane.Card(Keziah(WithScythe(Placed(), 6, fed: 4)), Shipped));
        Assert.Equal("Kinsbane: fed 4, teeth 1/5. Power +1. Fed this phase.", Kinsbane.Card(Keziah(WithScythe(Placed(), 6, fed: 4, hasFed: true)), Shipped));
        Assert.StartsWith("Kinsbane: fed 0, teeth 0/5. Power +0. Starved: half Power, uses 1", Kinsbane.Card(Keziah(WithScythe(Placed(), 1, starved: true, uses: 1)), Shipped));
        Assert.Equal("Kinsbane: fed 15, teeth 5/5. Power +5. Woken: no drain.", Kinsbane.Card(Keziah(WithScythe(Placed(), 6, fed: 15)), Shipped));
        Assert.Null(Kinsbane.Card(Placed().Find("captain")!, Shipped));
        Assert.Contains(PlaySession.ShowLines(Placed(), Shipped, Keziah(Placed())), line => line.StartsWith("  Kinsbane: fed 0, teeth 0/5.", StringComparison.Ordinal));
    }
}
