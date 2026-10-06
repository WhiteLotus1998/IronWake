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
/// over the threshold by map 7). Per map it also reads the battle won as it began, at that map's camp
/// (issue 738): the map's enemy level, the levels of the units deployed, and the EXP the captain and
/// the whole company kept from it, so the spread under the top unit is printed beside it. The heuristic never promotes, so the
/// levels are those of the first step; a lost map ends that run. A measurement only; nothing here
/// changes what ships.
/// </summary>
public static class LevelRun
{
    /// <summary>The level the second tier asks for (classes.json, every advanced form; 7 since rounds 224 to 226).</summary>
    public const int Threshold = 7;

    /// <summary>One run: per map won, the levels of the company standing after it, how many of it meet some advanced form's level and ranks (<see cref="Ready"/>) and the map's <see cref="Camp"/> reading, and the map no try won, or null.</summary>
    public sealed record Run(IReadOnlyList<(int Map, IReadOnlyList<int> Levels, int Ready, Camp Camp)> Maps, int? LostOn)
    {
        /// <summary>The weapons the company attacked and countered with over the run's won battles, by class as each unit stood when it struck (issue 746).</summary>
        public IReadOnlyDictionary<string, WeaponMix> Weapons { get; init; } = new Dictionary<string, WeaponMix>(StringComparer.Ordinal);

        /// <summary>The record's rapport after each won map, one entry per <see cref="Maps"/> entry in the same order (issue 77).</summary>
        public IReadOnlyList<IReadOnlyList<Rapport>> Rapport { get; init; } = [];

        /// <summary>The company standing after each won map, one entry per <see cref="Maps"/> entry in the same order (issue 1135).</summary>
        public IReadOnlyList<IReadOnlyList<Member>> Companies { get; init; } = [];

        /// <summary>The kills <see cref="EvenPlayer"/> handed to another unit over the run's won battles (issue 1150); 0 for any other player.</summary>
        public int Handed { get; init; }
    }

    /// <summary>
    /// One living unit after a won map (issue 1135): its level, the rank points in its main weapon
    /// (<see cref="MainType"/>), whether it meets some advanced form (<see cref="Ready"/>), and whether it is the captain.
    /// </summary>
    public sealed record Member(int Level, int MainRank, bool Ready, bool Captain)
    {
        /// <summary>The tier-2 bar of rounds 224 to 226 read on the main weapon alone: <see cref="Threshold"/> and rank C.</summary>
        public bool AtBar => AtLevel(Threshold) && AtRank;

        /// <summary>
        /// What the closest advanced form still refuses a unit that meets none (issue 1150): the requirement
        /// keys of <see cref="Certifications.Check(Unit, UnitClass, UnitClass, bool)"/> as <see cref="Ready"/>
        /// reads it, for the form with the fewest, content order breaking ties; empty when the unit meets one.
        /// </summary>
        public IReadOnlyList<string> Refused { get; init; } = [];

        /// <summary>The bar's level half alone, read at <paramref name="level"/> or above.</summary>
        public bool AtLevel(int level) => Level >= level;

        /// <summary>The tier-2 bar's rank half alone: rank C or above in the main weapon.</summary>
        public bool AtRank => MainRank >= WeaponRanks.Threshold(WeaponRank.C);

        /// <summary>The <see cref="Member"/> reading of <paramref name="unit"/>.</summary>
        public static Member Of(Unit unit, GameContent content)
        {
            var ready = LevelRun.Ready(unit, content);
            return new(unit.Level, unit.Skill.Points(MainType(unit, content)), ready, CampaignRecord.IsCaptain(unit, content))
            {
                Refused = ready ? [] : Closest(unit, content),
            };
        }
    }

    /// <summary>
    /// A unit's main weapon type: the first weapon on its cast card, as the seat trains it (issue 1130),
    /// or, for a unit whose card carries none, the type it holds the most rank points in.
    /// </summary>
    public static WeaponType MainType(Unit unit, GameContent content)
    {
        var card = content.Units.TryGetValue(unit.Id, out var cast) ? cast : unit;
        if (card.Inventory.Items.Select(s => content.Weapons.GetValueOrDefault(s.ItemId)).FirstOrDefault(w => w is not null) is { } main)
        {
            return main.Type;
        }

        return Enum.GetValues<WeaponType>().OrderByDescending(unit.Skill.Points).First();
    }

    /// <summary>
    /// A won map as its battle began and ended (issue 738): the enemy level it was fought at, the
    /// levels of the player units deployed into it, and the EXP the captain and the company kept from
    /// it. A unit that fell keeps nothing, since the campaign brings it back as it began the battle.
    /// </summary>
    public sealed record Camp(int EnemyLevel, IReadOnlyList<int> Deployed, int CaptainExp, int CompanyExp);

    /// <summary>The <see cref="Camp"/> reading of a battle from its first state to its won last one.</summary>
    public static Camp Read(BattleState start, BattleState end, GameContent content)
    {
        var deployed = start.UnitsOf(Side.Player).ToList();
        var after = end.Survivors().ToDictionary(u => u.Id, u => u.Unit);
        var captainExp = 0;
        var companyExp = 0;
        foreach (var unit in deployed)
        {
            if (!after.TryGetValue(unit.Id, out var kept))
            {
                continue;
            }

            var earned = TotalExp(kept) - TotalExp(unit.Unit);
            companyExp += earned;
            if (CampaignRecord.IsCaptain(unit.Unit, content))
            {
                captainExp += earned;
            }
        }

        return new Camp(start.Map.EnemyLevel, deployed.Select(u => u.Unit.Level).ToList(), captainExp, companyExp);
    }

    /// <summary>The EXP a unit holds counted from level 1: a level is <see cref="Experience.LevelUpAt"/>.</summary>
    public static int TotalExp(Unit unit) => (unit.Level - Unit.MinLevel) * Experience.LevelUpAt + unit.Exp;

    /// <summary>The captain's share of the company's EXP on one map, a whole percent, or null when the company earned none.</summary>
    public static int? CaptainShare(Camp camp) =>
        camp.CompanyExp <= 0 ? null : (int)Math.Round(100.0 * camp.CaptainExp / camp.CompanyExp, MidpointRounding.AwayFromZero);

    /// <summary>The median of a list of levels, the lower middle on an even count, as <see cref="Percentile"/> reads p50 elsewhere in the table.</summary>
    public static int Median(IReadOnlyList<int> levels)
    {
        var sorted = levels.Order().ToList();
        return sorted.Count == 0 ? 0 : sorted[(sorted.Count - 1) / 2];
    }

    /// <summary>
    /// Every run over seeds 1..<paramref name="seeds"/>. With <paramref name="pair"/> set the player is
    /// <see cref="PairingPlayer"/> for that support pair, each map's bench set by
    /// <see cref="PairingPlayer.Deploy"/> so both members fight it (issue 77, slice 5).
    /// </summary>
    /// <remarks>With <paramref name="even"/> set the player is <see cref="EvenPlayer"/>, the even-company chair (issue 1150).</remarks>
    public static IReadOnlyList<Run> Measure(string contentRoot, GameContent content, int seeds, (string A, string B)? pair = null, bool even = false)
    {
        var runs = new List<Run>();
        for (var seed = 1; seed <= seeds; seed++)
        {
            var record = CampaignRecord.Start(content, (ulong)seed, permadeath: false);
            var maps = new List<(int, IReadOnlyList<int>, int, Camp)>();
            var weapons = new Dictionary<string, WeaponMix>(StringComparer.Ordinal);
            var rapport = new List<IReadOnlyList<Rapport>>();
            var companies = new List<IReadOnlyList<Member>>();
            var handed = 0;
            int? lost = null;
            while (!record.IsFinished(content))
            {
                record = SimPick.Made(record, content);
                var number = record.MapIndex + 1;
                var map = MapFiles.Load(MapFiles.CampaignPath(contentRoot, content, record.NextMap(content).MapId), content);
                BattleState? won = null;
                Camp? camp = null;
                Dictionary<string, WeaponMix>? struck = null;
                for (var attempt = 0; attempt < HeirloomRun.Attempts && won is null; attempt++)
                {
                    var tried = record with { Seed = unchecked(record.Seed + (ulong)attempt * 7919UL) };
                    if (pair is { } p)
                    {
                        tried = PairingPlayer.Deploy(tried, map, content, p.A, p.B);
                    }

                    var start = tried.Begin(map, content);
                    var tally = new Dictionary<string, WeaponMix>(StringComparer.Ordinal);
                    IPlayer player = pair is { } q ? new PairingPlayer(q.A, q.B) : even ? new EvenPlayer() : new HeuristicPlayer();
                    var end = Fight(start, content, seed, number, tally, player);
                    if (end.Outcome.Result == BattleResult.Won)
                    {
                        handed += player is EvenPlayer chair ? chair.Handed : 0;
                        won = end;
                        struck = tally;
                        camp = Read(start, end, content);
                    }
                }

                if (won is null)
                {
                    lost = number;
                    break;
                }

                foreach (var (classId, mix) in struck!)
                {
                    weapons[classId] = weapons.GetValueOrDefault(classId, WeaponMix.Zero).Plus(mix);
                }

                record = record.AfterBattle(won, content);
                var company = record.Present(content);
                var members = company.Select(u => Member.Of(u, content)).ToList();
                maps.Add((number, company.Select(u => u.Level).ToList(), members.Count(m => m.Ready), camp!));
                rapport.Add(record.Rapport.ToList());
                companies.Add(members);
            }

            runs.Add(new Run(maps, lost) { Weapons = weapons, Rapport = rapport, Companies = companies, Handed = handed });
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

    /// <summary>
    /// The requirement keys the closest advanced form refuses <paramref name="unit"/> (issue 1150), read as
    /// <see cref="Ready"/> reads a form: in that form's base. The form with the fewest refusals wins, content
    /// order breaking ties; empty when the content holds no advanced form.
    /// </summary>
    public static IReadOnlyList<string> Closest(Unit unit, GameContent content) =>
        content.Classes.Values
            .Where(f => f.Advances is not null)
            .Select(f => Certifications.Check(unit with { ClassId = f.Advances!.Id }, f, null, CampaignRecord.IsCaptain(unit, content)).Select(r => r.Requirement).ToList())
            .OrderBy(r => r.Count)
            .FirstOrDefault() ?? [];

    private static BattleState Fight(BattleState state, GameContent content, int seed, int number, Dictionary<string, WeaponMix> weapons, IPlayer player)
    {
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

                foreach (var (_, classId, type, counter, _) in WeaponMix.Strikes(state, content, command, result.Events))
                {
                    weapons[classId] = weapons.GetValueOrDefault(classId, WeaponMix.Zero).With(type, counter);
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

    /// <summary>
    /// The printed table: per map, over the runs that won it, the company at or above the threshold, the highest level, the third highest and the levels the whole company has gained, each as p25 p50 p75;
    /// then the map's camp line (issue 738): its enemy level, the deployed units' median and lowest level as the battle began, and the captain's share of the EXP the company kept from it, each as p50; last, the weapons each class attacked and countered with (issue 746).
    /// </summary>
    public static IEnumerable<string> Lines(GameContent content, IReadOnlyList<Run> runs)
    {
        yield return $"levels: {runs.Count} runs, the heuristic player through the campaign, never promoting; at {Threshold}+ is the second tier's level; each map's camp line is its won battle as it began";
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
            var camps = won.Select(m => m.Camp).ToList();
            var enemy = camps.Select(c => c.EnemyLevel).ToList();
            var median = camps.Select(c => Median(c.Deployed)).ToList();
            var lowest = camps.Select(c => c.Deployed.Count == 0 ? 0 : c.Deployed.Min()).ToList();
            var topAtCamp = camps.Select(c => c.Deployed.Count == 0 ? 0 : c.Deployed.Max()).ToList();
            var shares = camps.Select(CaptainShare).OfType<int>().ToList();
            var share = shares.Count == 0 ? "none earned" : $"{Percentile(shares, 0.5)}%";
            yield return $"    camp {id}: enemy {Percentile(enemy, 0.5)}, deployed top p50 {Percentile(topAtCamp, 0.5)}, median p50 {Percentile(median, 0.5)}, lowest p50 {Percentile(lowest, 0.5)}, captain share p50 {share}";
            if (MedianLine(id, Companies(runs, number)) is { } line)
            {
                yield return line;
            }
        }

        if (FirstCertified(runs) is { } first)
        {
            yield return first;
        }

        var byClass = runs.SelectMany(r => r.Weapons).GroupBy(kv => kv.Key, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key} [{g.Aggregate(WeaponMix.Zero, (sum, kv) => sum.Plus(kv.Value))}]").ToList();
        yield return $"  weapons by class over won battles, attacks/counters: {(byClass.Count == 0 ? "none" : string.Join(", ", byClass))}";

        foreach (var lostOn in runs.Where(r => r.LostOn is not null).GroupBy(r => r.LostOn!.Value).OrderBy(g => g.Key))
        {
            yield return $"  no try won map {lostOn.Key}: {lostOn.Count()}";
        }
    }

    /// <summary>The first map the even read covers (issue 1150): every map after map 5.</summary>
    public const int EvenFrom = 6;

    /// <summary>The map whose company the verdict reads (round 387): the company standing after map 8.</summary>
    public const int VerdictMap = 8;

    /// <summary>
    /// The even chair's read (issue 1150): per map from <see cref="EvenFrom"/>, over the companies standing
    /// after it, the non-captains at L7, L6 and L5 with rank C in the main weapon, the level half and the rank
    /// half alone, and those meeting a form, each as p50 p75; then, over the non-captains at L7 and rank C who
    /// meet no form, what the closest form refuses; last, round 387's verdict from the map-8 p50 (<see cref="Verdict"/>).
    /// </summary>
    public static IEnumerable<string> EvenLines(GameContent content, IReadOnlyList<Run> runs)
    {
        yield return $"levels --even: {runs.Count} runs, the even-company chair (issue 1150): a kill the heuristic plans goes to the lowest level, then the fewest main-weapon rank points, that takes it from a tile the heuristic accepts at no lower kill chance and no more exposure; kills handed {runs.Sum(r => r.Handed)}";
        foreach (var number in runs.SelectMany(r => r.Maps.Select(m => m.Map)).Distinct().Where(n => n >= EvenFrom).Order())
        {
            var companies = Companies(runs, number).Select(c => c.Where(m => !m.Captain).ToList()).ToList();
            var id = content.Campaign.Maps[number - 1].MapId;
            string Pair(Func<Member, bool> p)
            {
                var counts = companies.Select(c => c.Count(p)).ToList();
                return $"p50 {Percentile(counts, 0.5)} p75 {Percentile(counts, 0.75)}";
            }

            yield return $"  map {number} {id}: companies {companies.Count}, non-captains at L7+C {Pair(m => m.AtLevel(7) && m.AtRank)}, L6+C {Pair(m => m.AtLevel(6) && m.AtRank)}, L5+C {Pair(m => m.AtLevel(5) && m.AtRank)}; L7 alone {Pair(m => m.AtLevel(Threshold))}, rank C alone {Pair(m => m.AtRank)}; meets a form {Pair(m => m.Ready)}";
            var refused = companies.SelectMany(c => c).Where(m => m.AtBar && !m.Ready).ToList();
            var keys = refused.SelectMany(m => m.Refused).GroupBy(k => k, StringComparer.Ordinal).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => $"{g.Key} {g.Count()}").ToList();
            yield return $"    at L7+C meeting no form: {refused.Count}{(keys.Count == 0 ? "" : $", the closest form refuses {string.Join(", ", keys)}")}";
        }

        yield return Verdict(Companies(runs, VerdictMap));
    }

    /// <summary>
    /// Round 387's verdict ladder read on the companies standing after map 8: the p50 count of non-captains
    /// at L7 with rank C keeps the bar; failing that L6, then L5; none at L5 sends the curve back to the Table.
    /// </summary>
    public static string Verdict(IReadOnlyList<IReadOnlyList<Member>> companies)
    {
        int P50(int level) => Percentile(companies.Select(c => c.Count(m => !m.Captain && m.AtLevel(level) && m.AtRank)), 0.5);
        var head = $"  verdict (round 387, after map {VerdictMap}, {companies.Count} companies): ";
        return companies.Count == 0 ? head + "no company reached it"
            : P50(7) >= 1 ? head + $"p50 {P50(7)} non-captains at L7+C; the bar stands"
            : P50(6) >= 1 ? head + $"p50 0 at L7+C, {P50(6)} at L6+C; the level half drops to L6, rank C kept"
            : P50(5) >= 1 ? head + $"p50 0 at L6+C, {P50(5)} at L5+C; the level half drops to L5, rank C kept"
            : head + "p50 0 at L5+C; the bar is not the lever, the curve is (back to the Table)";
    }

    /// <summary>The company standing after map <paramref name="number"/> in every run that won it and recorded one (issue 1135).</summary>
    public static IReadOnlyList<IReadOnlyList<Member>> Companies(IReadOnlyList<Run> runs, int number) =>
        runs.SelectMany(r => r.Maps.Select((m, i) => (m.Map, Company: i < r.Companies.Count ? r.Companies[i] : null)))
            .Where(x => x.Map == number && x.Company is not null).Select(x => x.Company!).ToList();

    /// <summary>
    /// The median unit's line for one map (issue 1135, round 381), or null when no run recorded a company:
    /// over the companies standing after it, the median unit's level and main-weapon rank points, each as
    /// p25 p50 p75, and how many living units are at the tier-2 bar (<see cref="Member.AtBar"/>), at p50 and p75.
    /// </summary>
    public static string? MedianLine(string id, IReadOnlyList<IReadOnlyList<Member>> companies)
    {
        if (companies.Count == 0)
        {
            return null;
        }

        var level = companies.Select(c => Median(c.Select(m => m.Level).ToList())).ToList();
        var rank = companies.Select(c => Median(c.Select(m => m.MainRank).ToList())).ToList();
        var bar = companies.Select(c => c.Count(m => m.AtBar)).ToList();
        return $"    median {id}: unit level {Band(level)}, main-weapon rank points {Band(rank)}, at L{Threshold} and rank C p50 {Percentile(bar, 0.5)} p75 {Percentile(bar, 0.75)}";
    }

    /// <summary>The first map after which a unit other than the captain meets some advanced form (<see cref="Member.Ready"/>), or null if none ever does (issue 1135).</summary>
    public static int? FirstCertifiedMap(Run run)
    {
        for (var i = 0; i < run.Maps.Count && i < run.Companies.Count; i++)
        {
            if (run.Companies[i].Any(u => u.Ready && !u.Captain))
            {
                return run.Maps[i].Map;
            }
        }

        return null;
    }

    /// <summary>
    /// The camp at which the first unit other than the captain meets some advanced form, over the runs
    /// (issue 1135): its p50 and p75 over the runs where one did, and how many runs saw none. Null with no runs.
    /// </summary>
    public static string? FirstCertified(IReadOnlyList<Run> runs)
    {
        if (runs.Count == 0)
        {
            return null;
        }

        var maps = runs.Select(FirstCertifiedMap).OfType<int>().ToList();
        var none = runs.Count - maps.Count;
        return maps.Count == 0
            ? $"  first non-captain to meet a form: none in {runs.Count} runs"
            : $"  first non-captain to meet a form: after map p50 {Percentile(maps, 0.5)} p75 {Percentile(maps, 0.75)} over {maps.Count} runs; none in {none} of {runs.Count}";
    }

    /// <summary>
    /// The company the heuristic's own campaign brings to a map's camp, slot by slot (issue 764): over
    /// <paramref name="camps"/>, the won battles of that map as they began, the deployed levels sorted
    /// highest first, and for each of the first <paramref name="slots"/> places the p50 over the camps
    /// that deployed that many units; a place no camp filled reads 0.
    /// </summary>
    public static IReadOnlyList<int> SlotLevels(IReadOnlyList<Camp> camps, int slots)
    {
        var sorted = camps.Select(c => c.Deployed.OrderDescending().ToList()).ToList();
        return Enumerable.Range(0, slots).Select(k => Percentile(sorted.Where(l => l.Count > k).Select(l => l[k]), 0.5)).ToList();
    }

    /// <summary>
    /// The same total levels as <paramref name="levels"/>, spread as evenly as the places allow, the
    /// remainder one level each to the top places (issue 764's <c>carried, spread</c>).
    /// </summary>
    public static IReadOnlyList<int> Spread(IReadOnlyList<int> levels)
    {
        if (levels.Count == 0)
        {
            return [];
        }

        var total = levels.Sum();
        return Enumerable.Range(0, levels.Count).Select(k => total / levels.Count + (k < total % levels.Count ? 1 : 0)).ToList();
    }

    internal static string Band(IReadOnlyList<int> values) =>
        $"p25 {Percentile(values, 0.25)} p50 {Percentile(values, 0.5)} p75 {Percentile(values, 0.75)}";

    internal static int Percentile(IEnumerable<int> values, double p)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, (int)Math.Floor(p * sorted.Count))];
    }
}
