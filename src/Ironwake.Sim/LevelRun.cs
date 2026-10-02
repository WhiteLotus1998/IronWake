using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// The second tier's timing (issue 704): the heuristic player fights the whole campaign from map 1,
/// each won map's record carried into the next as the campaign carries it, a map tried on fresh seeds
/// up to <see cref="HeirloomRun.Attempts"/> times until it is won, as <see cref="HeirloomRun"/> does,
/// with permadeath off: a player Recalls a death the heuristic cannot, so a unit that fell comes back
/// as it began the battle, wounded, and the run keeps its company.
/// Per map won it reads the company standing after it: how many are at or above
/// <see cref="Threshold"/>, the highest level, and the third highest (the issue asks for two or three
/// over the threshold by map 7). The heuristic never promotes, so the
/// levels are those of the first step; a lost map ends that run. A measurement only; nothing here
/// changes what ships.
/// </summary>
public static class LevelRun
{
    /// <summary>The level the second tier asks for (classes.json, every advanced form; 7 since rounds 224 to 226).</summary>
    public const int Threshold = 7;

    /// <summary>One run: per map won, the levels of the company standing after it and how many of it meet some advanced form's level and ranks (<see cref="Ready"/>), and the map no try won, or null.</summary>
    public sealed record Run(IReadOnlyList<(int Map, IReadOnlyList<int> Levels, int Ready)> Maps, int? LostOn);

    /// <summary>Every run over seeds 1..<paramref name="seeds"/>.</summary>
    public static IReadOnlyList<Run> Measure(string contentRoot, GameContent content, int seeds)
    {
        var runs = new List<Run>();
        for (var seed = 1; seed <= seeds; seed++)
        {
            var record = CampaignRecord.Start(content, (ulong)seed, permadeath: false);
            var maps = new List<(int, IReadOnlyList<int>, int)>();
            int? lost = null;
            while (!record.IsFinished(content))
            {
                var number = record.MapIndex + 1;
                var map = MapFiles.Load(MapFiles.CampaignPath(contentRoot, content, record.NextMap(content).MapId), content);
                BattleState? won = null;
                for (var attempt = 0; attempt < HeirloomRun.Attempts && won is null; attempt++)
                {
                    var tried = record with { Seed = unchecked(record.Seed + (ulong)attempt * 7919UL) };
                    var end = Fight(tried.Begin(map, content), content, seed, number);
                    if (end.Outcome.Result == BattleResult.Won)
                    {
                        won = end;
                    }
                }

                if (won is null)
                {
                    lost = number;
                    break;
                }

                record = record.AfterBattle(won, content);
                var company = record.Present(content);
                maps.Add((number, company.Select(u => u.Level).ToList(), company.Count(u => Ready(u, content))));
            }

            runs.Add(new Run(maps, lost));
        }

        return runs;
    }

    /// <summary>
    /// Whether <paramref name="unit"/> meets some advanced form's level, ranks and stats as if it stood in
    /// that form's base: the heuristic never certifies, so a cadet is still a Levy, and the class step it
    /// skipped is not what this measures.
    /// </summary>
    public static bool Ready(Unit unit, GameContent content) =>
        content.Classes.Values.Any(f => f.Advances is { } basis && Certifications.Check(unit with { ClassId = basis.Id }, f, null, CampaignRecord.IsCaptain(unit, content)).Count == 0);

    private static BattleState Fight(BattleState state, GameContent content, int seed, int number)
    {
        var player = new HeuristicPlayer();
        while (!state.Outcome.IsOver)
        {
            var commands = state.Phase == Side.Player ? player.Next(state, content) : EnemyAi.Plan(state, content);
            foreach (var command in commands)
            {
                var result = Resolver.Apply(state, content, command);
                if (!result.Accepted)
                {
                    throw new InvalidOperationException($"seed {seed} map {number}: {command} was rejected: {result.Rejection!.Message}");
                }

                state = result.Next;
                if (state.Outcome.IsOver)
                {
                    break;
                }
            }
        }

        return state;
    }

    /// <summary>The printed table: per map, over the runs that won it, the company at or above the threshold, the highest level, the third highest and the levels the whole company has gained, each as p25 p50 p75.</summary>
    public static IEnumerable<string> Lines(GameContent content, IReadOnlyList<Run> runs)
    {
        yield return $"levels: {runs.Count} runs, the heuristic player through the campaign, never promoting; at {Threshold}+ is the second tier's level";
        foreach (var number in runs.SelectMany(r => r.Maps.Select(m => m.Map)).Distinct().Order())
        {
            var won = runs.SelectMany(r => r.Maps.Where(m => m.Map == number)).ToList();
            var reached = won.Select(m => m.Levels).ToList();
            var ready = won.Select(m => m.Ready).ToList();
            var at = reached.Select(l => l.Count(v => v >= Threshold)).ToList();
            var top = reached.Select(l => l.Count == 0 ? 0 : l.Max()).ToList();
            var third = reached.Select(l => l.OrderDescending().Skip(2).FirstOrDefault()).ToList();
            var gained = reached.Select(l => l.Sum(v => v - Unit.MinLevel)).ToList();
            var id = content.Campaign.Maps[number - 1].MapId;
            yield return $"  map {number} {id}: won {reached.Count}, at {Threshold}+ {Band(at)}, meets a form's level and ranks {Band(ready)}, highest {Band(top)}, third {Band(third)}, levels gained by the company {Band(gained)}";
        }

        foreach (var lostOn in runs.Where(r => r.LostOn is not null).GroupBy(r => r.LostOn!.Value).OrderBy(g => g.Key))
        {
            yield return $"  no try won map {lostOn.Key}: {lostOn.Count()}";
        }
    }

    private static string Band(IReadOnlyList<int> values) =>
        $"p25 {Percentile(values, 0.25)} p50 {Percentile(values, 0.5)} p75 {Percentile(values, 0.75)}";

    private static int Percentile(IEnumerable<int> values, double p)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, (int)Math.Floor(p * sorted.Count))];
    }
}
