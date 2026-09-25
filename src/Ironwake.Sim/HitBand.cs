using System.Globalization;
using System.Text;
using Ironwake.Core;

namespace Ironwake.Sim;

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
    private readonly SortedDictionary<string, int[]>[] byTerrain = { new(StringComparer.Ordinal), new(StringComparer.Ordinal) };

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

        Count(unit.Side, forecast.Attacker, state.Map.TerrainIdAt(target.At));
        Count(target.Side, forecast.Defender, state.Map.TerrainIdAt(unit.At));
    }

    private void Count(Side side, SideForecast forecast, string defenderTerrain)
    {
        if (!forecast.Strikes)
        {
            return;
        }

        var index = side == Side.Player ? 0 : 1;
        var bucket = Math.Min(forecast.HitChance / 10, Buckets - 1);
        buckets[index, bucket]++;
        if (!byTerrain[index].TryGetValue(defenderTerrain, out var row))
        {
            row = new int[Buckets];
            byTerrain[index][defenderTerrain] = row;
        }

        row[bucket]++;
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

    /// <summary>
    /// The terrain column (Design Table, eighteenth round): one row per side and per terrain
    /// id the struck unit stood on, with the ten-wide buckets and the strike count, so
    /// "terrain visibly below" reads off the table. The attacker's strike is filed under the
    /// target's tile and the counter under the attacker's.
    /// </summary>
    public string TerrainLines(string prefix)
    {
        var text = new StringBuilder();
        for (var index = 0; index < 2; index++)
        {
            var side = index == 0 ? "player" : "enemy";
            foreach (var (terrain, row) in byTerrain[index])
            {
                text.Append(prefix).Append(' ').Append(side).Append(" into ").Append(terrain).Append(':');
                for (var bucket = 0; bucket < Buckets; bucket++)
                {
                    var label = bucket == Buckets - 1 ? "100" : $"{bucket * 10}-{bucket * 10 + 9}";
                    text.Append(' ').Append(label).Append(' ').Append(row[bucket]);
                }

                text.Append(", strikes ").Append(row.Sum()).Append('\n');
            }
        }

        return text.ToString().TrimEnd('\n');
    }
}

/// <summary>
/// The hit-band table of issue 158: the raw hit chance of every strike side in the
/// heuristic's games, both sides, as a histogram, beside the doubling rate. A cell of
/// the table is one arm (<see cref="FormulaArm"/>) under one roll scheme on one map.
/// </summary>
public static class HitBand
{
    /// <summary>One cell: gate 1 with the tally attached, gate 4 on that baseline, the two histogram rows, and the terrain rows.</summary>
    public static IReadOnlyList<string> Cell(GameContent loaded, MapDefinition map, string id, int seeds, FormulaArm arm, RollScheme scheme)
    {
        var content = FormulaArms.Content(loaded, arm);
        var hits = new HitTally();
        var prefix = $"hitband: {id}, {FormulaArms.Name(arm)}, {Gates.Name(scheme)}";
        var formula = FormulaArms.Formula(arm);
        var (gate1, baseline) = Gates.Gate1(content, map, id, seeds, scheme, hits, formula);
        var gate4 = Gates.Gate4(content, map, id, baseline, scheme, formula);
        return new[] { prefix, hits.Lines(prefix), hits.TerrainLines(prefix), gate1.Line, gate4.Line };
    }
}
