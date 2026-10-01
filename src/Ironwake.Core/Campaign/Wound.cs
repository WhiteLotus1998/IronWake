namespace Ironwake.Core;

/// <summary>
/// Wounded (issue 664, DESIGN section 9): with permadeath off, a unit that falls on a map the
/// party wins comes back with <see cref="Points"/> taken from its two highest stats for the next
/// <see cref="MainMaps"/> main maps, and loses the EXP toward its next level. HP is never one of
/// the two, so a wound is felt in the fight and not only in the bar. The penalty is taken from
/// the unit's own stats the moment it is inflicted, so every battle, forecast and card reads the
/// wounded numbers with no rule of its own, and it is given back whole when the wound expires.
/// Side maps never count down a wound, though a wounded unit fights on them wounded.
/// </summary>
/// <param name="Penalty">What was taken from each stat: <see cref="Points"/> from two of them, less where a stat had fewer to give.</param>
/// <param name="MapsLeft">Main maps the wound still lasts, 1 or 2; printed as <c>Wounded (n)</c>.</param>
public sealed record Wound(Stats Penalty, int MapsLeft)
{
    /// <summary>Taken from each of the two stats.</summary>
    public const int Points = 2;

    /// <summary>The main maps a fresh wound lasts.</summary>
    public const int MainMaps = 2;

    /// <summary>What a card prints: <c>Wounded (2)</c>.</summary>
    public string Label => $"Wounded ({MapsLeft})";

    /// <summary>
    /// <paramref name="unit"/> after a fall with permadeath off: any older wound given back first,
    /// then <see cref="Points"/> taken from the two highest stats other than HP as the unit's
    /// class reads them (a tie goes to the earlier stat in <see cref="Stats.All"/>), never below
    /// 0 in the unit's own stats, the EXP toward the next level set to 0, and a wound of
    /// <see cref="MainMaps"/> maps recorded. Its level, items and their counters are untouched.
    /// </summary>
    public static Unit Inflict(Unit unit, UnitClass unitClass)
    {
        var healed = Heal(unit);
        var effective = healed.EffectiveStats(unitClass);
        var chosen = Stats.All.Where(s => s != Stat.Hp)
            .Select((stat, order) => (stat, order))
            .OrderByDescending(p => effective.Get(p.stat)).ThenBy(p => p.order)
            .Take(2)
            .Select(p => p.stat)
            .ToHashSet();
        var penalty = Stats.Zero.Map((stat, _) => chosen.Contains(stat) ? Math.Min(Points, healed.Stats.Get(stat)) : 0);
        return healed with
        {
            Stats = healed.Stats - penalty,
            Exp = 0,
            Wound = new Wound(penalty, MainMaps),
        };
    }

    /// <summary><paramref name="unit"/> with its wound's penalty given back and the wound gone; unchanged when it has none.</summary>
    public static Unit Heal(Unit unit) =>
        unit.Wound is { } wound ? unit with { Stats = unit.Stats + wound.Penalty, Wound = null } : unit;

    /// <summary>
    /// <paramref name="unit"/> after one main map has passed (fought or not): a wound's count goes
    /// down by one, and at 0 the wound heals. Unchanged when it has none.
    /// </summary>
    public static Unit Tick(Unit unit) =>
        unit.Wound is not { } wound ? unit
        : wound.MapsLeft <= 1 ? Heal(unit)
        : unit with { Wound = wound with { MapsLeft = wound.MapsLeft - 1 } };
}
