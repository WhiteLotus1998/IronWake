namespace Ironwake.Core;

/// <summary>The three rolls a strike may draw, DESIGN.md section 5.</summary>
public enum CombatRoll
{
    HitA,
    HitB,
    Crit,
}

/// <summary>
/// The key a roll is derived from (issue 31). Every roll site has one documented tuple,
/// DESIGN.md section 5; the tuples are the contract that replaced draw order. Two keys
/// are equal when their text is, and the text is what an <see cref="IRng"/> hashes.
/// </summary>
public sealed record RollKey
{
    private RollKey(string text)
    {
        Text = text;
    }

    /// <summary>The key's canonical text, one segment per tuple element.</summary>
    public string Text { get; }

    /// <summary>
    /// A combat roll: (turn, phase, attacker, target, strike index, roll). The counter's
    /// strike keys with the defender as the attacker, so each striker's rolls are its own.
    /// </summary>
    public static RollKey Combat(int turn, Side phase, string attackerId, string targetId, int strikeIndex, CombatRoll roll) =>
        new($"combat/{turn}/{phase}/{attackerId}/{targetId}/{strikeIndex}/{roll}");

    /// <summary>
    /// A growth roll at level-up: (unit, new level, stat) and nothing else, so a recruit's
    /// trajectory is fixed at the campaign seed whatever else happens (DESIGN.md section 3).
    /// </summary>
    public static RollKey Growth(string unitId, int newLevel, Stat stat) =>
        new($"growth/{unitId}/{newLevel}/{stat}");

    public override string ToString() => Text;
}
