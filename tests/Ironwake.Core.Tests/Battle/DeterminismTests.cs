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
            Assert.Equal(CommandsPerRun, commands.Count);
            attacks += commands.Count(c => c is Attack);
        }

        Assert.True(attacks > 0, "the random player never attacked, so the rolls were never exercised");
    }

    [Fact]
    public void ARunNeverRejectsALegalCommandAndNeverThrows()
    {
        var map = MapFixture.Parse(MapFixture.OldMillRoad);
        var state = BattleState.From(map, Starter, ValueList<Unit>.Of(Hale, Wren), 3);
        var random = new Random(3);
        for (var i = 0; i < 400; i++)
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
        while (commands.Count < CommandsPerRun)
        {
            var command = Pick(Legal(state), random);
            var result = Resolver.Apply(state, Starter, command);
            Assert.True(result.Accepted, result.Rejection?.Message);
            commands.Add(command);
            events.Add(string.Join("|", result.Events));
            state = result.Next;
            canonical.Add(state.Canonical());
        }

        return (commands, new Run(string.Join("\n", canonical), events));
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

        return new Run(string.Join("\n", canonical), events);
    }

    private sealed record Run(string Canonical, List<string> Events);

    private static Command Pick(List<Command> legal, Random random) => legal[random.Next(legal.Count)];

    /// <summary>
    /// Every legal command in the state: each unacted unit's moves, attacks, and Wait,
    /// EndPhase, and (at most one, so a long history does not drown the rest) a Recall.
    /// </summary>
    public static List<Command> Legal(BattleState state)
    {
        var legal = new List<Command>();
        foreach (var unit in state.UnitsOf(state.Phase))
        {
            if (unit.Acted)
            {
                continue;
            }

            if (!unit.Moved)
            {
                foreach (var to in state.ReachOf(unit, Starter).Destinations)
                {
                    if (to != unit.At)
                    {
                        legal.Add(new Move(unit.Id, to));
                    }
                }
            }

            var weapon = unit.EquippedWeapon(Starter);
            if (weapon is not null)
            {
                foreach (var target in state.UnitsOf(state.Phase == Side.Player ? Side.Enemy : Side.Player))
                {
                    if (weapon.InRange(unit.At.DistanceTo(target.At)))
                    {
                        legal.Add(new Attack(unit.Id, target.Id));
                    }
                }
            }

            legal.Add(new Wait(unit.Id));
        }

        legal.Add(new EndPhase());
        if (state.RecallCharges > 0 && state.History.Count > 0)
        {
            legal.Add(new Recall(state.History.Count / 2));
        }

        return legal;
    }
}
