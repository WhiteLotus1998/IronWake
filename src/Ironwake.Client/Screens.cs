using Ironwake.Core;

namespace Ironwake.Client;

/// <summary>
/// The words of the showcase's screens around the battle (issue 515): the title, the one
/// how-to-play screen, and the end card. Kept here rather than in the renderer so the text is
/// tested, and so any renderer says the same thing.
/// </summary>
public static class Screens
{
    /// <summary>The line under the title.</summary>
    public const string Tagline = "A small company, one road, and a rewind you will want to save.";

    /// <summary>The title screen's choices, in order, each with the key that picks it.</summary>
    public static readonly IReadOnlyList<(string Label, string Key)> TitleChoices = new[]
    {
        ("Play", "Enter"), ("How to play", "H"), ("Quit", "Esc"),
    };

    /// <summary>
    /// Recall as a stranger gets it on the first read (issue 515, Chat's question on slice 4): the
    /// screen was built around it. Chat's wording from round 163, taken word for word in round 164.
    /// </summary>
    public const string RecallSentence =
        "Recall rewinds the battle to any earlier moment in one of your turns. You get a few charges a map, and the dice remember: the same swing rolls the same.";

    /// <summary>The how-to-play screen: each section's heading and its lines, short enough to be read standing up.</summary>
    public static readonly IReadOnlyList<(string Heading, IReadOnlyList<string> Lines)> HowTo = new (string, IReadOnlyList<string>)[]
    {
        ("The goal", new[]
        {
            "The GOAL chip on the top bar says what wins the map. On the Tollgate it is simple: walk your captain onto the gate.",
            "If the captain falls, the battle is lost. Anyone else who falls stays fallen.",
        }),
        ("Your turn", new[]
        {
            "Click one of your units (the amber discs) to select it, then one of four things:",
            "Move: click a lit tile.",
            "Strike: click an enemy in reach.",
            "Wait: click the unit itself.",
            "End: press E once everyone has acted.",
            "Then the enemy moves. Space shows its next act, C skips to the end.",
        }),
        ("The forecast", new[]
        {
            "Point at an enemy before you strike. The card says what you will hit for and how often, and what comes back at you.",
            "The numbers are the real ones. If it says 64, it is 64.",
        }),
        ("Recall", new[]
        {
            RecallSentence,
            "Press R to open the list of moments and click one to go back.",
        }),
    };

    /// <summary>The how-to-play screen's last line: how to leave it.</summary>
    public const string HowToLeave = "Enter plays, Esc goes back.";
}

/// <summary>The three one-time turn-1 hints (issue 515), in the order they appear.</summary>
public enum Callout
{
    /// <summary>Select a unit.</summary>
    Select,

    /// <summary>Point at a tile or an enemy for the forecast.</summary>
    Forecast,

    /// <summary>End the phase.</summary>
    End,
}

/// <summary>
/// The turn-1 callouts (issue 515, 0092): one hint at a time, each dismissed by doing what it
/// says, never shown again once the first player phase is over. A renderer calls
/// <see cref="Observe"/> each frame with what the player is pointing at.
/// </summary>
public sealed class Callouts
{
    /// <summary>The hint on screen, or null once all three are done or turn 1's player phase has passed.</summary>
    public Callout? Showing { get; private set; } = Callout.Select;

    /// <summary>The words of a hint.</summary>
    public static string Text(Callout callout) => callout switch
    {
        Callout.Select => "Click one of your units, an amber disc, to select it.",
        Callout.Forecast => "Point at a tile before you move. The forecast above says who could strike you there, and on an enemy in reach, what your strike would do.",
        _ => "When your units have acted, press E to end the phase. Then they move.",
    };

    /// <summary>
    /// Moves the hints on from what the session shows: a selection dismisses the first; the
    /// second goes once <paramref name="hover"/> puts a forecast or a move preview on screen for
    /// a tile other than the selected unit's own, since on turn 1 of the Tollgate nothing is in
    /// reach yet and the preview is the forecast there is; the end of turn 1's player phase
    /// dismisses the third and any still waiting.
    /// </summary>
    public void Observe(ClientSession client, Coord? hover)
    {
        var state = client.State;
        if (state.Turn > 1 || state.Phase != Side.Player || client.EnemyPhasePlaying || state.Outcome.IsOver)
        {
            Showing = null;
            return;
        }

        if (Showing == Callout.Select && client.Selected is not null)
        {
            Showing = Callout.Forecast;
        }

        if (Showing == Callout.Forecast && hover is { } tile && client.Selected is { } id && state.Find(id)?.At != tile
            && (client.Hover(tile).Count > 0 || client.Stop(tile) is not null))
        {
            Showing = Callout.End;
        }
    }
}

/// <summary>
/// The end card (issue 515): won or lost, a line on what it cost or why, and the turn it ended
/// on. The reason for a loss is the console's own verdict, without its "lost because", which
/// the headline already says; a Seize map run out of turns says it without the coordinates
/// (issue 516, round 163), which stay in the log.
/// </summary>
public sealed record EndCard(bool Won, string Headline, string Line, string Turn)
{
    /// <summary>The card for a decided battle: a win names who fell, if anyone did; a loss says why.</summary>
    public static EndCard Of(BattleState state, GameContent content)
    {
        var won = state.Outcome.Result == BattleResult.Won;
        // A map lost on its clock is decided after its last turn: the card counts that turn, not the next.
        var turn = $"turn {Math.Min(state.Turn, state.Map.TurnLimit)} of {state.Map.TurnLimit}";
        if (!won && state.Map.Win == WinCondition.Seize && state.Outcome.Cause == LossCause.Timeout)
        {
            // The seize tile by the name the legend gives it, so the card and the legend agree.
            var tile = content.Terrain.TryGetValue(MapDefinition.ThroneTerrainId, out var throne) ? throne.Name.ToLowerInvariant() : MapDefinition.ThroneTerrainId;
            return new EndCard(false, "Lost", $"Turn {state.Map.TurnLimit} ran out with the captain short of the {tile}.", turn);
        }

        if (!won)
        {
            const string lead = "lost because ";
            var why = Objective.Verdict(state, content) ?? "lost";
            why = why.StartsWith(lead, StringComparison.Ordinal) ? why[lead.Length..] : why;
            return new EndCard(false, "Lost", char.ToUpperInvariant(why[0]) + why[1..] + ".", turn);
        }

        var start = state.History.Count > 0 ? state.History[0] : state;
        var fallen = start.UnitsOf(Side.Player)
            .Where(unit => state.Find(unit.Id) is null && !state.HasEscaped(unit.Id))
            .Select(unit => unit.Unit.Name)
            .ToList();
        var line = fallen.Count == 0
            ? "Nobody left behind."
            : $"Won, and it cost {Names(fallen)}.";
        return new EndCard(true, "Won", line, turn);
    }

    private static string Names(IReadOnlyList<string> names) => names.Count switch
    {
        1 => names[0],
        2 => $"{names[0]} and {names[1]}",
        _ => string.Join(", ", names.Take(names.Count - 1)) + ", and " + names[^1],
    };
}
