using System.Globalization;
using Ironwake.Content.Protocol;
using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>What one unit did in one game, the action mix gate 4 prints beside the drop (DESIGN.md section 11).</summary>
public sealed record ActionMix(int Attacks, int Damage, int Heals, int Absorbed)
{
    public static ActionMix Zero { get; } = new(0, 0, 0, 0);

    public ActionMix Plus(ActionMix other) => new(Attacks + other.Attacks, Damage + other.Damage, Heals + other.Heals, Absorbed + other.Absorbed);

    public override string ToString() => $"atk {Attacks} dmg {Damage} heal {Heals} abs {Absorbed}";
}

/// <summary>
/// One finished game: who won, when, how a loss was lost, the last turn a combat was fought
/// (zero when none was), what every player unit did, and the highest kill probability the
/// veto refused (<see cref="HeuristicPlayer.HighestRefusedKill"/>; null for any other
/// player or when it refused nothing).
/// </summary>
public sealed record GameResult(BattleResult Result, int Turns, IReadOnlyDictionary<string, ActionMix> Mix, LossCause Cause = LossCause.None, int LastCombatTurn = 0, double? RefusedKill = null)
{
    /// <summary>
    /// The highest kill probability the veto refused on the game's last player phase
    /// (<see cref="HeuristicPlayer.LastPhaseRefusedKill"/>, issue 157): on a timeout, the
    /// board the game stalled on. Null for any other player or when that phase refused nothing.
    /// </summary>
    public double? StallRefusedKill { get; init; }

    public bool Won => Result == BattleResult.Won;

    /// <summary>The weapon rank points of every player unit as it last stood in the game (issue 67), by id.</summary>
    public IReadOnlyDictionary<string, WeaponSkill> Skills { get; init; } = new Dictionary<string, WeaponSkill>(StringComparer.Ordinal);

    /// <summary>The mastery points of every player unit as it last stood in the game (issue 69), by id.</summary>
    public IReadOnlyDictionary<string, MasteryProgress> Masteries { get; init; } = new Dictionary<string, MasteryProgress>(StringComparer.Ordinal);

    /// <summary>The player units other than the captain deployed at the start of the game (issue 263).</summary>
    public int Recruits { get; init; }

    /// <summary>
    /// The player units other than the captain that came out of the game alive, by
    /// <see cref="BattleState.Survivors"/>: on an Escape map the ones that left through an
    /// exit, so a recruit left behind is not counted (issues 263 and 269).
    /// </summary>
    public int RecruitsOut { get; init; }

    /// <summary>
    /// The ids of the player units counted in <see cref="RecruitsOut"/>, the captain aside,
    /// so gate 4 on an Escape map can leave the benched recruit out of the baseline's count
    /// (issue 338).
    /// </summary>
    public IReadOnlySet<string> Out { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Overwatch's counts for the game (DESIGN.md 13.17, round 115), per side: watches taken,
    /// watches taken while a strike at a displayed 50 or more was legal, and watch shots fired.
    /// </summary>
    public WatchCounts PlayerWatch { get; init; } = WatchCounts.Zero;

    /// <inheritdoc cref="PlayerWatch"/>
    public WatchCounts EnemyWatch { get; init; } = WatchCounts.Zero;

    /// <summary>The pincer's counts for the game (DESIGN.md 13.13, issue 495); zero on a map without the header.</summary>
    public PinCounts Pins { get; init; } = PinCounts.Zero;

    /// <summary>Cover's counts for the game (DESIGN.md 13.19, issue 531); zero on a map without the header.</summary>
    public CoverCounts Covers { get; init; } = CoverCounts.Zero;
}

/// <summary>
/// One game's cover counts (DESIGN.md 13.19): covers taken, of them taken with a legal strike
/// passed up, swaps fired, and enemy actors the cover turned away (<see cref="CoverRule.PassedLine"/>).
/// </summary>
public sealed record CoverCounts(int Taken, int OverStrike, int Fired, int Passed)
{
    public static CoverCounts Zero { get; } = new(0, 0, 0, 0);

    /// <summary>The counts after one event.</summary>
    public CoverCounts After(GameEvent e) => e switch
    {
        CoverTaken t => this with { Taken = Taken + 1, OverStrike = OverStrike + (t.PassedUpTargetId is null ? 0 : 1) },
        CoverFired => this with { Fired = Fired + 1 },
        _ => this,
    };
}

/// <summary>
/// The pincer's counts in a game (DESIGN.md 13.13, issue 495): the enemy anvils that acted
/// (<see cref="EnemyAi.PlanWithAnvils"/>, counted when an anvil plan's first command is
/// applied), and each side's strikes made pinned and landed pinned. A strike is read from
/// the resolver's <see cref="CombatFought"/>; whether it was pinned is
/// <see cref="Pincer.PinnedBy"/> on the board the resolver struck on, the one function its
/// hit reads, so the count is of what happened and never of a plan.
/// </summary>
public sealed record PinCounts(int Anvils, int EnemyStrikes, int EnemyLanded, int PlayerStrikes, int PlayerLanded)
{
    public static PinCounts Zero { get; } = new(0, 0, 0, 0, 0);

    /// <summary>
    /// The counts after <paramref name="command"/> was applied to <paramref name="before"/>
    /// with <paramref name="events"/>: every strike of a <see cref="CombatFought"/> an Attack
    /// raised, counted for its striker's side when that striker was pinning its foe.
    /// </summary>
    public PinCounts After(BattleState before, Command command, IReadOnlyList<GameEvent> events)
    {
        if (command is not Attack attack || before.Find(attack.UnitId) is not { } attacker || before.Find(attack.TargetId) is not { } target)
        {
            return this;
        }

        var pinning = new HashSet<string>(StringComparer.Ordinal);
        if (Pincer.PinnedBy(before, attacker, target) is not null)
        {
            pinning.Add(attacker.Id);
        }

        if (Pincer.PinnedBy(before, target, attacker) is not null)
        {
            pinning.Add(target.Id);
        }

        var next = this;
        foreach (var strike in events.OfType<CombatFought>().SelectMany(f => f.Strikes).Where(s => pinning.Contains(s.AttackerId)))
        {
            var landed = strike.Hit ? 1 : 0;
            next = before.Find(strike.AttackerId)!.Side == Side.Enemy
                ? next with { EnemyStrikes = next.EnemyStrikes + 1, EnemyLanded = next.EnemyLanded + landed }
                : next with { PlayerStrikes = next.PlayerStrikes + 1, PlayerLanded = next.PlayerLanded + landed };
        }

        return next;
    }
}

/// <summary>One side's overwatch counts in a game (DESIGN.md 13.17).</summary>
public sealed record WatchCounts(int Taken, int OverStrike, int Shots)
{
    public static WatchCounts Zero { get; } = new(0, 0, 0);

    /// <summary>The counts after one event, read on the phase <paramref name="phase"/> it happened in.</summary>
    public WatchCounts After(GameEvent e, Side side, Side phase) => e switch
    {
        WatchTaken t when phase == side => this with { Taken = Taken + 1, OverStrike = OverStrike + (t.PassedUpHit >= Overwatch.OverStrikeHit ? 1 : 0) },
        WatchFired when phase != side => this with { Shots = Shots + 1 },
        _ => this,
    };
}

/// <summary>A gate's printed line and verdict.</summary>
public sealed record GateResult(string Line, bool Passed);

/// <summary>Plays whole games, a player controller against <see cref="EnemyAi"/>, and tallies them.</summary>
public static class Runner
{
    /// <summary>
    /// A full game from the opening state until the battle is decided. With
    /// <paramref name="turns"/>, each player phase is read by <see cref="TurnState"/> at its
    /// start (issue 47).
    /// </summary>
    public static GameResult Play(GameContent content, MapDefinition map, ulong seed, IPlayer player, ValueList<string> benched = default, RollScheme scheme = RollScheme.TwoRollAverage, HitTally? hits = null, List<TurnReading>? turns = null)
    {
        var state = BattleState.From(map, content, content.Cast, seed, scheme, benched);
        var recruits = state.UnitsOf(Side.Player).Count(u => !u.IsCaptain);
        var mix = new Dictionary<string, ActionMix>(StringComparer.Ordinal);
        foreach (var unit in state.UnitsOf(Side.Player))
        {
            mix[unit.Id] = ActionMix.Zero;
        }

        var lastCombatTurn = 0;
        var playerWatch = WatchCounts.Zero;
        var enemyWatch = WatchCounts.Zero;
        var pins = PinCounts.Zero;
        var covers = CoverCounts.Zero;
        while (!state.Outcome.IsOver)
        {
            if (turns is not null && state.Phase == Side.Player && (turns.Count == 0 || turns[^1].Turn < state.Turn))
            {
                turns.Add(TurnState.Read(state, content));
            }

            var anvils = new List<EnemyAi.AnvilPlan>();
            IReadOnlyList<Command> commands;
            if (state.Phase == Side.Player)
            {
                commands = player.Next(state, content);
            }
            else
            {
                (commands, var planned) = EnemyAi.PlanWithAnvils(state, content);
                anvils.AddRange(planned);
            }

            if (commands.Count == 0)
            {
                throw new InvalidOperationException($"the player produced no command on turn {state.Turn}");
            }

            foreach (var command in commands)
            {
                hits?.Record(state, content, command);
                if (state.Phase == Side.Enemy && CoverRule.PassedLine(state, content, command) is not null)
                {
                    covers = covers with { Passed = covers.Passed + 1 };
                }

                var result = Resolver.Apply(state, content, command);
                if (!result.Accepted)
                {
                    throw new InvalidOperationException($"{command} was rejected: {result.Rejection!.Message}");
                }

                Tally(mix, result.Events);
                pins = pins.After(state, command, result.Events);
                if (anvils.FindIndex(a => a.Commands[0] == command) is var anvil and >= 0)
                {
                    pins = pins with { Anvils = pins.Anvils + 1 };
                    anvils.RemoveAt(anvil);
                }

                foreach (var e in result.Events)
                {
                    playerWatch = playerWatch.After(e, Side.Player, state.Phase);
                    enemyWatch = enemyWatch.After(e, Side.Enemy, state.Phase);
                    covers = covers.After(e);
                }

                if (result.Events.OfType<CombatFought>().Any())
                {
                    lastCombatTurn = state.Turn;
                }

                state = result.Next;
                if (state.Outcome.IsOver)
                {
                    break;
                }

                if (result.Events.OfType<WatchFired>().Any(f => f.Strike.TargetHpAfter == 0))
                {
                    // A watch shot killed the mover (DESIGN.md 13.17): the rest of its plan is void, so plan again.
                    break;
                }
            }
        }

        return new GameResult(state.Outcome.Result, state.Turn, mix, state.Outcome.Cause, lastCombatTurn, RefusedKillOf(player))
        {
            StallRefusedKill = StallRefusedKillOf(player),
            PlayerWatch = playerWatch,
            EnemyWatch = enemyWatch,
            Pins = pins,
            Covers = covers,
            Skills = LastOf(state, unit => unit.Skill),
            Masteries = LastOf(state, unit => unit.Mastery),
            Recruits = recruits,
            RecruitsOut = state.Survivors().Count(u => !u.IsCaptain),
            Out = state.Survivors().Where(u => !u.IsCaptain).Select(u => u.Id).ToHashSet(StringComparer.Ordinal),
        };
    }

    /// <summary>Something of each player unit as it last stood in the game, so a unit that fell keeps the points it had.</summary>
    private static Dictionary<string, T> LastOf<T>(BattleState state, Func<Unit, T> read)
    {
        var last = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var step in state.History.Append(state))
        {
            foreach (var unit in step.UnitsOf(Side.Player))
            {
                last[unit.Id] = read(unit.Unit);
            }
        }

        return last;
    }

    private static double? RefusedKillOf(IPlayer player) => player switch
    {
        HeuristicPlayer heuristic => heuristic.HighestRefusedKill,
        PrefixPlayer prefix => prefix.HighestRefusedKill,
        _ => null,
    };

    private static double? StallRefusedKillOf(IPlayer player) => player switch
    {
        HeuristicPlayer heuristic => heuristic.LastPhaseRefusedKill,
        PrefixPlayer prefix => prefix.LastPhaseRefusedKill,
        _ => null,
    };

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

    /// <summary>The scheme's name as the Sim prints it, so a pasted row says which arm of the hit A/B it came from.</summary>
    public static string Name(RollScheme scheme) => scheme == RollScheme.OneRoll ? "one roll" : "two-roll average";

    /// <summary>
    /// Gate 1: the heuristic player wins at least 60 percent of the seeds within the turn limit.
    /// Also prints the median and p90 winning turn beside the limit, the instrument issue 47
    /// reads; the losses by cause in section 7's order with the mean quiet tail of the
    /// timeouts (turns from the last combat to the limit, so a tail near the limit is a board
    /// that stopped and one near zero a fight that ran out of clock; issue 114); and the roll
    /// scheme the games were fought under. On an Escape map it also prints the survivors
    /// over the wins (<see cref="Survivors"/>, issue 263). With <paramref name="readings"/>, every game's
    /// player phases are read for issue 47's turn-state counters, one list per seed.
    /// </summary>
    public static (GateResult Gate, IReadOnlyList<GameResult> Games) Gate1(GameContent content, MapDefinition map, string id, int seeds, RollScheme scheme = RollScheme.TwoRollAverage, HitTally? hits = null, List<IReadOnlyList<TurnReading>>? readings = null)
    {
        var games = new List<GameResult>();
        for (var seed = 1; seed <= seeds; seed++)
        {
            var read = readings is null ? null : new List<TurnReading>();
            games.Add(Runner.Play(content, map, (ulong)seed, new HeuristicPlayer(), scheme: scheme, hits: hits, turns: read));
            if (read is not null)
            {
                readings!.Add(read);
            }
        }

        var wins = games.Count(g => g.Won);
        var rate = (double)wins / seeds;
        var winning = games.Where(g => g.Won).Select(g => g.Turns).OrderBy(t => t).ToList();
        var turns = winning.Count == 0 ? "no wins" : $"winning turn median {Percentile(winning, 0.5)} p90 {Percentile(winning, 0.9)} limit {map.TurnLimit}";
        var passed = rate >= BeatableRate;
        return (new GateResult($"gate 1 beatable: {id}, heuristic wins {wins}/{seeds} ({rate:P0}), {turns}, {Losses(games, map)}, {RefusedKill(games)}, {EscapeSurvivors(games, map)}{Watches(games, map)}{Pins(games, map)}{Covers(games, map)}{Name(scheme)}: {Verdict(passed)}", passed), games);
    }

    /// <summary>
    /// On a <c>cover: on</c> map, the per-game means of the cover counts (DESIGN.md 13.19, issue 531),
    /// as <c>cover 1.2 taken 0.0 over a strike 0.4 fired 0.3 passed, </c>; nothing on any other map.
    /// </summary>
    public static string Covers(IReadOnlyList<GameResult> games, MapDefinition map) =>
        !map.CoverEnabled || games.Count == 0
            ? ""
            : string.Create(CultureInfo.InvariantCulture, $"cover {games.Average(g => g.Covers.Taken):0.0} taken {games.Average(g => g.Covers.OverStrike):0.0} over a strike {games.Average(g => g.Covers.Fired):0.0} fired {games.Average(g => g.Covers.Passed):0.0} passed, ");

    /// <summary>
    /// On an <c>overwatch: on</c> map, the per-game means of each side's watch counts (DESIGN.md
    /// 13.17, round 115), as <c>watch player 1.2 taken 0.0 over 50 0.3 shots, enemy ...</c>; nothing on any other map.
    /// </summary>
    public static string Watches(IReadOnlyList<GameResult> games, MapDefinition map)
    {
        if (!map.OverwatchEnabled || games.Count == 0)
        {
            return "";
        }

        string Side(Func<GameResult, WatchCounts> of) =>
            string.Create(CultureInfo.InvariantCulture, $"{games.Average(g => of(g).Taken):0.0} taken {games.Average(g => of(g).OverStrike):0.0} over 50 {games.Average(g => of(g).Shots):0.0} shots");
        return $"watch player {Side(g => g.PlayerWatch)}, enemy {Side(g => g.EnemyWatch)}, ";
    }

    /// <summary>
    /// On a <c>pincer: on</c> map, the pincer's totals over the games (DESIGN.md 13.13, issue
    /// 495), as <c>pins enemy anvils in 40/40 games 167 anvils 158 strikes pinned 120 landed,
    /// player 12 strikes pinned 9 landed, </c>; nothing on any other map.
    /// </summary>
    public static string Pins(IReadOnlyList<GameResult> games, MapDefinition map)
    {
        if (!map.PincerEnabled || games.Count == 0)
        {
            return "";
        }

        return $"pins enemy anvils in {games.Count(g => g.Pins.Anvils > 0)}/{games.Count} games {games.Sum(g => g.Pins.Anvils)} anvils "
            + $"{games.Sum(g => g.Pins.EnemyStrikes)} strikes pinned {games.Sum(g => g.Pins.EnemyLanded)} landed, "
            + $"player {games.Sum(g => g.Pins.PlayerStrikes)} strikes pinned {games.Sum(g => g.Pins.PlayerLanded)} landed, ";
    }

    /// <summary>
    /// Over the won games, the median number of recruits that came out alive and the count
    /// of wins with recruits deployed and none out, as <c>survivors p50 1 of 4, captain alone 12</c>,
    /// or <c>survivors -</c> when nothing was won (issue 263). An Escape win is the captain
    /// leaving, so a win rate alone cannot tell a party that got out from a captain who
    /// walked away from everyone; this row can. Printed, never gated.
    /// </summary>
    public static string Survivors(IReadOnlyList<GameResult> games)
    {
        var wins = games.Where(g => g.Won).ToList();
        if (wins.Count == 0)
        {
            return "survivors -";
        }

        var median = Median(wins.Select(g => (double)g.RecruitsOut).ToList()).ToString("0.#", CultureInfo.InvariantCulture);
        var alone = wins.Count(g => g.Recruits > 0 && g.RecruitsOut == 0);
        return $"survivors p50 {median} of {wins.Max(g => g.Recruits)}, captain alone {alone}";
    }

    /// <summary>The survivors row and a separator on an Escape map; nothing on any other.</summary>
    private static string EscapeSurvivors(IReadOnlyList<GameResult> games, MapDefinition map) =>
        map.Win == WinCondition.Escape ? $"{Survivors(games)}, " : "";

    /// <summary>The losses by cause, zeros printed so pasted rows line up, and the mean quiet tail of the timeouts or a dash when there were none.</summary>
    public static string Losses(IReadOnlyList<GameResult> games, MapDefinition map)
    {
        var timeouts = games.Where(g => g.Cause == LossCause.Timeout).ToList();
        var tail = timeouts.Count == 0 ? "-" : timeouts.Average(g => map.TurnLimit - g.LastCombatTurn).ToString("F1", CultureInfo.InvariantCulture);
        return $"losses {LossCounts(games)}, quiet tail {tail}";
    }

    /// <summary>The losses by cause in section 7's order, zeros printed: <c>2 timeout 1 captain 0 protected</c>.</summary>
    public static string LossCounts(IReadOnlyList<GameResult> games) =>
        $"{games.Count(g => g.Cause == LossCause.Timeout)} timeout {games.Count(g => g.Cause == LossCause.Captain)} captain {games.Count(g => g.Cause == LossCause.Protected)} protected";

    /// <summary>
    /// Over the timeout losses, the median of each game's highest kill probability the veto
    /// refused, with the count of games it is over, as <c>refused kill p50 0.9994 over 16</c>, four decimals floored
    /// (<see cref="FormatKill"/>) so that only a kill that cannot fail prints on the certainty line; a dash when no timeout had a refusal (issue 125). The probability is over the strikes
    /// the attacker lives to make, the counter between them counted (issue 147). Printed, never classified: a value
    /// near one is a stall the veto caused by refusing a near-certain kill, a low value is a
    /// board the player judged too risky, and the row says which without a threshold. The
    /// count is printed so one game reads as one game and not as a finding. Beside it, over
    /// the same timeouts, the median of each game's highest refusal on its last player phase,
    /// the board it stalled on, with its own count, as <c>, at the stall p50 0.7191 over 89</c>,
    /// or <c>, at the stall -</c> when no timeout refused on its last phase (issue 157): the
    /// game's highest can sit ten turns before the stall and read as a different board.
    /// </summary>
    public static string RefusedKill(IReadOnlyList<GameResult> games)
    {
        var timeouts = games.Where(g => g.Cause == LossCause.Timeout).ToList();
        var refused = timeouts.Where(g => g.RefusedKill is not null).Select(g => g.RefusedKill!.Value).ToList();
        if (refused.Count == 0)
        {
            return "refused kill -";
        }

        var stall = timeouts.Where(g => g.StallRefusedKill is not null).Select(g => g.StallRefusedKill!.Value).ToList();
        var atStall = stall.Count == 0 ? "at the stall -" : $"at the stall p50 {FormatKill(Median(stall))} over {stall.Count}";
        return $"refused kill p50 {FormatKill(Median(refused))} over {refused.Count}, {atStall}";
    }

    /// <summary>
    /// A kill probability at four decimals, floored rather than rounded, so nothing under
    /// one prints as <c>1.0000</c> (issue 153): raw 98 and 99 under two rolls print apart
    /// from the certainty line as 0.9994 and 0.9999, and a doubled raw 99 whose either
    /// strike kills, at one minus a hundred-millionth, prints 0.9999 where rounding put it
    /// on the line. The floor is taken a hair above the value so a probability that is
    /// exactly a four-decimal number in arithmetic and a hair under it in floating point
    /// still prints as itself.
    /// </summary>
    public static string FormatKill(double probability) =>
        (Math.Floor(probability * 10000 + 1e-9) / 10000).ToString("F4", CultureInfo.InvariantCulture);

    /// <summary>Gate 2: the random legal player wins at most 5 percent of the seeds.</summary>
    public static GateResult Gate2(GameContent content, MapDefinition map, string id, int seeds, RollScheme scheme = RollScheme.TwoRollAverage)
    {
        var wins = 0;
        for (var seed = 1; seed <= seeds; seed++)
        {
            if (Runner.Play(content, map, (ulong)seed, new RandomLegalPlayer(seed), scheme: scheme).Won)
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
        var state = BattleState.From(map, content, content.Cast, 1);
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

    /// <summary>
    /// One recruit's ablation row: the paired drop, its standard error, and three action
    /// mixes over the same set of units on both sides of the bench: the recruit's own
    /// baseline mix, the rest of the cast's baseline mix, and the rest of the cast's mix
    /// with the recruit benched.
    /// On an Escape map the drop is over units out and <paramref name="WinDrop"/> carries the
    /// win-pair drop beside it, unjudged (issue 338); elsewhere it is null.
    /// </summary>
    public sealed record AblationRow(string RecruitId, double Drop, double StandardError, ActionMix Own, ActionMix RestBaseline, ActionMix RestBenched, IReadOnlyList<GameResult>? Arm = null, (double Drop, double StandardError)? WinDrop = null);

    /// <summary>
    /// Gate 4: each deployed recruit benched in turn over the baseline's seeds, outcomes
    /// paired by seed. Drop is (baseline wins - arm wins) / n; the standard error comes
    /// from the discordant seeds only, sqrt(b + c) / n. A median drop at or below zero
    /// fails the cast as a whole (DESIGN.md section 11, issue 105): benching the median
    /// recruit raises the win rate, so the deployment is wrong and no recruit is judged.
    /// Above zero a recruit fails only when drop + 2 * SE is under half the median drop.
    /// A unit whose death loses the map is never benched and never judged, because
    /// <see cref="BattleState.From"/> refuses the bench: its empty placement would lose
    /// every seed on turn 1 by section 7, so its paired drop would measure the loss
    /// condition and not the unit (issue 141). The captain's mix, and the protected
    /// recruit's on a <c>protect:</c> map, print as data on their own lines and are not
    /// in the median. Each row also prints the benched arm's losses by cause and its
    /// refused-kill median (issue 125), so a large drop beside many captain deaths or a
    /// refused kill near one is read as the veto's doing and not the recruit's. On an
    /// Escape map each row also prints the benched arm's survivors (issue 263), and the
    /// outcome paired is units out, not the win (issue 338): per seed a loss scores 0 and a
    /// win scores the captain plus the recruits out, the benched recruit left out of the
    /// baseline's count so both arms count the same units (<see cref="PairedUnitsOut"/>).
    /// The win-pair drop prints beside it, unjudged.
    /// </summary>
    public static GateResult Gate4(GameContent content, MapDefinition map, string id, IReadOnlyList<GameResult> baseline, RollScheme scheme = RollScheme.TwoRollAverage)
    {
        var opening = BattleState.From(map, content, content.Cast, 1);
        var captain = opening.UnitsOf(Side.Player).Single(u => u.IsCaptain).Id;
        var unjudged = opening.UnitsOf(Side.Player).Where(u => HeuristicPlayer.LosesTheMap(opening, u)).Select(u => u.Id).ToList();
        var recruits = opening.UnitsOf(Side.Player).Select(u => u.Id).Where(unitId => !unjudged.Contains(unitId)).ToList();
        var escape = map.Win == WinCondition.Escape;
        var rows = new List<AblationRow>();
        foreach (var recruit in recruits)
        {
            var benched = ValueList<string>.Of(recruit);
            var flippedDown = 0;
            var flippedUp = 0;
            var own = ActionMix.Zero;
            var restBaseline = ActionMix.Zero;
            var restBenched = ActionMix.Zero;
            var arms = new List<GameResult>();
            for (var seed = 1; seed <= baseline.Count; seed++)
            {
                var arm = Runner.Play(content, map, (ulong)seed, new HeuristicPlayer(), benched, scheme);
                arms.Add(arm);
                var before = baseline[seed - 1];
                if (before.Won && !arm.Won)
                {
                    flippedDown++;
                }
                else if (!before.Won && arm.Won)
                {
                    flippedUp++;
                }

                own = own.Plus(before.Mix.GetValueOrDefault(recruit, ActionMix.Zero));
                restBaseline = restBaseline.Plus(Sum(before.Mix, except: recruit));
                restBenched = restBenched.Plus(Sum(arm.Mix, except: recruit));
            }

            var n = (double)baseline.Count;
            var winDrop = (Drop: (flippedDown - flippedUp) / n, StandardError: Math.Sqrt(flippedDown + flippedUp) / n);
            var (drop, se) = escape ? PairedUnitsOut(baseline, arms, recruit) : winDrop;
            rows.Add(new AblationRow(recruit, drop, se, own, restBaseline, restBenched, arms, escape ? winDrop : null));
        }

        var median = rows.Count == 0 ? 0.0 : Median(rows.Select(r => r.Drop).ToList());
        var verdict = CastVerdict(rows);
        var failing = Judge(rows);
        var passed = verdict == CastVerdictKind.Passes && failing.Count == 0;
        var lines = new List<string>
        {
            $"gate 4 no dead weight: {id}, {recruits.Count} recruits x {baseline.Count} seeds, {(escape ? $"units out, 0 to {opening.UnitsOf(Side.Player).Count() - 1}, " : "")}median drop {median:F3}: {Verdict(passed)}",
        };
        switch (verdict)
        {
            case CastVerdictKind.BenchingRaisesWins:
                lines.Add($"  cast not earning its deployment: benching the median recruit raises the win rate by {-median:F3}; rows are data, no recruit is judged");
                break;
            case CastVerdictKind.NoOutcomesChange:
                lines.Add($"  cast not earning its deployment: the median recruit changes no outcomes (drop {median:F3}, se {MedianError(rows):F3}); rows are data, no recruit is judged");
                break;
        }

        foreach (var r in rows)
        {
            var arm = r.Arm ?? Array.Empty<GameResult>();
            var win = r.WinDrop is { } w ? $" win drop {w.Drop:F3} se {w.StandardError:F3}" : "";
            lines.Add($"  {r.RecruitId}: drop {r.Drop:F3} se {r.StandardError:F3}{win} own [{r.Own}] rest baseline [{r.RestBaseline}] rest benched [{r.RestBenched}] benched losses {LossCounts(arm)}, {RefusedKill(arm)}{(map.Win == WinCondition.Escape ? $", benched {Survivors(arm)}" : "")}{(failing.Contains(r.RecruitId) ? " DEAD WEIGHT" : "")}");
        }

        foreach (var unitId in unjudged)
        {
            var mix = ActionMix.Zero;
            foreach (var game in baseline)
            {
                mix = mix.Plus(game.Mix.GetValueOrDefault(unitId, ActionMix.Zero));
            }

            lines.Add($"  {(unitId == captain ? "captain" : "protected")} {unitId}: baseline [{mix}] never benched, not judged");
        }
        return new GateResult(string.Join('\n', lines), passed);
    }

    /// <summary>
    /// Gate 4's paired outcome on an Escape map (issue 338): the mean per-seed difference of
    /// <see cref="UnitsOut"/>, baseline minus benched arm, with <paramref name="recruit"/>
    /// counted in neither, and its <see cref="PairedDifferenceError"/>.
    /// </summary>
    public static (double Drop, double StandardError) PairedUnitsOut(IReadOnlyList<GameResult> baseline, IReadOnlyList<GameResult> arm, string recruit)
    {
        var differences = baseline.Zip(arm, (before, after) => (double)(UnitsOut(before, recruit) - UnitsOut(after, recruit))).ToList();
        return (differences.Count == 0 ? 0.0 : differences.Average(), PairedDifferenceError(differences));
    }

    /// <summary>
    /// The paired-difference standard error, the sample standard deviation of the per-seed
    /// differences over sqrt(n), zero under two differences. Gate 4 reads it on a count
    /// (issue 338); on 0/1 outcomes it agrees with the discordant-seed sqrt(b + c) / n up to
    /// the n / (n - 1) factor and the squared mean, so maps that are not Escape keep that one.
    /// </summary>
    public static double PairedDifferenceError(IReadOnlyList<double> differences)
    {
        var n = differences.Count;
        if (n < 2)
        {
            return 0.0;
        }

        var mean = differences.Average();
        return Math.Sqrt(differences.Sum(d => (d - mean) * (d - mean)) / (n - 1) / n);
    }

    /// <summary>
    /// Gate 4's per-seed score on an Escape map (issue 338): 0 for a loss, else the captain
    /// plus the recruits out, <paramref name="except"/> not counted whether or not it got out.
    /// </summary>
    public static int UnitsOut(GameResult game, string except) =>
        game.Won ? 1 + game.Out.Count(unitId => !string.Equals(unitId, except, StringComparison.Ordinal)) : 0;

    private static ActionMix Sum(IReadOnlyDictionary<string, ActionMix> mix, string except)
    {
        var total = ActionMix.Zero;
        foreach (var (unitId, part) in mix)
        {
            if (!string.Equals(unitId, except, StringComparison.Ordinal))
            {
                total = total.Plus(part);
            }
        }

        return total;
    }

    /// <summary>The map-level verdict's three outcomes (issues 105 and 115).</summary>
    public enum CastVerdictKind
    {
        Passes,
        /// <summary>The median drop is below minus twice its row's standard error: benching the median recruit raises the win rate.</summary>
        BenchingRaisesWins,
        /// <summary>The median drop is not above twice its row's standard error: the median recruit changes no outcomes, a ceiling.</summary>
        NoOutcomesChange,
    }

    /// <summary>
    /// The map-level verdict (issues 105 and 115): the cast fails unless the median paired
    /// drop is above zero by twice the standard error of the row supplying it, since a
    /// median that is noise flips the verdict on the third decimal between runs. The two
    /// failures are different findings and print different lines. Passes for an empty cast.
    /// </summary>
    public static CastVerdictKind CastVerdict(IReadOnlyList<AblationRow> rows)
    {
        if (rows.Count == 0)
        {
            return CastVerdictKind.Passes;
        }

        var median = Median(rows.Select(r => r.Drop).ToList());
        var margin = 2 * MedianError(rows);
        if (median < -margin)
        {
            return CastVerdictKind.BenchingRaisesWins;
        }

        return median > margin ? CastVerdictKind.Passes : CastVerdictKind.NoOutcomesChange;
    }

    /// <summary>Whether <see cref="CastVerdict"/> fails the cast, on either line.</summary>
    public static bool CastFails(IReadOnlyList<AblationRow> rows) => CastVerdict(rows) != CastVerdictKind.Passes;

    /// <summary>The standard error of the row supplying the median: on an even count, the larger of the two straddling rows' errors.</summary>
    public static double MedianError(IReadOnlyList<AblationRow> rows)
    {
        var sorted = rows.OrderBy(r => r.Drop).ToList();
        return sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2].StandardError
            : Math.Max(sorted[sorted.Count / 2 - 1].StandardError, sorted[sorted.Count / 2].StandardError);
    }

    /// <summary>
    /// The recruits that fail section 11's rule: <c>drop + 2 * SE &lt; 0.5 * median</c>.
    /// Empty for an empty cast, and empty when the cast fails (<see cref="CastFails"/>),
    /// since the rule is meaningless on that side of zero.
    /// </summary>
    public static IReadOnlyList<string> Judge(IReadOnlyList<AblationRow> rows)
    {
        if (rows.Count == 0 || CastFails(rows))
        {
            return Array.Empty<string>();
        }

        var median = Median(rows.Select(r => r.Drop).ToList());
        return rows.Where(r => r.Drop + 2 * r.StandardError < 0.5 * median).Select(r => r.RecruitId).ToList();
    }

    /// <summary>
    /// Gate 5's tally: every combat's displayed numbers against what the resolver did. The
    /// forecast is counted as a renderer in another process would read it, written to the
    /// protocol's JSON and read back (issue 25), so the gate runs over the protocol as well
    /// as in-process; a forecast the round trip changes is a mismatch and fails the gate.
    /// </summary>
    public sealed class ForecastTally
    {
        public int Combats { get; private set; }
        public int ProtocolMismatches { get; private set; }
        public int Strikes { get; private set; }
        public int Hits { get; private set; }
        public double ExpectedHits { get; private set; }
        public int Crits { get; private set; }
        public double ExpectedCrits { get; private set; }
        public int WrongDamage { get; private set; }

        /// <summary>Combats where a side made more strikes than its forecast's <see cref="SideForecast.StrikeCount"/>, or, with both alive at the end, a different number (issue 70).</summary>
        public int WrongStrikeCounts { get; private set; }

        /// <summary>Combats where either side fought with a multi-strike round, a gauntlet (issue 70).</summary>
        public int GauntletCombats { get; private set; }

        /// <summary>Counts one combat: the forecast asked before the attack against the strikes it produced.</summary>
        public void Count(CombatForecast forecast, CombatFought fought)
        {
            Combats++;
            var overProtocol = ProtocolJson.ReadForecast(ProtocolJson.Forecast(forecast));
            if (overProtocol != forecast)
            {
                ProtocolMismatches++;
            }

            forecast = overProtocol;
            GauntletCombats += forecast.Attacker.StrikesPerRound > 1 || forecast.Defender.StrikesPerRound > 1 ? 1 : 0;
            var bothAlive = fought.AttackerHpAfter > 0 && fought.TargetHpAfter > 0;
            var attackerMade = fought.Strikes.Count(s => s.AttackerId == fought.AttackerId);
            var defenderMade = fought.Strikes.Count - attackerMade;
            if (!StrikeCountHolds(attackerMade, forecast.Attacker.StrikeCount, bothAlive)
                || !StrikeCountHolds(defenderMade, forecast.Defender.StrikeCount, bothAlive))
            {
                WrongStrikeCounts++;
            }

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

        private static bool StrikeCountHolds(int made, int forecast, bool bothAlive) =>
            bothAlive ? made == forecast : made <= forecast;

        /// <summary>Hits and crits within three standard errors of the displayed probabilities, damage exact, at least <paramref name="minimum"/> combats.</summary>
        public GateResult Result(int minimum)
        {
            var hitSe = Math.Sqrt(Math.Max(1, ExpectedHits * (1 - ExpectedHits / Math.Max(1, Strikes))));
            var critSe = Math.Sqrt(Math.Max(1, ExpectedCrits * (1 - ExpectedCrits / Math.Max(1, Hits))));
            var hitOk = Math.Abs(Hits - ExpectedHits) <= 3 * hitSe;
            var critOk = Math.Abs(Crits - ExpectedCrits) <= 3 * critSe;
            var passed = Combats >= minimum && WrongDamage == 0 && WrongStrikeCounts == 0 && ProtocolMismatches == 0 && hitOk && critOk;
            return new GateResult(
                $"gate 5 forecast honesty: {Combats} combats ({GauntletCombats} with gauntlets), {Strikes} strikes, hits {Hits} expected {ExpectedHits:F1}, crits {Crits} expected {ExpectedCrits:F1}, damage mismatches {WrongDamage}"
                + (WrongStrikeCounts > 0 ? $", {WrongStrikeCounts} combats with a strike count the forecast did not show" : "")
                + (ProtocolMismatches > 0 ? $", {ProtocolMismatches} forecasts changed over the protocol" : "")
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
    /// count covers both the formulas and the resolver's use of them. Half the attackers
    /// with a whole weapon declare a drawn combat art (issue 68, <see cref="DrawnArt"/>),
    /// from a generator of their own so the rest of the stream draws what it drew before.
    /// One fighter in four on either side swaps its weapon for a drawn gauntlet (issue 70,
    /// <see cref="DrawnGauntlet"/>), from a third generator, so the stream covers two-strike
    /// rounds whether or not content ships a gauntlet yet.
    /// </summary>
    public static void ForecastStream(GameContent content, Gates.ForecastTally tally, int combats, int seed = 1)
    {
        var random = new Random(seed);
        var pool = new List<Unit>(content.Cast);
        foreach (var template in content.Units.Values)
        {
            for (var level = 1; level <= 10; level += 3)
            {
                pool.Add(template.ScaledTo(level, content.Class(template.ClassId)));
            }
        }

        var terrains = content.Terrain.Values.ToList();
        var arts = new Random(seed + 68);
        var fists = new Random(seed + 70);
        var counted = 0;
        for (var attempt = 0; counted < combats && attempt < combats * 20; attempt++)
        {
            var attacker = Fighter(pool[random.Next(pool.Count)], content, terrains, random);
            var defender = Fighter(pool[random.Next(pool.Count)], content, terrains, random);
            if (attacker is null || defender is null || attacker.Weapon is null || attacker.Id == defender.Id)
            {
                continue;
            }

            attacker = fists.Next(4) == 0 ? Gauntleted(attacker, fists) : attacker;
            defender = fists.Next(4) == 0 ? Gauntleted(defender, fists) : defender;
            if (!attacker.Broken && arts.Next(2) == 0)
            {
                var weapon = DrawnArt(arts).Apply(attacker.Weapon!);
                attacker = new Combatant(attacker.Unit, attacker.Class, weapon, attacker.Terrain, attacker.Hp, abilities: attacker.Abilities);
            }

            var distance = random.Next(attacker.Weapon!.MinRange, attacker.Weapon.MaxRange + 1);
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
            if (content.Weapons.TryGetValue(stack.ItemId, out var w) && unit.CanWield(w, unitClass) && !w.Heals)
            {
                weapon = w;
                break;
            }
        }

        var abilities = ValueList<Ability>.From(content.AbilitiesOf(unit).Concat(DrawnAbilities(content, random)));
        var maxHp = (unit.EffectiveStats(unitClass) + AbilityRules.Passive(abilities)).Hp;
        return new Combatant(unit, unitClass, weapon, passable[random.Next(passable.Count)], 1 + random.Next(maxHp), 0, random.Next(10) == 0, abilities: abilities);
    }

    /// <summary>
    /// Gate 5 runs with abilities on both sides (issue 66): each fighter draws, at even odds
    /// each, one ability from the content's own and one synthetic effect of each kind, a
    /// passive stat delta and a combat modifier on a random condition, so the stream covers
    /// every effect kind whether or not content ships one yet.
    /// </summary>
    public static IEnumerable<Ability> DrawnAbilities(GameContent content, Random random)
    {
        var drawn = new List<Ability>();
        if (content.Abilities.Count > 0 && random.Next(2) == 0)
        {
            drawn.Add(content.Abilities.Values.ElementAt(random.Next(content.Abilities.Count)));
        }

        if (random.Next(2) == 0)
        {
            var delta = Stats.Zero.Map((stat, _) => stat == Stat.Hp ? random.Next(0, 4) : random.Next(-2, 4));
            drawn.Add(new Ability("sim_passive", "Sim Passive", "a drawn stat delta", new StatDeltaEffect(delta)));
        }

        if (random.Next(2) == 0)
        {
            var weapons = Enum.GetValues<WeaponType>();
            var movements = Enum.GetValues<MovementType>();
            var against = new OpponentCondition(
                random.Next(2) == 0 ? weapons[random.Next(weapons.Length)] : null,
                random.Next(3) == 0 ? movements[random.Next(movements.Length)] : null);
            var modifier = new CombatModifierEffect(against, random.Next(-20, 21), random.Next(-20, 21), random.Next(-10, 16), random.Next(-10, 16));
            drawn.Add(new Ability("sim_combat", "Sim Combat", "a drawn combat modifier", modifier));
        }

        return drawn;
    }

    /// <summary>A combat art with drawn deltas (issue 68), so gate 5 covers arts whether or not content ships one yet; its weapon type and rank are the resolver's checks, not the formulas', and are not read here.</summary>
    public static CombatArtEffect DrawnArt(Random random) => new(
        WeaponType.Sword, WeaponRank.E, 1 + random.Next(3), random.Next(-2, 6), random.Next(-20, 21), random.Next(-5, 21), random.Next(0, 6), random.Next(2));

    /// <summary>A gauntlet with drawn numbers (issue 70): low Mt, high hit, a little crit, light, range 1.</summary>
    public static Weapon DrawnGauntlet(Random random) => new(
        "sim_gauntlet", "Sim Gauntlet", WeaponType.Gauntlet, 1 + random.Next(5), 70 + random.Next(31), random.Next(11), random.Next(5), 1, 1, 30, ValueList<MovementType>.Empty);

    /// <summary>The fighter with a drawn gauntlet in hand, its class widened to wield one; everything else as drawn.</summary>
    private static Combatant Gauntleted(Combatant fighter, Random random)
    {
        var unitClass = fighter.Class.CanUse(WeaponType.Gauntlet)
            ? fighter.Class
            : fighter.Class with { Weapons = fighter.Class.Weapons.Add(WeaponType.Gauntlet) };
        return new Combatant(fighter.Unit, unitClass, DrawnGauntlet(random), fighter.Terrain, fighter.Hp, abilities: fighter.Abilities);
    }

    public static string Verdict(bool passed) => passed ? "ok" : "FAILED";

    private static int Percentile(List<int> sorted, double p) => sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(p * sorted.Count) - 1)];

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2;
    }
}
