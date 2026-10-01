namespace Ironwake.Client;

/// <summary>What the side panel shows, one part per block, top to bottom.</summary>
public enum PanelPart
{
    /// <summary>The Recall browser, which takes the whole column.</summary>
    Recall,

    /// <summary>The live card: a hover forecast, the act on show, a move preview, or the idle hint.</summary>
    Forecast,

    /// <summary>The drawn unit card.</summary>
    UnitCard,

    /// <summary>The event log: its newest lines when closed, the whole log when open.</summary>
    Log,
}

/// <summary>
/// The side panel's order (issue 608). Tab opens the event log, and an open log takes what the
/// column has left, never the card being read: a forecast, the act on show or a move preview
/// keeps its place at the top. Only the unit card and the idle hint give way to the log.
/// </summary>
public static class PanelLayout
{
    /// <summary>The key strip's label for Tab.</summary>
    public const string TabLabel = "event log";

    /// <summary>
    /// The parts the column draws, in order. <paramref name="cardLive"/> is whether a card is
    /// showing (a hover forecast, the act on show, or a move preview) rather than the idle hint.
    /// </summary>
    public static IReadOnlyList<PanelPart> Parts(bool recallOpen, bool logOpen, bool cardLive)
    {
        if (recallOpen)
        {
            return new[] { PanelPart.Recall };
        }

        if (!logOpen)
        {
            return new[] { PanelPart.Forecast, PanelPart.UnitCard, PanelPart.Log };
        }

        return cardLive ? new[] { PanelPart.Forecast, PanelPart.Log } : new[] { PanelPart.Log };
    }
}
