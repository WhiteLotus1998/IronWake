using Ironwake.Core;

namespace Ironwake.Client;

/// <summary>
/// Reads a <c>play --script</c> file into the commands the client submits (issue 347), so the
/// parity gate drives the client and the console from the same file. Only lines that change
/// the board become commands; a query (<c>forecast</c>, <c>threat</c>, <c>show</c>, <c>reach</c>,
/// <c>map</c>, <c>help</c>, a bare <c>recall</c> or <c>recall list</c>), a blank line or a
/// <c>#</c> comment is skipped, as none of them prints an event. Slots count from 1, as the
/// console reads them.
/// </summary>
public static class Script
{
    /// <summary>The command a script line names on the board as it stands, or null for a line that prints no event.</summary>
    public static Command? Parse(string line, BattleState state)
    {
        var words = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || words[0].StartsWith('#'))
        {
            return null;
        }

        string? art = null;
        if (words[0] == "attack" && words.Length > 3)
        {
            var at = Array.IndexOf(words, "art", 3);
            if (at >= 0 && at + 1 < words.Length)
            {
                art = words[at + 1];
                words = words.Take(at).Concat(words.Skip(at + 2)).ToArray();
            }
        }

        return (words[0], words.Length) switch
        {
            ("move", 3) when TryCoord(words[2], out var to) => new Move(words[1], to),
            ("attack", 3) => new Attack(words[1], words[2], null, art),
            ("attack", 4) when int.TryParse(words[3], out var slot) => new Attack(words[1], words[2], slot - 1, art),
            ("item", 3 or 4) when int.TryParse(words[2], out var slot) => new UseItem(words[1], slot - 1, words.Length == 4 ? words[3] : null),
            ("wait", 2) => new Wait(words[1]),
            ("canto", 3) when words[2] == "stay" && state.Find(words[1]) is { } stayer => new Canto(stayer.Id, stayer.At),
            ("canto", 3) when TryCoord(words[2], out var to) => new Canto(words[1], to),
            ("exit", 2) => new Exit(words[1]),
            ("recover", 2) => new Recover(words[1]),
            ("shove", 3) => new Shove(words[1], words[2]),
            ("end", 1) => new EndPhase(),
            ("recall", 2) when int.TryParse(words[1], out var index) => new Recall(index),
            _ => null,
        };
    }

    /// <summary>
    /// Plays a script through a fresh client and returns its event log: each command is
    /// submitted as the player would click it, and each enemy phase is stepped to its end.
    /// </summary>
    public static string Play(GameContent content, BattleState state, string script)
    {
        var client = new ClientSession(content, state);
        foreach (var line in script.Split('\n'))
        {
            if (Parse(line, client.State) is { } command)
            {
                client.Submit(command);
                client.Continue();
            }
        }

        return client.LogText;
    }

    private static bool TryCoord(string text, out Coord at)
    {
        at = default;
        var parts = text.Split(',');
        if (parts.Length == 2 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y))
        {
            at = new Coord(x, y);
            return true;
        }

        return false;
    }
}
