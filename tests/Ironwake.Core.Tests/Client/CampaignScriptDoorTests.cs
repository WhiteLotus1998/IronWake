using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The campaign-script writer's door options (issue 1100): <c>--quest</c> takes a named side map
/// wherever it is offered, <c>--until-certify</c> ends the script at the first camp where a named
/// promotion would be accepted, before any action there, and names the refusal when no camp reaches
/// it. And the hand plays the script opens with are written as the console reads them since issue
/// 1093: a bare <c>end</c> into a lethal gains its <c>!</c>.
/// </summary>
[Collection("console")]
public class CampaignScriptDoorTests
{
    private const ulong Seed = 22;
    private const string Difficulty = "recruit";
    private const int Variant = 66;

    private static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    private static CampaignScript.Result Write(string? quest = null, (string, string)? until = null) =>
        CampaignScript.Write(Content, Fixture.RealContentDirectory(), Seed, Ironwake.Sim.Program.HandPlays(Fixture.RealContentDirectory()), Difficulty, permadeath: false, Variant, quest, until);

    /// <summary>The written script up to and including the first map's <c>leave</c>.</summary>
    private static string FirstMap(string text)
    {
        var lines = text.Split('\n').ToList();
        return string.Join('\n', lines.Take(lines.IndexOf("leave") + 1)) + "\n";
    }

    private static string Console(string script)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ironwake-door-{Guid.NewGuid():N}.script");
        try
        {
            File.WriteAllText(path, script);
            return ConsoleCapture.Run(() => CampaignSession.Run(new[]
            {
                "--seed", Seed.ToString(), "--difficulty", Difficulty, "--permadeath", "off", "--script", path, "--strict",
                "--content", Fixture.RealContentDirectory(),
            }));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AHandPlaysEndIntoALethalIsWrittenWithTheBangTheConsoleAsksFor()
    {
        var first = FirstMap(Write().Text);

        Assert.Contains("\nend !\n", first);
        Assert.DoesNotContain("ERROR", Console(first));
    }

    [Fact]
    public void AHandPlaysBareEndIntoALethalIsRefusedByTheConsole()
    {
        var bare = FirstMap(Write().Text).Replace("\nend !\n", "\nend\n", StringComparison.Ordinal);

        Assert.Contains("ERROR: Lethal if all land", Console(bare));
    }

    [Fact]
    public void UntilCertifyStopsAtTheFirstCampTheDoorOpensBeforeAnyActionThere()
    {
        var plain = Write().Text;
        var stopped = Write(until: ("captain", "ranger")).Text;

        Assert.Contains("\ncertify captain ranger\n", plain);
        Assert.DoesNotContain("\ncertify captain ranger\n", stopped);
        Assert.Matches("# stopped at the camp before [a-z_]+: certify captain ranger would be accepted\n$", stopped);
        Assert.StartsWith(stopped[..stopped.LastIndexOf("# stopped", StringComparison.Ordinal)], plain);
    }

    [Fact]
    public void UntilCertifyNamesTheRefusalWhenNoCampReachesTheDoor()
    {
        var text = Write(until: ("rook", "drover")).Text;

        Assert.Matches("# never reached: certify rook drover: .*needs level 7, has \\d+; needs lance C, has [A-E]\n$", text);
    }

    [Fact]
    public void QuestIsTakenWhereverOfferedBesideTheFirstQuest()
    {
        var plain = Write().Text;
        var preferred = Write(quest: "pell_1").Text;

        Assert.DoesNotContain("\nquest pell_1 ", plain);
        Assert.Contains("\nquest pell_1 ", preferred);
    }
}
