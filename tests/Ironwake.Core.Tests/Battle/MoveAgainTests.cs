using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Move Again (issue 71, DESIGN.md section 7): an ability the cavalry classes carry in content.
/// After an Attack, Item or Wait the unit moves again on its Mov minus the path cost its
/// Move spent, through <see cref="Movement.Reach"/> from where it stands, and is then done.
/// Its own tile is a legal destination, so a Move Again declined is a command. The wake check
/// runs after the Move Again on where the unit ends.
/// </summary>
public class MoveAgainTests
{
    /// <summary>A 12x4 field: a rider at 1,1 beside Hale, a brigand on the road at 4,2, and a Guard group (soldier 9,0, archer 11,3) past it.</summary>
    private const string Field = """
        name: Field
        size: 12x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ............
        ............
        ............
        ............

        units:
        P captain 0,1
        P recruit:rider 1,1
        E brigand 4,2 group:road behavior:aggressive
        E soldier 9,0 group:watch behavior:guard
        E archer 11,3 group:watch behavior:guard

        """;

    private static readonly Unit Rider = Recruit("rider", "outrider", new Stats(40, 9, 0, 9, 9, 5, 9, 2, 3), "iron_lance", "field_dressing");

    private static BattleState Start(GameContent? content = null) =>
        BattleState.From(MapFixture.Parse(Field, "field.map"), content ?? Starter, ValueList<Unit>.Of(Hale, Rider), 7);

    /// <summary>The starter content with the outrider at Mov 8, the issue's worked example.</summary>
    private static GameContent MovEight()
    {
        var outrider = Starter.Class("outrider");
        return Starter with { Classes = Starter.Classes.SetItem("outrider", outrider with { Mov = 8 }) };
    }

    private static BattleState Do(BattleState state, GameContent content, Command command)
    {
        var result = Resolver.Apply(state, content, command);
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result.Next;
    }

    [Fact]
    public void TheOutriderClassCarriesMoveAgainInContentAndTheCadetDoesNot()
    {
        Assert.Equal(ValueList<string>.Of("move_again"), Starter.Class("outrider").Abilities);
        Assert.IsType<MoveAgainEffect>(Starter.Ability("move_again").Effect);
        Assert.Equal(AbilityTrigger.AfterAction, Starter.Ability("move_again").Trigger);
        Assert.True(AbilityRules.HasMoveAgain(Starter.AbilitiesOf(Rider)));
        Assert.True(AbilityRules.HasMoveAgain(Starter.AbilitiesOf(Starter.Cast.Single(u => u.Id == "ansgar"))));
        Assert.False(AbilityRules.HasMoveAgain(Starter.AbilitiesOf(Hale)));
    }

    [Fact]
    public void MoveAgainIsHeldByTheClassSoAUnitThatLeavesTheClassLosesIt()
    {
        Assert.False(AbilityRules.HasMoveAgain(Starter.AbilitiesOf(Rider with { ClassId = "cadet" })));
    }

    [Fact]
    public void TheMoveAgainBudgetIsMovMinusThePathCostSpent()
    {
        var content = MovEight();
        var state = Start(content);
        state = Do(state, content, new Move("rider", new Coord(3, 2)));
        state = Do(state, content, new Attack("rider", "brigand-1"));

        var rider = state.Find("rider")!;
        Assert.Equal(5, rider.MoveAgain);
        var reach = state.MoveAgainReachOf(rider, content)!;
        var expected = Movement.Reach(state.Map, content, rider.At, MovementType.Cavalry, 5, at => state.OccupantAt(at, Side.Player));
        Assert.Equal(expected, reach);
        Assert.Equal(5, reach.Mov);
        Assert.Equal(expected, Queries.Reachable(state, content, rider));
    }

    [Fact]
    public void AUnitThatActsWithoutMovingIsOwedItsFullMov()
    {
        var state = Start().Do(new Wait("rider"));

        Assert.Equal(6, state.Find("rider")!.MoveAgain);
    }

    [Fact]
    public void MoveAgainFollowsAnItem()
    {
        var state = Start();
        state = state.WithUnit(state.Find("rider")! with { Hp = 20 }).Do(new UseItem("rider", 1));

        Assert.Equal(6, state.Find("rider")!.MoveAgain);
        Assert.NotNull(state.MoveAgainReachOf(state.Find("rider")!, Starter));
    }

    [Fact]
    public void AUnitThatSpentItsWholeMovGetsAMoveAgainOfZeroTilesAndItsOwnTileIsLegal()
    {
        var state = Start().Do(new Move("rider", new Coord(5, 3))).Do(new Wait("rider"));
        var rider = state.Find("rider")!;

        Assert.Equal(0, rider.MoveAgain);
        Assert.Equal(new[] { new Coord(5, 3) }, state.MoveAgainReachOf(rider, Starter)!.Destinations);
        Assert.Equal(RejectionReason.OutOfReach, state.Refused(new MoveAgain("rider", new Coord(6, 3))).Reason);
        var result = state.Try(new MoveAgain("rider", new Coord(5, 3)));
        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(new MovedAgain("rider", new Coord(5, 3), new Coord(5, 3), ValueList<Coord>.Empty), result.Events);
    }

    [Fact]
    public void AMoveAgainMovesTheUnitAlongTheReachPathAndTheUnitIsThenDone()
    {
        var state = Start().Do(new Move("rider", new Coord(3, 2))).Do(new Attack("rider", "brigand-1"));
        var result = state.Try(new MoveAgain("rider", new Coord(1, 3)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var movedAgain = Assert.Single(result.Events.OfType<MovedAgain>());
        Assert.Equal(new Coord(3, 2), movedAgain.From);
        Assert.Equal(new Coord(1, 3), movedAgain.To);
        Assert.Equal(state.MoveAgainReachOf(state.Find("rider")!, Starter)!.PathTo(new Coord(1, 3)), movedAgain.Path);
        var after = result.Next;
        Assert.Equal(new Coord(1, 3), after.Find("rider")!.At);
        Assert.Null(after.Find("rider")!.MoveAgain);
        Assert.Equal(RejectionReason.NoMoveAgain, after.Refused(new MoveAgain("rider", new Coord(1, 2))).Reason);
        Assert.Equal(RejectionReason.AlreadyActed, after.Refused(new Move("rider", new Coord(1, 2))).Reason);
        Assert.Equal(RejectionReason.AlreadyActed, after.Refused(new Wait("rider")).Reason);
    }

    [Fact]
    public void AMoveAgainBeyondWhatTheMoveLeftIsRefused()
    {
        var state = Start().Do(new Move("rider", new Coord(3, 2))).Do(new Attack("rider", "brigand-1"));

        var rejection = state.Refused(new MoveAgain("rider", new Coord(0, 0)));

        Assert.Equal(RejectionReason.OutOfReach, rejection.Reason);
        Assert.Contains("not within the 3 movement rider's Move Again has left", rejection.Message);
    }

    [Fact]
    public void AMoveAgainOntoAnAllyIsRefused()
    {
        var state = Start().Do(new Wait("rider"));

        Assert.Equal(RejectionReason.OutOfReach, state.Refused(new MoveAgain("rider", new Coord(0, 1))).Reason);
    }

    [Fact]
    public void AMoveAgainBeforeTheUnitActsIsRefused()
    {
        var state = Start();

        var rejection = state.Refused(new MoveAgain("rider", new Coord(1, 2)));
        Assert.Equal(RejectionReason.NoMoveAgain, rejection.Reason);
        Assert.Contains("has not acted", rejection.Message);
        rejection = state.Do(new Move("rider", new Coord(1, 2))).Refused(new MoveAgain("rider", new Coord(2, 2)));
        Assert.Equal(RejectionReason.NoMoveAgain, rejection.Reason);
    }

    [Fact]
    public void AUnitWithoutMoveAgainIsRefusedOne()
    {
        var state = Start().Do(new Wait("hale"));

        var rejection = state.Refused(new MoveAgain("hale", new Coord(1, 1)));
        Assert.Equal(RejectionReason.NoMoveAgain, rejection.Reason);
        Assert.Contains("has no Move Again", rejection.Message);
        Assert.Null(state.Find("hale")!.MoveAgain);
    }

    [Fact]
    public void AMoveAgainNotTakenIsLostWhenThePhaseEnds()
    {
        var state = Start().Do(new Wait("rider"));
        Assert.Equal(6, state.Find("rider")!.MoveAgain);

        state = state.Do(new EndPhase());

        Assert.Null(state.Find("rider")!.MoveAgain);
        Assert.Equal(RejectionReason.NotThisSide, state.Refused(new MoveAgain("rider", new Coord(1, 2))).Reason);
    }

    [Fact]
    public void AMoveAgainOwedIsInTheCanonicalStateAndRecallRestoresIt()
    {
        var start = Start();
        var waited = start.Do(new Wait("rider"));
        Assert.Contains(" again 6", waited.Canonical());
        Assert.DoesNotContain(" again ", start.Canonical());

        var movedAgain = waited.Do(new MoveAgain("rider", new Coord(1, 2)));
        var back = movedAgain.Do(new Recall(1));
        Assert.Equal(6, back.Find("rider")!.MoveAgain);
    }

    [Fact]
    public void AMoveAgainDestinationWakesAGuardGroupByProximity()
    {
        var state = Start().Do(new Wait("rider"));

        var result = state.Try(new MoveAgain("rider", new Coord(5, 0)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(4, new Coord(5, 0).DistanceTo(new Coord(9, 0)));
        Assert.Equal(new GroupWoke("watch", WakeCause.Proximity), Assert.Single(result.Events.OfType<GroupWoke>()));
    }

    [Fact]
    public void TheStrikingTileWakesAGuardGroupByNoiseEvenWhenTheMoveAgainEndsOutsideTheRadius()
    {
        var state = Start().Do(new Move("rider", new Coord(4, 1)));
        Assert.False(state.IsAwake("watch"));

        var strike = state.Try(new Attack("rider", "brigand-1"));

        Assert.True(strike.Accepted, strike.Rejection?.Message);
        Assert.Equal(new GroupWoke("watch", WakeCause.Noise), Assert.Single(strike.Events.OfType<GroupWoke>()));
        var away = strike.Next.Try(new MoveAgain("rider", new Coord(1, 1)));
        Assert.True(away.Accepted, away.Rejection?.Message);
        Assert.Empty(away.Events.OfType<GroupWoke>());
        Assert.True(away.Next.IsAwake("watch"));
    }

    [Fact]
    public void TheLegalCommandsOfAUnitOwedAMoveAgainAreItsMoveAgainDestinationsIncludingItsOwnTile()
    {
        var state = Start().Do(new Move("rider", new Coord(4, 1))).Do(new Attack("rider", "brigand-1"));
        var rider = state.Find("rider")!;

        var movesAgain = Resolver.Legal(state, Starter).OfType<MoveAgain>().ToList();

        Assert.Equal(state.MoveAgainReachOf(rider, Starter)!.Destinations.Select(to => new MoveAgain("rider", to)), movesAgain);
        Assert.Contains(new MoveAgain("rider", rider.At), movesAgain);
        Assert.Empty(Resolver.Legal(state.Do(new MoveAgain("rider", rider.At)), Starter).OfType<MoveAgain>());
    }

    [Fact]
    public void AUnitOwedAMoveAgainIsAskedItsThreatFromAnyTileItsMoveAgainCanEndOn()
    {
        var state = Start().Do(new Move("rider", new Coord(3, 2))).Do(new Attack("rider", "brigand-1"));
        var rider = state.Find("rider")!;

        Assert.NotNull(Queries.Threats(state, Starter, rider, new Coord(1, 3)));
        Assert.Null(Queries.Threats(state, Starter, rider, new Coord(0, 0)));
        var done = state.Do(new MoveAgain("rider", new Coord(1, 3)));
        Assert.Null(Queries.Threats(done, Starter, done.Find("rider")!, new Coord(1, 2)));
    }
}
