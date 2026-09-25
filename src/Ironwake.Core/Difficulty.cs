namespace Ironwake.Core;

/// <summary>
/// A difficulty as data (DESIGN.md section 9, issue 76): a percent per stat applied to every
/// enemy after the map's level floor, an offset added to the map's <c>enemy_level</c>, and
/// an optional Recall charge count that replaces the map header's. Normal is the identity
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

    /// <summary>True when applying this difficulty changes nothing: every percent 100, no offset, the map's own charges.</summary>
    public bool IsIdentity => StatPercent == FullPercent && EnemyLevelOffset == 0 && RecallCharges is null;

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
