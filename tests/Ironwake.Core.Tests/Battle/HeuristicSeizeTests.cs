using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The seize step (issue 1409): a captain that can end on the throne this turn plans its action from the throne,
/// ahead of a heal or a strike from elsewhere; on a held Seize map only where the throne's no-crit exposure stays
/// under its HP, and on the last turn regardless. Before it, the campaign writer's Maud salved Wren on the Shrine's
/// last turn with no enemy left and the altar two steps away, and lost the side map that pays the Psalter.
/// </summary>
public class HeuristicSeizeTests
{
    private static readonly Coord Throne = new(12, 1);

    private static readonly Unit Dressed = Recruit("hale", new Stats(22, 8, 0, 7, 8, 6, 5, 2, 9), "iron_sword", "field_dressing");

    private static string Hall(string? header, int turnLimit, string enemy) => $"""
        name: Hall
        size: 16x3
        win: seize{(header is null ? "" : "\n" + header)}
        turn_limit: {turnLimit}
        recall: 3
        enemy_level: 1

        ................
        ............T...
        ................

        units:
        P captain 9,1
        E {enemy}

        """;

    private const string Far = "brigand 0,0 group:far behavior:hold";

    private const string Near = "brigand 15,1 group:near behavior:aggressive";

    private static (BattleState State, BattleUnit Hale) Wounded(string map, int hp = 5)
    {
        var start = Start(roster: ValueList<Unit>.Of(Dressed), map: map);
        var state = start.WithUnit(start.Find("hale")! with { Hp = hp });
        return (state, state.Find("hale")!);
    }

    private static Coord EndOf(IReadOnlyList<Command> plan, BattleUnit unit) => plan.OfType<Move>().Select(m => m.To).DefaultIfEmpty(unit.At).Single();

    [Fact]
    public void AWoundedCaptainThatCanReachThePlainThroneStepsOntoItRatherThanHealingElsewhere()
    {
        var (state, hale) = Wounded(Hall(null, 10, Far));

        var plan = HeuristicPlayer.PlanUnit(state, Starter, hale, out _);

        Assert.Equal(Throne, EndOf(plan, hale));
        Assert.Equal(BattleResult.Won, state.Do(plan.OfType<Move>().Single()).Outcome.Result);
    }

    [Fact]
    public void OnAHeldThroneTheCaptainHealsFromTheThroneWhenItIsSafe()
    {
        var (state, hale) = Wounded(Hall("seize_hold: 1", 10, Far));

        var plan = HeuristicPlayer.PlanUnit(state, Starter, hale, out _);

        Assert.Equal(Throne, EndOf(plan, hale));
        Assert.Contains(plan, c => c is UseItem);
    }

    [Fact]
    public void BeforeTheLastTurnTheCaptainRefusesAHeldThroneWhoseExposureReachesItsHp()
    {
        var (state, hale) = Wounded(Hall("seize_hold: 1", 10, Near));
        Assert.True(Exposure.Of(state, Starter, hale, Throne).NoCrit >= hale.Hp);

        Assert.Null(HeuristicPlayer.SeizeTile(state, Starter, hale, new[] { Throne }));
        Assert.NotEqual(Throne, EndOf(HeuristicPlayer.PlanUnit(state, Starter, hale, out _), hale));
    }

    [Fact]
    public void OnTheLastTurnTheCaptainStepsOntoAHeldThroneWhateverItsExposure()
    {
        var (state, hale) = Wounded(Hall("seize_hold: 1", 1, Near));
        Assert.True(Exposure.Of(state, Starter, hale, Throne).NoCrit >= hale.Hp);

        Assert.Equal(Throne, EndOf(HeuristicPlayer.PlanUnit(state, Starter, hale, out _), hale));
    }

    [Fact]
    public void TheSeizeStepIsTheCaptainsAloneAndOnlyBeforeItMoves()
    {
        var (state, hale) = Wounded(Hall(null, 10, Far));

        Assert.Equal(Throne, HeuristicPlayer.SeizeTile(state, Starter, hale, new[] { Throne }));
        Assert.Null(HeuristicPlayer.SeizeTile(state, Starter, hale with { Moved = true }, new[] { Throne }));
        Assert.Null(HeuristicPlayer.SeizeTile(state, Starter, hale with { IsCaptain = false }, new[] { Throne }));
    }

    [Fact]
    public void OffASeizeMapThereIsNoSeizeStep()
    {
        var (state, hale) = Wounded(Hall(null, 10, Far).Replace("win: seize", "win: rout"));

        Assert.Null(HeuristicPlayer.SeizeTile(state, Starter, hale, new[] { Throne }));
    }
}
