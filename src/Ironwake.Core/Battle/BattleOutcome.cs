namespace Ironwake.Core;

public enum BattleResult
{
    Ongoing,
    Won,
    Lost,
}

/// <summary>Whether the battle is decided and why, in words a renderer can print (DESIGN.md section 7).</summary>
public sealed record BattleOutcome(BattleResult Result, string Reason)
{
    public static BattleOutcome Ongoing { get; } = new(BattleResult.Ongoing, "");

    public bool IsOver => Result != BattleResult.Ongoing;
}
