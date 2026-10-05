using Ironwake.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The Mill, the campaign's second map (issue 632, DESIGN section 14): Maud holds the miller's
/// house alone and is protected, so leaving her to it loses the map, and Code's journaled play
/// replays to its transcript; its before scene (issue 1005) holds on the board and prints after
/// <c>march</c>.
/// </summary>
[Collection("console")]
public class TheMillTests
{
    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static string Transcript(string name) => Path.Combine(Repo, "docs", "transcripts", name);

    /// <summary>The captain never comes: on seed 2 the road pair reaches the fort and Maud falls, which loses the map.</summary>
    [Fact]
    public void LeavingMaudToHoldTheFortAloneLosesTheMap()
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-mill-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, string.Concat(Enumerable.Repeat("wait captain\nwait maud\nend\n", 4)));
        try
        {
            var output = Run(out var exit, "play", "the_mill", "--seed", "2", "--script", path, "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Maud falls at 8,5\n", output);
            Assert.Contains("Lost because Maud fell. This map is lost if Maud falls or is left behind.\n", output);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Code's journaled play (seed 632): Maud leaves the fort on turn 2 to finish the road archer
    /// the captain opened, so only the brigand reaches her; the road pair is dead by turn 3 with no
    /// combat near the mill, and the mill pair falls on turns 5 and 6 with no Recall spent.
    /// </summary>
    [Fact]
    public void TheJournaledPlayWinsOnTurnSixWithNoRecall()
    {
        var script = Transcript("2026-10-01-the_mill-632.script");

        var output = Run(out var exit, "play", "the_mill", "--seed", "632", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.Contains("Brigand hits Maud for 13 (hp 4)\n", output);
        Assert.Contains("Archer 1 falls at 9,1\n", output);
        Assert.EndsWith("Battle won: rout\n", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// The before scene (issue 1005, round 373) puts the miller's house "on the far bank" and has
    /// Alder go on afoot to it, and its rules line says the map is lost if Maud falls: the board
    /// keeps all three, Maud on the fort east of the stream, the captain west of it, Maud protected.
    /// </summary>
    [Fact]
    public void TheBeforeScenesFarBankHoldsOnTheBoard()
    {
        var content = MapFixture.Content;
        var map = MapFiles.Load(Path.Combine(MapFixture.MapsDirectory, "the_mill.map"), content);
        var scene = content.Scenes.Single(s => s.Id == "the_mill_before");

        Assert.Equal(ScenePoint.Before, scene.Point);
        Assert.Contains(scene.Lines, l => l.Text.Contains("On the far bank the miller's house", StringComparison.Ordinal));
        Assert.Contains(scene.Lines, l => l.Text.StartsWith("(This map is lost if Maud falls.", StringComparison.Ordinal));
        Assert.Equal("maud", map.ProtectId);
        var maud = Assert.Single(map.Placements.OfType<PlayerPlacement>(), p => p.RecruitId == "maud");
        var captain = Assert.Single(map.Placements.OfType<PlayerPlacement>(), p => p.Slot == PlayerSlot.Captain);
        Assert.Equal("fort", map.TerrainIdAt(maud.At));
        Assert.Equal("water", map.TerrainIdAt(new Coord(6, maud.At.Y)));
        Assert.True(captain.At.X < 6 && maud.At.X > 6, $"captain {captain.At}, Maud {maud.At}");
        Assert.Empty(content.Campaign.Maps[1].Before);
    }

    /// <summary>
    /// The Mill's before card is replaced by its scene (issue 1005): the camp after Starting Alone
    /// opens on no card, and <c>march</c> prints the scene after the map line, rules line last, inside
    /// the card width; the card's "took the chaplain" is retired, and none of it is in the log.
    /// </summary>
    [Fact]
    public void MarchingToTheMillPrintsItsBeforeSceneAfterTheMapLine()
    {
        var battle = File.ReadAllText(Transcript("2026-10-01-starting_alone-631.script"));
        var path = Path.Combine(Path.GetTempPath(), "ironwake-mill-" + Guid.NewGuid().ToString("N") + ".script");
        var log = Path.ChangeExtension(path, ".log");
        File.WriteAllText(path, "march\n" + battle + "leave\nmarch\n");
        try
        {
            var output = Run(out _, "campaign", "--seed", "631", "--script", path, "--content", Fixture.RealContentDirectory(), "--log", log);

            Assert.Contains("string.\n\n-- Before map 2 of 10: The Mill; the purse holds 800 --\n", output);
            Assert.Contains("Map 2 of 10: The Mill, seed 632\n-- The Mill --\nThe mill road follows a stream that outlived its mill.", output);
            Assert.Contains("Alder ties the horse to the dead mill's wheel and goes on afoot.\n(This map is lost if Maud falls.", output);
            Assert.Contains("threat names what a stop would wake.)\n\nObjective:", output);
            Assert.DoesNotContain("took the chaplain", output);
            Assert.DoesNotContain("It says she mends things", output);
            var start = output.IndexOf("-- The Mill --", StringComparison.Ordinal);
            var scene = output[start..output.IndexOf("Objective:", start, StringComparison.Ordinal)];
            Assert.All(scene.Split('\n'), line => Assert.True(line.Length <= Ironwake.Cli.CampaignSession.CardWidth, line));
            Assert.DoesNotContain("goes on afoot", File.ReadAllText(log));
        }
        finally
        {
            File.Delete(path);
            File.Delete(log);
        }
    }

    private static string Run(out int exit, params string[] args)
    {
        var code = 0;
        var output = ConsoleCapture.Run(() => code = Ironwake.Cli.Program.Main(args));
        exit = code;
        return output;
    }
}
