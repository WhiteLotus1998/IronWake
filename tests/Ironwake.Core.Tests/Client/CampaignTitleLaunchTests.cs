using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// Issue 786 slice 1: the exported build, launched with nothing that names a battle, opens the
/// campaign's title, and the single battle is one choice there whose title leads back to it.
/// </summary>
public class CampaignTitleLaunchTests
{
    [Fact]
    public void ABareLaunchOpensTheCampaignsTitle()
    {
        Assert.True(Screens.OpensCampaign(Array.Empty<string>()));
        Assert.True(Screens.OpensCampaign(new[] { "--seed", "7", "--content", "content", "--saves", "s" }));
    }

    [Theory]
    [InlineData("--map")]
    [InlineData("--script")]
    [InlineData("--screen")]
    [InlineData("--screenshot")]
    [InlineData("--strip")]
    [InlineData("--parity")]
    public void AnArgumentThatNamesABattleKeepsTheLaunchOffTheCampaign(string argument)
    {
        Assert.False(Screens.OpensCampaign(new[] { argument, "x" }));
    }

    [Fact]
    public void CampaignAsksForTheCampaignWhateverElseIsNamed()
    {
        Assert.True(Screens.OpensCampaign(new[] { "--campaign", "--screenshot", "out.png" }));
    }

    [Fact]
    public void OneBattleIsAChoiceOnTheCampaignsTitleWithItsOwnKey()
    {
        var title = Screens.CampaignTitle(hasSave: false);

        Assert.Contains(TitleChoice.OneBattle, title);
        Assert.Equal("One battle", Screens.Label(TitleChoice.OneBattle));
        Assert.Equal("B", Screens.Key(TitleChoice.OneBattle));
        Assert.Equal(TitleChoice.NewGame, Screens.DefaultTitleChoice(false));
    }

    [Fact]
    public void OneBattlesMapIsAShippedMap()
    {
        var path = Path.Combine(Fixture.RealContentDirectory(), "maps", Screens.OneBattleMap + ".map");

        Assert.True(File.Exists(path));
        MapFiles.Load(path, ContentLoader.Load(Fixture.RealContentDirectory()));
    }

    [Fact]
    public void TheSingleBattlesTitleFromTheCampaignEndsInBackNotQuit()
    {
        var fromCampaign = Screens.SingleBattleTitle(fromCampaign: true);
        var alone = Screens.SingleBattleTitle(fromCampaign: false);

        Assert.Equal(("Back", "Esc"), fromCampaign[^1]);
        Assert.Equal(("Quit", "Esc"), alone[^1]);
        Assert.Equal(alone.Take(alone.Count - 1), fromCampaign.Take(fromCampaign.Count - 1));
    }
}
