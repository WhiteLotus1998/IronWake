using System.Text.RegularExpressions;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Docs;

/// <summary>
/// Issue 1291: a decision record is cited by its number, so two records with one number leave a
/// citation that names neither. Two PRs open at once each took the next free number twice before
/// this test (0256 and 0264); those two pairs stay, allowed by full name, and any other repeat
/// fails. <c>tools/next_decision.py</c> reads open branches too, so a new record stays clear of
/// one still in review.
/// </summary>
public class DecisionRecordTests
{
    /// <summary>
    /// The pairs that predate the rule. Renumbering one of each would move every citation of it,
    /// which is the Table's call; until then they are named here, file by file.
    /// </summary>
    private static readonly IReadOnlySet<string> AllowedRepeats = new HashSet<string>
    {
        "0256-the-wind-kept-on-its-sample.md",
        "0256-wrens-talk-sample.md",
        "0264-a-guard-boss-on-his-post-holds.md",
        "0264-pillar-5-a-lightning-mage-and-the-schools.md",
    };

    private static readonly Regex Numbered = new(@"^(\d{4})-.*\.md$", RegexOptions.CultureInvariant);

    private static string Decisions =>
        Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "DECISIONS");

    /// <summary>
    /// One line per number that more than one record carries, empty when every number is unique.
    /// A repeat made only of allowed files passes; a third file on an allowed number does not.
    /// </summary>
    private static IReadOnlyList<string> Repeats(IEnumerable<string> fileNames) =>
        fileNames
            .Select(name => (name, match: Numbered.Match(name)))
            .Where(x => x.match.Success)
            .GroupBy(x => x.match.Groups[1].Value)
            .Where(g => g.Count() > 1 && !g.All(x => AllowedRepeats.Contains(x.name)))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key} is taken by {string.Join(", ", g.Select(x => x.name).OrderBy(n => n, StringComparer.Ordinal))}")
            .ToList();

    [Fact]
    public void DecisionRecordNumbersAreUnique()
    {
        var names = Directory.GetFiles(Decisions).Select(Path.GetFileName).OfType<string>();
        Assert.Empty(Repeats(names));
    }

    [Fact]
    public void ANewRepeatedNumberFails()
    {
        var repeats = Repeats(new[] { "0310-one.md", "0310-two.md", "0311-three.md" });
        Assert.Equal(new[] { "0310 is taken by 0310-one.md, 0310-two.md" }, repeats);
    }

    [Fact]
    public void AThirdRecordOnAnAllowedNumberFails()
    {
        var repeats = Repeats(AllowedRepeats.Append("0264-a-third.md"));
        Assert.Equal(
            new[] { "0264 is taken by 0264-a-guard-boss-on-his-post-holds.md, 0264-a-third.md, 0264-pillar-5-a-lightning-mage-and-the-schools.md" },
            repeats);
    }

    [Fact]
    public void EveryAllowedRepeatIsStillOnDisk()
    {
        var names = Directory.GetFiles(Decisions).Select(Path.GetFileName).ToHashSet();
        Assert.All(AllowedRepeats, name => Assert.Contains(name, names));
    }
}
