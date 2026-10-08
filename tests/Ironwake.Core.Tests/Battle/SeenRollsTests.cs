using Ironwake.Content.Protocol;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// What a Recall has shown (issue 1359, <see cref="SeenRolls"/>): every first strike a discarded line
/// rolled is kept for the battle, and speaks only while its outcome still follows at today's chance.
/// </summary>
public class SeenRollsTests
{
    private static readonly Coord BesideBrigand = new(2, 1);

    private static (BattleState Moved, BattleState Fought, CombatFought Combat) FightOnce()
    {
        var moved = Start().Do(new Move("hale", BesideBrigand));
        var result = moved.Try(new Attack("hale", "brigand-1"));
        Assert.True(result.Accepted, result.Rejection?.Message);
        return (moved, result.Next, Assert.IsType<CombatFought>(result.Events[0]));
    }

    [Fact]
    public void AnAttackRecordsEachSidesFirstStrikeInTheLine()
    {
        var (moved, fought, combat) = FightOnce();

        Assert.Empty(moved.Struck);
        var firsts = combat.Strikes.GroupBy(s => s.AttackerId).Select(g => g.First()).ToList();
        Assert.Equal(firsts.Select(s => (s.AttackerId, s.TargetId, s.Hit)), fought.Struck.Select(s => (s.StrikerId, s.TargetId, s.Hit)));
        Assert.All(fought.Struck, s => Assert.Equal((1, Side.Player), (s.Turn, s.Phase)));
        Assert.Empty(fought.Seen);
    }

    [Fact]
    public void ARecallKeepsTheDiscardedLinesFirstStrikesAsSeen()
    {
        var (_, fought, _) = FightOnce();

        var recalled = fought.Do(new Recall(1));

        Assert.Empty(recalled.Struck);
        Assert.Equal(fought.Struck, recalled.Seen);
    }

    [Fact]
    public void WhatARecallShowedSurvivesALaterRecallToBeforeIt()
    {
        var (_, fought, _) = FightOnce();
        var once = fought.Do(new Recall(1));

        var twice = once.Do(new Wait("wren")).Do(new Recall(0));

        Assert.Equal(fought.Struck, twice.Seen);
    }

    [Fact]
    public void AStrikeTheLineStillHoldsIsNotSeen()
    {
        var (_, fought, _) = FightOnce();
        var strike = fought.Struck[0];

        Assert.Null(SeenRolls.Outcome(fought, strike.Turn, strike.Phase, strike.StrikerId, strike.TargetId, strike.HitChance));
    }

    [Fact]
    public void ASeenStrikeRepeatsTheOutcomeTheReplayedAttackRolls()
    {
        var (_, fought, combat) = FightOnce();
        var recalled = fought.Do(new Recall(1));
        var first = recalled.Seen[0];

        var shown = SeenRolls.Outcome(recalled, first.Turn, first.Phase, first.StrikerId, first.TargetId, first.HitChance);
        var again = Assert.IsType<CombatFought>(recalled.Try(new Attack("hale", "brigand-1")).Events[0]);

        Assert.Equal(combat.Strikes[0].Hit, shown);
        Assert.Equal(again.Strikes[0].Hit, shown);
    }

    [Theory]
    [InlineData(true, 70, 70, true)]
    [InlineData(true, 70, 90, true)]
    [InlineData(true, 70, 69, null)]
    [InlineData(false, 70, 70, false)]
    [InlineData(false, 70, 40, false)]
    [InlineData(false, 70, 71, null)]
    public void ASeenStrikeSpeaksOnlyWhileItsOutcomeStillFollowsAtTodaysChance(bool hit, int seenAt, int chanceNow, bool? expected)
    {
        var state = Start() with { Seen = ValueList<SeenStrike>.Of(new SeenStrike(1, Side.Player, "hale", "brigand-1", seenAt, hit)) };

        Assert.Equal(expected, SeenRolls.Outcome(state, 1, Side.Player, "hale", "brigand-1", chanceNow));
    }

    [Fact]
    public void ASeenStrikeIsKeyedByTurnPhaseStrikerAndTarget()
    {
        var state = Start() with { Seen = ValueList<SeenStrike>.Of(new SeenStrike(1, Side.Player, "hale", "brigand-1", 70, true)) };

        Assert.Null(SeenRolls.Outcome(state, 2, Side.Player, "hale", "brigand-1", 70));
        Assert.Null(SeenRolls.Outcome(state, 1, Side.Enemy, "hale", "brigand-1", 70));
        Assert.Null(SeenRolls.Outcome(state, 1, Side.Player, "wren", "brigand-1", 70));
        Assert.Null(SeenRolls.Outcome(state, 1, Side.Player, "hale", "soldier-1", 70));
    }

    [Fact]
    public void TheSeenLineNamesTheStrikeThenTheCounterAndOnlyWhatItKnows()
    {
        var state = Start() with
        {
            Seen = ValueList<SeenStrike>.Of(
                new SeenStrike(1, Side.Player, "hale", "brigand-1", 76, false),
                new SeenStrike(1, Side.Player, "brigand-1", "hale", 24, true)),
        };

        Assert.Equal("Seen before the recall: Hale misses, Brigand hits", SeenRolls.Line(state, 1, Side.Player, "hale", "Hale", 76, "brigand-1", "Brigand", 24));
        Assert.Equal("Seen before the recall: Hale misses", SeenRolls.Line(state, 1, Side.Player, "hale", "Hale", 76, "brigand-1", "Brigand", null));
        Assert.Equal("Seen before the recall: Brigand hits", SeenRolls.Line(state, 1, Side.Player, "hale", "Hale", 90, "brigand-1", "Brigand", 30));
        Assert.Null(SeenRolls.Line(state, 1, Side.Player, "hale", "Hale", 90, "brigand-1", "Brigand", 10));
    }

    [Fact]
    public void TheProtocolCarriesTheLinesStrikesAndWhatARecallShowed()
    {
        var (_, fought, _) = FightOnce();
        var recalled = fought.Do(new Recall(1)).Do(new Attack("hale", "brigand-1"));

        var json = ProtocolJson.State(recalled, Starter);
        var read = ProtocolJson.ReadState(json, Starter);

        Assert.Contains("\"seen\":[{\"turn\":1,\"phase\":\"player\",\"striker\":\"hale\",\"target\":\"brigand-1\"", json);
        Assert.Equal(recalled.Struck, read.Struck);
        Assert.Equal(recalled.Seen, read.Seen);
        Assert.DoesNotContain("\"seen\"", ProtocolJson.State(fought, Starter));
    }
}
