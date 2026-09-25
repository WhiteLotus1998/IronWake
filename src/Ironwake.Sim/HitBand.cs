using System.Globalization;
using System.Text;
using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// The hit-band table of issue 158: the raw hit chance of every strike side in the
/// heuristic's games, both sides, as a histogram, beside the doubling rate. A cell of
/// the table is one arm under one roll scheme on one map. The arms that change content
/// (the iron tier's hit 15 lower) are applied here to the loaded content, so the content
/// files and the formulas on main are untouched by a measurement.
/// </summary>
public enum HitBandArm
{
    /// <summary>Main as it ships: burden against Str / 5, speed once in avoid, content as loaded.</summary>
    Main,

    /// <summary>Arm 1: burden against the whole of Str.</summary>
    FullStr,

    /// <summary>Arm 2: arm 1 with attack speed counted twice in avoid.</summary>
    FullStrSpeedTwice,

    /// <summary>Arm 3: content only, every iron-tier weapon's hit 15 lower.</summary>
    IronHit15,

    /// <summary>Arm 4: arm 2's formulas with arm 3's content.</summary>
    FullStrSpeedTwiceIronHit15,
}

/// <summary>
/// A tally of raw hit chances by side, read from the forecast of every attack command
/// before the resolver applies it (the same forecast the player and the AI read), so a
/// side that strikes counts once per combat it strikes in, and doubles are counted beside.
/// </summary>
public sealed class HitTally
{
    private const int Buckets = 11;
    private readonly int[,] buckets = new int[2, Buckets];
    private readonly int[] combats = new int[2];
    private readonly int[] doubles = new int[2];

    public void Record(BattleState state, GameContent content, Command command)
    {
        if (command is not Attack attack)
        {
            return;
        }

        var unit = state.Find(attack.UnitId);
        var target = state.Find(attack.TargetId);
        if (unit is null || target is null)
        {
            return;
        }

        var forecast = Queries.Forecast(state, content, unit, target, attack.Slot);
        if (forecast is null)
        {
            return;
        }

        Count(unit.Side, forecast.Attacker);
        Count(target.Side, forecast.Defender);
    }

    private void Count(Side side, SideForecast forecast)
    {
        if (!forecast.Strikes)
        {
            return;
        }

        var index = side == Side.Player ? 0 : 1;
        buckets[index, Math.Min(forecast.HitChance / 10, Buckets - 1)]++;
        combats[index]++;
        if (forecast.Doubles)
        {
            doubles[index]++;
        }
    }

    /// <summary>Both sides' rows: ten-wide raw-hit buckets, 100 on its own, the strike count, and the doubling rate.</summary>
    public string Lines(string prefix)
    {
        var text = new StringBuilder();
        for (var index = 0; index < 2; index++)
        {
            var side = index == 0 ? "player" : "enemy";
            text.Append(prefix).Append(' ').Append(side).Append(" raw hit:");
            for (var bucket = 0; bucket < Buckets; bucket++)
            {
                var label = bucket == Buckets - 1 ? "100" : $"{bucket * 10}-{bucket * 10 + 9}";
                text.Append(' ').Append(label).Append(' ').Append(buckets[index, bucket]);
            }

            var rate = combats[index] == 0 ? 0 : (double)doubles[index] / combats[index];
            text.Append(", strikes ").Append(combats[index]).Append(", doubles ").Append(rate.ToString("P0", CultureInfo.InvariantCulture)).Append('\n');
        }

        return text.ToString().TrimEnd('\n');
    }
}

public static class HitBand
{
    public const int IronHitCut = 15;

    /// <summary>The content an arm measures: main's as loaded, or the iron tier (every weapon whose id starts with <c>iron_</c>) with its hit cut by 15.</summary>
    public static GameContent Content(GameContent content, HitBandArm arm)
    {
        if (arm != HitBandArm.IronHit15 && arm != HitBandArm.FullStrSpeedTwiceIronHit15)
        {
            return content;
        }

        var weapons = content.Weapons.ToBuilder();
        foreach (var (id, weapon) in content.Weapons)
        {
            if (id.StartsWith("iron_", StringComparison.Ordinal))
            {
                weapons[id] = weapon with { Hit = weapon.Hit - IronHitCut };
            }
        }

        return content with { Weapons = weapons.ToImmutable() };
    }

    /// <summary>The section 5 formulas an arm fights under.</summary>
    public static CombatFormula Formula(HitBandArm arm) => arm switch
    {
        HitBandArm.Main or HitBandArm.IronHit15 => CombatFormula.Standard,
        HitBandArm.FullStr => CombatFormula.FullStrBurden,
        HitBandArm.FullStrSpeedTwice or HitBandArm.FullStrSpeedTwiceIronHit15 => CombatFormula.FullStrBurdenSpeedTwice,
        _ => throw new ArgumentOutOfRangeException(nameof(arm), arm, "unknown arm"),
    };

    public static string Name(HitBandArm arm) => arm switch
    {
        HitBandArm.Main => "main",
        HitBandArm.FullStr => "arm 1 (full Str)",
        HitBandArm.FullStrSpeedTwice => "arm 2 (full Str, speed twice)",
        HitBandArm.IronHit15 => "arm 3 (iron hit -15)",
        HitBandArm.FullStrSpeedTwiceIronHit15 => "arm 4 (arm 2 + iron hit -15)",
        _ => throw new ArgumentOutOfRangeException(nameof(arm), arm, "unknown arm"),
    };

    /// <summary>One cell: gate 1 with the tally attached, gate 4 on that baseline, and the two histogram rows.</summary>
    public static IReadOnlyList<string> Cell(GameContent loaded, MapDefinition map, string id, int seeds, HitBandArm arm, RollScheme scheme)
    {
        var content = Content(loaded, arm);
        var hits = new HitTally();
        var prefix = $"hitband: {id}, {Name(arm)}, {Gates.Name(scheme)}";
        var formula = Formula(arm);
        var (gate1, baseline) = Gates.Gate1(content, map, id, seeds, scheme, hits, formula);
        var gate4 = Gates.Gate4(content, map, id, baseline, scheme, formula);
        return new[] { prefix, hits.Lines(prefix), gate1.Line, gate4.Line };
    }
}
