using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// The heirloom's timing (issue 646): the heuristic player fights the campaign from its owner's
/// arrival map (the map before the ladder's floor when the owner arrives on none) through <see cref="LastMap"/>, the owner carrying the heirloom in front of their pack,
/// each won map's record carried into the next as the campaign carries it. A player keeps the
/// owner alive with Recall and restarts, which the heuristic never does, so a map is tried on
/// fresh seeds, up to <see cref="Attempts"/> times, until it is won with the owner standing. Per seed it reads the
/// combats fought with the heirloom by the end of each map and the map each stage turned on; a lost
/// map ends that run. With the quest arm (issue 635 slice 7, round 266) the run also plays the
/// quest that wakes a gated heirloom (<see cref="CampaignQuest.Wakes"/>) at the first camp that
/// offers it, its owner beside the first other standing recruit, retried like a map; a quest no try
/// wins is passed by and the run goes on without it. A measurement only; nothing here changes what ships.
/// </summary>
public static class HeirloomRun
{
    /// <summary>The campaign map, counted from 1, at which a run stops.</summary>
    public const int LastMap = 7;

    /// <summary>The tries a run gives one map before it counts the map lost.</summary>
    public const int Attempts = 50;

    /// <summary>One run: per map won, the combats by its end, whether the owner was deployed, and the tries it took; the map each stage turned on (0 when it never did); and the map no try won, or null.</summary>
    public sealed record Run(IReadOnlyList<(int Map, int Combats, bool Deployed, int Attempts)> Maps, IReadOnlyList<int> TurnedOn, int? LostOn)
    {
        /// <summary>The camp, counted as the map it follows, at which the quest arm won the waking quest; null when it never did or the arm is off.</summary>
        public int? QuestWonAfter { get; init; }

        /// <summary>The tries the quest arm spent on the waking quest; 0 when it was never offered.</summary>
        public int QuestTries { get; init; }
    }

    /// <summary>Every run over seeds 1..<paramref name="seeds"/>, for the heirloom <paramref name="itemId"/> issued to its owner.</summary>
    public static IReadOnlyList<Run> Measure(string contentRoot, GameContent content, string itemId, int seeds, bool quest = false)
    {
        var item = content.Weapon(itemId);
        if (item.Heirloom is not { } ladder || item.BoundTo is not { } owner)
        {
            throw new ArgumentException($"{itemId} is not an heirloom bound to a cast member");
        }

        var arrival = content.Campaign.ArrivalIndex(owner);
        var first = arrival > 0 ? arrival : Math.Max(0, ladder.FromMap - 2);
        var runs = new List<Run>();
        for (var seed = 1; seed <= seeds; seed++)
        {
            var record = CampaignRecord.StartAt(content, (ulong)seed, content.Campaign.Maps[first].MapId);
            record = record with { Roster = ValueList<Unit>.From(record.Present(content).Select(u => u.Id == owner ? Issued(u, item, content) : u)) };
            var maps = new List<(int, int, bool, int)>();
            var turned = new int[ladder.Turns.Count];
            int? lost = null;
            int? questWon = null;
            var questTries = 0;
            while (!record.IsFinished(content) && record.MapIndex + 1 <= LastMap)
            {
                record = SimPick.Made(record, content);
                var number = record.MapIndex + 1;
                var map = MapFiles.Load(MapFiles.CampaignPath(contentRoot, content, record.NextMap(content).MapId), content);
                BattleState? won = null;
                var events = new List<HeirloomTurned>();
                var attempt = 0;
                for (; attempt < Attempts && won is null; attempt++)
                {
                    var tried = record with { Seed = unchecked(record.Seed + (ulong)attempt * 7919UL) };
                    var (end, turns) = Fight(tried.Begin(map, content), content, seed, number);
                    if (end.Outcome.Result == BattleResult.Won && (!end.History[0].UnitsOf(Side.Player).Any(u => u.Id == owner) || end.Survivors().Any(u => u.Id == owner)))
                    {
                        won = end;
                        events = turns;
                    }
                }

                if (won is null)
                {
                    lost = number;
                    break;
                }

                foreach (var turn in events)
                {
                    turned[turn.Stage - 1] = number;
                }

                record = record.AfterBattle(won, content);
                var stack = record.Find(owner)!.Inventory.Items.FirstOrDefault(s => s.ItemId == itemId);
                maps.Add((number, stack.Combats, won.History[0].UnitsOf(Side.Player).Any(u => u.Id == owner), attempt));
                if (quest && questWon is null && questTries == 0 && content.Campaign.Quests.FirstOrDefault(q => q.Wakes == itemId) is { } waking
                    && record.QuestsOffered(content).Contains(waking) && Ally(record, content, owner) is { } ally)
                {
                    var board = MapFiles.Load(Path.Combine(contentRoot, MapFiles.QuestsDirectory, waking.MapId + MapFiles.Extension), content);
                    for (; questTries < Attempts && questWon is null; questTries++)
                    {
                        var tried = record with { Seed = unchecked(record.Seed + (ulong)questTries * 7919UL) };
                        var (end, _) = Fight(tried.BeginQuest(board, waking.Id, ally, content), content, seed, number);
                        if (end.Outcome.Result == BattleResult.Won && end.Survivors().Any(u => u.Id == owner))
                        {
                            record = record.AfterQuest(end, waking.Id, content).Record;
                            questWon = number;
                            var opened = record.Find(owner)!.Inventory.Items.FirstOrDefault(s => s.ItemId == itemId);
                            if (opened.Stage == ladder.Turns.Count && turned[^1] == 0)
                            {
                                turned[^1] = number;
                            }
                        }
                    }
                }
            }

            runs.Add(new Run(maps, turned, lost) { QuestWonAfter = questWon, QuestTries = questTries });
        }

        return runs;
    }

    /// <summary>The ally the quest arm takes beside <paramref name="owner"/>: the first standing recruit on the roster who is neither the owner nor the captain.</summary>
    private static string? Ally(CampaignRecord record, GameContent content, string owner) =>
        record.Roster.Select(u => u.Id).FirstOrDefault(id => id != owner && id != content.Cast[0].Id);

    /// <summary>One battle under the heuristic player to its end, with the heirloom turns it saw.</summary>
    private static (BattleState End, List<HeirloomTurned> Turns) Fight(BattleState state, GameContent content, int seed, int number)
    {
        var player = new HeuristicPlayer();
        var turns = new List<HeirloomTurned>();
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

                turns.AddRange(result.Events.OfType<HeirloomTurned>());
                state = result.Next;
                if (state.Outcome.IsOver)
                {
                    break;
                }
            }
        }

        return (state, turns);
    }

    /// <summary>The printed table: per map, the median combats by its end over runs that reached it; per stage, the median map it turned on and how many runs turned it.</summary>
    public static IEnumerable<string> Lines(GameContent content, string itemId, IReadOnlyList<Run> runs)
    {
        var ladder = content.Weapon(itemId).Heirloom!;
        yield return $"heirloom: {itemId}, {runs.Count} runs, the heuristic player from its owner's first map to map {LastMap}";
        foreach (var number in runs.SelectMany(r => r.Maps.Select(m => m.Map)).Distinct().Order())
        {
            var reached = runs.SelectMany(r => r.Maps.Where(m => m.Map == number)).ToList();
            yield return $"  map {number}: won {reached.Count}, owner deployed in {reached.Count(m => m.Deployed)}, tries p50 {Percentile(reached.Select(m => m.Attempts), 0.5)}, combats by its end p25 {Percentile(reached.Select(m => m.Combats), 0.25)} p50 {Percentile(reached.Select(m => m.Combats), 0.5)} p75 {Percentile(reached.Select(m => m.Combats), 0.75)}";
        }

        if (runs.Any(r => r.QuestTries > 0))
        {
            var tried = runs.Where(r => r.QuestTries > 0).ToList();
            yield return $"  quest arm: won the waking quest in {tried.Count(r => r.QuestWonAfter is not null)} of {tried.Count} runs offered it, tries p50 {Percentile(tried.Select(r => r.QuestTries), 0.5)}";
        }

        foreach (var lostOn in runs.Where(r => r.LostOn is not null).GroupBy(r => r.LostOn!.Value).OrderBy(g => g.Key))
        {
            yield return $"  no try won map {lostOn.Key}: {lostOn.Count()}";
        }

        for (var i = 0; i < ladder.Turns.Count; i++)
        {
            var stage = ladder.Turns[i];
            var on = runs.Select(r => r.TurnedOn[i]).Where(m => m > 0).ToList();
            var median = on.Count * 2 > runs.Count ? Percentile(runs.Select(r => r.TurnedOn[i] == 0 ? int.MaxValue : r.TurnedOn[i]), 0.5).ToString(System.Globalization.CultureInfo.InvariantCulture) : "never";
            yield return $"  {stage.Id} (at {stage.At}): turned in {on.Count} of {runs.Count}, median map {median}";
        }
    }

    /// <summary>The owner with the heirloom in front of their pack in place of the first weapon of its type they carry.</summary>
    private static Unit Issued(Unit unit, Weapon item, GameContent content)
    {
        var items = unit.Inventory.Items.Where(s => s.ItemId != item.Id).ToList();
        var replaced = items.FindIndex(s => content.Weapons.TryGetValue(s.ItemId, out var w) && w.Type == item.Type);
        if (replaced >= 0)
        {
            items.RemoveAt(replaced);
        }

        items.Insert(0, new ItemStack(item.Id, item.Durability));
        return unit with { Inventory = new Inventory(ValueList<ItemStack>.From(items)) };
    }

    private static int Percentile(IEnumerable<int> values, double p)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, (int)Math.Floor(p * sorted.Count))];
    }
}
