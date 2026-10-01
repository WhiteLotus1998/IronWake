using System.Collections.Immutable;

namespace Ironwake.Client;

/// <summary>
/// The footer's keys and what each does, in order (issue 609, Lotus's note that P and S said
/// nothing): every label names what the key does, so S steps the game speed the top bar's
/// buttons set (issue 625), T draws every enemy's reach. P, the priced threat on a tile, is cut
/// (issue 625). The renderer draws them as keycaps; this list is the one place the words live.
/// </summary>
public static class KeyStrip
{
    public static ImmutableArray<(string Key, string Does)> Keys { get; } = ImmutableArray.Create(
        ("click", "select, move, strike"),
        ("E", "end phase"),
        ("Space", "next enemy act"),
        ("C", "skip"),
        ("S", "game speed"),
        ("R", "recall"),
        ("T", "enemy reach"),
        ("Tab", PanelLayout.TabLabel),
        (Sound.MuteKey, "sound"),
        ("Esc", Screens.MenuLabel));
}
