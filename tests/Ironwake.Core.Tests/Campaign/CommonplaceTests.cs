using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Pell's Commonplace and its art Read Ahead (issue 635 slice 7, DECISIONS/0200): Cinder's
/// numbers bound to Pell, never igniting, paid by her quest 2; the art reaches one tile further
/// for 2 less Power and is declared only with the Commonplace.
/// </summary>
public class CommonplaceTests
{
    private static readonly GameContent Content = MapFixture.Content;

    /// <summary>The Tollgate with Pell three tiles west of the forest brigand at 6,5, carrying <paramref name="items"/>.</summary>
    private static (BattleState State, string Brigand) Board(params string[] items)
    {
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map"), Content);
        var state = BattleState.From(map, Content, Content.Cast, 635);
        var brigand = state.Units.Single(u => u.At == new Coord(6, 5));
        var pell = state.Find("pell")!;
        var pack = new Inventory(ValueList<ItemStack>.From(items.Select(id => new ItemStack(id, Content.Weapon(id).Durability))));
        return (state.WithUnit(pell with { At = new Coord(3, 5), Unit = pell.Unit with { Inventory = pack } }), brigand.Id);
    }

    [Fact]
    public void PellsCommonplaceIsCindersNumbersBoundToHerAndNeverCatches()
    {
        var book = Content.Weapon("pell_commonplace");
        var cinder = Content.Weapon("cinder");

        Assert.Equal((cinder.Type, cinder.Rank, cinder.Mt, cinder.Hit, cinder.Crit, cinder.Wt, cinder.MinRange, cinder.MaxRange, cinder.Durability), (book.Type, book.Rank, book.Mt, book.Hit, book.Crit, book.Wt, book.MinRange, book.MaxRange, book.Durability));
        Assert.Equal("pell", book.BoundTo);
        Assert.Null(book.Price);
        Assert.True(cinder.Ignites);
        Assert.False(book.Ignites);
        Assert.Contains("read_ahead", Content.Cast.Single(u => u.Id == "pell").Abilities);
    }

    [Fact]
    public void ReadAheadReachesThreeTilesWithTheCommonplaceAndOnlyWithIt()
    {
        var (armed, brigand) = Board("pell_commonplace");

        Assert.False(Resolver.Apply(armed, Content, new Attack("pell", brigand, null, null)).Accepted);
        Assert.True(Resolver.Apply(armed, Content, new Attack("pell", brigand, null, "read_ahead")).Accepted);

        var (cinder, target) = Board("cinder");
        var refused = Resolver.Apply(cinder, Content, new Attack("pell", target, null, "read_ahead"));

        Assert.False(refused.Accepted);
        Assert.EndsWith("Read Ahead is declared only with Pell's Commonplace", refused.Rejection!.Message);
    }
}
