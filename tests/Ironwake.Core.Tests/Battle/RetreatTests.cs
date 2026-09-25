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
    /// both at cost 3 and can strike Hale at 0,1 from 1,1. Hale and Wren (Mov 4) reach 2,0
    /// and not 6,0.
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
    public void TheRetreatPicksTheFortFewestPlayerUnitsCanReach()
    {
        var state = EnemyPhase();
        var tiles = RetreatRule.Tiles(state, Starter, state.Find("brigand-1")!);

        Assert.Equal(new[] { FarFort, new Coord(2, 0) }, tiles);
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

        Assert.Contains(PlanOf(state), c => c is Attack);
        Assert.Equal(RejectionReason.CannotRetreat, state.Refused(new Retreat("brigand-1", FarFort)).Reason);
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
    public void LegalListsEachRetreatTileBestFirst()
    {
        var retreats = Resolver.Legal(EnemyPhase(), Starter).OfType<Retreat>().ToList();

        Assert.Equal(new[] { new Retreat("brigand-1", FarFort), new Retreat("brigand-1", new Coord(2, 0)) }, retreats);
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
}
