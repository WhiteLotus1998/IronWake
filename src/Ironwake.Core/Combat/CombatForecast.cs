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
/// <see cref="CritGrounds"/> marks a bow striking a flier (issue 723, <see cref="Weapon.GroundsAgainst"/>):
/// its crit deals plain damage and grounds the flier, so <see cref="CritDamage"/> is <see cref="Damage"/>.
/// <see cref="Bite"/> is what the side's drake adds once after the exchange when one of its strikes hit and
/// both units stand (issue 872, <see cref="BiteEffect"/>), 0 for every side without one; never a strike.
/// <see cref="NeverDoubles"/> marks a side in a single-strike class (<see cref="UnitClass.SingleStrike"/>), so the line can say why.
/// <see cref="Stoop"/> is what the side's first strike adds when it hits (issue 1127, <see cref="Ironwake.Core.Stoop"/>), 0 for every side that does not stoop.
/// <see cref="CashesMark"/> is a side whose first hit lands on a mark of its tome's school (issue 1329, <see cref="Mark"/>):
/// that hit deals <see cref="MarkedDamage"/> or <see cref="MarkedCritDamage"/>, every later one plain damage.
/// <see cref="Shell"/> is the Def a one-hit shell on the target adds to this side's first hit that lands (issue 1403,
/// <see cref="Armor"/>): that hit deals <see cref="FirstHit"/> on <see cref="Shelled"/>, every later one plain damage.
/// </summary>
public sealed record SideForecast(bool Strikes, int Damage, int HitChance, int DisplayedHit, int CritChance, bool Doubles, int StrikesPerRound = 1, bool CritGrounds = false, int Bite = 0, bool NeverDoubles = false, int Stoop = 0, bool CashesMark = false)
{
    /// <summary>
    /// The side's damage before its <see cref="Scale"/>: Atk less Def or Res. Null when nothing scales it, so it is <see cref="Damage"/>.
    /// </summary>
    public int? Unscaled { get; init; }

    /// <summary>
    /// The multiple on this side's final damage from the status and spell multipliers other than the mark (DECISIONS/0322):
    /// Lightning Rod's x0.5 on a caught spell and its charge's x1.25 (issue 1329, <see cref="LightningRod"/>). One by default.
    /// <see cref="Damage"/>, <see cref="CritDamage"/>, <see cref="MarkedDamage"/> and <see cref="MarkedCritDamage"/> each take
    /// every multiple on the unscaled damage, after the crit, rounded down once.
    /// </summary>
    public DamageScale Scale { get; init; } = DamageScale.One;

    private int Raw => Unscaled ?? Damage;

    /// <summary>The Def the target's shell adds to this side's first hit that lands (issue 1403), 0 when it wears none.</summary>
    public int Shell { get; init; }

    /// <summary>This side's damage before its <see cref="Scale"/> on the hit that meets the target's shell: Atk less Def plus <see cref="Shell"/> (Res for magic, which the shell does not touch). Null when the target wears no shell.</summary>
    public int? Shelled { get; init; }

    /// <summary>
    /// What this side's first hit that lands deals (issues 1329, 1403): on the shell's Def when the target wears one, times
    /// the mark's multiple when it cashes one, then its <see cref="Scale"/>, rounded down once; the crit triples the unscaled
    /// damage first unless it grounds. Its Stoop is not counted here.
    /// </summary>
    public int FirstHit(bool crit)
    {
        var raw = Shelled ?? Raw;
        var scale = CashesMark ? Scale.Times(Mark.Scale) : Scale;
        return scale.Of(crit && !CritGrounds ? raw * Combat.CritMultiplier : raw);
    }

    /// <summary>What the target's shell turns aside from this side's first plain hit (issue 1403): <see cref="Damage"/> less that hit on the shell, 0 when it wears none.</summary>
    public int ShellTurned => Shelled is { } shelled ? Damage - Scale.Of(shelled) : 0;

    /// <summary>What the first hit that lands deals beyond plain <see cref="Damage"/>: the mark's gain less what the shell turns aside, 0 for a side meeting neither.</summary>
    public int FirstHitBonus => CashesMark || Shelled is not null ? FirstHit(crit: false) - Damage : 0;

    /// <summary>What this side's first hit deals when it cashes a mark: its damage times the mark's multiple and its <see cref="Scale"/>, rounded down once.</summary>
    public int MarkedDamage => Scale.Times(Mark.Scale).Of(Raw);

    /// <summary>What this side's first hit deals when it crits and cashes a mark: its crit damage times the mark's multiple and its <see cref="Scale"/>, rounded down once.</summary>
    public int MarkedCritDamage => Scale.Times(Mark.Scale).Of(CritGrounds ? Raw : Raw * Combat.CritMultiplier);

    /// <summary>What the mark adds to this side's first hit, plain: <see cref="MarkedDamage"/> less <see cref="Damage"/> when it cashes one, else 0.</summary>
    public int MarkBonus => CashesMark ? MarkedDamage - Damage : 0;

    public static SideForecast None { get; } = new(false, 0, 0, 0, 0, false);

    /// <summary>What one crit from this side deals: its unscaled damage times <see cref="Combat.CritMultiplier"/> and its <see cref="Scale"/>, rounded down once, or plain <see cref="Damage"/> when the crit grounds instead (issue 723).</summary>
    public int CritDamage => CritGrounds ? Damage : Scale.Of(Raw * Combat.CritMultiplier);

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
    /// Whether the defender would have countered but has spent its answer this phase on a
    /// <c>one_answer: on</c> map (DESIGN.md 13.29, <see cref="Answer"/>), so the screen can say why there is no counter.
    /// </summary>
    public bool CounterAnswered { get; init; }

    /// <summary>
    /// The holder of a Lightning Rod that catches this attack (issue 1280, <see cref="LightningRod"/>), when one does:
    /// both sides' columns are then the strike on the holder and the holder's counter. Null otherwise.
    /// </summary>
    public string? CaughtBy { get; init; }

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
        Attacker.Rounds < 2 || !Defender.Strikes || Defender.Damage * Defender.StrikesPerRound - Defender.ShellTurned < attackerHp
            ? Attacker.StrikeCount
            : Attacker.StrikesPerRound;

    /// <summary>
    /// The attacker's plain damage over the strikes it lives to make (<see cref="AttackerStrikesLivedFor"/>), no crit,
    /// its first strike's Stoop (issue 1127), what a mark adds to its first hit (issue 1329) less what a shell turns aside (issue 1403), and its drake's bite (issue 872) when the counter, every strike landing, leaves it standing: the bite needs both
    /// units up after the exchange, and if the strikes alone kill, the bite adds nothing that matters.
    /// </summary>
    public int AttackerDamageLivedFor(int attackerHp) =>
        Attacker.Damage * AttackerStrikesLivedFor(attackerHp) + Attacker.Stoop + Attacker.FirstHitBonus
        + (Attacker.Bite > 0 && Defender.Damage * Defender.StrikeCount < attackerHp ? Attacker.Bite : 0);

    /// <summary>
    /// Whether the defender's counter kills the attacker if every counter strike lands (issue 539):
    /// its plain damage over all the strikes it can make reaches <paramref name="attackerHp"/>, no
    /// crit counted, the deterministic reading <c>threat</c>'s "if all land" uses. False when the
    /// defender does not counter, and when the attacker's first round is certain to kill it first
    /// (displayed hit 100, its plain damage over that round reaching <paramref name="defenderHp"/>).
    /// </summary>
    public bool CounterIsLethal(int attackerHp, int defenderHp) =>
        Defender.Strikes
        && CounterIfAllLand >= attackerHp
        && !(Attacker.Strikes && Attacker.DisplayedHit == 100 && Attacker.Damage * Attacker.StrikesPerRound - Attacker.ShellTurned >= defenderHp);

    /// <summary>
    /// The chance in 100 that every strike of the attacker's first round misses, when one plain strike of that
    /// round reaches <paramref name="defenderHp"/> (issue 991): the attacker strikes first, so then the defender
    /// counters only if the whole round misses. Read from the displayed hit, <c>100 - hit</c> for one strike and
    /// <c>(100 - hit)^2 / 100</c> for a gauntlet's two, rounded half away from zero and held inside 1 to 99 as
    /// <see cref="Combat.DisplayedHit"/> is (issue 452). Null when no single strike kills, when the attacker does
    /// not strike, and when its displayed hit is 0 or 100, where the miss is certain or impossible.
    /// </summary>
    public int? FirstRoundMissChance(int defenderHp)
    {
        if (!Attacker.Strikes || Attacker.FirstHit(crit: false) + Attacker.Stoop < defenderHp || Attacker.DisplayedHit is <= 0 or >= 100)
        {
            return null;
        }

        var miss = Math.Pow((100 - Attacker.DisplayedHit) / 100.0, Attacker.StrikesPerRound);
        return Math.Clamp((int)Math.Round(miss * 100, MidpointRounding.AwayFromZero), 1, 99);
    }

    /// <summary>
    /// The counter's plain damage if every strike it can make lands, no crit (issue 539), with what a mark adds to its first hit (issue 1329) less what a shell turns aside (issue 1403) and its drake's bite
    /// (issue 872): when the strikes alone fall short of the attacker's HP the attacker stands, so the bite lands.
    /// </summary>
    public int CounterIfAllLand => Defender.Strikes ? Defender.Damage * Defender.StrikeCount + Defender.FirstHitBonus + Defender.Bite : 0;
}

/// <summary>
/// A multiple on final damage as a fraction (DECISIONS/0322): status and spell multipliers multiply, and the product is applied
/// to final damage after Def or Res and after the crit, rounded down once.
/// </summary>
public readonly record struct DamageScale(int Numerator, int Denominator)
{
    /// <summary>No multiple.</summary>
    public static DamageScale One { get; } = new(1, 1);

    /// <summary>Whether this is no multiple at all.</summary>
    public bool IsOne => Numerator == Denominator;

    /// <summary>This multiple times <paramref name="other"/>.</summary>
    public DamageScale Times(DamageScale other) => new(Numerator * other.Numerator, Denominator * other.Denominator);

    /// <summary><paramref name="damage"/> times this multiple, rounded down once.</summary>
    public int Of(int damage) => damage * Numerator / Denominator;
}
