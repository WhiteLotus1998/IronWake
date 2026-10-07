using Ironwake.Cli;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 1258: <c>threat &lt;unit&gt; from &lt;x,y&gt;</c> reads the board the stop would leave, so an
/// enter event the stop fires is applied before arrivals are seated. On the Lazar House a stop on
/// 8,1 walls 8,0, and the brigand announced to arrive there is blocked, so the read from 8,1 leaves
/// it out. The board is Chat's cold play, seed 4207, turn 2, before anyone moves.
/// </summary>
[Collection("console")]
public sealed class BarredLaneThreatTests
{
    private static string Turn2(params string[] queries)
    {
        var script = Path.Combine(Path.GetTempPath(), "ironwake-1258-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllLines(script, new[] { "quest maud_1 teodor", "move teodor 5,2", "wait teodor", "wait maud", "end" }.Concat(queries));
        try
        {
            return ConsoleCapture.Run(() => Program.Main(new[] { "campaign", "--from", "the_tollgate", "--seed", "4207", "--script", script, "--content", Fixture.RealContentDirectory() }));
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Fact]
    public void ThreatFromABarTileLeavesOutTheArrivalTheBarBlocks()
    {
        var output = Turn2("threat teodor from 8,1");
        var read = output[output.IndexOf("Threat on Teodor at 8,1", StringComparison.Ordinal)..];

        Assert.DoesNotContain("Brigand 2", read);
        Assert.Contains("  Brigand 1 from 7,1 with Iron Axe (slot 1):", read);
        Assert.Contains("  If all land: 15 against 21 hp\n", read);
    }

    [Fact]
    public void ThreatFromATileOffTheBarStillPricesTheArrival()
    {
        var output = Turn2("threat teodor from 7,1");
        var read = output[output.IndexOf("Threat on Teodor at 7,1", StringComparison.Ordinal)..];

        Assert.Contains("Brigand 2 (arrives this enemy phase at 8,0)", read);
    }

    [Fact]
    public void TheReadDoesNotSpendTheBar()
    {
        var output = Turn2("threat teodor from 8,1", "threat teodor from 7,1");
        var read = output[output.IndexOf("Threat on Teodor at 7,1", StringComparison.Ordinal)..];

        Assert.Contains("Brigand 2 (arrives this enemy phase at 8,0)", read);
    }
}
