using Ironwake.Content;
using Ironwake.Content.Protocol;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The synthetic Drake Warden save (issue 1100, round 380's route (c)): run 3's real camp before
/// Brackwater Cut on the parity campaign 644, with Rook set to L7, lance C and a Grown drake and
/// <c>rook_1</c> recorded as won, so a cold chair stands at her second door. The save is a
/// committed fixture; these pin that it still loads and that both doors still open on it.
/// </summary>
public class DrakeWardenSaveTests
{
    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static string SaveDir => Path.Combine(Repo, "docs", "transcripts", "2026-10-05-drake_warden-644-synthetic.saves");

    private static GameContent Content => ContentLoader.Load(Fixture.RealContentDirectory());

    [Fact]
    public void TheSyntheticSaveStandsRookAtTheDoorWithAGrownDrake()
    {
        var content = Content;
        var record = new SaveStore(SaveDir).Load("brackwater", content).Record!;
        var rook = record.Roster.Single(u => u.Id == "rook");

        Assert.Equal("skyrider", rook.ClassId);
        Assert.Equal(7, rook.Level);
        Assert.Equal(DrakeStage.Grown, rook.Drake!.Stage);
        Assert.Contains("rook_1", record.WonQuestIds);
        Assert.Equal("brackwater_cut", content.Campaign.Maps[record.MapIndex].MapId);
    }

    [Theory]
    [InlineData("drover")]
    [InlineData("skycaptain")]
    public void BothOfRooksDoorsAcceptOnTheSyntheticSave(string classId)
    {
        var content = Content;
        var record = new SaveStore(SaveDir).Load("brackwater", content).Record!;

        var result = record.Certify("rook", classId, content);

        Assert.True(result.Accepted, result.Text);
    }
}
