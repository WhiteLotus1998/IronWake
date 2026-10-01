using System.Collections.Immutable;

namespace Ironwake.Client;

/// <summary>
/// The footer's keys and what each does, in order (issue 609, Lotus's note that P and S said
/// nothing): every label names what the key does, so P is the threat on the hovered tile, S the
/// enemy phase's speed, T every enemy's reach. The renderer draws them as keycaps; this list is
/// the one place the words live.
/// </summary>
public static class KeyStrip
{
    public static ImmutableArray<(string Key, string Does)> Keys { get; } = ImmutableArray.Create(
        ("click", "select, move, strike"),
        ("E", "end phase"),
        ("Space", "next enemy act"),
        ("C", "skip"),
        ("S", "enemy speed"),
        ("R", "recall"),
        ("T", "enemy reach"),
        ("P", "threat on tile"),
        ("Tab", PanelLayout.TabLabel),
        (Sound.MuteKey, "sound"),
        ("Esc", Screens.MenuLabel));
}
