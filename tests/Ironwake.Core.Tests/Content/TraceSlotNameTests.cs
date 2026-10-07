using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The Sim's trace names a weapon or item by id (issue 1114): the console refuses a slot number
/// that a swing has moved since the pack was last listed, so a trace that typed numbers would
/// stop replaying after the first reorder. A duplicate id keeps its number.
/// </summary>
public class TraceSlotNameTests
{
    private static BattleState Start() =>
        BattleState.From(MapFixture.Parse(MapFixture.OldMillRoad), MapFixture.Content, MapFixture.Content.Cast, 1);

    [Fact]
    public void ATraceNamesAWeaponOrItemByIdWhenItsIdIsUniqueInThePack()
    {
        var state = Start();

        Assert.Equal("attack captain brigand-1 iron_sword", Ironwake.Sim.Program.Script(state, MapFixture.Content, new Attack("captain", "brigand-1", 0)));
        Assert.Equal("item captain field_dressing", Ironwake.Sim.Program.Script(state, MapFixture.Content, new UseItem("captain", 1)));
    }

    [Fact]
    public void ATraceKeepsTheNumberWhenThePackCarriesTheIdTwice()
    {
        var state = Start();
        var captain = state.Find("captain")!;
        var doubled = captain with { Unit = captain.Unit with { Inventory = captain.Unit.Inventory.Add(new ItemStack("iron_sword", 40)) } };
        state = state.WithUnit(doubled);

        Assert.Null(Ironwake.Sim.Program.UniqueItemAt(state, "captain", 0));
        Assert.Equal("attack captain brigand-1 1", Ironwake.Sim.Program.Script(state, MapFixture.Content, new Attack("captain", "brigand-1", 0)));
    }

    [Fact]
    public void ATraceWritesTheDrakeVerbsAndAHealArtAsTheScriptReaderTakesThem()
    {
        var state = Start();

        Assert.Equal("carry rook wren 4,2 4,3", Ironwake.Sim.Program.Script(new Carry("rook", "wren", new Coord(4, 2), new Coord(4, 3))));
        Assert.Equal("breathe rook 5,5", Ironwake.Sim.Program.Script(new Breathe("rook", new Coord(5, 5))));
        Assert.Equal("item maud 2 wren art unasked", Ironwake.Sim.Program.Script(new UseItem("maud", 1, "wren", "unasked")));
        Assert.Equal("item captain field_dressing captain art unasked", Ironwake.Sim.Program.Script(state, MapFixture.Content, new UseItem("captain", 1, "captain", "unasked")));
    }
}
