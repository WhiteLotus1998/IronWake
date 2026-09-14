namespace Ironwake.Content;

/// <summary>
/// A content file failed to load. The message always names the file, and the entry and
/// field when the failure is inside one, so the fix is obvious without a debugger.
/// </summary>
public sealed class ContentException : Exception
{
    public ContentException(string file, string? entry, string? field, string problem)
        : base(Format(file, entry, field, problem))
    {
        File = file;
        Entry = entry;
        Field = field;
        Problem = problem;
    }

    public string File { get; }

    public string? Entry { get; }

    public string? Field { get; }

    public string Problem { get; }

    private static string Format(string file, string? entry, string? field, string problem)
    {
        var where = file;
        if (entry is not null)
        {
            where += " > entry '" + entry + "'";
        }

        if (field is not null)
        {
            where += " > field '" + field + "'";
        }

        return where + ": " + problem;
    }
}
