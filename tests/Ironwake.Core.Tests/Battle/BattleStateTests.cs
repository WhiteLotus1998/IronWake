using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>Building the opening state from a map and a roster (issue 6).</summary>
public class BattleStateTests
{
    [Fact]
    public void TheRosterFillsTheSlotsAndEnemiesAreNamedByTemplateAndCount()
    {
        var state = Start();

        Assert.Equal(new[] { "brigand-1", "hale", "soldier-1", "wren" }, state.Units.Select(u => u.Id));
        Assert.Equal(new Coord(0, 1), state.Find("hale")!.At);
        Assert.Equal(new Coord(0, 2), state.Find("wren")!.At);
        Assert.Equal(Side.Player, state.Find("hale")!.Side);
        Assert.Equal(Side.Enemy, state.Find("brigand-1")!.Side);
        Assert.Equal("yard", state.Find("brigand-1")!.Group);
        Assert.Equal(Behavior.Aggressive, state.Find("brigand-1")!.Behavior);
        Assert.Equal(1, state.Turn);
        Assert.Equal(Side.Player, state.Phase);
        Assert.Equal(3, state.RecallCharges);
        Assert.Empty(state.History);
    }

    [Fact]
    public void EveryUnitStartsAtFullEffectiveHp()
    {
        var state = Start();

        Assert.Equal(22, state.Find("hale")!.Hp);
        var brigand = state.Find("brigand-1")!;
        Assert.Equal(brigand.MaxHp(Starter), brigand.Hp);
        Assert.Equal(brigand.Unit.EffectiveStats(Starter.Class("reaver")).Hp, brigand.Hp);
    }

    [Fact]
    public void TwoPlacementsOfOneTemplateCountFromOne()
    {
        var map = Yard.Replace("E soldier 3,2 group:yard behavior:aggressive", "E brigand 3,2 group:yard behavior:aggressive");

        var state = Start(map: map);

        Assert.Equal(new[] { "brigand-1", "brigand-2", "hale", "wren" }, state.Units.Select(u => u.Id));
        Assert.Equal(new Coord(3, 2), state.Find("brigand-2")!.At);
        Assert.Equal("brigand-2", state.Find("brigand-2")!.Unit.Id);
    }

    [Fact]
    public void BareRecruitSlotsAreFilledInRosterOrderSkippingNamedRecruits()
    {
        var state = BattleState.From(
            MapFixture.Parse(MapFixture.OldMillRoad.Replace("P recruit:wren 2,8", "P recruit 2,8\nP recruit:ivo 3,8")),
            Starter,
            ValueList<Unit>.Of(Hale, Ivo, Wren),
            1);

        Assert.Equal(new Coord(2, 8), state.Find("wren")!.At);
        Assert.Equal(new Coord(3, 8), state.Find("ivo")!.At);
    }

    [Fact]
    public void RosterUnitsBeyondTheSlotsAreBenched()
    {
        var state = Start(roster: ValueList<Unit>.Of(Hale, Wren, Ivo));

        Assert.Null(state.Find("ivo"));
    }

    [Fact]
    public void ARosterShortOfTheMapIsRefusedNamingTheSlot()
    {
        var ex = Assert.Throws<ArgumentException>(() => Start(roster: ValueList<Unit>.Of(Hale)));
        Assert.Contains("recruit 'wren' at 0,2", ex.Message);

        var bare = Yard.Replace("P recruit:wren 0,2", "P recruit 0,2");
        var ex2 = Assert.Throws<ArgumentException>(() => Start(roster: ValueList<Unit>.Of(Hale), map: bare));
        Assert.Contains("recruit slot at 0,2", ex2.Message);

        Assert.Throws<ArgumentException>(() => Start(roster: ValueList<Unit>.Empty));
    }

    [Fact]
    public void AUnitThatCannotStandOnItsSlotIsRefused()
    {
        var rider = new Unit("rook", "rook", "outrider", 1, 0, Hale.Stats, Stats.Zero, Inventory.Empty, ValueList<string>.Empty);
        var map = Yard.Replace("......\n......\n......\n......", "......\n......\nM.....\n......");

        var ex = Assert.Throws<ArgumentException>(() => Start(roster: ValueList<Unit>.Of(Hale, rider with { Id = "wren" }), map: map));

        Assert.Contains("cavalry cannot stand on Mountain at 0,2", ex.Message);
    }

    [Fact]
    public void OccupancyIsAnsweredFromWhereUnitsStandNow()
    {
        var state = Start().Do(new Move("hale", new Coord(2, 1)));

        Assert.Equal(Occupant.None, state.OccupantAt(new Coord(0, 1), Side.Player));
        Assert.Equal(Occupant.Ally, state.OccupantAt(new Coord(2, 1), Side.Player));
        Assert.Equal(Occupant.Enemy, state.OccupantAt(new Coord(2, 1), Side.Enemy));
        Assert.Equal(Occupant.Enemy, state.OccupantAt(new Coord(3, 1), Side.Player));
        Assert.False(state.ReachOf(state.Find("wren")!, Starter).CanEnd(new Coord(2, 1)));
        Assert.True(state.ReachOf(state.Find("wren")!, Starter).CanCross(new Coord(2, 1)));
    }

    [Fact]
    public void TheCanonicalTextIsOneLinePerUnitAndChangesWithTheState()
    {
        var start = Start();
        var moved = start.Do(new Move("hale", new Coord(2, 1)));

        var text = start.Canonical();
        Assert.Contains("turn 1 phase Player seed 7 scheme TwoRollAverage recall 3 history 0\n", text);
        Assert.Contains("unit hale Player 0,1 hp 22 unmoved ready class cadet level 1 exp 0 stats HP 22", text);
        Assert.Contains("items iron_swordx40", text);
        Assert.Contains("unit brigand-1 Enemy 3,1 hp", text);
        Assert.Contains("group yard Aggressive\n", text);
        Assert.NotEqual(text, moved.Canonical());
        Assert.Contains("unit hale Player 2,1 hp 22 moved ready", moved.Canonical());
        Assert.Equal(text, Start().Canonical());
    }
}
