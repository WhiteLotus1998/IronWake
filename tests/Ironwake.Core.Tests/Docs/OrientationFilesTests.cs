using System.Text;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Docs;

/// <summary>
/// Issue 401: <c>docs/STATE.md</c> and <c>docs/DIALOGUE.md</c> are the two files a session reads
/// to be current, so CLAUDE.md caps them. Each stays under 20 KB, and DIALOGUE.md under 150
/// lines as well, since a line cap alone is met by longer lines.
/// Issue 1291: two green PRs that each fit can merge into a main that does not, since branch
/// protection does not make a branch re-run against the newest main. So the 20 KB is the hard
/// cap only where the merge is tested, a push to main; on a pull request and on a local run the
/// working cap is 19 KB, and the 1 KB between them is room for merge skew.
/// </summary>
public class OrientationFilesTests
{
    private const int MaxBytes = 20 * 1024;
    private const int WorkingMaxBytes = 19 * 1024;
    private const int MaxDialogueLines = 150;

    private static string Docs => Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs");

    /// <summary>
    /// The byte cap for a run: the hard cap on a push (GitHub Actions' event name for the commit
    /// a merge lands on main), the working cap anywhere else.
    /// </summary>
    private static int ByteCap(string? githubEventName) =>
        githubEventName == "push" ? MaxBytes : WorkingMaxBytes;

    private static int ByteCapHere => ByteCap(Environment.GetEnvironmentVariable("GITHUB_EVENT_NAME"));

    /// <summary>
    /// Every way a text breaks the cap, empty when it keeps it.
    /// </summary>
    private static IReadOnlyList<string> Breaches(string name, string text, int? maxLines, int maxBytes = MaxBytes)
    {
        var breaches = new List<string>();
        var bytes = Encoding.UTF8.GetByteCount(text);
        if (bytes > maxBytes)
        {
            breaches.Add($"{name} is {bytes} bytes, over {maxBytes}");
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
        Assert.Empty(Breaches("STATE.md", text, maxLines: null, ByteCapHere));
    }

    [Fact]
    public void DialogueStaysUnderTwentyKilobytesAndOneHundredFiftyLines()
    {
        var text = File.ReadAllText(Path.Combine(Docs, "DIALOGUE.md"));
        Assert.Empty(Breaches("DIALOGUE.md", text, MaxDialogueLines, ByteCapHere));
    }

    [Theory]
    [InlineData("push", MaxBytes)]
    [InlineData("pull_request", WorkingMaxBytes)]
    [InlineData(null, WorkingMaxBytes)]
    [InlineData("workflow_dispatch", WorkingMaxBytes)]
    public void TheHardCapHoldsOnlyOnAPushToMain(string? eventName, int expected)
    {
        Assert.Equal(expected, ByteCap(eventName));
    }

    [Fact]
    public void ABranchWithoutMergeHeadroomFails()
    {
        var text = new string('x', WorkingMaxBytes + 1);
        Assert.Equal(
            new[] { $"STATE.md is {WorkingMaxBytes + 1} bytes, over {WorkingMaxBytes}" },
            Breaches("STATE.md", text, maxLines: null, ByteCap("pull_request")));
        Assert.Empty(Breaches("STATE.md", text, maxLines: null, ByteCap("push")));
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
