namespace Ironwake.Core;

/// <summary>
/// Identifies the rules engine build. Content files and replays record this so a
/// replay from an older ruleset can be rejected instead of silently diverging.
/// </summary>
public static class RulesVersion
{
    /// <summary>Incremented whenever a change alters how a command resolves.</summary>
    public const int Current = 0;
}
