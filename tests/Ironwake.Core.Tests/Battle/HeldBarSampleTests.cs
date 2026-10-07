using Ironwake.Cli;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 1259 on its sample, <c>docs/samples/the_lazar_house_held.map</c>, played as Maud's quest 1 through
/// the campaign with the sample in the quest map's place: Code's warm play, seed 1259, at turn 3 with
/// Teodor holding the north bar and a brigand waiting behind it. <c>threat</c> from the bar prices the lane
/// shut; from any tile off it, the next waiting arrival lands, and only the one.
/// </summary>
[Collection("console")]
public sealed class HeldBarSampleTests
{
    private static readonly string[] Turn3 =
    {
        "quest maud_1 teodor", "move teodor 5,2", "wait teodor", "wait maud", "end",
        "move maud 5,1", "attack maud brigand-1", "move teodor 8,1", "wait teodor", "end",
    };

    private static string Play(params string[] queries)
    {
        var root = Path.Combine(Path.GetTempPath(), "ironwake-1259-" + Guid.NewGuid().ToString("N"));
        var content = Path.Combine(root, "content");
        Copy(Fixture.RealContentDirectory(), content);
        File.Copy(SamplePath(), Path.Combine(content, "quests", "the_lazar_house.map"), overwrite: true);
        var script = Path.Combine(root, "play.script");
        File.WriteAllLines(script, Turn3.Concat(queries));
        try
        {
            return ConsoleCapture.Run(() => Program.Main(new[] { "campaign", "--from", "the_tollgate", "--seed", "1259", "--script", script, "--content", content }));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string SamplePath() =>
        Path.Combine(Fixture.RealContentDirectory(), "..", "docs", "samples", "the_lazar_house_held.map");

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }

        foreach (var dir in Directory.GetDirectories(from))
        {
            Copy(dir, Path.Combine(to, Path.GetFileName(dir)));
        }
    }

    private static string Read(string output, string header) => output[output.IndexOf(header, StringComparison.Ordinal)..];

    [Fact]
    public void TheBoardNamesTheHolderAndWhatWaitsBehindTheBar()
    {
        var output = Play();

        Assert.Contains("Brigand waits at 8,0 (wall); it lands at the first enemy phase that starts with 8,0 open", output);
        Assert.Contains("north bar (8,0): barred while teodor holds 8,1; waiting: brigand\n", Read(output, "-- Player phase, turn 3 --"));
    }

    [Fact]
    public void ThreatFromTheBarPricesTheLaneShut()
    {
        var read = Read(Play("threat teodor"), "Threat on Teodor at 8,1");

        Assert.DoesNotContain("Brigand 2", read);
        Assert.DoesNotContain("Hexer", read);
    }

    [Fact]
    public void ThreatFromATileOffTheBarPricesTheWaitingArrivalLandingAndOnlyTheOldest()
    {
        var read = Read(Play("threat teodor from 7,1"), "Threat on Teodor at 7,1");

        Assert.Contains("Brigand 2 (arrives this enemy phase at 8,0) from 7,0", read);
        Assert.DoesNotContain("Hexer", read);
    }

    [Fact]
    public void SteppingOffTheBarPrintsThatItGivesWay()
    {
        var output = Play("move teodor 8,2");

        Assert.Contains("North bar gives way: nobody holds 8,1\n  8,0 becomes Plain", output);
        Assert.Contains("north bar (8,0): open, barred only while one of yours holds 8,1; waiting: brigand", Read(output, "> move teodor 8,2"));
    }
}
