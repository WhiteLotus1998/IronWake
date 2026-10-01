using System.Text.RegularExpressions;
using Ironwake.Cli;
using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Issue 415: at dusk the console prints a run of consecutive dark acts as one counted line,
/// order kept, while the event log and the protocol keep one entry per dark command.
/// </summary>
[Collection("console")]
public class DarkRunTests
{
    private static readonly string Repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static string Seed53(string? log = null)
    {
        var args = new List<string>
        {
            "play", Path.Combine(Repo, "docs", "samples", "brackwater_cut_exit_after_move.map"),
            "--seed", "53", "--script", Path.Combine(Repo, "docs", "transcripts", "2026-09-26-brackwater_cut-53.script"),
            "--strict", "--content", Fixture.RealContentDirectory(),
        };
        if (log is not null)
        {
            args.AddRange(new[] { "--log", log });
        }

        return ConsoleCapture.Run(() => Program.Main(args.ToArray()));
    }

    /// <summary>The number of dark commands a console output stands for: one per bare line, the count per counted line.</summary>
    private static int DarkActs(string output) =>
        output.Split('\n').Sum(line => line == ProtocolSession.DarkLine ? 1 : Regex.Match(line, @"^enemy: something in the dark acts \(x(\d+)\)$") is { Success: true } m ? int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0);

    [Fact]
    public void ARunOfThreeDarkActsPrintsOneCountedLine()
    {
        var output = Seed53();

        Assert.Equal("enemy: something in the dark acts (x3)", PlaySession.DarkRunLine(3));
        Assert.Contains("-- Enemy phase, turn 2 --\nenemy: something in the dark acts (x3)\nenemy: move brigand-1 8,3\n", output);
        Assert.DoesNotContain(ProtocolSession.DarkLine + "\n" + ProtocolSession.DarkLine, output);
    }

    [Fact]
    public void ARunOfOneDarkActPrintsTheBareLine()
    {
        var output = Seed53();

        Assert.Equal(ProtocolSession.DarkLine, PlaySession.DarkRunLine(1));
        Assert.Contains("Pell falls at 10,2\nenemy: something in the dark acts\nenemy: move brigand-1 5,4\n", output);
    }

    [Fact]
    public void DarkThenSeenThenDarkPrintsThreeLinesInOrder()
    {
        var output = Seed53();

        Assert.Contains(
            "enemy: something in the dark acts (x3)\nenemy: move brigand-1 8,3\nBrigand 1 moves 5,4 -> 8,3 via 5,3 6,3 7,3\nenemy: wait brigand-1\nBrigand 1 waits\nenemy: something in the dark acts (x4)\nenemy: move rider-1 10,3\n",
            output);
    }

    [Fact]
    public void TheEventLogKeepsOneDarkLinePerCommand()
    {
        var log = Path.Combine(Path.GetTempPath(), $"ironwake-darkrun-{Guid.NewGuid():N}.log");
        try
        {
            var output = Seed53(log);
            var events = File.ReadAllText(log);

            Assert.Contains(ProtocolSession.DarkLine + "\n" + ProtocolSession.DarkLine + "\n", events);
            Assert.DoesNotContain("dark acts (x", events);
            Assert.Equal(DarkActs(output), events.Split('\n').Count(l => l == ProtocolSession.DarkLine));
        }
        finally
        {
            File.Delete(log);
        }
    }

    [Fact]
    public void TheProtocolStillEmitsOneUnseenActsPerDarkCommand()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var mapPath = Path.Combine(Fixture.RealContentDirectory(), "maps", "brackwater_cut.map");
        var definition = MapFiles.Load(mapPath, content);
        var answer = new ProtocolSession(content, BattleState.From(definition, content, content.Cast, 53), new StringWriter()).Answer("""{"type":"end"}""");
        var script = Path.Combine(Path.GetTempPath(), $"ironwake-darkrun-{Guid.NewGuid():N}.script");
        File.WriteAllText(script, "end\n");
        try
        {
            var console = ConsoleCapture.Run(() => Program.Main(new[] { "play", mapPath, "--seed", "53", "--script", script, "--content", Fixture.RealContentDirectory() }));
            var unseen = Regex.Matches(answer, "\"type\":\"unseenActs\",\"text\":\"enemy: something in the dark acts\"").Count;

            Assert.Contains("dark acts (x", console);
            Assert.True(unseen > 1);
            Assert.DoesNotContain("dark acts (x", answer);
            Assert.Equal(DarkActs(console[console.IndexOf("-- Enemy phase, turn 1 --", StringComparison.Ordinal)..]), unseen);
        }
        finally
        {
            File.Delete(script);
        }
    }
}
