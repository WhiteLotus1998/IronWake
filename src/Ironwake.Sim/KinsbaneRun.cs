using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// Kinsbane's timing (issue 804, round 250: measure the wake first): the heuristic player fights the
/// whole campaign from map 1 as <see cref="LevelRun"/> does, permadeath off, but picks
/// <see cref="Owner"/> at the branch's camp, where the campaign issues her the hungering weapon in
/// front of her pack beside her iron axe (<see cref="CampaignRecord.Kitted"/>, issue 851),
/// and benches from the back so she deploys on every map she can (<see cref="PairingPlayer.Deploy"/>).
/// The control arm (round 263) takes the scythe out and leaves the iron axe in front, on the same
/// seeds, so Sallow's clear rate with and without the hunger can be laid side by side.
/// The heuristic is the one every other read uses, so it takes the hunt only as far as it already
/// strikes with her. A map is tried on fresh seeds, up to <see cref="HeirloomRun.Attempts"/> times,
/// until it is won with her standing where she fought, the Recall a player would spend. Per seed it
/// reads the feed count by each map's end, the drains she paid and the starved forms, and the map each
/// tooth grew on (<see cref="Kinsbane.Teeth"/>; the fifth wakes it); a lost map ends that run.
/// A measurement only; nothing here changes what ships.
/// </summary>
public static class KinsbaneRun
{
    /// <summary>The claimant who carries the scythe (STORY draft 6, DESIGN 13.23).</summary>
    public const string Owner = "keziah";

    /// <summary>One map won: its campaign number, the feed count by its end, whether she was deployed, the drains she paid and the times it starved on it, the tries it took, the attacks she made on the winning try with the scythe and with anything else (issue 851, round 277), and whether the scythe was still in its starved form at the map's end (issue 856, round 281).</summary>
    public sealed record MapRead(int Map, int Fed, bool Deployed, int Drains, int Starved, int Attempts, int ScytheAttacks = 0, int OtherAttacks = 0, bool EndedStarved = false);

    /// <summary>One run: the maps won from her first, the campaign map each tooth grew on (0 when it never did), and the map no try won, or null.</summary>
    public sealed record Run(IReadOnlyList<MapRead> Maps, IReadOnlyList<int> ToothOn, int? LostOn);

    /// <summary>Every run over seeds 1..<paramref name="seeds"/>.</summary>
    public static IReadOnlyList<Run> Measure(string contentRoot, GameContent content, int seeds, bool axe = false)
    {
        var runs = new List<Run>();
        for (var seed = 1; seed <= seeds; seed++)
        {
            var record = CampaignRecord.Start(content, (ulong)seed, permadeath: false);
            var maps = new List<MapRead>();
            var toothOn = new int[Kinsbane.MtCap];
            var issued = false;
            int? lost = null;
            while (!record.IsFinished(content))
            {
                if (record.Pick is null && record.NextMap(content).Branch.Contains(Owner))
                {
                    record = record.PickClaimant(Owner, content).Record;
                }

                record = SimPick.Made(record, content);
                if (!issued && record.Present(content).FirstOrDefault(u => u.Id == Owner) is not null)
                {
                    if (axe)
                    {
                        record = record with { Roster = ValueList<Unit>.From(record.Present(content).Select(u => u.Id == Owner ? Control(u, content) : u)) };
                    }

                    issued = true;
                }

                var number = record.MapIndex + 1;
                var map = MapFiles.Load(MapFiles.CampaignPath(contentRoot, content, record.NextMap(content).MapId), content);
                BattleState? won = null;
                var events = new List<GameEvent>();
                var swings = (Scythe: 0, Other: 0);
                var attempt = 0;
                for (; attempt < HeirloomRun.Attempts && won is null; attempt++)
                {
                    var tried = record with { Seed = unchecked(record.Seed + (ulong)attempt * 7919UL) };
                    if (issued)
                    {
                        tried = PairingPlayer.Deploy(tried, map, content, Owner, Owner);
                    }

                    var (end, seen, swung) = Fight(tried.Begin(map, content), content, seed, number);
                    var fought = end.History[0].UnitsOf(Side.Player).Any(u => u.Id == Owner);
                    if (end.Outcome.Result == BattleResult.Won && (!fought || end.Survivors().Any(u => u.Id == Owner)))
                    {
                        won = end;
                        events = seen;
                        swings = swung;
                    }
                }

                if (won is null)
                {
                    lost = number;
                    break;
                }

                record = record.AfterBattle(won, content);
                if (!issued)
                {
                    continue;
                }

                foreach (var fed in events.OfType<HungerFed>().Where(f => f.UnitId == Owner && Kinsbane.ToothGrew(f.Fed)))
                {
                    toothOn[Kinsbane.Teeth(fed.Fed) - 1] = number;
                }

                var stack = record.Find(Owner)?.Inventory.Items.FirstOrDefault(s => s.ItemId == Kinsbane.ItemId);
                var drains = events.OfType<HungerDrained>().Where(d => d.UnitId == Owner).ToList();
                maps.Add(new MapRead(number, stack?.Fed ?? 0, won.History[0].UnitsOf(Side.Player).Any(u => u.Id == Owner), drains.Count, drains.Count(d => d.Starved), attempt, swings.Scythe, swings.Other, stack?.Starved ?? false));
            }

            runs.Add(new Run(maps, toothOn, lost));
        }

        return runs;
    }

    /// <summary>One battle under the heuristic player to its end, with the hunger's events it saw and the attacks <see cref="Owner"/> made with the scythe and with anything else.</summary>
    private static (BattleState End, List<GameEvent> Events, (int Scythe, int Other) Swings) Fight(BattleState state, GameContent content, int seed, int number)
    {
        var player = new HeuristicPlayer();
        var seen = new List<GameEvent>();
        var swings = (Scythe: 0, Other: 0);
        while (!state.Outcome.IsOver)
        {
            var commands = state.Phase == Side.Player ? player.Next(state, content) : EnemyAi.Plan(state, content);
            foreach (var command in commands)
            {
                if (command is Attack attack && attack.UnitId == Owner && state.Find(Owner) is { } owner)
                {
                    var slot = attack.Slot ?? owner.EquippedSlot(content);
                    if (owner.Unit.Inventory.Items[slot].ItemId == Kinsbane.ItemId)
                    {
                        swings.Scythe++;
                    }
                    else
                    {
                        swings.Other++;
                    }
                }

                var result = Resolver.Apply(state, content, command);
                if (!result.Accepted)
                {
                    throw new InvalidOperationException($"seed {seed} map {number}: {command} was rejected: {result.Rejection!.Message}");
                }

                seen.AddRange(result.Events.Where(e => e is HungerFed or HungerDrained));
                state = result.Next;
                if (state.Outcome.IsOver)
                {
                    break;
                }
            }
        }

        return (state, seen, swings);
    }

    /// <summary>The printed table: per map from her first, the feed count by its end (p25, p50, p75), the drains and starved forms, and on the scythe arm the runs woken and starved by its end (rounds 279, 281) and those woken entering the last map, the keep; per tooth, the median map it grew on and how many runs grew it.</summary>
    public static IEnumerable<string> Lines(IReadOnlyList<Run> runs, bool axe = false)
    {
        yield return $"kinsbane: {runs.Count} runs, the heuristic player through the campaign, {Owner} picked at the branch and fielded on every map she can, " + (axe ? "the control arm: her iron axe in front, no scythe" : "the scythe in front of her pack, her iron axe behind it, as the campaign issues it");
        var first = runs.SelectMany(r => r.Maps.Select(m => m.Map)).DefaultIfEmpty(0).Min();
        foreach (var number in runs.SelectMany(r => r.Maps.Select(m => m.Map)).Distinct().Order())
        {
            var reached = runs.SelectMany(r => r.Maps.Where(m => m.Map == number)).ToList();
            yield return $"  map {number} (her map {number - first + 1}): won {reached.Count}, deployed in {reached.Count(m => m.Deployed)}, tries p50 {Percentile(reached.Select(m => m.Attempts), 0.5)}, fed by its end p25 {Percentile(reached.Select(m => m.Fed), 0.25)} p50 {Percentile(reached.Select(m => m.Fed), 0.5)} p75 {Percentile(reached.Select(m => m.Fed), 0.75)} (teeth p50 {Kinsbane.Teeth(Percentile(reached.Select(m => m.Fed), 0.5))}/{Kinsbane.MtCap}), drains p50 {Percentile(reached.Select(m => m.Drains), 0.5)}, starved in {reached.Count(m => m.Starved > 0)}, her attacks {reached.Sum(m => m.ScytheAttacks + m.OtherAttacks)} (scythe {reached.Sum(m => m.ScytheAttacks)})" + (axe ? "" : $", woken by its end in {reached.Count(m => Kinsbane.Woken(m.Fed))}, ended starved in {reached.Count(m => m.EndedStarved)}");
        }

        foreach (var lostOn in runs.Where(r => r.LostOn is not null).GroupBy(r => r.LostOn!.Value).OrderBy(g => g.Key))
        {
            yield return $"  no try won map {lostOn.Key}: {lostOn.Count()}";
        }

        if (axe)
        {
            yield break;
        }

        var last = runs.SelectMany(r => r.Maps.Select(m => m.Map)).DefaultIfEmpty(0).Max();
        var entering = runs.Select(r => r.Maps.FirstOrDefault(m => m.Map == last - 1)).OfType<MapRead>().ToList();
        yield return $"  woken entering the keep (map {last}, fed {Kinsbane.WakeKills} by map {last - 1}'s end): {entering.Count(m => Kinsbane.Woken(m.Fed))} of {entering.Count} reachers";
        var armed = runs.Where(r => r.Maps.Count > 0).ToList();
        yield return $"  teeth, over the {armed.Count} runs that won her first map (a run lost later counts as never):";
        for (var i = 0; i < Kinsbane.MtCap; i++)
        {
            var on = armed.Select(r => r.ToothOn[i]).Where(m => m > 0).ToList();
            var median = on.Count * 2 > armed.Count ? Percentile(armed.Select(r => r.ToothOn[i] == 0 ? int.MaxValue : r.ToothOn[i]), 0.5).ToString(System.Globalization.CultureInfo.InvariantCulture) : "never";
            var label = i + 1 == Kinsbane.MtCap ? $"tooth {i + 1} (wakes, fed {Kinsbane.WakeKills})" : $"tooth {i + 1} (fed {Kinsbane.FedFor(i + 1)})";
            yield return $"  {label}: grew in {on.Count} of {armed.Count}, median map {median}";
        }
    }

    /// <summary>The control arm's owner: the hungering weapon taken out of her pack, leaving her cast iron axe in front at full uses (issue 851: it already sits behind the scythe).</summary>
    private static Unit Control(Unit unit, GameContent content)
    {
        var axe = content.Unit(Owner).Inventory.Items.First(s => content.Weapons.TryGetValue(s.ItemId, out var w) && w.Type == content.Weapon(Kinsbane.ItemId).Type);
        var rest = unit.Inventory.Items.Where(s => s.ItemId != Kinsbane.ItemId && s.ItemId != axe.ItemId);
        var items = rest.Prepend(new ItemStack(axe.ItemId, content.Weapon(axe.ItemId).Durability));
        return unit with { Inventory = new Inventory(ValueList<ItemStack>.From(items)) };
    }

    private static int Percentile(IEnumerable<int> values, double p)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, (int)Math.Floor(p * sorted.Count))];
    }
}
