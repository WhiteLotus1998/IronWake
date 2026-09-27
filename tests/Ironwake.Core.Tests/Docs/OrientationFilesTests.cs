using System.Text;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Docs;

/// <summary>
/// Issue 401: <c>docs/STATE.md</c> and <c>docs/DIALOGUE.md</c> are the two files a session reads
/// to be current, so CLAUDE.md caps them. Each stays under 20 KB, and DIALOGUE.md under 150
/// lines as well, since a line cap alone is met by longer lines.
/// </summary>
public class OrientationFilesTests
{
    private const int MaxBytes = 20 * 1024;
    private const int MaxDialogueLines = 150;

    private static string Docs => Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs");

    /// <summary>
    /// Every way a text breaks the cap, empty when it keeps it.
    /// </summary>
    private static IReadOnlyList<string> Breaches(string name, string text, int? maxLines)
    {
        var breaches = new List<string>();
        var bytes = Encoding.UTF8.GetByteCount(text);
        if (bytes > MaxBytes)
        {
            breaches.Add($"{name} is {bytes} bytes, over {MaxBytes}");
        }

        var lines = text.Split('\n').Length;
        if (maxLines is { } cap && lines > cap)
        {
            breaches.Add($"{name} is {lines} lines, over {cap}");
        }

        return breaches;
    }

    [Fact]
    public void StateStaysUnderTwentyKilobytes()
    {
        var text = File.ReadAllText(Path.Combine(Docs, "STATE.md"));
        Assert.Empty(Breaches("STATE.md", text, maxLines: null));
    }

    [Fact]
    public void DialogueStaysUnderTwentyKilobytesAndOneHundredFiftyLines()
    {
        var text = File.ReadAllText(Path.Combine(Docs, "DIALOGUE.md"));
        Assert.Empty(Breaches("DIALOGUE.md", text, MaxDialogueLines));
    }

    [Fact]
    public void ACapMetByLongerLinesStillFails()
    {
        var longLines = string.Join('\n', Enumerable.Repeat(new string('x', 9049), 3));
        var breaches = Breaches("DIALOGUE.md", longLines, MaxDialogueLines);
        Assert.Equal(new[] { $"DIALOGUE.md is {9049 * 3 + 2} bytes, over {MaxBytes}" }, breaches);
    }

    [Fact]
    public void ALineCapBreachIsNamed()
    {
        var manyLines = string.Join('\n', Enumerable.Repeat("x", MaxDialogueLines + 1));
        var breaches = Breaches("DIALOGUE.md", manyLines, MaxDialogueLines);
        Assert.Equal(new[] { $"DIALOGUE.md is {MaxDialogueLines + 1} lines, over {MaxDialogueLines}" }, breaches);
    }
}
