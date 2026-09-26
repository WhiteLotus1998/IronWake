using Ironwake.Cli;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 295, 13.8's second arm: a tile holds a stack of keepsakes and <c>recover</c> takes
/// the newest; an enemy that ends a move on a stack takes all of it, carries it past the
/// inventory cap, strikes with it whenever it can wield one, and drops every keepsake where
/// it dies; the battle's end names each keepsake nobody recovered; Recall restores all of it.
/// </summary>
public class KeepsakeCarrierTests
{
    private static readonly Coord Grave = new(2, 1);

    private const string Yard = """
        name: Yard
        size: 6x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        keepsakes: on

        ......
        ......
        ......
        ......

        units:
        P captain 0,1
        P recruit:wren 0,2
        E brigand 3,1 group:yard behavior:aggressive
        E soldier 5,3 group:yard behavior:aggressive

        """;

    private static Keepsake Left(string fallen, string item, Coord at, int uses = 20) =>
        new(at, fallen, new ItemStack(item, uses) { Keepsake = fallen });

    private static BattleState WithStack(params Keepsake[] stack) =>
        Start(map: Yard) with { Keepsakes = ValueList<Keepsake>.From(stack) };

    private static BattleState WithInventory(BattleState state, string unitId, params ItemStack[] items)
    {
        var unit = state.Find(unitId)!;
        return state.WithUnit(unit with { Unit = unit.Unit with { Inventory = new Inventory(ValueList<ItemStack>.From(items)) } });
    }

    [Fact]
    public void KeepsakeAtReadsTheNewestOfATilesStack()
    {
        var state = WithStack(Left("wren", "iron_sword", Grave), Left("teodor", "iron_lance", Grave));

        Assert.Equal("teodor", state.KeepsakeAt(Grave)!.FallenId);
    }

    [Fact]
    public void RecoverTakesTheNewestKeepsakeFirstOnePerAction()
    {
        var state = WithStack(Left("wren", "iron_sword", Grave), Left("teodor", "iron_lance", Grave)).Do(new Move("hale", Grave));

        var first = state.Try(new Recover("hale"));

        Assert.Equal(new GameEvent[] { new KeepsakeRecovered("hale", "teodor", "iron_lance") }, first.Events.ToArray());
        Assert.Equal("wren", Assert.Single(first.Next.Keepsakes).FallenId);
    }

    [Fact]
    public void RecoverOfOneOfTwoIdenticalKeepsakesLeavesTheOther()
    {
        var state = WithStack(Left("wren", "iron_sword", Grave), Left("wren", "iron_sword", Grave)).Do(new Move("hale", Grave));

        Assert.Single(state.Do(new Recover("hale")).Keepsakes);
    }

    [Fact]
    public void AnEnemyThatEndsAMoveOnAStackTakesAllOfIt()
    {
        var state = WithStack(Left("wren", "iron_sword", Grave), Left("teodor", "iron_axe", Grave)).Do(new EndPhase());

        var result = state.Try(new Move("brigand-1", Grave));

        Assert.Contains(new KeepsakeTaken("brigand-1", "wren", "iron_sword"), result.Events);
        Assert.Contains(new KeepsakeTaken("brigand-1", "teodor", "iron_axe"), result.Events);
        Assert.Empty(result.Next.Keepsakes);
        var items = result.Next.Find("brigand-1")!.Unit.Inventory.Items;
        Assert.Equal(new[] { "iron_axe", "iron_sword", "iron_axe" }, items.Select(i => i.ItemId));
        Assert.Equal(new string?[] { null, "wren", "teodor" }, items.Select(i => i.Keepsake));
    }

    [Fact]
    public void APlayerUnitThatEndsAMoveOnAStackTakesNothing()
    {
        var state = WithStack(Left("wren", "iron_sword", Grave));

        var result = state.Try(new Move("hale", Grave));

        Assert.DoesNotContain(result.Events, e => e is KeepsakeTaken);
        Assert.Single(result.Next.Keepsakes);
    }

    [Fact]
    public void ACarrierCarriesKeepsakesPastTheInventoryCap()
    {
        var full = Enumerable.Repeat(new ItemStack("iron_axe", 30), Inventory.Capacity).ToArray();
        var state = WithInventory(WithStack(Left("wren", "iron_sword", Grave)), "brigand-1", full).Do(new EndPhase());

        var carrier = state.Do(new Move("brigand-1", Grave)).Find("brigand-1")!;

        Assert.Equal(Inventory.Capacity + 1, carrier.Unit.Inventory.Count);
        Assert.Equal("wren", carrier.Unit.Inventory.Items[^1].Keepsake);
    }

    [Fact]
    public void TheInventoryCapStillRefusesASixthOrdinaryStack()
    {
        var six = ValueList<ItemStack>.From(Enumerable.Repeat(new ItemStack("iron_axe", 30), Inventory.Capacity + 1));

        Assert.Throws<ArgumentException>(() => new Inventory(six));
    }

    [Fact]
    public void ACarrierThatDiesDropsEveryKeepsakeItCarriesWhereItDied()
    {
        for (ulong seed = 1; seed < 200; seed++)
        {
            var state = WithInventory(Start(seed, map: Yard), "brigand-1",
                new ItemStack("iron_axe", 30), new ItemStack("iron_sword", 20) { Keepsake = "wren" }, new ItemStack("hatchet", 20) { Keepsake = "teodor" });
            state = state.WithUnit(state.Find("brigand-1")! with { Hp = 1 }).Do(new Move("hale", Grave));
            var result = state.Try(new Attack("hale", "brigand-1"));
            if (result.Next.Find("brigand-1") is not null)
            {
                continue;
            }

            var at = new Coord(3, 1);
            Assert.Equal(
                new[] { new KeepsakeLeft("wren", "iron_sword", at), new KeepsakeLeft("teodor", "hatchet", at) },
                result.Events.OfType<KeepsakeLeft>().ToArray());
            Assert.Equal(new[] { "wren", "teodor" }, result.Next.KeepsakesAt(at).Select(k => k.FallenId));
            return;
        }

        throw new InvalidOperationException("no seed below 200 lets hale kill the brigand");
    }

    [Fact]
    public void AFallenPlayerUnitAlsoLeavesTheKeepsakesItCarried()
    {
        var hale = Start(map: Yard).Find("hale")! with { At = Grave };
        hale = hale with { Unit = hale.Unit with { Inventory = new Inventory(ValueList<ItemStack>.Of(new ItemStack("iron_sword", 30), new ItemStack("iron_lance", 20) { Keepsake = "teodor" })) } };

        var dropped = Keepsake.Dropped(hale, Starter);

        Assert.Equal(new[] { ("hale", "iron_sword"), ("teodor", "iron_lance") }, dropped.Select(k => (k.FallenId, k.Item.ItemId)));
    }

    [Fact]
    public void AFallenPlayerUnitWieldingAnotherFallensKeepsakeLeavesItUnderThatName()
    {
        var hale = Start(map: Yard).Find("hale")! with { At = Grave };
        hale = hale with { Unit = hale.Unit with { Inventory = new Inventory(ValueList<ItemStack>.Of(new ItemStack("iron_lance", 20) { Keepsake = "teodor" })) } };

        var keepsake = Assert.Single(Keepsake.Dropped(hale, Starter));

        Assert.Equal("teodor", keepsake.FallenId);
        Assert.Equal("teodor", keepsake.Item.Keepsake);
    }

    [Fact]
    public void ACarrierStrikesWithTheKeepsakeOverABetterWeapon()
    {
        var state = Start(map: Yard);
        state = state.WithUnit(state.Find("hale")! with { At = new Coord(2, 1) }).Do(new EndPhase());
        var plain = WithInventory(state, "brigand-1", new ItemStack("steel_axe", 30), new ItemStack("hatchet", 20));
        var grudge = WithInventory(state, "brigand-1", new ItemStack("steel_axe", 30), new ItemStack("hatchet", 20) { Keepsake = "wren" });

        var free = EnemyAi.PlanUnit(plain, Starter, plain.Find("brigand-1")!).OfType<Attack>().Single();
        var held = EnemyAi.PlanUnit(grudge, Starter, grudge.Find("brigand-1")!).OfType<Attack>().Single();

        Assert.Null(free.Slot);
        Assert.Equal(1, held.Slot);
    }

    [Fact]
    public void ACarrierThatCannotWieldTheKeepsakeFightsWithItsOwnWeapon()
    {
        var state = Start(map: Yard);
        state = state.WithUnit(state.Find("hale")! with { At = new Coord(2, 1) }).Do(new EndPhase());
        state = WithInventory(state, "brigand-1", new ItemStack("iron_axe", 30), new ItemStack("iron_sword", 20) { Keepsake = "wren" });

        var attack = EnemyAi.PlanUnit(state, Starter, state.Find("brigand-1")!).OfType<Attack>().Single();

        Assert.Null(attack.Slot);
    }

    [Fact]
    public void AnEnemyThatWillTakeAStackStrikesWithItOnTheSameTurn()
    {
        var state = WithStack(Left("teodor", "steel_axe", Grave)).WithoutUnit("wren");
        state = state.WithUnit(state.Find("hale")! with { At = new Coord(1, 1) });
        state = WithInventory(state, "brigand-1", new ItemStack("hatchet", 30)).Do(new EndPhase());

        var plan = EnemyAi.PlanUnit(state, Starter, state.Find("brigand-1")!);

        Assert.Equal(new Command[] { new Move("brigand-1", Grave), new Attack("brigand-1", "hale", 1) }, plan.ToArray());
        Assert.True(state.Try(plan[0]).Next.Try(plan[1]).Accepted);
    }

    [Fact]
    public void ThreatAndTheForecastLineNameTheKeepsakeTheCarrierWillSwing()
    {
        var state = Start(map: Yard);
        state = state.WithUnit(state.Find("hale")! with { At = new Coord(2, 1) });
        state = WithInventory(state, "brigand-1", new ItemStack("steel_axe", 30), new ItemStack("hatchet", 20) { Keepsake = "wren" });
        var hale = state.Find("hale")!;

        var line = Queries.Threats(state, Starter, hale, hale.At)!.Single(l => l.Enemy.Id == "brigand-1");
        var text = PlaySession.ThreatText(state, Starter, hale, hale.At, new[] { line }, Array.Empty<SleepingThreat>());

        Assert.Equal("hatchet", line.Weapon.Id);
        Assert.Contains("brigand-1 from 3,1 with Hatchet (Wren's) (slot 2)", text);
        Assert.Equal(" with Hatchet (Wren's)", PlaySession.KeepsakeWith(state.Find("brigand-1")!, Starter, 1));
        Assert.Equal("", PlaySession.KeepsakeWith(state.Find("brigand-1")!, Starter, null));
    }

    [Fact]
    public void TheUnitRowNamesWhatACarrierCarries()
    {
        var state = WithInventory(Start(map: Yard), "brigand-1", new ItemStack("iron_axe", 30), new ItemStack("iron_sword", 20) { Keepsake = "wren" });

        Assert.Contains("carries Iron Sword (Wren's)", MapRenderer.Render(state, Starter));
    }

    [Fact]
    public void RecallRestoresTheStackAndTheCarrier()
    {
        var before = WithStack(Left("wren", "iron_sword", Grave)).Do(new EndPhase());
        var taken = before.Do(new Move("brigand-1", Grave));

        var back = taken.Do(new Recall(0));

        Assert.Single(back.Keepsakes);
        Assert.Single(back.Find("brigand-1")!.Unit.Inventory.Items);
    }

    [Fact]
    public void TheBattlesEndNamesEachKeepsakeNobodyRecovered()
    {
        for (ulong seed = 1; seed < 200; seed++)
        {
            var state = WithStack(Left("teodor", "iron_lance", new Coord(5, 0))) with { Seed = seed };
            state = state.WithUnit(state.Find("hale")! with { Hp = 1, At = Grave });
            state = WithInventory(state, "brigand-1", new ItemStack("iron_axe", 30), new ItemStack("iron_sword", 20) { Keepsake = "wren" }).Do(new EndPhase());
            var result = state.Try(new Attack("brigand-1", "hale"));
            if (!result.Next.Outcome.IsOver)
            {
                continue;
            }

            var lost = result.Events.OfType<KeepsakeLost>().ToArray();
            Assert.Contains(new KeepsakeLost("teodor", "iron_lance", new Coord(5, 0), null), lost);
            Assert.Contains(new KeepsakeLost("wren", "iron_sword", new Coord(3, 1), "brigand-1"), lost);
            Assert.Equal("Iron Lance (Teodor's) was left at 5,0", PlaySession.Describe(lost[0], Starter));
            Assert.Equal("Iron Sword (Wren's) went with brigand-1", PlaySession.Describe(lost.Single(l => l.CarrierId is not null), Starter));
            return;
        }

        throw new InvalidOperationException("no seed below 200 lets the brigand kill hale");
    }

    [Fact]
    public void AKeepsakeTakenReadsByItsDisplayName()
    {
        Assert.Equal("brigand-2 takes Iron Lance (Teodor's)", PlaySession.Describe(new KeepsakeTaken("brigand-2", "teodor", "iron_lance"), Starter));
    }

    [Fact]
    public void ACarrierWithSevenStacksRoundTripsThroughTheProtocolState()
    {
        var full = Enumerable.Repeat(new ItemStack("iron_axe", 30), Inventory.Capacity).ToArray();
        var state = WithInventory(WithStack(Left("wren", "iron_sword", Grave), Left("teodor", "hatchet", Grave)), "brigand-1", full).Do(new EndPhase());
        var carried = state.Do(new Move("brigand-1", Grave)) with { History = ValueList<BattleState>.Empty };

        Assert.Equal(carried, Ironwake.Content.Protocol.ProtocolJson.ReadState(Ironwake.Content.Protocol.ProtocolJson.State(carried, Starter), Starter));
    }
}
