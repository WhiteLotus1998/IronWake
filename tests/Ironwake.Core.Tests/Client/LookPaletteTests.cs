using System.Text.RegularExpressions;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core;
using Ironwake.Core.Tests.Content;
using static Ironwake.Client.ColourVision;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The showcase's palette (issue 510): the readability pass's colour rules hold for the new
/// tokens, warmth belongs to the player alone, and <c>docs/LOOK.md</c> and the mocked frames
/// in <c>docs/look/</c> carry exactly the values <see cref="LookPalette"/> holds.
/// </summary>
public partial class LookPaletteTests
{
    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static IEnumerable<(string, string)> Pairs(IReadOnlyList<string> ids) =>
        ids.SelectMany((a, i) => ids.Skip(i + 1).Select(b => (a, b)));

    [Fact]
    public void EveryContentTerrainHasALookColour()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        Assert.All(content.Terrain.Keys, id => Assert.True(LookPalette.Terrain.ContainsKey(id), id));
    }

    [Fact]
    public void EveryLookTerrainPairStaysApartUnderColourVisionDeficiency()
    {
        var failing = Pairs(LookPalette.Terrain.Keys.ToList())
            .Select(p => (p, d: WorstDistance(LookPalette.Terrain[p.Item1], LookPalette.Terrain[p.Item2])))
            .Where(x => x.d < Palette.TerrainSeparation)
            .Select(x => $"{x.p.Item1}/{x.p.Item2} {x.d:F1}");
        Assert.Empty(failing);
    }

    [Fact]
    public void TheLookSidesStayApartUnderColourVisionDeficiency()
    {
        Assert.True(WorstDistance(LookPalette.Player, LookPalette.Enemy) >= Palette.SideSeparation);
    }

    [Fact]
    public void EachLookSideStaysApartFromEveryTerrainItCanStandOn()
    {
        var failing = LookPalette.Standable
            .SelectMany(id => new[] { ("player", LookPalette.Player), ("enemy", LookPalette.Enemy) }
                .Select(side => (id, side.Item1, d: WorstDistance(side.Item2, LookPalette.Terrain[id]))))
            .Where(x => x.d < Palette.UnitOnTerrainSeparation)
            .Select(x => $"{x.Item2} on {x.id} {x.d:F1}");
        Assert.Empty(failing);
    }

    [Fact]
    public void EveryMarkPairStaysApartUnderColourVisionDeficiency()
    {
        var failing = Pairs(LookPalette.Marks.Keys.ToList())
            .Select(p => (p, d: WorstDistance(LookPalette.Marks[p.Item1], LookPalette.Marks[p.Item2])))
            .Where(x => x.d < LookPalette.MarkSeparation)
            .Select(x => $"{x.p.Item1}/{x.p.Item2} {x.d:F1}");
        Assert.Empty(failing);
    }

    [Fact]
    public void TheCaptainsGoldStandsApartFromTheAmberDiscItRings()
    {
        Assert.True(WorstDistance(LookPalette.Marks["captain"], LookPalette.Player) >= LookPalette.MarkSeparation);
    }

    [Fact]
    public void TheCaptainsGoldIsAPlayerMark()
    {
        Assert.Contains("captain", LookPalette.PlayerMarks);
    }

    [Fact]
    public void WarmthBelongsToThePlayerAlone()
    {
        Assert.True(Chroma(LookPalette.Player) >= LookPalette.PlayerChromaFloor);
        var cold = LookPalette.Terrain.Where(t => t.Key != LookPalette.HatchedTerrain).Select(t => ($"terrain.{t.Key}", t.Value))
            .Append(("enemy", LookPalette.Enemy))
            .Append(("enemy.bone", LookPalette.EnemyBone))
            .Concat(LookPalette.Marks.Where(m => !LookPalette.PlayerMarks.Contains(m.Key)).Select(m => ($"mark.{m.Key}", m.Value)));
        var failing = cold.Where(c => Chroma(c.Item2) > LookPalette.WorldChromaCeiling).Select(c => $"{c.Item1} {Chroma(c.Item2):F1}");
        Assert.Empty(failing);
    }

    [Fact]
    public void NoGroundButFireAndTheOutlandSandIsWarm()
    {
        Assert.Equal(new[] { LookPalette.WarmGroundRegion }, WarmRegions(LookPalette.GroundOf));
        var warmTerrain = LookPalette.Terrain.Where(t => t.Key != LookPalette.HatchedTerrain && LookPalette.IsWarmGround(t.Value)).Select(t => t.Key);
        Assert.Empty(warmTerrain);
        Assert.Equal(new[] { "sand" }, LookPalette.Grounds.Where(g => LookPalette.IsWarmGround(g.Value)).Select(g => g.Key));
    }

    [Fact]
    public void ASecondWarmGroundFailsTheException()
    {
        var oldHill = new Rgb(0xB8, 0x9E, 0x6C);
        var warm = WarmRegions(region => region == MapRegion.Sallow ? oldHill : LookPalette.GroundOf(region));
        Assert.NotEqual(new[] { LookPalette.WarmGroundRegion }, warm);
    }

    private static MapRegion[] WarmRegions(Func<MapRegion, Rgb> ground) =>
        Enum.GetValues<MapRegion>().Where(r => LookPalette.IsWarmGround(ground(r))).ToArray();

    [Fact]
    public void TheWarmGroundRuleRefusesTheOldHill()
    {
        Assert.True(LookPalette.IsWarmGround(new Rgb(0xB8, 0x9E, 0x6C)));
    }

    [Fact]
    public void TheOutlandSandHoldsTheGuard()
    {
        Assert.True(LookPalette.OutlandGroundHolds(LookPalette.GroundOf(MapRegion.Outland)));
    }

    [Fact]
    public void TheGuardRefusesABrightDesertSand()
    {
        var bright = new Rgb(0xC0, 0xA8, 0x78);
        Assert.True(Chroma(bright) <= LookPalette.WorldChromaCeiling);
        Assert.False(LookPalette.OutlandGroundHolds(bright));
    }

    [Fact]
    public void TheInkEdgeIsDrawnOnEveryWarmGroundAndNoOther()
    {
        Assert.All(LookPalette.Grounds, g => Assert.Equal(LookPalette.IsWarmGround(g.Value), LookPalette.EdgedOn(g.Value)));
        Assert.True(LookPalette.EdgedOn(LookPalette.GroundOf(MapRegion.Outland)));
        Assert.False(LookPalette.EdgedOn(LookPalette.GroundOf(MapRegion.Seam)));
        Assert.False(LookPalette.EdgedOn(LookPalette.GroundOf(MapRegion.Sallow)));
    }

    [Fact]
    public void TheInkEdgeFiresOnTheOldHill()
    {
        Assert.True(LookPalette.EdgedOn(new Rgb(0xB8, 0x9E, 0x6C)));
    }

    [Fact]
    public void TheSeamsGroundIsPlainsOwnColour()
    {
        Assert.Equal(LookPalette.Terrain["plain"], LookPalette.GroundOf(MapRegion.Seam));
        Assert.Equal(LookPalette.Terrain["forest"], LookPalette.TerrainIn("forest", MapRegion.Outland));
        Assert.Equal(LookPalette.Grounds["moss"], LookPalette.TerrainIn("plain", MapRegion.Aldmere));
        Assert.Null(LookPalette.TerrainIn("lava", MapRegion.Seam));
    }

    public static TheoryData<MapRegion> Regions()
    {
        var data = new TheoryData<MapRegion>();
        foreach (var region in Enum.GetValues<MapRegion>())
        {
            data.Add(region);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void EachRegionsGroundStaysApartFromEveryOtherTerrain(MapRegion region)
    {
        var ground = LookPalette.GroundOf(region);
        var failing = LookPalette.Terrain.Where(t => t.Key != "plain")
            .Select(t => (t.Key, d: WorstDistance(ground, t.Value)))
            .Where(x => x.d < Palette.TerrainSeparation)
            .Select(x => $"{region} ground/{x.Key} {x.d:F1}");
        Assert.Empty(failing);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void EachSideStandsApartFromEachRegionsGround(MapRegion region)
    {
        var ground = LookPalette.GroundOf(region);
        Assert.True(WorstDistance(LookPalette.Player, ground) >= Palette.UnitOnTerrainSeparation, $"player on {region}");
        Assert.True(WorstDistance(LookPalette.Enemy, ground) >= Palette.UnitOnTerrainSeparation, $"enemy on {region}");
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void EveryMarkStandsApartFromEachRegionsGround(MapRegion region)
    {
        var ground = LookPalette.GroundOf(region);
        var failing = LookPalette.Marks.Append(new("bone", LookPalette.EnemyBone))
            .Select(m => (m.Key, d: WorstDistance(m.Value, ground)))
            .Where(x => x.d < LookPalette.MarkSeparation)
            .Select(x => $"{x.Key} on {region} {x.d:F1}");
        Assert.Empty(failing);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void EachRegionsGroundStaysUnderTheWorldsChromaCeiling(MapRegion region)
    {
        Assert.True(Chroma(LookPalette.GroundOf(region)) <= LookPalette.WorldChromaCeiling, region.ToString());
    }

    [Fact]
    public void TheWarmthCheckRefusesTheDebugClientsOrangeEnemy()
    {
        Assert.True(Chroma(Palette.Enemy) > LookPalette.WorldChromaCeiling);
    }

    [Fact]
    public void LookMdListsEveryTokenWithItsValueAndNoOther()
    {
        var text = File.ReadAllText(Path.Combine(Repo, "docs", "LOOK.md"));
        var listed = TokenRow().Matches(text).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        var expected = LookPalette.Tokens.ToDictionary(t => t.Name, t => LookPalette.ToHex(t.Colour));
        Assert.Equal(expected.OrderBy(e => e.Key), listed.OrderBy(e => e.Key));
    }

    [Fact]
    public void TheMockedFramesDrawOnlyWithTokens()
    {
        var tokens = LookPalette.Tokens.Select(t => LookPalette.ToHex(t.Colour)).ToHashSet();
        var svgs = Directory.GetFiles(Path.Combine(Repo, "docs", "look"), "*.svg");
        Assert.NotEmpty(svgs);
        var stray = svgs.SelectMany(f => HexColour().Matches(File.ReadAllText(f))
                .Select(m => m.Groups[1].Value.ToUpperInvariant())
                .Where(hex => !tokens.Contains(hex))
                .Select(hex => $"{Path.GetFileName(f)} #{hex}"))
            .Distinct();
        Assert.Empty(stray);
    }

    [Fact]
    public void TheTokenCheckFindsAStrayColour()
    {
        var tokens = LookPalette.Tokens.Select(t => LookPalette.ToHex(t.Colour)).ToHashSet();
        var stray = HexColour().Matches("<rect fill=\"#E86E14\"/>").Select(m => m.Groups[1].Value).Where(h => !tokens.Contains(h));
        Assert.Single(stray);
    }

    [GeneratedRegex(@"^\| `([a-z._]+)` \| `#([0-9A-F]{6})` \|", RegexOptions.Multiline)]
    private static partial Regex TokenRow();

    [GeneratedRegex(@"#([0-9A-Fa-f]{6})\b")]
    private static partial Regex HexColour();
}
