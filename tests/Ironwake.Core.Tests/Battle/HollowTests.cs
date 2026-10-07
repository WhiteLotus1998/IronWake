using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Dark's raise dead (issue 1284, Lotus's #1247 rulings, DECISIONS/0307 and 0314): a tome naming <c>hollow</c> on a
/// school whose rider is drain is cast through the Item action on a fallen foe's body, and a Hollow stands there on
/// the caster's side at half the fallen unit's HP, for three of its side's phases or until its raiser falls. No shipped
/// tome names it, so these tests give the Adept dark and Pell a fixture tome, <c>test_grave</c>, in slot 1 behind
/// Cinder, on the sample <c>the_tollgate_frost.map</c>, where the woods brigand stands at 6,5.
/// </summary>
public class HollowTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Weapon Cinder = Shipped.Weapon("cinder");

    private const int Uses = 2;

    private static readonly GameContent Raising = Shipped with
    {
        Weapons = Shipped.Weapons.SetItem("test_grave", Cinder with { Id = "test_grave", Name = "Test Grave", School = MagicSchool.Dark, Rider = RiderKind.Hollow, Ignites = false, MinRange = 1, MaxRange = 2 }),
        Classes = Shipped.Classes.SetItem("adept", Shipped.Class("adept") with { Schools = Shipped.Class("adept").Schools.Add(MagicSchool.Dark) }),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private static readonly Coord Woods = new(6, 5);

    private static string BrigandId(BattleState state) => state.Units.Single(u => u.At == Woods).Id;

    /// <summary>
    /// The first seed whose Cinder from Pell at 6,7 kills the woods brigand, left at 1 HP: the board after it with
    /// Pell's action given back, so she may cast in the same test, and the brigand's id.
    /// </summary>
    private static (BattleState State, string Fallen) Killed()
    {
        for (ulong seed = 1; seed < 400; seed++)
        {
            var state = BattleState.From(MapFiles.Load(SamplePath, Raising), Raising, Raising.Cast, seed);
            var pell = state.Find("pell")!;
            var inventory = pell.Unit.Inventory.Replace(0, new ItemStack("cinder", Cinder.Durability));
            inventory = inventory.Count > 1 ? inventory.Replace(1, new ItemStack("test_grave", Uses)) : inventory.Add(new ItemStack("test_grave", Uses));
            state = state.WithUnit(pell with { Unit = pell.Unit with { Inventory = inventory }, At = new Coord(6, 7) });
            var brigand = state.UnitAt(Woods)!;
            state = state.WithUnit(brigand with { Hp = 1 });
            var result = Resolver.Apply(state, Raising, new Attack("pell", brigand.Id, Slot: 0));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (result.Next.Find(brigand.Id) is null && result.Next.Find("pell") is { } living)
            {
                return (result.Next.WithUnit(living with { Moved = false, Acted = false, Canto = null }), brigand.Id);
            }
        }

        throw new InvalidOperationException("no seed under 400 killed the brigand");
    }

    private static ApplyResult Raise(BattleState state, string target) => Resolver.Apply(state, Raising, new UseItem("pell", 1, target));

    private static BattleState Raised()
    {
        var (state, fallen) = Killed();
        var result = Raise(state, fallen);
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result.Next;
    }

    private static BattleState End(BattleState state)
    {
        var result = Resolver.Apply(state, Raising, new EndPhase());
        Assert.True(result.Accepted, result.Rejection?.Message);
        return result.Next;
    }

    [Fact]
    public void HollowIsATomesRiderBorrowedFromDarksDrain()
    {
        Assert.Equal(RiderKind.Drain, Shipped.Riders[MagicSchool.Dark].Kind);
        Assert.Equal("hollow", SchoolRider.Label(RiderKind.Hollow));
        Assert.True(SchoolRider.Borrows(RiderKind.Hollow, RiderKind.Drain));
        Assert.False(SchoolRider.Borrows(RiderKind.Hollow, RiderKind.Burn));
        Assert.True(Hollow.Raises(Raising, Raising.Weapon("test_grave")));
        Assert.False(Hollow.Raises(Raising, Cinder));
        Assert.DoesNotContain(Shipped.Weapons.Values, w => w.Rider == RiderKind.Hollow);
    }

    [Fact]
    public void ASchoolsOwnRiderIsNeverAHollow()
    {
        var rules = """{ "wakeRadius": 4, "schools": { "dark": { "rider": { "kind": "hollow" } } } }""";

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(rules: rules)));

        Assert.Contains("schools.dark", error.Message);
        Assert.Contains("never a school's own rider", error.Message);
    }

    [Fact]
    public void AHollowTomeOnASchoolWhoseRiderIsNotTheDrainIsRefusedAtLoad()
    {
        var files = ContentSerializer.Write(Shipped);
        files = files with { Weapons = new ContentFile(files.Weapons.Name, files.Weapons.Text.Replace("\"school\": \"fire\"", "\"school\": \"fire\", \"rider\": \"hollow\"")) };

        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(files));

        Assert.Equal("cinder", error.Entry);
        Assert.Equal("rider", error.Field);
        Assert.Contains("school's rider is 'burn'", error.Message);
    }

    [Fact]
    public void ADeathLeavesABodyWhereTheUnitFell()
    {
        var (state, fallen) = Killed();

        var body = Assert.Single(state.Bodies);
        Assert.Equal(fallen, body.Id);
        Assert.Equal(Woods, body.At);
        Assert.Equal(Side.Enemy, body.Side);
        Assert.Equal(body, Hollow.BodyAt(state, Woods));
        Assert.Contains("bodies " + fallen + "@6,5", state.Canonical());
    }

    [Fact]
    public void ARaisedBodyStandsAsAHollowOnTheCastersSideAtHalfItsHp()
    {
        var (state, fallen) = Killed();
        var body = state.Bodies.Single();
        var result = Raise(state, fallen);

        Assert.True(result.Accepted, result.Rejection?.Message);
        var max = body.MaxHp(Raising);
        var hollow = result.Next.Find("hollow-" + fallen)!;
        Assert.Equal(Side.Player, hollow.Side);
        Assert.Equal(Woods, hollow.At);
        Assert.Equal((max + 1) / 2, hollow.Hp);
        Assert.Equal(body.Unit.ClassId, hollow.Unit.ClassId);
        Assert.Equal(body.EquippedWeapon(Raising)!.Id, Assert.Single(hollow.Unit.Inventory.Items).ItemId);
        Assert.True(hollow.Moved && hollow.Acted);
        Assert.Null(hollow.Group);
        Assert.Equal(new HollowMark("pell", fallen, Hollow.Phases), hollow.Hollow);
        Assert.Empty(result.Next.Bodies);
        Assert.Equal(
            [new ItemUsed("pell", "test_grave", fallen, Uses - 1), new HollowRaised("pell", "hollow-" + fallen, fallen, Woods, (max + 1) / 2, Hollow.Phases)],
            result.Events.Where(e => e is ItemUsed or HollowRaised).ToList());
        var pell = result.Next.Find("pell")!;
        Assert.True(pell.Moved && pell.Acted && pell.RaiseSpent);
        Assert.Equal(Uses - 1, pell.Unit.Inventory.Items[1].Uses);
        Assert.DoesNotContain(result.Events, e => e is ExpGained);
    }

    [Fact]
    public void ABodyIsNamedByItsTileToo()
    {
        var (state, fallen) = Killed();

        var result = Raise(state, "6,5");

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.NotNull(result.Next.Find("hollow-" + fallen));
    }

    [Fact]
    public void AHollowReadsAsTheBodyItRoseFrom()
    {
        var (state, fallen) = Killed();
        var names = UnitNames.Of(state, Raising);

        Assert.Equal("Hollow " + names[fallen], UnitNames.Of(Raise(state, fallen).Next, Raising)["hollow-" + fallen]);
    }

    [Fact]
    public void AnAllysBodyIsNeverRaised()
    {
        var (state, _) = Killed();
        var teodor = state.Find("teodor")!;
        state = Hollow.LeaveBody(state, teodor with { At = new Coord(6, 6) }).WithoutUnit("teodor");

        var result = Raise(state, "teodor");

        Assert.False(result.Accepted);
        Assert.Contains("its dead stay dead", result.Rejection!.Message);
    }

    [Fact]
    public void ABodyUnderAStandingUnitIsNotRaised()
    {
        var (state, fallen) = Killed();
        var wren = state.Find("wren")!;
        state = state.WithUnit(wren with { At = Woods });

        var result = Raise(state, fallen);

        Assert.False(result.Accepted);
        Assert.Contains("rises only from an empty tile", result.Rejection!.Message);
    }

    [Fact]
    public void ATileWithNoBodyIsRefused()
    {
        var (state, _) = Killed();

        var result = Raise(state, "6,6");

        Assert.False(result.Accepted);
        Assert.Contains("no body", result.Rejection!.Message);
    }

    [Fact]
    public void ABodyOutOfTheTomesRangeIsRefused()
    {
        var (state, fallen) = Killed();
        state = state.WithUnit(state.Find("pell")! with { At = new Coord(6, 9) });

        var result = Raise(state, fallen);

        Assert.False(result.Accepted);
        Assert.Contains("reaches 1-2", result.Rejection!.Message);
    }

    [Fact]
    public void ACasterRaisesOnceAMap()
    {
        var (state, fallen) = Killed();
        state = state.WithUnit(state.Find("pell")! with { RaiseSpent = true });

        var result = Raise(state, fallen);

        Assert.False(result.Accepted);
        Assert.Contains("once a map", result.Rejection!.Message);
    }

    [Fact]
    public void AHollowActsInThreeOfItsSidesPhasesAndCrumblesAsTheThirdEnds()
    {
        var state = Raised();
        var id = state.Units.Single(u => u.Hollow is not null).Id;
        var acted = 0;
        for (var turn = 0; turn < 3; turn++)
        {
            state = End(End(state));
            var hollow = state.Find(id)!;
            Assert.False(hollow.Acted);
            Assert.Equal(2 - turn, hollow.Hollow!.Phases);
            acted++;
        }

        var result = Resolver.Apply(state, Raising, new EndPhase());

        Assert.Equal(3, acted);
        Assert.Contains(new HollowCrumbled(id, RaiserFell: false), result.Events);
        Assert.Null(result.Next.Find(id));
        Assert.DoesNotContain(result.Events, e => e is UnitDied);
        Assert.Empty(result.Next.Bodies);
    }

    [Fact]
    public void AHollowCrumblesTheMomentItsRaiserIsOffTheBoard()
    {
        var state = Raised();
        var id = state.Units.Single(u => u.Hollow is not null).Id;
        var hollow = state.Find(id)!;
        state = state.WithUnit(hollow with { Hollow = hollow.Hollow! with { RaiserId = "gone" } });

        var result = Resolver.Apply(state, Raising, new Wait("wren"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(new HollowCrumbled(id, RaiserFell: true), result.Events);
        Assert.Null(result.Next.Find(id));
    }

    [Fact]
    public void AHollowMovesStrikesAndWaitsAndNothingElse()
    {
        var state = Raised();
        var id = state.Units.Single(u => u.Hollow is not null).Id;
        state = state.WithUnit(state.Find(id)! with { Moved = false, Acted = false });

        var item = Resolver.Apply(state, Raising, new UseItem(id, 0));
        var wait = Resolver.Apply(state, Raising, new Wait(id));

        Assert.False(item.Accepted);
        Assert.Contains("is a Hollow: it moves, strikes and waits", item.Rejection!.Message);
        Assert.True(wait.Accepted, wait.Rejection?.Message);
    }

    [Fact]
    public void AHollowEarnsNothingAndLeavesNoBodyWhenItFalls()
    {
        for (var tries = 0; tries < 400; tries++)
        {
            var state = Raised() with { Seed = (ulong)tries };
            var hollow = state.Units.Single(u => u.Hollow is not null);
            var warden = state.Units.Single(u => u.Side == Side.Enemy && u.At == new Coord(6, 2));
            state = state.WithUnit(hollow with { Moved = false, Acted = false, Hp = 1, At = new Coord(5, 2) });
            var result = Resolver.Apply(state, Raising, new Attack(hollow.Id, warden.Id));
            Assert.True(result.Accepted, result.Rejection?.Message);
            Assert.DoesNotContain(result.Events, e => e is ExpGained g && g.UnitId == hollow.Id);
            Assert.DoesNotContain(result.Events, e => e is RankRaised r && r.UnitId == hollow.Id);
            if (result.Next.Find(hollow.Id) is null)
            {
                Assert.Contains(result.Events, e => e is UnitDied d && d.UnitId == hollow.Id);
                Assert.Empty(result.Next.Bodies);
                return;
            }
        }

        throw new InvalidOperationException("no seed under 400 felled the Hollow");
    }

    [Fact]
    public void TheHollowsRoundTripThroughTheProtocol()
    {
        var (state, fallen) = Killed();
        var raised = Raised();

        Assert.Equal(state.Canonical(), ProtocolJson.ReadState(ProtocolJson.State(state, Raising), Raising).Canonical());
        Assert.Equal(raised.Canonical(), ProtocolJson.ReadState(ProtocolJson.State(raised, Raising), Raising).Canonical());
        Assert.Contains("hollow pell/" + fallen + "/3", raised.Canonical());
        Assert.Contains("raisespent", raised.Canonical());
    }

    [Fact]
    public void AHollowsCardSaysWhoRaisedItAndWhenItCrumbles()
    {
        var state = Raised();
        var hollow = state.Units.Single(u => u.Hollow is not null);

        Assert.Equal(
            "hollow: raised by Pell, crumbles after 3 more player phases or when Pell falls; earns nothing, leaves no body",
            Hollow.CardLine(hollow, UnitNames.Of(state, Raising)));
        Assert.Null(Hollow.CardLine(state.Find("pell")!, UnitNames.Of(state, Raising)));
    }
}
