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
    /// Played north of the water, before issue 1198 moved the start south.
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
            var output = Run(out var exit, "campaign", "--load", "shrine", "--saves", saves, "--script", script, "--strict", "--content", Fixture.ShrineNorthStartContentDirectory());

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
    /// Chat's cold chair on Maud's quest 2 (round 406), reseeded to 2130 from the 875 save with
    /// Ottilie as the ally: Maud's forest counters crit brigand 2 for 36 and spend four of
    /// Radiance's five uses, the forecasts counting them down (issue 1200), and the door soldier
    /// still stands; the play recalls to turn 2 and stops undecided. Played north of the water,
    /// before issue 1198 moved the start south.
    /// </summary>
    [Fact]
    public void ChatsColdShrinePlaySpendsRadianceOnCountersAndStopsUndecided()
    {
        var script = Transcript("2026-10-06-the_first_shrine-2130-chat.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-shrine-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_first_shrine-875.saves", "shrine.json")), Path.Combine(saves, "shrine.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "shrine", "--saves", saves, "--reseed", "2130", "--script", script, "--strict", "--content", Fixture.ShrineNorthStartContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("  Maud crits Brigand 2 for 36 (hp 0)\n", output);
            Assert.Contains("counter: acc 81% dmg 11 crit 3% (Radiance 1 of 5 left)\n", output);
            Assert.Contains("Maud's Radiance is spent for this battle\n", output);
            Assert.EndsWith("Campaign stopped in The First Shrine at turn 2, undecided\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Code's warm play of Maud's quest 2 after issue 1198 moved the start south of the water,
    /// reseeded to 1530 from the 875 save with Wren as the ally: the soldier braces before anyone
    /// reaches the door, Wren holds the turn-2 spawn tile, Radiance runs dry on turn 4, both Recalls
    /// go, and Wren's sword opens the door on turn 9 for Maud to take the altar on turn 10 of 10.
    /// Played before issue 1264 woke the sanctum archer on the door's fall.
    /// </summary>
    [Fact]
    public void TheShrineFromTheSouthStartIsWonOnTheLastTurn()
    {
        var script = Transcript("2026-10-06-the_first_shrine-1530.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-shrine-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_first_shrine-875.saves", "shrine.json")), Path.Combine(saves, "shrine.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "shrine", "--saves", saves, "--reseed", "1530", "--script", script, "--strict", "--content", Fixture.ShrineArcherHeldContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Soldier 1 waits and braces\nenemy: end\n-- Enemy phase ends, turn 1 --\n", output);
            Assert.Contains("Reinforcements are blocked: a unit holds 7,8\n", output);
            Assert.Contains("Maud's Radiance is spent for this battle\n", output);
            Assert.Contains("The First Shrine  turn 10 of 10  player phase", output);
            Assert.Contains("Battle won: seize", output);
            Assert.Contains("Maud wins maud_2; Maud receives Maud's Psalter; nobody fell\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Code's warm play of Maud's quest 2 from the south start under 0278, reseeded to 1600 from
    /// the 875 save with Ottilie as the ally: Maud breaks the braced door alone with two Radiance
    /// strikes, Ottilie's Aimed Shot kills the hexer and she corks 7,4 at 6 hp, and Maud takes the
    /// altar on turn 4 of 10. Played before issue 1264 woke the sanctum archer on the door's fall.
    /// </summary>
    [Fact]
    public void TheShrineFromTheSouthStartFallsToMaudAloneOnTurnFour()
    {
        var script = Transcript("2026-10-07-the_first_shrine-1600.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-shrine-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_first_shrine-875.saves", "shrine.json")), Path.Combine(saves, "shrine.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "shrine", "--saves", saves, "--reseed", "1600", "--script", script, "--strict", "--content", Fixture.ShrineArcherHeldContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Reinforcements are blocked: a unit holds 7,8\n", output);
            Assert.Contains("Soldier 1 falls at 7,1\n", output);
            Assert.Contains("The First Shrine  turn 4 of 10  player phase", output);
            Assert.Contains("Battle won: seize", output);
            Assert.Contains("Maud wins maud_2; Maud receives Maud's Psalter; nobody fell\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Code's warm replay of 1600 after issue 1264 woke the sanctum archer on the door's fall: the
    /// door soldier dies on turn 3, the loft group wakes on the death, the archer takes the altar
    /// and shoots Maud, and the brigand finishes her. One Recall back to after the hexer's death,
    /// Maud steps off the door; Ottilie falls on turn 4, Radiance runs dry on turn 5, and Maud
    /// braces on the fort to the end of turn 10, off the altar.
    /// </summary>
    [Fact]
    public void TheWokenShrineArcherTakesTheAltarWhenTheDoorFalls()
    {
        var script = Transcript("2026-10-07-the_first_shrine-1600-woken.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-shrine-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_first_shrine-875.saves", "shrine.json")), Path.Combine(saves, "shrine.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "shrine", "--saves", saves, "--reseed", "1600", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("The loft group wakes (a death in the sanctum group)\n", output);
            Assert.Contains("Archer 1 moves 5,1 -> 7,0 via 6,1 6,0\n", output);
            Assert.Contains("Maud falls at 7,2\n", output);
            Assert.Contains("Ottilie falls at 9,5\n", output);
            Assert.Contains("Lost because turn 10 ended and the captain ended at 10,4, not on the altar at 7,0.\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Chat's cold chair on Maud's quest 2 from the south start after issue 1264, reseeded to
    /// 4426 from the 875 save with Pell as the ally: three pursuer arrivals are blocked by
    /// standing on their tiles, the yard dies at the bridge, the door soldier falls on turn 8,
    /// the woken archer steps onto the freed door tile and spends Maud's last Radiance, and
    /// Maud takes the altar on turn 9 of 10 with nobody fallen.
    /// </summary>
    [Fact]
    public void TheWokenShrineArcherTakesTheFreedDoorTileAgainstPellAndMaudSeizesOnTurnNine()
    {
        var script = Transcript("2026-10-07-the_first_shrine-4426-chat.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-shrine-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_first_shrine-875.saves", "shrine.json")), Path.Combine(saves, "shrine.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "shrine", "--saves", saves, "--reseed", "4426", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Reinforcements are blocked: a unit holds 6,8\n", output);
            Assert.Contains("The loft group wakes (a death in the sanctum group)\n", output);
            Assert.Contains("Archer 1 moves 5,1 -> 7,1 via 6,1\n", output);
            Assert.Contains("Maud's Radiance is spent for this battle\n", output);
            Assert.Contains("Battle won: seize", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Code's warm Teodor chair on Maud's quest 2 after issue 1266, reseeded to 1610 from the 875
    /// save: Maud's Radiance takes the braced door to 9 on turn 2, Teodor screens 7,4 and drops to
    /// 2, then on turn 3 he kills the door from 7,2 at 52 percent before Maud has moved, and she
    /// walks 7,3 to the altar in the same phase, so the woken loft archer never acts.
    /// </summary>
    [Fact]
    public void AnAllyKillingTheShrineDoorBeforeMaudMovesSeizesBeforeTheWokenArcherActs()
    {
        var script = Transcript("2026-10-07-the_first_shrine-1610-teodor.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-shrine-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_first_shrine-875.saves", "shrine.json")), Path.Combine(saves, "shrine.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "shrine", "--saves", saves, "--reseed", "1610", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Soldier 1 falls at 7,1\nThe loft group wakes (a death in the sanctum group)\n", output);
            Assert.Contains("Maud moves 7,3 -> 7,0 via 7,2 7,1\n", output);
            Assert.DoesNotContain("enemy: move archer-1", output);
            Assert.Contains("Maud wins maud_2; Maud receives Maud's Psalter; nobody fell\n", output);
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
    /// Code's journaled play of Ottilie's quest 1 (side-map seed 980) on #925's board, from a save
    /// at the camp after map 6, with Teodor: the opening is Chat's round-311 line, the house wakes
    /// on the turn-5 kill at 4,3 as before, and the archer, now a house guard at 11,2, comes to the
    /// water with the lector and the Sworn Captain. The lector dies on Ottilie's counter, the
    /// captain at the cork, and the archer, unhit in four strikes, stands on the forest at 10,7
    /// when turn 10 ends. Issue 931's eleventh turn lets her phase run: she shoots Teodor dead
    /// and the script stops on turn 11, undecided.
    /// </summary>
    [Fact]
    public void TheJournaledCountingHousePlayBringsTheArcherToTheWater()
    {
        var script = Transcript("2026-10-04-the_counting_house-980.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-counting-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_counting_house-980.saves", "counting.json")), Path.Combine(saves, "counting.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "counting", "--saves", saves, "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Side map: The Counting House, seed 980\n", output);
            Assert.Contains("The house group wakes (noise)\n", output);
            Assert.Contains("Archer moves 11,2 -> 8,3 via 10,2 9,2 9,3\n", output);
            Assert.Contains("Teodor falls at 9,7\n", output);
            Assert.Contains("Campaign stopped in The Counting House at turn 11, undecided\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Code's journaled play of Ottilie's quest 1 (seed 980) at issue 931's limit of 11, from the
    /// same save with Teodor: Chat's opening to turn 6, then Teodor falls back off the far bank on
    /// turn 8 (a Recall spent to undo his death there), the archer and the Sworn Captain follow
    /// him over the bridge, the captain dies on turn 9, and on turn 10 the archer, at 8 hp, shoots
    /// Ottilie dead from 5,7 with `end` naming the lethal.
    /// </summary>
    [Fact]
    public void TheJournaledElevenTurnCountingHousePlayDrawsTheArcherOverTheBridge()
    {
        var script = Transcript("2026-10-04-the_counting_house-980-931.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-counting-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_counting_house-980.saves", "counting.json")), Path.Combine(saves, "counting.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "counting", "--saves", saves, "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Objective: Defeat every enemy by the end of turn 11. Ottilie must survive.\n", output);
            Assert.Contains("Archer moves 8,6 -> 5,7 via 7,6 7,7 6,7\n", output);
            Assert.Contains("Sworn Captain falls at 4,7\n", output);
            Assert.Contains("Lost because Ottilie fell.\n", output);
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
    /// Code's journaled play of Ottilie's quest 2 (side-map seed 91) on #930's board, from a save
    /// at the camp after map 8 with the cast at level 7 and Wren: the archer holds the road at
    /// 11,4, shoots Wren on the bridge end on turn 2 with `end` naming the lethal, and holds the
    /// road shut, so the pair goes round her, Wren first through the exit on turn 8. The save
    /// records Rook's quest 1 won at the camp after map 7, so the camp's two seats offer Teodor's
    /// quest 2 and Ottilie's on the shipped content (round 313). The camp after it deploys for the
    /// field as it read before the drake was seen there (issue 973), so it replays on that content.
    /// </summary>
    [Fact]
    public void TheJournaledLongCountPlayEscapesPastTheRoadArcher()
    {
        var script = Transcript("2026-10-04-the_long_count-91-930.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-longcount-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_long_count-91.saves", "longcount.json")), Path.Combine(saves, "longcount.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "longcount", "--saves", saves, "--script", script, "--strict", "--content", Fixture.FieldUnseenContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Side map: The Long Count, seed 91\n", output);
            Assert.Contains("Lethal if all land: Wren (Archer for 6, Hexer 1 for 12, against 14 hp)\n", output);
            Assert.Contains("enemy: attack archer-1 wren\n", output);
            Assert.Contains("> leave\nOttilie wins ottilie_2; Ottilie receives Ottilie's Tally; the stores take 3 frozen iron; nobody fell\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Code's warm play of Ottilie's quest 2 on a fresh seed (1540, side-map seed 1638) with Pell
    /// as the ally, the weakest-untuned-map run of DECISIONS/0278: the first line loses Pell on the
    /// fort at 11,3 to the road brigand's 36 and Ottilie on turn 6; the second Recall returns to
    /// turn 1, and Ottilie's missed shot from 8,2 on turn 3 wakes the gate by noise, whose hexer
    /// kills her. The map is lost on turn 3 with no Recall left.
    /// </summary>
    [Fact]
    public void TheJournaledLongCountPlayWithPellLosesOttilieToTheGateHexer()
    {
        var script = Transcript("2026-10-06-the_long_count-1540.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-longcount-1540-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_long_count-91.saves", "longcount.json")), Path.Combine(saves, "longcount.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "longcount", "--saves", saves, "--reseed", "1540", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Side map: The Long Count, seed 1638\n", output);
            Assert.Contains("  Fighting here wakes: the gate group (noise, heard from 8,2)\n", output);
            Assert.Contains("Lost because Ottilie fell.\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Chat's cold line of round 313, replayed on #930's board: the bridge still wakes on Teodor's
    /// first step, as it did with the archer on the fort, and Ottilie dressing on the bridge end at
    /// 9,4 on turn 3 is now in the road archer's range, so the archer, the gate hexer and the rider
    /// strike her there and she falls; the line stops at its next command. The 2026-10-04 transcript
    /// stays as the record of the old board.
    /// </summary>
    [Fact]
    public void ChatsColdLongCountLineFallsOnTheBridgeEndUnderTheRoadArcher()
    {
        var script = Transcript("2026-10-04-the_long_count-91-chat.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-longcount-chat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_long_count-91.saves", "longcount.json")), Path.Combine(saves, "longcount.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "longcount", "--saves", saves, "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(3, exit);
            Assert.Contains("> move teodor 6,4\nTeodor moves 2,4 -> 6,4", output);
            Assert.Contains("The bridge group wakes (proximity); their lamps are lit (Soldier 10,4, Archer 11,4)\n", output);
            Assert.Contains("enemy: attack archer-1 ottilie\n", output);
            Assert.Contains("Ottilie falls at 9,4\n", output);
            Assert.Contains("Campaign stopped in The Long Count at turn 3, decided and not left\n", output);
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
    /// Code's warm play of Keziah's quest 1 on a fresh seed (1550, side-map seed 1677) with Teodor
    /// as the ally, the weakest-untuned-map run of DECISIONS/0278: Teodor wakes the nave from 3,4
    /// and his counter takes the brawler, the shieldbearer's fight wakes the grove from its own tile,
    /// Teodor corks the breach against the turn-3 rider, then baits the hexer onto the plain at 7,6
    /// from 7,4, and the last two fall on turn 8.
    /// </summary>
    [Fact]
    public void TheJournaledBurnedShrinePlayWithTeodorRoutsOnTurnEight()
    {
        var script = Transcript("2026-10-06-the_burned_shrine-1550.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-shrine-1550-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-03-the_burned_shrine-1113.saves", "shrine.json")), Path.Combine(saves, "shrine.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "shrine", "--saves", saves, "--reseed", "1550", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Side map: The Burned Shrine, seed 1677\n", output);
            Assert.Contains("  Fighting here wakes: the grove group (noise, heard from 7,2)\n", output);
            Assert.Contains("Hexer moves 10,6 -> 7,6 via 9,6 8,6\n", output);
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
    /// Code's journaled play of Rook's quest 2 with the drake shipped (issue 1094, side-map seed 1132): the
    /// Rookery save with Rook's drake Grown, no <c>carry:</c> header. The carry line prints on the side map,
    /// the turn-2 carry flies Wren over the ravine to 10,6 and she lands free to kill the archer; Wren escapes
    /// on turn 7, Rook falls on 14,6 to the rider on turn 8, and the Recall to turn 8 replays the same roll.
    /// </summary>
    [Fact]
    public void TheJournaledRookeryCarryPlayFliesWrenOverTheRavineWithNoHeader()
    {
        var script = Transcript("2026-10-05-the_rookery-1132-carry.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-rookery-carry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-05-the_rookery-1132-carry.saves", "rookery.json")), Path.Combine(saves, "rookery.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "rookery", "--saves", saves, "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.DoesNotContain("carry: ", output);
            Assert.Contains("carry (a grown drake's whole turn, from beside an ally that has not moved): carry rook <ally> <x,y> <set down x,y>; the ally lands free to move and act\n", output);
            Assert.Contains("Rook's drake carries Wren 4,5 -> 10,6; lands free to move and act\n", output);
            Assert.Contains("Archer falls at 12,6\n", output);
            Assert.Contains("Rook falls at 14,6; her drake leaves the field\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Chat's cold Drake Warden chair (issue 1100, Table comment 6005059467): issue 1133's synthetic
    /// Brackwater camp, Rook certified to the Drake Warden, Brackwater Cut won on turn 6 with Pell
    /// fallen and one Recall, then the Field won on turn 11 with Keziah turned on turn 1 and the
    /// Sworn Captain killed on his fort. The boss's off-fort shuttle on turns 5 to 10 was issue 1138:
    /// replayed loose under it, he holds 18,6 from turn 5 to turn 9 and comes off it on turn 10 only
    /// to strike Teodor, so the journaled line no longer replays strictly past turn 5. Under the levy
    /// floor (issue 1164) the camp after Brackwater drills the levy to L7, the replay drifts further,
    /// and he never comes off at all; what stays pinned is that he leaves his fort only to strike.
    /// </summary>
    [Fact]
    public void TheJournaledColdDrakeWardenChairNowFindsTheBossHoldingHisFort()
    {
        var script = Transcript("2026-10-05-drake_warden-644-chat.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-drake-warden-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-05-drake_warden-644-synthetic.saves", "brackwater.json")), Path.Combine(saves, "brackwater.json"));
        try
        {
            var output = Run(out _, "campaign", "--load", "brackwater", "--saves", saves, "--script", script, "--content", Fixture.RealContentDirectory());

            var field = output[output.IndexOf("Battle won: escape", StringComparison.Ordinal)..];
            Assert.Contains("Pell falls at 13,4\n", output);
            Assert.Contains("Battle won: escape", output);
            for (var turn = 5; turn <= 9; turn++)
            {
                var start = field.IndexOf($"-- Enemy phase, turn {turn} --", StringComparison.Ordinal);
                var phase = field[start..field.IndexOf($"-- Enemy phase ends, turn {turn} --", start, StringComparison.Ordinal)];
                Assert.DoesNotContain("Sworn Captain moves", phase);
                Assert.Contains("Sworn Captain waits\n", phase);
            }

            foreach (var move in field.Split('\n').Select((line, at) => (line, at)).Where(l => l.line.StartsWith("Sworn Captain moves", StringComparison.Ordinal)))
            {
                Assert.StartsWith("enemy: attack sworn_captain-1 ", field.Split('\n')[move.at + 1]);
            }
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Code's journaled replay of Keziah's quest 2 on the board as slice 16 left it (side-map seed 1133),
    /// played with the rear rider on turn 3 (issue 940 moved it to 5),
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
            var output = Run(out var exit, "campaign", "--load", "oath", "--saves", saves, "--script", script, "--strict", "--content", Fixture.OathRiderOnTurnThreeContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("Side map: The Oath Stone, seed 1133\n", output);
            Assert.Contains("Joab is bound to Sworn Captain: freed when Sworn Captain falls\n", output);
            Assert.Contains("    Counter kills on hit: Keziah +10 HP, to max 26 (Kinsbane feeds, fed 11)\n", output);
            Assert.Contains("> leave\nKeziah falls on keziah_2, which closes for good; fallen for good: Keziah\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Code's journaled play of Keziah's quest 2 at issue 939's limit of 10 (side-map seed 1133, the rear rider on turn 3 as before issue 940), Chat's
    /// round-317 line through turn 7, then Code's: the archer killed on turn 8, Maud's chip and Keziah
    /// standing at 13,3 after the turn-9 Recall, and on turn 10 the envoy at 24, out of a Cleave's reach;
    /// Keziah falls on enemy phase 10, Joab never moved. The forecast's kill rows read `On a kill:` and `Kills on hit:`.
    /// </summary>
    [Fact]
    public void TheJournaledOathStonePlayAtLimitTenIsLostOnTurnTen()
    {
        var script = Transcript("2026-10-04-the_oath_stone-1133-939.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-oath-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-04-the_oath_stone-1133.saves", "oath.json")), Path.Combine(saves, "oath.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "oath", "--saves", saves, "--script", script, "--strict", "--content", Fixture.OathRiderOnTurnThreeContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("The Oath Stone  turn 10 of 10  player phase", output);
            Assert.Contains("  Kills on hit: Keziah +10 HP", output);
            Assert.Contains("> leave\nKeziah falls on keziah_2, which closes for good; fallen for good: Keziah\n", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }

    /// <summary>
    /// Code's journaled play of Keziah's quest 2 with the rear rider on turn 5 (issue 940), from the
    /// oath save reseeded to 940: the camp fed on turns 2 and 3, the archer's kill woke Kinsbane on
    /// turn 7 and Keziah held the breach at 11,6 for Maud, the rider died on her on turn 8, and on
    /// turn 10 Maud's last Radiance took the envoy from 8, after one Recall to turn 9 that kept Maud alive.
    /// </summary>
    [Fact]
    public void TheJournaledOathStonePlayWithTheRiderOnTurnFiveIsWonOnTurnTen()
    {
        var script = Transcript("2026-10-04-the_oath_stone-940-code.script");
        var saves = Path.Combine(Path.GetTempPath(), "ironwake-oath-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saves);
        File.Copy(Transcript(Path.Combine("2026-10-04-the_oath_stone-1133.saves", "oath.json")), Path.Combine(saves, "oath.json"));
        try
        {
            var output = Run(out var exit, "campaign", "--load", "oath", "--saves", saves, "--reseed", "940", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("  turn 5, enemy phase: a rider arrives at 0,0 (aggressive). A unit standing on 0,0 stops it.\n", output);
            Assert.Contains("Kinsbane, to Keziah: \"Keziah. I remember what I'm for.\"\n", output);
            Assert.Contains("  Maud hits Sworn Captain for 9 (hp 0)\n", output);
            Assert.Contains("Battle won: defeat_boss", output);
            Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
        }
        finally
        {
            Directory.Delete(saves, true);
        }
    }
}
