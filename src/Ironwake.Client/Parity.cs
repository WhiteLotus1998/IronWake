namespace Ironwake.Client;

/// <summary>
/// The thin renderer's parity gate (issue 347): the client's event log against the console's
/// (<c>ironwake play --log</c>) for the same map, seed and script, byte for byte.
/// </summary>
public static class Parity
{
    /// <summary>
    /// Null when the two logs are the same bytes; otherwise where they first differ, as the
    /// one-based line and column and both lines, so a failing gate names the event that
    /// fell behind.
    /// </summary>
    public static string? FirstDifference(string console, string client)
    {
        if (string.Equals(console, client, StringComparison.Ordinal))
        {
            return null;
        }

        var at = 0;
        while (at < console.Length && at < client.Length && console[at] == client[at])
        {
            at++;
        }

        var line = 1 + console.Take(at).Count(c => c == '\n');
        var start = console.LastIndexOf('\n', Math.Max(0, at - 1)) + 1;
        if (at > 0 && console[at - 1] == '\n')
        {
            start = at;
        }

        return $"event log differs at line {line}, column {at - start + 1}: console '{LineAt(console, start)}', client '{LineAt(client, start)}'";
    }

    private static string LineAt(string text, int start)
    {
        if (start >= text.Length)
        {
            return "(end of log)";
        }

        var end = text.IndexOf('\n', start);
        return end < 0 ? text[start..] : text[start..end];
    }
}
