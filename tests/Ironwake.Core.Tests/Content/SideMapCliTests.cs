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

            Assert.Contains("Side maps (the member and one ally you pick, not the captain; a lost side map never ends the campaign):\n  maud_1: Maud's quest 1, The Lazar House (quest maud_1 <ally>); permadeath applies\n", output);
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

        var output = Run(out var exit, "campaign", "--from", "the_tollgate", "--seed", "701", "--script", script, "--strict", "--content", Fixture.RoomlessContentDirectory());

        Assert.Equal(1, exit);
        Assert.Contains("Wren falls at 3,1\n", output);
        Assert.Contains("> leave\nMaud wins maud_1; fallen for good: Wren\n-- After The Lazar House --\n", output);
        Assert.Contains("  Fallen: Wren (fell on The Lazar House)\n", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Code's journaled play of Maud's quest 2 (seed 875), from a save before the raid with her
    /// quest 1 won: Wren kills the hexer, Maud both brigands, the braced door soldier falls on
    /// Wren's counter, and Maud takes the altar on turn 4 with the Psalter paid into her pack.
    /// </summary>
    [Fact]
    public void TheJournaledShrinePlayWinsAndPaysThePsalter()
    {
        var script = Transcript("2026-10-03-the_first_shrine-875.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-shrine-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_first_shrine-875.saves", "shrine.json")), Path.Combine(saves, "shrine.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "shrine", "--saves", saves, "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Battle won: seize", output);
            Assert.Contains("> leave\nMaud wins maud_2; Maud receives Maud's Psalter; nobody fell\n-- After The First Shrine --\n", output);
            Assert.Contains("  maud_2: Maud's quest 2, The First Shrine; paid Maud's Psalter\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Code's journaled play of Pell's quest 1 (seed 884), from a save at the camp after map 4:
    /// Wren's counter breaks the shieldbearer at the east gate, Pell turns back for the north
    /// chest and is boxed in its door, Wren leaves first, and Pell gets out on turn 6 at 5 hp
    /// from 12,5, the one exit tile the chase cannot reach.
    /// </summary>
    /// <summary>
    /// Code's warm play of The Old Watch (issue 635 slice 8, side-map seed 961): the Family Lance passes
    /// count 10 on turn 3 and the card says the rust holds; both Recalls are spent; turn 9 ends
    /// with the Sworn Captain at 2, Wren falls on the last enemy phase, and the side map is lost.
    /// </summary>
    [Fact]
    public void TheJournaledOldWatchPlayIsLostByTwoHitPointsAndWrenIsFallenForGood()
    {
        var script = Transcript("2026-10-03-the_old_watch-961.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-watch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_old_watch-961.saves", "watch.json")), Path.Combine(saves, "watch.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "watch", "--saves", saves, "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("The rust holds; it waits on Teodor.", output);
            Assert.Contains("Lost because turn 9 ended and the boss still stands.\n", output);
            Assert.Contains("> leave\nSide map teodor_1 is lost: turn 9 passed; it opens again after the next map; fallen for good: Wren\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Code's journaled play of Ottilie's quest 1 (side-map seed 980), from a save at the camp
    /// after map 6: a shot at the bank archer on turn 2 wakes the house and the lector's
    /// Radiance kills Ottilie over the canal (recalled), the road pair is fought on the near
    /// bank, and on turn 8 a missed Heavy Cut leaves the Sworn Captain on the bridge to cross
    /// it to Ottilie at 2 HP.
    /// </summary>
    [Fact]
    public void TheJournaledCountingHousePlayIsLostWhenTheBridgeIsLeftOpen()
    {
        var script = Transcript("2026-10-03-the_counting_house-980.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-counting-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_counting_house-980.saves", "counting.json")), Path.Combine(saves, "counting.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "counting", "--saves", saves, "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Side map: The Counting House, seed 980\n", output);
            Assert.Contains("The house group wakes (noise)\n", output);
            Assert.Contains("> leave\nOttilie falls on ottilie_1, which closes for good; fallen for good: Wren, Ottilie\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    [Fact]
    public void TheJournaledBurnedSchoolPlayEscapesWithTheGust()
    {
        var script = Transcript("2026-10-03-the_burned_school-884.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-school-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_burned_school-884.saves", "school.json")), Path.Combine(saves, "school.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "school", "--saves", saves, "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Pell opens the chest at 6,2: Gust\n", output);
            Assert.Contains("Battle won: escape", output);
            Assert.Contains("> leave\nPell wins pell_1; the stores take 2 common material; nobody fell\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Code's journaled play of Pell's quest 2 (seed 960), from a save at the camp after map 6:
    /// the north passage crossed on the quiet stops, Pell takes the lector's shot at 14,1 (its
    /// noise wakes the stacks), kills him from 14,2, Wren corks the middle door at 14,4 through
    /// her, and Pell reaches the desk on turn 6.
    /// </summary>
    [Fact]
    public void TheJournaledUndercroftPlaySeizesTheDeskAndPaysTheCommonplace()
    {
        var script = Transcript("2026-10-03-the_undercroft-960.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-undercroft-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_undercroft-960.saves", "undercroft.json")), Path.Combine(saves, "undercroft.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "undercroft", "--saves", saves, "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("The stacks group wakes (noise)\n", output);
            Assert.Contains("Battle won: seize", output);
            Assert.Contains("> leave\nPell wins pell_2; Pell receives Pell's Commonplace; the stores take 3 frozen iron; nobody fell\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Code's journaled play of Ottilie's quest 2 (side-map seed 91), from a save at the camp
    /// after map 8 with the cast at level 7: the bridge is taken on turn 2 while the dusk still
    /// lets Ottilie shoot at range, the pursuers are killed with Wren beside them as the spotter,
    /// a turn-5 line that cannot reach the exits by turn 8 is recalled, and the pair leaves on
    /// turn 8, Wren first.
    /// Replayed without Teodor's quest 2, which the save's camp would offer ahead of Ottilie's.
    /// </summary>
    [Fact]
    public void TheJournaledLongCountPlayEscapesAndPaysTheTally()
    {
        var script = Transcript("2026-10-03-the_long_count-91.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-longcount-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_long_count-91.saves", "longcount.json")), Path.Combine(saves, "longcount.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "longcount", "--saves", saves, "--script", script, "--strict", "--content", Fixture.BeforeWardensGateContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Side map: The Long Count, seed 91\n", output);
            Assert.Contains("Battle won: escape", output);
            Assert.Contains("> leave\nOttilie wins ottilie_2; Ottilie receives Ottilie's Tally; the stores take 3 frozen iron; nobody fell\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Code's journaled play of Rook's quest 1 (side-map seed 1100), from a save at the camp
    /// after map 7 with Rook picked: the deacon leaves the yard and is killed at the door, a
    /// counter on the cork kills Rook twice over two Recalls, and on turn 7 Wren opens the cork
    /// so Rook's first strike kills it, and she takes the roll room on turn 8.
    /// </summary>
    [Fact]
    public void TheJournaledChapterRollPlaySeizesTheRollOnTheLastTurn()
    {
        var script = Transcript("2026-10-03-the_chapter_roll-1100.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-chapter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_chapter_roll-1100.saves", "chapter.json")), Path.Combine(saves, "chapter.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "chapter", "--saves", saves, "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Side map: The Chapter Roll, seed 1100\n", output);
            Assert.Contains("Battle won: seize", output);
            Assert.Contains("> leave\nRook wins rook_1; the stores take 2 common material; nobody fell\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    private static string Run(out int exit, params string[] args)
    {
        var code = 0;
        var output = ConsoleCapture.Run(() => code = Ironwake.Cli.Program.Main(args));
        exit = code;
        return output;
    }

    /// <summary>
    /// Code's journaled play of Teodor's quest 2 (side-map seed 1110), from the Chapter Roll's camp
    /// save with Teodor's lance set woken and in front: the west soldier is lured and killed, Wren
    /// holds the one-tile gap against the rear, Long Thrust from 5,2 wears the boss down while the
    /// woken yard comes for Wren at 2 HP, and the third thrust wins on turn 7.
    /// </summary>
    [Fact]
    public void TheJournaledWardensGatePlayNamesTheLance()
    {
        var script = Transcript("2026-10-03-the_wardens_gate-1110.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-wardens-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_wardens_gate-1110.saves", "wardens.json")), Path.Combine(saves, "wardens.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "wardens", "--saves", saves, "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Side map: The Warden's Gate, seed 1110\n", output);
            Assert.Contains("Battle won: defeat_boss", output);
            Assert.Contains("> leave\nTeodor wins teodor_2; Family Lance is the First Warden's Lance now; the stores take 3 frozen iron; nobody fell\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Code's journaled play of Keziah's quest 1 (side-map seed 1113), from a save at the camp
    /// after map 7 with Keziah picked and the cast at level 7: the nave is woken and drawn to the
    /// west door, Keziah takes the hearth and the shieldbearer beside it, which wakes the grove,
    /// Wren corks the breach, and the last burner falls on the hearth on turn 8.
    /// </summary>
    [Fact]
    public void TheJournaledBurnedShrinePlayRoutsOnTurnEight()
    {
        var script = Transcript("2026-10-03-the_burned_shrine-1113.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-shrine-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_burned_shrine-1113.saves", "shrine.json")), Path.Combine(saves, "shrine.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "shrine", "--saves", saves, "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Side map: The Burned Shrine, seed 1113\n", output);
            Assert.Contains("The grove group wakes (noise)", output);
            Assert.Contains("> leave\nKeziah wins keziah_1; the stores take 2 common material; nobody fell\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Code's journaled play of Rook's quest 2 (side-map seed 1132), from a save at the camp
    /// after map 9 with Rook picked, the Chapter Roll won and the cast at level 7: the bridge
    /// fight wakes the loft, Wren kills the Wing Captain and both pursuing wingriders. On issue 862's
    /// holds Rook waits on 14,5, the exit outside both rings, at 4 HP from turn 4, Wren kills the archer
    /// on 12,6 to open 14,6, and leaves before her on turn 9.
    /// </summary>
    [Fact]
    public void TheJournaledRookeryPlayEscapesOnTurnNine()
    {
        var script = Transcript("2026-10-03-the_rookery-1132-862.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-rookery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_rookery-1132.saves", "rookery.json")), Path.Combine(saves, "rookery.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "rookery", "--saves", saves, "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Side map: The Rookery, seed 1132\n", output);
            Assert.Contains("The loft group wakes", output);
            Assert.Contains("Archer falls at 12,6\n", output);
            Assert.Contains("escaped: wren rook\n", output);
            Assert.Contains("> leave\nRook wins rook_2; the stores take 2 common material; nobody fell\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Code's journaled replay of Keziah's quest 2 on the board as slice 16 left it (side-map seed 1133),
    /// from the same save at the camp after map 9: Joab named and bound at the door, the envoy a
    /// Boss on his fort. Keziah stood at the door on turn 3 with his counter lethal, went round,
    /// fed on the camp, and fell on turn 6 to the soldier's counter after two misses at 74, Joab never struck.
    /// (The slice 15 play of the same seed, on the old board, stays in its transcript as history.)
    /// </summary>
    [Fact]
    public void TheJournaledOathStonePlayIsLostOnTurnSix()
    {
        var script = Transcript("2026-10-04-the_oath_stone-1133-fort.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-oath-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-04-the_oath_stone-1133-fort.saves", "oath.json")), Path.Combine(saves, "oath.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "oath", "--saves", saves, "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Side map: The Oath Stone, seed 1133\n", output);
            Assert.Contains("Joab is bound to Sworn Captain: freed when Sworn Captain falls\n", output);
            Assert.Contains("    Counter kill: Keziah +10 HP, to max 26 (Kinsbane feeds, fed 11)\n", output);
            Assert.Contains("> leave\nKeziah falls on keziah_2, which closes for good; fallen for good: Keziah\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }
}
