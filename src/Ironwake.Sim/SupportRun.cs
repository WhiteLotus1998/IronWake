using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// The supports' climb (issue 77, slice 4): over <see cref="LevelRun"/>'s runs, the heuristic player
/// through the whole campaign with each won map's record carried into the next, the rapport the
/// record holds after each won map, read against the rivalry block's support tiers (C 16, B 40, A 72
/// since DECISIONS/0184). Only support pairs count. The heuristic stands nobody together on purpose,
/// so the read is the floor of the climb, not what a player who farms it sees. A measurement only;
/// nothing here changes what ships.
/// </summary>
public static class SupportRun
{
    /// <summary>
    /// The printed table: a header; per map, over the runs that won it, how many pairs stand at each
    /// tier or better and the points of the best pair, the best pair between two recruits and the
    /// best pair with the captain, each as p25 p50 p75; then per support pair in file order, over the
    /// runs that finished the campaign, its final points as p50 and p75 and in how many of those runs it
    /// reached each tier.
    /// </summary>
    public static IEnumerable<string> Lines(GameContent content, IReadOnlyList<LevelRun.Run> runs)
    {
        var tiers = content.Rivalry.SupportTiers;
        var pairs = content.Campaign.Supports;
        var captain = content.Cast.Count > 0 ? content.Cast[0].Id : string.Empty;
        var tierNames = string.Join(", ", tiers.Select(t => $"{t.Name} {t.At}"));
        yield return $"supports: {runs.Count} runs, the heuristic player through the campaign, {pairs.Count} support pairs; tiers {tierNames}; the heuristic never stands a pair together on purpose, so this is the floor of the climb";

        foreach (var number in runs.SelectMany(r => r.Maps.Select(m => m.Map)).Distinct().Order())
        {
            var after = runs.SelectMany(r => r.Maps.Select((m, i) => (m.Map, Points: i < r.Rapport.Count ? r.Rapport[i] : [])))
                .Where(x => x.Map == number)
                .Select(x => pairs.Select(p => PointsOf(x.Points, p)).ToList())
                .ToList();
            var id = content.Campaign.Maps[number - 1].MapId;
            var atTier = tiers.Select(t => $"at {t.Name} {LevelRun.Band(after.Select(ps => ps.Count(v => v >= t.At)).ToList())}");
            var best = LevelRun.Band(after.Select(ps => ps.DefaultIfEmpty(0).Max()).ToList());
            var recruits = LevelRun.Band(after.Select(ps => Best(ps, pairs, p => !p.Involves(captain))).ToList());
            var withCaptain = LevelRun.Band(after.Select(ps => Best(ps, pairs, p => p.Involves(captain))).ToList());
            yield return $"  map {number} {id}: won {after.Count}, {string.Join(", ", atTier)}; best pair {best}, best recruit pair {recruits}, best captain pair {withCaptain}";
        }

        var finished = runs.Where(r => r.LostOn is null && r.Rapport.Count > 0).Select(r => r.Rapport[^1]).ToList();
        yield return $"  per pair over the {finished.Count} runs that finished the campaign: final points p50 p75, and the runs reaching each tier";
        foreach (var pair in pairs)
        {
            var final = finished.Select(f => PointsOf(f, pair)).ToList();
            var reached = tiers.Select(t => $"{t.Name} {final.Count(v => v >= t.At)}");
            yield return $"    {pair.A} and {pair.B} ({pair.Kind.ToString().ToLowerInvariant()}): p50 {LevelRun.Percentile(final, 0.5)} p75 {LevelRun.Percentile(final, 0.75)}, {string.Join(", ", reached)}";
        }
    }

    /// <summary>The points <paramref name="pair"/> holds in <paramref name="rapport"/>, in either order, or 0.</summary>
    public static int PointsOf(IReadOnlyList<Rapport> rapport, SupportPair pair) =>
        rapport.FirstOrDefault(r => (r.A == pair.A && r.B == pair.B) || (r.A == pair.B && r.B == pair.A))?.Points ?? 0;

    private static int Best(IReadOnlyList<int> points, IReadOnlyList<SupportPair> pairs, Func<SupportPair, bool> which) =>
        points.Where((_, i) => which(pairs[i])).DefaultIfEmpty(0).Max();
}
