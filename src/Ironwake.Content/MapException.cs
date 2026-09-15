namespace Ironwake.Content;

/// <summary>
/// A map file failed to parse or validate. The message names the file and the 1-based
/// line, so the fix is one jump away in any editor. Line 0 means the problem is with the
/// file as a whole (empty, or missing a block).
/// </summary>
public sealed class MapException : Exception
{
    public MapException(string file, int line, string problem)
        : base(line > 0 ? $"{file}, line {line}: {problem}" : $"{file}: {problem}")
    {
        File = file;
        Line = line;
        Problem = problem;
    }

    public string File { get; }

    public int Line { get; }

    public string Problem { get; }
}
