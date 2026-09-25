namespace Ironwake.Core;

/// <summary>How hit rolls are read, DESIGN.md section 5. The switch inside <see cref="Combat.HitProbability"/>.</summary>
/// <summary>
/// Which burden and avoid formulas section 5 uses. <see cref="Standard"/> is what ships.
/// The others are the arms of issue 158, measured by the Sim's hit-band table and never
/// chosen by content or a map: the state carries the formula the way it carries the roll
/// scheme, so the forecast, the resolver, and the AI read one formula.
/// </summary>
public enum CombatFormula
{
    /// <summary>Burden is weight less a fifth of Str; speed counts once in avoid.</summary>
    Standard,

    /// <summary>Arm 1: burden is weight less the whole of Str; speed counts once in avoid.</summary>
    FullStrBurden,

    /// <summary>Arm 2: burden is weight less the whole of Str, and attack speed counts twice in avoid.</summary>
    FullStrBurdenSpeedTwice,
}

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
