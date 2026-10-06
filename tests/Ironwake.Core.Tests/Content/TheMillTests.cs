using Ironwake.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The Mill, the campaign's second map (issue 632, DESIGN section 14): Maud holds the miller's
/// house alone and is protected, so leaving her to it loses the map, and Code's journaled play
/// replays to its transcript; its before and after scenes (issue 1005) hold on the board, the
/// before scene printed after <c>march</c>, the after scene below the won line.
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
        File.WriteAllText(path, string.Concat(Enumerable.Repeat("wait captain\nwait maud\nend !\n", 4)));
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
    /// combat near the mill, and the mill pair falls on turns 5 and 6 with no Recall spent. It
    /// replays on the Mill it was played on, before the <c>holds:</c> header (issue 1189).
    /// </summary>
    [Fact]
    public void TheJournaledPlayWinsOnTurnSixWithNoRecall()
    {
        var script = Transcript("2026-10-01-the_mill-632.script");

        var output = Run(out var exit, "play", Path.Combine(Repo, "docs", "samples", "the_mill_0278.map"), "--seed", "632", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.Contains("Brigand hits Maud for 13 (hp 4)\n", output);
        Assert.Contains("Archer 1 falls at 9,1\n", output);
        Assert.EndsWith("Battle won: rout\n", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Chat's warm play of the Mill (round 402, seed 2061), the patient line: the road group is
    /// lured west out of the mill's hearing and the captain baits the guards off the hill on turn 6.
    /// It replays on the Mill it was played on, and the same commands play the same game under the
    /// <c>holds:</c> header (issue 1189), since the guards step off their ground only to strike.
    /// </summary>
    [Fact]
    public void ChatsPatientLineWinsOnTurnNineAndTheHoldLeavesItAlone()
    {
        var script = Transcript("2026-10-06-the_mill-2061-chat.script");

        var played = Run(out var exit, "play", Path.Combine(Repo, "docs", "samples", "the_mill_0278.map"), "--seed", "2061", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());
        var held = Run(out var heldExit, "play", "the_mill", "--seed", "2061", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.Contains("Soldier hits Alder Fenn for 8 (hp 4)\n", played);
        Assert.Contains("The Mill  turn 9 of 12  player phase", played);
        Assert.EndsWith("Battle won: rout\n", played);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), played);
        Assert.Equal(0, heldExit);
        Assert.Equal(played, string.Concat(held.Split('\n').Where(l => !l.StartsWith("holds:", StringComparison.Ordinal)).Select(l => l + "\n"))[..^1]);
    }

    /// <summary>
    /// Code's warm play under the <c>holds:</c> header (issue 1189, seed 1500): the fort strike wakes
    /// the mill on turn 2, the guards wait on the north bank instead of marching to the fort, Maud
    /// leaves the fort at 9 HP for a tile they cannot strike, and on turn 5 the guards that stepped
    /// off to strike the captain on the fort put her tile in reach, so she steps back; won on turn 8, no Recall.
    /// </summary>
    [Fact]
    public void UnderTheHoldTheWokenMillWaitsOnTheBankAndTheMapRunsToTurnEight()
    {
        var script = Transcript("2026-10-06-the_mill-1500.script");

        var output = Run(out var exit, "play", "the_mill", "--seed", "1500", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.Contains("The mill group wakes (noise)\n", output);
        Assert.Contains("enemy: move soldier-1 8,2\n", output);
        Assert.Contains("Maud moves 7,8 -> 7,9\n", output);
        Assert.Contains("The Mill  turn 8 of 12  player phase", output);
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
    /// The after scene (issue 1005, rounds 373 and 374) has Maud pray "at each of the four": a rout
    /// leaves every enemy dead, so the line holds only while the board fields four, and it fails here
    /// before it can come untrue on the screen. Its t1 brings her "from the house", which holds
    /// whoever struck last, because she starts on the fort the before scene barred.
    /// </summary>
    [Fact]
    public void TheAfterScenesFourDeadMatchTheBoardsEnemies()
    {
        var content = MapFixture.Content;
        var map = MapFiles.Load(Path.Combine(MapFixture.MapsDirectory, "the_mill.map"), content);
        var scene = content.Scenes.Single(s => s.Id == "the_mill_after");

        Assert.Equal(ScenePoint.After, scene.Point);
        Assert.StartsWith("When the last raider is down, Maud comes from the house", scene.Lines[0].Text, StringComparison.Ordinal);
        Assert.Contains(scene.Lines, l => l.Text.Contains("At each of the four she kneels", StringComparison.Ordinal));
        Assert.Equal(WinCondition.Rout, map.Win);
        Assert.Equal(4, map.Placements.Count(p => p.Side == Side.Enemy));
        var maud = Assert.Single(map.Placements.OfType<PlayerPlacement>(), p => p.RecruitId == "maud");
        Assert.Equal("fort", map.TerrainIdAt(maud.At));
        Assert.Empty(content.Campaign.Maps[1].After);
    }

    /// <summary>
    /// The Mill's after card is replaced by its scene (issue 1005): winning the map in the campaign
    /// prints the scene below the won line and above the next camp, inside the card width; the old
    /// card's "Two names on the list" is retired, and none of the scene is in the log.
    /// </summary>
    [Fact]
    public void WinningTheMillPrintsItsAfterSceneBelowTheWonLine()
    {
        var alone = File.ReadAllText(Transcript("2026-10-01-starting_alone-631.script"));
        var mill = string.Join("\n", File.ReadAllLines(Transcript("2026-10-01-the_mill-632.script")).Take(25)) + "\n";
        var path = Path.Combine(Path.GetTempPath(), "ironwake-mill-" + Guid.NewGuid().ToString("N") + ".script");
        var log = Path.ChangeExtension(path, ".log");
        File.WriteAllText(path, "march\n" + alone + "leave\nmarch\n" + mill + "leave\n");
        try
        {
            var output = Run(out _, "campaign", "--seed", "631", "--script", path, "--content", Fixture.RealContentDirectory(), "--log", log);

            Assert.Contains("The Mill won: rout; reward 600, the purse holds 1400; nobody fell\n-- After The Mill --\nWhen the last raider is down, Maud comes from the house", output);
            Assert.Contains("Maud: They stopped me at the crossing and asked for the rite-keeper by\n", output);
            Assert.Contains("as long as a name would take, and goes on.\n\n-- Before map 3 of 10: Saltmarsh Ford;", output);
            Assert.DoesNotContain("Two names on the list", output);
            var start = output.IndexOf("-- After The Mill --", StringComparison.Ordinal);
            var scene = output[start..output.IndexOf("-- Before map 3 of 10", start, StringComparison.Ordinal)];
            Assert.All(scene.Split('\n'), line => Assert.True(line.Length <= Ironwake.Cli.CampaignSession.CardWidth, line));
            Assert.DoesNotContain("goes on.", File.ReadAllText(log));
        }
        finally
        {
            File.Delete(path);
            File.Delete(log);
        }
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
