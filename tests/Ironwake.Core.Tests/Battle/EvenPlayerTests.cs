using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The even-company chair (issue 1150, round 387): a kill the heuristic plans goes to the lowest
/// level, then the fewest main-weapon rank points, among the units that take the same kill from a tile
/// the heuristic would accept, at no lower kill chance and no more exposure; otherwise the plan stands.
/// </summary>
public class EvenPlayerTests
{
    /// <summary>A 6x4 yard: Hale at 0,1, Wren at 0,2, a brigand at 3,1 both reach on turn 1.</summary>
    private const string Lone = """
        name: Lone
        size: 6x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ......
        ......
        ......
        ......

        units:
        P captain 0,1
        P recruit:wren 0,2
        E brigand 3,1 group:yard behavior:aggressive

        """;

    private static readonly WeaponType Sword = Starter.Weapons["iron_sword"].Type;

    /// <summary>Wren with Hale's stats, so the two strike alike and only level and rank part them.</summary>
    private static readonly Unit Twin = Recruit("wren", Hale.Stats, "iron_sword");

    private static BattleState Board(Unit hale, Unit wren, int? brigandHp = 1, string map = Lone)
    {
        var state = Start(roster: ValueList<Unit>.Of(hale, wren), map: map);
        var brigand = state.Find("brigand-1") ?? state.UnitsOf(Side.Enemy).First();
        return brigandHp is { } hp ? state.WithUnit(brigand with { Hp = hp }) : state;
    }

    private static IReadOnlyList<Command> HalesPlan(BattleState state) => HeuristicPlayer.PlanUnit(state, Starter, state.Find("hale")!);

    private static string? Striker(IReadOnlyList<Command> plan) => plan[^1] is Attack attack ? attack.UnitId : null;

    [Fact]
    public void AKillGoesToTheLowestLevelThatCanTakeIt()
    {
        var state = Board(Hale with { Level = 5 }, Twin);
        var plan = HalesPlan(state);
        Assert.Equal("hale", Striker(plan));

        var handed = EvenPlayer.Hand(state, Starter, plan);

        Assert.Equal("wren", Striker(handed));
        Assert.True(Resolver.Apply(state, Starter, handed[0]).Accepted);
    }

    [Fact]
    public void ALevelTieGoesToTheFewestRankPointsInTheMainWeapon()
    {
        var trained = Hale with { Skill = Hale.Skill.With(Sword, 50) };
        var state = Board(trained, Twin);

        Assert.Equal("wren", Striker(EvenPlayer.Hand(state, Starter, HalesPlan(state))));
    }

    [Fact]
    public void AFullTieKeepsThePlannedAttacker()
    {
        var state = Board(Hale, Twin);
        var plan = HalesPlan(state);

        Assert.Same(plan, EvenPlayer.Hand(state, Starter, plan));
    }

    [Fact]
    public void AHigherUnitNeverTakesTheKill()
    {
        var state = Board(Hale, Twin with { Level = 5 });
        var plan = HalesPlan(state);

        Assert.Same(plan, EvenPlayer.Hand(state, Starter, plan));
    }

    [Fact]
    public void AnAttackThatDoesNotKillOnAHitIsNeverHanded()
    {
        var state = Board(Hale with { Level = 5 }, Twin, brigandHp: null);
        var plan = HalesPlan(state);
        Assert.Equal("hale", Striker(plan));

        Assert.Same(plan, EvenPlayer.Hand(state, Starter, plan));
    }

    [Fact]
    public void ALowerKillChanceKeepsThePlannedAttacker()
    {
        var clumsy = Twin with { Stats = Twin.Stats with { Dex = 0, Lck = 0 } };
        var state = Board(Hale with { Level = 5 }, clumsy);
        var plan = HalesPlan(state);

        Assert.Same(plan, EvenPlayer.Hand(state, Starter, plan));
    }

    [Fact]
    public void ATileInReachOfMoreEnemiesThanThePlannedOneIsRefused()
    {
        var state = Board(Hale with { Level = 5 }, Twin);
        var wren = state.Find("wren")!;
        var brigand = state.UnitsOf(Side.Enemy).Single();
        var reach = state.UnitsOf(Side.Enemy).Select(e => state.ReachOf(e, Starter)).ToList();

        Assert.NotNull(EvenPlayer.KillFrom(state, Starter, wren, brigand, 0, int.MaxValue, reach));
        Assert.Null(EvenPlayer.KillFrom(state, Starter, wren, brigand, 0, -1, reach));
    }

    [Fact]
    public void AUnitWhoseDeathLosesTheMapKeepsTheVetoOnTheKillTile()
    {
        var map = Lone.Replace("E brigand 3,1 group:yard behavior:aggressive", "E brigand 3,1 group:yard behavior:aggressive\nE soldier 4,2 group:yard behavior:aggressive");
        var state = Board(Hale, Twin, map: map);
        var brigand = state.UnitsOf(Side.Enemy).Single(e => e.At == new Coord(3, 1));
        state = state.WithUnit(state.Find("hale")! with { Hp = 1 }).WithUnit(state.Find("wren")! with { Hp = 1 });
        var reach = state.UnitsOf(Side.Enemy).Select(e => state.ReachOf(e, Starter)).ToList();

        Assert.Null(EvenPlayer.KillFrom(state, Starter, state.Find("hale")!, brigand, 0, int.MaxValue, reach));
        Assert.NotNull(EvenPlayer.KillFrom(state, Starter, state.Find("wren")!, brigand, 0, int.MaxValue, reach));
    }
}
