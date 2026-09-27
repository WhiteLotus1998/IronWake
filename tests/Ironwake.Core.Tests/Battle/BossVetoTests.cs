using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The boss veto (issue 385, DESIGN.md section 8): on a Defeat Boss map the boss plans under
/// the exposure veto the Sim's captain plays by. A strike or an approach tile where the
/// party's no-crit sum, the counter included, reaches the boss's HP is refused; with every
/// tile refused the boss holds.
/// </summary>
public class BossVetoTests
{
    /// <summary>A 9x3 field: the party at the west end, a woken guard boss alone in group hall at 6,1, five from the captain.</summary>
    private static string Field(string win, int players) => $"""
        name: Field
        size: 9x3
        win: {win}
        turn_limit: 10
        recall: 3
        enemy_level: 1

        .........
        .........
        .........

        units:
        P captain 1,1
        {string.Join("\n", new[] { "P recruit 1,0", "P recruit 1,2" }.Take(players - 1))}
        B grange_reeve 6,1 group:hall behavior:guard

        """;

    private static BattleState Woken(string win, ValueList<Unit> roster) =>
        Start(roster: roster, map: Field(win, roster.Count)).Wake("hall").Do(new EndPhase());

    private static readonly ValueList<Unit> Party = ValueList<Unit>.Of(Hale, Wren, Ivo);

    [Fact]
    public void ADefeatBossBossRefusesAStrikeWhereThePartysSumKillsIt()
    {
        var state = Woken("defeat_boss", Party);
        var reeve = state.Find("grange_reeve-1")!;
        var captain = state.Find("hale")!;

        Assert.True(EnemyAi.BossVetoApplies(state, Starter, reeve));
        Assert.True(EnemyAi.BossVetoRefuses(state, Starter, reeve, new Coord(2, 1), captain));
        var plan = EnemyAi.PlanUnit(state, Starter, reeve);
        Assert.DoesNotContain(plan, c => c is Attack);
        Assert.Null(EnemyAi.StrikeOn(state, Starter, reeve, captain));
        var ends = plan.OfType<Move>().Select(m => m.To).DefaultIfEmpty(reeve.At).Single();
        Assert.True(ends == reeve.At || !EnemyAi.BossVetoRefuses(state, Starter, reeve, ends));
    }

    [Fact]
    public void ADefeatBossBossStrikesWhereThePartysSumDoesNotKillIt()
    {
        var state = Woken("defeat_boss", ValueList<Unit>.Of(Unarmed));
        var reeve = state.Find("grange_reeve-1")!;

        Assert.Equal(0, Exposure.OfBoss(state, Starter, reeve, new Coord(2, 1), state.Find("pell")));
        Assert.IsType<Attack>(EnemyAi.PlanUnit(state, Starter, reeve)[^1]);
    }

    [Fact]
    public void TheBossVetoDoesNotApplyOffADefeatBossMap()
    {
        var state = Woken("rout", Party);
        var reeve = state.Find("grange_reeve-1")!;

        Assert.False(EnemyAi.BossVetoApplies(state, Starter, reeve));
        Assert.IsType<Attack>(EnemyAi.PlanUnit(state, Starter, reeve)[^1]);
    }

    [Fact]
    public void TheBossVetoDoesNotApplyToABossThatHolds()
    {
        var state = Start(roster: Party, map: Field("defeat_boss", 3)).Do(new EndPhase());
        var reeve = state.Find("grange_reeve-1")!;

        Assert.Equal(Behavior.Hold, state.EffectiveBehavior(reeve, Starter));
        Assert.False(EnemyAi.BossVetoApplies(state, Starter, reeve));
    }

    [Fact]
    public void TheBossVetoDoesNotApplyToAnOrdinaryEnemy()
    {
        var map = Field("defeat_boss", 3).Replace("B grange_reeve 6,1 group:hall behavior:guard", "B grange_reeve 8,2 group:far behavior:boss\nE soldier 6,1 group:hall behavior:aggressive");
        var state = Start(roster: Party, map: map).Do(new EndPhase());

        Assert.False(EnemyAi.BossVetoApplies(state, Starter, state.Find("soldier-1")!));
    }

    [Fact]
    public void TheBossSumSeatsOneStrikerPerTile()
    {
        var shared = new Coord(3, 1);
        var lines = new List<(int Weight, IReadOnlyList<Coord> Tiles)>
        {
            (14, new[] { shared }),
            (16, new[] { shared }),
            (5, new[] { shared, new Coord(4, 1) }),
        };

        Assert.Equal(21, Exposure.SeatedSum(lines));
    }
}
