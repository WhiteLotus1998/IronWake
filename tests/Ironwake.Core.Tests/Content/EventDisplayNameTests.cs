using Ironwake.Cli;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Issue 296: every event line names items, weapons, abilities, classes and terrain by their
/// display names, and a keepsake by <c>Iron Sword (Wren's)</c>, the form <c>show</c> prints.
/// Units keep their ids, the handles a command types.
/// </summary>
public class EventDisplayNameTests
{
    private static GameContent Content => MapFixture.Content;

    private static string Line(GameEvent e) => PlaySession.Describe(e, Content, UnitNames.None);

    [Fact]
    public void AKeepsakeLeftOnItsTileReadsByItsDisplayName()
    {
        Assert.Equal("Iron Sword (Wren's) lies at 10,4", Line(new KeepsakeLeft("wren", "iron_sword", new Coord(10, 4))));
    }

    [Fact]
    public void AKeepsakeRecoveredReadsByItsDisplayName()
    {
        Assert.Equal("Dunstan recovers Iron Sword (Wren's)", Line(new KeepsakeRecovered("dunstan", "wren", "iron_sword")));
    }

    [Fact]
    public void AChestOpenedReadsItsContentsByDisplayName()
    {
        Assert.Equal("Wren opens the chest at 2,1: Steel Sword, Field Dressing", Line(new ChestOpened("wren", new Coord(2, 1), ValueList<string>.Of("steel_sword", "field_dressing"), ValueList<string>.Empty)));
    }

    [Fact]
    public void AChestOpenedPrintsThePackAndTheWagonOneLineEach()
    {
        Assert.Equal(
            "Wren opens the chest at 2,1: Steel Sword\n  To the wagon, kept if the map is won: Iron Bow, Field Dressing",
            Line(new ChestOpened("wren", new Coord(2, 1), ValueList<string>.Of("steel_sword"), ValueList<string>.Of("iron_bow", "field_dressing"))));
        Assert.Equal(
            "Wren opens the chest at 2,1: nothing fits in the pack\n  To the wagon, kept if the map is won: Iron Bow",
            Line(new ChestOpened("wren", new Coord(2, 1), ValueList<string>.Empty, ValueList<string>.Of("iron_bow"))));
    }

    [Fact]
    public void AChestLineSaysWhichTomeTheOpenerCannotWieldInThePackAndTheWagon()
    {
        var shorts = ValueList<WieldShort>.Of(new WieldShort("cinder", "fire school"), new WieldShort("bolt", "Mag 8"));

        Assert.Equal(
            "Wren opens the chest at 2,1: Cinder (cannot wield: fire school), Steel Sword\n  To the wagon, kept if the map is won: Bolt (cannot wield: Mag 8)",
            Line(new ChestOpened("wren", new Coord(2, 1), ValueList<string>.Of("cinder", "steel_sword"), ValueList<string>.Of("bolt"), shorts)));
    }

    [Fact]
    public void AKeepsakeOfSomeoneOutsideTheCastFallsBackToTheId()
    {
        Assert.Equal("Iron Sword (stranger's) lies at 1,1", Line(new KeepsakeLeft("stranger", "iron_sword", new Coord(1, 1))));
    }

    [Fact]
    public void AnItemUsedReadsByItsDisplayName()
    {
        Assert.Equal("Teodor uses Field Dressing (2 left)", Line(new ItemUsed("teodor", "field_dressing", "teodor", 2)));
        Assert.Equal("Teodor uses Field Dressing on wren (1 left)", Line(new ItemUsed("teodor", "field_dressing", "wren", 1)));
    }

    [Fact]
    public void AWeaponEquippedReadsByItsDisplayName()
    {
        Assert.Equal("Captain equips Iron Sword", Line(new WeaponEquipped("captain", "iron_sword")));
    }

    [Fact]
    public void AnArtDeclaredReadsTheArtAndTheWeaponByTheirDisplayNames()
    {
        Assert.Equal("Ansgar declares Move Again with Iron Lance, spending 2 extra uses", Line(new ArtDeclared("ansgar", "move_again", "iron_lance", 2)));
    }

    [Fact]
    public void AnArtTheContentDoesNotKnowFallsBackToTheId()
    {
        Assert.Equal("Captain declares sunder with Iron Sword, spending 2 extra uses", Line(new ArtDeclared("captain", "sunder", "iron_sword", 2)));
    }

    [Fact]
    public void AnArtCostingOneUseSaysOneUseNotUses()
    {
        Assert.Equal("Captain declares Feint with Iron Sword, spending 1 extra use", Line(new ArtDeclared("captain", "feint", "iron_sword", 1)));
    }

    [Fact]
    public void AWeaponBrokeReadsByItsDisplayName()
    {
        Assert.Equal("Wren's Iron Sword breaks", Line(new WeaponBroke("wren", "iron_sword")));
    }

    [Fact]
    public void ASpellSpentReadsByItsDisplayName()
    {
        Assert.Equal("Keziah's Cinder is spent for this battle", Line(new SpellSpent("keziah", "cinder")));
    }

    [Fact]
    public void AMasteryEarnedReadsTheClassAndTheAbilityByTheirDisplayNames()
    {
        Assert.Equal("Hale masters the Cadet class and keeps Axe Sense", Line(new MasteryEarned("hale", "cadet", "axe_sense")));
    }

    [Fact]
    public void ATerrainChangedReadsByItsDisplayName()
    {
        Assert.Equal("  4,1 becomes Road", Line(new TerrainChanged(new Coord(4, 1), "road")));
    }

    [Fact]
    public void AnUnknownItemFallsBackToTheId()
    {
        Assert.Equal("Captain equips relic", Line(new WeaponEquipped("captain", "relic")));
    }
}
