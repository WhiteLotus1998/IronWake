using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Chat's cold chair feeding Teodor through the main line (round 399, campaign seed 3971, maps 1
/// to 6), replayed on the drill that keeps EXP (DECISIONS/0276). The play is unchanged from the
/// build it was played on: Teodor reaches L7 on the raid, the drill never touches him, and the
/// Halberdier door is open at the camp before Sallow Grange. Under 0276 Pell keeps 84 EXP at the
/// camp after Harrow Weir and levels on his first strike of the raid. The Mill was played before
/// its <c>holds:</c> header (issue 1189), so the replay reads a copy of the content with the Mill
/// as it was then, kept as <c>docs/samples/the_mill_0278.map</c>.
/// </summary>
[Collection("console")]
public class Campaign3971ColdChairReplayTests
{
    [Fact]
    public void TheColdChairFeedingTeodorOnSeed3971OpensHisDoorAfterTheRaidWithoutTheDrill()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-06-campaign-3971.script");
        var content = Path.Combine(Path.GetTempPath(), "ironwake-3971-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(Fixture.RealContentDirectory(), content);
        File.Copy(Path.Combine(root, "docs", "samples", "the_mill_0278.map"), Path.Combine(content, "maps", "the_mill.map"), overwrite: true);
        var args = new[] { "campaign", "--seed", "3971", "--script", script, "--content", content };

        string output;
        try
        {
            output = ConsoleCapture.Run(() => Program.Main(args));
        }
        finally
        {
            Directory.Delete(content, recursive: true);
        }

        Assert.Contains("Raid on Ironwake won: rout; reward 1000, the purse holds 2790; nobody fell\n", output);
        Assert.Contains("Teodor reaches level 7: hp +1\n", output);
        Assert.Contains("Halberdier (from Pikeman; adds axe): level 7, lance 50 (between D and C) -- Teodor may be promoted\n", output);
        Assert.DoesNotContain("Teodor drilled", output);
        Assert.Contains("Pell drilled with the levy: L2 -> L3, 84 EXP kept.\n", output);
        Assert.Contains("Pell gains 16 exp (0)\nPell reaches level 4: hp +1 cha +1\n", output);
        Assert.Contains("Rejected 1 of 457 commands:\n", output);
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(from))
        {
            CopyDirectory(directory, Path.Combine(to, Path.GetFileName(directory)));
        }
    }
}
