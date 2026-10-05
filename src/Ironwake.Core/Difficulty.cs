namespace Ironwake.Core;

/// <summary>
/// A difficulty as data (DESIGN.md section 9, issue 76): a percent per stat applied to every
/// enemy after the map's level floor, an offset added to the map's <c>enemy_level</c>, and
/// an optional Recall charge count that replaces the map header's or an offset added to it. Normal is the identity
/// entry of <c>rules.json</c> (every percent 100, offset 0, charges from the map), declared
/// there rather than special-cased here, so every difficulty takes the same path through
/// <see cref="MapDefinition.Under"/> and <see cref="MapDefinition.EnemyUnit"/>. Nothing
/// outside this record asks which difficulty is in play.
/// </summary>
/// <param name="Id">The entry's key in <c>rules.json</c>'s <c>difficulties</c> block.</param>
/// <param name="StatPercent">Per stat, the percent an enemy's stat is scaled to; 100 leaves it as it is.</param>
/// <param name="EnemyLevelOffset">Added to the map's enemy level floor, the sum held to the level range.</param>
/// <param name="RecallCharges">Recall charges on every map, or null to keep each map's own.</param>
public sealed record Difficulty(string Id, Stats StatPercent, int EnemyLevelOffset, int? RecallCharges)
{
    /// <summary>The id of the identity entry that <c>rules.json</c> must declare whenever it declares any.</summary>
    public const string NormalId = "normal";

    /// <summary>Every stat at 100 percent.</summary>
    public static Stats FullPercent { get; } = new(100, 100, 100, 100, 100, 100, 100, 100, 100);

    /// <summary>
    /// Recall charges added to each map's own (issue 664), negative for fewer, the sum held to
    /// 0..99; ignored when <see cref="RecallCharges"/> names a count.
    /// </summary>
    public int RecallOffset { get; init; }

    /// <summary>The name the screens print (issue 664: Normal is printed Captain), or null to print the id.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// The difficulty a campaign must be won on before this one may be chosen (issue 664: Tactician
    /// after a Captain campaign), or null when it is open from the start. The wins are the
    /// player's profile, kept outside any record.
    /// </summary>
    public string? UnlockedBy { get; init; }

    /// <summary>
    /// Where this difficulty stands on the ladder (issue 677): lower is easier, Captain (the identity)
    /// at 0 by default. A campaign's difficulty may be lowered at a camp to one with a lower tier,
    /// never raised; the tier changes no rule.
    /// </summary>
    public int Tier { get; init; }

    /// <summary>
    /// Whether this difficulty lets the end-turn lethal confirm ask (issue 1120, DECISIONS/0260):
    /// true by default, false on Tactician, where ending a phase with a unit lethal if all land is
    /// never refused whatever the player's setting. The lethal lines print either way.
    /// </summary>
    public bool LethalConfirm { get; init; } = true;

    /// <summary>
    /// Whether <c>end</c> is refused while a unit is lethal if all land (issue 1120): the player's
    /// <paramref name="setting"/>, and only where <see cref="LethalConfirm"/> allows it.
    /// </summary>
    public bool AsksOnLethal(bool setting) => setting && LethalConfirm;

    /// <summary>What the screens call this difficulty: its <see cref="Name"/>, else its id.</summary>
    public string DisplayName => Name ?? Id;

    /// <summary>
    /// True when applying this difficulty changes nothing: every percent 100, no offset, the map's
    /// own charges. The name and the unlock change no rule.
    /// </summary>
    public bool IsIdentity => StatPercent == FullPercent && EnemyLevelOffset == 0 && RecallCharges is null && RecallOffset == 0;

    /// <summary>Whether this difficulty may be chosen by a player whose won campaigns are <paramref name="won"/> (difficulty ids).</summary>
    public bool IsUnlocked(IEnumerable<string> won) => UnlockedBy is not { } needed || won.Contains(needed);

    /// <summary>
    /// An enemy's stats under this difficulty: each scaled to its percent and rounded to the
    /// nearest whole number, half up. HP never falls below 1, so a percent can weaken an
    /// enemy but never place a dead one.
    /// </summary>
    public Unit Apply(Unit enemy) =>
        enemy with { Stats = enemy.Stats.Map((stat, value) => stat == Stat.Hp ? Math.Max(1, Scale(value, StatPercent.Hp)) : Scale(value, StatPercent.Get(stat))) };

    /// <summary>A value scaled to a percent, rounded to the nearest whole number, half away from zero.</summary>
    public static int Scale(int value, int percent)
    {
        var scaled = (long)value * percent;
        return (int)(scaled >= 0 ? (scaled + 50) / 100 : -((-scaled + 50) / 100));
    }
}
