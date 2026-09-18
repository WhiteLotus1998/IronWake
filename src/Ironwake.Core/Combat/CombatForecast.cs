namespace Ironwake.Core;

/// <summary>How hit rolls are read, DESIGN.md section 5. The switch inside <see cref="Combat.HitProbability"/>.</summary>
public enum RollScheme
{
    /// <summary>Hit lands when the floor of the average of two rolls is below the hit chance. The section 5 default.</summary>
    TwoRollAverage,

    /// <summary>Hit lands when one roll is below the hit chance.</summary>
    OneRoll,
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

/// <summary>What both sides can expect from a combat before it is fought.</summary>
public sealed record CombatForecast(SideForecast Attacker, SideForecast Defender, RollScheme Scheme);
