using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The terrain card (issue 610): what a terrain does for a unit on it, generated from
/// <c>terrain.json</c> and the map, never written by hand.
/// </summary>
public class TerrainCardTests
{
    private const string Ford = """
        name: Ford
        size: 6x3
        win: rout
        turn_limit: 5
        recall: 3
        enemy_level: 1
        {0}
        ..T^F.
        ~~~~=.
        ......

        units:
        P captain 0,0
        E brigand 5,2 group:far behavior:hold

        events:
        flood turn 3 enemy terrain 4,1 ~

        """;

    private static BattleState Board(string headers = "", string win = "rout") =>
        BattleFixture.Start(map: string.Format(System.Globalization.CultureInfo.InvariantCulture, Ford, headers).Replace("win: rout", "win: " + win, StringComparison.Ordinal));

    private static string Card(BattleState state, string id) => TerrainCard.Text(state, MapFixture.Content, id);

    [Theory]
    [InlineData("plain", "Plain. No cover. Costs 1 to enter.")]
    [InlineData("forest", "Forest. -20 to hit a unit here, +1 Def. Flyers get none of it. Costs 2 to enter on foot or armored, 3 mounted, 1 flying.")]
    [InlineData("hill", "Hill. -10 to hit a unit here, +1 Def. Flyers get none of it. Costs 2 to enter on foot, 3 mounted or armored, 1 flying.")]
    [InlineData("mountain", "Mountain. -30 to hit a unit here, +2 Def. Flyers get none of it. Costs 3 to enter on foot, 1 flying. Mounted and armored cannot enter.")]
    [InlineData("water", "Water. Only flyers can enter.")]
    [InlineData("wall", "Wall. Nobody can enter.")]
    [InlineData("fort", "Fort. -15 to hit a unit here, +2 Def, +2 Res. A unit here heals 20 percent of max HP at its phase start. Costs 1 to enter.")]
    [InlineData("throne", "Gate. -30 to hit a unit here, +3 Def, +3 Res. A unit here heals 20 percent of max HP at its phase start. Costs 1 to enter.")]
    [InlineData("fire", "Fire. A unit here loses 20 percent of max HP at its phase start, never below 1. Costs 2 to enter on foot or armored, 3 mounted, 1 flying.")]
    public void EachTerrainsCardReadsItsNumbersFromTheTerrainFile(string id, string card)
    {
        Assert.Equal(card, Card(Board(), id));
    }

    [Fact]
    public void TheGateCardSaysTheCaptainWinsThereOnlyOnASeizeMap()
    {
        Assert.StartsWith("Gate. The captain wins by standing here. -30 to hit", Card(Board(win: "seize"), "throne"), StringComparison.Ordinal);
        Assert.DoesNotContain("captain", Card(Board(), "throne"), StringComparison.Ordinal);
    }

    [Fact]
    public void FlyersAreToldTheyGetNoCoverOnlyWhereTheBonusSkipsThem()
    {
        var state = Board();

        Assert.Contains("Flyers get none of it.", Card(state, "forest"), StringComparison.Ordinal);
        Assert.DoesNotContain("Flyers", Card(state, "fort"), StringComparison.Ordinal);
        Assert.DoesNotContain("Flyers", Card(state, "plain"), StringComparison.Ordinal);
    }

    [Fact]
    public void AWildfireMapsForestAndFireCardsCarryTheFireRule()
    {
        var lit = Board("wildfire: on\n");

        Assert.EndsWith("On this map a Cinder hit on a unit here sets the tile alight.", Card(lit, "forest"), StringComparison.Ordinal);
        Assert.EndsWith("On this map fire burns out to plain at each player phase start and lights the forest beside it.", Card(lit, "fire"), StringComparison.Ordinal);
        Assert.DoesNotContain("On this map", Card(Board(), "forest"), StringComparison.Ordinal);
        Assert.DoesNotContain("On this map", Card(Board(), "fire"), StringComparison.Ordinal);
    }

    [Fact]
    public void AnAnnouncedFloodPutsTheHeldTileRuleOnTheWaterCard()
    {
        var rule = "On this map events turn ground into water; a unit that cannot enter water holds its tile, and that change is spent.";

        Assert.EndsWith(rule, Card(Board("announce: on\n"), "water"), StringComparison.Ordinal);
        Assert.DoesNotContain("On this map", Card(Board("announce: on\n"), "road"), StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnannouncedOrSpentFloodIsNeverOnTheCard()
    {
        var announced = Board("announce: on\n");
        var spent = announced with { Fired = announced.Fired.Add("flood") };

        Assert.Equal("Water. Only flyers can enter.", Card(Board(), "water"));
        Assert.Equal("Water. Only flyers can enter.", Card(spent, "water"));
    }

    [Theory]
    [InlineData("^", "forest")]
    [InlineData("forest", "forest")]
    [InlineData("GATE", "throne")]
    [InlineData("throne", "throne")]
    [InlineData("~", "water")]
    public void APlayerNamesATerrainByGlyphIdOrName(string text, string id)
    {
        Assert.Equal(id, TerrainCard.Find(MapFixture.Content, text)?.Id);
    }

    [Fact]
    public void AnUnknownTerrainNameFindsNothing()
    {
        Assert.Null(TerrainCard.Find(MapFixture.Content, "swamp"));
    }

    /// <summary>
    /// Issue 610's done-when: every terrain on every shipped map has a card, and each card's
    /// numbers are that terrain's in <c>terrain.json</c>: its avoid, Def and Res only when it has
    /// them, its heal, and the cost of each movement type that can enter.
    /// </summary>
    [Fact]
    public void EveryTerrainOnEveryShippedMapHasACardWithItsNumbers()
    {
        var content = MapFixture.Content;
        var maps = MapFiles.LoadAll(Fixture.RealContentDirectory(), content);
        Assert.NotEmpty(maps);
        foreach (var (mapId, map) in maps)
        {
            var state = BattleState.From(map, content, content.Cast, 1);
            foreach (var id in TerrainCard.OnBoard(map))
            {
                var terrain = content.TerrainById(id);
                var card = TerrainCard.Text(state, content, id);
                var where = $"{mapId} {id}: {card}";
                Assert.True(card.StartsWith(terrain.Name + ".", StringComparison.Ordinal), where);
                Assert.True(card.Contains($"-{terrain.Avoid} to hit", StringComparison.Ordinal) == terrain.Avoid > 0, where);
                Assert.True(card.Contains($"+{terrain.Def} Def", StringComparison.Ordinal) == terrain.Def > 0, where);
                Assert.True(card.Contains($"+{terrain.Res} Res", StringComparison.Ordinal) == terrain.Res > 0, where);
                Assert.True(card.Contains($"heals {terrain.HealPercent} percent", StringComparison.Ordinal) == terrain.HealPercent > 0, where);
                foreach (var cost in Enum.GetValues<MovementType>().Select(terrain.MoveCost).OfType<int>().Distinct())
                {
                    Assert.True(card.Contains($"Costs {cost} ", StringComparison.Ordinal) || card.Contains($", {cost} ", StringComparison.Ordinal) || card == $"{terrain.Name}. Only flyers can enter.", where);
                }
            }
        }
    }
}
