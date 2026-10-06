using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The chip (issue 1178, round 395): on the chipping line, when the fed unit cannot kill a planned target on a
/// hit, another non-captain strikes it first so that the hit leaves it in his one-hit range, under the paying
/// guard; with no such strike the striking line's own strike follows.
/// </summary>
public class ChipTests
{
    /// <summary>A 6x4 yard: Hale at 0,1, Teodor at 0,2, Ash at 0,3, a brigand at 3,1 all reach on turn 1.</summary>
    private const string Yard = """
        name: Yard
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
        P recruit:teodor 0,2
        P recruit:ash 0,3
        E brigand 3,1 group:yard behavior:aggressive

        """;

    /// <summary>Teodor and Ash with Hale's stats, so all three strike alike.</summary>
    private static readonly Unit Teodor = Recruit(FocusedPlayer.Fed, Hale.Stats, "iron_sword");

    private static readonly Unit Ash = Recruit("ash", Hale.Stats, "iron_sword");

    private static BattleUnit Brigand(BattleState state) => state.UnitsOf(Side.Enemy).Single(e => e.At == new Coord(3, 1));

    /// <summary>One plain hit of Teodor's sword on the brigand from 2,1, which every unit on the yard shares.</summary>
    private static int OneHit(BattleState state) =>
        FocusedPlayer.Hit(state, Starter, state.Find(FocusedPlayer.Fed)!, new Coord(2, 1), Brigand(state))!.Value;

    /// <summary>The yard with the brigand at <paramref name="hits"/> of those hits plus <paramref name="extra"/> HP, its maximum raised to fit.</summary>
    private static BattleState Board(int hits, int extra, Unit? planner = null)
    {
        var state = Start(roster: ValueList<Unit>.Of(planner ?? Hale, Teodor, Ash), map: Yard);
        var hp = hits * OneHit(state) + extra;
        var brigand = Brigand(state);
        var stats = brigand.Unit.Stats;
        return state.WithUnit(brigand with { Hp = hp, Unit = brigand.Unit with { Stats = stats with { Hp = Math.Max(stats.Hp, hp) } } });
    }

    private static IReadOnlyList<Command> Plan(BattleState state, string unit) => HeuristicPlayer.PlanUnit(state, Starter, state.Find(unit)!);

    private static string? Striker(IReadOnlyList<Command>? plan) => plan is [.., Attack attack] ? attack.UnitId : null;

    [Fact]
    public void AChipLeavesAPlannedTargetTheFedUnitCannotKillInHisRange()
    {
        // One hit and one HP: no one hit kills it, and one chip leaves it a hit from death.
        var state = Board(1, 1);
        var plan = Plan(state, "hale");
        Assert.Equal("hale", Striker(plan));

        var chip = FocusedPlayer.Chip(state, Starter, plan);

        Assert.Equal("hale", Striker(chip));
        Assert.Equal(Brigand(state).Id, ((Attack)chip![^1]).TargetId);
        Assert.True(Resolver.Apply(state, Starter, chip[0]).Accepted);
    }

    [Fact]
    public void NoChipWhenTheFedUnitCanAlreadyKill()
    {
        var state = Board(1, 0);

        Assert.Null(FocusedPlayer.Chip(state, Starter, Plan(state, "hale")));
    }

    [Fact]
    public void NoChipWhenOneHitLeavesTheTargetOutOfHisRange()
    {
        var state = Board(2, 1);

        Assert.Null(FocusedPlayer.Chip(state, Starter, Plan(state, "hale")));
    }

    [Fact]
    public void TheCaptainNeverChipsAndTheNextNonCaptainDoes()
    {
        var captain = Recruit("captain", Hale.Stats, "iron_sword");
        var state = Board(1, 1, planner: captain);
        Assert.True(CampaignRecord.IsCaptain(state.Find("captain")!.Unit, Starter));
        var plan = Plan(state, "captain");
        Assert.Equal("captain", Striker(plan));

        Assert.Equal("ash", Striker(FocusedPlayer.Chip(state, Starter, plan)));
    }

    [Fact]
    public void AChipOntoAForecastDeathIsRefused()
    {
        var captain = Recruit("captain", Hale.Stats, "iron_sword");
        var state = Board(1, 1, planner: captain);
        state = state.WithUnit(state.Find("ash")! with { Hp = 1 });

        Assert.Null(FocusedPlayer.Chip(state, Starter, Plan(state, "captain")));
    }

    [Fact]
    public void AChipTakesTheTilesExposureAndRefusesOneLess()
    {
        var state = Board(1, 1);
        var reach = state.UnitsOf(Side.Enemy).Select(e => state.ReachOf(e, Starter)).ToList();
        var ash = state.Find("ash")!;
        var teodor = state.Find(FocusedPlayer.Fed)!;

        // Every tile beside the brigand is in its own reach: one enemy.
        Assert.NotNull(FocusedPlayer.ChipFrom(state, Starter, ash, teodor, Brigand(state), 1, reach));
        Assert.Null(FocusedPlayer.ChipFrom(state, Starter, ash, teodor, Brigand(state), 0, reach));
    }

    [Fact]
    public void TheChippingPlayerCountsTheChipAndTheStrikingPlayerNever()
    {
        var state = Board(1, 1);
        var chipping = new FocusedPlayer(FocusedPlayer.Guard.Chipping);
        var striking = new FocusedPlayer(FocusedPlayer.Guard.Striking);

        var chipped = chipping.Next(state, Starter);
        striking.Next(state, Starter);

        Assert.NotEqual(FocusedPlayer.Fed, Striker(chipped));
        Assert.Equal(1, chipping.Chips);
        Assert.Equal(0, chipping.Strikes);
        Assert.Equal(0, striking.Chips);
    }

    [Theory]
    [InlineData(7, 100, 100, 100, 100, "the bar passes on the chip (teodor p50 L7)")]
    [InlineData(6, 100, 50, 100, 51, "kill-chance refusals collapse and no-tile refusals do not (kill chance 100 to 50, no tile 100 to 51): positioning")]
    [InlineData(6, 100, 51, 100, 100, "the bar fails (teodor p50 L6, needs L7; kill chance 100 to 51, no tile 100 to 100); the cold hand play")]
    [InlineData(6, 100, 10, 100, 50, "the bar fails (teodor p50 L6, needs L7; kill chance 100 to 10, no tile 100 to 50); the cold hand play")]
    public void TheChipVerdictReadsAsRound395Agreed(int level, int chanceBefore, int chanceAfter, int noTileBefore, int noTileAfter, string expected)
    {
        Assert.Contains(expected, LevelRun.ChipVerdict(level, chanceBefore, chanceAfter, noTileBefore, noTileAfter), StringComparison.Ordinal);
    }
}
