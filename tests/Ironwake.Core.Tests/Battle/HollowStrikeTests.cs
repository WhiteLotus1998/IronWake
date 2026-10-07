using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Light's Hollow strike (issue 1321 slice 2, DECISIONS/0317): a Faith strike carrying <c>hollow</c> in its <c>effective</c>
/// list triples its Mt against a Hollow (<see cref="Core.Combat.IsEffective"/>), the one multiplier a flier takes from a bow, never
/// stacked. Only a Faith strike may carry it. No shipped tome names it, so these tests use a fixture, <c>test_sunlance</c>
/// (Radiance's numbers at rank E), in Mira the chaplain's hands at 2,2, beside a brigand at 3,2.
/// </summary>
public class HollowStrikeTests
{
    private const string Field = """
        name: Field
        size: 6x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ......
        ......
        ......
        ......

        units:
        P captain 0,1
        P recruit 2,2
        E brigand 3,2 group:field behavior:aggressive
        E brigand 5,0 group:far behavior:aggressive

        """;

    private static readonly GameContent Lit = Starter with
    {
        Weapons = Starter.Weapons
            .SetItem("test_sunlance", Starter.Weapon("radiance") with { Id = "test_sunlance", Name = "Test Sunlance", Rank = WeaponRank.E, EffectiveAgainstHollows = true })
            .SetItem("test_plain", Starter.Weapon("radiance") with { Id = "test_plain", Name = "Test Plain", Rank = WeaponRank.E }),
    };

    private static readonly Unit Mira = Recruit("mira", "chaplain", new Stats(16, 1, 6, 4, 4, 3, 1, 5, 3), "salve");

    /// <summary>The field with Mira holding <paramref name="tome"/>, and the brigand beside her a Hollow when <paramref name="hollow"/>.</summary>
    private static BattleState Board(string tome, bool hollow)
    {
        var mira = Mira with { Inventory = Mira.Inventory.Replace(0, new ItemStack(tome, Lit.Weapon(tome).Durability)) };
        var state = BattleState.From(MapFixture.Parse(Field, "test.map"), Lit, ValueList<Unit>.Of(Hale, mira), 1321);
        var brigand = state.UnitAt(new Coord(3, 2))!;
        return hollow ? state.WithUnit(brigand with { Hollow = new HollowMark("caller", "fallen", Hollow.Phases) }) : state;
    }

    private static int Damage(string tome, bool hollow)
    {
        var state = Board(tome, hollow);
        return Queries.Forecast(state, Lit, state.Find("mira")!, state.UnitAt(new Coord(3, 2))!)!.Attacker.Damage;
    }

    /// <summary>Mira's Atk with Radiance's Mt counted <paramref name="times"/> times, less the brigand's Res, floored at zero.</summary>
    private static int Expected(BattleState state, int times)
    {
        var mira = state.Find("mira")!;
        var brigand = state.UnitAt(new Coord(3, 2))!;
        var res = Lit.StatsOf(brigand.Unit).Res + state.Map.TerrainAt(brigand.At, Lit).ResFor(Lit.Class(brigand.Unit.ClassId).Movement);
        return Math.Max(0, Lit.StatsOf(mira.Unit).Mag + Lit.Weapon("radiance").Mt * times - res);
    }

    [Fact]
    public void AHollowStrikeTriplesItsMtAgainstAHollow()
    {
        Assert.Equal(Expected(Board("test_sunlance", true), Core.Combat.EffectiveMultiplier), Damage("test_sunlance", true));
        Assert.True(Damage("test_sunlance", true) > Damage("test_sunlance", false));
    }

    [Fact]
    public void AHollowStrikeIsPlainAgainstALivingUnit()
    {
        Assert.Equal(Expected(Board("test_sunlance", false), 1), Damage("test_sunlance", false));
    }

    [Fact]
    public void ATomeWithoutTheTagIsPlainAgainstAHollow()
    {
        Assert.Equal(Damage("test_plain", false), Damage("test_plain", true));
    }

    [Fact]
    public void AHollowStrikeOffTheBoardSeesNoHollow()
    {
        var state = Board("test_sunlance", true);
        var brigand = state.UnitAt(new Coord(3, 2))!;

        Assert.True(brigand.ToCombatant(state, Lit).Hollow);
        Assert.False(brigand.ToCombatant(state.Map, Lit).Hollow);
    }

    [Fact]
    public void TheResolverDealsTheTripledDamageTheForecastShows()
    {
        var state = Board("test_sunlance", true);
        var forecast = Queries.Forecast(state, Lit, state.Find("mira")!, state.UnitAt(new Coord(3, 2))!)!;
        var before = state.UnitAt(new Coord(3, 2))!;

        var result = Resolver.Apply(state, Lit, new Attack("mira", before.Id));

        Assert.True(result.Accepted, result.Rejection?.Message);
        var hits = result.Events.OfType<CombatFought>().SelectMany(e => e.Strikes).Where(e => e.AttackerId == "mira" && e.Hit).ToList();
        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.True(h.Damage == forecast.Attacker.Damage || h.Damage == forecast.Attacker.CritDamage));
    }

    [Fact]
    public void AHollowStrikeLoadsAndRoundTrips()
    {
        var content = ContentLoader.Parse(WithRadiance("\"effective\": [\"hollow\"]"));
        var again = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.True(content.Weapon("radiance").EffectiveAgainstHollows);
        Assert.Empty(content.Weapon("radiance").EffectiveAgainst);
        Assert.True(again.Weapon("radiance").EffectiveAgainstHollows);
        Assert.All(Starter.Weapons.Values, w => Assert.False(w.EffectiveAgainstHollows));
    }

    [Fact]
    public void AHollowStrikeKeepsItsMovementTypes()
    {
        var content = ContentLoader.Parse(WithRadiance("\"effective\": [\"flying\", \"hollow\"]"));

        Assert.True(content.Weapon("radiance").EffectiveAgainstHollows);
        Assert.True(content.Weapon("radiance").IsEffectiveAgainst(MovementType.Flying));
    }

    [Theory]
    [InlineData("radiance", "\"effective\": [\"hollow\", \"hollow\"]")]
    [InlineData("salve", "\"effective\": [\"hollow\"]")]
    [InlineData("iron_sword", "\"effective\": [\"hollow\"]")]
    public void AHollowTagOffAFaithStrikeOrRepeatedIsRefusedAtLoad(string id, string to)
    {
        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(With(id, to)));

        Assert.Equal(ContentFiles.WeaponsName, error.File);
        Assert.Equal(id, error.Entry);
        Assert.Equal("effective", error.Field);
    }

    [Fact]
    public void TheItemCardSaysAHollowStrikeIsEffectiveAgainstHollows()
    {
        Assert.Contains("Effective against hollows.", ItemCard.Text(Lit, "test_sunlance"));
        Assert.DoesNotContain("hollows", ItemCard.Text(Lit, "test_plain"));
    }

    private static ContentFiles WithRadiance(string to) => With("radiance", to);

    /// <summary>The starter content written out with weapon <paramref name="id"/>'s empty <c>effective</c> list replaced by <paramref name="to"/>.</summary>
    private static ContentFiles With(string id, string to)
    {
        var files = ContentSerializer.Write(Starter);
        var text = files.Weapons.Text;
        var at = text.IndexOf($"\"id\": \"{id}\"", StringComparison.Ordinal);
        var end = text.IndexOf('}', at);
        var entry = System.Text.RegularExpressions.Regex.Replace(text[at..end], "\"effective\": \\[\\s*\\]", to);
        Assert.NotEqual(text[at..end], entry);
        return files with { Weapons = new ContentFile(files.Weapons.Name, text[..at] + entry + text[end..]) };
    }
}
