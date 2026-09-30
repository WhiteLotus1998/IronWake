using System.Text.RegularExpressions;
using Ironwake.Client;
using Ironwake.Content;
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
    public void TheThreatHatchIsSlateOverAWarmGroundAndBoneElsewhere()
    {
        Assert.True(LookPalette.WarmGround("hill"));
        Assert.Equal(LookPalette.Enemy, LookPalette.ThreatHatch("hill"));
        var cold = LookPalette.Terrain.Keys.Where(id => id is not ("hill" or "fire")).ToList();
        Assert.All(cold, id => Assert.Equal(LookPalette.Marks["threat"], LookPalette.ThreatHatch(id)));
    }

    [Fact]
    public void TheSlateHatchOverTheHillReadsFartherFromOursThanBone()
    {
        static Rgb Half(Rgb a, Rgb b) => new((byte)((a.R + b.R) / 2), (byte)((a.G + b.G) / 2), (byte)((a.B + b.B) / 2));
        var hill = LookPalette.Terrain["hill"];

        var bone = ColourVision.Distance(Half(LookPalette.Marks["threat"], hill), LookPalette.Player);
        var slate = ColourVision.Distance(Half(LookPalette.ThreatHatch("hill"), hill), LookPalette.Player);

        Assert.True(slate > bone, $"slate {slate:F1} against bone {bone:F1}");
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

    [GeneratedRegex(@"^\| `([a-z.]+)` \| `#([0-9A-F]{6})` \|", RegexOptions.Multiline)]
    private static partial Regex TokenRow();

    [GeneratedRegex(@"#([0-9A-Fa-f]{6})\b")]
    private static partial Regex HexColour();
}
