using System.Collections.Immutable;

namespace Ironwake.Client;

/// <summary>
/// The game's speed (issue 625, Lotus's note on #573): three buttons at the top bar's right
/// under "Game speed", 1x, 2x and 5x, each drawn as that many filled triangles (one, two,
/// three). <see cref="Factor"/> multiplies every <see cref="Rhythm"/> length, so 2x plays a beat
/// in half its time. S cycles the same three; the pick lasts the session.
/// </summary>
public static class GameSpeed
{
    /// <summary>The label over the buttons.</summary>
    public const string Label = "Game speed";

    /// <summary>The speeds in button order: the name, the triangles drawn, and the multiplier on a beat's length.</summary>
    public static ImmutableArray<(string Name, int Triangles, float Factor)> Speeds { get; } = ImmutableArray.Create(
        ("1x", 1, 1f),
        ("2x", 2, 1f / 2),
        ("5x", 3, 1f / 5));

    /// <summary>The speed a session opens on: 1x.</summary>
    public const int Default = 0;

    /// <summary>The speed after <paramref name="current"/>, as S steps them: 1x, 2x, 5x, then 1x again.</summary>
    public static int Next(int current) => (current + 1) % Speeds.Length;

    /// <summary>The multiplier on a beat's length at speed <paramref name="index"/>.</summary>
    public static float Factor(int index) => Speeds[index].Factor;
}
