using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Recall (DESIGN.md section 7): a rewind to any prior state that spends a charge, and
/// the issue 31 rule that a Recall restores the rolls, never rerolls them.
/// </summary>
public class RecallTests
{
    private static readonly Coord BesideBrigand = new(2, 1);
    private static readonly Coord BesideSoldier = new(2, 2);

    [Fact]
    public void RecallRestoresTheHistoricalStateAndSpendsACharge()
    {
        var start = Start();
        var moved = start.Do(new Move("hale", BesideBrigand));
        var fought = moved.Do(new Attack("hale", "brigand-1"));
        Assert.Equal(2, fought.History.Count);

        var result = fought.Try(new Recall(1));

        Assert.True(result.Accepted);
        Assert.Equal(new Recalled(1, 2), Assert.Single(result.Events));
        Assert.Equal(2, result.Next.RecallCharges);
        Assert.Equal(moved with { RecallCharges = 2 }, result.Next);
        Assert.Single(result.Next.History);
        Assert.Equal(start with { History = ValueList<BattleState>.Empty }, result.Next.History[0]);
    }

    [Fact]
    public void RecallToTheOpeningStateBringsADeadUnitBack()
    {
        var wounded = Start();
        wounded = wounded.WithUnit(wounded.Find("brigand-1")! with { Hp = 1 });
        var dead = wounded.Do(new Move("hale", BesideBrigand)).Do(new Attack("hale", "brigand-1"));
        Assert.Null(dead.Find("brigand-1"));

        var restored = dead.Do(new Recall(0));

        Assert.Equal(1, restored.Find("brigand-1")!.Hp);
        Assert.Empty(restored.History);
        Assert.Equal(2, restored.RecallCharges);
    }

    [Fact]
    public void RecallIsRefusedWithoutChargesOrOutsideTheHistory()
    {
        var state = Start().Do(new Wait("hale"));

        Assert.Equal(RejectionReason.NoSuchHistoryIndex, state.Refused(new Recall(1)).Reason);
        Assert.Equal(RejectionReason.NoSuchHistoryIndex, state.Refused(new Recall(-1)).Reason);
        Assert.Equal("history holds 1 states; there is no state 1 to recall", state.Refused(new Recall(1)).Message);

        var spent = state with { RecallCharges = 0 };
        var rejection = spent.Refused(new Recall(0));
        Assert.Equal(RejectionReason.NoRecallCharges, rejection.Reason);
    }

    [Fact]
    public void TheSpentChargesSurviveARecallToBeforeTheyWereSpent()
    {
        var state = Start().Do(new Wait("hale")).Do(new Wait("wren"));
        var once = state.Do(new Recall(1));
        var twice = once.Do(new Recall(0));

        Assert.Equal(1, twice.RecallCharges);
        Assert.Equal(0, twice.Do(new Wait("hale")).Do(new Recall(0)).RecallCharges);
    }

    private static ValueList<StrikeEvent> StrikesOf(BattleState state, Command attack)
    {
        var result = state.Try(attack);
        Assert.True(result.Accepted, result.Rejection?.Message);
        return Assert.IsType<CombatFought>(result.Events[0]).Strikes;
    }

    [Fact]
    public void ARecallFollowedByTheIdenticalCommandProducesTheIdenticalStrikes()
    {
        var moved = Start().Do(new Move("hale", BesideBrigand));
        var original = StrikesOf(moved, new Attack("hale", "brigand-1"));
        var fought = moved.Do(new Attack("hale", "brigand-1"));

        var recalled = fought.Do(new Recall(1));

        Assert.Equal(original, StrikesOf(recalled, new Attack("hale", "brigand-1")));
    }

    [Fact]
    public void ARecallThenADifferentCommandThenTheOriginalStillDrawsTheOriginalRolls()
    {
        var moved = Start().Do(new Move("hale", BesideBrigand));
        var original = StrikesOf(moved, new Attack("hale", "brigand-1"));
        var recalled = moved.Do(new Attack("hale", "brigand-1")).Do(new Recall(1));

        var different = recalled.Do(new Move("wren", BesideSoldier)).Do(new Attack("wren", "soldier-1"));

        Assert.Equal(original, StrikesOf(different, new Attack("hale", "brigand-1")));
    }

    [Fact]
    public void TheSameAttackAfterReorderingUnrelatedCommandsGetsIdenticalRolls()
    {
        var haleFirst = Start().Do(new Move("hale", BesideBrigand)).Do(new Move("wren", BesideSoldier));
        var wrenFirst = Start().Do(new Move("wren", BesideSoldier)).Do(new Move("hale", BesideBrigand));
        var wrenFought = Start().Do(new Move("wren", BesideSoldier)).Do(new Attack("wren", "soldier-1")).Do(new Move("hale", BesideBrigand));

        var expected = StrikesOf(haleFirst, new Attack("hale", "brigand-1"));

        Assert.Equal(expected, StrikesOf(wrenFirst, new Attack("hale", "brigand-1")));
        Assert.Equal(expected, StrikesOf(wrenFought, new Attack("hale", "brigand-1")));
    }

    [Fact]
    public void TheSameAttackOnALaterTurnDrawsDifferentRolls()
    {
        var turn1 = Start().Do(new Move("hale", BesideBrigand));
        var turn2 = turn1.Do(new EndPhase()).Do(new EndPhase());
        var keys = new[] { RollKey.Combat(1, Side.Player, "hale", "brigand-1", 0, CombatRoll.HitA), RollKey.Combat(2, Side.Player, "hale", "brigand-1", 0, CombatRoll.HitA) };

        Assert.NotEqual(new KeyedRng(7).Roll(keys[0]), new KeyedRng(7).Roll(keys[1]));
        Assert.NotEqual(StrikesOf(turn1, new Attack("hale", "brigand-1")), StrikesOf(turn2, new Attack("hale", "brigand-1")));
    }
}
