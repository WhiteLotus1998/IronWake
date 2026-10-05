using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>One enemy template's row of the Drover's measure: Rook's expected damage per combat in each class, each phase.</summary>
public sealed record DroverRow(string Template, double DroverPlayer, double CaptainPlayer, double DroverEnemy, double CaptainEnemy)
{
    public double DroverTotal => DroverPlayer + DroverEnemy;

    public double CaptainTotal => CaptainPlayer + CaptainEnemy;
}

/// <summary>The measure at one level and one drake stage: the rows, read by the price ceiling per phase.</summary>
public sealed record DroverReading(int Level, DrakeStage Stage, ValueList<DroverRow> Rows);

/// <summary>
/// The price ceiling on one phase of one reading (issue 882, round 293): over the templates where the Sky Captain's
/// expected damage is above hers, how many there are, the median and the worst of hers over his. A template where
/// neither deals damage, or where she leads, is not counted.
/// </summary>
public sealed record DroverGate(int Led, int Templates, double Median, double Worst)
{
    /// <summary>The ceiling: at Grown her median is at least this share of the Sky Captain's damage where he leads.</summary>
    public const double Ceiling = 0.70;

    /// <summary>Whether the median meets <see cref="Ceiling"/>; with no template led there is no price, and it passes.</summary>
    public bool Passes => Led == 0 || Median >= Ceiling;

    /// <summary>The gate over <paramref name="rows"/>, read on <paramref name="drover"/> against <paramref name="captain"/>.</summary>
    public static DroverGate Of(IReadOnlyCollection<DroverRow> rows, Func<DroverRow, double> drover, Func<DroverRow, double> captain)
    {
        var ratios = rows.Where(r => captain(r) > drover(r)).Select(r => drover(r) / captain(r)).OrderBy(x => x).ToList();
        if (ratios.Count == 0)
        {
            return new DroverGate(0, rows.Count, 1, 1);
        }

        var mid = ratios.Count / 2;
        var median = ratios.Count % 2 == 1 ? ratios[mid] : (ratios[mid - 1] + ratios[mid]) / 2;
        return new DroverGate(ratios.Count, rows.Count, median, ratios[0]);
    }

    /// <summary>Only Grown is gated; half-grown prints the same numbers (round 293).</summary>
    public static bool Gated(DrakeStage stage) => stage == DrakeStage.Grown;
}

/// <summary>
/// The Drover's measure (issue 872): Rook at a level in the Drover and in the Sky Captain, against every enemy
/// template the content fields (<see cref="SignatureCeiling.Targets"/>) raised to the same level, both on plain.
/// The player phase is her attack at distance 1; the enemy phase is the template's attack at its nearest range
/// and her counter, 0 when the lance cannot reach. Expected damage per combat is the section 5 expectation
/// (<see cref="SignatureCeiling.DamagePerCombat"/>'s: damage times the hit probability times the crit factor,
/// times the strikes) plus the drake's bite times the chance that at least one strike lands, not capped at the
/// target's HP and not conditioned on both standing, so a kill never hides the margin.
/// </summary>
public static class DroverMeasure
{
    /// <summary>The levels the issue names.</summary>
    public static readonly int[] Levels = { 7, 10, 15 };

    /// <summary>The stages the issue names.</summary>
    public static readonly DrakeStage[] Stages = { DrakeStage.HalfGrown, DrakeStage.Grown };

    /// <summary>Rook at <paramref name="level"/> in <paramref name="classId"/>, her drake at <paramref name="stage"/>, with her first lance.</summary>
    public static Unit Rook(GameContent content, string classId, int level, DrakeStage stage)
    {
        var card = content.Cast.Single(u => u.Id == "rook");
        return card.AtLevel(level, content.Class(card.ClassId)) with { ClassId = classId, Drake = new DrakeState(stage, 0) };
    }

    public static DroverReading Read(GameContent content, int level, DrakeStage stage, RollScheme scheme)
    {
        var plain = content.Terrain[SignatureCeiling.TerrainId];
        var drover = Rook(content, "drover", level, stage);
        var captain = Rook(content, "skycaptain", level, stage);
        var lance = content.Weapon(drover.Inventory.Items.Select(s => s.ItemId).First(id => content.Weapons.TryGetValue(id, out var w) && w.Type == WeaponType.Lance));
        var rows = new List<DroverRow>();
        foreach (var (template, weapon) in SignatureCeiling.Targets(content))
        {
            var enemy = template.Level >= level ? template : template.AtLevel(level, content.Class(template.ClassId));
            Combatant Foe() => content.CombatantOf(enemy, weapon, plain, content.StatsOf(enemy).Hp);
            Combatant Her(Unit rook) => content.CombatantOf(rook, lance, plain, content.StatsOf(rook).Hp);
            double Attack(Unit rook) => Expected(Combat.Forecast(Her(rook), Foe(), 1, scheme).Attacker, scheme);
            double Counter(Unit rook)
            {
                var distance = Math.Max(1, weapon.MinRange);
                return Foe().CanStrike(distance) ? Expected(Combat.Forecast(Foe(), Her(rook), distance, scheme).Defender, scheme) : 0;
            }

            rows.Add(new DroverRow(template.Id, Attack(drover), Attack(captain), Counter(drover), Counter(captain)));
        }

        return new DroverReading(level, stage, ValueList<DroverRow>.From(rows));
    }

    /// <summary>A side's expected damage in one combat: the strikes' section 5 expectation and the bite times the chance any strike lands.</summary>
    public static double Expected(SideForecast side, RollScheme scheme)
    {
        if (!side.Strikes)
        {
            return 0;
        }

        var p = Combat.HitProbability(side.HitChance, scheme);
        var perStrike = side.Damage * p * (side.CritGrounds ? 1 : 1 + (Combat.CritMultiplier - 1) * side.CritChance / 100.0);
        return perStrike * side.StrikeCount + side.Bite * (1 - Math.Pow(1 - p, side.StrikeCount));
    }

    /// <summary>The start of the verdict line when a Grown cell fails the price ceiling.</summary>
    public const string FailVerdict = "drover: the price ceiling fails";

    /// <summary>The report <c>--drover</c> prints: per level and stage, the sidegrade measures, the table, and the price ceiling per phase; the last line is the verdict.</summary>
    public static IReadOnlyList<string> Lines(GameContent content, RollScheme scheme)
    {
        var lines = new List<string> { "drover: Rook as the Drake Warden against the Sky Captain, expected damage per combat on plain (issues 872, 882)" };
        var failed = false;
        foreach (var level in Levels)
        {
            foreach (var stage in Stages)
            {
                var reading = Read(content, level, stage, scheme);
                var drover = Rook(content, "drover", level, stage);
                var measure = $"doubling {Sidegrades.Measure(drover, content.Class("drover"), content, SidegradeMeasure.Doubling)} vs {Sidegrades.Measure(drover, content.Class("skycaptain"), content, SidegradeMeasure.Doubling)}, damage {Sidegrades.Measure(drover, content.Class("drover"), content, SidegradeMeasure.Damage)} vs {Sidegrades.Measure(drover, content.Class("skycaptain"), content, SidegradeMeasure.Damage)}";
                lines.Add($"level {level}, {Drake.Word(stage)} (bite {AbilityRules.Bite(content.AbilitiesOf(drover), drover)}): {measure}");
                lines.Add("  template          player: drover  captain   enemy: drover  captain");
                foreach (var row in reading.Rows)
                {
                    lines.Add($"  {row.Template,-16} {row.DroverPlayer,15:F2} {row.CaptainPlayer,8:F2} {row.DroverEnemy,15:F2} {row.CaptainEnemy,8:F2}");
                }

                foreach (var (name, d, c) in new (string, Func<DroverRow, double>, Func<DroverRow, double>)[]
                {
                    ("player phase", r => r.DroverPlayer, r => r.CaptainPlayer),
                    ("enemy phase", r => r.DroverEnemy, r => r.CaptainEnemy),
                })
                {
                    var gate = DroverGate.Of(reading.Rows, d, c);
                    var verdict = !DroverGate.Gated(stage) ? "not gated" : gate.Passes ? "gate passes" : "gate fails";
                    failed |= DroverGate.Gated(stage) && !gate.Passes;
                    lines.Add($"  {name}: the captain leads on {gate.Led} of {gate.Templates}, her median {gate.Median:F3} of his, worst {gate.Worst:F3}; {verdict}");
                }
            }
        }

        lines.Add(failed
            ? $"{FailVerdict} (Grown median under {DroverGate.Ceiling:F2} of the Sky Captain's damage where he leads)"
            : $"drover: the price ceiling passes at every level and phase (Grown median at least {DroverGate.Ceiling:F2} where he leads)");
        return lines;
    }
}
