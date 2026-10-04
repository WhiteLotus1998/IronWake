namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Issue 975: an <c>attack</c> whose forecast prints the counter-lethal line (issue 539) is refused
/// unless the line ends in <c>!</c>, and nothing is spent; a swing that is not counter-lethal runs
/// with or without the mark. A certain first-round kill never draws the line, so never asks
/// (<see cref="Battle.LethalCounterTests.ACertainFirstRoundKillDrawsNoLethalFlag"/>).
/// </summary>
[Collection("console")]
public sealed class LethalSwingConfirmTests
{
    private static string Yard(int enemyLevel) =>
        $"name: Yard\nsize: 6x3\nwin: rout\nturn_limit: 10\nrecall: 3\nenemy_level: {enemyLevel}\n\n"
        + "......\n......\n......\n\n"
        + "units:\nP captain 1,1\nE soldier 2,1 group:a behavior:hold\n";

    [Fact]
    public void ACounterLethalAttackIsRefusedAndSpendsNothing()
    {
        var output = RunInline(Yard(30), "attack captain soldier-1\nwait captain\n");

        Assert.Contains("add ! to swing anyway: attack captain soldier-1 !", output);
        Assert.DoesNotContain("attacks Soldier", output);
        Assert.DoesNotContain("rejected: captain has already acted", output);
    }

    [Fact]
    public void TheSameLineMarkedWithABangSwingsAnyway()
    {
        var output = RunInline(Yard(30), "attack captain soldier-1 !\n");

        Assert.Contains("Counter: lethal to", output);
        Assert.Contains("attacks Soldier", output);
        Assert.DoesNotContain("swing anyway", output);
    }

    [Theory]
    [InlineData("attack captain soldier-1\n")]
    [InlineData("attack captain soldier-1 !\n")]
    public void AnAttackThatIsNotCounterLethalRunsWithOrWithoutTheMark(string script)
    {
        var output = RunInline(Yard(1), script);

        Assert.DoesNotContain("Counter: lethal to", output);
        Assert.Contains("attacks Soldier", output);
        Assert.DoesNotContain("swing anyway", output);
    }

    [Fact]
    public void HelpPrintsTheRule()
    {
        Assert.Contains("a swing whose counter is lethal to the attacker is refused unless the line ends in !", RunInline(Yard(1), "help\n"));
    }

    private static string RunInline(string mapText, string scriptText)
    {
        var map = Path.Combine(Path.GetTempPath(), "ironwake-lethal-" + Guid.NewGuid().ToString("N") + ".map");
        var script = Path.ChangeExtension(map, ".script");
        File.WriteAllText(map, mapText);
        File.WriteAllText(script, scriptText);
        try
        {
            return ConsoleCapture.Run(() => Ironwake.Cli.Program.Main(["play", map, "--seed", "1", "--script", script, "--content", Fixture.RealContentDirectory()]));
        }
        finally
        {
            File.Delete(map);
            File.Delete(script);
        }
    }
}
