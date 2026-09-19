using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>What one unit did in one game, the action mix gate 4 prints beside the drop (DESIGN.md section 11).</summary>
public sealed record ActionMix(int Attacks, int Damage, int Heals, int Absorbed)
{
    public static ActionMix Zero { get; } = new(0, 0, 0, 0);

    public ActionMix Plus(ActionMix other) => new(Attacks + other.Attacks, Damage + other.Damage, Heals + other.Heals, Absorbed + other.Absorbed);

    public override string ToString() => $"atk {Attacks} dmg {Damage} heal {Heals} abs {Absorbed}";
}

/// <summary>One finished game: who won, when, and what every player unit did.</summary>
public sealed record GameResult(BattleResult Result, int Turns, IReadOnlyDictionary<string, ActionMix> Mix)
{
    public bool Won => Result == BattleResult.Won;
}

/// <summary>A gate's printed line and verdict.</summary>
public sealed record GateResult(string Line, bool Passed);

/// <summary>Plays whole games, a player controller against <see cref="EnemyAi"/>, and tallies them.</summary>
public static class Runner
{
    /// <summary>A full game from the opening state until the battle is decided.</summary>
    public static GameResult Play(GameContent content, MapDefinition map, ulong seed, IPlayer player, ValueList<string> benched = default)
    {
        var state = BattleState.From(map, content, SimRoster.Roster, seed, benched: benched);
        var mix = new Dictionary<string, ActionMix>(StringComparer.Ordinal);
        foreach (var unit in state.UnitsOf(Side.Player))
        {
            mix[unit.Id] = ActionMix.Zero;
        }

        while (!state.Outcome.IsOver)
        {
            var commands = state.Phase == Side.Player ? player.Next(state, content) : EnemyAi.Plan(state, content);
            if (commands.Count == 0)
            {
                throw new InvalidOperationException($"the player produced no command on turn {state.Turn}");
            }

            foreach (var command in commands)
            {
                var result = Resolver.Apply(state, content, command);
                if (!result.Accepted)
                {
                    throw new InvalidOperationException($"{command} was rejected: {result.Rejection!.Message}");
                }

                Tally(mix, result.Events);
                state = result.Next;
                if (state.Outcome.IsOver)
                {
                    break;
                }
            }
        }

        return new GameResult(state.Outcome.Result, state.Turn, mix);
    }

    private static void Tally(Dictionary<string, ActionMix> mix, IReadOnlyList<GameEvent> events)
    {
        foreach (var e in events)
        {
            switch (e)
            {
                case CombatFought fought when mix.ContainsKey(fought.AttackerId):
                    mix[fought.AttackerId] = mix[fought.AttackerId].Plus(new ActionMix(1, fought.Strikes.Where(s => s.AttackerId == fought.AttackerId).Sum(s => s.Damage), 0, 0));
                    break;
                case CombatFought fought when mix.ContainsKey(fought.TargetId) && fought.Phase == Side.Enemy:
                    mix[fought.TargetId] = mix[fought.TargetId].Plus(new ActionMix(0, fought.Strikes.Where(s => s.AttackerId == fought.TargetId).Sum(s => s.Damage), 0, 1));
                    break;
                case ItemUsed used when mix.ContainsKey(used.UnitId) && used.TargetId != used.UnitId:
                    mix[used.UnitId] = mix[used.UnitId].Plus(new ActionMix(0, 0, 1, 0));
                    break;
            }
        }
    }
}

/// <summary>Gates 1 to 5 of DESIGN.md section 11, each a function returning its printed line and verdict.</summary>
public static class Gates
{
    public const int DefaultSeeds = 200;
    public const double BeatableRate = 0.60;
    public const double RandomRate = 0.05;

    /// <summary>Gate 1: the heuristic player wins at least 60 percent of the seeds within the turn limit. Also prints the median and p90 winning turn beside the limit, the instrument issue 47 reads.</summary>
    public static (GateResult Gate, IReadOnlyList<GameResult> Games) Gate1(GameContent content, MapDefinition map, string id, int seeds)
    {
        var games = new List<GameResult>();
        for (var seed = 1; seed <= seeds; seed++)
        {
            games.Add(Runner.Play(content, map, (ulong)seed, new HeuristicPlayer()));
        }

        var wins = games.Count(g => g.Won);
        var rate = (double)wins / seeds;
        var winning = games.Where(g => g.Won).Select(g => g.Turns).OrderBy(t => t).ToList();
        var turns = winning.Count == 0 ? "no wins" : $"winning turn median {Percentile(winning, 0.5)} p90 {Percentile(winning, 0.9)} limit {map.TurnLimit}";
        var passed = rate >= BeatableRate;
        return (new GateResult($"gate 1 beatable: {id}, heuristic wins {wins}/{seeds} ({rate:P0}), {turns}: {Verdict(passed)}", passed), games);
    }

    /// <summary>Gate 2: the random legal player wins at most 5 percent of the seeds.</summary>
    public static GateResult Gate2(GameContent content, MapDefinition map, string id, int seeds)
    {
        var wins = 0;
        for (var seed = 1; seed <= seeds; seed++)
        {
            if (Runner.Play(content, map, (ulong)seed, new RandomLegalPlayer(seed)).Won)
            {
                wins++;
            }
        }

        var rate = (double)wins / seeds;
        var passed = rate <= RandomRate;
        return new GateResult($"gate 2 decisions matter: {id}, random wins {wins}/{seeds} ({rate:P0}): {Verdict(passed)}", passed);
    }

    /// <summary>Gate 3: one enemy phase from deployment with no player move kills nobody. A map declaring <c>cheap_shots: allowed</c> prints the waiver and the kills, and passes.</summary>
    public static GateResult Gate3(GameContent content, MapDefinition map, string id)
    {
        var state = BattleState.From(map, content, SimRoster.Roster, 1);
        var kills = 0;
        state = Apply(state, content, new EndPhase(), ref kills);
        foreach (var command in EnemyAi.Plan(state, content))
        {
            state = Apply(state, content, command, ref kills);
        }

        if (map.CheapShotsAllowed)
        {
            return new GateResult($"gate 3 no cheap shots: {id}, waived by cheap_shots: allowed, {kills} player units killed from deployment: ok", true);
        }

        var passed = kills == 0;
        return new GateResult($"gate 3 no cheap shots: {id}, {kills} player units killed from deployment: {Verdict(passed)}", passed);
    }

    private static BattleState Apply(BattleState state, GameContent content, Command command, ref int kills)
    {
        var result = Resolver.Apply(state, content, command);
        if (!result.Accepted)
        {
            throw new InvalidOperationException($"{command} was rejected: {result.Rejection!.Message}");
        }

        kills += result.Events.Count(e => e is UnitDied { Side: Side.Player });
        return result.Next;
    }

    /// <summary>One recruit's ablation row: the paired drop, its standard error, and the recruit's baseline action mix.</summary>
    public sealed record AblationRow(string RecruitId, double Drop, double StandardError, ActionMix Baseline, ActionMix Benched);

    /// <summary>
    /// Gate 4: each deployed recruit benched in turn over the baseline's seeds, outcomes
    /// paired by seed. Drop is (baseline wins - arm wins) / n; the standard error comes
    /// from the discordant seeds only, sqrt(b + c) / n. A recruit fails only when
    /// drop + 2 * SE is under half the median drop across the cast.
    /// </summary>
    public static GateResult Gate4(GameContent content, MapDefinition map, string id, IReadOnlyList<GameResult> baseline)
    {
        var opening = BattleState.From(map, content, SimRoster.Roster, 1);
        var recruits = opening.UnitsOf(Side.Player).Where(u => !u.IsCaptain).Select(u => u.Id).ToList();
        var rows = new List<AblationRow>();
        foreach (var recruit in recruits)
        {
            var benched = ValueList<string>.Of(recruit);
            var flippedDown = 0;
            var flippedUp = 0;
            var baseMix = ActionMix.Zero;
            var armMix = ActionMix.Zero;
            for (var seed = 1; seed <= baseline.Count; seed++)
            {
                var arm = Runner.Play(content, map, (ulong)seed, new HeuristicPlayer(), benched);
                var before = baseline[seed - 1];
                if (before.Won && !arm.Won)
                {
                    flippedDown++;
                }
                else if (!before.Won && arm.Won)
                {
                    flippedUp++;
                }

                baseMix = baseMix.Plus(before.Mix.GetValueOrDefault(recruit, ActionMix.Zero));
                foreach (var other in arm.Mix.Values)
                {
                    armMix = armMix.Plus(other);
                }
            }

            var n = (double)baseline.Count;
            rows.Add(new AblationRow(recruit, (flippedDown - flippedUp) / n, Math.Sqrt(flippedDown + flippedUp) / n, baseMix, armMix));
        }

        var failing = Judge(rows);
        var lines = rows.Select(r => $"  {r.RecruitId}: drop {r.Drop:F3} se {r.StandardError:F3} baseline [{r.Baseline}] rest of cast benched [{r.Benched}]{(failing.Contains(r.RecruitId) ? " DEAD WEIGHT" : "")}");
        var passed = failing.Count == 0;
        var header = $"gate 4 no dead weight: {id}, {recruits.Count} recruits x {baseline.Count} seeds, median drop {Median(rows.Select(r => r.Drop).ToList()):F3}: {Verdict(passed)}";
        return new GateResult(string.Join('\n', new[] { header }.Concat(lines)), passed);
    }

    /// <summary>The recruits that fail section 11's rule: <c>drop + 2 * SE &lt; 0.5 * median</c>. Empty for an empty cast.</summary>
    public static IReadOnlyList<string> Judge(IReadOnlyList<AblationRow> rows)
    {
        if (rows.Count == 0)
        {
            return Array.Empty<string>();
        }

        var median = Median(rows.Select(r => r.Drop).ToList());
        return rows.Where(r => r.Drop + 2 * r.StandardError < 0.5 * median).Select(r => r.RecruitId).ToList();
    }

    /// <summary>Gate 5's tally: every combat's displayed numbers against what the resolver did.</summary>
    public sealed class ForecastTally
    {
        public int Combats { get; private set; }
        public int Strikes { get; private set; }
        public int Hits { get; private set; }
        public double ExpectedHits { get; private set; }
        public int Crits { get; private set; }
        public double ExpectedCrits { get; private set; }
        public int WrongDamage { get; private set; }

        /// <summary>Counts one combat: the forecast asked before the attack against the strikes it produced.</summary>
        public void Count(CombatForecast forecast, CombatFought fought)
        {
            Combats++;
            foreach (var strike in fought.Strikes)
            {
                var side = strike.AttackerId == fought.AttackerId ? forecast.Attacker : forecast.Defender;
                Strikes++;
                ExpectedHits += Combat.HitProbability(side.HitChance, forecast.Scheme);
                if (!strike.Hit)
                {
                    continue;
                }

                Hits++;
                ExpectedCrits += side.CritChance / 100.0;
                if (strike.Crit)
                {
                    Crits++;
                }
                else if (strike.Damage != side.Damage)
                {
                    WrongDamage++;
                }
            }
        }

        /// <summary>Hits and crits within three standard errors of the displayed probabilities, damage exact, at least <paramref name="minimum"/> combats.</summary>
        public GateResult Result(int minimum)
        {
            var hitSe = Math.Sqrt(Math.Max(1, ExpectedHits * (1 - ExpectedHits / Math.Max(1, Strikes))));
            var critSe = Math.Sqrt(Math.Max(1, ExpectedCrits * (1 - ExpectedCrits / Math.Max(1, Hits))));
            var hitOk = Math.Abs(Hits - ExpectedHits) <= 3 * hitSe;
            var critOk = Math.Abs(Crits - ExpectedCrits) <= 3 * critSe;
            var passed = Combats >= minimum && WrongDamage == 0 && hitOk && critOk;
            return new GateResult(
                $"gate 5 forecast honesty: {Combats} combats, {Strikes} strikes, hits {Hits} expected {ExpectedHits:F1}, crits {Crits} expected {ExpectedCrits:F1}, damage mismatches {WrongDamage}"
                + (Combats < minimum ? $", under the {minimum} required" : "") + $": {Verdict(passed)}",
                passed);
        }
    }

    /// <summary>
    /// Gate 5's own stream: <paramref name="combats"/> random combats between the content's
    /// units (enemy templates scaled to a random level, the Sim's roster) on random
    /// passable terrain at a random distance the attacker's weapon reaches, each resolved
    /// through <see cref="CombatResolver"/> under a keyed rng, every one counted against
    /// the forecast asked first. Gate 6's board-level stream feeds the same tally, so the
    /// count covers both the formulas and the resolver's use of them.
    /// </summary>
    public static void ForecastStream(GameContent content, Gates.ForecastTally tally, int combats, int seed = 1)
    {
        var random = new Random(seed);
        var pool = new List<Unit>(SimRoster.Roster);
        foreach (var template in content.Units.Values)
        {
            for (var level = 1; level <= 10; level += 3)
            {
                pool.Add(template.ScaledTo(level, content.Class(template.ClassId)));
            }
        }

        var terrains = content.Terrain.Values.ToList();
        var counted = 0;
        for (var attempt = 0; counted < combats && attempt < combats * 20; attempt++)
        {
            var attacker = Fighter(pool[random.Next(pool.Count)], content, terrains, random);
            var defender = Fighter(pool[random.Next(pool.Count)], content, terrains, random);
            if (attacker is null || defender is null || attacker.Weapon is null || attacker.Id == defender.Id)
            {
                continue;
            }

            var distance = random.Next(attacker.Weapon.MinRange, attacker.Weapon.MaxRange + 1);
            var scheme = random.Next(2) == 0 ? RollScheme.TwoRollAverage : RollScheme.OneRoll;
            var forecast = Combat.Forecast(attacker, defender, distance, scheme);
            var result = CombatResolver.Resolve(attacker, defender, distance, new CombatContext(1 + attempt, Side.Player), new KeyedRng((ulong)seed * 1000003 + (ulong)attempt), scheme);
            tally.Count(forecast, new CombatFought(attacker.Id, defender.Id, 1, Side.Player, result.Strikes, result.AttackerHp, result.DefenderHp));
            counted++;
        }
    }

    private static Combatant? Fighter(Unit unit, GameContent content, List<Terrain> terrains, Random random)
    {
        var unitClass = content.Class(unit.ClassId);
        var passable = terrains.Where(t => t.IsPassable(unitClass.Movement)).ToList();
        if (passable.Count == 0)
        {
            return null;
        }

        Weapon? weapon = null;
        foreach (var stack in unit.Inventory.Items)
        {
            if (content.Weapons.TryGetValue(stack.ItemId, out var w) && unitClass.CanUse(w.Type) && !w.Heals)
            {
                weapon = w;
                break;
            }
        }

        var maxHp = unit.EffectiveStats(unitClass).Hp;
        return new Combatant(unit, unitClass, weapon, passable[random.Next(passable.Count)], 1 + random.Next(maxHp), 0, random.Next(10) == 0);
    }

    public static string Verdict(bool passed) => passed ? "ok" : "FAILED";

    private static int Percentile(List<int> sorted, double p) => sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(p * sorted.Count) - 1)];

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
    }
}

/// <summary>The player's roster until issue 13 lands the cast.</summary>
public static class SimRoster
{
    public static ValueList<Unit> Roster => Ironwake.Content.SyntheticRoster.Cadets;
}
