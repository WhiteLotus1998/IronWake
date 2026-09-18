using System.Diagnostics;
using Ironwake.Content;
using Ironwake.Core;

namespace Ironwake.Sim;

/// <summary>
/// Headless harness. Runs the quality gates from DESIGN.md section 11.
/// --smoke runs the per-PR gates over every map under content/maps; --full runs the
/// per-map gates (1-4). Gates 6 (determinism, over random commands and over the enemy
/// AI's phases), 7 (a full map of the random player against the enemy AI under a second)
/// and 8 (crash-free) run now; gate 5 (forecast honesty over the resolver) registers when
/// the Sim fights enough combats to count. Until issue 13 lands the cast, the player's
/// roster is five synthetic cadets with iron swords, and the output says so.
/// </summary>
public static class Program
{
    private const int Gate6Seeds = 100;
    private const int Gate6Commands = 100;
    private const int Gate8Sequences = 10000;
    private const int Gate8Commands = 20;
    private const int Gate7Seeds = 5;
    private const int Gate7LimitMs = 1000;

    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--smoke")
        {
            return Smoke();
        }

        if (args.Length > 0 && args[0] == "--full")
        {
            Console.WriteLine("full: no gates registered yet");
            return 0;
        }

        Console.WriteLine("usage: ironwake-sim --smoke | --full <map> | --full --all");
        return 2;
    }

    private static int Smoke()
    {
        var contentDir = FindContent();
        if (contentDir is null)
        {
            Console.WriteLine("smoke: no content directory found from the working directory or the build output");
            return 1;
        }

        var content = ContentLoader.Load(contentDir);
        var maps = MapFiles.LoadAll(contentDir, content);
        Console.WriteLine($"smoke: {maps.Count} maps from {contentDir}; roster is {Roster.Count} synthetic cadets until issue 13");
        var failed = false;
        failed |= !Gate6(content, maps);
        failed |= !Gate7(content, maps);
        failed |= !Gate8(content, maps);
        Console.WriteLine("gate 5 forecast honesty: waiting on a Sim that counts combats");
        Console.WriteLine(failed ? "smoke: FAILED" : "smoke: ok");
        return failed ? 1 : 0;
    }

    /// <summary>Gate 6: the same seed and commands replay byte-identical through <see cref="BattleState.Canonical"/>.</summary>
    private static bool Gate6(GameContent content, IReadOnlyList<(string Id, MapDefinition Map)> maps)
    {
        var watch = Stopwatch.StartNew();
        var ok = true;
        foreach (var (id, map) in maps)
        {
            for (var seed = 1; seed <= Gate6Seeds; seed++)
            {
                var commands = new List<Command>();
                var random = new Random(seed);
                var (first, firstEvents) = Run(content, map, (ulong)seed, Gate6Commands, random, commands, null);
                var (second, secondEvents) = Run(content, map, (ulong)seed, Gate6Commands, null, null, commands);
                if (first != second || firstEvents != secondEvents)
                {
                    Console.WriteLine($"gate 6 determinism: {id} seed {seed} replayed differently");
                    ok = false;
                    break;
                }
            }
        }

        Console.WriteLine($"gate 6 determinism: {maps.Count} maps x {Gate6Seeds} seeds x {Gate6Commands} commands, {watch.ElapsedMilliseconds} ms: {(ok ? "ok" : "FAILED")}");
        return ok;
    }

    /// <summary>
    /// Gate 7: a full map, the random player against <see cref="EnemyAi.Plan"/>, resolves
    /// in under a second; the same seed replayed lands on the same canonical state, which
    /// is gate 6 over the AI's phases. The slowest game per map is what is printed and judged.
    /// </summary>
    private static bool Gate7(GameContent content, IReadOnlyList<(string Id, MapDefinition Map)> maps)
    {
        var ok = true;
        foreach (var (id, map) in maps)
        {
            long slowest = 0;
            var turns = 0;
            for (var seed = 1; seed <= Gate7Seeds; seed++)
            {
                var watch = Stopwatch.StartNew();
                var (first, firstTurns) = FullGame(content, map, (ulong)seed);
                watch.Stop();
                slowest = Math.Max(slowest, watch.ElapsedMilliseconds);
                turns += firstTurns;
                var (second, _) = FullGame(content, map, (ulong)seed);
                if (first != second)
                {
                    Console.WriteLine($"gate 6 determinism: {id} seed {seed} AI-vs-AI game replayed differently");
                    ok = false;
                }
            }

            var fast = slowest < Gate7LimitMs;
            ok &= fast;
            Console.WriteLine($"gate 7 speed: {id}, {Gate7Seeds} AI-vs-AI games, {turns} turns, slowest {slowest} ms: {(fast ? "ok" : "FAILED")}");
        }

        return ok;
    }

    /// <summary>The random player against the enemy AI until the battle is decided. Returns the canonical end state and the turns played.</summary>
    private static (string State, int Turns) FullGame(GameContent content, MapDefinition map, ulong seed)
    {
        var state = BattleState.From(map, content, Roster, seed);
        var random = new Random((int)seed);
        while (!state.Outcome.IsOver)
        {
            if (state.Phase == Side.Player)
            {
                var legal = Resolver.Legal(state, content).ToList();
                state = Apply(state, content, legal[random.Next(legal.Count)]);
            }
            else
            {
                foreach (var command in EnemyAi.Plan(state, content))
                {
                    state = Apply(state, content, command);
                }
            }
        }

        return (state.Canonical(), state.Turn);
    }

    private static BattleState Apply(BattleState state, GameContent content, Command command)
    {
        var result = Resolver.Apply(state, content, command);
        if (!result.Accepted)
        {
            throw new InvalidOperationException($"{command} was rejected: {result.Rejection!.Message}");
        }

        return result.Next;
    }

    /// <summary>Gate 8: random legal command sequences throw nothing and are never rejected.</summary>
    private static bool Gate8(GameContent content, IReadOnlyList<(string Id, MapDefinition Map)> maps)
    {
        var watch = Stopwatch.StartNew();
        var perMap = Math.Max(1, Gate8Sequences / Math.Max(1, maps.Count));
        var ok = true;
        foreach (var (id, map) in maps)
        {
            for (var sequence = 1; sequence <= perMap && ok; sequence++)
            {
                try
                {
                    Run(content, map, (ulong)sequence, Gate8Commands, new Random(sequence), null, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"gate 8 crash-free: {id} sequence {sequence}: {ex.GetType().Name}: {ex.Message}");
                    ok = false;
                }
            }
        }

        Console.WriteLine($"gate 8 crash-free: {maps.Count} maps x {perMap} sequences x {Gate8Commands} commands, {watch.ElapsedMilliseconds} ms: {(ok ? "ok" : "FAILED")}");
        return ok;
    }

    /// <summary>
    /// Plays random legal commands (with a Recall now and then) from <paramref name="random"/>,
    /// or replays <paramref name="replay"/>, recording what was played into
    /// <paramref name="record"/>. Returns the final canonical state and the event log.
    /// A rejected command is a harness fault and throws.
    /// </summary>
    private static (string State, string Events) Run(
        GameContent content, MapDefinition map, ulong seed, int commands, Random? random, List<Command>? record, List<Command>? replay)
    {
        var state = BattleState.From(map, content, Roster, seed);
        var events = new System.Text.StringBuilder();
        for (var i = 0; i < commands; i++)
        {
            Command command;
            if (replay is not null)
            {
                if (i >= replay.Count)
                {
                    break;
                }

                command = replay[i];
            }
            else
            {
                var legal = Resolver.Legal(state, content).ToList();
                if (state.RecallCharges > 0 && state.History.Count > 0)
                {
                    legal.Add(new Recall(state.History.Count / 2));
                }

                if (legal.Count == 0)
                {
                    break;
                }

                command = legal[random!.Next(legal.Count)];
            }

            var result = Resolver.Apply(state, content, command);
            if (!result.Accepted)
            {
                throw new InvalidOperationException($"{command} was rejected: {result.Rejection!.Message}");
            }

            record?.Add(command);
            foreach (var e in result.Events)
            {
                events.Append(e).Append('\n');
            }

            state = result.Next;
        }

        return (state.Canonical(), events.ToString());
    }

    private static readonly ValueList<Unit> Roster = ValueList<Unit>.Of(
        Cadet("captain", new Stats(22, 8, 0, 7, 8, 6, 5, 2, 9)),
        Cadet("wren", new Stats(20, 7, 0, 6, 8, 5, 4, 2, 3)),
        Cadet("recruit-2", new Stats(19, 6, 0, 5, 7, 4, 4, 3, 4)),
        Cadet("recruit-3", new Stats(21, 7, 0, 5, 6, 3, 5, 2, 2)),
        Cadet("recruit-4", new Stats(18, 5, 0, 8, 9, 5, 3, 3, 5)));

    private static Unit Cadet(string id, Stats stats) =>
        new(id, id, "cadet", 1, 0, stats, Stats.Zero, new Inventory(ValueList<ItemStack>.Of(new ItemStack("iron_sword", 40))), ValueList<string>.Empty);

    private static string? FindContent()
    {
        if (File.Exists(Path.Combine("content", ContentFiles.TerrainName)))
        {
            return Path.GetFullPath("content");
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "content");
            if (File.Exists(Path.Combine(candidate, ContentFiles.TerrainName)))
            {
                return candidate;
            }
        }

        return null;
    }
}
