using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The boss veto prices the player phase that follows (issue 385); a unit that spent Full Measure
/// rests through that phase (issue 636, DECISIONS/0125 and 0170), so the veto weighs it at nothing
/// (issue 966). A unit resting now is free again by then and is still weighed.
/// </summary>
public class BossVetoRestingTests
{
    /// <summary>A 9x3 field: the captain at 1,1, a woken guard boss alone in group hall at 6,1.</summary>
    private const string Field = """
        name: Field
        size: 9x3
        win: defeat_boss
        turn_limit: 10
        recall: 3
        enemy_level: 1

        .........
        .........
        .........

        units:
        P captain 1,1
        B grange_reeve 6,1 group:hall behavior:guard

        """;

    private static BattleState Woken(int spent)
    {
        var state = Start(roster: ValueList<Unit>.Of(Hale), map: Field).Wake("hall").Do(new EndPhase());
        return state.WithUnit(state.Find("hale")! with { Spent = spent });
    }

    private static readonly Coord Approach = new(3, 1);

    [Fact]
    public void TheBossVetoWeighsAUnitThatRestsNextPhaseAtNothing()
    {
        var fresh = Woken(spent: 0);
        var spent = Woken(spent: 1);
        var reeve = fresh.Find("grange_reeve-1")!;

        Assert.True(Exposure.OfBoss(fresh, Starter, reeve, Approach) > 0);
        Assert.Equal(0, Exposure.OfBoss(spent, Starter, reeve, Approach));
    }

    [Fact]
    public void TheBossVetoStillWeighsAUnitRestingNowSinceItIsFreeByTheNextPlayerPhase()
    {
        var fresh = Woken(spent: 0);
        var resting = Woken(spent: 2);
        var reeve = fresh.Find("grange_reeve-1")!;

        Assert.Equal(Exposure.OfBoss(fresh, Starter, reeve, Approach), Exposure.OfBoss(resting, Starter, reeve, Approach));
    }

    /// <summary>The Reeve wounded to exactly the captain's worst, so the captain alone decides the veto.</summary>
    [Fact]
    public void ABossVetoRefusedOnlyByASpentCaptainLetsTheBossApproach()
    {
        var fresh = Woken(spent: 0);
        var wounded = fresh.Find("grange_reeve-1")! with { Hp = Exposure.OfBoss(fresh, Starter, fresh.Find("grange_reeve-1")!, Approach) };
        fresh = fresh.WithUnit(wounded);
        var spent = Woken(spent: 1).WithUnit(wounded);
        var reeve = fresh.Find("grange_reeve-1")!;

        Assert.True(reeve.Hp > 0);
        Assert.True(EnemyAi.BossVetoRefuses(fresh, Starter, reeve, Approach));
        Assert.False(EnemyAi.BossVetoRefuses(spent, Starter, reeve, Approach));
    }

    [Fact]
    public void ATileASpentUnitStandsOnStaysShutToTheOthers()
    {
        var map = Field.Replace("P captain 1,1", "P captain 1,1\nP recruit 4,0");
        var state = Start(roster: ValueList<Unit>.Of(Hale, Wren), map: map).Wake("hall").Do(new EndPhase());
        var reeve = state.Find("grange_reeve-1")!;
        var wren = state.Find("wren")!;
        var tile = new Coord(5, 1);

        var open = Exposure.OfBoss(state, Starter, reeve, tile);
        var shut = Exposure.OfBoss(state.WithUnit(wren with { Spent = 1 }), Starter, reeve, tile);
        var gone = Exposure.OfBoss(state.WithoutUnit(wren.Id), Starter, reeve, tile);

        Assert.True(shut <= gone);
        Assert.True(shut < open);
    }
    /// <summary>
    /// <c>threat</c>'s refusal line reads the same veto (<see cref="Queries.Refusals"/> through
    /// <see cref="EnemyAi.Refusal"/>): once the captain has spent Full Measure in his phase, the
    /// boss the party held off is no longer named as refusing, and his strike is priced instead.
    /// </summary>
    [Fact]
    public void ThreatStopsNamingARefusalOnceTheCaptainSpentFullMeasure()
    {
        var map = Field.Replace("P captain 1,1", "P captain 1,1\nP recruit 2,0");
        var fresh = Start(roster: ValueList<Unit>.Of(Hale, Wren), map: map).Wake("hall");
        var spent = fresh.WithUnit(fresh.Find("hale")! with { Spent = 1 });
        var wren = fresh.Find("wren")!;

        Assert.Single(Queries.Refusals(fresh, Starter, wren, wren.At)!);
        Assert.Empty(Queries.Refusals(spent, Starter, wren, wren.At)!);
        Assert.Contains(Queries.Threats(spent, Starter, wren, wren.At)!, line => line.Enemy.Id == "grange_reeve-1");
    }
}
