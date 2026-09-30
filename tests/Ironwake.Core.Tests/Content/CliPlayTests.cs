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
    /// <summary>Brackwater Cut as it shipped before <c>dusk: 5</c> (issue 318); its daylight plays replay on it.</summary>
    private static string BrackwaterDaylight => Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "brackwater_cut_daylight.map");

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

    /// <summary>
    /// Issue 331: on the grudges sample, Code's seed 65 script prints the sworn crit avoid
    /// under a forecast between a sworn unit and its enemy, and a grudge strike's best
    /// alternative under the enemy's forecast.
    /// </summary>
    [Fact]
    public void AGrudgeStrikePrintsTheSwornCritAvoidAndThePlannersBestAlternative()
    {
        var docs = Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs");
        var output = Run(
            out _, "play", Path.Combine(docs, "samples", "old_mill_road_grudges.map"), "--seed", "65", "--strict",
            "--script", Path.Combine(docs, "transcripts", "2026-09-26-old_mill_road_grudges-65.script"), "--content", Fixture.RealContentDirectory());

        Assert.Contains("enemy: attack archer-2 wren\nforecast archer-2 -> wren: dmg 6 hit 70% crit 20%; counter: none\n  sworn: archer-2 on wren: wren crit avoid -20\n  grudge: archer-2 strikes sworn wren (score 15.9); best alternative captain (score 13.4)\n", output);
        Assert.Contains("battle won: rout", output);
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
    [InlineData("attack captain", "ERROR: usage: attack <unit> <target> [slot|weapon]")]
    [InlineData("attack captain brigand-1 x", "ERROR: captain carries no 'x'; slots: 1 Iron Sword, 2 Field Dressing")]
    [InlineData("attack captain brigand-1 1,4", "ERROR: usage: attack <unit> <target> [slot|weapon]")]
    [InlineData("forecast captain brigand-1 2", "ERROR: captain cannot attack with field_dressing: an item, not a weapon")]
    [InlineData("forecast captain brigand-1 0", "ERROR: captain has nothing in slot 0; slots run 1-2")]
    [InlineData("attack captain brigand-1 3", "ERROR: captain has nothing in slot 3; slots run 1-2")]
    [InlineData("item captain 3", "ERROR: captain has nothing in slot 3; slots run 1-2")]
    [InlineData("wait", "ERROR: usage: wait <unit>")]
    [InlineData("end now", "ERROR: usage: end")]
    [InlineData("recall x", "ERROR: usage: recall <n>")]
    [InlineData("item captain", "ERROR: usage: item <unit> <slot> [ally]")]
    [InlineData("item captain 1", "ERROR: Iron Sword is a weapon, not an item; attack with it")]
    [InlineData("forecast captain", "ERROR: usage: forecast <unit> <target> [slot|weapon] [art <id>] [from <x,y>]")]
    [InlineData("forecast captain brigand-1 from", "ERROR: usage: forecast <unit> <target> [slot|weapon] [art <id>] [from <x,y>]")]
    [InlineData("forecast captain brigand-1 from 1,4 2", "ERROR: usage: forecast <unit> <target> [slot|weapon] [art <id>] [from <x,y>]")]
    [InlineData("forecast captain brigand-1 at 1,4", "ERROR: usage: forecast <unit> <target> [slot|weapon] [art <id>] [from <x,y>]")]
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
        Assert.Contains("  forecast <unit> <target> [slot|weapon] [art <id>] [from <x,y>]  show the forecast", Play(out _, "help\n"));
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
            Assert.Contains("> threat wren from 4,3\nthreat on wren at 4,3 (Plain): no enemy can strike it next phase\n  group y asleep, could strike here if woken: archer-1 at 10,3\n  asleep: wakes if a unit ends within 4 tiles of a member, a combat happens within 6, or a member dies\n", output);
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
    /// captain, the captain takes the door, and he seizes on turn 10 of 10. Issue 351 moved the
    /// shipped spawn to 13,4, so the line replays on the map it was played on.
    /// </summary>
    [Fact]
    public void TheJournaledScriptWinsTheTollgateOnSeedOneSixtyThree()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-25-the_tollgate-163.script");

        var output = Run(out var exit, "play", ProtocolSessionTests.TollgateRowFive, "--seed", "163", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

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
    /// Issue 370: the Tollgate places no rider and no longer brings one on a clock. With no
    /// player unit stopping on the door's tiles 6,4 or 6,3, four full turns pass and rider-1
    /// never arrives; the same script replays to the same text.
    /// </summary>
    [Fact]
    public void TheTollgateRiderDoesNotArriveOnAClock()
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-play-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, "show rider-1\nend\nend\nend\nend\nend\nshow rider-1\n");
        try
        {
            var first = Run(out _, "play", "the_tollgate", "--seed", "163", "--script", path, "--content", Fixture.RealContentDirectory());
            var second = Run(out _, "play", "the_tollgate", "--seed", "163", "--script", path, "--content", Fixture.RealContentDirectory());

            Assert.Contains("-- enemy phase, turn 4 --", first);
            Assert.DoesNotContain("event riders", first);
            Assert.DoesNotContain("rider-1 arrives", first);
            Assert.Equal(2, first.Split("ERROR: no living unit 'rider-1'").Length - 1);
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
    /// Issue 315: Chat's cold play of Sallow Grange at <c>dusk: 5</c> on seed 17. On enemy
    /// phase 3 the brawler on 11 HP swings Iron Gauntlets at Pell on 16: a gauntlet's round
    /// is both strikes before her counter, so the 9 x2 is a real kill if both land, and one
    /// missing is what kept her alive. The captain seizes on turn 8 with nobody lost.
    /// </summary>
    [Fact]
    public void ChatsSallowScriptOnSeedSeventeenShowsAGauntletRoundLandingBeforeTheCounter()
    {
        var output = RunSample("sallow_grange_dusk5.map", "2026-09-26-sallow_grange_dusk5-17.script", 17, out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: seize\n", output);
        Assert.Contains("  brawler-1 hits pell for 9 (hp 7)\n  brawler-1 misses pell\n  pell hits brawler-1 for 13 (hp 0)\n", output);
    }

    /// <summary>
    /// Issues 317 and 321: Sallow Grange at <c>dusk: 5</c> with the field group Aggressive,
    /// the brawler at 8,6 and the archer at 7,6 (fifty-second round). Knowing of nobody after
    /// the turn 1 Recall, it leaves 7,5 7,6 8,6 for the throne on enemy phase 1 and stops in
    /// the wall's gap; on enemy phase 2 the brawler takes the throne at 16,6, and the archer,
    /// the only objective tile held, stops short at 14,6.
    /// </summary>
    [Fact]
    public void TheAggressiveFieldGroupDriftsToTheThroneInTheDark()
    {
        var output = RunSample("sallow_grange_dusk5_drift.map", "2026-09-26-sallow_grange_dusk5_drift-41.script", 41, out var exit);

        Assert.Equal(0, exit);
        Assert.Contains("unseen at 4,2 12,2 7,5 7,6 8,6 15,6 13,7\n", output);
        Assert.Contains("unseen at 4,2 12,2 11,5 11,6 12,6 15,6 13,7\n", output);
        var turnThree = output[output.IndexOf("-- player phase, turn 3 --", StringComparison.Ordinal)..];
        Assert.Contains("unseen at 4,2 12,2 11,5 14,6 15,6 16,6 13,7\n", turnThree);
    }

    /// <summary>
    /// Issue 321: Code's replay of Chat's seed 41 line on the brawler placement, played on by
    /// hand once it diverged. The brawler holds the throne through enemy phases 3 to 8, since
    /// it can strike nobody; on enemy phase 9, with Pell on the ring at range 2, it steps off
    /// to 15,6 to kill her, and the captain walks onto the empty throne on turn 10.
    /// </summary>
    [Fact]
    public void TheSeatedBrawlerStepsOffOnlyToStrikeAndTheCaptainSeizes()
    {
        var output = RunSample("sallow_grange_dusk5_drift.map", "2026-09-26-sallow_grange_dusk5_drift-41.script", 41, out var exit);

        Assert.Equal(0, exit);
        string Between(string from, string to) =>
            output[output.IndexOf(from, StringComparison.Ordinal)..output.IndexOf(to, StringComparison.Ordinal)];
        IEnumerable<string> Lines(string text, string prefix) =>
            text.Split('\n').Where(line => line.StartsWith(prefix, StringComparison.Ordinal));

        var unseen = Lines(Between("-- player phase, turn 3 --", "-- player phase, turn 9 --"), "?  unseen at ").ToList();
        var spotted = Lines(Between("-- player phase, turn 9 --", "-- enemy phase, turn 9 --"), "c  brawler-1 ").ToList();
        Assert.NotEmpty(unseen);
        Assert.NotEmpty(spotted);
        Assert.All(unseen, line => Assert.Contains(" 16,6 ", line));
        Assert.All(spotted, line => Assert.Contains(" 16,6 ", line));
        Assert.Contains("brawler-1 moves 16,6 -> 15,6\nenemy: attack brawler-1 pell\n", output);
        Assert.Contains("pell falls at 14,6\n", output);
        Assert.EndsWith("battle won: seize\n", output);
    }

    /// <summary>
    /// Chat's cold play of Brackwater Cut at <c>dusk: 5</c> on seed 17 under the third arm, played
    /// on the sample that is now the shipped map (issue 318):
    /// rider-1 drifts onto the route and is killed there, and all five walk out on turn 5.
    /// Since issue 377 it replays on the shipped map's pre-377 copy, which carries <c>exit_after_move: on</c>.
    /// </summary>
    [Fact]
    public void ChatsBrackwaterScriptOnSeedSeventeenEscapesWithAllFive()
    {
        var output = RunSample("brackwater_cut_exit_after_move.map", "2026-09-26-brackwater_cut_dusk5-17.script", 17, out var exit);

        Assert.Equal(0, exit);
        Assert.Contains("rider-1 falls at", output);
        Assert.EndsWith("escaped: rook, dunstan, pell, wren, captain; left behind: none; fell: none\n", output);
    }

    /// <summary>
    /// Issue 308: Code's play of Brackwater Cut at <c>dusk: 5</c> on seed 23, on the sample that is
    /// now the shipped map (issue 318). Knowing of
    /// nobody, the chase makes for the exits through the gap instead of standing still; the
    /// fight it brings to the staging tiles wakes the bank, and only Rook and the captain get out.
    /// Since issue 377 it replays on the shipped map's pre-377 copy, which carries <c>exit_after_move: on</c>.
    /// </summary>
    [Fact]
    public void TheJournaledScriptEscapesBrackwaterUnderTheThirdArm()
    {
        var output = RunSample("brackwater_cut_exit_after_move.map", "2026-09-26-brackwater_cut_dusk5-23.script", 23, out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith("escaped: rook, captain; left behind: none; fell: dunstan, pell, wren\n", output);
        Assert.Contains("rider-1 moves 9,3 -> 13,3 via 10,3 11,3 12,3\n", output);
        Assert.Contains("group bank wakes: noise\n", output);
    }

    /// <summary>
    /// Issue 318's play of the shipped Brackwater Cut at <c>dusk: 5</c> on seed 41, replayed
    /// after issue 326. Its transcript is kept as the record of the cork: there, with Wren on
    /// the gap at 11,3, no chase unit had a path to an exit and all nine waited. Drift now
    /// paths as if the party were not there, so the chase walks up to the gap, strikes Wren
    /// there on enemy phase 3 and kills her on enemy phase 4, and the script stops at its
    /// first command for her.
    /// Since issue 377 it replays on the shipped map's pre-377 copy, which carries <c>exit_after_move: on</c>.
    /// </summary>
    [Fact]
    public void TheJournaledCorkNoLongerFreezesTheChaseOnTheShippedDuskMap()
    {
        var output = RunSample("brackwater_cut_exit_after_move.map", "2026-09-26-brackwater_cut-41.script", 41, out var exit);

        Assert.NotEqual(0, exit);
        var enemyThree = output[output.IndexOf("-- enemy phase, turn 3 --", StringComparison.Ordinal)..];
        Assert.Contains("brigand-1 moves 8,3 -> 10,3 via 9,3\n", enemyThree);
        Assert.Contains("  brigand-1 hits wren for 12 (hp 8)\n", enemyThree);
        Assert.Contains("wren falls at 11,3\n", output);
        Assert.Contains("strict: stopped at line 39 (move wren 15,3)", output);
    }

    /// <summary>
    /// Issue 326: Code's play of the shipped Brackwater Cut at <c>dusk: 5</c> on seed 53 with
    /// drift pathing as if the party were not there. The chase reaches Dunstan on the gap on
    /// enemy phase 2 and kills him on phase 3; on phase 5 rider-2 drifts onto the exit at 19,3,
    /// and the captain spots it so Pell can shoot it from 19,1. Wren, Rook and the captain
    /// escape on turn 7.
    /// Since issue 377 it replays on the shipped map's pre-377 copy, which carries <c>exit_after_move: on</c>.
    /// </summary>
    [Fact]
    public void TheJournaledScriptFightsTheChaseAtTheGapAndOnTheExit()
    {
        var output = RunSample("brackwater_cut_exit_after_move.map", "2026-09-26-brackwater_cut-53.script", 53, out var exit);

        Assert.Equal(0, exit);
        Assert.Contains("rider-1 attacks dunstan\n", output);
        Assert.Contains("dunstan falls at 11,3\n", output);
        Assert.Contains("rider-2 moves 13,3 -> 19,3 via 14,3 15,3 16,3 17,3 18,3\n", output);
        Assert.Contains("  pell hits rider-2 for 12 (hp 0)\n", output);
        Assert.EndsWith("escaped: wren, rook, captain; left behind: none; fell: dunstan, pell\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "transcripts", "2026-09-26-brackwater_cut-53.txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Issue 313: Code's play of the reworked Ford Chief (Toll Axe in front, then Steel Axe) on
    /// seed 47. With the captain inside the Toll Axe's reach on turn 10 the chief takes the free
    /// range-2 swing and keeps the light axe; on turn 11 Teodor is the only unit in reach and it
    /// equips the Steel Axe to strike him, so on turn 13, after a Recall and a standoff, the
    /// cadets double into the heavy axe and the captain finishes it.
    /// </summary>
    [Fact]
    public void TheJournaledScriptBaitsTheFordChiefOntoTheSteelAxeAndRoutsIt()
    {
        var output = RunSample("saltmarsh_ford_chief.map", "2026-09-26-saltmarsh_ford_chief-47.script", 47, out var exit);

        Assert.Equal(0, exit);
        Assert.Contains("forecast ford_chief-1 -> captain: dmg 10 hit 43% crit 0%; counter: none\n", output);
        Assert.Contains("forecast ford_chief-1 -> teodor with Steel Axe: dmg 16 hit 74% crit 1%; counter: dmg 8 hit 58% crit 0%\nford_chief-1 equips Steel Axe\n", output);
        Assert.Contains("forecast captain -> ford_chief-1: dmg 8 x2 hit 74% crit 4%; counter with Steel Axe: dmg 15 hit 62% crit 0%\n", output);
        Assert.EndsWith("battle won: rout\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "transcripts", "2026-09-26-saltmarsh_ford_chief-47.txt")).ReplaceLineEndings("\n"), output);
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

    /// <summary>
    /// Issue 355's acceptance: both journaled plays of shove's second arm still replay under
    /// <c>--strict</c> once the enemy half and heft are cut, since neither pushed an enemy, and
    /// the console prints no heft on either map.
    /// </summary>
    [Theory]
    [InlineData("sallow_grange_shove.map", "2026-09-27-sallow_grange_shove-83.script", 83, "battle won: seize\n")]
    [InlineData("brackwater_cut_shove.map", "2026-09-27-brackwater_cut_shove-67.script", 67, "left behind: pell; fell: none\n")]
    public void TheJournaledShovePlaysReplayWithNoHeft(string map, string script, int seed, string ending)
    {
        var output = RunSample(map, script, seed, out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith(ending, output);
        Assert.DoesNotContain("rejected ", output);
        Assert.DoesNotContain("strict: stopped", output);
        Assert.Contains(" shoves ", output);
        Assert.DoesNotContain("heft", output);
    }

    /// <summary>
    /// 13.13's first play: Code's seed 97 on <c>docs/samples/sallow_grange_pincer.map</c>. Ansgar
    /// rides to 5,6 so Wren pins the brawler (93 percent where 4,7 read 77), Wren finishes it
    /// pinned by Ansgar after a Recall, and on turn 7 the captain's forecast on the Reeve reads
    /// the pin Wren makes from 14,5. The captain seizes on turn 8.
    /// </summary>
    [Fact]
    public void TheJournaledPincerPlayReplaysWithItsPins()
    {
        var output = RunSample("sallow_grange_pincer.map", "2026-09-27-sallow_grange_pincer-97.script", 97, out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: seize\n", output);
        Assert.Contains("  pincer: brawler-1 pinned by wren: ansgar hit +15\n", output);
        Assert.Contains("  pincer: brawler-1 pinned by ansgar: wren hit +15\n", output);
        Assert.Contains("  pincer: grange_reeve-1 pinned by wren: captain hit +15\n", output);
        Assert.Contains("ottilie falls at 12,6\n", output);
    }

    /// <summary>
    /// 13.13's second play: Chat's cold seed 113 on <c>docs/samples/sallow_grange_pincer.map</c>.
    /// Wren waits on 4,4 so the captain strikes the brawler pinned, Ansgar takes 6,5 so Teodor
    /// pins the archer, and on turn 7 Ansgar stands on 14,8 as the anvil for the captain's strike
    /// on the Reeve. Nobody falls; the captain seizes on turn 8.
    /// </summary>
    [Fact]
    public void ChatsColdPincerPlayReplaysWithItsPins()
    {
        var output = RunSample("sallow_grange_pincer.map", "2026-09-27-sallow_grange_pincer-113.script", 113, out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: seize\n", output);
        Assert.Contains("  pincer: brawler-1 pinned by wren: captain hit +15\n", output);
        Assert.Contains("  pincer: archer-2 pinned by teodor: ansgar hit +15\n", output);
        Assert.Contains("  pincer: grange_reeve-1 pinned by ansgar: captain hit +15\n", output);
        Assert.Contains("grange_reeve-1 falls at 14,7\n", output);
    }

    /// <summary>
    /// 13.14's first play: Code's seed 307 on <c>docs/samples/harrow_weir_brace.map</c>. Ottilie
    /// waits braced as bait for the west brigand at 31 percent and it misses; the Foreman waits
    /// braced on his hill until Teodor's strike draws his answer, and Pell kills him unbraced
    /// on turn 11.
    /// </summary>
    [Fact]
    public void TheJournaledBracePlayReplaysWithItsBraces()
    {
        var output = RunSample("harrow_weir_brace.map", "2026-09-27-harrow_weir_brace-307.script", 307, out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: defeat_boss\n", output);
        Assert.Contains("forecast brigand-2 -> ottilie: dmg 12 hit 31% crit 0%; counter: none\n  brace: ottilie braced: brigand-2 hit -15\n", output);
        Assert.Contains("  brace: weir_foreman-1 braced: teodor hit -15\n", output);
        Assert.Contains("forecast pell -> weir_foreman-1 with Cinder: dmg 12 hit 94% crit 1%", output);
        Assert.Contains("weir_foreman-1 falls at 13,6\n", output);
    }

    /// <summary>
    /// 13.15's first play: Code's seed 457 on <c>docs/samples/the_tollgate_wildfire.map</c>. Pell's
    /// Cinder sets the archer's wood alight on turn 2, the fire walks onto the brigand's tile at
    /// the next player phase and Wren doubles it at 88 with no forest under it, and on turn 4 the
    /// fire has reached the door approach, where the warden strikes Teodor at 67 with no cover,
    /// and Teodor, still standing in it at the next player phase start, burns for 4.
    /// </summary>
    [Fact]
    public void TheJournaledWildfirePlayReplaysWithItsFire()
    {
        var output = RunSample("the_tollgate_wildfire.map", "2026-09-28-the_tollgate_wildfire-457.script", 457, out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: seize\n", output);
        Assert.Contains("  5,5 becomes Fire\n", output);
        Assert.Contains("archer-2 burns 3 (hp 2)\n", output);
        Assert.Contains("  6,5 becomes Fire\n  5,6 becomes Fire\n", output);
        Assert.Contains("forecast wren -> toll_brigand-1: dmg 10 x2 hit 88% crit 4%; counter: dmg 11 hit 41% crit 0%\n", output);
        Assert.Contains("  6,4 becomes Fire\n", output);
        Assert.Contains("forecast toll_warden-1 -> teodor: dmg 7 hit 67% crit 1%; counter: none\n", output);
        Assert.Contains("  wildfire: pell ignites 5,5 on a hit\n", output);
        Assert.Contains("fire: burning 5,5; next front 6,5 5,6\n", output);
        Assert.Contains("teodor burns 4 (hp 2)\n", output);
    }

    /// <summary>
    /// 13.16's first play: Code's seed 461 on <c>docs/samples/the_tollgate_windup.map</c>. On turn 5
    /// the mauler raises a blow over the door with Teodor on it at 5 hp; on turn 6 Pell's Cinder
    /// from 6,4 hits the mauler from outside its reach, which leaves the blow raised (round 99),
    /// Teodor steps off the door, and the blow falls on empty ground. The map is seized on turn 10.
    /// </summary>
    [Fact]
    public void TheJournaledWindupPlayReplaysWithItsBlow()
    {
        var output = RunSample("the_tollgate_windup.map", "2026-09-28-the_tollgate_windup-461.script", 461, out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: seize\n", output);
        Assert.Contains("forecast toll_mauler-1 -> teodor: dmg 14 hit -- crit --; counter: none\n", output);
        Assert.Contains("toll_mauler-1 raises a blow over 6,3 (teodor); it lands at toll_mauler-1's next phase start\n", output);
        Assert.Contains("blows: toll_mauler-1 over 6,3 (teodor 14, sure)\n", output);
        Assert.Contains("under a blow from toll_mauler-1 (14, sure)", output);
        Assert.Contains("  windup: a hit from 6,4 does not break toll_mauler-1's blow over 6,3 (outside its reach)\n", output);
        Assert.Contains("toll_mauler-1's blow falls on empty ground at 6,3\n", output);
        Assert.DoesNotContain("is broken", output);
        Assert.DoesNotContain("blow lands on", output);
    }

    /// <summary>
    /// 13.17's first play: Code's seed 491 on <c>docs/samples/harrow_weir_overwatch.map</c>. On turn 5
    /// Pell's watch from 8,6 fires on the brigand that ends on 8,8, and Ottilie's from 9,2 fires on
    /// the Foreman as he lands on the crest at 11,2; on turn 6 Teodor and Ottilie kill him there.
    /// </summary>
    [Fact]
    public void TheJournaledOverwatchPlayReplaysWithItsShots()
    {
        var output = RunSample("harrow_weir_overwatch.map", "2026-09-29-harrow_weir_overwatch-491.script", 491, out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: defeat_boss\n", output);
        Assert.Contains("ottilie watches from 9,2; no strike passed up\n", output);
        Assert.Contains("pell's watch fires on brigand-3 at 8,8: hit 12 (brigand-3 hp 10)\n", output);
        Assert.Contains("ottilie's watch fires on weir_foreman-1 at 11,2: hit 6 (weir_foreman-1 hp 22)\n", output);
        Assert.Contains("ottilie is struck and stops watching\n", output);
        Assert.Contains("watches: archer-1 at 14,4 over 14,2 13,3 15,3 12,4 13,5 15,5 14,6\n", output);
        Assert.Contains("weir_foreman-1 falls at 11,2\n", output);
    }

    /// <summary>
    /// 13.14's deciding play: Chat's seed 439 on <c>docs/samples/harrow_weir_brace.map</c>. Dunstan
    /// waits braced on 10,10 and the Foreman, let out by the veto, takes the one strike at 43; on
    /// turn 13 the Foreman sits braced on his hill and Dunstan kills him at 29.
    /// </summary>
    [Fact]
    public void ChatsColdBracePlayReplaysWithItsBraces()
    {
        var output = RunSample("harrow_weir_brace.map", "2026-09-27-harrow_weir_brace-439.script", 439, out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: defeat_boss\n", output);
        Assert.Contains("forecast weir_foreman-1 -> dunstan: dmg 8 hit 43% crit 2%; counter: none\n  brace: dunstan braced: weir_foreman-1 hit -15\n", output);
        Assert.Contains("forecast dunstan -> weir_foreman-1: dmg 8 hit 29% crit 0%; counter: dmg 8 hit 70% crit 2%\n  brace: weir_foreman-1 braced: dunstan hit -15\n", output);
        Assert.Contains("weir_foreman-1 falls at 13,6\n", output);
    }

    /// <summary>
    /// Issue 430's hand play: Code's seed 449 on <c>docs/samples/saltmarsh_ford_brace.map</c>, the
    /// 13.14 keep round. Ottilie steps into the braced leader's throw range as bait at 39 percent,
    /// his answer strips the brace, and Wren kills him on the fort on turn 13.
    /// </summary>
    [Fact]
    public void TheSaltmarshBracePlayReplaysWithItsBait()
    {
        var output = RunSample("saltmarsh_ford_brace.map", "2026-09-27-saltmarsh_ford_brace-449.script", 449, out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: rout\n", output);
        Assert.Contains("forecast ottilie -> bandit_leader-1: dmg 4 hit 39% crit 3%; counter: none\n  brace: bandit_leader-1 braced: ottilie hit -15\n", output);
        Assert.Contains("teodor falls at 10,1\n", output);
        Assert.Contains("bandit_leader-1 falls at 10,0\n", output);
    }

    /// <summary>
    /// Issue 486's hand play: Code's seed 563 on <c>docs/samples/saltmarsh_ford_brace_signatures.map</c>,
    /// the cadets' signature sample. Teodor waits so Ottilie and Wren strike under his orders, Wren
    /// wakes the fort from 10,5 and cantos back behind the braced line, Teodor strikes the leader
    /// from 9,0 where no ally is within 2 of him, and the captain kills under his orders on turn 10.
    /// </summary>
    [Fact]
    public void TheSignatureSamplePlayReplaysWithTheCadetsSignatures()
    {
        var output = RunSample("saltmarsh_ford_brace_signatures.map", "2026-09-30-saltmarsh_ford_brace_signatures-563.script", 563, out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: rout\n", output);
        Assert.Contains("forecast ottilie -> wingrider-1: dmg 19 hit 83% crit 3%; counter: none\n  signature: teodor's orders: ottilie hit +5\n", output);
        Assert.Contains("group fort wakes: proximity\n", output);
        Assert.Contains("wren cantos 10,5 -> 9,7 via 9,5 9,6\n", output);
        Assert.Contains("forecast teodor -> bandit_leader-1: dmg 8 hit 51% crit 0%; counter with Toll Axe: dmg 11 hit 58% crit 1%\n", output);
        Assert.Contains("bandit_leader-1 falls at 10,0\n", output);
    }

    /// <summary>
    /// Chat's cold play of issue 419's anvil arm: seed 433 on <c>docs/samples/sallow_grange_pincer.map</c>.
    /// The closed formation leaves no tile behind the front in reach, so the archer walks to 6,7
    /// and shoots instead of anviling, and every pin in the game is the player's. The captain
    /// kills the Reeve in the open and seizes on turn 7.
    /// </summary>
    [Fact]
    public void ChatsColdAnvilArmPlayReplaysWithNoEnemyPin()
    {
        var output = RunSample("sallow_grange_pincer.map", "2026-09-27-sallow_grange_pincer-433.script", 433, out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: seize\n", output);
        Assert.Contains("archer-2 moves 8,6 -> 6,7 via 7,6 7,7\n", output);
        Assert.Contains("  pincer: archer-2 pinned by ottilie: ansgar hit +15\n", output);
        Assert.Single(output.Split('\n'), line => line.StartsWith("  pincer: ", StringComparison.Ordinal));
        Assert.Contains("grange_reeve-1 falls at 13,6\n", output);
    }

    /// <summary>
    /// Issue 419's hand play: Code's seed 131 on <c>docs/samples/sallow_grange_pincer.map</c> with
    /// the planner's anvil arm built. The field group never had a tile behind the front in reach,
    /// so no enemy stepped in as an anvil and every pin was the player's: Ansgar on the brawler
    /// with Wren behind it, the captain's kill with Teodor behind it. The captain seizes on turn 7.
    /// </summary>
    [Fact]
    public void TheAnvilArmPlayReplaysWithThePlayersPinsOnly()
    {
        var output = RunSample("sallow_grange_pincer.map", "2026-09-27-sallow_grange_pincer-131.script", 131, out var exit);

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: seize\n", output);
        Assert.Contains("  pincer: brawler-1 pinned by wren: ansgar hit +15\n", output);
        Assert.Contains("  pincer: brawler-1 pinned by teodor: captain hit +15\n", output);
        Assert.Contains("grange_reeve-1 falls at 13,6\n", output);
        Assert.DoesNotContain(" falls at ", output.Replace("soldier-1 falls at", "").Replace("brawler-1 falls at", "").Replace("archer-2 falls at", "").Replace("grange_reeve-1 falls at", ""));
    }

    /// <summary>
    /// Issue 429's hand play: Code's seed 443 on <c>docs/samples/brackwater_cut_pincer.map</c>,
    /// the shipped Brackwater in daylight with the pincer on. On enemy phase 5 two anvils act
    /// ahead of id order (rider-1 beside Rook, brawler-1 beside Wren), rider-2 kills Wren pinned
    /// by the brawler, and the soldier strikes the captain pinned by the shieldbearer on the
    /// north edge. The captain falls on enemy phase 6; nobody escapes.
    /// </summary>
    [Fact]
    public void TheBrackwaterPincerPlayReplaysWithTheEnemysPins()
    {
        var output = RunSample("brackwater_cut_pincer.map", "2026-09-27-brackwater_cut_pincer-443.script", 443, out var exit);

        Assert.Equal(1, exit);
        Assert.DoesNotContain("rejected", output);
        Assert.Contains("enemy: move rider-1 13,0\nrider-1 moves 14,3 -> 13,0 via 15,3 15,2 15,1 15,0 14,0\n", output);
        Assert.Contains("  pincer: wren pinned by brawler-1: rider-2 hit +15\n", output);
        Assert.Contains("  pincer: captain pinned by shieldbearer-1: soldier-1 hit +15\n", output);
        Assert.Contains("escaped: none; left behind: none; fell: captain, dunstan, pell, rook, wren\n", output);
    }

    /// <summary>
    /// Issue 495's hand play: Code's seed 503 on <c>docs/samples/the_tollgate_pincer.map</c>, the
    /// keep round. The enemy's arm cannot fire there (the rider is alone in its group), so every
    /// pin is the player's: Teodor's on the woods archer with Wren behind it misses at 68, Wren's
    /// with Teodor behind it kills at 79, and Wren's on the brigand with the captain behind it
    /// kills. The captain seizes on turn 9 with two Recalls spent and nobody lost.
    /// </summary>
    [Fact]
    public void TheTollgatePincerPlayReplaysWithThePlayersPinsOnly()
    {
        var output = RunSample("the_tollgate_pincer.map", "2026-09-29-the_tollgate_pincer-503.script", 503, out var exit);

        Assert.Equal(0, exit);
        Assert.DoesNotContain("rejected", output);
        Assert.EndsWith("battle won: seize\n", output);
        Assert.Contains("  pincer: archer-2 pinned by wren: teodor hit +15\n", output);
        Assert.Contains("  pincer: archer-2 pinned by teodor: wren hit +15\n", output);
        Assert.Contains("  pincer: toll_brigand-1 pinned by captain: wren hit +15\n", output);
        Assert.DoesNotContain("pinned by toll_", output);
        Assert.DoesNotContain("pinned by archer", output);
        Assert.DoesNotContain("pinned by rider", output);
        Assert.DoesNotContain("pinned by bandit", output);
    }

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

    /// <summary>
    /// Issue 377: a unit exits without moving, from an exit it began its turn on. Chat's seed 241
    /// play of Brackwater at dusk staged four units outside the bank's radius and walked them
    /// out through 19,3 in one turn; on the sample that keeps the older rule it replays byte for
    /// byte, and on the shipped map the first move and exit in one turn is refused.
    /// </summary>
    [Fact]
    public void ChatsSeed241WalkOutReplaysOnlyUnderTheOlderExitRule()
    {
        var older = RunSample("brackwater_cut_exit_after_move.map", "2026-09-27-brackwater_cut-241.script", 241, out var olderExit);
        var shipped = RunShipped("brackwater_cut.map", "2026-09-27-brackwater_cut-241.script", 241, out var shippedExit);

        Assert.Equal(0, olderExit);
        Assert.Equal(File.ReadAllText(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "transcripts", "2026-09-27-brackwater_cut-241.txt")).ReplaceLineEndings("\n"), older);
        Assert.NotEqual(0, shippedExit);
        Assert.Contains("rook cannot exit: it moved this turn; a unit exits without moving, from an exit it began its turn on", shipped);
    }

    /// <summary>
    /// Issue 377: Code's replay of Chat's seed 241 staging under the new rule. The party stands on
    /// 19,3 to 19,6 through enemy phase 5 with the bank awake; the brawler kills Rook on 19,6,
    /// the hexer kills Dunstan on the gap, and on turn 6 Wren, Pell and the captain leave.
    /// </summary>
    [Fact]
    public void TheJournaledStandOnTheExitsPaysTheBankOnSeed241()
    {
        var output = RunShipped("brackwater_cut.map", "2026-09-27-brackwater_cut-241-stand.script", 241, out var exit);

        Assert.Equal(0, exit);
        Assert.Contains("rook falls at 19,6\n", output);
        Assert.EndsWith("escaped: wren, pell, captain; left behind: none; fell: dunstan, rook\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "transcripts", "2026-09-27-brackwater_cut-241-stand.txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Issue 382: Code's play of seed 241 on Brackwater Cut with the lamps. The captain's step
    /// onto 19,3 wakes the bank and lights its lamps, <c>threat</c> prices the brawler on 19,6,
    /// so nobody stands there; the lamps go out on turn 6 and the brawler, last seen walking to
    /// 19,8, takes Rook on 19,4 out of the dark.
    /// </summary>
    [Fact]
    public void TheJournaledLampsPlayPricesTheBankOnSeed241()
    {
        var output = RunShipped("brackwater_cut.map", "2026-09-27-brackwater_cut-241-lamps.script", 241, out var exit);

        Assert.Equal(0, exit);
        Assert.Contains("group bank wakes: proximity; its lamps are lit (shieldbearer-1 17,5, soldier-1 17,6, brawler-1 17,7)\n", output);
        Assert.Contains("  brawler-1 from 18,6 with Iron Gauntlets (slot 1): dmg 10 x2 hit 83% crit 0%;", output);
        Assert.Contains("brawler-1 moves 17,7 -> 19,8 via 18,7 19,7\n", output);
        Assert.Contains("rook falls at 19,4\n", output);
        Assert.EndsWith("escaped: wren, captain; left behind: pell; fell: dunstan, rook\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "transcripts", "2026-09-27-brackwater_cut-241-lamps.txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Code's Fun Gate re-rate of Brackwater Cut on the built file (DECISIONS/0078), seed 283.
    /// Pell's kill on the gap is noise that wakes the bank and lights its lamps; the bank marches
    /// west on the units the chase can see, so the exits empty. One Recall moves Pell off the
    /// brawler's path, and Wren, Rook and the captain leave on turn 6.
    /// </summary>
    /// <summary>
    /// Issue 403's acceptance on the Critic's cold board (issue 399, seed 509): Dunstan on 16,6 at
    /// sight 1 is told the soldier's <c>?</c> at 15,4 is three tiles off, nearest first, unnamed.
    /// </summary>
    [Fact]
    public void OnTheCriticsBoardThreatListsTheSoldiersTileThreeOff()
    {
        var output = RunShipped("brackwater_cut.map", "2026-09-27-brackwater_cut-509.script", 509, out _);

        Assert.Contains("> threat dunstan\nthreat on dunstan at 16,6 (Plain): no enemy in sight can strike it next phase\n  in the dark, unpriced: ? at 15,4 (3), ", output);
        Assert.Equal(File.ReadAllText(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "transcripts", "2026-09-27-brackwater_cut-509.txt")).ReplaceLineEndings("\n"), output);
    }

    [Fact]
    public void TheJournaledReRateEmptiesTheExitsOnSeed283()
    {
        var output = RunShipped("brackwater_cut.map", "2026-09-27-brackwater_cut-283.script", 283, out var exit);

        Assert.Equal(0, exit);
        Assert.Contains("group bank wakes: noise; its lamps are lit (shieldbearer-1 17,5, soldier-1 17,6, brawler-1 17,7)\n", output);
        Assert.Contains("shieldbearer-1 moves 17,5 -> 15,3 via 17,4 17,3 16,3\n", output);
        Assert.Contains("pell falls at 13,3\n", output);
        Assert.EndsWith("escaped: wren, rook, captain; left behind: pell; fell: dunstan\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "transcripts", "2026-09-27-brackwater_cut-283.txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Issue 385's pocket under issue 393: Code's seed 379 and Chat's seed 263 scripts wake the weir
    /// group with Pell on 9,6 and nobody on 10,6. Under 379 the Foreman walked onto 10,6 and struck
    /// Pell there; under 385 he waited on 12,6. Now every strike he has is refused and he is a guard
    /// boss on his post, so he holds his hill at 13,6. The scripts no longer replay past that phase.
    /// </summary>
    [Theory]
    [InlineData("2026-09-27-harrow_weir-379.script", 379)]
    [InlineData("2026-09-27-harrow_weir-263.script", 263)]
    public void TheWokenForemanHoldsHisHillOverTheTenSixPocket(string script, int seed)
    {
        var output = RunLoose(script, seed);

        var phase = output[output.IndexOf("-- enemy phase, turn 3 --", StringComparison.Ordinal)..output.IndexOf("-- enemy phase ends, turn 3 --", StringComparison.Ordinal)];
        Assert.Contains("group weir wakes: proximity\n", output);
        Assert.DoesNotContain("weir_foreman-1 moves", phase);
        Assert.Contains("enemy: wait weir_foreman-1\n", phase);
    }

    /// <summary>
    /// Issue 385's ranged-only opening (Code's seed 263 script) under issue 393: with Pell breaking
    /// the shieldbearer from 9,6, the Foreman keeps his hill on enemy phase 4, where under 389 he
    /// stepped onto the bridge head and swung. On enemy phase 5 a strike from 12,6 passes the veto,
    /// and he takes it. He never stands on 10,6.
    /// </summary>
    [Fact]
    public void TheForemanKeepsHisHillThenStrikesFromTheBridgeHeadOnSeed263()
    {
        var output = RunLoose("2026-09-27-harrow_weir-263-veto.script", 263);

        var fourth = output[output.IndexOf("-- enemy phase, turn 4 --", StringComparison.Ordinal)..output.IndexOf("-- enemy phase ends, turn 4 --", StringComparison.Ordinal)];
        Assert.DoesNotContain("weir_foreman-1 moves", fourth);
        Assert.Contains("weir_foreman-1 moves 13,6 -> 12,6\nenemy: attack weir_foreman-1 dunstan\n", output);
        Assert.DoesNotContain("weir_foreman-1 moves 12,6 -> 10,6", output);
        Assert.DoesNotContain("weir_foreman-1 moves 13,6 -> 10,6", output);
    }

    /// <summary>
    /// Issue 393 (1), a refused guard boss goes home: Chat's seed 293 script. On enemy phase 3 the
    /// Foreman's strike on Keziah from 12,6 passes the veto and he takes it; on enemy phase 4 every
    /// strike is refused, and he walks back to his post at 13,6 instead of running away from it.
    /// </summary>
    [Fact]
    public void ARefusedForemanGoesHomeToHisHillOnSeed293()
    {
        var output = RunLoose("2026-09-27-harrow_weir-293.script", 293);

        var third = output[output.IndexOf("-- enemy phase, turn 3 --", StringComparison.Ordinal)..output.IndexOf("-- enemy phase ends, turn 3 --", StringComparison.Ordinal)];
        var fourth = output[output.IndexOf("-- enemy phase, turn 4 --", StringComparison.Ordinal)..output.IndexOf("-- enemy phase ends, turn 4 --", StringComparison.Ordinal)];
        Assert.Contains("weir_foreman-1 moves 13,6 -> 12,6\nenemy: attack weir_foreman-1 keziah\n", third);
        Assert.Contains("weir_foreman-1 moves 12,6 -> 13,6\nenemy: wait weir_foreman-1\n", fourth);
    }

    /// <summary>
    /// Issue 393 (1) alone, on Chat's seed 401 line with the map's <c>wake_links:</c> header taken
    /// out: the captain wakes the weir on turn 7 with the Foreman at full HP, and on enemy phase 7
    /// he stays on his hill at 13,6. Under 389 he ran to the forest at 14,8 (the journaled transcript).
    /// </summary>
    [Fact]
    public void AFullHpForemanStaysOnHisHillOnSeed401WithoutTheLink()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ironwake-393-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var map = Path.Combine(dir, "harrow_weir.map");
        File.WriteAllText(map, File.ReadAllText(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "samples", "harrow_weir_0081.map")).Replace("wake_links: ford>weir\n", ""));
        string output;
        try
        {
            var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
            output = Run(out _, "play", map, "--seed", "401", "--script", Path.Combine(repo, "docs", "transcripts", "2026-09-27-harrow_weir-401.script"), "--content", Fixture.RealContentDirectory());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }

        var seventh = output[output.IndexOf("-- enemy phase, turn 7 --", StringComparison.Ordinal)..output.IndexOf("-- enemy phase ends, turn 7 --", StringComparison.Ordinal)];
        Assert.DoesNotContain("group weir wakes: called by ford", output);
        Assert.Contains("group weir wakes: proximity\n", output);
        Assert.DoesNotContain("weir_foreman-1 moves", seventh);
        Assert.Contains("enemy: wait weir_foreman-1\n", seventh);
    }

    /// <summary>
    /// Issue 393 (2), the ford wakes the weir: Chat's seed 401 script on the shipped map. Dunstan
    /// wakes the ford on turn 3, the ford calls the weir, and on enemy phase 3 the Foreman crosses
    /// to 9,6 while the party is split; on enemy phase 4, with every strike refused, he goes back
    /// toward his post. The journaled 401 transcript is history and no longer replays past turn 3.
    /// </summary>
    [Fact]
    public void TheFordCallsTheForemanIntoTheSplitPartyOnSeed401()
    {
        var output = RunLoose("2026-09-27-harrow_weir-401.script", 401);

        Assert.Contains("group ford wakes: proximity\ngroup weir wakes: called by ford\n", output);
        var third = output[output.IndexOf("-- enemy phase, turn 3 --", StringComparison.Ordinal)..output.IndexOf("-- enemy phase ends, turn 3 --", StringComparison.Ordinal)];
        Assert.Contains("weir_foreman-1 moves 13,6 -> 9,6 via 12,6 11,6 10,6\n", third);
        Assert.Contains("weir_foreman-1 moves 9,6 -> 12,6 via 10,6 11,6\n", output);
        Assert.DoesNotContain("-> 14,8", output);
    }

    /// <summary>
    /// Issue 393 on Code's seed 397 script: the ford calls the weir on turn 3, and the Foreman
    /// crosses onto 10,6, the old pocket, where the veto passes the tile while the party is split
    /// at the ford; with every strike refused on enemy phase 4 he walks home to 13,6.
    /// </summary>
    [Fact]
    public void TheCalledForemanStandsOnTheBridgeThenGoesHomeOnSeed397()
    {
        var output = RunLoose("2026-09-27-harrow_weir-397.script", 397);

        Assert.Contains("group weir wakes: called by ford\n", output);
        Assert.Contains("weir_foreman-1 moves 13,6 -> 10,6 via 12,6 11,6\nenemy: wait weir_foreman-1\n", output);
        Assert.Contains("weir_foreman-1 moves 10,6 -> 13,6 via 11,6 12,6\nenemy: wait weir_foreman-1\n", output);
    }

    /// <summary>
    /// Issue 393 on Code's seed 389 script: the Foreman, woken on turn 2, fights from the bridge head
    /// at 12,6 and his hill instead of running to the mountain at 15,0 as he did under 389.
    /// </summary>
    [Fact]
    public void TheForemanFightsFromTheBridgeHeadOnSeed389()
    {
        var output = RunLoose("2026-09-27-harrow_weir-389.script", 389);

        Assert.Contains("weir_foreman-1 moves 13,6 -> 12,6\nenemy: attack weir_foreman-1 keziah\n", output);
        Assert.DoesNotContain("-> 15,0", output);
    }

    /// <summary>
    /// Issue 379, the bridge case: Chat's seed 257 script, made before the change, replayed on
    /// the built file. Keziah on 10,6 breaks the shieldbearer on turn 3, and on that enemy phase
    /// the woken Foreman steps to 12,6 and strikes her at range 2 with the Toll Axe.
    /// </summary>
    [Fact]
    public void AWokenForemanStrikesTheBridgeFromTwelveSixOnSeed257()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var output = Run(out _, "play", Path.Combine(repo, "docs", "samples", "harrow_weir_0081.map"), "--seed", "257", "--script", Path.Combine(repo, "docs", "transcripts", "2026-09-27-harrow_weir-257.script"), "--content", Fixture.RealContentDirectory());

        var phase = output[output.IndexOf("-- enemy phase, turn 3 --", StringComparison.Ordinal)..output.IndexOf("-- enemy phase ends, turn 3 --", StringComparison.Ordinal)];
        Assert.Contains("weir_foreman-1 moves 13,6 -> 12,6\n", phase);
        Assert.Contains("forecast weir_foreman-1 -> keziah: dmg 12 hit 60% crit 1%; counter: none\n", phase);
        Assert.Contains("  weir_foreman-1 hits keziah for 12 (hp 2)\n", phase);
    }

    /// <summary>
    /// Issue 393: Code's hand play of seed 409 replays to its journaled transcript. The ford calls
    /// the Foreman across to 9,6 on turn 3, he hunts Teodor to 9,3 on enemy phase 4, walks home onto
    /// 10,6 when the gathered party refuses him on enemy phase 5, is broken to 10 there, reaches his
    /// hill on enemy phase 6, and falls to Pell on turn 9, with one Recall and nobody dead.
    /// </summary>
    [Fact]
    public void TheJournaledSeed409PlayReplaysWithTheCalledForeman()
    {
        var output = RunSample("harrow_weir_0081.map", "2026-09-27-harrow_weir-409.script", 409, out var exit);

        Assert.Equal(0, exit);
        Assert.Contains("group weir wakes: called by ford\n", output);
        Assert.Contains("weir_foreman-1 moves 9,6 -> 9,3 via 9,5 9,4\nenemy: attack weir_foreman-1 teodor\n", output);
        Assert.Contains("weir_foreman-1 moves 9,3 -> 10,6 via 9,4 9,5 9,6\n", output);
        Assert.Contains("weir_foreman-1 falls at 13,6\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "transcripts", "2026-09-27-harrow_weir-409.txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Seventy-ninth round: Code's hand play of seed 419 replays to its journaled transcript. Pell on
    /// 9,6 wakes the weir, his Gust breaks the shieldbearer on turn 5 and the noise wakes the ford,
    /// the Foreman comes off his hill at full HP to throw at Keziah on 10,6, is broken to 2 on 12,6,
    /// goes home to 13,6 and throws at her again, and falls to Pell on turn 7 after one Recall.
    /// </summary>
    [Fact]
    public void TheJournaledSeed419PlayReplaysWithTheFullHpThrowAndTheWalkHome()
    {
        var output = RunSample("harrow_weir_0081.map", "2026-09-27-harrow_weir-419.script", 419, out var exit);

        Assert.Equal(0, exit);
        Assert.Contains("group weir wakes: proximity\n", output);
        Assert.Contains("shieldbearer-1 falls at 11,6\ngroup ford wakes: noise\n", output);
        Assert.Contains("weir_foreman-1 moves 13,6 -> 12,6\nenemy: attack weir_foreman-1 keziah\n", output);
        Assert.Contains("weir_foreman-1 moves 12,6 -> 13,6\nenemy: attack weir_foreman-1 keziah\n", output);
        Assert.Contains("weir_foreman-1 falls at 13,6\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "transcripts", "2026-09-27-harrow_weir-419.txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Eightieth round: Chat's hand play of seed 421 replays to its journaled transcript. The ford wakes
    /// on turn 3 and calls the weir, the Foreman crosses to 9,6, goes home to 12,6 and 13,6 when refused,
    /// comes back out to 10,6 as a guard, takes the lone Teodor from 8,4 at full HP, and after one Recall
    /// is boxed at 9,2 and falls on Teodor's counter on enemy phase 9.
    /// </summary>
    [Fact]
    public void TheJournaledSeed421PlayReplaysWithThePatrolAndTheBox()
    {
        var output = RunSample("harrow_weir_0081.map", "2026-09-27-harrow_weir-421.script", 421, out var exit);

        Assert.Equal(0, exit);
        Assert.Contains("group ford wakes: proximity\ngroup weir wakes: called by ford\n", output);
        Assert.Contains("weir_foreman-1 moves 13,6 -> 9,6 via 12,6 11,6 10,6\n", output);
        Assert.Contains("weir_foreman-1 moves 9,6 -> 12,6 via 10,6 11,6\n", output);
        Assert.Contains("weir_foreman-1 moves 13,6 -> 10,6 via 12,6 11,6\n", output);
        Assert.Contains("weir_foreman-1 moves 10,6 -> 8,4 via 9,6 8,6 8,5\nenemy: attack weir_foreman-1 teodor\n", output);
        Assert.Contains("recalled to state 133; 2 charges left\n", output);
        Assert.Contains("weir_foreman-1 falls at 9,2\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "transcripts", "2026-09-27-harrow_weir-421.txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Issue 131, the timing arm: Code's play of Saltmarsh Ford with the ford group arriving behind
    /// the party (seed 511) replays to its transcript on the file it was played on, kept as
    /// <c>docs/samples/saltmarsh_ford_0090.map</c> once the shipped map took <c>brace: on</c>: the pair
    /// spawning on the command that stops Teodor on 10,4 and the rout won on turn 11.
    /// </summary>
    [Fact]
    public void TheTimingArmPlayReplaysOnTheFileItWasPlayedOnToItsTranscript()
    {
        var output = RunSample("saltmarsh_ford_0090.map", "2026-09-29-saltmarsh_ford-511.script", 511, out var exit);

        Assert.Equal(0, exit);
        Assert.Contains("  brigand-1 arrives at 0,9, group ford, aggressive\n", output);
        Assert.Contains("battle won: rout", output);
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        Assert.Equal(File.ReadAllText(Path.Combine(repo, "docs", "transcripts", "2026-09-29-saltmarsh_ford-511.txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Issue 131, the brace header: Code's play of Saltmarsh Ford with <c>brace: on</c> on top of
    /// the timing arm (seed 523, on the file kept as <c>docs/samples/saltmarsh_ford_0091.map</c>)
    /// replays to its transcript: the leader braced until the adjacent bait on turn 8, one Recall,
    /// and the rout won on turn 10.
    /// </summary>
    [Fact]
    public void TheBracePlayReplaysOnTheBracedSaltmarshToItsTranscript()
    {
        var output = RunSample("saltmarsh_ford_0091.map", "2026-09-29-saltmarsh_ford-523.script", 523, out var exit);

        Assert.Equal(0, exit);
        Assert.Contains("bandit_leader-1 waits and braces\n", output);
        Assert.Contains("battle won: rout", output);
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        Assert.Equal(File.ReadAllText(Path.Combine(repo, "docs", "transcripts", "2026-09-29-saltmarsh_ford-523.txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Issue 131, round 125: Chat's cold play of the braced Saltmarsh Ford (seed 541, on the file
    /// kept as <c>docs/samples/saltmarsh_ford_0091.map</c>) replays to its transcript: the pair
    /// called from the south bank onto a braced line, and the rout won on turn 12.
    /// </summary>
    [Fact]
    public void ChatsColdPlayReplaysOnTheBracedSaltmarshToItsTranscript()
    {
        var output = RunSample("saltmarsh_ford_0091.map", "2026-09-29-saltmarsh_ford-541.script", 541, out var exit);

        Assert.Equal(0, exit);
        Assert.Contains("battle won: rout", output);
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        Assert.Equal(File.ReadAllText(Path.Combine(repo, "docs", "transcripts", "2026-09-29-saltmarsh_ford-541.txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Issue 131, the north cut (DECISIONS/0093): Code's play of the shipped Saltmarsh Ford
    /// (seed 547) replays to its transcript: the south bank cleared by turn 5 with nothing
    /// called, the pair arriving on Wren's step to 10,1 on turn 7, and the rout won on turn 11.
    /// </summary>
    [Fact]
    public void TheNorthCutPlayReplaysOnTheShippedSaltmarshToItsTranscript()
    {
        var output = RunShipped("saltmarsh_ford.map", "2026-09-30-saltmarsh_ford-547.script", 547, out var exit);

        Assert.Equal(0, exit);
        Assert.Contains("wren moves 10,4 -> 10,1 via 10,3 10,2\nevent ford\n", output);
        Assert.Contains("battle won: rout", output);
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        Assert.Equal(File.ReadAllText(Path.Combine(repo, "docs", "transcripts", "2026-09-30-saltmarsh_ford-547.txt")).ReplaceLineEndings("\n"), output);
    }

    /// <summary>
    /// Issue 131: Chat's cold re-rate of Saltmarsh Ford (seed 503) replays on the file it was played
    /// on, kept as <c>docs/samples/saltmarsh_ford_0030.map</c> once the ford group became a spawn on
    /// the shipped map: every line accepted and the rout won on turn 10.
    /// </summary>
    [Fact]
    public void TheReRateBeforeTheTimingArmReplaysOnTheFileItWasPlayedOn()
    {
        var output = RunSample("saltmarsh_ford_0030.map", "2026-09-29-saltmarsh_ford-503.script", 503, out var exit);

        Assert.Equal(0, exit);
        Assert.DoesNotContain("rejected", output);
        Assert.Contains("-- player phase, turn 10 --", output);
        Assert.Contains("battle won: rout", output);
    }

    /// <summary>
    /// Issue 471: the two re-rates of the retuned Harrow Weir (Code's seed 463, Chat's seed 473) replay
    /// on the file they were played on, kept as <c>docs/samples/harrow_weir_0087.map</c> once the weir
    /// crest became a crossing on the shipped map: every line accepted and the Foreman dead on turn 6.
    /// Seed 463's transcript predates issue 458's wake lines, so the game is checked, not the bytes.
    /// </summary>
    [Theory]
    [InlineData(463)]
    [InlineData(473)]
    public void TheRetuneReRatesReplayOnTheFileBeforeTheCrest(int seed)
    {
        var output = RunSample("harrow_weir_0087.map", $"2026-09-29-harrow_weir-{seed}.script", seed, out var exit);

        Assert.Equal(0, exit);
        Assert.DoesNotContain("rejected", output);
        Assert.Contains("-- player phase, turn 6 --", output);
        Assert.DoesNotContain("-- player phase, turn 7 --", output);
        Assert.Contains("weir_foreman-1 falls at", output);
        Assert.Contains("battle won: defeat_boss", output);
    }

    /// <summary>
    /// Issue 456's first check: the Critic's seed 601 line (captain on 7,0, Gust from 9,6 on turn 4)
    /// on the retuned file, its <c>shieldbearer-1</c> read as the weir-only bulwark. At res 3 the Gust
    /// reads 9 x2 and leaves the bulwark on 6, and the second hit comes from 10,6: on enemy phase 4
    /// the Foreman throws at Teodor there and the bulwark falls on Teodor's counter.
    /// </summary>
    [Fact]
    public void OnTheRetunedWeirTheCriticsGustFromNineSixLeavesTheBulwarkStanding()
    {
        var output = RunRetuned("2026-09-28-harrow_weir-601.script", 601, dropTail: 0);

        Assert.Contains("event north1 is blocked: its tile is held\n", output);
        Assert.Contains("forecast pell -> weir_shieldbearer-1 with Gust: dmg 9 x2 hit 100% crit 8%; counter: none\n", output);
        Assert.Contains("  pell hits weir_shieldbearer-1 for 9 (hp 6)\n", output);
        var fourth = output[output.IndexOf("-- enemy phase, turn 4 --", StringComparison.Ordinal)..output.IndexOf("-- player phase, turn 5 --", StringComparison.Ordinal)];
        Assert.Contains("enemy: attack weir_foreman-1 teodor\n", fourth);
        Assert.Contains("  teodor hits weir_shieldbearer-1 for 6 (hp 0)\n", fourth);
    }

    /// <summary>
    /// Issue 456, one spawn per door: on the seed 601 line with the last turn's two strikes left out,
    /// the captain on 7,0 spends only the rider, and on enemy phase 5 the turn-5 brigand arrives at
    /// 9,11 on the south edge beside the west wave.
    /// </summary>
    [Fact]
    public void OnTheRetunedWeirTheCaptainOnSevenZeroSpendsOnlyTheRider()
    {
        var output = RunRetuned("2026-09-28-harrow_weir-601.script", 601, dropTail: 2);

        Assert.Contains("event north1 is blocked: its tile is held\n", output);
        var fifth = output[output.IndexOf("-- enemy phase, turn 5 --", StringComparison.Ordinal)..];
        Assert.Contains("arrives at 0,11, group west, aggressive\n", fifth);
        Assert.Contains("arrives at 9,11, group south, aggressive\n", fifth);
        Assert.DoesNotContain("is blocked", fifth);
    }

    /// <summary>
    /// A Critic script from before issue 456 played loose on the shipped, retuned Harrow Weir, its
    /// <c>shieldbearer-1</c> renamed to the weir-only bulwark, its last <paramref name="dropTail"/>
    /// lines left out and the turn ended.
    /// </summary>
    private static string RunRetuned(string script, int seed, int dropTail)
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var lines = File.ReadAllLines(Path.Combine(repo, "docs", "transcripts", script)).Where(l => l.Length > 0).ToList();
        var kept = lines.Take(lines.Count - dropTail).Select(l => l.Replace("shieldbearer-1", "weir_shieldbearer-1", StringComparison.Ordinal));
        var path = Path.Combine(Path.GetTempPath(), "ironwake-456-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, string.Join("\n", kept) + (dropTail > 0 ? "\nend\n" : "\n"));
        try
        {
            return Run(out _, "play", "harrow_weir", "--seed", seed.ToString(System.Globalization.CultureInfo.InvariantCulture), "--script", path, "--content", Fixture.RealContentDirectory());
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// A transcript script played without <c>--strict</c>, so a line the rules have moved past still
    /// plays on, on Harrow Weir as issue 456 found it: <c>docs/samples/harrow_weir_0081.map</c>, the
    /// file every Harrow Weir play before the retune was made on.
    /// </summary>
    private static string RunLoose(string script, int seed)
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        return Run(out _, "play", Path.Combine(repo, "docs", "samples", "harrow_weir_0081.map"), "--seed", seed.ToString(System.Globalization.CultureInfo.InvariantCulture), "--script", Path.Combine(repo, "docs", "transcripts", script), "--content", Fixture.RealContentDirectory());
    }

    private static string RunShipped(string map, string script, int seed, out int exit)
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        return Run(
            out exit,
            "play",
            Path.Combine(Fixture.RealContentDirectory(), "maps", map),
            "--seed",
            seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--script",
            Path.Combine(repo, "docs", "transcripts", script),
            "--strict",
            "--content",
            Fixture.RealContentDirectory());
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
    /// its phase. An enter trigger on several tiles names each, joined by "or" (issue 370).
    /// </summary>
    [Fact]
    public void AnnouncedEventsOfEveryKindReadInPlayerWords()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var sample = File.ReadAllText(Path.Combine(repo, "docs", "samples", "sluice_gate.map")).ReplaceLineEndings("\n");
        var map = Path.Combine(Path.GetTempPath(), "ironwake-announce-" + Guid.NewGuid().ToString("N") + ".map");
        var script = Path.ChangeExtension(map, ".script");
        File.WriteAllText(map, sample.Replace("enemy_level: 1\n", "enemy_level: 1\nannounce: on\n") + "alarm turn 2 player flag alarm\nbell enter 2,1 3,2 flag bell\n");
        File.WriteAllText(script, "");
        try
        {
            var output = Run(out _, "play", map, "--seed", "7", "--script", script, "--content", Fixture.RealContentDirectory());

            Assert.Contains("  when one of yours stops on 3,1: 4,1 becomes road.\n", output);
            Assert.Contains("  turn 3, enemy phase: a brigand arrives at 9,0 (aggressive). A unit standing on 9,0 stops it.\n", output);
            Assert.Contains("  turn 2, player phase: alarm is set.\n", output);
            Assert.Contains("  when one of yours stops on 2,1 or 3,2: bell is set.\n", output);
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
            Assert.Contains("  turn 5, enemy phase: a brigand arrives at 9,11 (aggressive). A unit standing on 9,11 stops it.\n", start);
            Assert.DoesNotContain("event: ", start);
            var afterMap = weir[weir.IndexOf("> map", StringComparison.Ordinal)..];
            Assert.DoesNotContain("turn 3, enemy phase", afterMap);
            Assert.Contains("  turn 5, enemy phase: a brigand arrives at 0,11 ", afterMap);
            Assert.Contains("  turn 5, enemy phase: a brigand arrives at 9,11 ", afterMap);
            Assert.DoesNotContain(" enemy phase: a ", tollgate);
        }
        finally
        {
            File.Delete(path);
        }
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
    /// Issue 80: Code's play of seed 29 on Brackwater Cut in daylight (the sample since issue 318). The first pass holds the gap at 11,3
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

        var output = Run(out var exit, "play", BrackwaterDaylight, "--seed", "29", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

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
    /// Issue 269: Code's replay of Chat's seed 73 line on Brackwater Cut in daylight under escape by
    /// leaving. Rook leaves from 19,6 on turn 6 instead of standing on the exit at 6 HP,
    /// Dunstan still falls holding 12,3, and on turn 7 Wren, Pell and then the captain exit.
    /// </summary>
    [Fact]
    public void TheJournaledScriptLeavesBrackwaterCutOnSeedSeventyThree()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-26-brackwater_cut-73-exit.script");

        var output = Run(out var exit, "play", BrackwaterDaylight, "--seed", "73", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

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

    /// <summary>Issue 267: <c>show</c> prints the class's Mov and movement type, one unit of each type on Brackwater Cut in daylight.</summary>
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
            var output = Run(out _, "play", BrackwaterDaylight, "--seed", "73", "--script", script, "--content", Fixture.RealContentDirectory());

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
    public void FullLoadsASampleMapFromItsFilePath()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var path = Path.Combine(repo, "docs", "samples", "brackwater_cut_pincer.map");
        var exit = -1;
        var output = Capture(() => exit = Ironwake.Sim.Program.Full(path, 2));
        Assert.Contains("full: 1 maps from ", output);
        Assert.Contains("gate 1 ", output);
        Assert.DoesNotContain("no map", output);
        Assert.NotEqual(2, exit);
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
    /// Issue 438: on a <c>wildfire: on</c> map the trace shows the fire. Seed 1 on the Tollgate
    /// sample: Pell's hit lights 5,5 under the archer, the archer burns at the enemy phase
    /// start, the front steps at the next player phase start, and Pell burns on the tile it lit.
    /// The plain Tollgate on the same seed prints no fire line.
    /// </summary>
    [Fact]
    public void AWildfireTracePrintsBurnsIgnitionsAndTheFrontsStep()
    {
        var sample = Path.Combine(Path.GetDirectoryName(Fixture.RealContentDirectory())!, "docs", "samples", "the_tollgate_wildfire.map");
        var trace = Capture(() => Ironwake.Sim.Program.Trace(sample, 1));
        Assert.Contains("#   pell vs archer-2: hit 12; pell 16 hp, archer-2 5 hp\n#   5,5 ignites\n", trace);
        Assert.Contains("\nend\n#   archer-2 burns 3, 2 hp\n# enemy: ", trace);
        Assert.Contains("\n# turn 3\n#   fire: out 5,5 6,5; lit 6,4 7,5 5,6\n", trace);
        Assert.Contains("\n# turn 4\n#   pell burns 3, 1 hp\n#   fire: out 6,4 7,5 5,6\n", trace);

        var plain = Capture(() => Ironwake.Sim.Program.Trace("the_tollgate", 1));
        Assert.DoesNotContain(" burns ", plain);
        Assert.DoesNotContain(" ignites", plain);
        Assert.DoesNotContain("fire:", plain);
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
