using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The hunt runs on (issue 804 item 4, round 251; DESIGN.md 13.23): once a battle, a kill with
/// the scythe on its carrier's own Attack that leaves it woken, the waking kill included, owes the
/// carrier a Canto of its full Move, with no second strike. Played on the spike's sample,
/// <c>docs/samples/the_gleaning_kinsbane.map</c>, where Keziah carries it.
/// </summary>
public class KinsbaneHuntTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_gleaning_kinsbane.map");

    private static BattleState Placed(ulong seed = 645) =>
        BattleState.From(MapFiles.Load(SamplePath, Shipped), Shipped, Shipped.Cast, seed);

    private static BattleUnit Keziah(BattleState state) => state.Find("keziah")!;

    private static int FullMov(BattleUnit unit) => Shipped.Class(unit.Unit.ClassId).Mov;

    private static BattleState WithScythe(BattleState state, int fed, bool huntRan = false)
    {
        var keziah = Keziah(state);
        var stack = keziah.Unit.Inventory.Items[0] with { Fed = fed };
        return state.WithUnit(keziah with { HuntRan = huntRan, Unit = keziah.Unit with { Inventory = keziah.Unit.Inventory.Replace(0, stack) } });
    }

    /// <summary>Keziah fed <paramref name="fed"/>, moved beside brigand-1 at 1 HP, and the first seed under 64 on which her attack kills it.</summary>
    private static (BattleState Before, ApplyResult Result) Kill(int fed, bool huntRan = false, bool moved = false)
    {
        for (ulong seed = 1; seed < 64; seed++)
        {
            var state = WithScythe(Placed(seed), fed, huntRan);
            var brigand = state.Find("brigand-1")!;
            state = state.WithUnit(brigand with { Hp = 1 }).WithUnit(Keziah(state) with { At = new Coord(brigand.At.X - 1, brigand.At.Y), Moved = moved });

            var result = Resolver.Apply(state, Shipped, new Attack("keziah", "brigand-1"));
            if (result.Events.OfType<UnitDied>().Any())
            {
                return (state, result);
            }
        }

        throw new InvalidOperationException("no seed under 64 lands a hit on a brigand at 1 hp");
    }

    [Fact]
    public void AWokenKillOnHerOwnAttackOwesHerACantoOfHerFullMove()
    {
        var (_, result) = Kill(fed: 12, moved: true);
        var keziah = Keziah(result.Next);

        Assert.Contains(new HuntRanOn("keziah", FullMov(keziah)), result.Events);
        Assert.Equal(FullMov(keziah), keziah.Canto);
        Assert.True(keziah.HuntRan);
        Assert.NotNull(result.Next.CantoReachOf(keziah, Shipped));
    }

    [Fact]
    public void TheWakingKillRunsTheHuntOn()
    {
        var (_, result) = Kill(fed: Kinsbane.WakeKills - 1);

        Assert.Contains(result.Events, e => e is HungerFed { Woke: true });
        Assert.Contains(new HuntRanOn("keziah", FullMov(Keziah(result.Next))), result.Events);
    }

    [Fact]
    public void AKillBeforeTheWakingRunsNothing()
    {
        var (_, result) = Kill(fed: 5);

        Assert.DoesNotContain(result.Events, e => e is HuntRanOn);
        Assert.Null(Keziah(result.Next).Canto);
        Assert.False(Keziah(result.Next).HuntRan);
    }

    [Fact]
    public void TheHuntRunsOnceABattle()
    {
        var (_, result) = Kill(fed: 13, huntRan: true);

        Assert.DoesNotContain(result.Events, e => e is HuntRanOn);
        Assert.Null(Keziah(result.Next).Canto);
        Assert.Null(result.Next.CantoReachOf(Keziah(result.Next), Shipped));
    }

    [Fact]
    public void TheHuntIsAMoveWithNoSecondStrike()
    {
        var (_, result) = Kill(fed: 12);
        var keziah = Keziah(result.Next);
        var other = result.Next.UnitsOf(Side.Enemy).OrderBy(e => Math.Abs(e.At.X - keziah.At.X) + Math.Abs(e.At.Y - keziah.At.Y)).First();

        Assert.False(Resolver.Apply(result.Next, Shipped, new Attack("keziah", other.Id)).Accepted);

        var to = result.Next.CantoReachOf(keziah, Shipped)!.Entries.Where(e => e.CanEnd && e.At != keziah.At).First().At;
        var moved = Resolver.Apply(result.Next, Shipped, new Canto("keziah", to));
        Assert.True(moved.Accepted, moved.Rejection?.Message);
        Assert.Equal(to, Keziah(moved.Next).At);
        Assert.Null(moved.Next.CantoReachOf(Keziah(moved.Next), Shipped));
    }

    [Fact]
    public void ACounterKillOnTheEnemyPhaseRunsNothing()
    {
        var state = WithScythe(Placed(), 12) with { Phase = Side.Enemy };
        var events = new List<GameEvent> { new HungerFed("keziah", Kinsbane.ItemId, 13, 0, 23, 5, false) };

        Assert.Equal(state, Kinsbane.RunsOn(state, Shipped, "keziah", events));
        Assert.Single(events);
    }

    [Fact]
    public void AChilledCarriersHuntIsOneShorter()
    {
        var state = WithScythe(Placed(), 12);
        var keziah = Keziah(state);

        Assert.Equal(FullMov(keziah), Kinsbane.HuntMov(state, Shipped, keziah));
        Assert.Equal(FullMov(keziah) - Frost.MovLost, Kinsbane.HuntMov(state, Shipped, keziah with { Chill = 1 }));
        Assert.Equal(FullMov(keziah) + 1, Kinsbane.HuntMov(state, Shipped, keziah with { Pressed = true }));
    }

    [Fact]
    public void TheForecastPrintsTheHuntWhileTheChargeIsUnspent()
    {
        var state = WithScythe(Placed(), 12);
        var brigand = state.Find("brigand-1")!;
        var mov = FullMov(Keziah(state));

        Assert.Equal(new[] { $"  kill: keziah moves again, {mov} movement (the hunt runs on, once a map)" }, PlaySession.HungerLines(Shipped, Keziah(state), brigand, null, true, state: state));
        Assert.Empty(PlaySession.HungerLines(Shipped, Keziah(state), brigand, null, true));
        Assert.Empty(PlaySession.HungerLines(Shipped, Keziah(WithScythe(Placed(), 12, huntRan: true)), brigand, null, true, state: state));
        Assert.Empty(PlaySession.HungerLines(Shipped, brigand, Keziah(state), null, true, state: state));
    }

    [Fact]
    public void TheForecastOfTheWakingKillPrintsTheFeedAndTheHunt()
    {
        var state = WithScythe(Placed(), Kinsbane.WakeKills - 1);
        var lines = PlaySession.HungerLines(Shipped, Keziah(state), state.Find("brigand-1")!, null, true, state: state).ToList();

        Assert.Equal(2, lines.Count);
        Assert.StartsWith("  kill: keziah +10 HP", lines[0]);
        Assert.StartsWith("  kill: keziah moves again", lines[1]);
    }

    [Fact]
    public void TheCardNamesTheHuntAndThenThatItHasRun()
    {
        Assert.EndsWith("Woken: no drain. A kill: move again (once a map).", Kinsbane.Card(Keziah(WithScythe(Placed(), 12)), Shipped));
        Assert.EndsWith("Woken: no drain. The hunt has run this map.", Kinsbane.Card(Keziah(WithScythe(Placed(), 12, huntRan: true)), Shipped));
    }

    [Fact]
    public void TheProtocolCarriesTheSpentHunt()
    {
        var state = WithScythe(Placed(), 12, huntRan: true);

        var back = ProtocolJson.ReadState(ProtocolJson.State(state, Shipped), Shipped);

        Assert.True(Keziah(back).HuntRan);
        Assert.Equal(Keziah(state), Keziah(back));
    }

    [Fact]
    public void RecallRestoresTheCharge()
    {
        var (before, result) = Kill(fed: 12);
        var back = Resolver.Apply(result.Next, Shipped, new Recall(0)).Next;

        Assert.False(Keziah(back).HuntRan);
        Assert.Equal(Keziah(before).HuntRan, Keziah(back).HuntRan);
    }

    [Fact]
    public void WokenNamingTheKinsbaneBearerIssuesTheScytheAlreadyWoken()
    {
        var path = Path.Combine(Path.GetDirectoryName(SamplePath)!, "the_gleaning_kinsbane_woken.map");
        var map = MapFiles.Load(path, Shipped);
        var keziah = BattleState.From(map, Shipped, Shipped.Cast, 804).Find("keziah")!;

        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), MapFormat.Write(map, Shipped));
        Assert.Equal(new ItemStack(Kinsbane.ItemId, Shipped.Weapon(Kinsbane.ItemId).Durability) { Fed = Kinsbane.WakeKills }, keziah.Unit.Inventory.Items[0]);
        Assert.Equal(1, keziah.Unit.Inventory.Items.Count(s => s.ItemId == Kinsbane.ItemId));
        Assert.Equal(0, Keziah(Placed()).Unit.Inventory.Items[0].Fed);
    }
}
