namespace Ironwake.Core;

/// <summary>How hit rolls are read, DESIGN.md section 5. The switch inside <see cref="Combat.HitProbability"/>.</summary>
public enum RollScheme
{
    /// <summary>Hit lands when the floor of the average of two rolls is below the hit chance. The section 5 default.</summary>
    TwoRollAverage,

    /// <summary>Hit lands when one roll is below the hit chance.</summary>
    OneRoll,
}

public static class RollSchemes
{
    /// <summary>The <c>--scheme</c> word: <c>one</c> is one roll, <c>two</c> the two-roll average; anything else is null.</summary>
    public static RollScheme? Parse(string text) => text switch
    {
        "one" => RollScheme.OneRoll,
        "two" => RollScheme.TwoRollAverage,
        _ => null,
    };
}

/// <summary>
/// One side of a forecast. <see cref="HitChance"/> is the raw section 5 number the
/// resolver rolls against; <see cref="DisplayedHit"/> is the resolved probability the
/// player sees, and the only one a renderer may print.
/// </summary>
public sealed record SideForecast(bool Strikes, int Damage, int HitChance, int DisplayedHit, int CritChance, bool Doubles)
{
    public static SideForecast None { get; } = new(false, 0, 0, 0, 0, false);
}

/// <summary>
/// What both sides can expect from a combat before it is fought. <see cref="ArtCost"/> is
/// the extra uses a declared combat art spends (issue 68), zero for a plain attack.
/// </summary>
public sealed record CombatForecast(SideForecast Attacker, SideForecast Defender, RollScheme Scheme, int ArtCost = 0)
{
    /// <summary>The most uses the attacker's weapon spends: one per strike it can make, and an art's cost, paid hit or miss.</summary>
    public int AttackerSpendsAtMost => (Attacker.Doubles ? 2 : 1) + ArtCost;
}
