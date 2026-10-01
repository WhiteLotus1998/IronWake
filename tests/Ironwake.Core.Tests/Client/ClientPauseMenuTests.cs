using Ironwake.Client;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// Lotus's third play (issue 629): a pause menu with a way back to the title, Escape backing out
/// of the innermost thing before it pauses, and the legend naming the sides Player and Enemy.
/// </summary>
public class ClientPauseMenuTests
{
    [Fact]
    public void ThePauseMenuOffersResumeHowToPlaySoundAndTheTitleInThatOrder()
    {
        Assert.Equal(
            new[] { PauseChoice.Resume, PauseChoice.HowToPlay, PauseChoice.Sound, PauseChoice.Title },
            Screens.PauseChoices.Select(c => c.Choice));
        Assert.Equal("Return to title", Screens.PauseChoices.Single(c => c.Choice == PauseChoice.Title).Label);
    }

    [Theory]
    [InlineData("Esc", PauseChoice.Resume)]
    [InlineData("H", PauseChoice.HowToPlay)]
    [InlineData("M", PauseChoice.Sound)]
    [InlineData("Q", PauseChoice.Title)]
    public void EachPauseChoiceHasItsKey(string key, PauseChoice choice)
    {
        Assert.Equal(choice, Screens.PauseKey(key));
    }

    [Fact]
    public void AKeyWithNoChoicePicksNothing()
    {
        Assert.Null(Screens.PauseKey("E"));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void EscapeClearsASelectionOrTheRecallBrowserBeforeItPauses(bool selected, bool recallOpen)
    {
        Assert.Equal(EscapeAction.Clear, Screens.Escape(attackMenuOpen: false, selected, recallOpen, campaign: false));
    }

    [Fact]
    public void EscapeClosesTheAttackMenuFirst()
    {
        Assert.Equal(EscapeAction.CloseAttackMenu, Screens.Escape(attackMenuOpen: true, selected: true, recallOpen: true, campaign: false));
    }

    [Fact]
    public void EscapeTakesBackASelectedUnitsMoveBeforeItClears()
    {
        Assert.Equal(EscapeAction.TakeBack, Screens.Escape(attackMenuOpen: false, selected: true, recallOpen: false, campaign: false, canTakeBack: true));
        Assert.Equal(EscapeAction.CloseAttackMenu, Screens.Escape(attackMenuOpen: true, selected: true, recallOpen: false, campaign: false, canTakeBack: true));
        Assert.Equal(EscapeAction.Clear, Screens.Escape(attackMenuOpen: false, selected: true, recallOpen: true, campaign: false, canTakeBack: true));
    }

    [Fact]
    public void EscapeWithNothingToBackOutOfPauses()
    {
        Assert.Equal(EscapeAction.Pause, Screens.Escape(attackMenuOpen: false, selected: false, recallOpen: false, campaign: false));
    }

    [Fact]
    public void EscapeNeverPausesTheCampaign()
    {
        Assert.Equal(EscapeAction.Clear, Screens.Escape(attackMenuOpen: false, selected: false, recallOpen: false, campaign: true));
    }

    [Fact]
    public void TheLegendNamesTheSidesPlayerAndEnemy()
    {
        Assert.Equal("Player", Legend.Player);
        Assert.Equal("Enemy", Legend.Enemy);
    }
}
