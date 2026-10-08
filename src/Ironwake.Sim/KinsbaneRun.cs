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
/// Issue 871 adds the walking drains (a drain paid at a phase start with no enemy in her reach,
/// <see cref="Kinsbane.Smells"/> read on the board it fired on) and the heeding arm, which benches
/// her on every <c>keziah_warning</c> map instead of answering the question.
/// Issue 1378 (rounds 483, 484) adds the quest arm: at each camp the run takes every side map offered,
/// at its seat, retried like a map and skipped when no try wins it, until it has tried
/// <see cref="Shrine"/>, her own quest; after that it takes none, so the field and the keep are fought
/// as the main line leaves them. The shrine's read is the feed on entering and leaving it and whether
/// it woke the scythe, split against the keep's wins. A tooth grown on a side map counts on the map its camp follows.
/// A measurement only; nothing here changes what ships.
/// </summary>
public static class KinsbaneRun
{
    /// <summary>The claimant who carries the scythe (STORY draft 6, DESIGN 13.23).</summary>
    public const string Owner = "keziah";

    /// <summary>Her first side map, the Burned Shrine (issue 1378): the quest arm takes side maps up to and including this one.</summary>
    public const string Shrine = "keziah_1";

    /// <summary>The three arms (issue 871): committed fields her everywhere, heeding benches her on a flagged map, axe is the control.</summary>
    public enum Arm
    {
        Committed,
        Heeding,
        Axe,
    }

    /// <summary>One map won: its campaign number, the feed count by its end, whether she was deployed, the drains she paid and the times it starved on it, the tries it took, the attacks she made on the winning try with the scythe and with anything else (issue 851, round 277), whether the scythe was still in its starved form at the map's end (issue 856, round 281), whether the woken scythe ran the hunt on (issue 804 item 4), and the drains among them paid with no enemy in her reach (issue 871), and the tries on which she fell (round 286; the winning try never has her fallen).</summary>
    public sealed record MapRead(int Map, int Fed, bool Deployed, int Drains, int Starved, int Attempts, int ScytheAttacks = 0, int OtherAttacks = 0, bool EndedStarved = false, bool HuntRan = false, int WalkingDrains = 0, int Falls = 0);

    /// <summary>One try at the shrine (issue 1378): the feed by its end (the last feed it saw, or the feed entering), the turn it ended on, and how it ended.</summary>
    public sealed record ShrineTry(int Fed, int Turn, BattleResult Result, LossCause Cause);

    /// <summary>The shrine on the quest arm (issue 1378): the map its camp follows, the feed on entering and on leaving it (entering again when no try won it), whether a try won it, and every try, won or lost.</summary>
    public sealed record ShrineRead(int After, int FedIn, int FedOut, bool Won, IReadOnlyList<ShrineTry> Tries);

    /// <summary>One run: the maps won from her first, the campaign map each tooth grew on (0 when it never did), and the map no try won, or null; on the quest arm the side maps it won and the shrine's read, null when it was never offered.</summary>
    public sealed record Run(IReadOnlyList<MapRead> Maps, IReadOnlyList<int> ToothOn, int? LostOn)
    {
        /// <summary>The side maps the quest arm won, the shrine included.</summary>
        public int QuestsWon { get; init; }

        /// <summary>The shrine's read on the quest arm; null when it was never offered or the arm is off.</summary>
        public ShrineRead? Shrine { get; init; }
    }

    /// <summary>Every run over seeds 1..<paramref name="seeds"/>.</summary>
    public static IReadOnlyList<Run> Measure(string contentRoot, GameContent content, int seeds, Arm arm = Arm.Committed, bool quests = false)
    {
        var axe = arm == Arm.Axe;
        var runs = new List<Run>();
        for (var seed = 1; seed <= seeds; seed++)
        {
            var record = CampaignRecord.Start(content, (ulong)seed, permadeath: false);
            var maps = new List<MapRead>();
            var toothOn = new int[Kinsbane.MtCap];
            var issued = false;
            int? lost = null;
            var questsWon = 0;
            ShrineRead? shrine = null;
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
                var walked = 0;
                var falls = 0;
                var attempt = 0;
                for (; attempt < HeirloomRun.Attempts && won is null; attempt++)
                {
                    var tried = record with { Seed = unchecked(record.Seed + (ulong)attempt * 7919UL) };
                    if (issued && arm == Arm.Heeding && map.KeziahWarning)
                    {
                        var benched = tried.Bench(Owner, map, content);
                        tried = benched.Accepted ? benched.Record : tried;
                    }
                    else if (issued)
                    {
                        tried = PairingPlayer.Deploy(tried, map, content, Owner, Owner);
                    }

                    var (end, seen, swung, walkingDrains) = Fight(tried.Begin(map, content), content, seed, number);
                    var fought = end.History[0].UnitsOf(Side.Player).Any(u => u.Id == Owner);
                    falls += fought && !end.Survivors().Any(u => u.Id == Owner) ? 1 : 0;
                    if (end.Outcome.Result == BattleResult.Won && (!fought || end.Survivors().Any(u => u.Id == Owner)))
                    {
                        won = end;
                        events = seen;
                        swings = swung;
                        walked = walkingDrains;
                    }
                }

                if (won is null)
                {
                    lost = number;
                    break;
                }

                record = record.AfterBattle(won, content);
                if (quests && shrine is null)
                {
                    (record, var taken, shrine) = TakeQuests(record, contentRoot, content, seed, number, toothOn);
                    questsWon += taken;
                }

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
                maps.Add(new MapRead(number, stack?.Fed ?? 0, won.History[0].UnitsOf(Side.Player).Any(u => u.Id == Owner), drains.Count, drains.Count(d => d.Starved), attempt, swings.Scythe, swings.Other, stack?.Starved ?? false, events.OfType<HuntRanOn>().Any(h => h.UnitId == Owner), walked, falls));
            }

            runs.Add(new Run(maps, toothOn, lost) { QuestsWon = questsWon, Shrine = shrine });
        }

        return runs;
    }

    /// <summary>
    /// The quest arm's camp after map <paramref name="number"/> (issue 1378): every side map offered is
    /// taken in the order offered, its member beside the first standing recruits who are neither the
    /// member nor the captain, as many as its board seats, on fresh seeds up to
    /// <see cref="HeirloomRun.Attempts"/> times; one refused (a full pack, a short roster) or never won is
    /// left. Returns the record, the side maps won, and the shrine's read if it was offered here.
    /// </summary>
    private static (CampaignRecord Record, int Won, ShrineRead? Shrine) TakeQuests(CampaignRecord record, string contentRoot, GameContent content, int seed, int number, int[] toothOn)
    {
        var won = 0;
        ShrineRead? shrine = null;
        var tried = new HashSet<string>(StringComparer.Ordinal);
        while (record.QuestsOffered(content).FirstOrDefault(q => !tried.Contains(q.Id)) is { } quest)
        {
            tried.Add(quest.Id);
            var board = MapFiles.Load(Path.Combine(contentRoot, MapFiles.QuestsDirectory, quest.MapId + MapFiles.Extension), content);
            var allies = record.Roster.Select(u => u.Id).Where(id => id != quest.MemberId && id != content.Cast[0].Id).Take(CampaignRecord.QuestAllies(board)).ToList();
            if (CampaignRecord.QuestAlliesRefusal(board, allies) is not null || record.QuestRefusal(quest.Id, allies, content) is not null)
            {
                continue;
            }

            var fedIn = FedOf(record);
            var tries = new List<ShrineTry>();
            BattleState? end = null;
            List<GameEvent> events = new();
            while (tries.Count < HeirloomRun.Attempts && end is null)
            {
                var attempt = record with { Seed = unchecked(record.Seed + (ulong)tries.Count * 7919UL) };
                var (fought, seen, _, _) = Fight(attempt.BeginQuest(board, quest.Id, allies, content), content, seed, number);
                tries.Add(new ShrineTry(seen.OfType<HungerFed>().Where(f => f.UnitId == Owner).Select(f => f.Fed).DefaultIfEmpty(fedIn).Last(), fought.Turn, fought.Outcome.Result, fought.Outcome.Cause));
                if (fought.Outcome.Result == BattleResult.Won)
                {
                    end = fought;
                    events = seen;
                }
            }

            if (end is not null)
            {
                record = record.AfterQuest(end, quest.Id, content).Record;
                won++;
                foreach (var fed in events.OfType<HungerFed>().Where(f => f.UnitId == Owner && Kinsbane.ToothGrew(f.Fed)))
                {
                    toothOn[Kinsbane.Teeth(fed.Fed) - 1] = number;
                }
            }

            if (quest.Id == Shrine)
            {
                shrine = new ShrineRead(number, fedIn, FedOf(record), end is not null, tries);
                break;
            }
        }

        return (record, won, shrine);
    }

    /// <summary>The scythe's feed count on the record, 0 when she does not carry it.</summary>
    private static int FedOf(CampaignRecord record)
    {
        var stack = record.Find(Owner)?.Inventory.Items.FirstOrDefault(s => s.ItemId == Kinsbane.ItemId);
        return stack?.Fed ?? 0;
    }

    /// <summary>One battle under the heuristic player to its end, with the hunger's events it saw and the attacks <see cref="Owner"/> made with the scythe and with anything else.</summary>
    private static (BattleState End, List<GameEvent> Events, (int Scythe, int Other) Swings, int Walking) Fight(BattleState state, GameContent content, int seed, int number)
    {
        var walking = 0;
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

                seen.AddRange(result.Events.Where(e => e is HungerFed or HungerDrained or HuntRanOn));
                if (result.Events.OfType<HungerDrained>().Any(d => d.UnitId == Owner) && result.Next.Find(Owner) is { } carrier && !Kinsbane.Smells(result.Next, content, carrier))
                {
                    walking++;
                }
                state = result.Next;
                if (state.Outcome.IsOver)
                {
                    break;
                }
            }
        }

        return (state, seen, swings, walking);
    }

    /// <summary>The printed table: per map from her first, the feed count by its end (p25, p50, p75), the drains and starved forms, and on the scythe arm the runs woken and starved by its end (rounds 279, 281) and those whose hunt ran on (issue 804) and those woken entering the last map, the keep; per tooth, the median map it grew on and how many runs grew it.</summary>
    public static IEnumerable<string> Lines(IReadOnlyList<Run> runs, Arm arm = Arm.Committed)
    {
        var axe = arm == Arm.Axe;
        yield return $"kinsbane: {runs.Count} runs, the heuristic player through the campaign, {Owner} picked at the branch and fielded on every map she can, " + (axe ? "the control arm: her iron axe in front, no scythe" : "the scythe in front of her pack, her iron axe behind it, as the campaign issues it") + (arm == Arm.Heeding ? "; the heeding arm: benched on every keziah_warning map" : "");
        var first = runs.SelectMany(r => r.Maps.Select(m => m.Map)).DefaultIfEmpty(0).Min();
        foreach (var number in runs.SelectMany(r => r.Maps.Select(m => m.Map)).Distinct().Order())
        {
            var reached = runs.SelectMany(r => r.Maps.Where(m => m.Map == number)).ToList();
            yield return $"  map {number} (her map {number - first + 1}): won {reached.Count}, deployed in {reached.Count(m => m.Deployed)}, tries p50 {Percentile(reached.Select(m => m.Attempts), 0.5)}, fed by its end p25 {Percentile(reached.Select(m => m.Fed), 0.25)} p50 {Percentile(reached.Select(m => m.Fed), 0.5)} p75 {Percentile(reached.Select(m => m.Fed), 0.75)} (teeth p50 {Kinsbane.Teeth(Percentile(reached.Select(m => m.Fed), 0.5))}/{Kinsbane.MtCap}), drains p50 {Percentile(reached.Select(m => m.Drains), 0.5)} (walking p50 {Percentile(reached.Select(m => m.WalkingDrains), 0.5)}, p75 {Percentile(reached.Select(m => m.WalkingDrains), 0.75)}), her falls {reached.Sum(m => m.Falls)}, starved in {reached.Count(m => m.Starved > 0)}, her attacks {reached.Sum(m => m.ScytheAttacks + m.OtherAttacks)} (scythe {reached.Sum(m => m.ScytheAttacks)})" + (axe ? "" : $", woken by its end in {reached.Count(m => Kinsbane.Woken(m.Fed))}, ended starved in {reached.Count(m => m.EndedStarved)}, the hunt ran on in {reached.Count(m => m.HuntRan)}");
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
        foreach (var line in ShrineLines(runs, last))
        {
            yield return line;
        }

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

    /// <summary>
    /// The quest arm's shrine read (issue 1378, round 483's bar): the runs offered it and won it, the feed
    /// entering and leaving at p25/p50/p75 over the winners, the runs woken by its end, and the keep
    /// (map <paramref name="last"/>) won by runs the shrine woke against the shrine's other winners. Empty off the quest arm.
    /// </summary>
    private static IEnumerable<string> ShrineLines(IReadOnlyList<Run> runs, int last)
    {
        var offered = runs.Where(r => r.Shrine is not null).ToList();
        if (offered.Count == 0)
        {
            yield break;
        }

        var won = offered.Where(r => r.Shrine!.Won).ToList();
        var tries = offered.SelectMany(r => r.Shrine!.Tries).ToList();
        var woken = won.Where(r => Kinsbane.Woken(r.Shrine!.FedOut)).ToList();
        var rest = won.Where(r => !Kinsbane.Woken(r.Shrine!.FedOut)).ToList();
        bool Keep(Run r) => r.Maps.Any(m => m.Map == last);
        yield return $"  side maps won before and at the shrine p50 {Percentile(offered.Select(r => r.QuestsWon), 0.5)}";
        yield return $"  the shrine ({Shrine}): offered in {offered.Count}, after map p50 {Percentile(offered.Select(r => r.Shrine!.After), 0.5)}, won in {won.Count}, tries p50 {Percentile(offered.Select(r => r.Shrine!.Tries.Count), 0.5)}, fed entering p25 {Percentile(offered.Select(r => r.Shrine!.FedIn), 0.25)} p50 {Percentile(offered.Select(r => r.Shrine!.FedIn), 0.5)} p75 {Percentile(offered.Select(r => r.Shrine!.FedIn), 0.75)}, leaving (winners) p25 {Percentile(won.Select(r => r.Shrine!.FedOut), 0.25)} p50 {Percentile(won.Select(r => r.Shrine!.FedOut), 0.5)} p75 {Percentile(won.Select(r => r.Shrine!.FedOut), 0.75)}";
        yield return $"  every try, won or lost: {tries.Count}, fed by its end p25 {Percentile(tries.Select(t => t.Fed), 0.25)} p50 {Percentile(tries.Select(t => t.Fed), 0.5)} p75 {Percentile(tries.Select(t => t.Fed), 0.75)} max {tries.Select(t => t.Fed).DefaultIfEmpty(0).Max()}, woken by its end in {tries.Count(t => Kinsbane.Woken(t.Fed))}, ended turn p50 {Percentile(tries.Select(t => t.Turn), 0.5)}, lost on her fall {tries.Count(t => t.Cause == LossCause.Captain)}, on the clock {tries.Count(t => t.Cause == LossCause.Timeout)}; runs with a try that woke it {offered.Count(r => r.Shrine!.Tries.Any(t => Kinsbane.Woken(t.Fed)))} of {offered.Count}";
        yield return $"  woken by the shrine's end: {woken.Count} of {won.Count} winners (woken entering it: {won.Count(r => Kinsbane.Woken(r.Shrine!.FedIn))})";
        yield return $"  the keep (map {last}) won: shrine-woken {woken.Count(Keep)} of {woken.Count}, the shrine's other winners {rest.Count(Keep)} of {rest.Count}, the shrine lost {offered.Count(r => !r.Shrine!.Won && Keep(r))} of {offered.Count - won.Count}";
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
