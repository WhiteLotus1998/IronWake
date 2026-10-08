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

    /// <summary>
    /// The door's points gate in the main weapon (classes.json, every level-7 door's <c>certification.points</c>;
    /// 50 since round 394, issue 1174), between D's 30 and C's 80.
    /// </summary>
    public const int Gate = 50;

    /// <summary>The margin the Recalling player's bound must clear the gate by at p50 (round 394).</summary>
    public const int GateMargin = 5;

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

        /// <summary>
        /// The focused chair's price per won map (issue 1157), one entry per <see cref="Maps"/> entry in the same
        /// order: kills handed to <see cref="FocusedPlayer.Fed"/>, kills given up (<see cref="FocusedPlayer.GivenUp"/>),
        /// the HP he lost in enemy phases and his falls in them; empty for any other player.
        /// </summary>
        public IReadOnlyList<Price> Prices { get; init; } = [];

        /// <summary>
        /// <see cref="FocusedPlayer.Fed"/>'s EXP per won map (issue 1181, round 396), one entry per <see cref="Maps"/>
        /// entry in the same order, on every player.
        /// </summary>
        public IReadOnlyList<FedExp> Exp { get; init; } = [];

        /// <summary>The drills the heuristic camp fought after each won map under a yard arm (issue 1331, <see cref="YardRun"/>); empty without one.</summary>
        public IReadOnlyList<YardRun.Drill> Drills { get; init; } = [];
    }

    /// <summary>
    /// The fed unit's EXP on one won map (issue 1181, round 396): whether he was deployed in the won battle, the EXP
    /// the record kept from it (counted from level 1, so a level gained counts whole; a fall keeps none), and the
    /// EXP the camp's drill zeroed after it (<see cref="CampaignRecord.Drill"/>: his EXP after the battle less what
    /// the drill left him; 0 since issue 1184, which keeps EXP through the drill).
    /// </summary>
    public sealed record FedExp(bool Deployed, int Earned, int Zeroed)
    {
        /// <summary>A map he was not in the company for.</summary>
        public static FedExp Absent { get; } = new(false, 0, 0);

        /// <summary>
        /// The reading from the record before the battle, the record after it (<see cref="CampaignRecord.Fought"/>, before
        /// the drill) and the units deployed into it.
        /// </summary>
        public static FedExp Of(CampaignRecord before, CampaignRecord fought, IEnumerable<string> deployed, GameContent content)
        {
            if (before.Roster.FirstOrDefault(u => u.Id == FocusedPlayer.Fed) is not { } prior
                || fought.Roster.FirstOrDefault(u => u.Id == FocusedPlayer.Fed) is not { } after)
            {
                return Absent;
            }

            var zeroed = after.Exp - (fought.Drill(content).Find(FocusedPlayer.Fed)?.Exp ?? after.Exp);
            return new(deployed.Contains(FocusedPlayer.Fed), TotalExp(after) - TotalExp(prior), zeroed);
        }
    }

    /// <summary>The focused chair's price on one won map (issue 1157): see <see cref="Run.Prices"/>.</summary>
    public sealed record Price(int Handed, int GivenUp, int HpLost, int Falls)
    {
        /// <summary>The kills offered to the fed unit and refused on the map, by the first clause that refused each (<see cref="FocusedPlayer.Refused"/>).</summary>
        public IReadOnlyDictionary<FocusedPlayer.Refusal, int> Refused { get; init; } = new Dictionary<FocusedPlayer.Refusal, int>();

        /// <summary>The strikes that do not kill on a hit the chair handed the fed unit on the map (<see cref="FocusedPlayer.Strikes"/>, issue 1167).</summary>
        public int Strikes { get; init; }

        /// <summary>Every attack the fed unit made on the map, whoever planned it (<see cref="FocusedPlayer.Combats"/>, issue 1167).</summary>
        public int Combats { get; init; }

        /// <summary>The chips the chair played on the map (<see cref="FocusedPlayer.Chips"/>, issue 1178).</summary>
        public int Chips { get; init; }

        /// <summary>Where the fed unit's main-weapon rank points went on the map (issue 1170, <see cref="RankTrace"/>).</summary>
        public RankTrace Rank { get; init; } = RankTrace.Benched;
    }

    /// <summary>
    /// The fed unit's main-weapon rank points on one won map (issue 1170, round 393): whether he was deployed,
    /// the combats he was in on each phase and those in which he struck (rank pays only a unit that struck,
    /// DESIGN 126), the points he earned in the won battle, those the record kept after it (a unit that fell
    /// comes back as he began the battle, so a fall loses the map's points), whether he fell, the points he
    /// earned in the map's lost tries, and the change at the camp before the battle (none is expected).
    /// </summary>
    public sealed record RankTrace(bool Deployed, int PlayerCombats, int PlayerStruck, int EnemyCombats, int EnemyStruck, int Earned, bool Fell)
    {
        /// <summary>A map the fed unit sat out.</summary>
        public static RankTrace Benched { get; } = new(false, 0, 0, 0, 0, 0, false);

        /// <summary>The points the record kept from the battle: his main-weapon points after it less those before it.</summary>
        public int Kept { get; init; }

        /// <summary>The points he earned in the map's lost tries, which no record keeps.</summary>
        public int LostTries { get; init; }

        /// <summary>His main-weapon points before the battle less those after the previous map (0 before map 1).</summary>
        public int Camp { get; init; }

        /// <summary>The won battle's points the record did not keep because he fell in it.</summary>
        public int LostToFall => Fell ? Earned - Kept : 0;

        /// <summary>Points neither kept nor lost to a fall: 0 when the in-battle count reconciles with the record.</summary>
        public int Unexplained => Earned - Kept - LostToFall;

        /// <summary>The combats he was in on either phase.</summary>
        public int Combats => PlayerCombats + EnemyCombats;

        /// <summary>The combats he was in and struck no blow in, on either phase.</summary>
        public int Strikeless => Combats - PlayerStruck - EnemyStruck;
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

        /// <summary>The unit's id (issue 1157, so the focused chair can read its fed unit).</summary>
        public string Id { get; init; } = "";

        /// <summary>The bar's level half alone, read at <paramref name="level"/> or above.</summary>
        public bool AtLevel(int level) => Level >= level;

        /// <summary>The tier-2 bar's rank half alone: rank C or above in the main weapon.</summary>
        public bool AtRank => MainRank >= WeaponRanks.Threshold(WeaponRank.C);

        /// <summary>The door's points gate alone (issue 1174, round 394): <see cref="Gate"/> or more in the main weapon.</summary>
        public bool AtGate => MainRank >= Gate;

        /// <summary>The door as round 394 reads it: <see cref="Threshold"/> and the points gate in the main weapon.</summary>
        public bool AtDoor => AtLevel(Threshold) && AtGate;

        /// <summary>The <see cref="Member"/> reading of <paramref name="unit"/>.</summary>
        public static Member Of(Unit unit, GameContent content)
        {
            var ready = LevelRun.Ready(unit, content);
            return new(unit.Level, unit.Skill.Points(MainType(unit, content)), ready, CampaignRecord.IsCaptain(unit, content))
            {
                Refused = ready ? [] : Closest(unit, content),
                Id = unit.Id,
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
    /// <remarks>With <paramref name="even"/> set the player is <see cref="EvenPlayer"/>, the even-company chair (issue 1150); with <paramref name="focused"/> set it is <see cref="FocusedPlayer"/> under that guard (issue 1157).</remarks>
    /// <remarks>With <paramref name="firstSeed"/> set the seeds are <paramref name="firstSeed"/> onward, <paramref name="seeds"/> of them, so a read can be split across threads (issue 1170).</remarks>
    /// <remarks>Under a <paramref name="yard"/> arm other than off, each camp after the levy drill fights the heuristic's yard drill (issue 1331, <see cref="YardRun.Camp"/>).</remarks>
    public static IReadOnlyList<Run> Measure(string contentRoot, GameContent content, int seeds, (string A, string B)? pair = null, bool even = false, FocusedPlayer.Guard? focused = null, int firstSeed = 1, YardRun.Arm yard = YardRun.Arm.Off)
    {
        var runs = new List<Run>();
        for (var seed = firstSeed; seed < firstSeed + seeds; seed++)
        {
            var record = CampaignRecord.Start(content, (ulong)seed, permadeath: false);
            var maps = new List<(int, IReadOnlyList<int>, int, Camp)>();
            var weapons = new Dictionary<string, WeaponMix>(StringComparer.Ordinal);
            var rapport = new List<IReadOnlyList<Rapport>>();
            var companies = new List<IReadOnlyList<Member>>();
            var handed = 0;
            var prices = new List<Price>();
            var exp = new List<FedExp>();
            var drills = new List<YardRun.Drill>();
            int? lost = null;
            int? lastPoints = null;
            while (!record.IsFinished(content))
            {
                record = SimPick.Made(record, content);
                var priorPoints = FedPoints(record.Present(content), content);
                var lostTries = 0;
                var number = record.MapIndex + 1;
                var map = MapFiles.Load(MapFiles.CampaignPath(contentRoot, content, record.NextMap(content).MapId), content);
                BattleState? won = null;
                Camp? camp = null;
                Dictionary<string, WeaponMix>? struck = null;
                Price? price = null;
                IReadOnlyList<string> deployed = [];
                for (var attempt = 0; attempt < HeirloomRun.Attempts && won is null; attempt++)
                {
                    var tried = record with { Seed = unchecked(record.Seed + (ulong)attempt * 7919UL) };
                    if (pair is { } p)
                    {
                        tried = PairingPlayer.Deploy(tried, map, content, p.A, p.B);
                    }

                    var start = tried.Begin(map, content);
                    var tally = new Dictionary<string, WeaponMix>(StringComparer.Ordinal);
                    IPlayer player = pair is { } q ? new PairingPlayer(q.A, q.B) : focused is { } guard ? new FocusedPlayer(guard) : even ? new EvenPlayer() : new HeuristicPlayer();
                    var (end, hpLost, falls, trace) = Fight(start, content, seed, number, tally, player);
                    if (end.Outcome.Result != BattleResult.Won)
                    {
                        lostTries += trace.Earned;
                    }
                    else
                    {
                        handed += player switch { EvenPlayer chair => chair.Handed, FocusedPlayer chair => chair.Handed, _ => 0 };
                        price = player is FocusedPlayer fed ? new Price(fed.Handed, fed.GivenUp, hpLost, falls) { Refused = fed.Refused.ToDictionary(), Strikes = fed.Strikes, Combats = fed.Combats, Chips = fed.Chips, Rank = trace } : null;
                        won = end;
                        struck = tally;
                        camp = Read(start, end, content);
                        deployed = start.UnitsOf(Side.Player).Select(u => u.Id).ToList();
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

                var fought = record.Fought(won, content);
                exp.Add(FedExp.Of(record, fought, deployed, content));
                record = fought.Drill(content);
                if (yard != YardRun.Arm.Off && !record.IsFinished(content))
                {
                    var (drilled, drill) = YardRun.Camp(record, contentRoot, content, pull: yard == YardRun.Arm.On);
                    record = drilled;
                    if (drill is not null)
                    {
                        drills.Add(drill);
                    }
                }

                var company = record.Present(content);
                var afterPoints = FedPoints(company, content);
                if (price is not null)
                {
                    price = price with
                    {
                        Rank = price.Rank with
                        {
                            Kept = (afterPoints ?? 0) - (priorPoints ?? 0),
                            LostTries = lostTries,
                            Camp = lastPoints is { } last && priorPoints is { } prior ? prior - last : 0,
                        },
                    };
                }

                lastPoints = afterPoints;
                var members = company.Select(u => Member.Of(u, content)).ToList();
                maps.Add((number, company.Select(u => u.Level).ToList(), members.Count(m => m.Ready), camp!));
                rapport.Add(record.Rapport.ToList());
                companies.Add(members);
                if (price is not null)
                {
                    prices.Add(price);
                }
            }

            runs.Add(new Run(maps, lost) { Weapons = weapons, Rapport = rapport, Companies = companies, Handed = handed, Prices = prices, Exp = exp, Drills = drills });
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

    /// <summary>The fed unit's main-weapon rank points in <paramref name="company"/>, or null when he is not in it (issue 1170).</summary>
    private static int? FedPoints(IEnumerable<Unit> company, GameContent content) =>
        company.FirstOrDefault(u => u.Id == FocusedPlayer.Fed) is { } fed ? fed.Skill.Points(MainType(fed, content)) : null;

    /// <summary>
    /// One battle to its end, and the HP <see cref="FocusedPlayer.Fed"/> lost in enemy phases and how often he fell in them (issue 1157),
    /// and his rank trace for the battle (issue 1170, <see cref="RankTrace"/>; its record-side fields are left for the caller).
    /// </summary>
    private static (BattleState End, int HpLost, int Falls, RankTrace Trace) Fight(BattleState state, GameContent content, int seed, int number, Dictionary<string, WeaponMix> weapons, IPlayer player)
    {
        var hpLost = 0;
        var falls = 0;
        var trace = RankTrace.Benched;
        var main = state.Find(FocusedPlayer.Fed) is { Side: Side.Player } deployed ? MainType(deployed.Unit, content) : (WeaponType?)null;
        if (main is not null)
        {
            trace = trace with { Deployed = true };
        }

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

                if (state.Phase == Side.Enemy && state.Find(FocusedPlayer.Fed) is { Side: Side.Player } before)
                {
                    var after = result.Next.Find(FocusedPlayer.Fed) is { Hp: > 0 } standing ? standing.Hp : 0;
                    hpLost += Math.Max(0, before.Hp - after);
                    falls += after == 0 ? 1 : 0;
                }

                if (main is { } mainType)
                {
                    trace = Traced(trace, state, result, mainType);
                }

                state = result.Next;
                if (state.Outcome.IsOver)
                {
                    break;
                }
            }
        }

        return (state, hpLost, falls, trace);
    }

    /// <summary>
    /// <paramref name="trace"/> after one accepted command (issue 1170): each combat the fed unit was in, by phase,
    /// and whether he struck in it, the main-weapon points he gained, and whether he fell.
    /// </summary>
    private static RankTrace Traced(RankTrace trace, BattleState before, ApplyResult result, WeaponType main)
    {
        if (before.Find(FocusedPlayer.Fed) is not { Side: Side.Player, Hp: > 0 } fed)
        {
            return trace;
        }

        foreach (var fought in result.Events.OfType<CombatFought>().Where(f => f.AttackerId == FocusedPlayer.Fed || f.TargetId == FocusedPlayer.Fed))
        {
            var struck = fought.Strikes.Any(s => s.AttackerId == FocusedPlayer.Fed) ? 1 : 0;
            trace = fought.Phase == Side.Player
                ? trace with { PlayerCombats = trace.PlayerCombats + 1, PlayerStruck = trace.PlayerStruck + struck }
                : trace with { EnemyCombats = trace.EnemyCombats + 1, EnemyStruck = trace.EnemyStruck + struck };
        }

        if (result.Next.Find(FocusedPlayer.Fed) is { Hp: > 0 } standing)
        {
            return trace with { Earned = trace.Earned + Math.Max(0, standing.Unit.Skill.Points(main) - fed.Unit.Skill.Points(main)) };
        }

        return result.Events.OfType<UnitDied>().Any(d => d.UnitId == FocusedPlayer.Fed) ? trace with { Fell = true } : trace;
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
        foreach (var line in ChairLines(content, runs))
        {
            yield return line;
        }

        yield return Verdict(Companies(runs, VerdictMap));
    }

    /// <summary>
    /// The focused chair's read (issue 1157, round 389): four chairs over the same seeds, the even chair
    /// (the ceiling), the focused chair's guarded line, its paying line and its striking line (issue 1167,
    /// round 392), each with its kills handed and <see cref="ChairLines"/>; the paying and striking lines
    /// also print their price through map 8 (<see cref="PriceLine"/>); last, the bar's read on the striking
    /// line (<see cref="FocusedVerdict"/>).
    /// </summary>
    public static IEnumerable<string> FocusedLines(GameContent content, IReadOnlyList<Run> even, IReadOnlyList<Run> guarded, IReadOnlyList<Run> paying, IReadOnlyList<Run> striking)
    {
        yield return $"levels --focused: {striking.Count} runs, four chairs over the same seeds, retries and campaign; the fed unit is {FocusedPlayer.Fed}";
        foreach (var (label, runs) in new[]
        {
            ("even (the ceiling): the lowest level that takes a kill at no lower kill chance and no more exposure", even),
            ("guarded: every kill " + FocusedPlayer.Fed + " can take at no lower kill chance and no more exposure", guarded),
            ($"paying: every kill {FocusedPlayer.Fed} can take that still kills on a hit, at most {(int)Math.Round(FocusedPlayer.PayingGap * 100)} points of kill chance below the planned attacker's, at most {FocusedPlayer.PayingReach} more enemy in reach, no forecast death", paying),
            ($"striking: the paying line, and a strike by {FocusedPlayer.Fed} on any target another unit is planned to attack, kill or not, at most {FocusedPlayer.PayingReach} more enemy in reach, no forecast death", striking),
        })
        {
            var refused = runs.SelectMany(r => r.Prices).SelectMany(p => p.Refused).GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));
            var why = runs.Any(r => r.Prices.Count > 0)
                ? $", offered and refused: no tile {refused.GetValueOrDefault(FocusedPlayer.Refusal.NoTile)}, kill chance {refused.GetValueOrDefault(FocusedPlayer.Refusal.Chance)}, exposure {refused.GetValueOrDefault(FocusedPlayer.Refusal.Exposure)}, forecast death {refused.GetValueOrDefault(FocusedPlayer.Refusal.ForecastDeath)}"
                : "";
            yield return $"  {label}; kills handed {runs.Sum(r => r.Handed)}{why}";
            foreach (var line in ChairLines(content, runs))
            {
                yield return "  " + line;
            }
        }

        yield return PriceLine(paying);
        yield return HandedSplit(paying, "paying");
        yield return PriceLine(striking, "striking");
        yield return HandedSplit(striking, "striking");
        yield return FocusedVerdict(Companies(even, VerdictMap), Companies(striking, VerdictMap));
    }

    /// <summary>
    /// The handed count's split (issue 1177, round 395): the kills a focused line handed the fed unit over every
    /// won map of every run (the count its header prints), split into those through map <see cref="VerdictMap"/>
    /// on the runs that won it (the price line's total) and the rest (maps after it, or runs lost before it);
    /// it reconciles when the per-map prices sum to the run's count on every run.
    /// </summary>
    public static string HandedSplit(IReadOnlyList<Run> runs, string line)
    {
        var all = runs.Sum(r => r.Handed);
        var reached = runs.Select(r => (Run: r, At: r.Maps.ToList().FindIndex(m => m.Map == VerdictMap))).Where(x => x.At >= 0 && x.At < x.Run.Companies.Count && x.At < x.Run.Prices.Count).ToList();
        var through = reached.Sum(x => x.Run.Prices.Take(x.At + 1).Sum(p => p.Handed));
        var reconciles = runs.All(r => r.Prices.Sum(p => p.Handed) == r.Handed);
        return $"  kills handed ({line}): {all} in all, {through} through map {VerdictMap} on the {reached.Count} runs that won it, {all - through} on later maps or runs lost before it; "
            + (reconciles ? "the per-map prices sum to each run's count" : "the per-map prices do not sum to each run's count (a bug)");
    }

    /// <summary>
    /// A focused line's price through map <see cref="VerdictMap"/> (issue 1157), over the runs that won it:
    /// the captain's level after it, the fed unit's level and main-weapon rank points after it, the kills
    /// handed and given up (each p50 p75 per run, then its total over those runs; issue 1177), the HP the fed unit
    /// lost in enemy phases (p50 p75 per run), and his falls in all.
    /// The striking line (issue 1167) adds the fed unit's combats per map and the strikes the chair handed him
    /// per map, each p50 p75 over every won map through it; the chipping line (issue 1178) adds its chips per map.
    /// </summary>
    public static string PriceLine(IReadOnlyList<Run> runs, string line = "paying")
    {
        var reached = runs.Select(r => (Run: r, At: r.Maps.ToList().FindIndex(m => m.Map == VerdictMap))).Where(x => x.At >= 0 && x.At < x.Run.Companies.Count && x.At < x.Run.Prices.Count).ToList();
        if (reached.Count == 0)
        {
            return $"  price ({line}, through map {VerdictMap}): no run won it";
        }

        string Pair(IEnumerable<int> values)
        {
            var list = values.ToList();
            return $"p50 {Percentile(list, 0.5)} p75 {Percentile(list, 0.75)}";
        }

        var through = reached.Select(x => x.Run.Prices.Take(x.At + 1).ToList()).ToList();
        var after = reached.Select(x => x.Run.Companies[x.At]).ToList();
        var captain = after.Select(c => c.FirstOrDefault(m => m.Captain)?.Level ?? 0);
        var fed = after.Select(c => c.FirstOrDefault(m => m.Id == FocusedPlayer.Fed)).ToList();
        var perMap = line is "striking" or "chipping"
            ? $"; per map, combats {Pair(through.SelectMany(p => p.Select(x => x.Combats)))}, strikes handed {Pair(through.SelectMany(p => p.Select(x => x.Strikes)))}"
                + (line == "chipping" ? $", chips {Pair(through.SelectMany(p => p.Select(x => x.Chips)))}" : "")
            : "";
        return $"  price ({line}, through map {VerdictMap}, {reached.Count} runs): captain level {Pair(captain)}; {FocusedPlayer.Fed} level {Pair(fed.Select(m => m?.Level ?? 0))}, main-weapon rank points {Pair(fed.Select(m => m?.MainRank ?? 0))}; kills handed {Pair(through.Select(p => p.Sum(x => x.Handed)))} (total {through.Sum(p => p.Sum(x => x.Handed))}), given up {Pair(through.Select(p => p.Sum(x => x.GivenUp)))} (total {through.Sum(p => p.Sum(x => x.GivenUp))}); HP lost in enemy phases {Pair(through.Select(p => p.Sum(x => x.HpLost)))}, falls {through.Sum(p => p.Sum(x => x.Falls))}{perMap}";
    }

    /// <summary>
    /// The rank trace (issue 1170, round 393): over the runs that won map <see cref="VerdictMap"/>, per map
    /// through it, the runs that deployed the fed unit and, as means over those, his combats and those he struck
    /// in on each phase and the main-weapon points he earned, kept, lost to a fall and earned in lost tries;
    /// then per run (p50 p75) the same totals, his strikeless combats and maps benched, the points the record
    /// reads after map <see cref="VerdictMap"/> and the Recalling player's bound (those plus the points lost to
    /// a fall); then whether the in-battle count reconciles with the record, the sinks in points, and the read
    /// the Table agreed before the numbers (<see cref="TraceVerdict"/>).
    /// </summary>
    public static IEnumerable<string> RankTraceLines(GameContent content, IReadOnlyList<Run> runs)
    {
        var reached = runs.Select(r => (Run: r, At: r.Maps.ToList().FindIndex(m => m.Map == VerdictMap))).Where(x => x.At >= 0 && x.At < x.Run.Companies.Count && x.At < x.Run.Prices.Count).ToList();
        yield return $"levels --rank-trace: {runs.Count} runs, the striking chair (issue 1167); {FocusedPlayer.Fed}'s main-weapon rank points, where they go (issue 1170, round 393); C is {WeaponRanks.Threshold(WeaponRank.C)}, {WeaponRanks.PerCombat} a combat struck, {WeaponRanks.PerKill} a kill";
        if (reached.Count == 0)
        {
            yield return $"  no run won map {VerdictMap}";
            yield break;
        }

        static string Mean(IEnumerable<int> values)
        {
            var list = values.ToList();
            return list.Count == 0 ? "-" : (list.Sum() / (double)list.Count).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        static string Pair(IEnumerable<int> values)
        {
            var list = values.ToList();
            return $"p50 {Percentile(list, 0.5)} p75 {Percentile(list, 0.75)}";
        }

        var through = reached.Select(x => x.Run.Prices.Take(x.At + 1).Select(p => p.Rank).ToList()).ToList();
        yield return $"  through map {VerdictMap}, {reached.Count} runs; per map, means over the runs that deployed him:";
        for (var i = 0; i <= reached.Min(x => x.At); i++)
        {
            var number = reached[0].Run.Maps[i].Map;
            var id = content.Campaign.Maps[number - 1].MapId;
            var maps = through.Select(t => t[i]).ToList();
            var deployed = maps.Where(m => m.Deployed).ToList();
            yield return $"    map {number} {id}: deployed {deployed.Count} of {maps.Count}; combats player {Mean(deployed.Select(m => m.PlayerCombats))} (struck {Mean(deployed.Select(m => m.PlayerStruck))}), enemy {Mean(deployed.Select(m => m.EnemyCombats))} (struck {Mean(deployed.Select(m => m.EnemyStruck))}); points earned {Mean(deployed.Select(m => m.Earned))}, kept {Mean(deployed.Select(m => m.Kept))}, lost to a fall {Mean(deployed.Select(m => m.LostToFall))} (fell {deployed.Count(m => m.Fell)}), in lost tries {Mean(deployed.Select(m => m.LostTries))}";
        }

        var final = reached.Select(x => x.Run.Companies[x.At].FirstOrDefault(m => m.Id == FocusedPlayer.Fed)?.MainRank ?? 0).ToList();
        var fall = through.Select(t => t.Sum(m => m.LostToFall)).ToList();
        var bound = final.Zip(fall, (f, l) => f + l).ToList();
        yield return $"  per run: maps deployed {Pair(through.Select(t => t.Count(m => m.Deployed)))}, benched {Pair(through.Select(t => t.Count(m => !m.Deployed)))}; combats {Pair(through.Select(t => t.Sum(m => m.Combats)))}, struck {Pair(through.Select(t => t.Sum(m => m.PlayerStruck + m.EnemyStruck)))}, strikeless {Pair(through.Select(t => t.Sum(m => m.Strikeless)))}";
        yield return $"  per run: points earned {Pair(through.Select(t => t.Sum(m => m.Earned)))}, kept {Pair(through.Select(t => t.Sum(m => m.Kept)))}, lost to a fall {Pair(fall)}, in lost tries {Pair(through.Select(t => t.Sum(m => m.LostTries)))}, at camps {Pair(through.Select(t => t.Sum(m => m.Camp)))}";
        yield return $"  after map {VerdictMap}: the record reads {Pair(final)}; the Recalling player's bound (plus the points lost to a fall) {Pair(bound)}";
        var maps8 = through.SelectMany(t => t).ToList();
        var unexplained = maps8.Count(m => m.Unexplained != 0);
        var camps = maps8.Count(m => m.Camp != 0);
        yield return $"  reconciles: in-battle points against the record on {maps8.Count - unexplained} of {maps8.Count} maps (unexplained {maps8.Sum(m => m.Unexplained)} points), camps that moved his points {camps}";
        var deployedMaps = maps8.Where(m => m.Deployed).ToList();
        var perDeployed = deployedMaps.Count == 0 ? 0.0 : deployedMaps.Sum(m => m.Earned) / (double)deployedMaps.Count;
        var sinks = new (string Name, double Points)[]
        {
            ("lost to a fall", fall.Average()),
            ($"strikeless combats at {WeaponRanks.PerCombat}", through.Average(t => t.Sum(m => m.Strikeless)) * WeaponRanks.PerCombat),
            ("maps benched at his mean earned on a deployed map", through.Average(t => t.Count(m => !m.Deployed)) * perDeployed),
        };
        yield return "  sinks, mean points a run: " + string.Join(", ", sinks.Select(k => $"{k.Name} {k.Points.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}"));
        yield return TraceVerdict(Percentile(bound, 0.5), unexplained, sinks.MaxBy(k => k.Points).Name);
    }

    /// <summary>
    /// The door's points gate read (issue 1174, round 394): the striking chair and the even chair over the same
    /// seeds, read after map <see cref="VerdictMap"/>. The striking line prints the fed unit's level, the points the
    /// record keeps and the Recalling player's bound (kept plus the points lost to a fall); the even chair prints
    /// the non-captains at L7 and the gate and at L7 alone, its best non-captain's points, and the pick's level and
    /// points; last, the read the Table agreed before the numbers (<see cref="GateVerdict"/>).
    /// </summary>
    public static IEnumerable<string> GateLines(GameContent content, IReadOnlyList<Run> even, IReadOnlyList<Run> striking)
    {
        static string Pair(IEnumerable<int> values)
        {
            var list = values.ToList();
            return $"p50 {Percentile(list, 0.5)} p75 {Percentile(list, 0.75)}";
        }

        var pick = PickId(content);
        yield return $"levels --gate: {striking.Count} runs, the striking chair and the even chair over the same seeds (issue 1174, round 394); the door asks L{Threshold} and {Gate} main-weapon rank points (D is {WeaponRanks.Threshold(WeaponRank.D)}, C is {WeaponRanks.Threshold(WeaponRank.C)}); the pick is {pick}";
        yield return PriceLine(striking, "striking");
        yield return HandedSplit(striking, "striking");
        var reached = striking.Select(r => (Run: r, At: r.Maps.ToList().FindIndex(m => m.Map == VerdictMap))).Where(x => x.At >= 0 && x.At < x.Run.Companies.Count && x.At < x.Run.Prices.Count).ToList();
        var fed = reached.Select(x => x.Run.Companies[x.At].FirstOrDefault(m => m.Id == FocusedPlayer.Fed)).ToList();
        var kept = fed.Select(m => m?.MainRank ?? 0).ToList();
        var bound = kept.Zip(reached.Select(x => x.Run.Prices.Take(x.At + 1).Sum(p => p.Rank.LostToFall)), (k, l) => k + l).ToList();
        var level = fed.Select(m => m?.Level ?? 0).ToList();
        yield return $"  striking, after map {VerdictMap} ({reached.Count} runs): {FocusedPlayer.Fed} level {Pair(level)}; points kept {Pair(kept)}, the Recalling player's bound {Pair(bound)}; at L{Threshold} and the gate (kept) {fed.Count(m => m is { AtDoor: true })} of {fed.Count}";
        var evens = Companies(even, VerdictMap).Select(c => c.Where(m => !m.Captain).ToList()).ToList();
        var door = evens.Select(c => c.Count(m => m.AtDoor)).ToList();
        var alone = evens.Select(c => c.Count(m => m.AtLevel(Threshold))).ToList();
        var best = evens.Select(c => c.Select(m => m.MainRank).DefaultIfEmpty(0).Max()).ToList();
        var picks = evens.Select(c => c.FirstOrDefault(m => m.Id == pick)).Where(m => m is not null).Select(m => m!).ToList();
        yield return $"  even, after map {VerdictMap} ({evens.Count} companies): non-captains at L{Threshold} and the gate {Pair(door)}, at L{Threshold} alone {Pair(alone)}; the best non-captain's points {Pair(best)}";
        yield return $"  the pick on the even chair, after map {VerdictMap} ({picks.Count} companies with {pick}): level {Pair(picks.Select(m => m.Level))}, points {Pair(picks.Select(m => m.MainRank))}; at L{Threshold} and the gate {picks.Count(m => m.AtDoor)}";
        yield return GateVerdict(
            Percentile(bound, 0.5),
            Percentile(level, 0.5),
            Percentile(kept, 0.5),
            Percentile(door, 0.5),
            Percentile(alone, 0.5),
            picks.Count == 0 ? 0 : Percentile(picks.Select(m => m.Level), 0.5),
            picks.Count == 0 ? 0 : Percentile(picks.Select(m => m.MainRank), 0.5));
    }

    /// <summary>
    /// The chip's read (issue 1178, round 395): the striking line and the chipping line over the same seeds, each
    /// with its kills handed, chips and the kills offered and refused by reason over every won map; the chipping
    /// line's price through map <see cref="VerdictMap"/> and its handed split; the fed unit's level and points after
    /// it on the chipping line; his own points on the even chair; last, the read the Table agreed before the
    /// numbers (<see cref="ChipVerdict"/>).
    /// </summary>
    public static IEnumerable<string> ChipLines(IReadOnlyList<Run> even, IReadOnlyList<Run> striking, IReadOnlyList<Run> chipping)
    {
        static string Pair(IEnumerable<int> values)
        {
            var list = values.ToList();
            return $"p50 {Percentile(list, 0.5)} p75 {Percentile(list, 0.75)}";
        }

        static Dictionary<FocusedPlayer.Refusal, int> Refusals(IReadOnlyList<Run> runs) =>
            runs.SelectMany(r => r.Prices).SelectMany(p => p.Refused).GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

        yield return $"levels --chip: {chipping.Count} runs, the striking chair with and without the chip and the even chair over the same seeds (issue 1178, round 395); the fed unit is {FocusedPlayer.Fed}";
        foreach (var (label, runs) in new[] { ("striking", striking), ("chipping", chipping) })
        {
            var refused = Refusals(runs);
            yield return $"  {label}, every won map: kills handed {runs.Sum(r => r.Handed)}, chips {runs.Sum(r => r.Prices.Sum(p => p.Chips))}; offered and refused: no tile {refused.GetValueOrDefault(FocusedPlayer.Refusal.NoTile)}, kill chance {refused.GetValueOrDefault(FocusedPlayer.Refusal.Chance)}, exposure {refused.GetValueOrDefault(FocusedPlayer.Refusal.Exposure)}, forecast death {refused.GetValueOrDefault(FocusedPlayer.Refusal.ForecastDeath)}";
        }

        yield return PriceLine(chipping, "chipping");
        yield return HandedSplit(chipping, "chipping");
        var level = AfterVerdict(chipping).Select(m => m?.Level ?? 0).ToList();
        var points = AfterVerdict(chipping).Select(m => m?.MainRank ?? 0).ToList();
        var baseline = AfterVerdict(striking).Select(m => m?.Level ?? 0).ToList();
        yield return $"  chipping, after map {VerdictMap} ({level.Count} runs): {FocusedPlayer.Fed} level {Pair(level)}, points kept {Pair(points)}; striking without the chip, level {Pair(baseline)} ({baseline.Count} runs)";
        var evenFed = AfterVerdict(even).Where(m => m is not null).Select(m => m!.MainRank).ToList();
        yield return $"  even, after map {VerdictMap} ({evenFed.Count} companies with {FocusedPlayer.Fed}): {FocusedPlayer.Fed}'s own points {Pair(evenFed)}";
        var before = Refusals(striking);
        var after = Refusals(chipping);
        yield return ChipVerdict(
            level.Count == 0 ? 0 : Percentile(level, 0.5),
            before.GetValueOrDefault(FocusedPlayer.Refusal.Chance),
            after.GetValueOrDefault(FocusedPlayer.Refusal.Chance),
            before.GetValueOrDefault(FocusedPlayer.Refusal.NoTile),
            after.GetValueOrDefault(FocusedPlayer.Refusal.NoTile));
    }

    /// <summary>The EXP margin the chipping chair's zeroed total must beat the even chair's by at p50 for the drill to be the lever (round 396).</summary>
    public const int DrillMargin = 50;

    /// <summary>
    /// The drill's print (issue 1181, round 396): per chair, over the runs that won map <see cref="VerdictMap"/>
    /// with the fed unit in the company after it, his EXP the camp's drill zeroed through it (summed per run, p50
    /// p75, and the total) and the EXP he kept per deployed map through it (p50 p75), then the zeroed EXP per map
    /// over those runs; last, the read the Table agreed before the numbers (<see cref="DrillVerdict"/>).
    /// </summary>
    public static IEnumerable<string> DrillLines(IReadOnlyList<Run> even, IReadOnlyList<Run> chipping)
    {
        static string Pair(IEnumerable<int> values)
        {
            var list = values.ToList();
            return $"p50 {Percentile(list, 0.5)} p75 {Percentile(list, 0.75)}";
        }

        static List<List<FedExp>> Through(IReadOnlyList<Run> runs) =>
            runs.Select(r => (Run: r, At: r.Maps.ToList().FindIndex(m => m.Map == VerdictMap)))
                .Where(x => x.At >= 0 && x.At < x.Run.Exp.Count && x.At < x.Run.Companies.Count && x.Run.Companies[x.At].Any(m => m.Id == FocusedPlayer.Fed))
                .Select(x => x.Run.Exp.Take(x.At + 1).ToList())
                .ToList();

        yield return $"levels --drill: {chipping.Count} runs, the chipping chair (issue 1178) and the even chair over the same seeds (issue 1181, round 396); {FocusedPlayer.Fed}'s EXP the levy floor's drill zeroed, and the EXP he kept per deployed map, through map {VerdictMap}";
        var zeroedP50 = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (label, runs) in new[] { ("chipping", chipping), ("even", even) })
        {
            var through = Through(runs);
            var zeroed = through.Select(t => t.Sum(e => e.Zeroed)).ToList();
            var earned = through.SelectMany(t => t.Where(e => e.Deployed).Select(e => e.Earned)).ToList();
            var drilled = through.Sum(t => t.Count(e => e.Zeroed > 0));
            zeroedP50[label] = zeroed.Count == 0 ? 0 : Percentile(zeroed, 0.5);
            yield return $"  {label} ({through.Count} runs with {FocusedPlayer.Fed} after map {VerdictMap}): EXP zeroed by the drill per run {Pair(zeroed)} (total {zeroed.Sum()}, {drilled} camps zeroed some); EXP kept per deployed map {Pair(earned)} ({earned.Count} maps)";
            var perMap = Enumerable.Range(0, VerdictMap).Select(i => through.Where(t => i < t.Count).Sum(t => t[i].Zeroed));
            yield return $"  {label}, EXP zeroed by map 1..{VerdictMap} (summed over those runs): {string.Join(" ", perMap)}";
        }

        yield return DrillVerdict(zeroedP50["chipping"], zeroedP50["even"]);
    }

    /// <summary>
    /// Round 396's read of the drill, agreed before the numbers: when the chipping chair's EXP zeroed through map
    /// <see cref="VerdictMap"/> is at least <see cref="DrillMargin"/> more than the even chair's at p50, feeding happens
    /// and the drill deletes it, so the lever is the drill carrying EXP over, read under the even ceiling's tripwire
    /// (its own issue); otherwise the drill is not the cause and the cold hand play feeding him decides the bar.
    /// </summary>
    public static string DrillVerdict(int chippingZeroedP50, int evenZeroedP50)
    {
        var head = $"  verdict (round 396, through map {VerdictMap}): ";
        var gap = chippingZeroedP50 - evenZeroedP50;
        return gap >= DrillMargin
            ? head + $"the drill deletes the feeding (zeroed p50 chipping {chippingZeroedP50}, even {evenZeroedP50}, {gap} apart, at least {DrillMargin}); the lever is the drill carrying EXP over, read under the tripwire (even p50 0 on L{Threshold} alone)"
            : head + $"the drill is not the cause (zeroed p50 chipping {chippingZeroedP50}, even {evenZeroedP50}, {gap} apart, under {DrillMargin}); the cold hand play feeding {FocusedPlayer.Fed} through map {VerdictMap} decides the bar";
    }

    /// <summary>The fed unit's record entry after map <see cref="VerdictMap"/> on each run that won it, null where he was not in the company.</summary>
    private static List<Member?> AfterVerdict(IReadOnlyList<Run> runs) =>
        runs.Select(r => (Run: r, At: r.Maps.ToList().FindIndex(m => m.Map == VerdictMap)))
            .Where(x => x.At >= 0 && x.At < x.Run.Companies.Count)
            .Select(x => x.Run.Companies[x.At].FirstOrDefault(m => m.Id == FocusedPlayer.Fed))
            .ToList();

    /// <summary>
    /// Round 395's read of the chip, agreed before the numbers: the bar passes when the fed unit is at p50
    /// <see cref="Threshold"/> after map <see cref="VerdictMap"/> on the chipping line. Failing it, kill-chance
    /// refusals that collapse (issue 1178: to half the striking line's or fewer) while no-tile refusals do not
    /// read as positioning, which a hand play answers, not a fifth chair; otherwise the cold hand play feeding him
    /// through map 8 decides the bar. Nothing is tuned to make a chair pass.
    /// </summary>
    public static string ChipVerdict(int fedLevelP50, int chanceBefore, int chanceAfter, int noTileBefore, int noTileAfter)
    {
        var head = $"  verdict (round 395, after map {VerdictMap}): ";
        if (fedLevelP50 >= Threshold)
        {
            return head + $"the bar passes on the chip ({FocusedPlayer.Fed} p50 L{fedLevelP50}); the cold hand play feeding him through map {VerdictMap} confirms it";
        }

        static bool Collapsed(int before, int after) => after * 2 <= before;
        var refusals = $"kill chance {chanceBefore} to {chanceAfter}, no tile {noTileBefore} to {noTileAfter}";
        return Collapsed(chanceBefore, chanceAfter) && !Collapsed(noTileBefore, noTileAfter)
            ? head + $"the bar fails ({FocusedPlayer.Fed} p50 L{fedLevelP50}, needs L{Threshold}); kill-chance refusals collapse and no-tile refusals do not ({refusals}): positioning, the cold hand play decides the bar"
            : head + $"the bar fails ({FocusedPlayer.Fed} p50 L{fedLevelP50}, needs L{Threshold}; {refusals}); the cold hand play feeding him through map {VerdictMap} decides the bar";
    }

    /// <summary>The claimant the Sim's campaigns pick at the branch (<see cref="SimPick.Claimant"/>), or the empty id when no map offers one.</summary>
    public static string PickId(GameContent content) =>
        content.Campaign.Maps.FirstOrDefault(m => m.Branch.Count > 0) is { } branch ? SimPick.Claimant(branch) : "";

    /// <summary>
    /// Round 394's read of the points gate, agreed before the numbers: the bar passes when the Recalling player's
    /// bound at p50 clears <see cref="Gate"/> by <see cref="GateMargin"/> and the fed unit is at p50
    /// <see cref="Threshold"/>; the points kept are printed and do not decide. The ceiling holds while the even
    /// chair reads p50 0 at L7 and the gate and p50 0 at L7 alone, and a broken one steps the levy floor's offset
    /// first. A pick at p50 L7 and the gate on the even chair raises the gate before anything else moves.
    /// </summary>
    public static string GateVerdict(int boundP50, int fedLevelP50, int keptP50, int evenDoorP50, int evenAloneP50, int pickLevelP50, int pickPointsP50)
    {
        var head = $"  verdict (round 394, after map {VerdictMap}): ";
        var needs = Gate + GateMargin;
        var bar = boundP50 >= needs && fedLevelP50 >= Threshold
            ? $"the bar passes (bound p50 {boundP50}, at least {needs}; {FocusedPlayer.Fed} p50 L{fedLevelP50}; kept p50 {keptP50}, not deciding)"
            : $"the bar fails (bound p50 {boundP50}, needs {needs}; {FocusedPlayer.Fed} p50 L{fedLevelP50}, needs L{Threshold}; kept p50 {keptP50}, not deciding)";
        var ceiling = evenDoorP50 == 0 && evenAloneP50 == 0
            ? $"the ceiling holds (even p50 0 at L{Threshold} and the gate, p50 0 at L{Threshold} alone)"
            : $"the ceiling is broken (even p50 {evenDoorP50} at L{Threshold} and the gate, p50 {evenAloneP50} at L{Threshold} alone); step the levy floor's offset first (round 392)";
        var pick = pickLevelP50 >= Threshold && pickPointsP50 >= Gate
            ? $"the pick reaches L{Threshold} and the gate on the even chair (p50 L{pickLevelP50}, {pickPointsP50}); the gate rises first (round 394)"
            : $"the pick stays short of the door on the even chair (p50 L{pickLevelP50}, {pickPointsP50})";
        return head + bar + "; " + ceiling + "; " + pick;
    }

    /// <summary>
    /// Round 393's read of the rank trace, agreed before the numbers: a count that does not reconcile is a bug
    /// and goes first; a Recalling player's bound at C or above pulls no rank lever; otherwise a rank lever is
    /// right, a door-only shape preferred over raising the points a strike pays, named with the largest sink.
    /// </summary>
    public static string TraceVerdict(int boundP50, int unexplainedMaps, string largestSink)
    {
        var head = $"  verdict (round 393, after map {VerdictMap}): ";
        if (unexplainedMaps > 0)
        {
            return head + $"the count does not reconcile on {unexplainedMaps} maps; a bug, and it goes first";
        }

        return boundP50 >= WeaponRanks.Threshold(WeaponRank.C)
            ? head + $"the Recalling player's bound reaches C (p50 {boundP50}); no rank lever"
            : head + $"the Recalling player's bound is short of C (p50 {boundP50}); a rank lever is right, a door-only shape preferred (round 393); the largest sink is {largestSink}";
    }

    /// <summary>
    /// Round 389's read after map <see cref="VerdictMap"/>: the bar is p50 1 non-captain at L7 and rank C,
    /// the ceiling p50 0 on the even chair, and a pass that leaves the captain's p50 below
    /// <see cref="Threshold"/> reopens the curve. Round 392 (issue 1166) reads the ceiling on the level half
    /// alone as well (p50 0 at L7 on the even chair) and steps the floor's offset first on a broken ceiling.
    /// From issue 1167 the bar is read on the striking line (<paramref name="fed"/>): a fail names the fed
    /// unit's p50 level and main-weapon rank points, and sends rank to its own lever when his rank is short
    /// of C with him striking every safe turn (round 392), else the level half back to the Table.
    /// </summary>
    public static string FocusedVerdict(IReadOnlyList<IReadOnlyList<Member>> even, IReadOnlyList<IReadOnlyList<Member>> fed)
    {
        int P50(IReadOnlyList<IReadOnlyList<Member>> companies) => Percentile(companies.Select(c => c.Count(m => !m.Captain && m.AtBar)), 0.5);
        var head = $"  verdict (round 389, after map {VerdictMap}): ";
        if (fed.Count == 0)
        {
            return head + "no striking company reached it";
        }

        var captain = Percentile(fed.Select(c => c.FirstOrDefault(m => m.Captain)?.Level ?? 0), 0.5);
        var unit = fed.Select(c => c.FirstOrDefault(m => m.Id == FocusedPlayer.Fed)).ToList();
        var unitLevel = Percentile(unit.Select(m => m?.Level ?? 0), 0.5);
        var unitRank = Percentile(unit.Select(m => m?.MainRank ?? 0), 0.5);
        var next = unitRank < WeaponRanks.Threshold(WeaponRank.C)
            ? "his rank is short of C with him striking every safe turn; rank takes its own lever (round 392)"
            : "his rank reaches C and the level half is short; the level half goes back to the Table";
        var bar = P50(fed) >= 1
            ? captain >= Threshold ? $"the bar passes (striking p50 {P50(fed)} at L7+C, captain p50 L{captain}); a door has to be fed" : $"the bar passes at p50 {P50(fed)} but the captain is at p50 L{captain}; the curve reopens"
            : $"the bar fails (striking p50 0 at L7+C; {FocusedPlayer.Fed} p50 L{unitLevel}, rank points p50 {unitRank}); {next}";
        var level = Percentile(even.Select(c => c.Count(m => !m.Captain && m.AtLevel(Threshold))), 0.5);
        var ceiling = P50(even) == 0 && level == 0
            ? "the ceiling holds (even p50 0 at L7+C, p50 0 at L7 alone)"
            : $"the ceiling is broken (even p50 {P50(even)} at L7+C, p50 {level} at L7 alone); step the levy floor's offset first (round 392)";
        return head + bar + "; " + ceiling;
    }

    /// <summary>
    /// One chair's per-map read (issue 1150): per map from <see cref="EvenFrom"/>, over the companies standing
    /// after it, the non-captains at L7, L6 and L5 with rank C in the main weapon, the level half and the rank
    /// half alone, and those meeting a form, each as p50 p75; then, over the non-captains at L7 and rank C who
    /// meet no form, what the closest form refuses.
    /// </summary>
    public static IEnumerable<string> ChairLines(GameContent content, IReadOnlyList<Run> runs)
    {
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
