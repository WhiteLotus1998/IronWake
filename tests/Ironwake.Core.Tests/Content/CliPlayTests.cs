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
        Assert.Contains("> item captain 2\ncaptain uses field_dressing (0 left)\ncaptain heals 10 (hp 22)\n", output);
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
    [InlineData("forecast captain", "ERROR: usage: forecast <unit> <target> [slot] [from <x,y>]")]
    [InlineData("forecast captain brigand-1 from", "ERROR: usage: forecast <unit> <target> [slot] [from <x,y>]")]
    [InlineData("forecast captain brigand-1 from 1,4 2", "ERROR: usage: forecast <unit> <target> [slot] [from <x,y>]")]
    [InlineData("forecast captain brigand-1 at 1,4", "ERROR: usage: forecast <unit> <target> [slot] [from <x,y>]")]
    [InlineData("forecast captain brigand-1 from 9,9", "ERROR: captain cannot move to 9,9")]
    [InlineData("show", "ERROR: usage: show <unit>")]
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
        Assert.Contains("  forecast <unit> <target> [slot] [from <x,y>]  show the forecast", Play(out _, "help\n"));
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
        Assert.Contains("mill_bandit-1 falls at 6,1", output);
        Assert.DoesNotContain("uses field_dressing", output);
    }

    /// <summary>
    /// Issue 208: Code's play of seed 131 on the Tollgate with the woods archer at 5,5, inside
    /// the toll brigand's band. Pell's strike on the brigand is answered, the archer takes
    /// two turns of melee and falls to Pell from range 2, Teodor opens the door and dies to
    /// the keep archer, the boss throws at Wren rather than swing at the captain beside him,
    /// and the captain seizes on turn 9 with no Recall.
    /// </summary>
    [Fact]
    public void TheJournaledScriptWinsTheTollgateOnSeedOneThirtyOne()
    {
        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(repo, "docs", "transcripts", "2026-09-25-the_tollgate-131.script");

        var output = Run(out var exit, "play", "the_tollgate", "--seed", "131", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.EndsWith("battle won: seize\n", output);
        Assert.DoesNotContain("rejected ", output);
        Assert.Contains("forecast pell -> toll_brigand-1: dmg 13 hit 87% crit 3%; counter: dmg 13 hit 62% crit 0%", output);
        Assert.Contains("archer-2 falls at 5,5", output);
        Assert.Contains("teodor falls at 6,3", output);
        Assert.Contains("enemy: attack bandit_leader-1 wren", output);
        Assert.Contains("The Tollgate  turn 9 of 10", output);
        Assert.DoesNotContain("The Tollgate  turn 10 of 10", output);
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
        Assert.Contains("teodor moves 1,2 -> 3,1 via 2,2 3,2\nevent sluice\n  4,1 becomes road\n", output);
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
    /// The other branch of the same turn 2: wingrider-1 on 5 is left alive, falls back over
    /// the water to the fort at 2,2 that no player unit can strike, heals 3 at its next phase
    /// start, and comes straight back, since it never retreats twice.
    /// </summary>
    [Fact]
    public void TheRiverRefugeLetGoScriptRetreatsAcrossTheWaterAndComesBack()
    {
        var output = RunRiver("2026-09-25-river_refuge_retreat-3-letgo.script", out _);

        Assert.DoesNotContain("rejected ", output);
        Assert.Contains("enemy: retreat wingrider-1 2,2\nwingrider-1 falls back to 2,2 and will not fight this phase\n", output);
        Assert.Contains("wingrider-1 heals 3 (hp 8)\n", output);
        Assert.Contains("wingrider-1 moves 2,2 -> 2,7", output);
        Assert.Equal(1, CountOf(output, "falls back"));
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
            Assert.Contains("> item wren 2\nwren uses field_dressing", output);
            Assert.Contains($"turn {turn} of ", output);
            Assert.DoesNotContain($"turn {turn + 1} of ", output);
            Assert.EndsWith($"battle {outcome.Groups[1].Value.ToLowerInvariant()}: {outcome.Groups[3].Value}\n", output);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string Capture(Action run) =>
        ConsoleCapture.Run(run, Path.GetDirectoryName(Fixture.RealContentDirectory())!);
}
