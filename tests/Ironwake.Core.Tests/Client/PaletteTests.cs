using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using static Ironwake.Client.ColourVision;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The readability pass's colour rule (issue 349): every terrain is distinguishable from every
/// other, the two sides from each other, and each side from every terrain it can stand on,
/// in full colour and under simulated deuteranopia and protanopia, checked by arithmetic.
/// </summary>
public class PaletteTests
{
    private static IEnumerable<(string, string)> Pairs(IReadOnlyList<string> ids) =>
        ids.SelectMany((a, i) => ids.Skip(i + 1).Select(b => (a, b)));

    [Fact]
    public void EveryContentTerrainHasItsOwnColour()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        Assert.All(content.Terrain.Keys, id => Assert.True(Palette.Terrain.ContainsKey(id), id));
    }

    [Fact]
    public void EveryTerrainPairStaysApartUnderColourVisionDeficiency()
    {
        var failing = Pairs(Palette.Terrain.Keys.ToList())
            .Select(p => (p, d: WorstDistance(Palette.Terrain[p.Item1], Palette.Terrain[p.Item2])))
            .Where(x => x.d < Palette.TerrainSeparation)
            .Select(x => $"{x.p.Item1}/{x.p.Item2} {x.d:F1}");
        Assert.Empty(failing);
    }

    [Fact]
    public void TheSidesStayApartUnderColourVisionDeficiency()
    {
        Assert.True(WorstDistance(Palette.Player, Palette.Enemy) >= Palette.SideSeparation);
    }

    [Fact]
    public void EachSideStaysApartFromEveryTerrainItCanStandOn()
    {
        var failing = Palette.Standable
            .SelectMany(id => new[] { ("player", Palette.Player), ("enemy", Palette.Enemy) }
                .Select(side => (id, side.Item1, d: WorstDistance(side.Item2, Palette.Terrain[id]))))
            .Where(x => x.d < Palette.UnitOnTerrainSeparation)
            .Select(x => $"{x.Item2} on {x.id} {x.d:F1}");
        Assert.Empty(failing);
    }

    [Fact]
    public void TheCheckRefusesTheClassicRedGreenPairThatFullColourPasses()
    {
        var red = new Rgb(200, 60, 50);
        var green = new Rgb(80, 150, 50);
        Assert.True(Distance(red, green) >= Palette.SideSeparation);
        Assert.True(WorstDistance(red, green) < Palette.SideSeparation);
    }

    [Fact]
    public void SimulationLeavesGreyAlone()
    {
        var grey = new Rgb(128, 128, 128);
        Assert.True(Distance(grey, Simulate(grey, Vision.Deuteranopia)) < 1.5);
        Assert.True(Distance(grey, Simulate(grey, Vision.Protanopia)) < 1.5);
    }
}
