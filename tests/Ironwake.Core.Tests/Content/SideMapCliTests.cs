namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Side maps at the console (issue 635): the screen lists what the interlude offers, <c>quest</c>
/// refuses the captain as the ally, and Code's journaled play of The Lazar House replays to its
/// transcript, Wren fallen for good and the campaign going on.
/// </summary>
[Collection("console")]
public class SideMapCliTests
{
    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static string Transcript(string name) => Path.Combine(Repo, "docs", "transcripts", name);

    [Fact]
    public void TheScreenListsTheSideMapAndRefusesTheCaptainAsTheAlly()
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-side-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, "quest maud_1 captain\n");
        try
        {
            var output = Run(out _, "campaign", "--from", "the_tollgate", "--seed", "701", "--script", path, "--content", Fixture.RealContentDirectory());

            Assert.Contains("Side maps (the member and one ally you pick, not the captain; who falls there is gone for good, and the campaign goes on):\n  maud_1: Maud's quest 1, The Lazar House (quest maud_1 <ally>)\n", output);
            Assert.Contains("ERROR: Alder Fenn is the captain and stays with the company; pick another ally\n", output);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NoSideMapIsListedBeforeOneOpens()
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-side-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, "quest maud_1 wren\n");
        try
        {
            var output = Run(out _, "campaign", "--from", "saltmarsh_ford", "--seed", "701", "--script", path, "--content", Fixture.RealContentDirectory());

            Assert.DoesNotContain("Side maps", output);
            Assert.Contains("ERROR: Side map maud_1 is not open before this map\n", output);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Code's journaled play (seed 701): Wren bars the north lane on turn 2, Maud strikes the
    /// soldier over the wall, and on turn 6 Wren stands in the north lane at 9 hp so the fort
    /// faces three strikers, not four; she falls to the east brigand, and the map is won.
    /// </summary>
    [Fact]
    public void TheJournaledPlayWinsAndWrenIsFallenForGood()
    {
        var script = Transcript("2026-10-01-the_lazar_house-701.script");

        var output = Run(out var exit, "campaign", "--from", "the_tollgate", "--seed", "701", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(1, exit);
        Assert.Contains("Wren falls at 3,1\n", output);
        Assert.Contains("> leave\nMaud wins maud_1; fallen for good: Wren\n-- After The Lazar House --\n", output);
        Assert.Contains("  Fallen: Wren\n", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    private static string Run(out int exit, params string[] args)
    {
        var code = 0;
        var output = ConsoleCapture.Run(() => code = Ironwake.Cli.Program.Main(args));
        exit = code;
        return output;
    }
}
