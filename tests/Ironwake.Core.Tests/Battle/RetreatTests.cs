using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Enemy retreat (DESIGN.md 13.10, issue 33): on a map with <c>retreat: on</c>, an
/// Aggressive enemy below 30 percent HP with a healing tile in reach falls back to it and
/// does not attack; one with no such tile fights; none retreats twice; the resolver
/// refuses every retreat the rule does not allow; the header parses and writes back.
/// </summary>
public class RetreatTests
{
    /// <summary>
    /// An 8x4 yard with forts at 2,0 and 6,0. The brigand at 4,1 (Mov 4, max HP 22) reaches
    /// both at cost 3 and can strike Hale at 0,1 from 1,1. Hale and Wren (Mov 4, iron
    /// swords) reach 2,0 and can strike it next phase; neither can strike 6,0.
    /// </summary>
    private const string Refuge = """
        name: Refuge
        size: 8x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        retreat: on

        ..F...F.
        ........
        ........
        ........

        units:
        P captain 0,1
        P recruit:wren 0,2
        E brigand 4,1 group:yard behavior:aggressive

        """;

    private static readonly Coord FarFort = new(6, 0);

    private static BattleState EnemyPhase(string map = Refuge, int hp = 6, bool retreated = false)
    {
        var state = Start(map: map).Do(new EndPhase());
        var brigand = state.Find("brigand-1")!;
        return state.WithUnit(brigand with { Hp = hp, Retreated = retreated });
    }

    private static IReadOnlyList<Command> PlanOf(BattleState state) =>
        EnemyAi.PlanUnit(state, Starter, state.Find("brigand-1")!);

    [Fact]
    public void AnAggressiveEnemyBelowThirtyPercentWithAReachableFortRetreatsAndDoesNotAttack()
    {
        var state = EnemyPhase();

        Assert.Equal(new Command[] { new Retreat("brigand-1", FarFort) }, PlanOf(state));

        var result = Resolver.Apply(state, Starter, new Retreat("brigand-1", FarFort));
        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(new UnitRetreated("brigand-1", new Coord(4, 1), FarFort), result.Events[0]);
        Assert.DoesNotContain(result.Events, e => e is CombatFought);
        var after = result.Next.Find("brigand-1")!;
        Assert.Equal(FarFort, after.At);
        Assert.True(after.Retreated);
        Assert.True(after.Acted);
    }

    [Fact]
    public void TheWholeEnemyPhaseRetreatsAndTheFortHealsAtTheNextEnemyPhase()
    {
        var state = EnemyPhase();
        foreach (var command in EnemyAi.Plan(state, Starter))
        {
            state = state.Do(command);
        }

        Assert.Equal(6, state.Find("brigand-1")!.Hp);
        state = state.Do(new EndPhase());
        var brigand = state.Find("brigand-1")!;
        Assert.Equal(6 + brigand.MaxHp(Starter) * 20 / 100, brigand.Hp);
    }

    [Fact]
    public void AFortAPlayerUnitCanStrikeNextPhaseIsNoRefuge()
    {
        var state = EnemyPhase();
        var tiles = RetreatRule.Tiles(state, Starter, state.Find("brigand-1")!);

        Assert.Contains(new Coord(2, 0), RetreatRule.Struck(state, Starter));
        Assert.DoesNotContain(FarFort, RetreatRule.Struck(state, Starter));
        Assert.Equal(new[] { FarFort }, tiles);
        Assert.Contains("not a healing tile", state.Refused(new Retreat("brigand-1", new Coord(2, 0))).Message);
    }

    [Fact]
    public void AnEnemyWhoseOnlyReachableFortAPlayerUnitCanStrikeStandsAndFights()
    {
        var state = EnemyPhase(Refuge.Replace("..F...F.", "..F....."));

        Assert.Empty(RetreatRule.Tiles(state, Starter, state.Find("brigand-1")!));
        Assert.Contains(PlanOf(state), c => c is Attack);
        Assert.DoesNotContain(PlanOf(state), c => c is Retreat);
    }

    [Fact]
    public void AnEnemyAlreadyOnAFortFightsAndDoesNotRetreatInPlaceOrAway()
    {
        var state = EnemyPhase(Refuge.Replace("E brigand 4,1", "E brigand 2,0"));
        var brigand = state.Find("brigand-1")!;

        Assert.Contains(PlanOf(state), c => c is Attack);
        Assert.DoesNotContain(PlanOf(state), c => c is Retreat);
        Assert.Contains("already stands on healing terrain", state.Refused(new Retreat("brigand-1", new Coord(2, 0))).Message);
        Assert.Contains("already stands on healing terrain", state.Refused(new Retreat("brigand-1", FarFort)).Message);
        Assert.Equal(new Coord(2, 0), brigand.At);
    }

    [Fact]
    public void ABowThatStrikesTheFortWithoutEndingOnItCountsAsReach()
    {
        var bowman = Recruit("wren", "bowman", Wren.Stats, "iron_bow");
        var map = Refuge.Replace("P recruit:wren 0,2", "P recruit:wren 0,0");
        var state = Start(map: map, roster: ValueList<Unit>.Of(Hale, bowman)).Do(new EndPhase());
        state = state.WithUnit(state.Find("brigand-1")! with { Hp = 6 });

        Assert.False(state.ReachOf(state.Find("wren")!, Starter).CanEnd(FarFort));
        Assert.Contains(FarFort, RetreatRule.Struck(state, Starter));
        Assert.Empty(RetreatRule.Tiles(state, Starter, state.Find("brigand-1")!));
        Assert.Contains(PlanOf(state), c => c is Attack);
    }

    [Fact]
    public void AnEnemyWithNoReachableFortStandsAndFights()
    {
        var state = EnemyPhase(Refuge.Replace("E brigand 4,1", "E brigand 4,3"));

        Assert.Empty(RetreatRule.Tiles(state, Starter, state.Find("brigand-1")!));
        Assert.Contains(PlanOf(state), c => c is Attack);
    }

    [Fact]
    public void AnEnemyThatHasRetreatedDoesNotRetreatASecondTime()
    {
        var state = EnemyPhase(retreated: true);

        Assert.DoesNotContain(PlanOf(state), c => c is Retreat);
        var rejection = state.Refused(new Retreat("brigand-1", FarFort));
        Assert.Equal(RejectionReason.CannotRetreat, rejection.Reason);
        Assert.Contains("already retreated once", rejection.Message);
    }

    /// <summary>
    /// Issue 215: a refugee strictly below half its max HP (22, so 10 holds and 11 does not)
    /// has the effective behavior Hold. On the far fort with Hale beside it, it strikes him
    /// from its tile and does not move.
    /// </summary>
    [Fact]
    public void ARefugeeBelowHalfHpHoldsItsRefugeAndStrikesFromItsTile()
    {
        var state = EnemyPhase(hp: 10, retreated: true);
        state = state.WithUnit(state.Find("brigand-1")! with { At = FarFort });
        state = state.WithUnit(state.Find("hale")! with { At = new Coord(6, 1) });
        var brigand = state.Find("brigand-1")!;

        Assert.True(RetreatRule.Holds(brigand, Starter));
        Assert.Equal(Behavior.Hold, state.EffectiveBehavior(brigand, Starter));
        Assert.Equal(new Command[] { new Attack("brigand-1", "hale") }, PlanOf(state));
    }

    [Fact]
    public void ARefugeeBelowHalfHpWithNothingInRangeWaitsOnItsRefuge()
    {
        var state = EnemyPhase(hp: 10, retreated: true);
        state = state.WithUnit(state.Find("brigand-1")! with { At = FarFort });

        Assert.Equal(new Command[] { new Wait("brigand-1") }, PlanOf(state));
        Assert.Equal(FarFort, state.Find("brigand-1")!.At);
    }

    [Fact]
    public void ARefugeeAtHalfHpIsAggressiveAgainAndReturns()
    {
        var state = EnemyPhase(hp: 11, retreated: true);
        state = state.WithUnit(state.Find("brigand-1")! with { At = FarFort });
        var brigand = state.Find("brigand-1")!;

        Assert.False(RetreatRule.Holds(brigand, Starter));
        Assert.Equal(Behavior.Aggressive, state.EffectiveBehavior(brigand, Starter));
        Assert.IsType<Move>(PlanOf(state)[0]);
    }

    [Theory]
    [InlineData(10, true, true)]
    [InlineData(11, true, false)]
    [InlineData(10, false, false)]
    public void ARefugeeHoldsStrictlyBelowHalfAndOnlyAfterARetreat(int hp, bool retreated, bool holds)
    {
        var brigand = EnemyPhase(hp: hp, retreated: retreated).Find("brigand-1")!;

        Assert.Equal(holds, RetreatRule.Holds(brigand, Starter));
    }

    [Fact]
    public void TheWholeCycleRetreatsHoldsTwiceAndReturnsOnTheThirdEnemyPhase()
    {
        var state = EnemyPhase(hp: 5);
        var moves = new List<bool>();
        for (var phase = 0; phase < 3; phase++)
        {
            var plan = EnemyAi.PlanUnit(state, Starter, state.Find("brigand-1")!);
            moves.Add(plan[0] is Move);
            if (phase == 0)
            {
                Assert.IsType<Retreat>(plan[0]);
            }

            foreach (var command in plan)
            {
                state = state.Do(command);
            }

            state = state.Do(new EndPhase()).Do(new EndPhase());
        }

        Assert.Equal(new[] { false, false, true }, moves);
    }

    /// <summary>
    /// Issue 215's forecast half: a strike that would leave the brigand alive and below 30
    /// percent names the refuge it would fall back to on the board as it stands; one that
    /// leaves it at 30 percent or kills it names none, and neither does a board whose only
    /// fort a player unit can strike.
    /// </summary>
    [Theory]
    [InlineData(6, true)]
    [InlineData(7, false)]
    [InlineData(0, false)]
    public void TheForecastNamesThePendingRetreatOnlyWhenTheStrikeLeavesItBelowThirtyPercent(int hpAfter, bool pending)
    {
        var state = Start(map: Refuge);
        var hale = state.Find("hale")!;

        var refuge = RetreatRule.Pending(state, Starter, hale, hale.At, state.Find("brigand-1")!, hpAfter);

        Assert.Equal(pending ? FarFort : null, refuge);
    }

    [Fact]
    public void TheForecastNamesNoRetreatWhenEveryReachableFortIsStruck()
    {
        var state = Start(map: Refuge.Replace("..F...F.", "..F....."));
        var hale = state.Find("hale")!;

        Assert.Null(RetreatRule.Pending(state, Starter, hale, hale.At, state.Find("brigand-1")!, 3));
    }

    [Fact]
    public void AnAttackerStrikingFromBesideTheRefugeCoversItAndTakesTheRetreatAway()
    {
        var state = Start(map: Refuge);
        var wren = state.Find("wren")!;
        var brigand = state.Find("brigand-1")!;

        Assert.Equal(FarFort, RetreatRule.Pending(state, Starter, wren, wren.At, brigand, 3));
        Assert.Null(RetreatRule.Pending(state, Starter, wren, new Coord(5, 1), brigand, 3));
    }

    [Fact]
    public void TheRetreatedMarkSurvivesTheTurn()
    {
        var state = EnemyPhase().Do(new Retreat("brigand-1", FarFort)).Do(new EndPhase()).Do(new EndPhase());
        var brigand = state.Find("brigand-1")!;
        state = state.WithUnit(brigand with { Hp = 2 });

        Assert.True(state.Find("brigand-1")!.Retreated);
        Assert.DoesNotContain(PlanOf(state), c => c is Retreat);
    }

    [Theory]
    [InlineData(6, true)]
    [InlineData(7, false)]
    public void TheThresholdIsStrictlyBelowThirtyPercent(int hp, bool retreats)
    {
        var state = EnemyPhase(hp: hp);

        Assert.Equal(retreats, PlanOf(state).Any(c => c is Retreat));
        if (!retreats)
        {
            Assert.Contains("not below 30 percent", state.Refused(new Retreat("brigand-1", FarFort)).Message);
        }
    }

    [Fact]
    public void AMapWithoutTheHeaderHasNoRetreat()
    {
        var state = EnemyPhase(Refuge.Replace("retreat: on\n", ""));

        Assert.DoesNotContain(PlanOf(state), c => c is Retreat);
        Assert.Contains("no retreat", state.Refused(new Retreat("brigand-1", FarFort)).Message);
    }

    [Fact]
    public void AHoldEnemyDoesNotRetreat()
    {
        var state = EnemyPhase(Refuge.Replace("behavior:aggressive", "behavior:hold"));

        Assert.DoesNotContain(PlanOf(state), c => c is Retreat);
        Assert.Contains("does not move on its own", state.Refused(new Retreat("brigand-1", FarFort)).Message);
    }

    [Fact]
    public void AWokenGuardRetreatsAsAggressive()
    {
        var state = EnemyPhase(Refuge.Replace("behavior:aggressive", "behavior:guard"));
        state = state.Wake("yard");

        Assert.Equal(new Command[] { new Retreat("brigand-1", FarFort) }, PlanOf(state));
    }

    [Fact]
    public void TheResolverRefusesARetreatToATileThatDoesNotHeal()
    {
        var state = EnemyPhase();

        var rejection = state.Refused(new Retreat("brigand-1", new Coord(5, 1)));

        Assert.Equal(RejectionReason.CannotRetreat, rejection.Reason);
        Assert.Contains("not a healing tile", rejection.Message);
    }

    [Fact]
    public void TheResolverRefusesARetreatByAPlayerUnit()
    {
        var state = Start(map: Refuge);
        state = state.WithUnit(state.Find("hale")! with { Hp = 2 });

        Assert.Contains("only enemies retreat", state.Refused(new Retreat("hale", new Coord(2, 0))).Message);
    }

    [Fact]
    public void TheResolverRefusesARetreatAfterAMove()
    {
        var state = EnemyPhase().Do(new Move("brigand-1", new Coord(5, 1)));

        Assert.Contains("already moved", state.Refused(new Retreat("brigand-1", FarFort)).Message);
    }

    [Fact]
    public void LegalListsOnlyTheRefugesNoPlayerUnitCanStrike()
    {
        var retreats = Resolver.Legal(EnemyPhase(), Starter).OfType<Retreat>().ToList();

        Assert.Equal(new[] { new Retreat("brigand-1", FarFort) }, retreats);
    }

    [Fact]
    public void ARecallRestoresTheUnitAsItWasBeforeItRetreated()
    {
        var state = Start(map: Refuge);
        state = state.WithUnit(state.Find("brigand-1")! with { Hp = 6 });
        state = state.Do(new EndPhase());
        foreach (var command in EnemyAi.Plan(state, Starter))
        {
            state = state.Do(command);
        }

        state = state.Do(new Recall(0));

        Assert.False(state.Find("brigand-1")!.Retreated);
    }

    [Fact]
    public void TheRetreatHeaderParsesAndWritesBack()
    {
        var map = MapFixture.Parse(Refuge);

        Assert.True(map.RetreatEnabled);
        Assert.Equal(Refuge.Replace("\r\n", "\n"), MapFormat.Write(map, Starter));
        Assert.False(MapFixture.Parse(Yard).RetreatEnabled);
    }

    [Fact]
    public void TheRetreatHeaderRefusesAnyValueButOn()
    {
        var error = Assert.Throws<MapException>(() => MapFixture.Parse(Refuge.Replace("retreat: on", "retreat: yes")));

        Assert.Contains("retreat may only be 'on'", error.Message);
    }

    [Fact]
    public void TheRetreatSampleIsOldMillRoadWithTheHeader()
    {
        var repo = Directory.GetParent(Ironwake.Core.Tests.Content.Fixture.RealContentDirectory())!.FullName;
        var text = File.ReadAllText(Path.Combine(repo, "docs", "samples", "old_mill_road_retreat.map")).Replace("\r\n", "\n");
        var shipped = File.ReadAllText(Path.Combine(repo, "content", "maps", "old_mill_road.map")).Replace("\r\n", "\n");
        var map = MapFixture.Parse(text, "old_mill_road_retreat.map");

        Assert.Equal(text, MapFormat.Write(map, Starter));
        Assert.True(map.RetreatEnabled);
        Assert.Equal(MapFixture.Parse(shipped, "old_mill_road.map") with { Name = map.Name, RetreatEnabled = true }, map);
    }

    [Fact]
    public void TheRiverSampleIsCanonicalAndItsFortIsOutOfThePartysStrikeReach()
    {
        var repo = Directory.GetParent(Ironwake.Core.Tests.Content.Fixture.RealContentDirectory())!.FullName;
        var text = File.ReadAllText(Path.Combine(repo, "docs", "samples", "river_refuge_retreat.map")).Replace("\r\n", "\n");
        var map = MapFixture.Parse(text, "river_refuge_retreat.map");

        Assert.Equal(text, MapFormat.Write(map, Starter));
        Assert.True(map.RetreatEnabled);

        var state = BattleState.From(map, Starter, Starter.Cast, 3);
        Assert.Equal("fort", map.TerrainIdAt(new Coord(2, 2)));
        Assert.DoesNotContain(new Coord(2, 2), RetreatRule.Struck(state, Starter));
    }

    /// <summary>
    /// Issue 215's sample: the party deploys at rows 7 and 8, so a wingrider that engages
    /// it from the river side and drops low still has the fort at 2,2 in its reach (Mov 6),
    /// one that comes round to the south flank does not, and no deployed unit can strike
    /// the fort from the south bank.
    /// </summary>
    [Fact]
    public void TheHoldSampleIsCanonicalAndAWingriderBesideThePartyHasTheFortInReach()
    {
        var repo = Directory.GetParent(Ironwake.Core.Tests.Content.Fixture.RealContentDirectory())!.FullName;
        var text = File.ReadAllText(Path.Combine(repo, "docs", "samples", "river_refuge_hold.map")).Replace("\r\n", "\n");
        var map = MapFixture.Parse(text, "river_refuge_hold.map");
        var fort = new Coord(2, 2);

        Assert.Equal(text, MapFormat.Write(map, Starter));
        Assert.True(map.RetreatEnabled);

        var state = BattleState.From(map, Starter, Starter.Cast, 3);
        Assert.DoesNotContain(fort, RetreatRule.Struck(state, Starter));
        foreach (var player in state.UnitsOf(Side.Player))
        {
            Assert.True(player.At.Y is 7 or 8, player.Id);
        }

        state = state.Do(new EndPhase());
        var rider = state.Find("wingrider-1")!;
        foreach (var beside in new[] { new Coord(2, 6), new Coord(3, 6), new Coord(1, 7) })
        {
            var low = state.WithUnit(rider with { At = beside, Hp = 5 });
            Assert.Equal(fort, RetreatRule.Choose(low, Starter, low.Find("wingrider-1")!));
        }

        foreach (var south in new[] { new Coord(1, 8), new Coord(3, 8), new Coord(4, 7) })
        {
            var low = state.WithUnit(rider with { At = south, Hp = 5 });
            Assert.Null(RetreatRule.Choose(low, Starter, low.Find("wingrider-1")!));
        }
    }
}
