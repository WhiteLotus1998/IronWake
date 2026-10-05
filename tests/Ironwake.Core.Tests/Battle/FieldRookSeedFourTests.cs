using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Sim;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 1061's check of the Sim's seed 4 on Rook's arm of the field (DECISIONS/0250 item 6):
/// the turn-3 stop beside the sleeping rider wakes the south group itself, so <see cref="Exposure.Of"/>
/// prices the rider awake on that tile, at a sum under Rook's HP; the stop is not lethal by the
/// cycle's no-crit sum, the rider's strike leaves her standing, and she falls on turn 4 to her own
/// <c>attack ... !</c> into the rider's counter, a recruit's strike the veto does not cover (0248).
/// </summary>
public class FieldRookSeedFourTests
{
    private static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    private static MapDefinition RooksArm()
    {
        var text = File.ReadAllText(Path.Combine(Fixture.RealContentDirectory(), "maps", "the_field.map"))
            .Replace("P recruit:keziah 1,13", "P recruit:rook 1,13");
        return MapFormat.Parse("the_field.map", text, Content);
    }

    /// <summary>The Sim's game on seed 4 up to the first command <paramref name="stop"/> matches, and that command.</summary>
    private static (BattleState State, Command Command) RunUntil(Func<BattleState, Command, bool> stop)
    {
        var state = BattleState.From(RooksArm(), Content, Content.Cast, 4, RollScheme.TwoRollAverage);
        var player = new HeuristicPlayer();
        while (!state.Outcome.IsOver)
        {
            var commands = state.Phase == Side.Enemy ? EnemyAi.Plan(state, Content) : player.Next(state, Content);
            foreach (var command in commands)
            {
                if (stop(state, command))
                {
                    return (state, command);
                }

                var result = Resolver.Apply(state, Content, command);
                Assert.True(result.Accepted, result.Rejection?.Message);
                state = result.Next;
            }
        }

        throw new Xunit.Sdk.XunitException("the stop never came");
    }

    [Fact]
    public void RooksTurnThreeStopWakesTheRiderAndIsPricedAwakeUnderHerHp()
    {
        var (state, command) = RunUntil((s, c) => s.Turn == 3 && s.Phase == Side.Player && c is Move { UnitId: "rook" });
        var rook = state.Find("rook")!;

        Assert.Equal(new Move("rook", new Coord(14, 13)), command);
        var after = state.WithUnit(rook with { At = new Coord(14, 13) });
        Assert.Contains(WakeCheck.Run(state, after, Content, Array.Empty<Noise>(), Array.Empty<string>()), w => w.Group == "south");
        var sum = Exposure.Of(state, Content, rook, new Coord(14, 13));
        Assert.Equal(17, rook.Hp);
        Assert.Equal(12, sum.NoCrit);
        Assert.True(sum.NoCrit < rook.Hp);
    }

    [Fact]
    public void RookFallsOnTurnFourToHerOwnSwingIntoTheRidersCounter()
    {
        var (state, command) = RunUntil((s, c) => c is Attack { UnitId: "rook", TargetId: "rider-1" });

        Assert.Equal(4, state.Turn);
        Assert.Equal(5, state.Find("rook")!.Hp);
        var result = Resolver.Apply(state, Content, command);
        Assert.Contains(result.Events, e => e is UnitDied { UnitId: "rook" });
    }
}
