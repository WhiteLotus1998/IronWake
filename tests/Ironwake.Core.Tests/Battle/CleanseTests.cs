using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Maps;
using Ironwake.Sim;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Light's cleanse (issue 1321, DECISIONS/0317): a healing spell naming <c>cleanses</c>, cast through the Item action on
/// an ally in range, clears its burn, chill (and any lock) and stun and heals nothing; it is refused on an ally carrying
/// none of the three. No shipped staff names it, so these tests use a fixture, <c>test_cleanse</c> (Salve's numbers,
/// range 1, 8 uses), in Mira the chaplain's hands. Mira moves to 2,3, beside Wren at 3,3.
/// </summary>
public class CleanseTests
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

    private const int Uses = 8;

    private static readonly GameContent Cleansing = Starter with
    {
        Weapons = Starter.Weapons.SetItem("test_cleanse", Starter.Weapon("salve") with { Id = "test_cleanse", Name = "Test Cleanse", Durability = Uses, Cleanses = true }),
    };

    private static readonly Unit Mira = Recruit("mira", "chaplain", new Stats(16, 1, 4, 4, 4, 3, 1, 5, 3), "salve");

    /// <summary>The field with Mira holding the cleanse in slot 0, moved beside Wren, and Wren as <paramref name="wren"/> leaves her.</summary>
    private static BattleState Board(Func<BattleUnit, BattleUnit>? wren = null)
    {
        var mira = Mira with { Inventory = Mira.Inventory.Replace(0, new ItemStack("test_cleanse", Uses)) };
        var state = BattleState.From(MapFixture.Parse(Field, "test.map"), Cleansing, ValueList<Unit>.Of(Hale, mira, Wren), 1321);
        state = Resolver.Apply(state, Cleansing, new Move("mira", new Coord(2, 3))).Next;
        return wren is null ? state : state.WithUnit(wren(state.Find("wren")!));
    }

    private static BattleUnit Afflicted(BattleUnit unit) =>
        unit with { Hp = 9, Burn = 2, BurnStacks = 2, BurnPhases = 2, Chill = 1, Stun = 1 };

    private static ApplyResult Cast(BattleState state, string target = "wren", string? art = null) =>
        Resolver.Apply(state, Cleansing, new UseItem("mira", 0, target, art));

    [Fact]
    public void ACleanseClearsBurnChillAndStunAndHealsNothing()
    {
        var result = Cast(Board(Afflicted));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Equal(
            [new ItemUsed("mira", "test_cleanse", "wren", Uses - 1), new UnitCleansed("wren", "mira", true, true, true, false)],
            result.Events.Where(e => e is ItemUsed or UnitCleansed).ToList());
        Assert.DoesNotContain(result.Events, e => e is UnitHealed);
        var wren = result.Next.Find("wren")!;
        Assert.Equal(9, wren.Hp);
        Assert.Equal((0, 0, 0, 0, 0), (wren.Burn, wren.BurnStacks, wren.BurnPhases, wren.Chill, wren.Stun));
        Assert.False(Cleanse.Afflicted(wren));
    }

    [Fact]
    public void ACleanseSpendsAUseAndTheHealersActionAndEarnsAHealsExp()
    {
        var result = Cast(Board(Afflicted));

        var mira = result.Next.Find("mira")!;
        Assert.True(mira.Moved && mira.Acted);
        Assert.Equal(Uses - 1, mira.Unit.Inventory.Items[0].Uses);
        Assert.Contains(result.Events, e => e is ExpGained { UnitId: "mira" });
    }

    [Fact]
    public void ACleanseClearsOnlyWhatTheAllyCarries()
    {
        var result = Cast(Board(w => w with { Chill = 2 }));

        Assert.Contains(new UnitCleansed("wren", "mira", false, true, false, false), result.Events);
        Assert.Equal(0, result.Next.Find("wren")!.Chill);
    }

    [Fact]
    public void ACleanseIsRefusedOnAnAllyCarryingNoBurnChillOrStunEvenWhenWounded()
    {
        var state = Board(w => w with { Hp = 5 });

        var result = Cast(state);

        Assert.Equal(RejectionReason.NothingToCleanse, result.Rejection?.Reason);
        Assert.Equal(state, result.Next);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void ACleanseDeclaresNoArt()
    {
        Assert.Equal(RejectionReason.ArtRefused, Cast(Board(Afflicted), art: "anything").Rejection?.Reason);
    }

    [Fact]
    public void ACleanseIsRefusedOnAFoeAndOutOfRange()
    {
        Assert.Equal(RejectionReason.NotAnAlly, Cast(Board(Afflicted), "brigand-1").Rejection?.Reason);
        Assert.Equal(RejectionReason.OutOfRange, Cast(Board(Afflicted), "hale").Rejection?.Reason);
    }

    [Fact]
    public void ClearingTheChillDropsALockWithIt()
    {
        var result = Cast(Board(w => w with { Chill = 1, LockedBy = "brigand-1" }));

        var wren = result.Next.Find("wren")!;
        Assert.Null(wren.LockedBy);
        Assert.False(Lock.Holds(result.Next, wren));
    }

    [Fact]
    public void AnAllySkippingThisPhaseToAStunIsFreedToMoveAndAct()
    {
        var result = Cast(Board(w => w with { Stun = 2, Moved = true, Acted = true }));

        Assert.Contains(new UnitCleansed("wren", "mira", false, false, true, true), result.Events);
        var wren = result.Next.Find("wren")!;
        Assert.False(wren.Moved || wren.Acted);
        Assert.True(Resolver.Apply(result.Next, Cleansing, new Move("wren", new Coord(4, 3))).Accepted);
    }

    [Fact]
    public void ARestingAllySkippingToAStunIsNotFreed()
    {
        var result = Cast(Board(w => w with { Stun = 2, Spent = 2, Moved = true, Acted = true }));

        Assert.Contains(new UnitCleansed("wren", "mira", false, false, true, false), result.Events);
        var wren = result.Next.Find("wren")!;
        Assert.True(wren.Moved && wren.Acted);
        Assert.Equal(0, wren.Stun);
    }

    [Fact]
    public void RecallRestoresWhatACleanseCleared()
    {
        var before = Board(Afflicted);
        var cleansed = Cast(before).Next;

        var recalled = Resolver.Apply(cleansed, Cleansing, new Recall(cleansed.History.Count - 1));

        Assert.True(recalled.Accepted, recalled.Rejection?.Message);
        var wren = recalled.Next.Find("wren")!;
        Assert.Equal((2, 2, 2, 1, 1), (wren.Burn, wren.BurnStacks, wren.BurnPhases, wren.Chill, wren.Stun));
    }

    [Fact]
    public void LegalOffersACleanseOnAnAfflictedAllyAndNeverOnAMerelyWoundedOne()
    {
        Assert.Contains(new UseItem("mira", 0, "wren"), Resolver.Legal(Board(Afflicted), Cleansing));
        Assert.DoesNotContain(new UseItem("mira", 0, "wren"), Resolver.Legal(Board(w => w with { Hp = 5 }), Cleansing));
    }

    [Fact]
    public void TheSimsPlayerNeverCastsACleanse()
    {
        var plan = HeuristicPlayer.PlanUnit(Board(w => Afflicted(w) with { Hp = 4 }), Cleansing, Board().Find("mira")! with { Moved = false });

        Assert.DoesNotContain(plan, c => c is UseItem);
    }

    [Fact]
    public void ACleanseLoadsAndRoundTrips()
    {
        var content = ContentLoader.Parse(WithSalve("\"heals\": true, \"cleanses\": true"));
        var again = ContentLoader.Parse(ContentSerializer.Write(content));

        Assert.True(content.Weapon("salve").Cleanses);
        Assert.True(again.Weapon("salve").Cleanses);
        Assert.All(Starter.Weapons.Values, w => Assert.False(w.Cleanses));
    }

    [Theory]
    [InlineData("\"heals\": true, \"healBase\": 0, \"cleanses\": true")]
    [InlineData("\"heals\": false, \"cleanses\": true")]
    public void ACleanseThatHealsOrIsNoHealingSpellIsRefusedAtLoad(string to)
    {
        var error = Assert.Throws<ContentException>(() => ContentLoader.Parse(WithSalve(to)));

        Assert.Equal(ContentFiles.WeaponsName, error.File);
        Assert.Equal("salve", error.Entry);
        Assert.Equal("cleanses", error.Field);
    }

    [Fact]
    public void TheProtocolCarriesTheCleanse()
    {
        Assert.Equal(
            """{"type":"unitCleansed","unit":"wren","by":"mira","burn":true,"chill":false,"stun":true,"freed":true}""",
            ProtocolJson.Event(new UnitCleansed("wren", "mira", true, false, true, true)));
    }

    [Fact]
    public void TheItemCardSaysWhatACleanseClears()
    {
        Assert.Contains("cleanses burn, chill, stun, curse and freeze", ItemCard.Text(Cleansing, "test_cleanse"));
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
