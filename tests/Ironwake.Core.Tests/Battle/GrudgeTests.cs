using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Named rivals, the grudge arm (DESIGN.md 13.4, experiment): on a <c>grudges: on</c> map a
/// player unit that kills an enemy is sworn against by every living enemy of that enemy's
/// group; the planner strikes the sworn unit whenever any tile reaches it, takes its best
/// other strike only when none does, and approaches the sworn unit first. The second pass
/// (issue 331): a keepsake strike outranks the grudge, a grudge strike names the planner's
/// best alternative, at dusk only witnesses swear, and the sworn unit fights the enemy
/// sworn on it at -20 crit evade, counters included.
/// </summary>
public class GrudgeTests
{
    private static string Yard(bool grudges, int? dusk = null) =>
        $"""
        name: Yard
        size: 8x4
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {(grudges ? "grudges: on" : "")}
        {(dusk is { } sight ? $"dusk: {sight}" : "")}

        ........
        ........
        ........
        ........

        units:
        P captain 0,1
        P recruit:wren 0,2
        E brigand 3,1 group:yard behavior:aggressive
        E soldier 7,3 group:yard behavior:aggressive
        E archer 7,0 group:tower behavior:hold

        """.Replace("\n\n\n\n", "\n\n").Replace("\n\n\n", "\n\n");

    /// <summary>A lane twelve wide: the captain, Wren, and one soldier, placed by the test.</summary>
    private static BattleState Lane(Coord hale, Coord wren, Coord soldier, bool keepsakes = false)
    {
        var map = $"""
            name: Lane
            size: 12x4
            win: rout
            turn_limit: 10
            recall: 3
            enemy_level: 1
            grudges: on
            {(keepsakes ? "keepsakes: on" : "")}

            ............
            ............
            ............
            ............

            units:
            P captain {hale.X},{hale.Y}
            P recruit:wren {wren.X},{wren.Y}
            E soldier {soldier.X},{soldier.Y} group:lane behavior:aggressive

            """;
        return Start(map: map.Replace("\n\n\n", "\n\n")).Do(new EndPhase());
    }

    /// <summary>Wren at 2,1 strikes the brigand at 1 HP: the first seed on which the brigand dies and Wren lives.</summary>
    private static ApplyResult WrenKillsTheBrigand(bool grudges = true, int? dusk = null)
    {
        for (ulong seed = 1; seed < 200; seed++)
        {
            var state = Start(seed, map: Yard(grudges, dusk));
            state = state.WithUnit(state.Find("brigand-1")! with { Hp = 1 }).Do(new Move("wren", new Coord(2, 1)));
            var result = state.Try(new Attack("wren", "brigand-1"));
            if (result.Next.Find("brigand-1") is null && result.Next.Find("wren") is not null)
            {
                return result;
            }
        }

        throw new InvalidOperationException("no seed below 200 lets Wren kill the brigand");
    }

    [Fact]
    public void AKillSwearsTheDeadEnemysGroupAgainstTheKiller()
    {
        var kill = WrenKillsTheBrigand();

        Assert.Equal("wren", kill.Next.Find("soldier-1")!.Grudge);
        Assert.Contains(new GrudgeSworn("soldier-1", "wren"), kill.Events);
    }

    [Fact]
    public void AnEnemyOfAnotherGroupSwearsNothing()
    {
        var kill = WrenKillsTheBrigand();

        Assert.Null(kill.Next.Find("archer-1")!.Grudge);
        Assert.DoesNotContain(kill.Events, e => e is GrudgeSworn { UnitId: "archer-1" });
    }

    [Fact]
    public void AMapWithoutTheHeaderSwearsNoGrudge()
    {
        var kill = WrenKillsTheBrigand(grudges: false);

        Assert.Null(kill.Next.Find("soldier-1")!.Grudge);
        Assert.DoesNotContain(kill.Events, e => e is GrudgeSworn);
    }

    [Fact]
    public void ACounterKillSwearsTheGroupAgainstTheUnitThatCountered()
    {
        for (ulong seed = 1; seed < 200; seed++)
        {
            var state = Start(seed, map: Yard(grudges: true)).Do(new EndPhase());
            state = state.WithUnit(state.Find("brigand-1")! with { Hp = 1, At = new Coord(1, 2) });
            var result = state.Try(new Attack("brigand-1", "wren"));
            if (result.Next.Find("brigand-1") is null && result.Next.Find("wren") is not null)
            {
                Assert.Equal("wren", result.Next.Find("soldier-1")!.Grudge);
                return;
            }
        }

        throw new InvalidOperationException("no seed below 200 lets Wren's counter kill the brigand");
    }

    [Fact]
    public void ANewerKillReplacesTheGrudge()
    {
        var state = WrenKillsTheBrigand().Next;
        var archer = state.Find("archer-1")! with { Group = "yard" };
        state = state.WithUnit(archer).WithUnit(state.Find("soldier-1")! with { Hp = 1, At = new Coord(0, 0) });

        for (ulong seed = 1; seed < 200; seed++)
        {
            var result = (state with { Seed = seed }).Try(new Attack("hale", "soldier-1"));
            if (result.Next.Find("soldier-1") is null && result.Next.Find("hale") is not null)
            {
                Assert.Equal("hale", result.Next.Find("archer-1")!.Grudge);
                return;
            }
        }

        throw new InvalidOperationException("no seed below 200 lets the captain kill the soldier");
    }

    [Fact]
    public void ASwornEnemyStrikesTheSwornUnitOverABetterScoringKill()
    {
        var state = Lane(hale: new Coord(4, 0), wren: new Coord(4, 2), soldier: new Coord(5, 1));
        state = state.WithUnit(state.Find("hale")! with { Hp = 1 });
        var soldier = state.Find("soldier-1")!;

        var unsworn = EnemyAi.PlanUnit(state, Starter, soldier);
        var sworn = EnemyAi.PlanUnit(state.WithUnit(soldier with { Grudge = "wren" }), Starter, soldier with { Grudge = "wren" });

        Assert.Equal("hale", unsworn.OfType<Attack>().Single().TargetId);
        Assert.Equal("wren", sworn.OfType<Attack>().Single().TargetId);
    }

    [Fact]
    public void ASwornEnemyThatCannotReachTheSwornUnitTakesItsBestOtherStrike()
    {
        var state = Lane(hale: new Coord(4, 0), wren: new Coord(11, 3), soldier: new Coord(5, 1));
        var soldier = state.Find("soldier-1")! with { Grudge = "wren" };

        var plan = EnemyAi.PlanUnit(state.WithUnit(soldier), Starter, soldier);

        Assert.Equal("hale", plan.OfType<Attack>().Single().TargetId);
    }

    [Fact]
    public void ASwornEnemyThatCanStrikeNobodyApproachesTheSwornUnitFirst()
    {
        var state = Lane(hale: new Coord(0, 3), wren: new Coord(11, 0), soldier: new Coord(5, 1));
        var soldier = state.Find("soldier-1")!;

        var unsworn = EnemyAi.PlanUnit(state, Starter, soldier).OfType<Move>().Single();
        var sworn = EnemyAi.PlanUnit(state.WithUnit(soldier with { Grudge = "wren" }), Starter, soldier with { Grudge = "wren" }).OfType<Move>().Single();

        Assert.True(unsworn.To.X < soldier.At.X, $"unsworn moved to {unsworn.To}");
        Assert.True(sworn.To.X > soldier.At.X, $"sworn moved to {sworn.To}");
    }

    [Fact]
    public void StrikeOnNamesNoStrikeOnAnotherUnitWhileTheSwornUnitIsInReach()
    {
        var state = Lane(hale: new Coord(4, 0), wren: new Coord(4, 2), soldier: new Coord(5, 1));
        var soldier = state.Find("soldier-1")! with { Grudge = "wren" };
        state = state.WithUnit(soldier);

        Assert.Null(EnemyAi.StrikeOn(state, Starter, soldier, state.Find("hale")!));
        Assert.NotNull(EnemyAi.StrikeOn(state, Starter, soldier, state.Find("wren")!));
    }

    [Fact]
    public void StrikeOnNamesTheOtherStrikeWhenTheSwornUnitIsOutOfReach()
    {
        var state = Lane(hale: new Coord(4, 0), wren: new Coord(11, 3), soldier: new Coord(5, 1));
        var soldier = state.Find("soldier-1")! with { Grudge = "wren" };
        state = state.WithUnit(soldier);

        Assert.NotNull(EnemyAi.StrikeOn(state, Starter, soldier, state.Find("hale")!));
    }

    [Fact]
    public void TheEnemyRowNamesTheSwornUnit()
    {
        var kill = WrenKillsTheBrigand();

        Assert.Contains("group yard, aggressive, sworn: wren", MapRenderer.Render(kill.Next, Starter));
    }

    [Fact]
    public void TheHeaderRoundTripsThroughTheMapFormat()
    {
        var map = MapFixture.Parse(Yard(grudges: true));

        Assert.True(map.GrudgesEnabled);
        Assert.Contains("grudges: on\n", MapFormat.Write(map, Starter));
        Assert.Equal(map, MapFixture.Parse(MapFormat.Write(map, Starter)));
        Assert.False(MapFixture.Parse(Yard(grudges: false)).GrudgesEnabled);
    }

    [Fact]
    public void AGrudgeRoundTripsThroughTheProtocol()
    {
        var state = WrenKillsTheBrigand().Next;

        var json = ProtocolJson.State(state, Starter);

        Assert.Contains("\"grudge\":\"wren\"", json);
        Assert.Equal("wren", ProtocolJson.ReadState(json, Starter).Find("soldier-1")!.Grudge);
    }

    [Fact]
    public void RecallTakesTheGrudgeBackWithTheKill()
    {
        var state = WrenKillsTheBrigand().Next;

        var back = state.Do(new Recall(0));

        Assert.Null(back.Find("soldier-1")!.Grudge);
    }

    [Fact]
    public void AtDuskOnlyAWitnessOfTheKillSwears()
    {
        var dark = WrenKillsTheBrigand(dusk: 2);
        var lit = WrenKillsTheBrigand(dusk: 8);

        Assert.Null(dark.Next.Find("soldier-1")!.Grudge);
        Assert.DoesNotContain(dark.Events, e => e is GrudgeSworn);
        Assert.Equal("wren", lit.Next.Find("soldier-1")!.Grudge);
    }

    [Fact]
    public void AKeepsakeStrikeOutranksTheGrudge()
    {
        var state = Lane(hale: new Coord(9, 1), wren: new Coord(4, 2), soldier: new Coord(5, 1), keepsakes: true);
        var soldier = state.Find("soldier-1")! with { Grudge = "wren" };
        state = state.WithUnit(soldier);
        var withStack = state with { Keepsakes = ValueList<Keepsake>.Of(new Keepsake(new Coord(8, 1), "teodor", new ItemStack("iron_lance", 20) { Keepsake = "teodor" })) };

        var plain = EnemyAi.PlanUnit(state, Starter, soldier).OfType<Attack>().Single();
        var held = EnemyAi.PlanUnit(withStack, Starter, soldier);

        Assert.Equal("wren", plain.TargetId);
        Assert.Equal(new Coord(8, 1), held.OfType<Move>().Single().To);
        Assert.Equal(new Attack("soldier-1", "hale", 1), held.OfType<Attack>().Single());
        Assert.Null(EnemyAi.StrikeOn(withStack, Starter, soldier, withStack.Find("wren")!));
        Assert.NotNull(EnemyAi.StrikeOn(withStack, Starter, soldier, withStack.Find("hale")!));
        Assert.Null(EnemyAi.GrudgeChoice(withStack, Starter, soldier));
    }

    [Fact]
    public void AGrudgeStrikeNamesThePlannersBestAlternative()
    {
        var state = Lane(hale: new Coord(4, 0), wren: new Coord(4, 2), soldier: new Coord(5, 1));
        state = state.WithUnit(state.Find("hale")! with { Hp = 1 });
        var soldier = state.Find("soldier-1")! with { Grudge = "wren" };
        state = state.WithUnit(soldier);

        var choice = EnemyAi.GrudgeChoice(state, Starter, soldier)!;

        Assert.Equal("wren", choice.SwornId);
        Assert.Equal("hale", choice.AlternativeId);
        Assert.True(choice.AlternativeScore > choice.Score, $"{choice.AlternativeScore} against {choice.Score}");
        Assert.Null(EnemyAi.GrudgeChoice(state.WithUnit(soldier with { Grudge = null }), Starter, soldier with { Grudge = null }));
    }

    [Fact]
    public void ASwornUnitLosesTwentyCritAvoidOnlyAgainstTheEnemySwornOnIt()
    {
        var state = Lane(hale: new Coord(0, 3), wren: new Coord(4, 1), soldier: new Coord(5, 1));
        var wren = state.Find("wren")!;
        var soldier = state.Find("soldier-1")!;
        var sworn = soldier with { Grudge = "wren" };

        Assert.Equal(Grudges.SwornCritAvoid, wren.ToCombatant(state, Starter, against: sworn).CritAvoidModifier);
        Assert.Equal(Grudges.SwornCritAvoid, wren.Answering(state, Starter, sworn.At, sworn).CritAvoidModifier);
        Assert.Equal(0, wren.ToCombatant(state, Starter, against: soldier).CritAvoidModifier);
        Assert.Equal(0, state.Find("hale")!.ToCombatant(state, Starter, against: sworn).CritAvoidModifier);
        Assert.Equal(0, sworn.ToCombatant(state, Starter, against: wren).CritAvoidModifier);
    }

    [Fact]
    public void ASwornEnemysStrikeAndItsCounterBothCarryTheSwornCrit()
    {
        var state = Lane(hale: new Coord(0, 3), wren: new Coord(4, 1), soldier: new Coord(5, 1));
        var swornState = state.WithUnit(state.Find("soldier-1")! with { Grudge = "wren" });

        var strike = Queries.Forecast(state, Starter, state.Find("soldier-1")!, state.Find("wren")!)!;
        var swornStrike = Queries.Forecast(swornState, Starter, swornState.Find("soldier-1")!, swornState.Find("wren")!)!;
        var counter = Queries.Forecast(state, Starter, state.Find("wren")!, state.Find("soldier-1")!)!;
        var swornCounter = Queries.Forecast(swornState, Starter, swornState.Find("wren")!, swornState.Find("soldier-1")!)!;

        Assert.True(swornStrike.Attacker.CritChance > strike.Attacker.CritChance, $"strike crit {strike.Attacker.CritChance} to {swornStrike.Attacker.CritChance}");
        Assert.True(swornCounter.Defender.CritChance > counter.Defender.CritChance, $"counter crit {counter.Defender.CritChance} to {swornCounter.Defender.CritChance}");
        Assert.Equal(counter.Attacker.CritChance, swornCounter.Attacker.CritChance);
    }
}
