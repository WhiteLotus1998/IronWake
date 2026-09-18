namespace Ironwake.Core;

/// <summary>
/// The rules engine's only source of chance: a roll from 0 to 99 derived from a key
/// (issue 31). There is no sequential draw and no stream position, so the same key gives
/// the same roll whatever was rolled before it, a Recall cannot launder a reroll by
/// reordering actions, and two runs that share a situation share its dice.
/// </summary>
public interface IRng
{
    /// <summary>The roll for <paramref name="key"/>, in 0..99.</summary>
    int Roll(RollKey key);
}
