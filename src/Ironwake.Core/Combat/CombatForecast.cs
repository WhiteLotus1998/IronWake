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
/// player sees, and the only one a renderer may print. <see cref="StrikesPerRound"/> is
/// the strikes each of this side's turns makes, two for gauntlets (issue 70).
/// </summary>
public sealed record SideForecast(bool Strikes, int Damage, int HitChance, int DisplayedHit, int CritChance, bool Doubles, int StrikesPerRound = 1)
{
    public static SideForecast None { get; } = new(false, 0, 0, 0, 0, false);

    /// <summary>The turns this side takes in the combat: two when it doubles, one when it strikes at all.</summary>
    public int Rounds => !Strikes ? 0 : Doubles ? 2 : 1;

    /// <summary>Every strike this side can make if nobody dies: its rounds times the strikes per round, four for a doubling gauntlet.</summary>
    public int StrikeCount => Rounds * StrikesPerRound;
}

/// <summary>
/// What both sides can expect from a combat before it is fought. <see cref="ArtCost"/> is
/// the extra uses a declared combat art spends (issue 68), zero for a plain attack.
/// </summary>
public sealed record CombatForecast(SideForecast Attacker, SideForecast Defender, RollScheme Scheme, int ArtCost = 0)
{
    /// <summary>
    /// The most uses the attacker's weapon spends: one per strike it can make, or one for
    /// the whole combat with a gauntlet (issue 70), and an art's cost, paid hit or miss.
    /// </summary>
    public int AttackerSpendsAtMost => (Attacker.StrikesPerRound > 1 ? 1 : Attacker.StrikeCount) + ArtCost;

    /// <summary>
    /// The strikes the attacker lives to make, read on plain damage with every hit
    /// landing: all of them, unless it doubles and the defender's first round, which falls
    /// between the attacker's two, reaches <paramref name="attackerHp"/>; then its first
    /// round only (issue 315). The deterministic reading of issue 147's survival, shared by
    /// section 8's kill flag and <c>threat</c>'s "if all land".
    /// </summary>
    public int AttackerStrikesLivedFor(int attackerHp) =>
        Attacker.Rounds < 2 || !Defender.Strikes || Defender.Damage * Defender.StrikesPerRound < attackerHp
            ? Attacker.StrikeCount
            : Attacker.StrikesPerRound;

    /// <summary>The attacker's plain damage over the strikes it lives to make (<see cref="AttackerStrikesLivedFor"/>), no crit.</summary>
    public int AttackerDamageLivedFor(int attackerHp) => Attacker.Damage * AttackerStrikesLivedFor(attackerHp);
}
