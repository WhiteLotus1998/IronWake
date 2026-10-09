using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Maud's signature item and its heal art (issue 635, rounds 260 and 261): Maud's Psalter is a
/// faith heal with Beacon's numbers at rank D, and Unasked, declared with it, heals an ally who has
/// neither moved, acted nor been shoved for double the plain cast, capped at the ally's max HP; the
/// ally's phase then ends where it stands, a Wait in place that braces where brace applies. Played
/// on the shipped Tollgate (no brace) and Saltmarsh Ford (<c>brace: on</c>).
/// </summary>
public class UnaskedTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static BattleState Placed(string map) =>
        BattleState.From(MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", map + ".map"), Shipped), Shipped, Shipped.Cast, 635);

    /// <summary>
    /// The first player unit made Maud's healer (a chaplain at Faith D who knows Unasked, carrying
    /// <paramref name="items"/>) one tile beside the second, which is wounded by <paramref name="wound"/>.
    /// </summary>
    private static BattleState Setup(out string healer, out string ally, int wound = 12, string map = "the_tollgate", params string[] items)
    {
        var state = Placed(map);
        var first = state.UnitsOf(Side.Player).ElementAt(0);
        var second = state.UnitsOf(Side.Player).ElementAt(1);
        var stacks = (items.Length == 0 ? new[] { "maud_psalter" } : items)
            .Select(i => new ItemStack(i, Shipped.Weapons.TryGetValue(i, out var w) ? w.Durability : Shipped.Item(i).Uses));
        var unit = first.Unit with
        {
            ClassId = "chaplain",
            Skill = WeaponSkill.Zero.With(WeaponType.Faith, 30),
            Abilities = ValueList<string>.Of("unasked"),
            Inventory = new Inventory(ValueList<ItemStack>.From(stacks)),
        };
        var tile = new[] { new Coord(1, 0), new Coord(-1, 0), new Coord(0, 1), new Coord(0, -1) }
            .Select(d => new Coord(second.At.X + d.X, second.At.Y + d.Y))
            .First(c => c.X >= 0 && c.Y >= 0 && state.Units.All(u => u.At != c));
        state = state.WithUnit(first with { Unit = unit, At = tile, Hp = Shipped.StatsOf(unit).Hp });
        state = state.WithUnit(second with { Hp = second.MaxHp(Shipped) - wound });
        healer = first.Id;
        ally = second.Id;
        return state;
    }

    private static int PlainHeal(BattleState state, string healer) =>
        Ironwake.Core.Combat.Heal(state.Find(healer)!.ToCombatant(state.Map, Shipped), Shipped.Weapon("maud_psalter"));

    [Fact]
    public void UnaskedHealsDoubleThePlainCast()
    {
        var state = Setup(out var healer, out var ally, wound: 40);
        var before = state.Find(ally)!.Hp;
        var plain = PlainHeal(state, healer);
        var max = state.Find(ally)!.MaxHp(Shipped);

        var result = Resolver.Apply(state, Shipped, new UseItem(healer, 0, ally, "unasked"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(Math.Min(max, before + 2 * plain), result.Next.Find(ally)!.Hp);
        Assert.True(2 * plain > max - before || result.Next.Find(ally)!.Hp == before + 2 * plain);
    }

    [Fact]
    public void UnaskedIsCappedAtTheAllysMaxHp()
    {
        var state = Setup(out var healer, out var ally, wound: 1);

        var result = Resolver.Apply(state, Shipped, new UseItem(healer, 0, ally, "unasked"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(state.Find(ally)!.MaxHp(Shipped), result.Next.Find(ally)!.Hp);
    }

    [Fact]
    public void UnaskedSpendsOneUseAsThePlainCastDoes()
    {
        var state = Setup(out var healer, out var ally);

        var result = Resolver.Apply(state, Shipped, new UseItem(healer, 0, ally, "unasked"));

        Assert.Equal(Shipped.Weapon("maud_psalter").Durability - 1, result.Next.Find(healer)!.Unit.Inventory.Items[0].Uses);
        Assert.Contains(result.Events, e => e is FormDeclared { ArtId: "unasked", ItemId: "maud_psalter", Cost: 0 });
    }

    [Fact]
    public void UnaskedEndsTheAllysPhaseOnItsOwnTile()
    {
        var state = Setup(out var healer, out var ally);
        var at = state.Find(ally)!.At;

        var result = Resolver.Apply(state, Shipped, new UseItem(healer, 0, ally, "unasked"));

        var held = result.Next.Find(ally)!;
        Assert.Equal(at, held.At);
        Assert.True(held.Moved && held.Acted);
        Assert.False(held.Braced);
        Assert.Contains(result.Events, e => e is UnitWaited { Braced: false } w && w.UnitId == ally);
        Assert.False(Resolver.Apply(result.Next, Shipped, new Wait(ally)).Accepted);
    }

    [Fact]
    public void AnAllyHeldByUnaskedBracesOnABraceMap()
    {
        var state = Setup(out var healer, out var ally, map: "saltmarsh_ford");

        var result = Resolver.Apply(state, Shipped, new UseItem(healer, 0, ally, "unasked"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.True(result.Next.Find(ally)!.Braced);
        Assert.Contains(result.Events, e => e is UnitWaited { Braced: true } w && w.UnitId == ally);
    }

    [Fact]
    public void APlainCastLeavesTheAllyFreeToAct()
    {
        var state = Setup(out var healer, out var ally);

        var result = Resolver.Apply(state, Shipped, new UseItem(healer, 0, ally));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.False(result.Next.Find(ally)!.Moved || result.Next.Find(ally)!.Acted);
        Assert.True(Resolver.Apply(result.Next, Shipped, new Wait(ally)).Accepted);
    }

    [Theory]
    [InlineData("moved")]
    [InlineData("acted")]
    [InlineData("shoved")]
    public void UnaskedIsRefusedOnAnAllyWhoHasMovedActedOrBeenShoved(string done)
    {
        var state = Setup(out var healer, out var ally);
        var unit = state.Find(ally)!;
        state = state.WithUnit(done switch
        {
            "moved" => unit with { Moved = true },
            "acted" => unit with { Acted = true },
            _ => unit with { Shoved = true },
        });

        var result = Resolver.Apply(state, Shipped, new UseItem(healer, 0, ally, "unasked"));

        Assert.Equal(RejectionReason.ArtRefused, result.Rejection?.Reason);
        Assert.Contains("needs an ally who has done neither", result.Rejection!.Message);
    }

    [Fact]
    public void UnaskedIsDeclaredOnlyWithMaudsPsalter()
    {
        var state = Setup(out var healer, out var ally, 12, "the_tollgate", "salve");

        var result = Resolver.Apply(state, Shipped, new UseItem(healer, 0, ally, "unasked"));

        Assert.Equal(RejectionReason.ArtRefused, result.Rejection?.Reason);
        Assert.Contains("declared only with Maud's Psalter", result.Rejection!.Message);
    }

    [Fact]
    public void UnaskedIsRefusedToAHealerWhoDoesNotKnowIt()
    {
        var state = Setup(out var healer, out var ally);
        var unit = state.Find(healer)!;
        state = state.WithUnit(unit with { Unit = unit.Unit with { Abilities = ValueList<string>.Empty } });

        var result = Resolver.Apply(state, Shipped, new UseItem(healer, 0, ally, "unasked"));

        Assert.Equal(RejectionReason.NoSuchArt, result.Rejection?.Reason);
    }

    [Fact]
    public void UnaskedIsRefusedBelowItsRank()
    {
        var state = Setup(out var healer, out var ally);
        var unit = state.Find(healer)!;
        state = state.WithUnit(unit with { Unit = unit.Unit with { Skill = WeaponSkill.Zero } });

        var result = Resolver.Apply(state, Shipped, new UseItem(healer, 0, ally, "unasked"));

        Assert.False(result.Accepted);
    }

    [Fact]
    public void NoArtIsDeclaredWithAConsumable()
    {
        var state = Setup(out var healer, out _, 12, "the_tollgate", "field_dressing");
        var unit = state.Find(healer)!;
        state = state.WithUnit(unit with { Hp = unit.Hp - 5 });

        var result = Resolver.Apply(state, Shipped, new UseItem(healer, 0, null, "unasked"));

        Assert.Equal(RejectionReason.ArtRefused, result.Rejection?.Reason);
    }

    [Fact]
    public void UnaskedIsNotATechniqueAnAttackCanDeclare()
    {
        var state = Setup(out var healer, out _);
        var enemy = state.UnitsOf(Side.Enemy).First();

        var result = Resolver.Apply(state, Shipped, new Attack(healer, enemy.Id, null, "unasked"));

        Assert.False(result.Accepted);
    }

    [Fact]
    public void MaudKnowsUnaskedAndHerPsalterIsBoundToHer()
    {
        var maud = Shipped.Cast.Single(u => u.Id == "maud");
        var psalter = Shipped.Weapon("maud_psalter");

        Assert.Contains("unasked", maud.Abilities);
        Assert.Equal("maud", psalter.BoundTo);
        Assert.Null(psalter.Price);
        Assert.Equal((WeaponRank.D, 1, 2, 4), (psalter.Rank, psalter.MinRange, psalter.MaxRange, psalter.Durability));
        Assert.True(maud.Skill.Rank(WeaponType.Faith) >= psalter.Rank);
        var art = (HealArtEffect)Shipped.Ability("unasked").Effect;
        Assert.Equal((WeaponType.Faith, WeaponRank.D, 2, "maud_psalter"), (art.Weapon, art.Rank, art.Factor, art.Item));
    }

    [Fact]
    public void BeaconIsStockedFromTheCampAfterMapFive()
    {
        var maps = Shipped.Campaign.Maps;
        var first = maps.Select((m, i) => (m, i)).First(x => x.m.Stock.Contains("beacon")).i;

        Assert.Equal("ironwake_raid", maps[first].MapId);
        Assert.Equal(5, first);
        Assert.All(maps.Skip(first), m => Assert.Contains("beacon", m.Stock));
    }

    [Fact]
    public void AHealArtRoundTripsThroughTheSerializer()
    {
        var reloaded = ContentLoader.Parse(ContentSerializer.Write(Shipped));

        Assert.Equal(Shipped.Ability("unasked").Effect, reloaded.Ability("unasked").Effect);
    }

    public static TheoryData<string, string> Malformed => new()
    {
        { "factor", "must be at least 2" },
        { "item", "'salve_x' must be a healing faith spell" },
        { "strike", "'radiance' must be a healing faith spell" },
    };

    [Theory]
    [MemberData(nameof(Malformed))]
    public void TheLoaderRefusesAMalformedHealArt(string change, string problem)
    {
        var art = (HealArtEffect)Shipped.Ability("unasked").Effect;
        var broken = change switch
        {
            "factor" => art with { Factor = 1 },
            "item" => art with { Item = "salve_x" },
            _ => art with { Item = "radiance" },
        };
        var content = Shipped with { Abilities = Shipped.Abilities.SetItem("unasked", Shipped.Ability("unasked") with { Effect = broken }) };

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(ContentSerializer.Write(content)));

        Assert.Equal((ContentFiles.AbilitiesName, "unasked"), (e.File, e.Entry));
        Assert.Contains(problem, e.Message);
    }
}
