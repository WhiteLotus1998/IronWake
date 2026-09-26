using Ironwake.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Certification trials at the console (issue 73, DESIGN.md section 13.6): the trial's opening
/// lines, <c>--candidate</c> and its refusals, the result line, and the two journaled puzzles
/// under <c>content/trials/</c> (issue 252), replayed to their transcripts.
/// </summary>
[Collection("console")]
public class CertificationTrialCliTests
{
    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static string Sample(string name) => Path.Combine(Fixture.RealContentDirectory(), "trials", name + ".map");

    private static string Transcript(string name) => Path.Combine(Repo, "docs", "transcripts", name);

    [Fact]
    public void BothTrialMapsAreCanonical()
    {
        foreach (var name in new[] { "bulwark_trial", "outrider_trial" })
        {
            var text = File.ReadAllText(Sample(name)).ReplaceLineEndings("\n");
            Assert.Equal(text, MapFormat.Write(MapFormat.Parse(name, text, MapFixture.Content), MapFixture.Content));
        }
    }

    [Fact]
    public void ATrialNamesItsCandidateClassAndLoadout()
    {
        var output = Play(out _, "outrider_trial", "7", "reach captain\n");

        Assert.Contains("certification trial: captain plays as Outrider with iron_lance, iron_sword\nTrial of the Outrider  turn 1 of 1", output);
        Assert.EndsWith("battle ongoing at turn 1, player phase\n", output);
        Assert.DoesNotContain("certification: ", output);
    }

    [Fact]
    public void TheCandidateFlagFieldsThatCastUnitInTheTrial()
    {
        var output = Play(out _, "bulwark_trial", "11", "show wren\n", "--candidate", "wren");

        Assert.Contains("certification trial: wren plays as Bulwark with iron_lance\n", output);
        Assert.Contains("wren: Wren", output);
        Assert.Contains("Bulwark L1", output);
        Assert.DoesNotContain("Alder Fenn", output);
    }

    [Fact]
    public void TheCandidateFlagIsRefusedOnAnOrdinaryMapAndForAStranger()
    {
        var ordinary = Run(out var exitOrdinary, "play", "old_mill_road", "--candidate", "wren", "--content", Fixture.RealContentDirectory());
        var stranger = Run(out var exitStranger, "play", Sample("bulwark_trial"), "--candidate", "nobody", "--content", Fixture.RealContentDirectory());

        Assert.Equal(2, exitOrdinary);
        Assert.Contains("ERROR: --candidate is for a certification map, and 'Old Mill Road' has no certification header", ordinary);
        Assert.Equal(2, exitStranger);
        Assert.Contains("ERROR: --candidate names 'nobody', who is not in the cast", stranger);
    }

    [Fact]
    public void TheOutriderTrialIsWonFromTheForestInFrontWhoseCantoReachesTheThrone()
    {
        var script = Transcript("2026-09-26-outrider_trial-12.script");

        var output = Run(out var exit, "play", Sample("outrider_trial"), "--seed", "12", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, exit);
        Assert.Contains("captain may canto up to 3 movement", output);
        Assert.EndsWith("battle won: seize; no recall is left\nbattle won: seize\ncertification: captain earned Outrider\n", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    [Fact]
    public void TheOutriderTrialIsLostWhenTheStrikeLeavesTheGuardStanding()
    {
        var script = Transcript("2026-09-26-outrider_trial-13.script");

        var output = Run(out var exit, "play", Sample("outrider_trial"), "--seed", "13", "--script", script, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(1, exit);
        Assert.Contains("hexer-1 hp 3", output);
        Assert.EndsWith("battle lost: turn 1 passed\ncertification: Outrider not earned\n", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    [Fact]
    public void AFlankOfTheGuardLeavesTheCantoOneShortOfTheThrone()
    {
        var script = Transcript("2026-09-26-outrider_trial-13-flank.script");

        var output = Run(out var exit, "play", Sample("outrider_trial"), "--seed", "13", "--script", script, "--content", Fixture.RealContentDirectory());

        Assert.Equal(1, exit);
        Assert.Contains("captain cannot Canto to 3,0: not within the 1 movement its Canto has left from 2,2", output);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(script, ".txt")).ReplaceLineEndings("\n"), output);
    }

    [Fact]
    public void TheBulwarkTrialIsHeldInTheCorridorAndLostOnTheGamble()
    {
        var held = Transcript("2026-09-25-bulwark_trial-11.script");
        var gamble = Transcript("2026-09-25-bulwark_trial-12-gamble.script");

        var heldOutput = Run(out var heldExit, "play", Sample("bulwark_trial"), "--seed", "11", "--script", held, "--strict", "--content", Fixture.RealContentDirectory());
        var gambleOutput = Run(out var gambleExit, "play", Sample("bulwark_trial"), "--seed", "12", "--script", gamble, "--strict", "--content", Fixture.RealContentDirectory());

        Assert.Equal(0, heldExit);
        Assert.EndsWith("battle won: survive\ncertification: captain earned Bulwark\n", heldOutput);
        Assert.Contains("Trial of the Bulwark  over after turn 1 of 1  survive  recall 0\n", heldOutput);
        Assert.Contains("battle won: survive; no recall is left\n", heldOutput);
        Assert.DoesNotContain("turn 2 of 1", heldOutput);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(held, ".txt")).ReplaceLineEndings("\n"), heldOutput);
        Assert.Equal(1, gambleExit);
        Assert.EndsWith("battle lost: the captain is dead\ncertification: Bulwark not earned\n", gambleOutput);
        Assert.Equal(File.ReadAllText(Path.ChangeExtension(gamble, ".txt")).ReplaceLineEndings("\n"), gambleOutput);
    }

    private static string Play(out int exit, string map, string seed, string script, params string[] extra)
    {
        var path = Path.Combine(Path.GetTempPath(), "ironwake-trial-" + Guid.NewGuid().ToString("N") + ".script");
        File.WriteAllText(path, script);
        try
        {
            return Run(out exit, new[] { "play", Sample(map), "--seed", seed, "--script", path, "--content", Fixture.RealContentDirectory() }.Concat(extra).ToArray());
        }
        finally
        {
            File.Delete(path);
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
