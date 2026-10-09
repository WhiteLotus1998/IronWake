using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Ottilie's Tally and its art Paid in Full (issue 635 slice 10, DECISIONS/0203): the Iron Bow's
/// numbers bound to Ottilie, paid by her quest 2; the art looses one arrow at +6 Power and -25 Acc
/// that never doubles, and is declared only with the Tally.
/// </summary>
public class TallyTests
{
    private static readonly GameContent Content = MapFixture.Content;

    /// <summary>Harrow Weir with Ottilie two tiles from the first enemy she can stand that far from, carrying <paramref name="items"/>.</summary>
    private static (BattleState State, string Enemy) Board(params string[] items)
    {
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "harrow_weir.map"), Content);
        var state = BattleState.From(map, Content, Content.Cast, 635);
        var ottilie = state.Find("ottilie")!;
        var steps = new[] { new Coord(2, 0), new Coord(-2, 0), new Coord(0, 2), new Coord(0, -2) };
        var (enemy, at) = state.UnitsOf(Side.Enemy)
            .SelectMany(e => steps.Select(d => (e, At: new Coord(e.At.X + d.X, e.At.Y + d.Y))))
            .First(p => map.Contains(p.At) && state.UnitAt(p.At) is null && map.TerrainAt(p.At, Content).IsPassable(MovementType.Infantry));
        var pack = new Inventory(ValueList<ItemStack>.From(items.Select(id => new ItemStack(id, Content.Weapon(id).Durability))));
        return (state.WithUnit(ottilie with { At = at, Unit = ottilie.Unit with { Inventory = pack } }), enemy.Id);
    }

    [Fact]
    public void OttiliesTallyIsTheIronBowsNumbersBoundToHer()
    {
        var tally = Content.Weapon("ottilie_tally");
        var iron = Content.Weapon("iron_bow");

        Assert.Equal((iron.Type, iron.Rank, iron.Mt, iron.Hit, iron.Crit, iron.Wt, iron.MinRange, iron.MaxRange, iron.Durability), (tally.Type, tally.Rank, tally.Mt, tally.Hit, tally.Crit, tally.Wt, tally.MinRange, tally.MaxRange, tally.Durability));
        Assert.Equal("ottilie", tally.BoundTo);
        Assert.Null(tally.Price);
        Assert.Contains("paid_in_full", Content.Cast.Single(u => u.Id == "ottilie").Abilities);
    }

    [Fact]
    public void PaidInFullIsOneHeavyArrowThatNeverDoubles()
    {
        var art = Assert.IsType<CombatArtEffect>(Content.Ability("paid_in_full").Effect);

        Assert.Equal(new CombatArtEffect(WeaponType.Bow, WeaponRank.E, 4, 6, -25, 0, 0, 0) { Single = true, Item = "ottilie_tally", Grit = 2 }, art);
    }

    [Fact]
    public void PaidInFullIsDeclaredWithTheTallyAndOnlyWithIt()
    {
        var (armed, brigand) = Board("ottilie_tally");

        Assert.True(Resolver.Apply(armed, Content, new Attack("ottilie", brigand, null, "paid_in_full")).Accepted);

        var (iron, target) = Board("iron_bow");
        var refused = Resolver.Apply(iron, Content, new Attack("ottilie", target, null, "paid_in_full"));

        Assert.False(refused.Accepted);
        Assert.EndsWith("Paid in Full is declared only with Ottilie's Tally", refused.Rejection!.Message);
    }

    [Fact]
    public void PaidInFullLosesToThePlainAttackOnSomeTargetsAndBeatsItOnOthers()
    {
        var read = SignatureCeiling.Read(Content, Content.Weapon("ottilie_tally"), RollScheme.TwoRollAverage);
        var art = read.Arts.Single(a => a.ArtId == "paid_in_full");

        Assert.InRange(art.LosesTo, 1, art.Targets - 1);
    }
}
