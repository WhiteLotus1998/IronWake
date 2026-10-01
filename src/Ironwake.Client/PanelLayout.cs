namespace Ironwake.Client;

/// <summary>What the side panel shows, one part per block, top to bottom.</summary>
public enum PanelPart
{
    /// <summary>The Recall browser, which takes the whole column.</summary>
    Recall,

    /// <summary>The forecast block: a hover forecast, the act on show, a move preview, or the idle hint.</summary>
    Forecast,

    /// <summary>The drawn unit card.</summary>
    UnitCard,

    /// <summary>The event log: its newest lines when closed, the whole log when open.</summary>
    Log,
}

/// <summary>
/// The side panel's order (issues 608, 629). Tab opens the event log, and an open log takes what
/// the column has left, never the forecast block: a forecast, the act on show, a move preview or
/// the idle hint keeps its place at the top. Only the unit card gives way to the log.
/// </summary>
public static class PanelLayout
{
    /// <summary>The key strip's label for Tab.</summary>
    public const string TabLabel = "event log";

    /// <summary>The parts the column draws, in order.</summary>
    public static IReadOnlyList<PanelPart> Parts(bool recallOpen, bool logOpen)
    {
        if (recallOpen)
        {
            return new[] { PanelPart.Recall };
        }

        return logOpen
            ? new[] { PanelPart.Forecast, PanelPart.Log }
            : new[] { PanelPart.Forecast, PanelPart.UnitCard, PanelPart.Log };
    }
}
