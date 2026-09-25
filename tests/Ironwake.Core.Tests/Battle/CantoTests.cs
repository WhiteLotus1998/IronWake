using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Canto (issue 71, DESIGN.md section 7): an ability the cavalry classes carry in content.
/// After an Attack, Item or Wait the unit moves again on its Mov minus the path cost its
/// Move spent, through <see cref="Movement.Reach"/> from where it stands, and is then done.
/// Its own tile is a legal destination, so a Canto declined is a command. The wake check
/// runs after the Canto on where the unit ends.
/// </summary>
public class CantoTests
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
    public void TheOutriderClassCarriesCantoInContentAndTheCadetDoesNot()
    {
        Assert.Equal(ValueList<string>.Of("canto"), Starter.Class("outrider").Abilities);
        Assert.IsType<CantoEffect>(Starter.Ability("canto").Effect);
        Assert.Equal(AbilityTrigger.AfterAction, Starter.Ability("canto").Trigger);
        Assert.True(AbilityRules.HasCanto(Starter.AbilitiesOf(Rider)));
        Assert.True(AbilityRules.HasCanto(Starter.AbilitiesOf(Starter.Cast.Single(u => u.Id == "ansgar"))));
        Assert.False(AbilityRules.HasCanto(Starter.AbilitiesOf(Hale)));
    }

    [Fact]
    public void CantoIsHeldByTheClassSoAUnitThatLeavesTheClassLosesIt()
    {
        Assert.False(AbilityRules.HasCanto(Starter.AbilitiesOf(Rider with { ClassId = "cadet" })));
    }

    [Fact]
    public void TheCantoBudgetIsMovMinusThePathCostSpent()
    {
        var content = MovEight();
        var state = Start(content);
        state = Do(state, content, new Move("rider", new Coord(3, 2)));
        state = Do(state, content, new Attack("rider", "brigand-1"));

        var rider = state.Find("rider")!;
        Assert.Equal(5, rider.Canto);
        var reach = state.CantoReachOf(rider, content)!;
        var expected = Movement.Reach(state.Map, content, rider.At, MovementType.Cavalry, 5, at => state.OccupantAt(at, Side.Player));
        Assert.Equal(expected, reach);
        Assert.Equal(5, reach.Mov);
        Assert.Equal(expected, Queries.Reachable(state, content, rider));
    }

    [Fact]
    public void AUnitThatActsWithoutMovingIsOwedItsFullMov()
    {
        var state = Start().Do(new Wait("rider"));

        Assert.Equal(6, state.Find("rider")!.Canto);
    }

    [Fact]
    public void CantoFollowsAnItem()
    {
        var state = Start();
        state = state.WithUnit(state.Find("rider")! with { Hp = 20 }).Do(new UseItem("rider", 1));

        Assert.Equal(6, state.Find("rider")!.Canto);
        Assert.NotNull(state.CantoReachOf(state.Find("rider")!, Starter));
    }

    [Fact]
    public void AUnitThatSpentItsWholeMovGetsACantoOfZeroTilesAndItsOwnTileIsLegal()
    {
        var state = Start().Do(new Move("rider", new Coord(5, 3))).Do(new Wait("rider"));
        var rider = state.Find("rider")!;

        Assert.Equal(0, rider.Canto);
        Assert.Equal(new[] { new Coord(5, 3) }, state.CantoReachOf(rider, Starter)!.Destinations);
        Assert.Equal(RejectionReason.OutOfReach, state.Refused(new Canto("rider", new Coord(6, 3))).Reason);
        var result = state.Try(new Canto("rider", new Coord(5, 3)));
        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(new Cantoed("rider", new Coord(5, 3), new Coord(5, 3), ValueList<Coord>.Empty), result.Events);
    }

    [Fact]
    public void ACantoMovesTheUnitAlongTheReachPathAndTheUnitIsThenDone()
    {
        var state = Start().Do(new Move("rider", new Coord(3, 2))).Do(new Attack("rider", "brigand-1"));
        var result = state.Try(new Canto("rider", new Coord(1, 3)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var cantoed = Assert.Single(result.Events.OfType<Cantoed>());
        Assert.Equal(new Coord(3, 2), cantoed.From);
        Assert.Equal(new Coord(1, 3), cantoed.To);
        Assert.Equal(state.CantoReachOf(state.Find("rider")!, Starter)!.PathTo(new Coord(1, 3)), cantoed.Path);
        var after = result.Next;
        Assert.Equal(new Coord(1, 3), after.Find("rider")!.At);
        Assert.Null(after.Find("rider")!.Canto);
        Assert.Equal(RejectionReason.NoCanto, after.Refused(new Canto("rider", new Coord(1, 2))).Reason);
        Assert.Equal(RejectionReason.AlreadyActed, after.Refused(new Move("rider", new Coord(1, 2))).Reason);
        Assert.Equal(RejectionReason.AlreadyActed, after.Refused(new Wait("rider")).Reason);
    }

    [Fact]
    public void ACantoBeyondWhatTheMoveLeftIsRefused()
    {
        var state = Start().Do(new Move("rider", new Coord(3, 2))).Do(new Attack("rider", "brigand-1"));

        var rejection = state.Refused(new Canto("rider", new Coord(0, 0)));

        Assert.Equal(RejectionReason.OutOfReach, rejection.Reason);
        Assert.Contains("not within the 3 movement its Canto has left", rejection.Message);
    }

    [Fact]
    public void ACantoOntoAnAllyIsRefused()
    {
        var state = Start().Do(new Wait("rider"));

        Assert.Equal(RejectionReason.OutOfReach, state.Refused(new Canto("rider", new Coord(0, 1))).Reason);
    }

    [Fact]
    public void ACantoBeforeTheUnitActsIsRefused()
    {
        var state = Start();

        var rejection = state.Refused(new Canto("rider", new Coord(1, 2)));
        Assert.Equal(RejectionReason.NoCanto, rejection.Reason);
        Assert.Contains("has not acted", rejection.Message);
        rejection = state.Do(new Move("rider", new Coord(1, 2))).Refused(new Canto("rider", new Coord(2, 2)));
        Assert.Equal(RejectionReason.NoCanto, rejection.Reason);
    }

    [Fact]
    public void AUnitWithoutCantoIsRefusedOne()
    {
        var state = Start().Do(new Wait("hale"));

        var rejection = state.Refused(new Canto("hale", new Coord(1, 1)));
        Assert.Equal(RejectionReason.NoCanto, rejection.Reason);
        Assert.Contains("has no Canto", rejection.Message);
        Assert.Null(state.Find("hale")!.Canto);
    }

    [Fact]
    public void ACantoNotTakenIsLostWhenThePhaseEnds()
    {
        var state = Start().Do(new Wait("rider"));
        Assert.Equal(6, state.Find("rider")!.Canto);

        state = state.Do(new EndPhase());

        Assert.Null(state.Find("rider")!.Canto);
        Assert.Equal(RejectionReason.NotThisSide, state.Refused(new Canto("rider", new Coord(1, 2))).Reason);
    }

    [Fact]
    public void ACantoOwedIsInTheCanonicalStateAndRecallRestoresIt()
    {
        var start = Start();
        var waited = start.Do(new Wait("rider"));
        Assert.Contains(" canto 6", waited.Canonical());
        Assert.DoesNotContain(" canto ", start.Canonical());

        var cantoed = waited.Do(new Canto("rider", new Coord(1, 2)));
        var back = cantoed.Do(new Recall(1));
        Assert.Equal(6, back.Find("rider")!.Canto);
    }

    [Fact]
    public void ACantoDestinationWakesAGuardGroupByProximity()
    {
        var state = Start().Do(new Wait("rider"));

        var result = state.Try(new Canto("rider", new Coord(5, 0)));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(4, new Coord(5, 0).DistanceTo(new Coord(9, 0)));
        Assert.Equal(new GroupWoke("watch", WakeCause.Proximity), Assert.Single(result.Events.OfType<GroupWoke>()));
    }

    [Fact]
    public void TheStrikingTileWakesAGuardGroupByNoiseEvenWhenTheCantoEndsOutsideTheRadius()
    {
        var state = Start().Do(new Move("rider", new Coord(4, 1)));
        Assert.False(state.IsAwake("watch"));

        var strike = state.Try(new Attack("rider", "brigand-1"));

        Assert.True(strike.Accepted, strike.Rejection?.Message);
        Assert.Equal(new GroupWoke("watch", WakeCause.Noise), Assert.Single(strike.Events.OfType<GroupWoke>()));
        var away = strike.Next.Try(new Canto("rider", new Coord(1, 1)));
        Assert.True(away.Accepted, away.Rejection?.Message);
        Assert.Empty(away.Events.OfType<GroupWoke>());
        Assert.True(away.Next.IsAwake("watch"));
    }

    [Fact]
    public void TheLegalCommandsOfAUnitOwedACantoAreItsCantoDestinationsIncludingItsOwnTile()
    {
        var state = Start().Do(new Move("rider", new Coord(4, 1))).Do(new Attack("rider", "brigand-1"));
        var rider = state.Find("rider")!;

        var cantos = Resolver.Legal(state, Starter).OfType<Canto>().ToList();

        Assert.Equal(state.CantoReachOf(rider, Starter)!.Destinations.Select(to => new Canto("rider", to)), cantos);
        Assert.Contains(new Canto("rider", rider.At), cantos);
        Assert.Empty(Resolver.Legal(state.Do(new Canto("rider", rider.At)), Starter).OfType<Canto>());
    }

    [Fact]
    public void AUnitOwedACantoIsAskedItsThreatFromAnyTileItsCantoCanEndOn()
    {
        var state = Start().Do(new Move("rider", new Coord(3, 2))).Do(new Attack("rider", "brigand-1"));
        var rider = state.Find("rider")!;

        Assert.NotNull(Queries.Threats(state, Starter, rider, new Coord(1, 3)));
        Assert.Null(Queries.Threats(state, Starter, rider, new Coord(0, 0)));
        var done = state.Do(new Canto("rider", new Coord(1, 3)));
        Assert.Null(Queries.Threats(done, Starter, done.Find("rider")!, new Coord(1, 2)));
    }
}
