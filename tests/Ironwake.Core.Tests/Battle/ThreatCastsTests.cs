using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// <c>threat</c> reads the enemy's casts (issue 1286, DECISIONS/0316 amended): a raiser or an earth-shaper casts in
/// place of any strike that is not a kill, so a line whose strike does not kill names the cast it may make instead and
/// stays counted; a line that kills names none. No shipped class or tome casts on the board, so these tests make the
/// woods archer of <c>the_tollgate_frost.map</c> an Adept given dark and earth, holding Cinder and a fixture tome,
/// and read <c>threat</c> on Pell at 5,7, two tiles from it.
/// </summary>
public class ThreatCastsTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static readonly Weapon Cinder = Shipped.Weapon("cinder");

    private static readonly GameContent Casting = Shipped with
    {
        Weapons = Shipped.Weapons
            .SetItem("test_grave", Cinder with { Id = "test_grave", Name = "Test Grave", School = MagicSchool.Dark, Rider = RiderKind.Hollow, Ignites = false, MinRange = 1, MaxRange = 2 })
            .SetItem("test_rampart", Cinder with { Id = "test_rampart", Name = "Test Rampart", School = MagicSchool.Earth, Rider = RiderKind.Raise, Ignites = false, MinRange = 1, MaxRange = 2 }),
        Classes = Shipped.Classes.SetItem("adept", Shipped.Class("adept") with { Schools = Shipped.Class("adept").Schools.Add(MagicSchool.Dark).Add(MagicSchool.Earth) }),
    };

    private static readonly string SamplePath = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "the_tollgate_frost.map");

    private static readonly Coord Archer = new(5, 5);

    private static readonly Coord Stop = new(5, 7);

    /// <summary>The player phase with Pell on 5,7 at <paramref name="hp"/> and the woods archer an Adept holding Cinder and <paramref name="tome"/>; the caster's id.</summary>
    private static (BattleState State, string Caster) Board(string? tome, int? hp = null)
    {
        var state = BattleState.From(MapFiles.Load(SamplePath, Casting), Casting, Casting.Cast, 1286);
        var pell = state.Find("pell")!;
        state = state.WithUnit(pell with { At = Stop, Hp = hp ?? pell.Hp });
        var archer = state.UnitAt(Archer)!;
        var inventory = Inventory.Empty.Add(new ItemStack("cinder", Cinder.Durability));
        var caster = archer with
        {
            Unit = archer.Unit with
            {
                ClassId = "adept",
                Stats = archer.Unit.Stats with { Mag = 12 },
                Inventory = tome is null ? inventory : inventory.Add(new ItemStack(tome, 2)),
            },
        };
        return (state.WithUnit(caster), archer.Id);
    }

    private static ThreatLine CasterLine(BattleState state, string caster)
    {
        var pell = state.Find("pell")!;
        return Queries.Threats(state, Casting, pell, pell.At)!.Single(l => l.Enemy.Id == caster);
    }

    [Fact]
    public void AnEarthShapersStrikeThatDoesNotKillNamesTheRampartItMayLayInstead()
    {
        var (state, caster) = Board("test_rampart");

        var line = CasterLine(state, caster);

        Assert.Equal(CastKind.Rampart, line.Casts);
        Assert.True(line.IfAllLand > 0);
    }

    [Fact]
    public void ARaisersStrikeThatDoesNotKillNamesTheRaiseItMayMakeInstead()
    {
        var (state, caster) = Board("test_grave");

        Assert.Equal(CastKind.Raise, CasterLine(state, caster).Casts);
    }

    [Fact]
    public void ARaiserThatHasRaisedThisMapNamesNoCast()
    {
        var (state, caster) = Board("test_grave");
        state = state.WithUnit(state.Find(caster)! with { RaiseSpent = true });

        Assert.Null(CasterLine(state, caster).Casts);
    }

    [Fact]
    public void ACastersStrikeThatKillsNamesNoCast()
    {
        var (state, caster) = Board("test_rampart", hp: 1);

        var line = CasterLine(state, caster);

        Assert.True(line.IfAllLand >= 1);
        Assert.Null(line.Casts);
    }

    [Fact]
    public void AStrikerWithNoCastingTomeNamesNoCast()
    {
        var (state, caster) = Board(null);

        Assert.Null(CasterLine(state, caster).Casts);
    }

    [Fact]
    public void ALineThatNamesACastStaysInTheTotal()
    {
        var (state, caster) = Board("test_rampart");
        var (plain, _) = Board(null);
        var pell = state.Find("pell")!;

        var cast = Queries.Threats(state, Casting, pell, pell.At)!;
        var strike = Queries.Threats(plain, Casting, pell, pell.At)!;

        Assert.Equal(Queries.IfAllLand(strike), Queries.IfAllLand(cast));
        Assert.Contains(cast, l => l.Casts is not null);
    }

    [Fact]
    public void ThreatPrintsTheCastRowUnderTheLine()
    {
        var (state, caster) = Board("test_rampart");
        var pell = state.Find("pell")!;
        var lines = Queries.Threats(state, Casting, pell, pell.At)!;

        var text = Ironwake.Cli.PlaySession.ThreatText(state, Casting, pell, pell.At, lines, Queries.SleepingThreats(state, Casting, pell, pell.At)!, Queries.Unseeing(state, Casting, pell, pell.At));

        Assert.Contains("    May lay Rampart instead: it casts in place of any strike that does not kill (still in the total)", text);
    }

    [Fact]
    public void ThreatPrintsNoCastRowUnderAKill()
    {
        var (state, _) = Board("test_rampart", hp: 1);
        var pell = state.Find("pell")!;
        var lines = Queries.Threats(state, Casting, pell, pell.At)!;

        var text = Ironwake.Cli.PlaySession.ThreatText(state, Casting, pell, pell.At, lines, Queries.SleepingThreats(state, Casting, pell, pell.At)!, Queries.Unseeing(state, Casting, pell, pell.At));

        Assert.DoesNotContain("Rampart instead", text);
    }
}
