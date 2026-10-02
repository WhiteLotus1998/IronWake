using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The way back to the campaign's title from a camp (issue 786): leaving rewrites the camp's
/// autosave with what was spent there, so Continue lands on the same camp, without pushing an older
/// camp out of the three kept; it is refused with a battle open, and a lost campaign saves nothing.
/// </summary>
[Collection("console")]
public class CampTitleReturnTests
{
    private static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    private static SaveStore Store() => new(Path.Combine(Path.GetTempPath(), "ironwake-camp-title-" + Guid.NewGuid().ToString("N")));

    private static void Clean(SaveStore store)
    {
        if (Directory.Exists(store.Directory))
        {
            Directory.Delete(store.Directory, recursive: true);
        }
    }

    [Fact]
    public void ACampLeftForTheTitleKeepsWhatWasSpentThere()
    {
        var store = Store();
        try
        {
            var campaign = new CampaignClient(Content, Fixture.RealContentDirectory(), CampActionsTests.Stocked(), saves: store);
            var arrived = campaign.Record;
            Assert.True(campaign.Buy(campaign.Stock[0], "captain"));
            Assert.NotEqual(arrived, campaign.Record);

            Assert.True(campaign.LeaveForTitle());

            Assert.Equal(SaveStore.AutoPrefix + "1", store.Newest());
            Assert.Equal(campaign.Record, store.Load(store.Newest()!, Content).Record);
        }
        finally
        {
            Clean(store);
        }
    }

    [Fact]
    public void LeavingForTheTitleRewritesTheCampsAutosaveRatherThanAddingOne()
    {
        var store = Store();
        try
        {
            var campaign = new CampaignClient(Content, Fixture.RealContentDirectory(), CampActionsTests.Stocked(), saves: store);

            Assert.True(campaign.LeaveForTitle());
            Assert.True(campaign.LeaveForTitle());

            Assert.Equal(new[] { SaveStore.AutoPrefix + "1" }, store.Names());
        }
        finally
        {
            Clean(store);
        }
    }

    [Fact]
    public void ACampaignCannotLeaveForTheTitleWithABattleOpen()
    {
        var store = Store();
        try
        {
            var campaign = new CampaignClient(Content, Fixture.RealContentDirectory(), CampaignRecord.Start(Content, 631), saves: store);
            Assert.True(campaign.March());

            Assert.False(campaign.LeaveForTitle());
            Assert.Contains("battle is open", campaign.Status);
        }
        finally
        {
            Clean(store);
        }
    }

    [Fact]
    public void ALostCampaignSavesNothingOnLeavingForTheTitle()
    {
        var store = Store();
        try
        {
            var campaign = new CampaignClient(Content, Fixture.RealContentDirectory(), CampaignRecord.Start(Content, 631), saves: store);
            Script.PlayCampaign(campaign, "march\n" + string.Concat(Enumerable.Repeat("end\n", 12)) + "leave\n");
            Assert.True(campaign.Over);
            Clean(store);

            Assert.True(campaign.LeaveForTitle());

            Assert.Null(store.Newest());
            Assert.Equal("Return to the title", campaign.TitleLine);
        }
        finally
        {
            Clean(store);
        }
    }

    [Fact]
    public void TheTitleRowSaysWhetherLeavingSavesTheCamp()
    {
        var store = Store();
        try
        {
            var saved = new CampaignClient(Content, Fixture.RealContentDirectory(), CampaignRecord.Start(Content, 631), saves: store);
            var unsaved = new CampaignClient(Content, Fixture.RealContentDirectory(), CampaignRecord.Start(Content, 631));

            Assert.Equal("Save the camp and return to the title", saved.TitleLine);
            Assert.Equal("Return to the title (this run keeps no saves)", unsaved.TitleLine);
            Assert.True(unsaved.LeaveForTitle());
        }
        finally
        {
            Clean(store);
        }
    }
}
