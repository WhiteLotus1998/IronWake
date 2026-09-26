using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The <c>play</c> command (issue 11): the missing-systems line, the stubs that refuse out
/// loud, usage paths, script mode, and a scripted run over Old Mill Road whose enemy phase
/// is the one section 8 predicts. CLI tests share the console collection.
/// </summary>
[Collection("console")]
public class CliPlayTests
{
    private static string OldMillRoad => Path.Combine(Fixture.RealContentDirectory(), "maps", "old_mill_road.map");

    private static string Play(out int exit, string script, string seed = "7")
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-play-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, script);
        try
        {
            return Run(out exit, "play", OldMillRoad, "--seed", seed, "--script", path, "--content", Fixture.RealContentDirectory());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PlayPrintsTheMapHeaderFirstAndFieldsTheCast()
    {
        var output = Play(out var exit, "");

        Assert.StartsWith("Old Mill Road, seed 7, scheme ", output);
        Assert.DoesNotContain("synthetic", output);
        Assert.DoesNotContain("issue 9", output);
        Assert.EndsWith("battle ongoing at turn 1, player phase\n", output);
        Assert.Equal(1, exit);
        Assert.All(output, c => Assert.True(c < 128, "non-ASCII character in CLI output"));
    }

    [Fact]
    public void ItemUsesADressingAfterACombatAndHelpListsIt()
    {
        var output = Play(out _, "item captain 2\nmove captain 2,6\nend\nend\nitem captain 2\nshow captain\nhelp\n");

        Assert.Contains("> item captain 2\nERROR: captain is at full HP\n", output);
        Assert.Contains("> item captain 2\ncaptain uses Field Dressing (0 left)\ncaptain heals 10 (hp 22)\n", output);
        Assert.Contains("  items: 1: Iron Sword x38\n", output);
        Assert.Contains("slots count from 1", output);
        Assert.Contains("--strict stops at the first", output);
        Assert.Contains("  item <unit> <slot> [ally] use the item in a slot", output);
        Assert.DoesNotContain("unavailable", output);
    }

    [Theory]
    [InlineData("move captain", "ERROR: usage: move <unit> <x,y>")]
    [InlineData("move captain 9", "ERROR: usage: move <unit> <x,y>")]
    [InlineData("attack captain", "ERROR: usage: attack <unit> <target> [slot]")]
    [InlineData("attack captain brigand-1 x", "ERROR: usage: attack <unit> <target> [slot]")]
    [InlineData("forecast captain brigand-1 2", "ERROR: captain cannot attack with field_dressing: an item, not a weapon")]
    [InlineData("forecast captain brigand-1 0", "ERROR: captain has nothing in slot 0; slots run 1-2")]
    [InlineData("attack captain brigand-1 3", "ERROR: captain has nothing in slot 3; slots run 1-2")]
    [InlineData("item captain 3", "ERROR: captain has nothing in slot 3; slots run 1-2")]
    [InlineData("wait", "ERROR: usage: wait <unit>")]
    [InlineData("end now", "ERROR: usage: end")]
    [InlineData("recall x", "ERROR: usage: recall <n>")]
    [InlineData("item captain", "ERROR: usage: item <unit> <slot> [ally]")]
    [InlineData("item captain 1", "ERROR: Iron Sword is a weapon, not an item; attack with it")]
    [InlineData("forecast captain", "ERROR: usage: forecast <unit> <target> [slot] [art <id>] [from <x,y>]")]
    [InlineData("forecast captain brigand-1 from", "ERROR: usage: forecast <unit> <target> [slot] [art <id>] [from <x,y>]")]
    [InlineData("forecast captain brigand-1 from 1,4 2", "ERROR: usage: forecast <unit> <target> [slot] [art <id>] [from <x,y>]")]
    [InlineData("forecast captain brigand-1 at 1,4", "ERROR: usage: forecast <unit> <target> [slot] [art <id>] [from <x,y>]")]
    [InlineData("forecast captain brigand-1 from 9,9", "ERROR: captain cannot move to 9,9")]
    [InlineData("show", "ERROR: usage: show <unit>")]
    [InlineData("threat", "ERROR: usage: threat <unit> [from <x,y>]")]
    [InlineData("threat captain at 1,4", "ERROR: usage: threat <unit> [from <x,y>]")]
    [InlineData("threat captain from 9,9", "ERROR: captain cannot move to 9,9")]
    [InlineData("threat brigand-1", "ERROR: brigand-1 is an enemy; threat answers for a player unit")]
    [InlineData("reach", "ERROR: usage: reach <unit>")]
    [InlineData("dance", "ERROR: unknown command 'dance'; type help")]
    [InlineData("move captain 9,9", "ERROR: captain cannot move to 9,9")]
    [InlineData("attack captain brigand-1", "ERROR: captain cannot attack brigand-1 from 1,8")]
    [InlineData("show nobody", "ERROR: no living unit 'nobody'")]
    [InlineData("recall 0", "ERROR: history holds 0 states")]
    public void EveryCommandHasAUsageErrorPath(string command, string expected)
    {
        var output = Play(out _, command + "\n");

        Assert.Contains("> " + command + "\n" + expected, output);
    }

    [Fact]
    public void PlayRefusesBadArguments()
    {
        Run(out var bare, "play");
        Assert.Equal(2, bare);
        var output = Run(out var exit, "play", OldMillRoad, "--bogus");
        Assert.Equal(2, exit);
        Assert.StartsWith("ERROR: unexpected argument '--bogus'", output);
        output = Run(out exit, "play", OldMillRoad, "--script", "/no/such.script", "--content", Fixture.RealContentDirectory());
        Assert.Equal(2, exit);
        Assert.StartsWith("ERROR: script file '/no/such.script' not found", output);
        output = Run(out exit, "play", OldMillRoad, "--strict", "--content", Fixture.RealContentDirectory());
        Assert.Equal(2, exit);
        Assert.StartsWith("ERROR: --strict applies to a scripted run; give --script", output);
        Assert.Contains("[--strict]", output);
    }

    /// <summary>Issue 101: a bare map name resolves under the content directory's maps; a path that exists is used as given.</summary>
    [Fact]
    public void PlayAcceptsABareMapName()
    {
        var output = Run(out var exit, "play", "old_mill_road", "--script", "/no/such.script", "--content", Fixture.RealContentDirectory());
        Assert.Equal(2, exit);
        Assert.StartsWith("ERROR: script file", output);
        Assert.Equal(OldMillRoad, PlaySession.ResolveMap("old_mill_road", Fixture.RealContentDirectory()));
        Assert.Equal(OldMillRoad, PlaySession.ResolveMap(OldMillRoad, Fixture.RealContentDirectory()));
        Assert.Equal("no_such_map", PlaySession.ResolveMap("no_such_map", Fixture.RealContentDirectory()));
    }

    /// <summary>
    /// Issue 101: a scripted run ends with every rejected line, numbered as in the file
    /// (blank lines and comments count), with its reason; the per-command ERROR lines stay.
    /// </summary>
    [Fact]
    public void AScriptedRunSummarisesItsRejectionsWithLineNumbers()
    {
        var output = Play(out var exit, "# a comment\n\nmove captain 9,9\nmove wren 2,6\nwait wren\nwait wren\nshow nobody\n");

        Assert.Contains("> move captain 9,9\nERROR: captain cannot move to 9,9", output);
        Assert.Contains("rejected 3 of 5 commands:\n  line 3: move captain 9,9: captain cannot move to 9,9", output);
        Assert.Contains("\n  line 6: wait wren: wren has already acted", output);
        Assert.Contains("\n  line 7: show nobody: no living unit 'nobody'\nbattle ongoing at turn 1, player phase\n", output);
        Assert.Equal(1, exit);
    }

    [Fact]
    public void AStrictRunStopsAtTheFirstRejectionAndAppliesNothingAfterIt()
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-strict-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, "move wren 2,6\nmove captain 9,9\nwait wren\n");
        try
        {
            var output = Run(out var exit, "play", OldMillRoad, "--seed", "7", "--script", path, "--strict", "--content", Fixture.RealContentDirectory());
            Assert.Equal(PlaySession.StrictStop, exit);
            Assert.Contains("wren moves ", output);
            Assert.Contains("-> 2,6", output);
            Assert.Contains("> move captain 9,9\nERROR: captain cannot move to 9,9", output);
            Assert.Contains("strict: stopped at line 2 (move captain 9,9); no later command applied\nrejected 1 of 2 commands:\n  line 2: move captain 9,9:", output);
            Assert.DoesNotContain("> wait wren", output);
            Assert.DoesNotContain("wren waits", output);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheEnemyPhasePlaysOutFromThePlannerAndTheBrigandGoesToThreeSix()
    {
        var output = Play(out _, "# stand still\nend\n");

        Assert.Contains("> end\n-- player phase ends, turn 1 --\n-- enemy phase, turn 1 --\nenemy: wait archer-1\narcher-1 waits\nenemy: move archer-2 4,7\narcher-2 moves 8,7 -> 4,7 via 7,7 6,7 5,7\nenemy: wait archer-2\narcher-2 waits\nenemy: move brigand-1 3,6\nbrigand-1 moves 6,5 -> 3,6 via 5,5 4,5 4,6\nenemy: wait brigand-1\nbrigand-1 waits\nenemy: wait mill_bandit-1\nmill_bandit-1 waits\nenemy: wait soldier-1\nsoldier-1 waits\nenemy: end\n-- enemy phase ends, turn 1 --\n-- player phase, turn 2 --\nOld Mill Road  turn 2 of 12  player phase  rout  recall 3\n", output);
        Assert.Contains(" 6 ...c.....#..\n 7 ....e.....^^\n", output);
        Assert.Contains("group mill, guard, asleep", output);
    }

    /// <summary>
    /// Issue 190: a Recall into the enemy phase's own states is refused without spending a
    /// charge, so the player is never stranded mid enemy phase and the next <c>end</c> plays
    /// on; bare <c>recall</c> lists the state each player turn started at.
    /// </summary>
    [Fact]
    public void ARecallIntoTheEnemyPhaseIsRefusedAndEndStillPlaysOn()
    {
        var output = Play(out _, "end\nrecall\nrecall 3\nend\nrecall\n");

        Assert.DoesNotContain("Unhandled", output);
        Assert.Contains("> recall\nplayer turns start at: turn 1 state 0; history holds 9 states; 3 charges left\n", output);
        Assert.Contains("> recall 3\nERROR: state 3 is inside the enemy phase of turn 1; Recall returns only to a player phase; the nearest player-phase state is 0\n", output);
        Assert.Contains("-- player phase, turn 3 --\n", output);
        Assert.Contains("> recall\nplayer turns start at: turn 1 state 0, turn 2 state 9; history holds 18 states; 3 charges left\n", output);
        Assert.Contains("  recall                   list the state each player turn started at", Play(out _, "help\n"));
    }

    /// <summary>
    /// Issue 151: <c>forecast ... from x,y</c> answers from any tile the unit can still move
    /// to, naming the tile and its terrain, and refuses a tile it cannot stand on or any
    /// other tile once the unit has moved. Nothing moves.
    /// </summary>
    [Fact]
    public void TheForecastAnswersFromAnyTileInReachBeforeTheMoveIsMade()
    {
        var output = Play(out _, "move captain 1,4\nmove wren 2,6\nend\nforecast wren brigand-1 from 4,6\nforecast wren brigand-1 1 from 3,7\nforecast wren brigand-1 from 3,5\nforecast wren brigand-1 from 2,7\nshow wren\nmove wren 3,7\nforecast wren brigand-1 from 4,6\nforecast wren brigand-1 from 3,7\n");

        Assert.Contains("> forecast wren brigand-1 from 4,6\nforecast wren -> brigand-1 from 4,6 (Plain): dmg 10 x2 hit 88% crit 4%; counter: dmg 11 hit 51% crit 0%\n", output);
        Assert.Contains("> forecast wren brigand-1 1 from 3,7\nforecast wren -> brigand-1 from 3,7 (Plain): dmg 10 x2 hit 88% crit 4%; counter: dmg 11 hit 51% crit 0%\n", output);
        Assert.Contains("> forecast wren brigand-1 from 3,5\nERROR: wren cannot move to 3,5\n", output);
        Assert.Contains("> forecast wren brigand-1 from 2,7\nERROR: wren cannot attack brigand-1 from 2,7\n", output);
        Assert.Contains("> show wren\nwren: Wren, Cadet L1, at 2,6 on Plain\n", output);
        Assert.Contains("> forecast wren brigand-1 from 4,6\nERROR: wren has already moved this phase; forecast from 3,7\n", output);
        Assert.Contains("> forecast wren brigand-1 from 3,7\nforecast wren -> brigand-1 from 3,7 (Plain): dmg 10 x2 hit 88% crit 4%; counter: dmg 11 hit 51% crit 0%\n", output);
        Assert.Contains("  forecast <unit> <target> [slot] [art <id>] [from <x,y>]  show the forecast", Play(out _, "help\n"));
    }

    /// <summary>
    /// Issue 217: <c>threat &lt;unit&gt; [from x,y]</c> lists each enemy that could strike the
    /// unit next enemy phase with the weapon the planner would swing, the tile, and the
    /// forecast line, then the total if all land; the enemy phase that follows prints the
    /// same forecasts. Refused from another tile once the unit has moved.
    /// </summary>
    [Fact]
    public void ThreatListsWhatEachEnemyWillStrikeWithAndTheEnemyPhasePrintsTheSame()
    {
        var output = Play(out _, "move captain 1,4\nmove wren 2,6\nend\nthreat wren from 4,6\nmove wren 4,6\nthreat wren from 3,7\nthreat wren\nwait wren\nend\n");

        const string Archer = "archer-2 from 5,5 with Iron Bow (slot 1): dmg 6 hit 70% crit 0%; counter: none";
        const string Brigand = "brigand-1 from 3,6 with Iron Axe (slot 1): dmg 11 hit 51% crit 0%; counter: dmg 10 x2 hit 88% crit 4%";
        var expected = $"threat on wren at 4,6 (Plain):\n  {Archer}\n  {Brigand}\n  if all land: 17 against 9 hp\n";
        Assert.Contains("> threat wren from 4,6\n" + expected, output);
        Assert.Contains("> threat wren from 3,7\nERROR: wren has already moved this phase; threat from 4,6\n", output);
        Assert.Contains("> threat wren\n" + expected, output);
        Assert.Contains("enemy: attack archer-2 wren\nforecast archer-2 -> wren: dmg 6 hit 70% crit 0%; counter: none\n", output);
        Assert.Contains("enemy: attack brigand-1 wren\nforecast brigand-1 -> wren: dmg 11 hit 51% crit 0%; counter: dmg 10 x2 hit 88% crit 4%\n", output);
        Assert.Contains("  threat <unit> [from <x,y>]  what each enemy would strike it with", Play(out _, "help\n"));
    }

    /// <summary>
    /// Issue 248: on an announced map <c>threat</c> prices the enemy an event brings this
    /// enemy phase and marks where it arrives, and it names a sleeping group that could
    /// strike the tile if woken, members and tiles and no numbers, with the wake rule.
    /// </summary>
    [Fact]
    public void ThreatMarksAnAnnouncedArrivalAndNamesASleepingGroupThatCouldReachTheTile()
    {
        const string Lane = "name: Lane\nsize: 16x4\nwin: rout\nturn_limit: 10\nrecall: 3\nenemy_level: 1\nannounce: on\n\n"
            + "................\n................\n................\n................\n\n"
            + "units:\nP captain 0,1\nP recruit:wren 0,3\nE soldier 10,1 group:y behavior:guard\nE archer 10,3 group:y behavior:guard\n\n"
            + "events:\narrival turn 1 enemy spawn soldier 7,0 group:n behavior:aggressive\n";
        var map = Path.Combine(Path.GetTempPath(), "ironwake-lane-" + Guid.NewGuid().ToString("N") + ".map");
        var script = Path.ChangeExtension(map, ".script");
        File.WriteAllText(map, Lane);
        File.WriteAllText(script, "threat captain from 4,1\nthreat wren from 4,3\n");
        try
        {
            var output = Run(out _, "play", map, "--seed", "7", "--script", script, "--content", Fixture.RealContentDirectory());

            Assert.Contains("> threat captain from 4,1\nthreat on captain at 4,1 (Plain):\n  soldier-2 (arrives this enemy phase at 7,0) from 4,0 with Iron Lance (slot 1): dmg 8 hit 62% crit 0%; counter: dmg 9 hit 87% crit 4%\n  if all land: 8 against 22 hp\n> ", output);
            Assert.Contains("> threat wren from 4,3\nthreat on wren at 4,3 (Plain): no enemy can strike it next phase\n  group y asleep, could strike here if woken: archer-1 at 10,3, soldier-1 at 10,1\n  asleep: wakes if a unit ends within 4 tiles of a member, a combat happens within 6, or a member dies\n", output);
        }
        finally
        {
            File.Delete(map);
            File.Delete(script);
        }
    }

    [Fact]
    public void TheForecastPrintsBeforeAnAttackAndEventsRenderStrikeByStrike()
    {
        var output = Play(out _, "move captain 1,4\nmove wren 2,6\nend\nforecast wren brigand-1\nattack wren brigand-1 1\nshow wren\nrecall 0\n");

        Assert.Contains("captain moves 1,8 -> 1,4 via 1,7 1,6 1,5\n", output);
        Assert.Contains("> forecast wren brigand-1\nforecast wren -> brigand-1: dmg 10 x2 hit 88% crit 4%; counter: dmg 11 hit 51% crit 0%\n", output);
        Assert.Contains("> attack wren brigand-1 1\nforecast wren -> brigand-1:", output);
        Assert.Contains("wren attacks brigand-1\n  wren misses brigand-1\n  brigand-1 misses wren\n  wren hits brigand-1 for 10", output);
        Assert.Contains("> show wren\nwren: Wren, Cadet L1, at 2,6 on Plain\n  hp ", output);
        Assert.Contains("weapon: Iron Sword (mt 5 hit 75 crit 0 wt 5 range 1-1)\n", output);
        Assert.Contains("> recall 0\nrecalled to state 0; 2 charges left\n", output);
        Assert.Contains("battle ongoing at turn 1, player phase\n", output);
    }

    /// <summary>
    /// Issue 75: <c>recall list</c> names every state a Recall can return to with the command
    /// that made it and what a rewind there gives back; <c>recall n</c> prints the same cost
    /// as it rewinds, and the replayed attack rolls what it rolled before.
    /// </summary>
    [Fact]
    public void RecallListShowsWhatEachRewindGivesBackAndTheReplayedAttackRollsTheSame()
    {
        var output = Play(out _, "move captain 1,4\nmove wren 2,6\nend\nattack wren brigand-1 1\nrecall list\nrecall 11\nattack wren brigand-1 1\n");

        Assert.Contains("> recall list\nrecall: 3 of 3 charges left, 0 spent; a spent charge does not come back, and the same attack will roll the same\n"
            + "  state 0  turn 1  the start  undoes: gives back 20 exp, 20 enemy hp; returns 11 hp\n"
            + "  state 1  turn 1  after move captain 1,4  undoes: gives back 20 exp, 20 enemy hp; returns 11 hp\n"
            + "  state 2  turn 1  after move wren 2,6  undoes: gives back 20 exp, 20 enemy hp; returns 11 hp\n"
            + "  state 11  turn 2  turn start  undoes: gives back 10 exp, 10 enemy hp\n", output);
        Assert.Contains("> recall 11\nrecalled to state 11; 2 charges left\nundone: gives back 10 exp, 10 enemy hp\nthe rolls do not change: the same attack will roll the same\n", output);
        const string Strikes = "wren attacks brigand-1\n  wren misses brigand-1\n  brigand-1 misses wren\n  wren hits brigand-1 for 10 (hp 2)\n";
        var first = output.IndexOf(Strikes, StringComparison.Ordinal);
        Assert.True(first >= 0);
        Assert.True(output.IndexOf(Strikes, first + 1, StringComparison.Ordinal) > output.IndexOf("> recall 11", StringComparison.Ordinal));
        Assert.Contains("  recall list              every state recall can return to", Play(out _, "help\n"));
    }

    /// <summary>Issue 75: a rewind names the kill it gives back and the unit it returns, and one over moves alone says so.</summary>
    [Fact]
    public void ARewindNamesTheKillItGivesBackAndOneOverMovesSaysMovesOnly()
    {
        const string Script = "move captain 1,4\nmove wren 2,6\nend\nattack wren brigand-1 1\nattack wren brigand-1 1\nrecall 0\n";

        Assert.Contains("> recall 0\nrecalled to state 0; 2 charges left\nundone: gives back 1 kill (brigand-1), 40 exp, 22 enemy hp\n", Play(out _, Script, seed: "5"));
        Assert.Contains("wren falls at 2,6\n", Play(out _, Script, seed: "3"));
        Assert.Contains("> recall 0\nrecalled to state 0; 2 charges left\nundone: gives back 20 enemy hp; returns wren alive, 20 hp\n", Play(out _, Script, seed: "3"));
        Assert.Contains("undone: moves only\n", Play(out _, "move captain 1,4\nrecall 0\n"));
    }

    /// <summary>
    /// Issue 75: a Recall at zero charges is refused with the reason, never ignored, and the
    /// list says the charges are spent rather than offering states it cannot return to.
    /// </summary>
    [Fact]
    public void ARecallAtZeroChargesIsRefusedAndTheListSaysSo()
    {
        var output = Play(out var exit, "move captain 1,4\nrecall 0\nmove captain 1,4\nrecall 0\nmove captain 1,4\nrecall 0\nmove captain 1,4\nrecall list\nrecall 0\n");

        Assert.Contains("> recall list\nrecall: 0 of 3 charges left, 3 spent; a spent charge does not come back, and the same attack will roll the same\n  no charges left: nothing more can be recalled on this map\n", output);
        Assert.Contains("> recall 0\nERROR: no Recall charges left on this map\n", output);
        Assert.Contains("line 9: recall 0: no Recall charges left on this map", output);
    }

    /// <summary>
    /// Issue 152: an enemy attack prints the same forecast line a player's attack does,
    /// between the planner's command and the strikes, so the transcript carries the odds
    /// of the enemy's combats too. The brigand's line is the mirror of Wren's on the same
    /// two tiles: its strike first, her doubled counter after.
    /// </summary>
    [Fact]
    public void AnEnemyAttackPrintsItsForecastBeforeTheStrikes()
    {
        var output = Play(out _, "move captain 1,4\nmove wren 2,6\nend\n");

        Assert.Contains("enemy: attack brigand-1 wren\nforecast brigand-1 -> wren: dmg 11 hit 51% crit 0%; counter: dmg 10 x2 hit 88% crit 4%\nbrigand-1 attacks wren\n  brigand-1 ", output);
    }

    /// <summary>
    /// Issue 11's acceptance: a journaled script under docs/transcripts wins the sample map
    /// under its seed. Keyed rolls keep it stable. The script is Code's play of seed 139 on
    /// `supplies: 1` (issue 160; DECISIONS/0039): both cadets take the trailing
    /// archer on turn 2, the captain holds the fort while the mill comes, and Wren finishes the
    /// bandit at 85 on 10 HP with her one dressing unspent. The seed-101 script spends a
    /// second dressing and no longer replays.
    /// </summary>
    [Fact]
    public void TheJournaledScriptWinsOldMillRoadOnSeedOneThirtyNine()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-25-old_mill_road-139.script");

        var output = Run(out var exit, "play", OldMillRoad, "--seed", "139", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: rout\n", output);
        Assert.DoesNotContain("rejected ", output);
        Assert.DoesNotContain("strict: stopped", output);
        Assert.Contains("archer-2 falls at 4,7", output);
        Assert.Contains("group mill wakes: proximity", output);
        Assert.Contains("  items: 1: Iron Sword x40, 2: Field Dressing x1\n", Play(out _, "show wren\n", "139"));
        Assert.Contains("  ranks: sword E (0), lance E (0), axe E (0)\n", Play(out _, "show captain\n", "139"));
        Assert.Contains("mill_bandit-1 falls at 6,1", output);
        Assert.DoesNotContain("uses Field Dressing", output);
    }

    /// <summary>
    /// Issue 222: Code's play of seed 163 on the Tollgate with the rider arriving by map event.
    /// The woods fight opens on turn 3, the rider arrives at 13,5 as enemy phase 4 opens and
    /// strikes Pell on the flank of it, one Recall on turn 5 buys the rider's kill by the
    /// captain, the captain takes the door, and he seizes on turn 10 of 10.
    /// </summary>
    [Fact]
    public void TheJournaledScriptWinsTheTollgateOnSeedOneSixtyThree()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-25-the_tollgate-163.script");

        var output = Run(out var exit, "play", "the_tollgate", "--seed", "163", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: seize\n", output);
        Assert.DoesNotContain("rejected ", output);
        Assert.Contains("-- enemy phase, turn 4 --\nevent riders\n  rider-1 arrives at 13,5, group flank, aggressive\n", output);
        Assert.Contains("rider-1 attacks pell", output);
        Assert.Contains("recalled to state 49; 2 charges left", output);
        Assert.Contains("rider-1 falls at 9,5", output);
        Assert.Contains("toll_warden-1 falls at 6,2", output);
        Assert.Contains("The Tollgate  turn 10 of 10", output);
    }

    /// <summary>
    /// Issue 222's rule: the Tollgate places no rider, and one spawn event brings rider-1 to
    /// 13,5 at the start of enemy phase 4, under the same id on every replay.
    /// </summary>
    [Fact]
    public void TheTollgateRiderArrivesByEventOnEnemyPhaseFour()
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-play-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, "show rider-1\nend\nend\nend\nend\n");
        try
        {
            var first = Run(out _, "play", "the_tollgate", "--seed", "163", "--script", path, "--content", Fixture.RealContentDirectory());
            var second = Run(out _, "play", "the_tollgate", "--seed", "163", "--script", path, "--content", Fixture.RealContentDirectory());

            var turnOne = first[..first.IndexOf("-- player phase ends, turn 1 --", StringComparison.Ordinal)];
            Assert.DoesNotContain("rider", turnOne.Replace("show rider-1", "").Replace("'rider-1'", ""));
            Assert.Contains("no living unit 'rider-1'", turnOne);
            Assert.DoesNotContain("arrives", first[..first.IndexOf("-- enemy phase, turn 4 --", StringComparison.Ordinal)]);
            Assert.Contains("-- enemy phase, turn 4 --\nevent riders\n  rider-1 arrives at 13,5, group flank, aggressive\n", first);
            Assert.Equal(first, second);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// DESIGN.md 13.8 (Carry the fallen, experiment): Code's play of the raid with
    /// <c>keepsakes: on</c> on seed 6, the heuristic's own trace to the end of enemy phase 2
    /// and by hand from there. Teodor falls to the hexer at 8,7 and his lance stays there;
    /// after the hexer dies, Dunstan steps onto the tile and recovers it, and it carries
    /// Teodor's name in Dunstan's inventory until the rout.
    /// </summary>
    [Fact]
    public void TheJournaledScriptRecoversTeodorsLanceOnTheRaid()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var map = Path.Combine(repo, "docs", "samples", "ironwake_raid_keepsakes.map");
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-26-ironwake_raid_keepsakes-6.script");

        var output = Run(out var exit, "play", map, "--seed", "6", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: rout\n", output);
        Assert.Contains("teodor falls at 8,7\nIron Lance (Teodor's) lies at 8,7\n", output);
        Assert.Contains("keepsakes: Iron Lance (Teodor's) at 8,7\n", output);
        Assert.Contains("dunstan recovers Iron Lance (Teodor's)\n", output);
        Assert.Contains("  items: 1: Iron Lance x39, 2: Iron Lance (Teodor's) x37\n", output);
    }

    /// <summary>
    /// Issue 295's hand play of the keep with <c>keepsakes: on</c> on seed 5: Pell falls at 9,5
    /// outside the wall and Dunstan at 11,8 on the last enemy phase, nobody goes back for
    /// either, and the battle's end names both keepsakes where they were left.
    /// </summary>
    [Fact]
    public void TheJournaledScriptLeavesTwoKeepsakesOnTheKeep()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var map = Path.Combine(repo, "docs", "samples", "ironwake_keep_keepsakes.map");
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-26-ironwake_keep_keepsakes-5.script");

        var output = Run(out var exit, "play", map, "--seed", "5", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: survive\n", output);
        Assert.Contains("pell falls at 9,5\nCinder (Pell's) lies at 9,5\n", output);
        Assert.Contains("Cinder (Pell's) was left at 9,5\nIron Lance (Dunstan's) was left at 11,8\n", output);
    }

    /// <summary>
    /// Issue 302, 13.7's second arm: Code's play of Sallow Grange at <c>dusk: 5</c> on seed 5.
    /// The hexer lights up only when Wren steps beside it, so Pell's range-2 forecast is
    /// refused before and given after; the Reeve, woken by hearing, kills Ansgar from the
    /// dark and is a question mark again; the captain seizes on turn 8.
    /// </summary>
    [Fact]
    public void TheJournaledScriptSeizesSallowGrangeAtDuskFive()
    {
        var output = RunSample("sallow_grange_dusk5.map", "2026-09-26-sallow_grange_dusk5-5.script", 5, out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: seize\n", output);
        Assert.Contains("ansgar falls at 13,6\n", output);
        Assert.Contains("grange_reeve-1 falls at 14,6\n", output);
        Assert.Contains("dusk: sight 1; it gets no darker;", output);
    }

    /// <summary>
    /// Issue 308, 13.7's third arm: Code's play of Sallow Grange at <c>dusk: 5</c> on seed 23.
    /// At sight 2 the fort archer answers Pell's range-2 shot from its own tile and kills her;
    /// after the Recall, the same shot at sight 1 with Ansgar beside the fort reads
    /// <c>counter: none</c>, and Ottilie takes the hexer from 12,6 with no answer out of the dark.
    /// </summary>
    [Fact]
    public void TheJournaledScriptSeizesSallowGrangeUnderTheThirdArm()
    {
        var output = RunSample("sallow_grange_dusk5.map", "2026-09-26-sallow_grange_dusk5-23.script", 23, out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: seize\n", output);
        Assert.Contains("forecast pell -> archer-1 with Cinder: dmg 10 hit 91% crit 2%; counter: dmg 8 hit 86% crit 2%\n", output);
        Assert.Contains("forecast pell -> archer-1 with Cinder: dmg 10 hit 91% crit 2%; counter: none\n", output);
        Assert.Contains("forecast ottilie -> hexer-1: dmg 9 x2 hit 89% crit 4%; counter: none\n", output);
    }

    /// <summary>
    /// Issue 308: Code's play of Brackwater Cut at <c>dusk: 5</c> on seed 23. Knowing of
    /// nobody, the chase makes for the exits through the gap instead of standing still; the
    /// fight it brings to the staging tiles wakes the bank, and only Rook and the captain get out.
    /// </summary>
    [Fact]
    public void TheJournaledScriptEscapesBrackwaterUnderTheThirdArm()
    {
        var output = RunSample("brackwater_cut_dusk5.map", "2026-09-26-brackwater_cut_dusk5-23.script", 23, out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith("escaped: rook, captain; left behind: none; fell: dunstan, pell, wren\n", output);
        Assert.Contains("rider-1 moves 9,3 -> 13,3 via 10,3 11,3 12,3\n", output);
        Assert.Contains("group bank wakes: noise\n", output);
    }

    /// <summary>
    /// Issue 313: with no header, a unit that could strike from that range with more than one
    /// weapon has it named on the forecast line. The Ford Chief starts with the Toll Axe in
    /// front, so its counter at range 1 is the light axe; the captain carries one sword and is
    /// not named.
    /// </summary>
    [Fact]
    public void ATwoWeaponEnemysCounterIsNamedWithoutAHeaderAndTheChiefStartsWithTheTollAxe()
    {
        var output = RunInline(ArmsYard, "forecast captain ford_chief-1 from 2,1\n");

        Assert.Contains("forecast captain -> ford_chief-1 from 2,1 (Plain): dmg ", output);
        Assert.Contains("; counter with Toll Axe: dmg ", output);
    }

    [Fact]
    public void AOneWeaponEnemysLineNamesNoWeapon()
    {
        var output = RunInline(ArmsYard, "forecast captain brigand-1 from 2,2\n");

        Assert.Contains("forecast captain -> brigand-1 from 2,2 (Plain): dmg ", output);
        Assert.Contains("; counter: dmg ", output);
        Assert.DoesNotContain(" with ", output.Split('\n').Single(l => l.StartsWith("forecast captain", StringComparison.Ordinal)));
    }

    /// <summary>Issue 313: at range 2 only the Toll Axe reaches, so the chief's counter is not named there.</summary>
    [Fact]
    public void WhenOnlyOneWeaponReachesThatRangeTheCounterIsNotNamed()
    {
        var output = RunInline(ArmsYard, "forecast ottilie ford_chief-1 from 1,1\n");

        Assert.Contains("forecast ottilie -> ford_chief-1 from 1,1 (Plain): dmg ", output);
        Assert.Contains("; counter: dmg ", output);
    }

    /// <summary>Issue 313: a player unit with two spells names the one it strikes with, and <c>threat</c> names its counter's; against Pell the chief's planner takes the Steel Axe.</summary>
    [Fact]
    public void APlayerUnitWithTwoWeaponsInRangeIsNamedOnForecastAndThreat()
    {
        var output = RunInline(ArmsYard, "forecast pell ford_chief-1 from 1,1\nthreat pell from 2,1\n");

        Assert.Contains("forecast pell -> ford_chief-1 from 1,1 (Plain) with Cinder: dmg ", output);
        Assert.Contains("  ford_chief-1 from 3,1 with Steel Axe (slot 2): dmg ", output);
        Assert.Contains("; counter with Cinder: dmg ", output);
    }

    private const string ArmsYard = """
        name: Arms Yard
        size: 7x3
        win: rout
        turn_limit: 5
        recall: 0
        enemy_level: 1

        .......
        .......
        .......

        units:
        P captain 0,1
        P recruit:ottilie 0,0
        P recruit:pell 0,2
        B ford_chief 3,1 group:yard behavior:boss
        E brigand 3,2 group:far behavior:hold

        """;

    private static string RunInline(string mapText, string scriptText)
    {
        var map = Path.Combine(Path.GetTempPath(), "ironwake-arms-" + Guid.NewGuid().ToString("N") + ".map");
        var script = Path.ChangeExtension(map, ".script");
        File.WriteAllText(map, mapText);
        File.WriteAllText(script, scriptText);
        try
        {
            return Run(out _, "play", map, "--seed", "1", "--script", script, "--content", Fixture.RealContentDirectory());
        }
        finally
        {
            File.Delete(map);
            File.Delete(script);
        }
    }

    private static string RunSample(string map, string script, int seed, out int exit)
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        return Run(
            out exit,
            "play",
            Path.Combine(repo, "docs", "samples", map),
            "--seed",
            seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--script",
            Path.Combine(repo, "docs", "transcripts", script),
            "--strict",
            "--content",
            Fixture.RealContentDirectory());
    }

    private const string DarkWaitMap = """
        name: Dark Wait
        size: 12x3
        win: rout
        turn_limit: 5
        recall: 0
        enemy_level: 1
        dusk: 2

        ............
        ............
        ............

        units:
        P captain 1,1
        E soldier 3,1 group:near behavior:hold
        E soldier 10,1 group:far behavior:hold
        """;

    private static string PlayDarkWait()
    {
        var map = Path.Combine(Path.GetTempPath(), "ironwake-dark-" + Guid.NewGuid().ToString("N") + ".map");
        var script = Path.ChangeExtension(map, ".script");
        File.WriteAllText(map, DarkWaitMap);
        File.WriteAllText(script, "end\n");
        try
        {
            return Run(out _, "play", map, "--seed", "1", "--script", script, "--content", Fixture.RealContentDirectory());
        }
        finally
        {
            File.Delete(map);
            File.Delete(script);
        }
    }

    private static string DarkReachMap(string ottilie) => $"""
        name: Dark Reach
        size: 12x3
        win: rout
        turn_limit: 5
        recall: 0
        enemy_level: 1
        dusk: 1

        ............
        ............
        ............

        units:
        P captain 3,0
        P recruit:ottilie {ottilie}
        E soldier 7,1 group:near behavior:hold
        """;

    private static string PlayDarkReach(string ottilie, string commands)
    {
        var map = Path.Combine(Path.GetTempPath(), "ironwake-reach-" + Guid.NewGuid().ToString("N") + ".map");
        var script = Path.ChangeExtension(map, ".script");
        File.WriteAllText(map, DarkReachMap(ottilie));
        File.WriteAllText(script, commands);
        try
        {
            return Run(out _, "play", map, "--seed", "1", "--script", script, "--content", Fixture.RealContentDirectory());
        }
        finally
        {
            File.Delete(map);
            File.Delete(script);
        }
    }

    /// <summary>
    /// Issue 309, from Chat's cold play of the dusk 5 grange: a forecast from a tile beside an
    /// enemy no one sees yet is given, since the mover would see it from there; at range 2 with
    /// nobody beside the enemy it is refused with the dark's message.
    /// </summary>
    [Fact]
    public void AForecastFromATileNamesAnEnemyTheMoverWouldSeeFromThere()
    {
        var output = PlayDarkReach("3,2", "forecast captain soldier-1 from 6,1\nforecast ottilie soldier-1 from 5,1\nend\n");

        Assert.Contains("> forecast captain soldier-1 from 6,1\nforecast captain -> soldier-1 from 6,1 (", output);
        Assert.Contains("> forecast ottilie soldier-1 from 5,1\nERROR: no unit 'soldier-1' in sight at dusk\n", output);
    }

    /// <summary>
    /// Issue 309: a friend who has already moved beside the enemy sees for the forecast, and
    /// the mover's own tile does not once the forecast reads it elsewhere: an archer beside the
    /// enemy asking from range 2 is refused as a strike, the enemy being on the board.
    /// </summary>
    [Fact]
    public void AForecastFromATileCountsTheSideNowAndNotTheMoversOldTile()
    {
        var spotted = PlayDarkReach("3,2", "move captain 6,1\nforecast ottilie soldier-1 from 5,1\nend\n");
        var left = PlayDarkReach("6,1", "forecast ottilie soldier-1 from 5,1\nend\n");

        Assert.Contains("> forecast ottilie soldier-1 from 5,1\nforecast ottilie -> soldier-1 from 5,1 (", spotted);
        Assert.Contains("> forecast ottilie soldier-1 from 5,1\nERROR: ottilie cannot attack soldier-1 from 5,1\n", left);
    }

    /// <summary>
    /// Issue 301: an unseen enemy that waits prints the same neutral line as one that moves,
    /// so the board never contradicts the line and the silence never tells the two apart.
    /// </summary>
    [Fact]
    public void AnUnseenEnemyThatWaitsPrintsTheNeutralDarkLine()
    {
        var output = PlayDarkWait();

        Assert.Contains("?  unseen at 10,1\n", output);
        Assert.Contains("soldier-1 waits\nenemy: something in the dark acts\nenemy: end\n", output);
        Assert.DoesNotContain("moves in the dark", output);
        Assert.DoesNotContain("soldier-2", output);
    }

    /// <summary>Issue 301: an enemy a player unit can see still prints its own command and event at dusk.</summary>
    [Fact]
    public void ASeenEnemyAtDuskStillPrintsItsFullLine()
    {
        var output = PlayDarkWait();

        Assert.Contains("-- enemy phase, turn 1 --\nenemy: wait soldier-1\nsoldier-1 waits\n", output);
    }

    /// <summary>
    /// Issue 32's acceptance: the map-events sample under docs/samples, played by hand on
    /// seed 7. Teodor ending on the lever at 3,1 opens the wall at 4,1, the reinforcement
    /// arrives from the east edge at the start of enemy phase 3 and walks through the new
    /// gap, and the transcript names both events.
    /// </summary>
    [Fact]
    public void TheJournaledScriptShowsTheSluiceOpenAndTheReinforcementArrive()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var map = Path.Combine(repo, "docs", "samples", "sluice_gate.map");
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-25-sluice_gate-7.script");

        var output = Run(out var exit, "play", map, "--seed", "7", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: rout\n", output);
        Assert.DoesNotContain("rejected ", output);
        Assert.Contains("teodor moves 1,2 -> 3,1 via 2,2 3,2\nevent sluice\n  4,1 becomes Road\n", output);
        Assert.Contains("-- enemy phase, turn 3 --\nevent reinforce\n  brigand-1 arrives at 9,0, group east, aggressive\n", output);
        Assert.Contains("brigand-1 moves 9,0 -> 5,0", output);
        Assert.Equal(1, CountOf(output, "event reinforce"));
    }

    /// <summary>
    /// Issue 33's hand play, Old Mill Road with <c>retreat: on</c>, seed 41, replayed under
    /// issue 204's amendment. The only fort, 6,2, is one the captain can strike next phase,
    /// so on enemy phase 6 the mill bandit on 4 does not fall back: it swings at Wren, and
    /// the script's later lines, written for the old board, are refused.
    /// </summary>
    [Fact]
    public void TheSeed41ScriptNoLongerRetreatsBecauseTheFortIsInTheCaptainsReach()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var map = Path.Combine(repo, "docs", "samples", "old_mill_road_retreat.map");
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-25-old_mill_road_retreat-41.script");

        var output = Run(out _, "play", map, "--seed", "41", "--script", script, "--content", Fixture.RealContentDirectory());

        Assert.DoesNotContain("falls back", output);
        Assert.Contains("enemy: attack mill_bandit-1 wren\n", output);
        Assert.Contains("  mill_bandit-1 hits wren for 10 (hp 2)\n", output);
    }

    /// <summary>
    /// Issue 204's hand play on the river sample, seed 3: the captain kills wingrider-1 on 5
    /// before it can fly, so the rule never fires and the captain seizes on turn 7.
    /// </summary>
    [Fact]
    public void TheRiverRefugeScriptSeizesWithoutARetreat()
    {
        var output = RunRiver("2026-09-25-river_refuge_retreat-3.script", out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: seize\n", output);
        Assert.DoesNotContain("rejected ", output);
        Assert.DoesNotContain("falls back", output);
        Assert.Contains("wingrider-1 falls at 3,7", output);
    }

    /// <summary>
    /// The other branch of the same turn 2: wingrider-1 on 5 is left alive and falls back over
    /// the water to the fort at 2,2 that no player unit can strike. On the second pass it
    /// healed 3 and came straight back; since issue 215 a refugee below half HP holds, so on
    /// 8 of 17 it waits on the fort, and the row says so.
    /// </summary>
    [Fact]
    public void TheRiverRefugeLetGoScriptRetreatsAcrossTheWaterAndHoldsBelowHalf()
    {
        var output = RunRiver("2026-09-25-river_refuge_retreat-3-letgo.script", out _);

        Assert.DoesNotContain("rejected ", output);
        Assert.Contains("enemy: retreat wingrider-1 2,2\nwingrider-1 falls back to 2,2 and will not fight this phase\n", output);
        Assert.Contains("wingrider-1 heals 3 (hp 8)\n", output);
        Assert.Contains("enemy: wait wingrider-1\n", output);
        Assert.DoesNotContain("wingrider-1 moves 2,2 ->", output);
        Assert.Contains("hp 8/17   Fort (heals 20 percent, 3 hp)  group sky, aggressive, holds its refuge until half hp", output);
        Assert.Equal(1, CountOf(output, "falls back"));
    }

    /// <summary>
    /// Issue 215's hand play on the hold sample, seed 5: the captain's forecast on turn 2
    /// names the refuge before he strikes, wingrider-2 falls back there on 5, holds two
    /// enemy phases (5, then 8, below half), returns on 11 to the far bridge head, and the
    /// captain seizes on turn 10 of 10.
    /// </summary>
    [Fact]
    public void TheHoldSampleScriptForecastsTheRetreatHoldsBelowHalfAndSeizes()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var map = Path.Combine(repo, "docs", "samples", "river_refuge_hold.map");
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-25-river_refuge_hold-5.script");

        var output = Run(out var exit, "play", map, "--seed", "5", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: seize\n", output);
        Assert.DoesNotContain("rejected ", output);
        Assert.Contains("forecast captain -> wingrider-2: dmg 12 hit 82% crit 3%; counter: dmg 6 hit 63% crit 0%\n  wingrider-2 would fall back to 2,2 at 5 hp\n", output);
        Assert.Contains("enemy: retreat wingrider-2 2,2\n", output);
        Assert.Contains("wingrider-2 heals 3 (hp 8)\n", output);
        Assert.Contains("enemy: wait wingrider-2\n", output);
        Assert.Contains("wingrider-2 moves 2,2 -> 8,2", output);
        Assert.Equal(1, CountOf(output, "would fall back"));
    }

    private static string RunRiver(string scriptName, out int exit)
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var map = Path.Combine(repo, "docs", "samples", "river_refuge_retreat.map");
        var script = Path.Combine(repo, "docs", "transcripts", scriptName);
        return Run(out exit, "play", map, "--seed", "3", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());
    }

    private static int CountOf(string text, string fragment)
    {
        var count = 0;
        for (var i = text.IndexOf(fragment, StringComparison.Ordinal); i >= 0; i = text.IndexOf(fragment, i + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>Issue 207: <c>show</c> on a unit standing on a fort names the heal, with the HP it gives that unit.</summary>
    [Fact]
    public void ShowOnAFortSaysHowMuchTheFortHeals()
    {
        var map = Path.Combine(Path.GetTempPath(), "ironwake-fort-" + Guid.NewGuid().ToString("N") + ".map");
        var script = Path.ChangeExtension(map, ".script");
        File.WriteAllText(map, Ironwake.Core.Tests.Maps.MapFixture.OldMillRoad.Replace("P captain 1,8\n", "P captain 6,2\n"));
        File.WriteAllText(script, "show captain\nshow wren\n");
        try
        {
            var output = Run(out _, "play", map, "--seed", "7", "--script", script, "--content", Fixture.RealContentDirectory());

            Assert.Contains("> show captain\ncaptain: ", output);
            Assert.Contains("at 6,2 on Fort (heals 20 percent, 4 hp)\n", output);
            Assert.Contains("at 2,8 on Plain\n", output);
        }
        finally
        {
            File.Delete(map);
            File.Delete(script);
        }
    }

    /// <summary>
    /// Issue 256: every trigger and action an announced map can carry reads in player words,
    /// a terrain change and a flag as well as a spawn, and a player-phase turn trigger names
    /// its phase.
    /// </summary>
    [Fact]
    public void AnnouncedEventsOfEveryKindReadInPlayerWords()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var sample = File.ReadAllText(Path.Combine(repo, "docs", "samples", "sluice_gate.map")).ReplaceLineEndings("\n");
        var map = Path.Combine(Path.GetTempPath(), "ironwake-announce-" + Guid.NewGuid().ToString("N") + ".map");
        var script = Path.ChangeExtension(map, ".script");
        File.WriteAllText(map, sample.Replace("enemy_level: 1\n", "enemy_level: 1\nannounce: on\n") + "alarm turn 2 player flag alarm\n");
        File.WriteAllText(script, "");
        try
        {
            var output = Run(out _, "play", map, "--seed", "7", "--script", script, "--content", Fixture.RealContentDirectory());

            Assert.Contains("  when one of yours stops on 3,1: 4,1 becomes road.\n", output);
            Assert.Contains("  turn 3, enemy phase: a brigand arrives at 9,0 (aggressive). A unit standing on 9,0 stops it.\n", output);
            Assert.Contains("  turn 2, player phase: alarm is set.\n", output);
        }
        finally
        {
            File.Delete(map);
            File.Delete(script);
        }
    }

    /// <summary>
    /// Issues 78 and 256: on a map with <c>announce: on</c> the console lists every event before
    /// the first command, in player words with the held-tile rule on each spawn, and <c>map</c>
    /// lists only those still to fire; the Tollgate, which does not announce, prints none.
    /// </summary>
    [Fact]
    public void AnAnnouncedMapListsItsEventsAtTheStartAndMapListsThoseStillToFire()
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-play-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, "end\nend\nend\nmap\n");
        try
        {
            var weir = Run(out _, "play", "harrow_weir", "--seed", "7", "--script", path, "--content", Fixture.RealContentDirectory());
            var tollgate = Run(out _, "play", "the_tollgate", "--seed", "7", "--script", path, "--content", Fixture.RealContentDirectory());

            var start = weir[..weir.IndexOf("> end", StringComparison.Ordinal)];
            Assert.Contains("  turn 3, enemy phase: a rider arrives at 7,0 (aggressive). A unit standing on 7,0 stops it.\n", start);
            Assert.Contains("  turn 5, enemy phase: a brigand arrives at 0,11 (aggressive). A unit standing on 0,11 stops it.\n", start);
            Assert.Contains("  turn 5, enemy phase: a brigand arrives at 7,0 (aggressive). A unit standing on 7,0 stops it.\n", start);
            Assert.DoesNotContain("event: ", start);
            var afterMap = weir[weir.IndexOf("> map", StringComparison.Ordinal)..];
            Assert.DoesNotContain("turn 3, enemy phase", afterMap);
            Assert.Contains("  turn 5, enemy phase: a brigand arrives at 0,11 ", afterMap);
            Assert.Contains("  turn 5, enemy phase: a brigand arrives at 7,0 ", afterMap);
            Assert.DoesNotContain(" enemy phase: a ", tollgate);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Issue 256: Code's play of seed 17 on Harrow Weir with Keziah in Wren's place and
    /// north2 on enemy phase 5. The captain stands on 7,0 from turn 3, so both north waves
    /// are spent; Keziah's axe (8 x2 against the shieldbearer's Def 8, the fists 3 x4) and her
    /// counter leave it at 8 and Pell breaks it; the west brigand arrives on enemy phase 5;
    /// Dunstan, Teodor, Ottilie and Pell take the foreman from 28 to 0 on turn 6.
    /// </summary>
    [Fact]
    public void TheJournaledScriptWinsHarrowWeirOnSeedSeventeen()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-26-harrow_weir-17.script");

        var output = Run(out var exit, "play", "harrow_weir", "--seed", "17", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: defeat_boss\n", output);
        Assert.DoesNotContain("rejected ", output);
        Assert.Contains("event north1 is blocked: its tile is held\n", output);
        Assert.Contains("event north2 is blocked: its tile is held\n", output);
        Assert.Contains("forecast keziah -> shieldbearer-1 with Iron Gauntlets: dmg 3 x4 hit 97% crit 2%", output);
        Assert.Contains("shieldbearer-1 falls at 11,6", output);
        Assert.Contains("  brigand-2 arrives at 0,11, group west, aggressive\n", output);
        Assert.Contains("weir_foreman-1 falls at 13,6", output);
        Assert.Contains("Harrow Weir  turn 6 of 14", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Issue 82's experiment: Code's play of seed 82 on the keep with both walls rebuilt, the
    /// edited keep written by the Sim's <c>--keep --write</c>. Each breach is held from inside,
    /// where only two tiles can strike the holder; the hexer's cast from 9,5 over the wall, the
    /// ditch's tile, kills Ottilie, and Wren falls on the last enemy phase. Survived, no Recall.
    /// </summary>
    [Fact]
    public void TheJournaledScriptSurvivesTheWalledKeepOnSeedEightyTwo()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-26-ironwake_keep-walls-82.script");

        var output = Run(out var exit, "play", Path.ChangeExtension(script, ".map"), "--seed", "82", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: survive\n", output);
        Assert.Contains("hexer-1 hits ottilie for 10 (hp 0)", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Issue 82's experiment: Chat's play of seed 91 on the keep with the ditch and both forts,
    /// the other half of the menu from seed 82's walls. The hexer walks the south row and dies
    /// on 8,8 without reaching the ditch; Teodor falls to a sortie on turn 6 and a Recall takes
    /// it back. Survived, nobody fallen.
    /// </summary>
    [Fact]
    public void TheJournaledScriptSurvivesTheDitchAndFortsKeepOnSeedNinetyOne()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-26-ironwake_keep-ditchforts-91.script");

        var output = Run(out var exit, "play", Path.ChangeExtension(script, ".map"), "--seed", "91", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: survive\n", output);
        Assert.Contains("hexer-1 falls at 8,8", output);
        Assert.Contains("recalled to state 108", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Issue 287: Code's play of seed 287 on the bare keep with the waves run to the clock. The
    /// van strikes the breaches on enemy phase 2, the hexer casts from 9,6 over the wall and kills
    /// Dunstan, Pell falls to an archer that walked in through an unheld breach, and Wren falls
    /// holding 11,3 on the last enemy phase. Two Recalls; survived with three recruits fallen.
    /// </summary>
    [Fact]
    public void TheJournaledScriptSurvivesTheBareKeepOnSeedTwoEightySeven()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-26-ironwake_keep-base-287.script");

        var output = Run(out var exit, "play", Path.ChangeExtension(script, ".map"), "--seed", "287", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: survive\n", output);
        Assert.Contains("hexer-1 hits dunstan for 11 (hp 0)", output);
        Assert.Contains("wren falls at 11,3", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Issue 275: Code's play of seed 61 on Sallow Grange the long way, with the hexer moved
    /// to 13,7. Killing the fort archer wakes the field by noise on turn 3 and the field is
    /// fought out west (three Recalls on turn 4); Pell breaks the north lock from 11,1 in two
    /// casts, the captain stops at 13,3 and 17,3 with no enemy able to strike him, and seizes
    /// on turn 10 of 10. The hexer never fires and the hall wakes only on the winning move.
    /// </summary>
    [Fact]
    public void TheJournaledScriptSeizesSallowGrangeTheLongWayOnSeedSixtyOne()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-26-sallow_grange-61.script");

        var output = Run(out var exit, "play", "sallow_grange", "--seed", "61", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: seize\n", output);
        Assert.DoesNotContain("rejected ", output);
        Assert.Contains("group field wakes: noise", output);
        Assert.Contains("shieldbearer-1 falls at 12,2", output);
        Assert.DoesNotContain("hexer-1 attacks", output);
        Assert.Equal(1, output.Split("group hall wakes").Length - 1);
        Assert.Contains("captain moves 17,3 -> 16,6 via 16,3 16,4 16,5\ngroup hall wakes: proximity\n", output);
        Assert.Contains("Sallow Grange  turn 10 of 10", output);
        Assert.All(new[] { "captain", "wren", "teodor", "pell", "ottilie", "ansgar" }, id => Assert.DoesNotContain(id + " falls at", output));
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Issue 80: Code's play of seed 29 on Brackwater Cut. The first pass holds the gap at 11,3
    /// and loses three units on turn 4, so a Recall goes back to turn 3; the second holds from
    /// 12,3, where the wall leaves one melee tile and one bow tile, until a second Recall to
    /// turn 5. The captain steps onto 19,3 on turn 8 of 8 as the only one left alive and
    /// exits (issue 269, which appended the exit to the script).
    /// </summary>
    [Fact]
    public void TheJournaledScriptEscapesBrackwaterCutOnSeedTwentyNine()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-26-brackwater_cut-29.script");

        var output = Run(out var exit, "play", "brackwater_cut", "--seed", "29", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: escape\nescaped: captain; left behind: none; fell: dunstan, pell, rook, wren\n", output);
        Assert.DoesNotContain("rejected ", output);
        Assert.Contains("recalled to state 54; 2 charges left", output);
        Assert.Contains("recalled to state 96; 1 charges left", output);
        Assert.Contains("archer-2 falls at 11,1", output);
        Assert.Contains("Brackwater Cut  turn 8 of 8", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Issue 269: Code's replay of Chat's seed 73 line on Brackwater Cut under escape by
    /// leaving. Rook leaves from 19,6 on turn 6 instead of standing on the exit at 6 HP,
    /// Dunstan still falls holding 12,3, and on turn 7 Wren, Pell and then the captain exit.
    /// </summary>
    [Fact]
    public void TheJournaledScriptLeavesBrackwaterCutOnSeedSeventyThree()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-26-brackwater_cut-73-exit.script");

        var output = Run(out var exit, "play", "brackwater_cut", "--seed", "73", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: escape\nescaped: rook, wren, pell, captain; left behind: none; fell: dunstan\n", output);
        Assert.Contains("rook leaves through the exit at 19,6", output);
        Assert.Contains("dunstan falls at 12,3", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>Issue 269: <c>exit</c> takes one unit, and one off an exit tile is refused with the core's reason.</summary>
    [Fact]
    public void ExitNeedsAUnitOnAnExitTile()
    {
        var script = Path.Combine(Path.GetTempPath(), "ironwake-exit-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(script, "exit\nexit captain\n");
        try
        {
            var output = Run(out var exit, "play", "brackwater_cut", "--seed", "73", "--script", script, "--content", Fixture.RealContentDirectory());

            Assert.Equal(1, exit);
            Assert.Contains("usage: exit <unit>", output);
            Assert.Contains("captain cannot exit: ", output);
            Assert.Contains(" is not an exit tile", output);
        }
        finally
        {
            File.Delete(script);
        }
    }

    /// <summary>Issue 267: <c>show</c> prints the class's Mov and movement type, one unit of each type on Brackwater Cut.</summary>
    [Theory]
    [InlineData("captain", "mov 4 (infantry)")]
    [InlineData("rider-1", "mov 6 (cavalry)")]
    [InlineData("rook", "mov 6 (flying)")]
    [InlineData("shieldbearer-1", "mov 4 (armored)")]
    public void ShowPrintsMovAndMovementType(string unit, string mov)
    {
        var script = Path.Combine(Path.GetTempPath(), "ironwake-show-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(script, "show " + unit + "\n");
        try
        {
            var output = Run(out _, "play", "brackwater_cut", "--seed", "73", "--script", script, "--content", Fixture.RealContentDirectory());

            var stats = output.Split('\n').SkipWhile(l => l != "> show " + unit).ElementAt(2);
            Assert.StartsWith("  hp ", stats);
            Assert.EndsWith("  " + mov, stats);
        }
        finally
        {
            File.Delete(script);
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

/// <summary>The Sim's <c>--full</c> prints all eight gate rows for a map and <c>--trace</c> prints a replayable script.</summary>
[Collection("console")]
public class SimFullTests
{
    [Fact]
    public void FullPrintsEightGateRowsForOneMap()
    {
        var output = Capture(() => Ironwake.Sim.Program.Full("old_mill_road", 3));
        for (var gate = 1; gate <= 8; gate++)
        {
            Assert.Contains($"gate {gate} ", output);
        }

        Assert.Contains("full: ", output);
    }

    [Fact]
    public void FullUnderADifficultyNamesItAndAnUnknownOneIsRefusedNamingTheKnown()
    {
        var output = Capture(() => Ironwake.Sim.Program.Main(new[] { "--full", "old_mill_road", "--seeds", "2", "--difficulty", "normal" }));
        Assert.Contains("2 seeds, two-roll average, difficulty normal\n", output);
        Assert.Contains("gate 6 ", output);

        var refused = 0;
        var unknown = Capture(() => refused = Ironwake.Sim.Program.Main(new[] { "--full", "old_mill_road", "--seeds", "2", "--difficulty", "brutal" }));
        Assert.Equal(2, refused);
        Assert.Contains("full: no difficulty 'brutal'; they are normal", unknown);
        Assert.DoesNotContain("gate 1", unknown);
    }

    [Fact]
    public void FullNamesTheMapsWhenTheMapIsUnknown()
    {
        var output = Capture(() => Ironwake.Sim.Program.Full("no_such_map", 3));
        Assert.Contains("no map 'no_such_map'", output);
        Assert.Contains("old_mill_road", output);
    }

    [Fact]
    public void TraceNamesTheSchemeAndAnUnknownSchemeIsRefusedWithUsage()
    {
        var one = Capture(() => Ironwake.Sim.Program.Main(new[] { "--trace", "old_mill_road", "3", "--scheme", "one" }));
        Assert.StartsWith("# old_mill_road seed 3, heuristic player, one roll\n", one);
        var refused = 0;
        var output = Capture(() => refused = Ironwake.Sim.Program.Main(new[] { "--full", "old_mill_road", "--seeds", "3", "--scheme", "both" }));
        Assert.Equal(2, refused);
        Assert.Contains("[--scheme one|two]", output);
        Assert.DoesNotContain("gate 1", output);
    }

    /// <summary>
    /// Issue 132: gate rows and forecasts print numbers the same on every machine and
    /// locale. Gate 1's win rate printed as <c>0 %</c> on Linux CI and <c>0%</c> on an
    /// en-US Windows desktop until every project ran under invariant globalization
    /// (Directory.Build.props); this fails if a project drops the setting.
    /// </summary>
    [Fact]
    public void ConsoleOutputIsCultureFree()
    {
        Assert.Equal(System.Globalization.CultureInfo.InvariantCulture, System.Globalization.CultureInfo.CurrentCulture);
        Assert.Equal("0 %", string.Format("{0:P0}", 0.0));
        Assert.Equal("0.630", string.Format("{0:F3}", 0.63));
    }

    [Fact]
    public void TraceEndsWithTheOutcome()
    {
        var output = Capture(() => Ironwake.Sim.Program.Trace("old_mill_road", 3));
        Assert.StartsWith("# old_mill_road seed 3", output);
        Assert.Contains("\nend\n", output);
        Assert.Matches("# (Won|Lost) on turn", output);
    }

    /// <summary>
    /// Issue 118: a trace is a script the CLI replays. Slots print one-based as the CLI reads
    /// them, and the enemy's commands print as comments, since the CLI plays the enemy phase
    /// itself from the same planner. Seed 20 on Old Mill Road has Wren use her dressing; the
    /// trace replayed under --strict applies every line and ends where the Sim said.
    /// </summary>
    [Fact]
    public void ATraceWithAnItemLineReplaysInTheCliUnderStrict()
    {
        var trace = Capture(() => Ironwake.Sim.Program.Trace("old_mill_road", 20));
        Assert.Contains("\nitem wren 2\n", trace);
        Assert.DoesNotContain("\nenemy:", trace);
        Assert.Contains("\n# enemy: wait archer-1\n", trace);
        var outcome = System.Text.RegularExpressions.Regex.Match(trace, "# (Won|Lost) on turn (\\d+): (.*)\n$");
        Assert.True(outcome.Success, trace);
        var turn = int.Parse(outcome.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal("item wren 2", Ironwake.Sim.Program.Script(new UseItem("wren", 1)));
        Assert.Equal("item wren 1 captain", Ironwake.Sim.Program.Script(new UseItem("wren", 0, "captain")));
        Assert.Equal("attack wren brigand-1 2", Ironwake.Sim.Program.Script(new Attack("wren", "brigand-1", 1)));
        Assert.Equal("attack wren brigand-1", Ironwake.Sim.Program.Script(new Attack("wren", "brigand-1")));

        var path = Path.Combine(Path.GetTempPath(), "ironwake-trace-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, trace);
        try
        {
            var exit = 0;
            var output = Capture(() => exit = Ironwake.Cli.Program.Main(new[] { "play", "old_mill_road", "--seed", "20", "--script", path, "--strict", "--content", Fixture.RealContentDirectory() }));

            Assert.Equal(0, exit);
            Assert.DoesNotContain("rejected ", output);
            Assert.DoesNotContain("strict: stopped", output);
            Assert.Contains("> item wren 2\nwren uses Field Dressing", output);
            Assert.Contains($"turn {turn} of ", output);
            Assert.DoesNotContain($"turn {turn + 1} of ", output);
            Assert.EndsWith($"battle {outcome.Groups[1].Value.ToLowerInvariant()}: {outcome.Groups[3].Value}\n", output);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// The heuristic's Canto (issue 262) prints as the CLI's own <c>canto</c> line, the tile
    /// even when it is a stay. Seed 3 on Sallow Grange has Ansgar ride south from 5,8 after
    /// striking; the trace replayed under --strict applies every line.
    /// </summary>
    [Fact]
    public void ATraceWithACantoLineReplaysInTheCliUnderStrict()
    {
        Assert.Equal("canto ansgar 3,7", Ironwake.Sim.Program.Script(new Canto("ansgar", new Coord(3, 7))));
        var trace = Capture(() => Ironwake.Sim.Program.Trace("sallow_grange", 3));
        Assert.Contains("\ncanto ansgar 4,10\n", trace);

        var path = Path.Combine(Path.GetTempPath(), "ironwake-trace-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, trace);
        try
        {
            var exit = 0;
            var output = Capture(() => exit = Ironwake.Cli.Program.Main(new[] { "play", "sallow_grange", "--seed", "3", "--script", path, "--strict", "--content", Fixture.RealContentDirectory() }));

            Assert.Equal(0, exit);
            Assert.DoesNotContain("rejected ", output);
            Assert.DoesNotContain("strict: stopped", output);
            Assert.Contains("ansgar cantos 5,8 -> 4,10", output);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string Capture(Action run) =>
        ConsoleCapture.Run(run, Path.GetDirectoryName(Fixture.RealContentDirectory())!);
}
