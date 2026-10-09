using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The Sim's heal threshold (issue 1429): the heuristic heals a unit at half its max HP or below, not only below half.
/// Under the old rule the Mill's captain parked at exactly 11/22 with every approach lethal at 11, his Field Dressing and
/// Maud's Salve unused to the turn limit (DECISIONS/0367).
/// </summary>
public class HealAtHalfTests
{
    private static readonly Unit Mira = Recruit("mira", "chaplain", new Stats(16, 1, 4, 4, 4, 3, 1, 5, 3), "salve") with { Skill = WeaponSkill.Zero.With(WeaponType.Faith, WeaponRanks.Threshold(WeaponRank.D)) };

    private static readonly Unit Dressed = Recruit("hale", new Stats(22, 8, 0, 7, 8, 6, 5, 2, 9), "iron_sword", "field_dressing");

    /// <summary>A 16x5 field with the company on the west edge and three soldiers out of reach on the east.</summary>
    private const string Field = """
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
        P recruit 0,2
        P recruit 1,2
        E soldier 15,1 group:pack behavior:aggressive
        E soldier 15,2 group:pack behavior:aggressive
        E soldier 15,3 group:pack behavior:aggressive

        """;

    private static BattleState Board(int captainHp, int wrenHp)
    {
        var state = Start(roster: ValueList<Unit>.Of(Dressed, Mira, Wren), map: Field);
        return state.WithUnit(state.Find("hale")! with { Hp = captainHp }).WithUnit(state.Find("wren")! with { Hp = wrenHp });
    }

    [Fact]
    public void TheCaptainAtExactlyHalfHpWithNoStrikeUsesHisOwnConsumable()
    {
        var state = Board(11, 20);
        var hale = state.Find("hale")!;

        var plan = HeuristicPlayer.PlanUnit(state, Starter, hale, out _);

        Assert.Contains(plan, c => c is UseItem { UnitId: "hale", TargetId: null });
    }

    [Fact]
    public void TheCaptainOneAboveHalfHpDoesNotHeal()
    {
        var state = Board(12, 20);
        var hale = state.Find("hale")!;

        Assert.DoesNotContain(HeuristicPlayer.PlanUnit(state, Starter, hale, out _), c => c is UseItem);
    }

    [Fact]
    public void AHealerMendsAnAllyAtExactlyHalfHp()
    {
        var state = Board(22, 10);
        var mira = state.Find("mira")!;

        Assert.Contains(HeuristicPlayer.PlanUnit(state, Starter, mira, out _), c => c is UseItem { TargetId: "wren" });
    }

    [Fact]
    public void AHealerLeavesAnAllyOneAboveHalfHpAlone()
    {
        var state = Board(22, 11);
        var mira = state.Find("mira")!;

        Assert.DoesNotContain(HeuristicPlayer.PlanUnit(state, Starter, mira, out _), c => c is UseItem);
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(11, true)]
    [InlineData(12, false)]
    [InlineData(22, false)]
    public void WoundedIsHalfMaxHpOrBelow(int hp, bool wounded)
    {
        var state = Board(hp, 20);

        Assert.Equal(wounded, HeuristicPlayer.Wounded(Starter, state.Find("hale")!));
    }
}
