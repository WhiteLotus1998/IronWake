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
    public void PlayPrintsTheMissingSystemsLineFirst()
    {
        var output = Play(out var exit, "");

        Assert.StartsWith(PlaySession.MissingSystems + "\n", output);
        Assert.Contains("no EXP or level-ups (issue 8)", output);
        Assert.Contains("no items (issue 9)", output);
        Assert.EndsWith("battle ongoing at turn 1, player phase\n", output);
        Assert.Equal(1, exit);
        Assert.All(output, c => Assert.True(c < 128, "non-ASCII character in CLI output"));
    }

    [Fact]
    public void ItemRefusesOutLoudAndHelpListsItUnavailable()
    {
        var output = Play(out _, "item captain 0\nhelp\n");

        Assert.Contains("> item captain 0\nERROR: item is not in this build: items land with issue 9", output);
        Assert.Contains("unavailable in this build:\n  item <unit> <slot> [target]   items land with issue 9", output);
    }

    [Theory]
    [InlineData("move captain", "ERROR: usage: move <unit> <x,y>")]
    [InlineData("move captain 9", "ERROR: usage: move <unit> <x,y>")]
    [InlineData("attack captain", "ERROR: usage: attack <unit> <target>")]
    [InlineData("wait", "ERROR: usage: wait <unit>")]
    [InlineData("end now", "ERROR: usage: end")]
    [InlineData("recall x", "ERROR: usage: recall <n>")]
    [InlineData("forecast captain", "ERROR: usage: forecast <unit> <target>")]
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
    }

    [Fact]
    public void TheEnemyPhasePlaysOutFromThePlannerAndTheBrigandGoesToThreeSix()
    {
        var output = Play(out _, "# stand still\nend\n");

        Assert.Contains("> end\n-- player phase ends, turn 1 --\n-- enemy phase, turn 1 --\nenemy: wait archer-1\narcher-1 waits\nenemy: wait bandit_leader-1\nbandit_leader-1 waits\nenemy: move brigand-1 3,6\nbrigand-1 moves 6,5 -> 3,6 via 5,5 4,5 4,6\nenemy: wait brigand-1\nbrigand-1 waits\nenemy: wait soldier-1\nsoldier-1 waits\nenemy: end\n-- enemy phase ends, turn 1 --\n-- player phase, turn 2 --\nOld Mill Road  turn 2 of 20  player phase  rout  recall 3\n", output);
        Assert.Contains(" 6 ...c.....#..\n", output);
        Assert.Contains("group mill, guard, asleep", output);
    }

    [Fact]
    public void TheForecastPrintsBeforeAnAttackAndEventsRenderStrikeByStrike()
    {
        var output = Play(out _, "move captain 1,4\nmove wren 2,6\nend\nforecast wren brigand-1\nattack wren brigand-1\nshow wren\nrecall 0\n");

        Assert.Contains("captain moves 1,8 -> 1,4 via 1,7 1,6 1,5\n", output);
        Assert.Contains("> forecast wren brigand-1\nforecast wren -> brigand-1: dmg 10 x2 hit 100% crit 4%; counter: dmg 11 hit 90% crit 0%\n", output);
        Assert.Contains("> attack wren brigand-1\nforecast wren -> brigand-1:", output);
        Assert.Contains("wren attacks brigand-1\n  wren hits brigand-1 for 10", output);
        Assert.Contains("> show wren\nwren: wren, Cadet L1, at 2,6 on Plain\n  hp ", output);
        Assert.Contains("weapon: Iron Sword (mt 5 hit 90 crit 0 wt 5 range 1-1)\n", output);
        Assert.Contains("> recall 0\nrecalled to state 0; 2 charges left\n", output);
        Assert.Contains("battle ongoing at turn 1, player phase\n", output);
    }

    private static string Run(out int exit, params string[] args)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            exit = Ironwake.Cli.Program.Main(args);
        }
        finally
        {
            Console.SetOut(original);
        }

        return writer.ToString().Replace("\r\n", "\n");
    }
}
