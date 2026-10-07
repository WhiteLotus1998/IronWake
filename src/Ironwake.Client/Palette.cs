namespace Ironwake.Client;

/// <summary>An sRGB colour, 0 to 255 per channel.</summary>
public readonly record struct Rgb(byte R, byte G, byte B);

/// <summary>
/// The readability pass's colours (issue 349), which the Godot client drew with until the
/// showcase's <see cref="LookPalette"/> replaced them (issue 511): a terrain palette, the two sides, and the dusk shade,
/// chosen so every pair the board must tell apart stays apart under deuteranopia and
/// protanopia as well as in full colour. <see cref="ColourVision"/> is the check; the
/// palette tests run it, so a colour is changed here and nowhere else.
/// </summary>
public static class Palette
{
    /// <summary>The least CIE76 distance between two terrain colours, in full colour and under each simulated deficiency.</summary>
    public const double TerrainSeparation = 12;

    /// <summary>The least CIE76 distance between the two side colours, in full colour and under each simulated deficiency.</summary>
    public const double SideSeparation = 40;

    /// <summary>The least CIE76 distance between a side colour and any terrain it may stand on.</summary>
    public const double UnitOnTerrainSeparation = 20;

    /// <summary>The terrain colours by terrain id; a terrain not listed draws as <see cref="UnknownTerrain"/>.</summary>
    public static readonly IReadOnlyDictionary<string, Rgb> Terrain = new Dictionary<string, Rgb>
    {
        ["plain"] = new(166, 184, 128),
        ["road"] = new(228, 220, 198),
        ["forest"] = new(58, 98, 62),
        ["hill"] = new(128, 112, 60),
        ["mountain"] = new(112, 108, 112),
        ["water"] = new(96, 150, 176),
        ["fort"] = new(180, 144, 232),
        ["wall"] = new(40, 40, 46),
        ["throne"] = new(244, 234, 150),
        ["fire"] = new(112, 0, 0),
        ["planks"] = new(192, 168, 96),
        ["split_planks"] = new(112, 80, 16),
        ["rime"] = new(150, 200, 225),
        ["earthwork"] = new(168, 160, 152),
    };

    public static readonly Rgb UnknownTerrain = new(128, 128, 128);

    /// <summary>The player's units: a strong blue, drawn as a circle.</summary>
    public static readonly Rgb Player = new(36, 84, 220);

    /// <summary>The enemy's units: an orange, drawn as a square, so shape carries the side when colour does not.</summary>
    public static readonly Rgb Enemy = new(232, 110, 20);

    /// <summary>The terrains a unit can stand on, which each side colour must stand clear of.</summary>
    public static IEnumerable<string> Standable => Terrain.Keys.Where(id => id != "wall");

    public static Rgb TerrainOf(string id) => Terrain.TryGetValue(id, out var colour) ? colour : UnknownTerrain;
}

/// <summary>
/// Colour-vision deficiency simulation and colour distance, for checking the palette by
/// arithmetic rather than by eye: Machado, Oliveira and Fernandes (2009) at severity 1.0,
/// applied in linear RGB, and CIE76 distance in CIELAB under D65.
/// </summary>
public static class ColourVision
{
    public enum Vision
    {
        Full,
        Deuteranopia,
        Protanopia,
    }

    private static readonly double[,] Deutan =
    {
        { 0.367322, 0.860646, -0.227968 },
        { 0.280085, 0.672501, 0.047413 },
        { -0.011820, 0.042940, 0.968881 },
    };

    private static readonly double[,] Protan =
    {
        { 0.152286, 1.052583, -0.204868 },
        { 0.114503, 0.786281, 0.099216 },
        { -0.003882, -0.048116, 1.051998 },
    };

    /// <summary>How <paramref name="colour"/> looks with <paramref name="vision"/>.</summary>
    public static Rgb Simulate(Rgb colour, Vision vision)
    {
        if (vision == Vision.Full)
        {
            return colour;
        }

        var m = vision == Vision.Deuteranopia ? Deutan : Protan;
        var r = ToLinear(colour.R);
        var g = ToLinear(colour.G);
        var b = ToLinear(colour.B);
        return new Rgb(
            ToByte(m[0, 0] * r + m[0, 1] * g + m[0, 2] * b),
            ToByte(m[1, 0] * r + m[1, 1] * g + m[1, 2] * b),
            ToByte(m[2, 0] * r + m[2, 1] * g + m[2, 2] * b));
    }

    /// <summary>The CIE76 distance between two colours as seen with <paramref name="vision"/>.</summary>
    public static double Distance(Rgb a, Rgb b, Vision vision = Vision.Full)
    {
        var (l1, a1, b1) = Lab(Simulate(a, vision));
        var (l2, a2, b2) = Lab(Simulate(b, vision));
        return Math.Sqrt((l1 - l2) * (l1 - l2) + (a1 - a2) * (a1 - a2) + (b1 - b2) * (b1 - b2));
    }

    /// <summary>The least distance between two colours over full colour and both simulated deficiencies.</summary>
    public static double WorstDistance(Rgb a, Rgb b) =>
        Enum.GetValues<Vision>().Min(vision => Distance(a, b, vision));

    /// <summary>A colour's CIELAB lightness L* under D65, 0 for black to 100 for white.</summary>
    public static double Lightness(Rgb colour) => Lab(colour).L;

    /// <summary>A colour's CIELAB chroma under D65: how far it sits from grey, whatever its hue.</summary>
    public static double Chroma(Rgb colour)
    {
        var (_, a, b) = Lab(colour);
        return Math.Sqrt(a * a + b * b);
    }

    private static double ToLinear(byte channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static byte ToByte(double linear)
    {
        var c = Math.Clamp(linear, 0, 1);
        var s = c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;
        return (byte)Math.Round(s * 255);
    }

    private static (double L, double A, double B) Lab(Rgb colour)
    {
        var r = ToLinear(colour.R);
        var g = ToLinear(colour.G);
        var b = ToLinear(colour.B);
        var x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047;
        var y = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        var z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883;
        var fx = F(x);
        var fy = F(y);
        var fz = F(z);
        return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    private static double F(double t) => t > 216.0 / 24389 ? Math.Cbrt(t) : (24389.0 / 27 * t + 16) / 116;
}
