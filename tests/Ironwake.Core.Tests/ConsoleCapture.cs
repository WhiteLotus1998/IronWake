namespace Ironwake.Core.Tests;

/// <summary>
/// The one way a test reads console output (issue 132). The CLI and the Sim write the
/// platform's line ending, so the capture normalises it to <c>\n</c>, which every
/// expectation is written with; a test that captured the console on its own would pass
/// on Linux and fail on Windows, as three did (issue 64 before it). Tests that use it
/// share the process-wide console and must carry <c>[Collection("console")]</c>.
/// When the environment variable <c>IRONWAKE_CAPTURE_DIR</c> names a directory, every capture
/// is also written there as its own file, so <c>tools/rejournal.py</c> can regenerate the
/// journaled transcripts after a change to what the console prints (see that tool).
/// </summary>
internal static class ConsoleCapture
{
    /// <summary>The variable that turns on <see cref="Run"/>'s copy of each capture.</summary>
    public const string CaptureDirVariable = "IRONWAKE_CAPTURE_DIR";

    /// <summary>
    /// Runs <paramref name="run"/> with <see cref="Console.Out"/> captured, from
    /// <paramref name="workingDirectory"/> when one is given, restoring both afterwards,
    /// and returns what it wrote with line endings normalised.
    /// </summary>
    public static string Run(Action run, string? workingDirectory = null)
    {
        var previous = Console.Out;
        var cwd = Directory.GetCurrentDirectory();
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            if (workingDirectory is not null)
            {
                Directory.SetCurrentDirectory(workingDirectory);
            }

            run();
        }
        finally
        {
            Directory.SetCurrentDirectory(cwd);
            Console.SetOut(previous);
        }

        var captured = writer.ToString().Replace("\r\n", "\n");
        if (Environment.GetEnvironmentVariable(CaptureDirVariable) is { Length: > 0 } dir && Directory.Exists(dir))
        {
            File.WriteAllText(Path.Combine(dir, Guid.NewGuid().ToString("N") + ".txt"), captured);
        }

        return captured;
    }
}
