using System.IO.Compression;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The generated art (issue 564): <c>docs/art/make_art.py</c> writes every token but the
/// captain's and every tile under <c>src/Ironwake.Godot/assets/art/</c> at the art spec's names,
/// and lists them in <c>generated.txt</c>. Every listed file is the spec's 2x frame and keeps
/// LOOK.md's colours: a token is its side's values and nothing else, a tile its terrain's colour
/// with detail laid over it, so terrain never adds a hue.
/// </summary>
public class ArtGeneratedTests
{
    private const int Frame = 2 * ArtSpec.TokenFrame;

    /// <summary>The enemy disc's ink shadow, at the client's 0.55.</summary>
    private const int ShadowAlpha = 140;

    private static readonly Rgb Ink = LookPalette.Ui["ink"];

    private static string ArtDirectory() =>
        Path.GetFullPath(Path.Combine(Fixture.RealContentDirectory(), "..", "src", "Ironwake.Godot", "assets", "art"));

    private static List<string> Generated() =>
        File.ReadAllLines(Path.Combine(ArtDirectory(), "generated.txt"))
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => Path.GetFileNameWithoutExtension(l))
            .ToList();

    [Fact]
    public void TheGeneratorWritesEveryTokenButTheCaptainsAndEveryTile()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var expected = ArtSpec.Tokens(content).Where(n => n != "token_captain_player").Concat(ArtSpec.Tiles(content)).ToHashSet();

        Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), Generated().OrderBy(n => n, StringComparer.Ordinal));
        Assert.All(Generated(), name => Assert.True(File.Exists(Path.Combine(ArtDirectory(), name + ".png")), name));
    }

    [Fact]
    public void EveryGeneratedFileIsTheSpecsFrameAtTwoX()
    {
        Assert.All(Generated(), name =>
        {
            var image = Png.Read(Path.Combine(ArtDirectory(), name + ".png"));
            Assert.Equal((Frame, Frame), (image.Width, image.Height));
        });
    }

    [Fact]
    public void ATokenIsItsSidesValuesAndNothingElse()
    {
        Assert.All(Generated().Where(n => n.StartsWith("token_", StringComparison.Ordinal)), name =>
        {
            var image = Png.Read(Path.Combine(ArtDirectory(), name + ".png"));
            Assert.Null(TokenFault(image, player: name.EndsWith("_player", StringComparison.Ordinal)));
        });
    }

    [Fact]
    public void ATileIsItsTerrainWithDetailLaidOverIt()
    {
        Assert.All(Generated().Where(n => n.StartsWith("tile_", StringComparison.Ordinal)), name =>
        {
            var image = Png.Read(Path.Combine(ArtDirectory(), name + ".png"));
            Assert.Null(TileFault(image, name["tile_".Length..]));
        });
    }

    [Fact]
    public void ATokenWithAForeignColourFailsTheSidesRule()
    {
        var image = Png.Read(Path.Combine(ArtDirectory(), "token_cadet_enemy.png"));
        Assert.Null(TokenFault(image, player: false));

        Assert.NotNull(TokenFault(image.With(48, 40, new Rgba(200, 40, 40, 255)), player: false));
        Assert.NotNull(TokenFault(image, player: true));
        Assert.NotNull(TokenFault(image.With(48, 90, new Rgba(Ink.R, Ink.G, Ink.B, 255)), player: false));
    }

    [Fact]
    public void ATileThatAddsAHueFailsTheTerrainRule()
    {
        var image = Png.Read(Path.Combine(ArtDirectory(), "tile_forest.png"));
        Assert.Null(TileFault(image, "forest"));

        Assert.NotNull(TileFault(image.With(10, 10, new Rgba(200, 40, 40, 255)), "forest"));
        Assert.NotNull(TileFault(image.With(10, 10, new Rgba(0, 0, 0, 0)), "forest"));
        Assert.NotNull(TileFault(image, "plain"));
    }

    /// <summary>
    /// Why a token breaks the look, or null: every pixel clear, one of its side's values, or (the
    /// enemy's) the ink shadow; the bottom 16 rows clear for the HP bar; the side's fill present.
    /// </summary>
    private static string? TokenFault(Png image, bool player)
    {
        var allowed = player
            ? new[] { Opaque(LookPalette.Player), Opaque(LookPalette.PlayerDeep), Opaque(Ink) }
            : new[] { Opaque(LookPalette.Enemy), Opaque(LookPalette.EnemyBone), new Rgba(Ink.R, Ink.G, Ink.B, ShadowAlpha) };
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var p = image.At(x, y);
                if (p.A == 0)
                {
                    continue;
                }

                if (y >= image.Height - 16)
                {
                    return $"pixel {x},{y} is inside the HP bar's rows";
                }

                if (!allowed.Contains(p))
                {
                    return $"pixel {x},{y} is {p}, not one of the side's values";
                }
            }
        }

        return image.Count(allowed[0]) > 0 ? null : "the side's fill is missing";
    }

    /// <summary>
    /// Why a tile breaks the look, or null: fire is ember hatch on clear ground; any other tile is
    /// opaque, each pixel its terrain's colour or that colour with ink, frost or iron laid over it
    /// at 20 to 65 percent (LOOK.md's detail and the wall's coursing), so no pixel adds a hue.
    /// </summary>
    private static string? TileFault(Png image, string terrain)
    {
        var baseColour = Opaque(LookPalette.Terrain[terrain]);
        var overlays = new[] { Ink, LookPalette.Marks["reach"], LookPalette.Terrain["mountain"] };
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var p = image.At(x, y);
                if (terrain == LookPalette.HatchedTerrain)
                {
                    if (p.A != 0 && p != baseColour)
                    {
                        return $"pixel {x},{y} is {p}, not ember or clear";
                    }

                    continue;
                }

                if (p.A != 255)
                {
                    return $"pixel {x},{y} is not opaque";
                }

                if (p != baseColour && !overlays.Any(o => LaidOver(LookPalette.Terrain[terrain], o, p)))
                {
                    return $"pixel {x},{y} is {p}, not {terrain} or a detail over it";
                }
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="p"/> is <paramref name="over"/> laid on <paramref name="under"/> at one alpha in 0.2 to 0.65, to a channel's rounding.</summary>
    private static bool LaidOver(Rgb under, Rgb over, Rgba p)
    {
        var channels = new[] { (under.R, over.R, p.R), (under.G, over.G, p.G), (under.B, over.B, p.B) };
        var (u, o, v) = channels.MaxBy(c => Math.Abs(c.Item2 - c.Item1));
        if (o == u)
        {
            return false;
        }

        var alpha = (double)(v - u) / (o - u);
        return alpha is >= 0.2 and <= 0.65
            && channels.All(c => Math.Abs(c.Item1 + (c.Item2 - c.Item1) * alpha - c.Item3) <= 1.5);
    }

    private static Rgba Opaque(Rgb c) => new(c.R, c.G, c.B, 255);

    private readonly record struct Rgba(byte R, byte G, byte B, byte A)
    {
        public override string ToString() => $"#{R:X2}{G:X2}{B:X2}/{A}";
    }

    /// <summary>A PNG read as 8-bit RGBA, enough of the format for the generator's files (every filter, no interlace).</summary>
    private sealed record Png(int Width, int Height, byte[] Pixels)
    {
        public Rgba At(int x, int y)
        {
            var i = 4 * (y * Width + x);
            return new Rgba(Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]);
        }

        public Png With(int x, int y, Rgba p)
        {
            var copy = (byte[])Pixels.Clone();
            var i = 4 * (y * Width + x);
            (copy[i], copy[i + 1], copy[i + 2], copy[i + 3]) = (p.R, p.G, p.B, p.A);
            return this with { Pixels = copy };
        }

        public int Count(Rgba p) =>
            Enumerable.Range(0, Width * Height).Count(i => At(i % Width, i / Width) == p);

        public static Png Read(string path)
        {
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }), $"{path} is not a PNG");
            int width = 0, height = 0;
            using var data = new MemoryStream();
            for (var at = 8; at < bytes.Length;)
            {
                var length = BigEndian(bytes, at);
                var kind = System.Text.Encoding.ASCII.GetString(bytes, at + 4, 4);
                if (kind == "IHDR")
                {
                    width = BigEndian(bytes, at + 8);
                    height = BigEndian(bytes, at + 12);
                    Assert.True(bytes[at + 16] == 8 && bytes[at + 17] == 6 && bytes[at + 20] == 0, $"{path} is not 8-bit RGBA without interlace");
                }
                else if (kind == "IDAT")
                {
                    data.Write(bytes, at + 8, length);
                }

                at += 12 + length;
            }

            data.Position = 0;
            using var inflate = new ZLibStream(data, CompressionMode.Decompress);
            using var raw = new MemoryStream();
            inflate.CopyTo(raw);
            return new Png(width, height, Unfilter(raw.ToArray(), width, height));
        }

        private static int BigEndian(byte[] b, int at) => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];

        private static byte[] Unfilter(byte[] raw, int width, int height)
        {
            var stride = 4 * width;
            var pixels = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                var filter = raw[y * (stride + 1)];
                for (var x = 0; x < stride; x++)
                {
                    var v = raw[y * (stride + 1) + 1 + x];
                    int a = x >= 4 ? pixels[y * stride + x - 4] : 0;
                    int b = y > 0 ? pixels[(y - 1) * stride + x] : 0;
                    int c = x >= 4 && y > 0 ? pixels[(y - 1) * stride + x - 4] : 0;
                    pixels[y * stride + x] = (byte)(filter switch
                    {
                        0 => v,
                        1 => v + a,
                        2 => v + b,
                        3 => v + ((a + b) >> 1),
                        4 => v + Paeth(a, b, c),
                        _ => throw new InvalidDataException($"PNG filter {filter}"),
                    });
                }
            }

            return pixels;
        }

        private static int Paeth(int a, int b, int c)
        {
            var p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }
    }
}
