using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Gate 6 (DESIGN.md section 11): 100 seeds times 100 random legal commands, replayed,
/// byte-identical through <see cref="BattleState.Canonical"/>. The random player here
/// is the test's, seeded by xUnit's process-independent arithmetic, never the engine's.
/// </summary>
public class DeterminismTests
{
    public const int Seeds = 100;
    public const int CommandsPerRun = 100;

    [Fact]
    public void AHundredSeedsOfRandomLegalCommandsReplayByteIdentical()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);
        var roster = ValueList<Unit>.Of(Hale, Wren);
        var attacks = 0;
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var (commands, first) = Play(map, roster, (ulong)seed, new Random(seed));
            var second = Replay(map, roster, (ulong)seed, commands);

            Assert.Equal(first.Canonical, second.Canonical);
            Assert.Equal(first.Events, second.Events);
            Assert.True(commands.Count == CommandsPerRun || first.Over, "a run stopped short of its commands without an outcome");
            attacks += commands.Count(c => c is Attack);
        }

        Assert.True(attacks > 0, "the random player never attacked, so the rolls were never exercised");
    }

    /// <summary>Issue 71: the same gate with an outrider on the roster, so Canto commands are in the random stream.</summary>
    [Fact]
    public void AHundredSeedsWithCantoInTheRandomStreamReplayByteIdentical()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);
        var rider = Recruit("wren", "outrider", Wren.Stats, "iron_lance");
        var roster = ValueList<Unit>.Of(Hale, rider);
        var cantos = 0;
        for (var seed = 1; seed <= Seeds; seed++)
        {
            var (commands, first) = Play(map, roster, (ulong)seed, new Random(seed));
            var second = Replay(map, roster, (ulong)seed, commands);

            Assert.Equal(first.Canonical, second.Canonical);
            Assert.Equal(first.Events, second.Events);
            cantos += commands.Count(c => c is Canto);
        }

        Assert.True(cantos > 0, "the random player never took a Canto, so the gate never exercised it");
    }

    [Fact]
    public void ARunNeverRejectsALegalCommandAndNeverThrows()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);
        var state = BattleState.From(map, Starter, ValueList<Unit>.Of(Hale, Wren), 3);
        var random = new Random(3);
        for (var i = 0; i < 400 && !state.Outcome.IsOver; i++)
        {
            var command = Pick(Legal(state), random);
            var result = Resolver.Apply(state, Starter, command);
            Assert.True(result.Accepted, $"{command} was rejected: {result.Rejection?.Message}");
            state = result.Next;
        }
    }

    private static (List<Command> Commands, Run Run) Play(MapDefinition map, ValueList<Unit> roster, ulong seed, Random random)
    {
        var state = BattleState.From(map, Starter, roster, seed);
        var commands = new List<Command>();
        var events = new List<string>();
        var canonical = new List<string>();
        while (commands.Count < CommandsPerRun && !state.Outcome.IsOver)
        {
            var command = Pick(Legal(state), random);
            var result = Resolver.Apply(state, Starter, command);
            Assert.True(result.Accepted, result.Rejection?.Message);
            commands.Add(command);
            events.Add(string.Join("|", result.Events));
            state = result.Next;
            canonical.Add(state.Canonical());
        }

        return (commands, new Run(string.Join("\n", canonical), events, state.Outcome.IsOver));
    }

    private static Run Replay(MapDefinition map, ValueList<Unit> roster, ulong seed, List<Command> commands)
    {
        var state = BattleState.From(map, Starter, roster, seed);
        var events = new List<string>();
        var canonical = new List<string>();
        foreach (var command in commands)
        {
            var result = Resolver.Apply(state, Starter, command);
            Assert.True(result.Accepted, result.Rejection?.Message);
            events.Add(string.Join("|", result.Events));
            state = result.Next;
            canonical.Add(state.Canonical());
        }

        return new Run(string.Join("\n", canonical), events, state.Outcome.IsOver);
    }

    private sealed record Run(string Canonical, List<string> Events, bool Over);

    private static Command Pick(List<Command> legal, Random random) => legal[random.Next(legal.Count)];

    /// <summary>The resolver's legal commands plus, when a charge is left, one Recall to the middle of the player-phase states in the history.</summary>
    public static List<Command> Legal(BattleState state)
    {
        var legal = Resolver.Legal(state, Starter).ToList();
        var targets = state.RecallTargets().ToList();
        if (state.RecallCharges > 0 && targets.Count > 0)
        {
            legal.Add(new Recall(targets[targets.Count / 2]));
        }

        return legal;
    }
}
