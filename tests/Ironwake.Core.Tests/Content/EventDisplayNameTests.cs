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

    private static string Line(GameEvent e) => PlaySession.Describe(e, Content);

    [Fact]
    public void AKeepsakeLeftOnItsTileReadsByItsDisplayName()
    {
        Assert.Equal("Iron Sword (Wren's) lies at 10,4", Line(new KeepsakeLeft("wren", "iron_sword", new Coord(10, 4))));
    }

    [Fact]
    public void AKeepsakeRecoveredReadsByItsDisplayName()
    {
        Assert.Equal("dunstan recovers Iron Sword (Wren's)", Line(new KeepsakeRecovered("dunstan", "wren", "iron_sword")));
    }

    [Fact]
    public void AKeepsakeOfSomeoneOutsideTheCastFallsBackToTheId()
    {
        Assert.Equal("Iron Sword (stranger's) lies at 1,1", Line(new KeepsakeLeft("stranger", "iron_sword", new Coord(1, 1))));
    }

    [Fact]
    public void AnItemUsedReadsByItsDisplayName()
    {
        Assert.Equal("teodor uses Field Dressing (2 left)", Line(new ItemUsed("teodor", "field_dressing", "teodor", 2)));
        Assert.Equal("teodor uses Field Dressing on wren (1 left)", Line(new ItemUsed("teodor", "field_dressing", "wren", 1)));
    }

    [Fact]
    public void AWeaponEquippedReadsByItsDisplayName()
    {
        Assert.Equal("captain equips Iron Sword", Line(new WeaponEquipped("captain", "iron_sword")));
    }

    [Fact]
    public void AnArtDeclaredReadsTheArtAndTheWeaponByTheirDisplayNames()
    {
        Assert.Equal("ansgar declares Canto with Iron Lance, spending 2 extra uses", Line(new ArtDeclared("ansgar", "canto", "iron_lance", 2)));
    }

    [Fact]
    public void AnArtTheContentDoesNotKnowFallsBackToTheId()
    {
        Assert.Equal("captain declares cleave with Iron Sword, spending 2 extra uses", Line(new ArtDeclared("captain", "cleave", "iron_sword", 2)));
    }

    [Fact]
    public void AWeaponBrokeReadsByItsDisplayName()
    {
        Assert.Equal("wren's Iron Sword breaks", Line(new WeaponBroke("wren", "iron_sword")));
    }

    [Fact]
    public void ASpellSpentReadsByItsDisplayName()
    {
        Assert.Equal("keziah's Cinder is spent for this battle", Line(new SpellSpent("keziah", "cinder")));
    }

    [Fact]
    public void AMasteryEarnedReadsTheClassAndTheAbilityByTheirDisplayNames()
    {
        Assert.Equal("hale masters the Cadet class and keeps Axebreaker", Line(new MasteryEarned("hale", "cadet", "axebreaker")));
    }

    [Fact]
    public void ATerrainChangedReadsByItsDisplayName()
    {
        Assert.Equal("  4,1 becomes Road", Line(new TerrainChanged(new Coord(4, 1), "road")));
    }

    [Fact]
    public void AnUnknownItemFallsBackToTheId()
    {
        Assert.Equal("captain equips relic", Line(new WeaponEquipped("captain", "relic")));
    }
}
