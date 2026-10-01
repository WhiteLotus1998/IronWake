using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The break (DESIGN.md 13.22, experiment): on a <c>break: on</c> map, when a boss dies, every
/// living member of his group at or below half its max HP leaves the board, one
/// <see cref="UnitBroke"/> each, no death and no EXP. A member above half, a member of another
/// group, and every unit on a map without the header fights on.
/// </summary>
public class BreakTests
{
    private static string Field(bool breaks) =>
        $"""
        name: Field
        size: 7x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(breaks ? "break: on" : "")}

        .......
        .......
        .......
        .......
        .......

        units:
        P captain 1,2
        P recruit:wren 0,4
        B bandit_leader 2,2 group:keep behavior:boss
        E soldier 6,0 group:keep behavior:hold
        E archer 6,4 group:keep behavior:hold
        E brigand 4,4 group:road behavior:hold

        """.Replace("\n\n\n", "\n\n");

    /// <summary>The field with the boss at 1 HP and every other enemy at <paramref name="hp"/>.</summary>
    private static BattleState Wounded(bool breaks, int hp, ulong seed = 7)
    {
        var state = BattleFixture.Start(seed, map: Field(breaks));
        foreach (var enemy in state.UnitsOf(Side.Enemy).ToList())
        {
            state = state.WithUnit(enemy with { Hp = enemy.IsBoss ? 1 : Math.Min(hp, enemy.Hp) });
        }

        return state;
    }

    /// <summary>The captain's strike on the boss, on the first seed from 1 on which it kills him.</summary>
    private static ApplyResult Behead(bool breaks, int hp)
    {
        for (ulong seed = 1; seed < 200; seed++)
        {
            var result = Wounded(breaks, hp, seed).Try(new Attack("hale", "bandit_leader-1"));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (result.Next.Find("bandit_leader-1") is null)
            {
                return result;
            }
        }

        throw new InvalidOperationException("no seed under 200 kills the boss");
    }

    [Fact]
    public void WhenABossFallsHisGroupAtOrBelowHalfBreaksAndLeavesTheBoard()
    {
        var result = Behead(breaks: true, hp: 1);

        Assert.Null(result.Next.Find("soldier-1"));
        Assert.Null(result.Next.Find("archer-1"));
        Assert.Contains(result.Events, e => e is UnitBroke { UnitId: "soldier-1" });
        Assert.Contains(result.Events, e => e is UnitBroke { UnitId: "archer-1" });
        Assert.DoesNotContain(result.Events, e => e is UnitDied { UnitId: "soldier-1" or "archer-1" });
    }

    [Fact]
    public void ABossWhoDiesOnACounterInHisOwnPhaseBreaksHisGroupAtOnce()
    {
        for (ulong seed = 1; seed < 200; seed++)
        {
            var state = Wounded(breaks: true, hp: 1, seed).Try(new EndPhase()).Next;
            var events = new List<GameEvent>();
            foreach (var command in EnemyAi.Plan(state, Starter))
            {
                var step = state.Try(command);
                if (!step.Accepted)
                {
                    break;
                }

                events.AddRange(step.Events);
                state = step.Next;
            }

            var died = events.FindIndex(e => e is UnitDied { UnitId: "bandit_leader-1" });
            if (died < 0)
            {
                continue;
            }

            var broke = events.FindIndex(e => e is UnitBroke { UnitId: "soldier-1" });
            Assert.True(broke > died);
            Assert.DoesNotContain(events.Skip(died), e => e is CombatFought { AttackerId: "soldier-1" or "archer-1" });
            return;
        }

        throw new InvalidOperationException("no seed under 200 has the boss die on a counter");
    }

    [Fact]
    public void ABrokenUnitIsGoneForRoutSoABreakThatEmptiesTheBoardWinsOnThatCommand()
    {
        var map = Field(true).Replace("E brigand 4,4 group:road behavior:hold\n", "");
        for (ulong seed = 1; seed < 200; seed++)
        {
            var state = BattleFixture.Start(seed, map: map);
            foreach (var enemy in state.UnitsOf(Side.Enemy).ToList())
            {
                state = state.WithUnit(enemy with { Hp = 1 });
            }

            Assert.Equal(BattleResult.Ongoing, state.Outcome.Result);
            var result = state.Try(new Attack("hale", "bandit_leader-1"));
            Assert.True(result.Accepted, result.Rejection?.Message);
            if (result.Next.Find("bandit_leader-1") is not null)
            {
                continue;
            }

            Assert.Contains(result.Events, e => e is UnitBroke { UnitId: "soldier-1" });
            Assert.Contains(result.Events, e => e is UnitBroke { UnitId: "archer-1" });
            Assert.Empty(result.Next.UnitsOf(Side.Enemy));
            Assert.Equal(BattleResult.Won, result.Next.Outcome.Result);
            return;
        }

        throw new InvalidOperationException("no seed under 200 kills the boss");
    }

    [Fact]
    public void AMemberOfAnotherGroupNeverBreaks()
    {
        var result = Behead(breaks: true, hp: 1);

        Assert.NotNull(result.Next.Find("brigand-1"));
        Assert.DoesNotContain(result.Events, e => e is UnitBroke { UnitId: "brigand-1" });
    }

    [Fact]
    public void AMemberAboveHalfFightsOn()
    {
        var result = Behead(breaks: true, hp: 999);

        Assert.NotNull(result.Next.Find("soldier-1"));
        Assert.NotNull(result.Next.Find("archer-1"));
        Assert.DoesNotContain(result.Events, e => e is UnitBroke);
    }

    [Fact]
    public void ExactlyHalfBreaks()
    {
        var state = Wounded(breaks: true, hp: 999);
        var soldier = state.Find("soldier-1")!;
        var max = soldier.MaxHp(Starter);
        Assert.True(Break.Wavers(soldier with { Hp = max / 2 }, Starter));
        Assert.False(Break.Wavers(soldier with { Hp = max / 2 + 1 }, Starter));
    }

    [Fact]
    public void WithoutTheHeaderNothingBreaks()
    {
        var result = Behead(breaks: false, hp: 1);

        Assert.NotNull(result.Next.Find("soldier-1"));
        Assert.DoesNotContain(result.Events, e => e is UnitBroke);
        Assert.Empty(Break.WouldBreak(result.Next, Starter, Wounded(false, 1).Find("bandit_leader-1")!));
    }

    [Fact]
    public void ABreakIsNotAKillAndGivesNoExp()
    {
        var result = Behead(breaks: true, hp: 1);

        var exp = result.Events.OfType<ExpGained>().Where(x => x.UnitId == "hale").Sum(x => x.Amount);
        var alone = Behead(breaks: false, hp: 1).Events.OfType<ExpGained>().Where(x => x.UnitId == "hale").Sum(x => x.Amount);
        Assert.Equal(alone, exp);
        Assert.Single(result.Events.OfType<UnitDied>());
    }

    [Fact]
    public void ARecallBringsTheBrokenBack()
    {
        var result = Behead(breaks: true, hp: 1);

        var back = result.Next.Try(new Recall(0));

        Assert.True(back.Accepted, back.Rejection?.Message);
        Assert.NotNull(back.Next.Find("soldier-1"));
        Assert.NotNull(back.Next.Find("bandit_leader-1"));
    }

    [Fact]
    public void TheForecastOnABossNamesWhoWouldBreak()
    {
        var state = Wounded(breaks: true, hp: 1);
        var lines = PlaySession.BreakLines(state, Starter, state.Find("hale")!, state.Find("bandit_leader-1")!).ToList();

        var line = Assert.Single(lines);
        Assert.StartsWith("  break if bandit_leader-1 falls: archer-1 (1/", line);
        Assert.Contains("soldier-1 (1/", line);
        Assert.DoesNotContain("brigand-1", line);
        Assert.Empty(PlaySession.BreakLines(Wounded(breaks: true, hp: 999), Starter, state.Find("hale")!, state.Find("bandit_leader-1")!));
        Assert.Empty(PlaySession.BreakLines(state, Starter, state.Find("hale")!, state.Find("soldier-1")!));
    }

    [Fact]
    public void TheBreakHeaderRoundTripsAndTheSampleCarriesIt()
    {
        var map = MapFixture.Parse(Field(true), "field.map");
        Assert.True(map.BreakEnabled);
        Assert.Contains("break: on\n", MapFormat.Write(map, Starter));
        Assert.Contains(MapRenderer.BreakLegend, MapRenderer.Render(map, Starter));
        Assert.DoesNotContain(MapRenderer.BreakLegend, MapRenderer.Render(MapFixture.Parse(Field(false), "field.map"), Starter));

        var repo = Directory.GetParent(Fixture.RealContentDirectory())!.FullName;
        foreach (var name in new[] { "the_tollgate_break.map", "saltmarsh_ford_break.map" })
        {
            var path = Path.Combine(repo, "docs", "samples", name);
            var sample = MapFiles.Load(path, MapFixture.Content);
            Assert.True(sample.BreakEnabled);
            Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), MapFormat.Write(sample, Starter));
        }
    }

    [Fact]
    public void TheBrokeEventHasAProtocolShape()
    {
        Assert.Equal(
            """{"type":"unitBroke","unit":"soldier-1","at":{"x":6,"y":0},"hp":4}""",
            ProtocolJson.Event(new UnitBroke("soldier-1", new Coord(6, 0), 4)));
    }
}
