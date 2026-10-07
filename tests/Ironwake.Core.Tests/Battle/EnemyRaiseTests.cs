using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The enemy's raise dead (issue 1286, DECISIONS/0315): an enemy raiser holding a tome naming hollow raises a body of
/// its own side in reach in place of any strike that is not a kill, once a map, and never the company's dead. No
/// shipped class or tome raises yet, so these tests make the woods archer of <c>the_tollgate_frost.map</c> an Adept
/// given dark, holding Cinder and a fixture tome, <c>test_grave</c>, after Pell's Cinder killed the brigand at 6,5.
/// </summary>
public class EnemyRaiseTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Weapon Cinder = Shipped.Weapon("cinder");

    private static readonly GameContent Raising = Shipped with
    {
        Weapons = Shipped.Weapons.SetItem("test_grave", Cinder with { Id = "test_grave", Name = "Test Grave", School = MagicSchool.Dark, Rider = RiderKind.Hollow, Ignites = false, MinRange = 1, MaxRange = 2 }),
        Classes = Shipped.Classes.SetItem("adept", Shipped.Class("adept") with { Schools = Shipped.Class("adept").Schools.Add(MagicSchool.Dark) }),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private static readonly Coord Woods = new(6, 5);

    private static readonly Coord Archer = new(5, 5);

    /// <summary>
    /// The enemy phase after the first seed whose Cinder from Pell at 6,7 kills the woods brigand, left at 1 HP, with
    /// the woods archer made a raiser; the raiser's id and the brigand's.
    /// </summary>
    private static (BattleState State, string Raiser, string Fallen) EnemyPhase()
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = BattleState.From(MapFiles.Load(SamplePath, Raising), Raising, Raising.Cast, seed);
            var pell = state.Find("pell")!;
            var inventory = pell.Unit.Inventory.Replace(0, new ItemStack("cinder", Cinder.Durability));
            state = state.WithUnit(pell with { Unit = pell.Unit with { Inventory = inventory }, At = new Coord(6, 7) });
            var brigand = state.UnitAt(Woods)!;
            state = state.WithUnit(brigand with { Hp = 1 });
            var result = Resolver.Apply(state, Raising, new Attack("pell", brigand.Id, Slot: 0));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (result.Next.Find(brigand.Id) is not null || result.Next.Find("pell") is null)
            {
                continue;
            }

            var ended = Resolver.Apply(result.Next, Raising, new EndPhase());
            Assert.True(ended.Accepted, ended.Rejection?.Message);
            var next = ended.Next;
            var archer = next.UnitAt(Archer)!;
            var raiser = archer with
            {
                Unit = archer.Unit with
                {
                    ClassId = "adept",
                    Stats = archer.Unit.Stats with { Mag = 12 },
                    Inventory = Inventory.Empty.Add(new ItemStack("cinder", Cinder.Durability)).Add(new ItemStack("test_grave", 2)),
                },
            };
            return (next.WithUnit(raiser), archer.Id, brigand.Id);
        }

        throw new InvalidOperationException("no seed under 400 killed the brigand");
    }

    private static IReadOnlyList<Command> Plan(BattleState state, string raiser) =>
        EnemyAi.PlanUnit(state, Raising, state.Find(raiser)!);

    [Fact]
    public void AnEnemyRaiserRaisesItsOwnSidesDeadInPlaceOfAStrikeThatDoesNotKill()
    {
        var (state, raiser, fallen) = EnemyPhase();

        var plan = Plan(state, raiser);

        Assert.Equal(new UseItem(raiser, 1, fallen), plan[^1]);
        var next = state;
        foreach (var command in plan)
        {
            var result = Resolver.Apply(next, Raising, command);
            Assert.True(result.Accepted, result.Rejection?.Message);
            next = result.Next;
        }

        var hollow = next.Find("hollow-" + fallen)!;
        Assert.Equal(Side.Enemy, hollow.Side);
        Assert.Equal(Woods, hollow.At);
        Assert.Equal(new HollowMark(raiser, fallen, Hollow.Phases), hollow.Hollow);
        Assert.True(next.Find(raiser)!.RaiseSpent);
    }

    [Fact]
    public void AnEnemyRaiserMovesToReachABody()
    {
        var (state, raiser, fallen) = EnemyPhase();
        var unit = state.Find(raiser)!;
        state = state.WithUnit(unit with { At = new Coord(2, 5), Behavior = Behavior.Aggressive });

        var plan = Plan(state, raiser);

        Assert.Equal(2, plan.Count);
        var move = Assert.IsType<Move>(plan[0]);
        Assert.InRange(move.To.DistanceTo(Woods), 1, 2);
        Assert.Equal(new UseItem(raiser, 1, fallen), plan[1]);
    }

    [Fact]
    public void AKillBeatsARaise()
    {
        var (state, raiser, _) = EnemyPhase();
        var pell = state.Find("pell")!;
        state = state.WithUnit(pell with { Hp = 1, At = new Coord(5, 6) });

        var plan = Plan(state, raiser);

        Assert.IsType<Attack>(plan[^1]);
    }

    [Fact]
    public void AnEnemyRaiserRaisesOnceAMap()
    {
        var (state, raiser, _) = EnemyPhase();
        state = state.WithUnit(state.Find(raiser)! with { RaiseSpent = true });

        var plan = Plan(state, raiser);

        Assert.DoesNotContain(plan, c => c is UseItem);
    }

    [Fact]
    public void TheCompanysDeadAreNeverRaised()
    {
        var (state, raiser, _) = EnemyPhase();
        var teodor = state.Find("teodor")!;
        state = Hollow.LeaveBody(state with { Bodies = ValueList<BattleUnit>.Empty }, teodor with { At = new Coord(4, 5) }).WithoutUnit("teodor");

        var plan = Plan(state, raiser);
        var refused = Resolver.Apply(state, Raising, new UseItem(raiser, 1, "teodor"));

        Assert.DoesNotContain(plan, c => c is UseItem);
        Assert.False(refused.Accepted);
        Assert.Contains("its dead stay dead", refused.Rejection!.Message);
    }

    [Fact]
    public void AnEnemyWithNoRaisingTomeNeverRaises()
    {
        var (state, raiser, _) = EnemyPhase();
        var unit = state.Find(raiser)!;
        state = state.WithUnit(unit with { Unit = unit.Unit with { Inventory = Inventory.Empty.Add(new ItemStack("cinder", Cinder.Durability)) } });

        var plan = Plan(state, raiser);

        Assert.DoesNotContain(plan, c => c is UseItem);
    }

    [Fact]
    public void TheWholeEnemyPhasePlansTheRaiseLegallyAndItsRaiserFallingCrumblesTheHollow()
    {
        var (state, raiser, fallen) = EnemyPhase();

        var plan = EnemyAi.Plan(state, Raising);

        Assert.Contains(new UseItem(raiser, 1, fallen), plan);
        var next = state;
        foreach (var command in plan)
        {
            var result = Resolver.Apply(next, Raising, command);
            Assert.True(result.Accepted, result.Rejection?.Message);
            next = result.Next;
        }

        Assert.Equal(Side.Player, next.Phase);
        var hollow = next.Find("hollow-" + fallen)!;
        var gone = next.WithoutUnit(raiser);
        var crumbled = Resolver.Apply(gone, Raising, new Wait("wren"));
        Assert.Contains(new HollowCrumbled(hollow.Id, RaiserFell: true), crumbled.Events);
    }
}
