using Ironwake.Client;

namespace Ironwake.Core.Tests.Client;

/// <summary>The side panel's order (issue 608): Tab opens the log without hiding the card being read.</summary>
public class ClientPanelLayoutTests
{
    [Fact]
    public void TabLeavesTheForecastCardVisible()
    {
        var parts = PanelLayout.Parts(recallOpen: false, logOpen: true, cardLive: true);

        Assert.Equal(new[] { PanelPart.Forecast, PanelPart.Log }, parts);
    }

    [Fact]
    public void AnOpenLogTakesTheColumnWhenNoCardIsLive()
    {
        Assert.Equal(new[] { PanelPart.Log }, PanelLayout.Parts(recallOpen: false, logOpen: true, cardLive: false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AClosedLogSitsUnderTheCardAndTheUnitCard(bool cardLive)
    {
        Assert.Equal(new[] { PanelPart.Forecast, PanelPart.UnitCard, PanelPart.Log }, PanelLayout.Parts(recallOpen: false, logOpen: false, cardLive: cardLive));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void TheRecallBrowserTakesTheWholeColumn(bool logOpen, bool cardLive)
    {
        Assert.Equal(new[] { PanelPart.Recall }, PanelLayout.Parts(recallOpen: true, logOpen: logOpen, cardLive: cardLive));
    }

    [Fact]
    public void TheKeyStripSaysTabOpensTheEventLog()
    {
        Assert.Equal("event log", PanelLayout.TabLabel);
    }
}
