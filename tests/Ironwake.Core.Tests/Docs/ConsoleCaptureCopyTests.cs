namespace Ironwake.Core.Tests.Docs;

/// <summary>
/// <see cref="ConsoleCapture"/> copies each capture into <c>IRONWAKE_CAPTURE_DIR</c> when the variable
/// names a directory, which is what <c>tools/rejournal.py</c> reads, and writes nothing otherwise.
/// </summary>
[Collection("console")]
public sealed class ConsoleCaptureCopyTests
{
    [Fact]
    public void ACaptureIsCopiedIntoTheCaptureDirectoryWhenTheVariableIsSet()
    {
        var dir = Directory.CreateTempSubdirectory("iw-capture-").FullName;
        var previous = Environment.GetEnvironmentVariable(ConsoleCapture.CaptureDirVariable);
        try
        {
            Environment.SetEnvironmentVariable(ConsoleCapture.CaptureDirVariable, dir);
            ConsoleCapture.Run(() => Console.WriteLine("hello"));

            Assert.Equal("hello\n", File.ReadAllText(Assert.Single(Directory.GetFiles(dir))));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConsoleCapture.CaptureDirVariable, previous);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void NoCopyIsWrittenWhenTheVariableNamesNoDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "iw-capture-missing-" + Guid.NewGuid().ToString("N"));
        var previous = Environment.GetEnvironmentVariable(ConsoleCapture.CaptureDirVariable);
        try
        {
            Environment.SetEnvironmentVariable(ConsoleCapture.CaptureDirVariable, dir);

            Assert.Equal("hello\n", ConsoleCapture.Run(() => Console.WriteLine("hello")));
            Assert.False(Directory.Exists(dir));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConsoleCapture.CaptureDirVariable, previous);
        }
    }
}
