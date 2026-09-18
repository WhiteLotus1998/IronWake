using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>Every command's rule, the events it emits, and the rejection that shows each guard firing (issue 6).</summary>
public class ResolverTests
{
    private static readonly Coord BesideBrigand = new(2, 1);

    [Fact]
    public void MoveWalksAReachablePathAndMarksTheUnitMoved()
    {
        var result = Start().Try(new Move("hale", BesideBrigand));

        Assert.True(result.Accepted);
        var moved = Assert.IsType<UnitMoved>(Assert.Single(result.Events));
        Assert.Equal(new UnitMoved("hale", new Coord(0, 1), BesideBrigand, ValueList<Coord>.Of(new Coord(1, 1), BesideBrigand)), moved);
        var hale = result.Next.Find("hale")!;
        Assert.Equal(BesideBrigand, hale.At);
        Assert.True(hale.Moved);
        Assert.False(hale.Acted);
        Assert.Single(result.Next.History);
        Assert.Empty(result.Next.History[0].History);
    }

    [Fact]
    public void MoveIsRefusedForAnUnknownUnit()
    {
        var rejection = Start().Refused(new Move("nobody", BesideBrigand));

        Assert.Equal(RejectionReason.NoSuchUnit, rejection.Reason);
        Assert.Equal("no living unit 'nobody'", rejection.Message);
    }

    [Fact]
    public void MoveIsRefusedOnTheOtherSidesPhase()
    {
        var rejection = Start().Refused(new Move("brigand-1", new Coord(4, 1)));

        Assert.Equal(RejectionReason.NotThisSide, rejection.Reason);
        Assert.Equal("brigand-1 is a Enemy unit and it is the Player phase", rejection.Message);
    }

    [Fact]
    public void ASecondMoveInOnePhaseIsRefused()
    {
        var rejection = Start().Do(new Move("hale", new Coord(1, 1))).Refused(new Move("hale", BesideBrigand));

        Assert.Equal(RejectionReason.AlreadyMoved, rejection.Reason);
    }

    [Fact]
    public void MoveIsRefusedBeyondReachOntoAnAllyOrOffTheMap()
    {
        var state = Start();

        var far = state.Refused(new Move("hale", new Coord(5, 3)));
        Assert.Equal(RejectionReason.OutOfReach, far.Reason);
        Assert.Equal("hale cannot move to 5,3: not within 4 movement from 0,1", far.Message);

        var ally = state.Refused(new Move("hale", new Coord(0, 2)));
        Assert.Equal(RejectionReason.OutOfReach, ally.Reason);
        Assert.Equal("hale cannot move to 0,2: occupied by an ally", ally.Message);

        var enemy = state.Refused(new Move("hale", new Coord(3, 1)));
        Assert.Equal(RejectionReason.OutOfReach, enemy.Reason);

        var outside = state.Refused(new Move("hale", new Coord(0, -1)));
        Assert.Equal("hale cannot move to 0,-1: outside the map", outside.Message);
    }

    [Fact]
    public void MoveToTheOwnTileIsLegalAndSpendsTheMove()
    {
        var state = Start().Do(new Move("hale", new Coord(0, 1)));

        Assert.True(state.Find("hale")!.Moved);
    }

    [Fact]
    public void AttackFightsTheCombatOnTheStatesSeedAndEndsTheAction()
    {
        var state = Start().Do(new Move("hale", BesideBrigand));

        var result = state.Try(new Attack("hale", "brigand-1"));

        Assert.True(result.Accepted);
        var fought = Assert.IsType<CombatFought>(result.Events[0]);
        var expected = CombatResolver.Resolve(
            state.Find("hale")!.ToCombatant(state.Map, Starter),
            state.Find("brigand-1")!.ToCombatant(state.Map, Starter),
            1,
            new CombatContext(1, Side.Player),
            new KeyedRng(7),
            RollScheme.TwoRollAverage);
        Assert.Equal(expected.Strikes, fought.Strikes);
        Assert.Equal(expected.AttackerHp, result.Next.Find("hale")!.Hp);
        Assert.Equal(expected.DefenderHp, result.Next.Find("brigand-1")?.Hp ?? 0);
        Assert.True(result.Next.Find("hale")!.Acted);
        Assert.NotEmpty(fought.Strikes);
    }

    [Fact]
    public void ADeadUnitLeavesTheBoardWithAnEvent()
    {
        var wounded = Start();
        var brigand = wounded.Find("brigand-1")!;
        wounded = wounded.WithUnit(brigand with { Hp = 1 }).Do(new Move("hale", BesideBrigand));

        var result = wounded.Try(new Attack("hale", "brigand-1"));

        var fought = Assert.IsType<CombatFought>(result.Events[0]);
        Assert.True(fought.Strikes[0].Hit, "the seed was chosen so the first strike lands");
        Assert.Equal(new UnitDied("brigand-1", Side.Enemy, new Coord(3, 1)), result.Events[1]);
        Assert.Null(result.Next.Find("brigand-1"));
        Assert.Equal(Occupant.None, result.Next.OccupantAt(new Coord(3, 1), Side.Player));
        Assert.Equal(RejectionReason.NoSuchTarget, result.Next.Do(new EndPhase()).Do(new EndPhase()).Refused(new Attack("wren", "brigand-1")).Reason);
    }

    [Fact]
    public void AttackIsRefusedForAnAllyAMissingTargetOrAnActedUnit()
    {
        var state = Start().Do(new Move("hale", BesideBrigand));

        Assert.Equal(RejectionReason.NotAnEnemy, state.Refused(new Attack("hale", "wren")).Reason);
        Assert.Equal(RejectionReason.NoSuchTarget, state.Refused(new Attack("hale", "ghost")).Reason);
        Assert.Equal(RejectionReason.AlreadyActed, state.Do(new Wait("hale")).Refused(new Attack("hale", "brigand-1")).Reason);
    }

    [Fact]
    public void AttackIsRefusedOutOfRangeNamingTheWeaponsReach()
    {
        var rejection = Start().Refused(new Attack("hale", "brigand-1"));

        Assert.Equal(RejectionReason.OutOfRange, rejection.Reason);
        Assert.Equal("brigand-1 at 3,1 is 3 tiles from hale at 0,1; Iron Sword reaches 1-1", rejection.Message);
    }

    [Fact]
    public void AttackIsRefusedWithoutAWeapon()
    {
        var state = Start(roster: ValueList<Unit>.Of(Unarmed, Wren)).Do(new Move("pell", BesideBrigand));

        var rejection = state.Refused(new Attack("pell", "brigand-1"));

        Assert.Equal(RejectionReason.NoWeapon, rejection.Reason);
        Assert.Null(state.Find("pell")!.EquippedWeapon(Starter));
    }

    [Fact]
    public void AnUnarmedDefenderTakesTheHitsAndCannotCounter()
    {
        var state = Start(roster: ValueList<Unit>.Of(Unarmed, Wren)).Do(new Move("pell", BesideBrigand)).Do(new EndPhase());

        var result = state.Try(new Attack("brigand-1", "pell"));

        var fought = Assert.IsType<CombatFought>(result.Events[0]);
        Assert.All(fought.Strikes, strike => Assert.Equal("brigand-1", strike.AttackerId));
    }

    [Fact]
    public void UseItemIsRefusedOutLoudUntilIssue9()
    {
        var rejection = Start().Refused(new UseItem("hale", 0));

        Assert.Equal(RejectionReason.NotAvailable, rejection.Reason);
        Assert.Equal("hale cannot use an item: items are not in this build (issue 9)", rejection.Message);
        Assert.Equal(RejectionReason.NoSuchUnit, Start().Refused(new UseItem("nobody", 0)).Reason);
    }

    [Fact]
    public void WaitEndsTheActionAndCannotBeRepeated()
    {
        var result = Start().Try(new Wait("hale"));

        Assert.Equal(new UnitWaited("hale"), Assert.Single(result.Events));
        Assert.True(result.Next.Find("hale")!.Acted);
        Assert.Equal(RejectionReason.AlreadyActed, result.Next.Refused(new Wait("hale")).Reason);
        Assert.Equal(RejectionReason.AlreadyActed, result.Next.Refused(new Move("hale", BesideBrigand)).Reason);
    }

    [Fact]
    public void EndPhaseFlipsTheSideThenIncrementsTheTurnAndClearsEveryFlag()
    {
        var player = Start().Do(new Wait("hale"));

        var enemyPhase = player.Try(new EndPhase());
        Assert.Equal(new GameEvent[] { new PhaseEnded(Side.Player, 1), new PhaseBegan(Side.Enemy, 1) }, enemyPhase.Events);
        Assert.Equal(Side.Enemy, enemyPhase.Next.Phase);
        Assert.Equal(1, enemyPhase.Next.Turn);
        Assert.False(enemyPhase.Next.Find("hale")!.Acted);

        var turn2 = enemyPhase.Next.Do(new Wait("brigand-1")).Try(new EndPhase());
        Assert.Equal(new GameEvent[] { new PhaseEnded(Side.Enemy, 1), new PhaseBegan(Side.Player, 2) }, turn2.Events);
        Assert.Equal(Side.Player, turn2.Next.Phase);
        Assert.Equal(2, turn2.Next.Turn);
        Assert.All(turn2.Next.Units, unit => Assert.False(unit.Acted || unit.Moved));
    }

    [Fact]
    public void AnUnknownCommandIsRejectedNotThrown()
    {
        var rejection = Start().Refused(new Mystery());

        Assert.Equal(RejectionReason.UnknownCommand, rejection.Reason);
    }

    private sealed record Mystery : Command;
}
