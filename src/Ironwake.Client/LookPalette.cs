namespace Ironwake.Client;

/// <summary>
/// The showcase's palette (issue 510, <c>docs/LOOK.md</c>): a cold, muted world, the player's
/// side the only warm thing on the board, and the enemy inside the world's cold. These are
/// the literal values <c>docs/LOOK.md</c> lists and <c>docs/look/*.svg</c> draws with; the
/// palette tests hold all three to each other and to the same colour-vision checks as
/// <see cref="Palette"/>. The Godot client draws with it since slice 1 (issue 511).
/// </summary>
public static class LookPalette
{
    /// <summary>The greatest CIELAB chroma any world or enemy colour may carry, so warmth stays the player's.</summary>
    public const double WorldChromaCeiling = 32;

    /// <summary>The least CIELAB chroma of the player's colour.</summary>
    public const double PlayerChromaFloor = 55;

    /// <summary>The least CIE76 distance between two marks, in full colour and under each simulated deficiency.</summary>
    public const double MarkSeparation = 12;

    /// <summary>
    /// The terrain colours by terrain id. Fire is the one warm world colour and is never drawn
    /// as a flat fill: LOOK.md draws it hatched over its ground, so a burning tile never reads
    /// as one of ours.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Rgb> Terrain = new Dictionary<string, Rgb>
    {
        ["plain"] = Hex("7E9470"),
        ["road"] = Hex("B3AE9C"),
        ["forest"] = Hex("4F6E54"),
        ["hill"] = Hex("C8C8A0"),
        ["mountain"] = Hex("77767C"),
        ["water"] = Hex("41667F"),
        ["fort"] = Hex("9FB0C4"),
        ["wall"] = Hex("23272E"),
        ["throne"] = Hex("E4E7EA"),
        ["fire"] = Hex("943C0C"),
    };

    /// <summary>The terrain drawn hatched rather than filled, exempt from the world's chroma ceiling.</summary>
    public const string HatchedTerrain = "fire";

    /// <summary>The player's token fill: lamplight amber.</summary>
    public static readonly Rgb Player = Hex("E8A33D");

    /// <summary>The player's token base and shadow.</summary>
    public static readonly Rgb PlayerDeep = Hex("9A6420");

    /// <summary>The enemy's token fill: slate, inside the world's cold.</summary>
    public static readonly Rgb Enemy = Hex("2F3742");

    /// <summary>The enemy's silhouette and rim: bone.</summary>
    public static readonly Rgb EnemyBone = Hex("E6E0D0");

    /// <summary>The board's marks by name: the selected unit, its reach, the enemy's threat, and a unit just struck.</summary>
    public static readonly IReadOnlyDictionary<string, Rgb> Marks = new Dictionary<string, Rgb>
    {
        ["selected"] = Hex("F6D38A"),
        ["reach"] = Hex("BFD9EA"),
        ["threat"] = Hex("EDE6D6"),
        ["struck"] = Hex("FFFFFF"),
    };

    /// <summary>
    /// How far a ground's red may pass its blue before it counts as warm (issue 578, round 166):
    /// the old hill `#B89E6C` (76) was warm and turned the bone hatch peach; since round 170 no
    /// ground but fire is, so the hatch is bone everywhere (issue 564).
    /// </summary>
    public const int WarmGroundMargin = 40;

    /// <summary>Whether a ground colour is warm: its red passes its blue by more than <see cref="WarmGroundMargin"/>.</summary>
    public static bool IsWarmGround(Rgb colour) => colour.R - colour.B > WarmGroundMargin;

    /// <summary>The marks that belong to the player's side, and so may be warm.</summary>
    public static readonly IReadOnlySet<string> PlayerMarks = new HashSet<string> { "selected" };

    /// <summary>The screen around the board: ink behind, panels, text, muted text, and the lost part of an HP bar.</summary>
    public static readonly IReadOnlyDictionary<string, Rgb> Ui = new Dictionary<string, Rgb>
    {
        ["ink"] = Hex("15181D"),
        ["panel"] = Hex("1E232A"),
        ["text"] = Hex("E9ECEF"),
        ["muted"] = Hex("8C96A3"),
        ["lost"] = Hex("5A6270"),
    };

    /// <summary>Every named token with its value, as LOOK.md's table lists them.</summary>
    public static IEnumerable<(string Name, Rgb Colour)> Tokens =>
        Terrain.Select(t => ($"terrain.{t.Key}", t.Value))
            .Append(("player", Player))
            .Append(("player.deep", PlayerDeep))
            .Append(("enemy", Enemy))
            .Append(("enemy.bone", EnemyBone))
            .Concat(Marks.Select(m => ($"mark.{m.Key}", m.Value)))
            .Concat(Ui.Select(u => ($"ui.{u.Key}", u.Value)));

    /// <summary>The terrains a unit can stand on, which each side colour must stand clear of.</summary>
    public static IEnumerable<string> Standable => Terrain.Keys.Where(id => id != "wall");

    /// <summary>A colour's six-digit upper-case hex, without the hash.</summary>
    public static string ToHex(Rgb colour) => $"{colour.R:X2}{colour.G:X2}{colour.B:X2}";

    private static Rgb Hex(string hex) => new(
        Convert.ToByte(hex[..2], 16),
        Convert.ToByte(hex[2..4], 16),
        Convert.ToByte(hex[4..], 16));
}
