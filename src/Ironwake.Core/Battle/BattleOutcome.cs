namespace Ironwake.Core;

public enum BattleResult
{
    Ongoing,
    Won,
    Lost,
}

/// <summary>How a lost battle was lost, in DESIGN.md section 7's order; <see cref="None"/> while ongoing or won.</summary>
public enum LossCause
{
    None,
    Captain,
    Protected,
    Timeout,
}

/// <summary>
/// Whether the battle is decided and why, in words a renderer can print (DESIGN.md section 7),
/// with the loss's cause as data so the Sim can count losses by kind without reading the words (issue 114).
/// </summary>
public sealed record BattleOutcome(BattleResult Result, string Reason, LossCause Cause = LossCause.None)
{
    public static BattleOutcome Ongoing { get; } = new(BattleResult.Ongoing, "");

    public bool IsOver => Result != BattleResult.Ongoing;
}
