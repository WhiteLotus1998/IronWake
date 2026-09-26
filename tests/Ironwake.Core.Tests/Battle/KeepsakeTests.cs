using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Carry the fallen (DESIGN.md 13.8, experiment): on a <c>keepsakes: on</c> map a player unit
/// that falls leaves its equipped weapon on its tile under its name; an ally standing there
/// takes it with Recover as its action; the name stays on the stack; Recall restores the tile.
/// </summary>
public class KeepsakeTests
{
    private static readonly Coord Grave = new(2, 1);

    private static string Yard(bool keepsakes) =>
        $"""
        name: Yard
        size: 6x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(keepsakes ? "keepsakes: on" : "")}

        ......
        ......
        ......
        ......

        units:
        P captain 0,1
        P recruit:wren 0,2
        E brigand 3,1 group:yard behavior:aggressive
        E soldier 5,3 group:yard behavior:aggressive

        """.Replace("\n\n\n", "\n\n");

    /// <summary>A board where Wren, at 1 HP, has just died on the brigand's counter at 2,1: the first seed on which the counter lands.</summary>
    private static (BattleState State, ApplyResult Death) WrenFalls(bool keepsakes = true)
    {
        for (ulong seed = 1; seed < 200; seed++)
        {
            var state = BattleFixture.Start(seed, map: Yard(keepsakes));
            state = state.WithUnit(state.Find("wren")! with { Hp = 1 }).Do(new Move("wren", Grave));
            var result = state.Try(new Attack("wren", "brigand-1"));
            if (result.Next.Find("wren") is null && result.Next.Find("brigand-1") is not null)
            {
                return (state, result);
            }
        }

        throw new InvalidOperationException("no seed below 200 lets the counter kill Wren");
    }

    [Fact]
    public void AFallenPlayerUnitLeavesItsEquippedWeaponOnItsTile()
    {
        var (_, death) = WrenFalls();

        var keepsake = Assert.Single(death.Next.Keepsakes);
        Assert.Equal(new Keepsake(Grave, "wren", new ItemStack("iron_sword", keepsake.Item.Uses) { Keepsake = "wren" }), keepsake);
        Assert.Contains(new KeepsakeLeft("wren", "iron_sword", Grave), death.Events);
        Assert.Equal(keepsake, death.Next.KeepsakeAt(Grave));
    }

    [Fact]
    public void TheKeepsakeLegendNamesTheWeaponAndTheFallenByTheirDisplayNames()
    {
        var (_, death) = WrenFalls();

        Assert.Contains("keepsakes: Iron Sword (Wren's) at 2,1\n", MapRenderer.Render(death.Next, Starter));
    }

    [Fact]
    public void AMapWithoutTheHeaderLeavesNoKeepsake()
    {
        var (_, death) = WrenFalls(keepsakes: false);

        Assert.Empty(death.Next.Keepsakes);
        Assert.DoesNotContain(death.Events, e => e is KeepsakeLeft);
    }

    [Fact]
    public void AnEnemyThatFallsLeavesNoKeepsake()
    {
        var state = BattleFixture.Start(map: Yard(true));
        state = state.WithUnit(state.Find("brigand-1")! with { Hp = 1 }).Do(new Move("hale", Grave));

        var result = state.Try(new Attack("hale", "brigand-1"));

        Assert.Null(result.Next.Find("brigand-1"));
        Assert.Empty(result.Next.Keepsakes);
    }

    [Fact]
    public void AnAllyOnTheTileRecoversTheWeaponUnderTheFallensNameAsItsAction()
    {
        var (_, death) = WrenFalls();
        var onGrave = death.Next.Do(new Move("hale", Grave));
        var before = onGrave.Find("hale")!.Unit.Inventory.Count;

        var result = onGrave.Try(new Recover("hale"));

        Assert.True(result.Accepted);
        Assert.Equal(new GameEvent[] { new KeepsakeRecovered("hale", "wren", "iron_sword") }, result.Events.ToArray());
        var hale = result.Next.Find("hale")!;
        Assert.Equal(before + 1, hale.Unit.Inventory.Count);
        Assert.Equal("wren", hale.Unit.Inventory.Items[^1].Keepsake);
        Assert.True(hale.Acted);
        Assert.Empty(result.Next.Keepsakes);
        Assert.Equal(" (Wren's)", Keepsake.Suffix(hale.Unit.Inventory.Items[^1], Starter));
    }

    [Fact]
    public void RecoverIsRefusedWhereNothingWasLeft()
    {
        var (_, death) = WrenFalls();

        Assert.Equal(RejectionReason.NoKeepsake, death.Next.Refused(new Recover("hale")).Reason);
    }

    [Fact]
    public void RecoverIsRefusedWithAFullInventory()
    {
        var (_, death) = WrenFalls();
        var state = death.Next.Do(new Move("hale", Grave));
        var hale = state.Find("hale")!;
        var full = new Inventory(ValueList<ItemStack>.From(Enumerable.Repeat(new ItemStack("iron_sword", 30), Inventory.Capacity)));
        state = state.WithUnit(hale with { Unit = hale.Unit with { Inventory = full } });

        Assert.Equal(RejectionReason.NoKeepsake, state.Refused(new Recover("hale")).Reason);
        Assert.DoesNotContain(Resolver.Legal(state, Starter), c => c is Recover);
    }

    [Fact]
    public void RecoverIsRefusedToAnEnemy()
    {
        var (_, death) = WrenFalls();
        var state = death.Next.Do(new EndPhase());
        var brigand = state.Find("brigand-1")!;
        state = state.WithUnit(brigand with { At = Grave });

        Assert.Equal(RejectionReason.NoKeepsake, state.Refused(new Recover("brigand-1")).Reason);
    }

    [Fact]
    public void RecoverIsLegalOnlyOnTheKeepsakesTile()
    {
        var (_, death) = WrenFalls();

        Assert.DoesNotContain(Resolver.Legal(death.Next, Starter), c => c is Recover);
        Assert.Contains(new Recover("hale"), Resolver.Legal(death.Next.Do(new Move("hale", Grave)), Starter));
    }

    [Fact]
    public void RecallRestoresTheKeepsakeWithTheBoard()
    {
        var (_, death) = WrenFalls();
        var recovered = death.Next.Do(new Move("hale", Grave)).Do(new Recover("hale"));

        var back = recovered.Do(new Recall(death.Next.History.Count - 1));

        Assert.Empty(back.Keepsakes);
        Assert.NotNull(back.Find("wren"));
        Assert.Single(recovered.History[death.Next.History.Count].Keepsakes);
    }

    [Fact]
    public void TheKeepsakesHeaderRoundTripsThroughTheMapFormat()
    {
        var map = MapFixture.Parse(Yard(true), "yard.map");

        Assert.True(map.KeepsakesEnabled);
        Assert.Contains("keepsakes: on\n", Ironwake.Content.MapFormat.Write(map, Starter));
        Assert.Equal(map, MapFixture.Parse(Ironwake.Content.MapFormat.Write(map, Starter), "yard.map"));
    }

    [Fact]
    public void KeepsakesAndTheirNamesRoundTripThroughTheProtocolState()
    {
        var (_, death) = WrenFalls();
        var lying = death.Next with { History = ValueList<BattleState>.Empty };
        var carried = lying.Do(new Move("hale", Grave)).Do(new Recover("hale")) with { History = ValueList<BattleState>.Empty };

        Assert.Equal(lying, Ironwake.Content.Protocol.ProtocolJson.ReadState(Ironwake.Content.Protocol.ProtocolJson.State(lying, Starter), Starter));
        Assert.Equal(carried, Ironwake.Content.Protocol.ProtocolJson.ReadState(Ironwake.Content.Protocol.ProtocolJson.State(carried, Starter), Starter));
    }
}
