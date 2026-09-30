using Godot;
using Ironwake.Client;
using Ironwake.Core;
using CoreSide = Ironwake.Core.Side;

namespace Ironwake.Godot;

/// <summary>
/// The map's art files (issue 564): each token and tile is read from
/// <c>assets/art/&lt;name&gt;.png</c> at <c>docs/ART_SPEC.md</c>'s names, whether the generator
/// wrote it or an artist delivered it, and scaled once to the size it is drawn at. A name with no
/// file keeps the vector placeholder <c>Main.Look.cs</c> draws, so a delivered file drops in with
/// no code change and the captain, whose token Lotus is making, keeps his.
/// </summary>
public partial class Main
{
    /// <summary>The token's disc centre below the frame's top and its radius, on ART_SPEC's 48-pixel frame.</summary>
    private const float TokenY = 21, TokenRadius = 16;

    private readonly Dictionary<string, Image?> _art = new();
    private readonly Dictionary<(string Name, int Size, bool DetailOnly), Texture2D?> _artScaled = new();

    /// <summary>
    /// A file's pixels, read once: the source tree's PNG, else the exported build's imported
    /// resource, else null.
    /// </summary>
    private Image? ArtImage(string name)
    {
        if (_art.TryGetValue(name, out var known))
        {
            return known;
        }

        var resource = $"res://assets/art/{name}.png";
        Image? image = null;
        var path = ProjectSettings.GlobalizePath(resource);
        if (File.Exists(path))
        {
            image = Image.LoadFromFile(path);
        }

        image ??= ResourceLoader.Exists(resource) ? GD.Load<Texture2D>(resource)?.GetImage() : null;
        if (image is not null)
        {
            if (image.IsCompressed())
            {
                image.Decompress();
            }

            image.Convert(Image.Format.Rgba8);
        }

        _art[name] = image;
        return image;
    }

    /// <summary>
    /// A file scaled to <paramref name="size"/> pixels square, cached per size. With
    /// <paramref name="detailOnly"/> the pixels of <paramref name="clear"/> (a tile's own ground)
    /// are cleared first, which leaves the tile's ink detail to lay over the threat hatch.
    /// </summary>
    private Texture2D? Art(string name, int size, bool detailOnly = false, Color clear = default)
    {
        if (size <= 0)
        {
            return null;
        }

        if (_artScaled.TryGetValue((name, size, detailOnly), out var known))
        {
            return known;
        }

        Texture2D? texture = null;
        if (ArtImage(name) is { } source)
        {
            var image = (Image)source.Duplicate();
            if (detailOnly)
            {
                var ground = clear.ToRgba32();
                for (var y = 0; y < image.GetHeight(); y++)
                {
                    for (var x = 0; x < image.GetWidth(); x++)
                    {
                        if ((image.GetPixel(x, y) with { A = 1 }).ToRgba32() == ground)
                        {
                            image.SetPixel(x, y, Colors.Transparent);
                        }
                    }
                }
            }

            image.Resize(size, size, Image.Interpolation.Lanczos);
            texture = ImageTexture.CreateFromImage(image);
        }

        _artScaled[(name, size, detailOnly)] = texture;
        return texture;
    }

    /// <summary>A unit's token file at <paramref name="size"/> pixels, the first of <see cref="ArtSpec.TokenFiles"/> on disk, or null for the placeholder.</summary>
    private Texture2D? TokenArt(BattleUnit unit, int size) =>
        ArtSpec.TokenFiles(_client!.Content, unit.Unit, unit.Side, unit.IsBoss, unit.IsCaptain)
            .Select(name => Art(name, size)).FirstOrDefault(t => t is not null);

    /// <summary>A tile's file at the board's tile size, or null for the placeholder; fire's is its hatch alone.</summary>
    private Texture2D? TileArt(string terrain, bool detailOnly = false) =>
        Art($"tile_{terrain}", _tile, detailOnly, TerrainColour(terrain));

    /// <summary>The token's disc centre in a tile at the board's scale.</summary>
    private Vector2 TokenCentre(Coord at) => Cell(at).Position + new Vector2(_tile / 2f, TokenY * S);

    /// <summary>
    /// A unit's token file drawn so its disc sits at <paramref name="centre"/> with
    /// <paramref name="radius"/>, tinted toward the ink by <paramref name="sink"/>; false when it
    /// has no file and the placeholder must draw.
    /// </summary>
    private bool DrawTokenArt(BattleUnit unit, Vector2 centre, float radius, float sink = 0)
    {
        var k = radius / TokenRadius;
        var size = Mathf.RoundToInt(ArtSpec.TokenFrame * k);
        if (TokenArt(unit, size) is not { } texture)
        {
            return false;
        }

        var frame = new Rect2(centre - new Vector2(ArtSpec.TokenFrame / 2f, TokenY) * k, new Vector2(ArtSpec.TokenFrame, ArtSpec.TokenFrame) * k);
        DrawTextureRect(texture, frame, false, Colors.White.Lerp(UiColour("ink"), sink));
        return true;
    }
}
