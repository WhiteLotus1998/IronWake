using Ironwake.Cli;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 1259's held bars, shipped on the Lazar House at five turns (issue 1266), played as Maud's quest 1
/// through the campaign: Code's warm play, seed 1259, at turn 3 with
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
        var script = Path.Combine(Path.GetTempPath(), "ironwake-1259-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllLines(script, Turn3.Concat(queries));
        try
        {
            return ConsoleCapture.Run(() => Program.Main(new[] { "campaign", "--from", "the_tollgate", "--seed", "1259", "--script", script, "--content", Fixture.RealContentDirectory() }));
        }
        finally
        {
            File.Delete(script);
        }
    }

    private static string Read(string output, string header) => output[output.IndexOf(header, StringComparison.Ordinal)..];

    /// <summary>
    /// Chat's cold chair on the sample with Ottilie (seed 4311, round 424's answer): one Recall to the start,
    /// neither bar ever stood on, and Maud falls on the fort in turn 4's enemy phase.
    /// </summary>
    [Fact]
    public void ChatsColdChairWithOttilieLosesOnTurnFourWithoutStandingOnABar()
    {
        var script = Path.Combine(Fixture.RealContentDirectory(), "..", "docs", "transcripts", "2026-10-07-the_lazar_house_held-4311-chat.script");
        var output = ConsoleCapture.Run(() => Program.Main(new[] { "campaign", "--from", "the_tollgate", "--seed", "4311", "--script", script, "--strict", "--content", Fixture.LazarHouseHeldSampleContentDirectory() }));

        Assert.Contains("Maud falls at 3,2\n", output);
        Assert.DoesNotContain("barred while", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Code's warm play (seed 1259) replayed on the shipped map at five turns and ended at the win (issue 1266):
    /// Teodor falls holding the north bar on turn 3, and Maud lives the turn-5 enemy phase at 2 hp on the fort.
    /// </summary>
    [Fact]
    public void TheWarmPlayAtFiveTurnsWinsWithTeodorFallenHoldingTheBar()
    {
        var script = Path.Combine(Fixture.RealContentDirectory(), "..", "docs", "transcripts", "2026-10-07-the_lazar_house-1259-limit5.script");
        var output = ConsoleCapture.Run(() => Program.Main(new[] { "campaign", "--from", "the_tollgate", "--seed", "1259", "--script", script, "--strict", "--content", Fixture.RealContentDirectory() }));

        Assert.Contains("Teodor falls at 8,1\n", output);
        Assert.Contains("Maud wins maud_1; the stores take 2 common material; fallen for good: Teodor\n", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

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
