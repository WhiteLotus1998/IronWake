namespace Ironwake.Core;

/// <summary>
/// The <c>seen_far:</c> header (issue 973, rounds 334 to 337; DECISIONS/0240): one unit a sleeping
/// Guard member hears <paramref name="Extra"/> tiles farther than the wake rule's radius while that
/// unit stands on the board as a player unit. Noise and death wakes are untouched, and an enemy-side
/// unit of the same id reads nothing. A campaign seats the unit when it is with the company and
/// refuses to bench it (<see cref="CampaignRecord.Bench"/>), so the sighting cannot be dodged by
/// leaving it home. <see cref="WakeCheck"/> is its one reader.
/// </summary>
public sealed record SeenFar(string UnitId, int Extra)
{
    /// <summary>The fewest tiles the header may add.</summary>
    public const int MinExtra = 1;

    /// <summary>The most tiles the header may add.</summary>
    public const int MaxExtra = 4;

    /// <summary>The tiles added to the wake radius around <paramref name="unit"/>: <see cref="Extra"/> for this unit on the player side, else 0.</summary>
    public int For(BattleUnit unit) => unit.Side == Side.Player && unit.Id == UnitId ? Extra : 0;

    /// <summary>The header's value as <see cref="MapDefinition"/> files write it: <c>rook 2</c>.</summary>
    public override string ToString() => $"{UnitId} {Extra}";

    /// <summary>
    /// The line under the wake legend while <paramref name="name"/> is seen far:
    /// <c>seen far: Rook wakes a sleeper within 6 (seen for miles)</c>.
    /// </summary>
    public string Line(string name, GameContent content) =>
        $"seen far: {name} wakes a sleeper within {content.WakeRadius + Extra} (seen for miles)";
}
