using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The console's side of issue 871: on Sallow Grange, the one map carrying <c>keziah_warning: on</c>,
/// the camp says so before seats are chosen, a bare <c>march</c> with Keziah deployed prints Lotus's
/// line and stays on the screen, and only <c>march sure</c> or benching her marches. A strict script
/// stops on the bare march and names its line.
/// </summary>
[Collection("console")]
public class KeziahWarningCliTests
{
    private const string Question = "This map is not ideal for Keziah. Are you sure you want to continue with her?";

    /// <summary>Benches the back of Sallow's order so Keziah deploys.</summary>
    private const string Seat = "bench dunstan\nbench maud\n";

    private static string Run(string script, params string[] extra)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-warning-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, script);
        try
        {
            var args = new[] { "campaign", "--from", "sallow_grange", "--pick", "keziah", "--seed", "871", "--script", path }
                .Concat(extra).Concat(new[] { "--content", Fixture.RealContentDirectory() }).ToArray();
            return ConsoleCapture.Run(() => Ironwake.Cli.Program.Main(args));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheCampSaysSoBeforeSeatsAreChosen()
    {
        var output = Run("roster\n");

        Assert.Contains("This map is not ideal for Keziah; march asks first if Keziah deploys (bench keziah to leave her).", output);
    }

    [Fact]
    public void ABareMarchWithHerDeployedPrintsLotussLineAndStaysOnTheScreen()
    {
        var output = Run(Seat + "march\nmarch sure\n");

        Assert.Contains("> march\nERROR: " + Question + " (march sure, or bench the unit)\n> march sure\n", output.Replace("\r\n", "\n"));
        Assert.Contains("Sallow Grange", output[output.IndexOf("> march sure", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void ASecondBareMarchIsNotAConfirm()
    {
        var output = Run(Seat + "march\nmarch\n");

        Assert.Contains("  line 4: march: " + Question, output);
        Assert.Contains("Campaign stopped before Sallow Grange: the script ended on the screen", output);
    }

    [Fact]
    public void BenchingHerMarchesWithoutTheQuestion()
    {
        var output = Run("march\n");

        Assert.DoesNotContain(Question, output);
    }

    [Fact]
    public void AStrictScriptStopsOnTheBareMarchAndNamesItsLine()
    {
        var output = Run(Seat + "march\nmarch sure\n", "--strict");

        Assert.Contains(Question, output);
        Assert.Contains("Strict: stopped at line 3 (march); no later command applied", output);
        Assert.DoesNotContain("> march sure", output);
    }
}
