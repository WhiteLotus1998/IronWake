using Ironwake.Cli;
using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The unit card names a signature art's item (issue 969): an art declared only with its own
/// item prints <c>with &lt;item&gt;</c> on the <c>show</c> card, and <c>not carried</c> while the
/// unit's inventory lacks it, so the card never offers what the resolver refuses. Played on the
/// Tollgate, with the shipped cast standing in for each claimant.
/// </summary>
public class SignatureArtCardTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    /// <summary>The Tollgate's first deployed player unit, made into the cast's <paramref name="id"/>, carrying <paramref name="item"/> if given.</summary>
    private static (BattleState State, BattleUnit Unit) As(string id, string? item = null)
    {
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), Shipped);
        var state = BattleState.From(map, Shipped, Shipped.Cast, 969);
        var seat = state.Units.First(u => u.Side == Side.Player);
        var cast = Shipped.Cast.Single(u => u.Id == id);
        var inventory = item is null ? cast.Inventory : cast.Inventory.Add(new ItemStack(item, Shipped.Weapon(item).Durability));
        var unit = seat with { Unit = cast with { Id = seat.Unit.Id, Inventory = inventory } };
        return (state.WithUnit(unit), unit);
    }

    private static string Line(string id, string prefix, string? item = null)
    {
        var (state, unit) = As(id, item);
        return PlaySession.ShowLines(state, Shipped, unit).Single(l => l.StartsWith(prefix, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("pell", "Read Ahead (lore E, cost 2, with Pell's Commonplace, not carried): ")]
    [InlineData("ottilie", "Paid in Full (bow E, cost 4, with Ottilie's Tally, not carried): ")]
    public void ASignatureArtWhoseItemIsNotCarriedSaysSoOnTheCard(string id, string expected) =>
        Assert.Contains(expected, Line(id, "  Techniques: "), StringComparison.Ordinal);

    [Theory]
    [InlineData("pell", "pell_commonplace", "Read Ahead (lore E, cost 2, with Pell's Commonplace): ")]
    [InlineData("ottilie", "ottilie_tally", "Paid in Full (bow E, cost 4, with Ottilie's Tally): ")]
    public void ASignatureArtWhoseItemIsCarriedNamesItWithoutTheWarning(string id, string item, string expected)
    {
        var line = Line(id, "  Techniques: ", item);
        Assert.Contains(expected, line, StringComparison.Ordinal);
        Assert.DoesNotContain("not carried", line, StringComparison.Ordinal);
    }

    [Fact]
    public void AnArtAnyWeaponOfItsTypeDeclaresCarriesNoItemTag() =>
        Assert.Contains("Overcast (lore E, cost 2): ", Line("pell", "  Techniques: "), StringComparison.Ordinal);

    [Fact]
    public void AnItemBoundHealArtSaysOnTheCardWhetherItsItemIsCarried()
    {
        Assert.EndsWith("Their phase ends where they stand; with Maud's Psalter, not carried)", Line("maud", "  Abilities: "), StringComparison.Ordinal);
        Assert.EndsWith("Their phase ends where they stand; with Maud's Psalter)", Line("maud", "  Abilities: ", "maud_psalter"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheCardsNotCarriedMatchesTheResolversRefusal()
    {
        var (_, pell) = As("pell");
        var weapon = pell.EquippedWeapon(Shipped)!;
        var (_, refused) = Resolver.ChooseArt(pell, Shipped, weapon, "read_ahead");
        Assert.NotNull(refused);
        Assert.Contains("declared only with Pell's Commonplace", refused!.Message, StringComparison.Ordinal);
    }
}
