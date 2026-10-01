using Ironwake.Client;

namespace Ironwake.Core.Tests.Client;

/// <summary>The side panel's order (issue 608): Tab opens the log without hiding the card being read.</summary>
public class ClientPanelLayoutTests
{
    /// <summary>Issue 629: with nothing selected, Tab took the idle FORECAST block away; it stays in every state.</summary>
    [Fact]
    public void TabLeavesTheForecastBlockVisible()
    {
        Assert.Equal(new[] { PanelPart.Forecast, PanelPart.Log }, PanelLayout.Parts(recallOpen: false, logOpen: true));
    }

    [Fact]
    public void AClosedLogSitsUnderTheForecastAndTheUnitCard()
    {
        Assert.Equal(new[] { PanelPart.Forecast, PanelPart.UnitCard, PanelPart.Log }, PanelLayout.Parts(recallOpen: false, logOpen: false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheRecallBrowserTakesTheWholeColumn(bool logOpen)
    {
        Assert.Equal(new[] { PanelPart.Recall }, PanelLayout.Parts(recallOpen: true, logOpen: logOpen));
    }

    [Fact]
    public void TheKeyStripSaysTabOpensTheEventLog()
    {
        Assert.Equal("event log", PanelLayout.TabLabel);
        Assert.Contains(("Tab", "event log"), KeyStrip.Keys);
    }

    /// <summary>Issue 609: Lotus asked what P and S do; each label names what its key does.</summary>
    [Theory]
    [InlineData("S", "game speed")]
    [InlineData("T", "enemy reach")]
    [InlineData("Esc", "menu")]
    public void EachKeyLabelSaysWhatTheKeyDoes(string key, string does)
    {
        Assert.Equal(does, KeyStrip.Keys.Single(k => k.Key == key).Does);
    }

    /// <summary>Issue 625: Lotus cut the priced threat, so no key in the strip is P.</summary>
    [Fact]
    public void TheKeyStripHasNoP()
    {
        Assert.DoesNotContain(KeyStrip.Keys, k => k.Key == "P");
    }
}
