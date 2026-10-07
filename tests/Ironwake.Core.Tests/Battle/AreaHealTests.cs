using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Light's area heal (issue 1321 slice 3, DECISIONS/0317): a healing spell naming <c>areaHeal</c>, cast through the Item
/// action with no target, heals every wounded unit of its caster's side within the radius, the caster too. No shipped
/// staff names it, so these tests use a fixture, <c>test_mend</c> (Salve with healBase 5, radius 2, one use), in Mira the
/// chaplain's hands. Mira moves to 1,2: Hale at 0,1 is 2 away, Wren at 3,3 is 3 away, outside it.
/// </summary>
public class AreaHealTests
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
        P recruit 0,2
        P recruit 3,3
        E brigand 3,1 group:field behavior:aggressive

        """;

    private static readonly GameContent Mending = Starter with
    {
        Weapons = Starter.Weapons.SetItem("test_mend", Starter.Weapon("salve") with { Id = "test_mend", Name = "Test Mend", Durability = 1, HealBase = 5, AreaHeal = 2 }),
    };

    private static readonly Unit Mira = Recruit("mira", "chaplain", new Stats(16, 1, 4, 4, 4, 3, 1, 5, 3), "salve");

    /// <summary>Mira's heal: Mag 4 / 2 + 5 + healBase 5.</summary>
    private const int Heal = 12;

    /// <summary>The field with Mira holding the mend in slot 0 and moved to 1,2, then <paramref name="edit"/> applied.</summary>
    private static BattleState Board(Func<BattleState, BattleState>? edit = null)
    {
        var mira = Mira with { Inventory = Mira.Inventory.Replace(0, new ItemStack("test_mend", 1)) };
        var state = BattleState.From(MapFixture.Parse(Field, "test.map"), Mending, ValueList<Unit>.Of(Hale, mira, Wren), 1321);
        state = Resolver.Apply(state, Mending, new Move("mira", new Coord(1, 2))).Next;
        return edit is null ? state : edit(state);
    }

    private static BattleState Hurt(BattleState state, string id, int hp) => state.WithUnit(state.Find(id)! with { Hp = hp });

    /// <summary>Hale at 14 of 22, Mira at 10 of 16, Wren at 5 of 20 and out of reach.</summary>
    private static BattleState Wounded(BattleState state) => Hurt(Hurt(Hurt(state, "hale", 14), "mira", 10), "wren", 5);

    private static ApplyResult Cast(BattleState state, string? target = null, string? art = null) =>
        Resolver.Apply(state, Mending, new UseItem("mira", 0, target, art));

    [Fact]
    public void AnAreaHealHealsEveryWoundedAllyWithinItsRadiusAndItsCaster()
    {
        var result = Cast(Board(Wounded));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(
            [new ItemUsed("mira", "test_mend", "mira", 0), new UnitHealed("hale", 8, 22), new UnitHealed("mira", 6, 16), new SpellSpent("mira", "test_mend")],
            result.Events.Where(e => e is ItemUsed or UnitHealed or SpellSpent).ToList());
        Assert.Equal((22, 16), (result.Next.Find("hale")!.Hp, result.Next.Find("mira")!.Hp));
    }

    [Fact]
    public void AnAreaHealPassesOverAnAllyOutsideItsRadius()
    {
        var result = Cast(Board(Wounded));

        Assert.Equal(5, result.Next.Find("wren")!.Hp);
        Assert.DoesNotContain(result.Events, e => e is UnitHealed { UnitId: "wren" });
    }

    [Fact]
    public void AnAreaHealHealsEachByTheCastersSingleHeal()
    {
        var result = Cast(Board(s => Hurt(s, "hale", 2)));

        Assert.Equal([new UnitHealed("hale", Heal, 2 + Heal)], result.Events.OfType<UnitHealed>().ToList());
    }

    [Fact]
    public void AnAreaHealIsRefusedWhenNoOneInItsRadiusIsWounded()
    {
        var state = Board(s => Hurt(s, "wren", 5));

        var result = Cast(state);

        Assert.Equal(RejectionReason.NothingToHeal, result.Rejection?.Reason);
        Assert.Equal(state, result.Next);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void AnAreaHealNamesNoTarget()
    {
        Assert.Equal(RejectionReason.NotUsable, Cast(Board(Wounded), "hale").Rejection?.Reason);
    }

    [Fact]
    public void AnAreaHealDeclaresNoArt()
    {
        Assert.Equal(RejectionReason.ArtRefused, Cast(Board(Wounded), art: "anything").Rejection?.Reason);
    }

    [Fact]
    public void AnAreaHealWithNoUsesLeftIsRefused()
    {
        var spent = Board(s => Wounded(s).WithUnit(s.Find("mira")! with { Hp = 10, Unit = s.Find("mira")!.Unit with { Inventory = s.Find("mira")!.Unit.Inventory.Replace(0, new ItemStack("test_mend", 0)) } }));

        Assert.Equal(RejectionReason.NotUsable, Cast(spent).Rejection?.Reason);
    }

    [Fact]
    public void AnAreaHealSpendsAUseAndTheActionAndEarnsOneHealsExpWithTheBelowHalfBonusWhenAnyoneWasUnderHalf()
    {
        var above = Cast(Board(s => Hurt(s, "hale", 14)));
        var below = Cast(Board(s => Hurt(Hurt(s, "hale", 14), "mira", 7)));

        var mira = above.Next.Find("mira")!;
        Assert.True(mira.Moved && mira.Acted);
        Assert.Equal(0, mira.Unit.Inventory.Items[0].Uses);
        Assert.Equal(Experience.ForHeal(false), Assert.Single(above.Events.OfType<ExpGained>()).Amount);
        Assert.Equal(Experience.ForHeal(true), Assert.Single(below.Events.OfType<ExpGained>()).Amount);
    }

    [Fact]
    public void RecallRestoresWhatAnAreaHealMended()
    {
        var healed = Cast(Board(Wounded)).Next;

        var recalled = Resolver.Apply(healed, Mending, new Recall(healed.History.Count - 1));

        Assert.True(recalled.Accepted, recalled.Rejection?.Message);
        Assert.Equal((14, 10, 1), (recalled.Next.Find("hale")!.Hp, recalled.Next.Find("mira")!.Hp, recalled.Next.Find("mira")!.Unit.Inventory.Items[0].Uses));
    }

    [Fact]
    public void LegalOffersAnAreaHealWithNoTargetOnlyWhenSomeoneInItsRadiusIsWounded()
    {
        Assert.Contains(new UseItem("mira", 0), Resolver.Legal(Board(Wounded), Mending));
        Assert.DoesNotContain(Resolver.Legal(Board(Wounded), Mending), c => c is UseItem { UnitId: "mira", TargetId: not null });
        Assert.DoesNotContain(Resolver.Legal(Board(s => Hurt(s, "wren", 5)), Mending), c => c is UseItem { UnitId: "mira" });
    }

    [Fact]
    public void TheSimsPlayerNeverCastsAnAreaHeal()
    {
        var state = Board(s => Hurt(Hurt(s, "hale", 3), "mira", 3));

        var plan = HeuristicPlayer.PlanUnit(state, Mending, state.Find("mira")! with { Moved = false });

        Assert.DoesNotContain(plan, c => c is UseItem);
    }

    [Fact]
    public void ThePreviewListsEveryUnitItWouldHealAndByHowMuch()
    {
        var state = Board(Wounded);

        Assert.Equal("Test Mend heals hale 8 (hp 22), mira 6 (hp 16)", AreaHeal.Preview(state, Mending, state.Find("mira")!, Mending.Weapon("test_mend")));
        Assert.Equal(state, Board(Wounded));
    }

    [Fact]
    public void ThePreviewSaysWhenItWouldHealNoOne()
    {
        var state = Board();

        Assert.Equal("Test Mend would heal no one: no unit within 2 of mira is wounded", AreaHeal.Preview(state, Mending, state.Find("mira")!, Mending.Weapon("test_mend")));
    }

    [Fact]
    public void TheItemCardSaysWhomAnAreaHealReaches()
    {
        Assert.Contains("heals every ally within 2 and its caster", ItemCard.Text(Mending, "test_mend"));
    }

    [Fact]
    public void AnAreaHealLoadsAndRoundTrips()
    {
        var content = ContentLoader.Parse(WithSalve("\"heals\": true, \"healBase\": 0, \"areaHeal\": 2"));
        var again = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.Equal(2, content.Weapon("salve").AreaHeal);
        Assert.Equal(2, again.Weapon("salve").AreaHeal);
        Assert.All(Starter.Weapons.Values, w => Assert.Equal(0, w.AreaHeal));
    }

    [Theory]
    [InlineData("\"heals\": true, \"healBase\": 0, \"areaHeal\": 0")]
    [InlineData("\"heals\": false, \"areaHeal\": 2")]
    [InlineData("\"heals\": true, \"cleanses\": true, \"areaHeal\": 2")]
    public void AnAreaHealThatIsNoHealingSpellOrIsACleanseOrHasNoRadiusIsRefusedAtLoad(string to)
    {
        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(WithSalve(to)));

        Assert.Equal(ContentFiles.WeaponsName, error.File);
        Assert.Equal("salve", error.Entry);
        Assert.Equal("areaHeal", error.Field);
    }

    /// <summary>The starter content written out with Salve's <c>heals</c> and <c>healBase</c> replaced by <paramref name="to"/>.</summary>
    private static ContentFiles WithSalve(string to)
    {
        string edit(string entry) => System.Text.RegularExpressions.Regex.Replace(entry, "\"heals\": true,\\s*\"healBase\": 0", to);
        var files = ContentSerializer.Write(Starter);
        var text = files.Weapons.Text;
        var at = text.IndexOf("\"id\": \"salve\"", StringComparison.Ordinal);
        var end = text.IndexOf('}', at);
        return files with { Weapons = new ContentFile(files.Weapons.Name, text[..at] + edit(text[at..end]) + text[end..]) };
    }
}
