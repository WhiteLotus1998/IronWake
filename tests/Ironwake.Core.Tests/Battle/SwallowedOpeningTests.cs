using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The Kin's boss under the hill (issue 1386 slice 3b'): a <c>swallowed:</c> map places its boss already in his second
/// stage, on the stage's bar with its Def and Res, rooted, with Frozen Iron set to the stage's dose and held a phase for
/// a late stage, so the same clock the keep's swallow starts runs from the map's first enemy phases.
/// </summary>
public class SwallowedOpeningTests
{
    private const string Hill = """
        name: Hill
        size: 10x5
        win: defeat_boss
        swallowed: 9,2
        turn_limit: 10
        recall: 3
        enemy_level: 1

        ..........
        ..........
        ..........
        ..........
        ..........

        units:
        P captain 1,2
        P recruit:wren 0,4
        P recruit:ivo 0,0
        B hask_warden 9,2 group:kin behavior:guard
        E soldier 7,2 group:sworn behavior:hold
        """;

    private static KinStage Stage => Starter.Unit("hask_warden").Swallow!;

    private static BattleState Start(string map = Hill) => BattleFixture.Start(1386, ValueList<Unit>.Of(Hale, Wren, Ivo), map);

    private static BattleUnit Kin(BattleState state) => state.Units.Single(u => u.IsBoss);

    private static BattleUnit Captain(BattleState state) => state.Units.Single(u => u.IsCaptain);

    [Fact]
    public void TheHeaderRoundTripsAndNamesABossWithASecondStage()
    {
        var map = MapFixture.Parse(Hill, "hill.map");
        Assert.Equal(new Coord(9, 2), map.Swallowed);
        Assert.Contains("swallowed: 9,2\n", MapFormat.Write(map, Starter));
        Assert.Null(MapFixture.Parse(Hill.Replace("swallowed: 9,2\n", ""), "hill.map").Swallowed);

        var empty = Assert.Throws<MapException>(() => MapFixture.Parse(Hill.Replace("swallowed: 9,2", "swallowed: 8,2"), "hill.map"));
        Assert.Contains("no B line places a boss there", empty.Message);
        var sworn = Assert.Throws<MapException>(() => MapFixture.Parse(Hill.Replace("swallowed: 9,2", "swallowed: 7,2"), "hill.map"));
        Assert.Contains("is not a boss", sworn.Message);
        var plain = Assert.Throws<MapException>(() => MapFixture.Parse(Hill.Replace("B hask_warden", "B hask"), "hill.map"));
        Assert.Contains("has no swallow block", plain.Message);
        var race = Assert.Throws<MapException>(() => MapFixture.Parse(Hill.Replace("swallowed: 9,2\n", "swallowed: 9,2\nshard_race: 0,2 5\n"), "hill.map"));
        Assert.Contains("one or the other", race.Message);
        var malformed = Assert.Throws<MapException>(() => MapFixture.Parse(Hill.Replace("swallowed: 9,2", "swallowed: here"), "hill.map"));
        Assert.Contains("needs the boss's tile", malformed.Message);
    }

    [Fact]
    public void TheSwallowedBossBeginsTheMapInHisSecondStage()
    {
        var plain = Kin(Start(Hill.Replace("swallowed: 9,2\n", "")));
        var state = Start();
        var kin = Kin(state);

        Assert.False(plain.Swallowed);
        Assert.True(kin.Swallowed);
        Assert.Equal(Stage.Hp, kin.MaxHp(Starter));
        Assert.Equal(Stage.Hp, kin.Hp);
        Assert.Equal(plain.Unit.Stats.Def + Stage.Def, kin.Unit.Stats.Def);
        Assert.Equal(plain.Unit.Stats.Res + Stage.Res, kin.Unit.Stats.Res);
        Assert.Equal(Behavior.Hold, kin.Behavior);
        Assert.Equal(Stage.Dose, state.FrozenIron);
        Assert.Equal(Stage.Late, state.FrozenIronHeld);
        Assert.True(Swallow.Casts(state, Side.Enemy));
    }

    [Fact]
    public void FrozenIronRunsOnTheStagesClockFromTheFirstEnemyPhase()
    {
        var first = Start().Try(new EndPhase());
        Assert.Null(first.Rejection);
        Assert.DoesNotContain(first.Events, e => e is FrozenIronFell);
        Assert.False(first.Next.FrozenIronHeld);

        var between = first.Next.Try(new EndPhase());
        Assert.DoesNotContain(between.Events, e => e is FrozenIronFell);
        var second = between.Next.Try(new EndPhase());
        Assert.Null(second.Rejection);
        var fell = Assert.Single(second.Events.OfType<FrozenIronFell>());
        Assert.Equal(Stage.Dose, fell.Amount);
        Assert.DoesNotContain(Kin(first.Next).Id, fell.Struck);
        Assert.Contains(Captain(first.Next).Id, fell.Struck);
        Assert.Equal(Stage.Dose + Stage.Step, second.Next.FrozenIron);
    }

    [Fact]
    public void TheOpeningSurvivesTheStatesTextRoundTrip()
    {
        var state = Start();
        var text = state.Canonical();
        Assert.Contains(" swallowed", text);
        Assert.Contains("frozeniron 0 held", text);
    }

    [Fact]
    public void TheBoardNamesTheClockOnlyWhereTheMapBeganSwallowed()
    {
        var state = Start();
        var names = UnitNames.Of(state, Starter);
        Assert.Equal("Hask stands swallowed: Frozen Iron lands for 0 on every unit but him from the enemy phase after next, 3 more each time, then the Kin heals him 2", Swallow.Line(state, Starter, names));
        var lifted = state with { FrozenIronHeld = false, FrozenIron = 3 };
        Assert.Contains("lands for 3 on every unit but him at each enemy phase start", Swallow.Line(lifted, Starter, names));
        Assert.Null(Swallow.Line(state.WithoutUnit(Kin(state).Id), Starter, names));

        var keep = Start(Hill.Replace("swallowed: 9,2\n", ""));
        var taken = Swallow.Take(keep.WithUnit(Kin(keep) with { Hp = 0 }), Starter, Kin(keep).Id, new List<GameEvent>());
        Assert.True(Kin(taken).Swallowed);
        Assert.Null(Swallow.Line(taken, Starter, UnitNames.Of(taken, Starter)));
    }
}
