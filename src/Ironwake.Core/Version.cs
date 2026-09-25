namespace Ironwake.Core;

/// <summary>
/// Identifies the rules engine build. Content files and replays record this so a
/// replay from an older ruleset can be rejected instead of silently diverging.
/// </summary>
public static class RulesVersion
{
    /// <summary>Incremented whenever a change alters how a command resolves.</summary>
    public const int Current = 1;
}

/// <summary>
/// Identifies the shapes of the presentation protocol (issue 25, docs/PROTOCOL.md): the
/// JSON a renderer reads for a state, an event, a query's answer, and a command. Consumers
/// ignore fields they do not know, so adding a field is not a change; removing one, or
/// changing what one means, increments this.
/// </summary>
public static class ProtocolVersion
{
    public const int Current = 1;
}
