using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// The captain's ladder's bar (issue 705, slice 3): the cast's captain fielded in each class of the
/// ladder (<see cref="UnitClass.Captain"/>) at the level the class certifies at, carrying a starting
/// weapon of each type the class adds (<see cref="Kit"/>), so a Ranger has a bow to draw and a Marshal
/// a spell to cast. Each tier is fought on its own <see cref="Board"/>. Per map and tier it reads gate 1
/// and gate 4 for the unpromoted captain and each class, and the captain's share of the company's EXP. The bar
/// (round 232, DECISIONS/0174, replacing 0165's per-map spread): per map a floor, no class reading more than
/// <see cref="Under"/> below the unpromoted captain on the same board unless its captain absorbs under half the
/// unpromoted captain's damage (then a hand play of that map is owed and decides), and gate 4 passing under every
/// class wherever it passes under the unpromoted captain on the same board, with the captain attacking in its
/// baseline; across the campaign (<see cref="Campaign"/>), each class's mean gate 1 over the campaign's maps within
/// <see cref="Spread"/> of the others. A measurement only; nothing here changes what ships.
/// </summary>
public static class LadderRun
{
    /// <summary>The widest spread a tier's classes' campaign means of gate 1 may show at 200 seeds, as a rate (10 points; round 232, DECISIONS/0174; 5 is the 400-seed target before anything ships as tuned). A single map's spread is printed and no longer fails it.</summary>
    public const double Spread = 0.10;

    /// <summary>The furthest a class's gate 1 rate may read below the unpromoted captain's on the same board, as a rate (10 points; DECISIONS/0165).</summary>
    public const double Under = 0.10;

    /// <summary>The starting weapon the kit adds for a weapon type the captain carries none of.</summary>
    public static IReadOnlyDictionary<WeaponType, string> Kit { get; } = new Dictionary<WeaponType, string>
    {
        [WeaponType.Sword] = "iron_sword",
        [WeaponType.Lance] = "iron_lance",
        [WeaponType.Axe] = "iron_axe",
        [WeaponType.Bow] = "iron_bow",
        [WeaponType.Reason] = "cinder",
    };

    /// <summary>
    /// The campaign maps the campaign check leaves out (round 232): Starting Alone, a lesson with the captain alone
    /// and every class near 0, and Sallow Grange, where every class reads within a few points and class does not matter.
    /// </summary>
    public static IReadOnlySet<string> OffTheBar { get; } = new HashSet<string>(StringComparer.Ordinal) { "starting_alone", "sallow_grange" };

    /// <summary>
    /// One class on one board: gate 1's rate, gate 4's verdict, the captain's baseline mix, the weapons the captain
    /// fought with in it (issue 746), and the EXP the captain and the company kept over the won games (issue 758).
    /// </summary>
    public sealed record Reading(string ClassId, double Rate, bool Gate4, ActionMix Captain, WeaponMix? Weapons = null, int CaptainExp = 0, int CompanyExp = 0)
    {
        /// <summary>The captain's share of the company's EXP over the won games, a whole percent (#738's share, pooled), or null when the company earned none.</summary>
        public int? Share => LevelRun.CaptainShare(new LevelRun.Camp(0, Array.Empty<int>(), CaptainExp, CompanyExp));
    }

    /// <summary>
    /// One tier on one map: the board it is fought on (party and enemies raised by <paramref name="Raise"/>
    /// levels), the unpromoted captain on that board, and the tier's classes. On an Escape map
    /// (<paramref name="Escape"/>) a captain who never attacks is leaving, as the map asks, and is not held against the class.
    /// </summary>
    public sealed record TierReading(int Tier, int Raise, Reading Baseline, IReadOnlyList<Reading> Classes, bool Escape = false)
    {
        /// <summary>The widest gap between two classes' gate 1 rates.</summary>
        public double SpreadOf => Classes.Count == 0 ? 0.0 : Classes.Max(c => c.Rate) - Classes.Min(c => c.Rate);

        /// <summary>The classes under which gate 4 fails though it passes with the unpromoted captain on the same board, or whose captain never attacks off an Escape map.</summary>
        public IReadOnlyList<string> Gate4Failures =>
            Classes.Where(c => (Baseline.Gate4 && !c.Gate4) || (!Escape && c.Captain.Attacks == 0)).Select(c => c.ClassId).ToList();

        /// <summary>The classes reading more than <see cref="Under"/> below the unpromoted captain, each with whether its captain absorbs under half the unpromoted captain's damage (the exemption: a hand play is owed and decides).</summary>
        public IReadOnlyList<(string ClassId, bool HandPlayOwed)> UnderBaseline =>
            Classes.Where(c => Baseline.Rate - c.Rate > Under + 1e-9)
                .Select(c => (c.ClassId, c.Captain.Absorbed * 2 < Baseline.Captain.Absorbed))
                .ToList();

        /// <summary>The per-map floor (round 232): no class over <see cref="Under"/> below the unpromoted captain without the exemption, and gate 4 kept. The spread is the campaign's check.</summary>
        public bool Passed => Gate4Failures.Count == 0 && UnderBaseline.All(u => u.HandPlayOwed);
    }

    /// <summary>
    /// One tier across the campaign (round 232): each class's mean gate 1 over <paramref name="Maps"/>, the maps read that are
    /// on the campaign and not <see cref="OffTheBar"/>, in the tier's class order.
    /// </summary>
    public sealed record CampaignTier(int Tier, IReadOnlyList<string> Maps, IReadOnlyList<(string ClassId, double Mean)> Means)
    {
        public double SpreadOf => Means.Count == 0 ? 0.0 : Means.Max(m => m.Mean) - Means.Min(m => m.Mean);

        /// <summary>Within <see cref="Spread"/>; a campaign with no map on the bar passes vacuously, and the line says so.</summary>
        public bool Passed => SpreadOf <= Spread + 1e-9;
    }

    /// <summary>The campaign check per tier over <paramref name="readings"/>: the maps among them that <paramref name="content"/>'s campaign fields and <see cref="OffTheBar"/> does not name.</summary>
    public static IReadOnlyList<CampaignTier> Campaign(GameContent content, IReadOnlyList<MapReading> readings)
    {
        var onCampaign = content.Campaign.Maps.Select(m => m.MapId).ToHashSet(StringComparer.Ordinal);
        var counted = readings.Where(r => onCampaign.Contains(r.MapId) && !OffTheBar.Contains(r.MapId)).ToList();
        var tiers = new List<CampaignTier>();
        foreach (var tier in readings.SelectMany(r => r.Tiers).Select(t => t.Tier).Distinct().OrderBy(t => t))
        {
            var rows = counted.Select(r => r.Tiers.FirstOrDefault(t => t.Tier == tier)).OfType<TierReading>().ToList();
            var ids = rows.SelectMany(t => t.Classes.Select(c => c.ClassId)).Distinct().ToList();
            var means = ids.Select(id => (id, rows.Select(t => t.Classes.First(c => c.ClassId == id).Rate).DefaultIfEmpty(0.0).Average())).ToList();
            tiers.Add(new CampaignTier(tier, counted.Select(r => r.MapId).ToList(), means));
        }

        return tiers;
    }

    /// <summary>The campaign check's printed line for one tier.</summary>
    public static string Line(CampaignTier tier) =>
        tier.Maps.Count == 0
            ? $"  campaign tier {tier.Tier}: no map read is on the bar: {Gates.Verdict(tier.Passed)}"
            : $"  campaign tier {tier.Tier} over {string.Join(", ", tier.Maps)}: {string.Join(", ", tier.Means.Select(m => $"{m.ClassId} {m.Mean * 100:F1}"))}; spread {tier.SpreadOf * 100:F1}, bar {Spread * 100:F0}: {Gates.Verdict(tier.Passed)}";

    /// <summary>One map: a reading per tier.</summary>
    public sealed record MapReading(string MapId, IReadOnlyList<TierReading> Tiers)
    {
        public bool Passed => Tiers.All(t => t.Passed);
    }

    /// <summary>The ladder's classes in content order, the base classes before their forms.</summary>
    public static IReadOnlyList<UnitClass> Ladder(GameContent content) =>
        content.Classes.Values.Where(c => c.Captain).OrderBy(c => c.Advances is null ? 0 : 1).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();

    /// <summary>1 for a base class of the ladder, 2 for an advanced form.</summary>
    public static int Tier(UnitClass unitClass) => unitClass.Advances is null ? 1 : 2;

    /// <summary>
    /// <paramref name="captain"/> promoted into <paramref name="target"/>, a class on the ladder: raised
    /// on its own class's growths to the level the target certifies at, given the ranks the target and
    /// its base ask for, certified through the base when the target is a form (each step through
    /// <see cref="Certifications.Certify(Unit, UnitClass, bool)"/>, so a refusal throws), each step's
    /// mastery earned, since the bar weighs a class as the player has it once its twelve combats are in, then carrying
    /// one <see cref="Kit"/> weapon of each type the class wields and the inventory lacks.
    /// </summary>
    public static Unit Promote(GameContent content, Unit captain, UnitClass target)
    {
        if (!target.Captain)
        {
            throw new ArgumentException($"{target.Id} is not on the captain's ladder", nameof(target));
        }

        var steps = target.Advances is { } basis ? new[] { basis, target } : new[] { target };
        var level = Math.Max(captain.Level, steps.Max(s => s.Certification.Level));
        var unit = captain.AtLevel(level, content.Class(captain.ClassId));
        foreach (var step in steps)
        {
            var skill = unit.Skill;
            foreach (var (type, rank) in step.Certification.Ranks)
            {
                skill = skill.With(type, Math.Max(skill.Points(type), WeaponRanks.Threshold(rank)));
            }

            unit = Certifications.Certify(unit with { Skill = skill }, step, captain: true);
            unit = unit with { Mastery = unit.Mastery.With(step.Id, Math.Max(unit.Mastery.Points(step.Id), step.MasteryPoints)) };
        }

        var inventory = unit.Inventory;
        foreach (var type in target.Weapons)
        {
            var carries = inventory.Items.Any(stack => content.Weapons.TryGetValue(stack.ItemId, out var weapon) && weapon.Type == type);
            if (!carries && Kit.TryGetValue(type, out var id) && !inventory.IsFull)
            {
                inventory = inventory.Add(new ItemStack(id, content.Weapons[id].Durability));
            }
        }

        return unit with { Inventory = inventory };
    }

    /// <summary><paramref name="content"/> with <paramref name="captain"/> in the cast's first slot and the unit table.</summary>
    public static GameContent WithCaptain(GameContent content, Unit captain) =>
        content with { Cast = content.Cast.SetItem(0, captain), Units = content.Units.ContainsKey(captain.Id) ? content.Units.SetItem(captain.Id, captain) : content.Units };

    /// <summary>
    /// The board a tier is fought on: <paramref name="map"/> with its enemy level raised by as many
    /// levels as the tier's certification level stands over the captain's, and the cast raised by the
    /// same (the curve's <c>party +N</c> proxy, issue 704), so a level 10 captain is measured among
    /// a company and against enemies of his own weight rather than on a level 1 board he carries alone.
    /// </summary>
    public static (GameContent Content, MapDefinition Map, int Raise) Board(GameContent content, MapDefinition map, int tier)
    {
        var level = Ladder(content).Where(c => Tier(c) == tier).Select(c => c.Certification.Level).DefaultIfEmpty(Unit.MinLevel).Max();
        var raise = Math.Max(0, level - content.Cast[0].Level);
        if (raise == 0)
        {
            return (content, map, 0);
        }

        var raised = ValueList<Unit>.From(content.Cast.Select(u => u.AtLevel(Math.Min(u.Level + raise, Unit.MaxLevel), content.Class(u.ClassId))));
        var party = content with { Cast = raised, Units = raised.Aggregate(content.Units, (units, u) => units.ContainsKey(u.Id) ? units.SetItem(u.Id, u) : units) };
        return (party, map with { EnemyLevel = Math.Min(map.EnemyLevel + raise, Unit.MaxLevel) }, raise);
    }

    /// <summary>Gate 1 and gate 4 on each tier's board of <paramref name="map"/> for the unpromoted captain and each of the tier's classes, over seeds 1..<paramref name="seeds"/>.</summary>
    public static MapReading Measure(GameContent content, string mapId, MapDefinition map, int seeds)
    {
        var tiers = new List<TierReading>();
        foreach (var tier in Ladder(content).Select(Tier).Distinct().OrderBy(t => t))
        {
            var (party, board, raise) = Board(content, map, tier);
            var baseline = Read(party, mapId, board, seeds, party.Cast[0].ClassId);
            var classes = Ladder(content).Where(c => Tier(c) == tier)
                .Select(c => Read(WithCaptain(party, Promote(party, party.Cast[0], c)), mapId, board, seeds, c.Id))
                .ToList();
            tiers.Add(new TierReading(tier, raise, baseline, classes, map.Win == WinCondition.Escape));
        }

        return new MapReading(mapId, tiers);
    }

    private static Reading Read(GameContent content, string mapId, MapDefinition map, int seeds, string classId)
    {
        var (_, games) = Gates.Gate1(content, map, mapId, seeds);
        var gate4 = Gates.Gate4(content, map, mapId, games);
        var captain = content.Cast[0].Id;
        var mix = games.Aggregate(ActionMix.Zero, (sum, g) => sum.Plus(g.Mix.GetValueOrDefault(captain, ActionMix.Zero)));
        var weapons = games.Aggregate(WeaponMix.Zero, (sum, g) => sum.Plus(g.Weapons.GetValueOrDefault(captain, WeaponMix.Zero)));
        var camps = games.Select(g => g.Camp).OfType<LevelRun.Camp>().ToList();
        return new Reading(classId, games.Count == 0 ? 0.0 : games.Count(g => g.Won) / (double)games.Count, gate4.Passed, mix, weapons, camps.Sum(c => c.CaptainExp), camps.Sum(c => c.CompanyExp));
    }

    /// <summary>The printed table for one map: per tier its board, a row per class, then the spread and the floor's verdict.</summary>
    public static IEnumerable<string> Lines(MapReading reading)
    {
        yield return $"ladder: {reading.MapId}";
        foreach (var tier in reading.Tiers)
        {
            yield return $"  tier {tier.Tier}, party and enemies +{tier.Raise}:";
            foreach (var r in new[] { tier.Baseline }.Concat(tier.Classes))
            {
                yield return $"    {r.ClassId}: gate 1 {r.Rate * 100:F1}, gate 4 {Gates.Verdict(r.Gate4)}, captain [{r.Captain}], captain share {(r.Share is { } share ? $"{share}%" : "none earned")}, weapons [{r.Weapons ?? WeaponMix.Zero}]";
            }

            var failures = tier.Gate4Failures;
            var under = tier.UnderBaseline;
            yield return $"    spread {tier.SpreadOf * 100:F1} (the bar is the campaign's)"
                + (under.Count == 0 ? "" : $"; over {Under * 100:F0} under {tier.Baseline.ClassId}: {string.Join(", ", under.Select(u => u.HandPlayOwed ? $"{u.ClassId} (absorbs under half; a hand play decides)" : u.ClassId))}")
                + (failures.Count == 0 ? "" : $"; gate 4 lost under {string.Join(", ", failures)}")
                + $": {Gates.Verdict(tier.Passed)}";
        }
    }

    /// <summary>
    /// The smoke's row (issue 705): every origin the campaign offers by every base class of the ladder,
    /// each captain built (origin, then <see cref="Promote"/>) and played in one AI-vs-AI game on
    /// <paramref name="map"/> twice, failing on a refusal, an exception or a game that replays differently.
    /// </summary>
    public static GateResult OriginByClass(GameContent content, string mapId, MapDefinition map)
    {
        var bases = Ladder(content).Where(c => Tier(c) == 1).ToList();
        var origins = content.Campaign.Origins;
        var failures = new List<string>();
        foreach (var origin in origins)
        {
            foreach (var unitClass in bases)
            {
                try
                {
                    var captain = Promote(content, origin.Apply(content.Cast[0]), unitClass);
                    var fielded = WithCaptain(content, captain);
                    var (first, _) = Program.FullGame(fielded, map, 1);
                    var (second, _) = Program.FullGame(fielded, map, 1);
                    if (first != second)
                    {
                        failures.Add($"{origin.Id} {unitClass.Id} replayed differently");
                    }
                }
                catch (Exception e) when (e is InvalidOperationException or ArgumentException or KeyNotFoundException)
                {
                    failures.Add($"{origin.Id} {unitClass.Id}: {e.Message}");
                }
            }
        }

        var count = origins.Count * bases.Count;
        var ok = failures.Count == 0 && count > 0;
        var detail = failures.Count > 0 ? "; " + string.Join("; ", failures) : count == 0 ? "; no origin or no ladder class" : "";
        return new GateResult($"origin by class: {count} captains ({origins.Count} origins x {bases.Count} classes), one AI-vs-AI game each on {mapId}{detail}: {Gates.Verdict(ok)}", ok);
    }
}
