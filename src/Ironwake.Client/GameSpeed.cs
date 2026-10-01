using System.Collections.Immutable;

namespace Ironwake.Client;

/// <summary>
/// The game's speed (issue 625, Lotus's note on #573): buttons at the top bar's right under
/// "Game speed", 1x, 2x and 5x, each drawn as that many filled triangles (one, two, three), and
/// instant (issue 677), three triangles against a bar. <see cref="Factor"/> multiplies every
/// <see cref="Rhythm"/> length, so 2x plays a beat in half its time and instant in a thousandth:
/// every beat still plays and every line still reaches the log, since animation never hides
/// state. S cycles the four; the pick is the profile's <c>speed</c> option.
/// </summary>
public static class GameSpeed
{
    /// <summary>The label over the buttons.</summary>
    public const string Label = "Game speed";

    /// <summary>The speeds in button order: the name (the profile's <c>speed</c> value), the triangles drawn, whether a bar ends them, and the multiplier on a beat's length.</summary>
    public static ImmutableArray<(string Name, int Triangles, bool Bar, float Factor)> Speeds { get; } = ImmutableArray.Create(
        ("1x", 1, false, 1f),
        ("2x", 2, false, 1f / 2),
        ("5x", 3, false, 1f / 5),
        ("instant", 3, true, 1f / 1000));

    /// <summary>The speed a session opens on: 1x.</summary>
    public const int Default = 0;

    /// <summary>The speed after <paramref name="current"/>, as S steps them: 1x, 2x, 5x, instant, then 1x again.</summary>
    public static int Next(int current) => (current + 1) % Speeds.Length;

    /// <summary>The index of the speed the profile names <paramref name="name"/>, or <see cref="Default"/> for a name that is none of them.</summary>
    public static int IndexOf(string name)
    {
        for (var i = 0; i < Speeds.Length; i++)
        {
            if (Speeds[i].Name == name)
            {
                return i;
            }
        }

        return Default;
    }

    /// <summary>The multiplier on a beat's length at speed <paramref name="index"/>.</summary>
    public static float Factor(int index) => Speeds[index].Factor;
}
