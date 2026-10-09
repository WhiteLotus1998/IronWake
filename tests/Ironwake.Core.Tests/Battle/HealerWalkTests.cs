using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The Sim's walk for a healer with no weapon (issue 1395, Table rounds 524 and 525): on a Rout map with no heal to give
/// it walks toward the nearest ally below half, else behind the ally nearest the enemy, only onto a tile that closes
/// and that no enemy's strike reaches (DECISIONS/0377); an unarmed unit with no heal still waits. Since issue 1441 (DECISIONS/0379)
/// she steps back to such a tile when she stands exposed and none closes, and heals from the least exposed tile, never a lethal one.
/// </summary>
public class HealerWalkTests
{
    private static readonly Unit Mira = Recruit("mira", "chaplain", new Stats(16, 1, 4, 4, 4, 3, 1, 5, 3), "salve") with { Skill = WeaponSkill.Zero.With(WeaponType.Faith, WeaponRanks.Threshold(WeaponRank.D)) };

    /// <summary>A 16x5 field: the captain at 0,0, the healer at <paramref name="healer"/>, Wren at <paramref name="front"/>, Ivo at 0,4, three soldiers at column <paramref name="column"/>.</summary>
    private static string Field(string healer, string front, int column) => $"""
        name: Field
        size: 16x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ................
        ................
        ................
        ................
        ................

        units:
        P captain 0,0
        P recruit {healer}
        P recruit {front}
        P recruit 0,4
        E soldier {column},1 group:pack behavior:aggressive
        E soldier {column},2 group:pack behavior:aggressive
        E soldier {column},3 group:pack behavior:aggressive

        """;

    private static BattleState Board(string healer, string front, int column, Unit? mira = null) =>
        Start(roster: ValueList<Unit>.Of(Hale, mira ?? Mira, Wren, Ivo), map: Field(healer, front, column));

    private static Coord EndOf(IReadOnlyList<Command> plan, BattleUnit unit) => plan.OfType<Move>().Select(m => m.To).DefaultIfEmpty(unit.At).Single();

    [Fact]
    public void AnUnarmedHealerWithNoOneHurtWalksBehindTheAllyNearestTheEnemyOnATileNoEnemyReaches()
    {
        var state = Board("0,2", "6,2", 15);
        var mira = state.Find("mira")!;

        var end = EndOf(HeuristicPlayer.PlanUnit(state, Starter, mira, out _), mira);

        Assert.Equal(new Coord(4, 2), end);
        Assert.Equal(0, Exposure.Of(state, Starter, mira, end).NoCrit);
    }

    [Fact]
    public void AnUnarmedHealerWalksTowardTheNearestAllyBelowHalfBeforeTheFront()
    {
        var state = Board("6,2", "9,2", 15);
        var ivo = state.Find("ivo")!;
        state = state.WithUnit(ivo with { Hp = 4 });
        var mira = state.Find("mira")!;

        var end = EndOf(HeuristicPlayer.PlanUnit(state, Starter, mira, out _), mira);

        Assert.True(end.DistanceTo(ivo.At) < mira.At.DistanceTo(ivo.At));
    }

    [Fact]
    public void AnUnarmedHealerWaitsWhenEveryTileThatClosesIsOneAnEnemyReaches()
    {
        var state = Board("1,2", "5,2", 6);
        var mira = state.Find("mira")!;
        var wren = state.Find("wren")!;
        var closer = state.ReachOf(mira, Starter).Destinations.Where(t => t.DistanceTo(wren.At) < mira.At.DistanceTo(wren.At)).ToList();
        Assert.NotEmpty(closer);
        Assert.All(closer, t => Assert.True(Exposure.Of(state, Starter, mira, t).NoCrit > 0));

        Assert.Empty(HeuristicPlayer.PlanUnit(state, Starter, mira, out _).OfType<Move>());
    }

    [Fact]
    public void AnUnarmedUnitWithNoHealStillWaits()
    {
        var state = Board("0,2", "6,2", 15, Unarmed with { Id = "mira" });
        var mira = state.Find("mira")!;

        Assert.Empty(HeuristicPlayer.PlanUnit(state, Starter, mira, out _).OfType<Move>());
    }

    [Fact]
    public void AnUnarmedHealerStandingExposedStepsBackToATileNoEnemyReachesWhenNoSafeTileCloses()
    {
        var state = Board("4,2", "5,2", 7);
        var mira = state.Find("mira")!;
        Assert.True(Exposure.Of(state, Starter, mira, mira.At).NoCrit > 0);

        var end = EndOf(HeuristicPlayer.PlanUnit(state, Starter, mira, out _), mira);

        Assert.NotEqual(mira.At, end);
        Assert.Equal(0, Exposure.Of(state, Starter, mira, end).NoCrit);
    }

    [Fact]
    public void AnUnarmedHealerHealsFromTheLeastExposedTileThatReachesThePatient()
    {
        var state = Board("2,2", "5,2", 10);
        var wren = state.Find("wren")!;
        state = state.WithUnit(wren with { Hp = 3 });
        var mira = state.Find("mira")!;

        var plan = HeuristicPlayer.PlanUnit(state, Starter, mira, out _);
        var end = EndOf(plan, mira);
        Assert.Contains(plan, c => c is UseItem { TargetId: "wren" });
        var reaching = state.ReachOf(mira, Starter).Destinations.Where(t => t.DistanceTo(wren.At) == 1).ToList();
        Assert.Equal(reaching.Min(t => Exposure.Of(state, Starter, mira, t).NoCrit), Exposure.Of(state, Starter, mira, end).NoCrit);
    }

    [Fact]
    public void AnUnarmedHealerNeverHealsFromATileWhoseNoCritSumKillsHer()
    {
        var state = Board("2,2", "5,2", 7);
        var wren = state.Find("wren")!;
        state = state.WithUnit(wren with { Hp = 3 }).WithUnit(state.Find("mira")! with { Hp = 1 });
        var mira = state.Find("mira")!;
        var reaching = state.ReachOf(mira, Starter).Destinations.Where(t => t.DistanceTo(wren.At) == 1).ToList();
        Assert.All(reaching, t => Assert.True(Exposure.Of(state, Starter, mira, t).NoCrit >= mira.Hp));

        Assert.DoesNotContain(HeuristicPlayer.PlanUnit(state, Starter, mira, out _), c => c is UseItem);
    }
}
