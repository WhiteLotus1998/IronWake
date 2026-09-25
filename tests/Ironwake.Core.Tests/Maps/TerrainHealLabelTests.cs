namespace Ironwake.Core.Tests.Maps;

/// <summary>
/// Issue 207: a healing tile says how much it heals wherever the console names the terrain
/// under a unit or in a map's legend, the percent read from content and the HP from the same
/// function the resolver applies. A tile with no heal prints its name alone.
/// </summary>
public class TerrainHealLabelTests
{
    private static Terrain Tile(int heal) => new(
        "keep", "Keep", 'K', ValueList<int?>.Of(1, 1, 1, 1), 10, 1, 1, heal, AppliesToFlyers: true);

    [Fact]
    public void AHealingTileNamesItsPercentAndTheHpForAUnitsMax()
    {
        Assert.Equal("Keep (heals 35 percent)", Tile(35).Label());
        Assert.Equal("Keep (heals 35 percent, 7 hp)", Tile(35).Label(21));
    }

    [Fact]
    public void ATileWithNoHealPrintsItsNameAlone()
    {
        Assert.Equal("Keep", Tile(0).Label());
        Assert.Equal("Keep", Tile(0).Label(21));
    }

    [Theory]
    [InlineData(20, 16, 3)]
    [InlineData(20, 22, 4)]
    [InlineData(20, 25, 5)]
    [InlineData(0, 25, 0)]
    public void TheHealIsThePercentOfMaxFloored(int percent, int max, int expected)
    {
        Assert.Equal(expected, Tile(percent).HealFor(max));
    }

    [Fact]
    public void AUnitOnAFortReadsTheHealItWillGetAndAUnitOnPlainReadsNone()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);
        var start = BattleState.From(map, MapFixture.Content, MapFixture.Content.Cast, 7);
        var captain = start.Find("captain")!;
        var onFort = start.WithUnit(captain with { At = new Coord(6, 2) });
        var max = captain.MaxHp(MapFixture.Content);
        var heal = MapFixture.Content.TerrainById("fort").HealFor(max);

        var rows = MapRenderer.Render(onFort, MapFixture.Content).Split('\n');

        Assert.Contains(rows, r => r.Contains(" captain ", StringComparison.Ordinal) && r.Contains($"Fort (heals 20 percent, {heal} hp)", StringComparison.Ordinal));
        Assert.Contains(rows, r => r.Contains(" wren ", StringComparison.Ordinal) && r.Contains("Plain", StringComparison.Ordinal) && !r.Contains("heals", StringComparison.Ordinal));
    }

    [Fact]
    public void TheMapLegendSaysAHealingGlyphHeals()
    {
        var terrainLine = MapRenderer.Render(MapFixture.Parse(MapFixture.OldMillRoad), MapFixture.Content)
            .Split('\n').Last(l => l.StartsWith("terrain:", StringComparison.Ordinal));

        Assert.Contains("F Fort (heals 20 percent)", terrainLine);
        Assert.DoesNotContain("Plain (heals", terrainLine);
    }

    [Fact]
    public void TheResolverHealsByTheSameFunctionTheConsolePrints()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);
        var start = BattleState.From(map, MapFixture.Content, MapFixture.Content.Cast, 7);
        var captain = start.Find("captain")!;
        var max = captain.MaxHp(MapFixture.Content);
        var hurt = start.WithUnit(captain with { At = new Coord(6, 2), Hp = 1 });

        var afterEnemy = Resolver.Apply(Resolver.Apply(hurt, MapFixture.Content, new EndPhase()).Next, MapFixture.Content, new EndPhase());

        Assert.Equal(1 + MapFixture.Content.TerrainById("fort").HealFor(max), afterEnemy.Next.Find("captain")!.Hp);
    }
}
