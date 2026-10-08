using Ironwake.Cli;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// Code's warm play of the Burned Shrine (Keziah's quest 1, issue 635) at the campaign's floor under
/// DECISIONS/0278 as amended by 0336 (seed 1390, the camp before map 8, the company at L5, Keziah with
/// Kinsbane fed 6, the 804 control's median by map 8, and Ottilie as the ally): Keziah takes all seven
/// kills, the scythe wakes on the archer on turn 8 and runs on, one Recall takes back Ottilie's turn-7 trade
/// with the hexer, and the hexer dies to a lethal-countered swing on turn 9.
/// </summary>
[Collection("console")]
public class BurnedShrineFloorReplayTests
{
    [Fact]
    public void TheBurnedShrineReplayOnSeed1390AtTheFloorIsRoutedOnTurn9WithKinsbaneWokenOnTheMap()
    {
        var root = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        var script = Path.Combine(root, "docs", "transcripts", "2026-10-08-the_burned_shrine-1390.script");
        var args = new[] { "campaign", "--seed", "1390", "--from", "brackwater_cut", "--pick", "keziah", "--fed", "6", "--level", "5", "--strict", "--script", script, "--content", Fixture.KeziahQuestOnlyContentDirectory() };

        var output = ConsoleCapture.Run(() => Program.Main(args));

        Assert.Contains("Keziah goes with Ottilie.", output);
        Assert.Contains("Kinsbane feeds: fed 12, power +5, a tooth grows (teeth 5/5); it wakes and hungers no more\n", output);
        Assert.Contains("Recalled to state 68; 1 charge left\n", output);
        Assert.Contains("Keziah wins keziah_1; the stores take 2 common material; nobody fell\n", output);
        Assert.Equal(File.ReadAllText(Path.Combine(root, "docs", "transcripts", "2026-10-08-the_burned_shrine-1390.txt")).ReplaceLineEndings("\n"), output);
    }
}
