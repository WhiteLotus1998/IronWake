namespace Ironwake.Client;

/// <summary>
/// Fits the console's lines to a panel of fixed width (issue 349), so a long forecast, threat
/// or shop line wraps inside the window instead of running past its edge. The text itself is
/// never changed: a line is only broken at spaces, and a word longer than the width is cut.
/// </summary>
public static class TextLayout
{
    /// <summary>
    /// Breaks <paramref name="line"/> into rows of at most <paramref name="columns"/> characters.
    /// Each continuation row keeps the line's own indent plus two spaces, so a wrapped row reads
    /// as part of the line above it. A line that fits comes back as it is.
    /// </summary>
    public static IEnumerable<string> Wrap(string line, int columns)
    {
        if (columns < 8)
        {
            throw new ArgumentOutOfRangeException(nameof(columns), columns, "a panel narrower than eight columns cannot wrap");
        }

        if (line.Length <= columns)
        {
            yield return line;
            yield break;
        }

        var indent = new string(' ', Math.Min(line.Length - line.TrimStart(' ').Length + 2, columns / 2));
        var rest = line;
        var prefix = "";
        while (prefix.Length + rest.Length > columns)
        {
            var room = columns - prefix.Length;
            var cut = rest.LastIndexOf(' ', room);
            if (cut <= 0)
            {
                cut = room;
            }

            yield return prefix + rest[..cut].TrimEnd();
            rest = rest[cut..].TrimStart(' ');
            prefix = indent;
        }

        if (rest.Length > 0)
        {
            yield return prefix + rest;
        }
    }
}
