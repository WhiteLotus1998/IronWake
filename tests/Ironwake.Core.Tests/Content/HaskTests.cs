using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Battle;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Hask replaces the finale's stand-in lord (issue 806, DECISIONS/0219): the same numbers as the
/// stand-in, a card line and a weapon description that name the frozen-iron shard in his pommel
/// with no mechanic riding on it, and text that names him as a person, never as a kind.
/// </summary>
public class HaskTests
{
    private static readonly GameContent Shipped = ContentLoader.Load(Fixture.RealContentDirectory());

    private static string UnitWith(string tail) =>
        "{ \"units\": [ { \"id\": \"u\", \"name\": \"U\", \"class\": \"cadet\", \"level\": 1,"
        + " \"stats\": { \"hp\": 20, \"str\": 6, \"mag\": 1, \"dex\": 5, \"spd\": 6, \"lck\": 3, \"def\": 4, \"res\": 2, \"cha\": 5 },"
        + " \"growths\": { \"hp\": 50, \"str\": 40, \"mag\": 10, \"dex\": 40, \"spd\": 45, \"lck\": 30, \"def\": 30, \"res\": 20, \"cha\": 35 },"
        + " \"inventory\": [ { \"item\": \"iron_sword\" } ]" + tail + " } ] }";

    [Fact]
    public void HasksCardAndHisLanceNameTheShardInThePommel()
    {
        var hask = Shipped.Unit("hask");
        var lance = Shipped.Weapon("wardens_lance");

        Assert.Equal("Hask", hask.Name);
        Assert.True(hask.Named);
        Assert.Contains("pommel", hask.Description!, StringComparison.Ordinal);
        Assert.Contains("frozen iron", hask.Description!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pommel", lance.Description, StringComparison.Ordinal);
        Assert.Contains("frozen iron", lance.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "wardens_lance" }, hask.Inventory.Items.Select(s => s.ItemId));
    }

    [Fact]
    public void TheShardIsTheKinsVoiceNotAWeaponStat()
    {
        var lance = Shipped.Weapon("wardens_lance");
        var steel = Shipped.Weapon("steel_lance");

        Assert.False(lance.FrozenIron);
        Assert.Null(lance.Price);
        Assert.Equal((steel.Type, steel.Mt, steel.Hit, steel.Crit, steel.Wt, steel.MinRange, steel.MaxRange, steel.Durability, steel.Rank),
            (lance.Type, lance.Mt, lance.Hit, lance.Crit, lance.Wt, lance.MinRange, lance.MaxRange, lance.Durability, lance.Rank));
        Assert.DoesNotContain(Shipped.Campaign.Maps, m => m.Stock.Contains("wardens_lance"));
    }

    [Fact]
    public void AUnitsCardPrintsItsDescriptionUnderItsName()
    {
        var map = """
            name: Field
            size: 5x3
            win: rout
            turn_limit: 10
            recall: 3
            enemy_level: 1

            .....
            .....
            .....

            units:
            P captain 0,1
            E hask 4,1 group:lord behavior:hold
            E brigand 4,0 group:field behavior:hold

            """;
        var state = BattleFixture.Start(map: map);

        var his = PlaySession.ShowLines(state, Shipped, state.Find("hask-1")!);
        var brigands = PlaySession.ShowLines(state, Shipped, state.Find("brigand-1")!);

        Assert.Equal("  " + Shipped.Unit("hask").Description, his[1]);
        Assert.StartsWith("  HP ", brigands[1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(", \"description\": \"\"")]
    [InlineData(", \"description\": \"One line.\\nAnd another.\"")]
    [InlineData(", \"description\": 3")]
    public void AUnitDescriptionIsHeldToTheItemRuleNamingFileEntryAndField(string tail)
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(units: UnitWith(tail))));

        Assert.Equal("units/units.json", e.File);
        Assert.Equal("u", e.Entry);
        Assert.Equal("description", e.Field);
    }

    [Fact]
    public void AUnitDescriptionLongerThanAConsoleLineIsRefused()
    {
        var tooLong = ", \"description\": \"" + new string('a', ContentLoader.DescriptionMax + 1) + "\"";
        var fits = ", \"description\": \"" + new string('a', ContentLoader.DescriptionMax) + "\"";

        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(Fixture.Files(units: UnitWith(tooLong))));

        Assert.Equal("description", e.Field);
        Assert.Equal(ContentLoader.DescriptionMax, ContentLoader.Parse(Fixture.Files(units: UnitWith(fits))).Unit("u").Description!.Length);
        Assert.Null(ContentLoader.Parse(Fixture.Files(units: UnitWith(""))).Unit("u").Description);
    }

    [Fact]
    public void ADescriptionAndANamedFlagRoundTripThroughTheSerializer()
    {
        var again = ContentLoader.Parse(ContentSerializer.Write(Shipped));

        Assert.Equal(Shipped.Unit("hask").Description, again.Unit("hask").Description);
        Assert.True(again.Unit("hask").Named);
        Assert.False(again.Unit("brigand").Named);
    }

    [Fact]
    public void ANamedTemplateIsAnnouncedByItsNameAndAKindByAnArticle()
    {
        MapEvent Spawn(string template, bool boss) => new("e", new TurnTrigger(6, Side.Enemy),
            new SpawnEnemy(new EnemyPlacement(new Coord(0, 6), template, "g", boss ? Behavior.Boss : Behavior.Aggressive, boss)));

        Assert.Equal(
            "turn 6, enemy phase: the boss, Hask, arrives at 0,6. A unit standing on 0,6 does not stop the boss, who takes the nearest free tile.",
            PlaySession.DescribeEvent(Spawn("hask", boss: true), Shipped));
        Assert.Equal(
            "turn 6, enemy phase: Hask arrives at 0,6 (aggressive). A unit standing on 0,6 stops Hask.",
            PlaySession.DescribeEvent(Spawn("hask", boss: false), Shipped));
        Assert.Equal(
            "turn 6, enemy phase: an archer arrives at 0,6 (aggressive). A unit standing on 0,6 stops it.",
            PlaySession.DescribeEvent(Spawn("archer", boss: false), Shipped));
    }
}
