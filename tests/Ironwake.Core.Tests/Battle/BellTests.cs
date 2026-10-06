using Ironwake.Content;
using Ironwake.Core.Tests.Maps;
using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// The bell (DESIGN.md 13.30, experiment): a player unit on the bell rings it as its action, once a
/// battle; every enemy within its radius but a boss or the messenger answers, waking its group, marches
/// to the bell on the next enemy phase striking no one, and is roused, aggressive, once that phase ends.
/// </summary>
public class BellTests
{
    private const string Square = """
        name: Square
        size: 9x5
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        bell: 4,2 3

        .........
        .........
        .........
        .........
        .........

        units:
        {0}
        E brigand 5,2 group:near behavior:hold
        E brigand 7,2 group:far behavior:guard
        E brigand 8,4 group:out behavior:guard
        B bandit_leader 3,0 group:keep behavior:boss
        """;

    private static BattleState Start(string units = "P captain 4,2\nP recruit 0,4") =>
        BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Wren), Square.Replace("{0}", units));

    private static string IdAt(BattleState state, int x, int y) => state.UnitAt(new Coord(x, y))!.Id;

    private static BattleState PlayEnemyPhase(BattleState state)
    {
        foreach (var command in EnemyAi.Plan(state, Starter))
        {
            state = state.Do(command);
        }

        return state;
    }

    [Fact]
    public void RingingMarksEveryEnemyInTheRadiusButTheBossAsAnswering()
    {
        var start = Start();
        var near = IdAt(start, 5, 2);
        var far = IdAt(start, 7, 2);
        var result = start.Try(new Ring("hale"));

        Assert.True(result.Accepted, result.Rejection?.Message);
        Assert.Contains(new BellRang("hale", new Coord(4, 2), ValueList<string>.Of(near, far)), result.Events);
        Assert.Equal(Bell.Answering, result.Next.Find(near)!.Rung);
        Assert.Equal(Bell.Answering, result.Next.Find(far)!.Rung);
        Assert.Equal(0, result.Next.Find(IdAt(start, 8, 4))!.Rung);
        Assert.Equal(0, result.Next.Find(IdAt(start, 3, 0))!.Rung);
        Assert.True(result.Next.BellRung);
    }

    [Fact]
    public void RingingWakesTheGroupOfEveryAnswerer()
    {
        var state = Start().Do(new Ring("hale"));

        Assert.True(state.IsAwake("far"));
        Assert.False(state.IsAwake("out"));
    }

    [Fact]
    public void AnAnsweringEnemyMarchesToTheBellAndStrikesNoOne()
    {
        var start = Start();
        var near = IdAt(start, 5, 2);
        var far = IdAt(start, 7, 2);
        var enemyPhase = start.Do(new Ring("hale")).Do(new Wait("wren")).Do(new EndPhase());

        var plan = EnemyAi.Plan(enemyPhase, Starter);

        Assert.DoesNotContain(plan, c => c is Attack a && (a.UnitId == near || a.UnitId == far));
        var after = PlayEnemyPhase(enemyPhase);
        Assert.True(after.Find(far)!.At.DistanceTo(new Coord(4, 2)) < 3);
    }

    [Fact]
    public void AnEnemyBesideAPlayerUnitStrikesItWithoutTheBell()
    {
        var start = Start();
        var near = IdAt(start, 5, 2);
        var enemyPhase = start.Do(new Wait("hale")).Do(new Wait("wren")).Do(new EndPhase());

        Assert.Contains(EnemyAi.Plan(enemyPhase, Starter), c => c is Attack a && a.UnitId == near);
    }

    [Fact]
    public void WhenTheEnemyPhaseEndsAnAnswererIsRousedAndFightsAsAggressive()
    {
        var start = Start();
        var near = IdAt(start, 5, 2);
        var enemyPhase = start.Do(new Ring("hale")).Do(new Wait("wren")).Do(new EndPhase());

        var after = PlayEnemyPhase(enemyPhase);

        Assert.Equal(Side.Player, after.Phase);
        Assert.Equal(Bell.Roused, after.Find(near)!.Rung);
        Assert.Equal(Behavior.Aggressive, after.EffectiveBehavior(after.Find(near)!, Starter));
    }

    [Fact]
    public void AnAnswererIsNoThreatUntilItIsRoused()
    {
        var start = Start();
        var near = start.UnitAt(new Coord(5, 2))!;
        var rung = start.Do(new Ring("hale"));

        Assert.NotNull(EnemyAi.StrikeOn(start, Starter, near, start.Find("hale")!));
        Assert.Null(EnemyAi.StrikeOn(rung, Starter, rung.Find(near.Id)!, rung.Find("hale")!));
    }

    [Fact]
    public void RingingIsTheUnitsAction()
    {
        var state = Start().Do(new Ring("hale"));

        Assert.True(state.Find("hale")!.Acted);
        Assert.Equal(RejectionReason.AlreadyActed, state.Refused(new Wait("hale")).Reason);
    }

    [Fact]
    public void RingingMayFollowAMove()
    {
        var state = Start("P captain 4,1\nP recruit 0,4").Do(new Move("hale", new Coord(4, 2))).Do(new Ring("hale"));

        Assert.True(state.BellRung);
    }

    [Fact]
    public void RingingIsRefusedOffTheBell()
    {
        var refusal = Start("P captain 4,1\nP recruit 0,4").Refused(new Ring("hale"));

        Assert.Equal(RejectionReason.CannotRing, refusal.Reason);
        Assert.Contains("the bell is on 4,2", refusal.Message);
    }

    [Fact]
    public void RingingIsRefusedOnceTheBellHasRung()
    {
        var state = Start().Do(new Ring("hale"));
        var again = state.WithUnit(state.Find("hale")! with { Acted = false, Moved = false });

        var refusal = again.Refused(new Ring("hale"));

        Assert.Equal(RejectionReason.CannotRing, refusal.Reason);
        Assert.Contains("already been rung", refusal.Message);
    }

    [Fact]
    public void RingingIsRefusedOnAMapWithoutABell()
    {
        var state = BattleFixture.Start(7, ValueList<Unit>.Of(Hale, Wren), Square.Replace("bell: 4,2 3\n", "").Replace("{0}", "P captain 4,2\nP recruit 0,4"));

        var refusal = state.Refused(new Ring("hale"));

        Assert.Equal(RejectionReason.CannotRing, refusal.Reason);
        Assert.Contains("no bell", refusal.Message);
    }

    [Fact]
    public void AnEnemyNeverRings()
    {
        var state = Start("P captain 0,0\nP recruit 0,4");
        var brigand = state.UnitAt(new Coord(5, 2))! with { At = new Coord(4, 2) };

        Assert.NotNull(Bell.Refusal(state.WithUnit(brigand), brigand));
    }

    [Fact]
    public void TheLegalCommandsOfAUnitOnTheBellIncludeTheRing()
    {
        var state = Start();

        Assert.Contains(new Ring("hale"), Resolver.Legal(state, Starter));
        Assert.DoesNotContain(new Ring("wren"), Resolver.Legal(state, Starter));
    }

    [Fact]
    public void TheBoardNamesTheBellAndThenItsAnswerers()
    {
        var start = Start();
        var rung = start.Do(new Ring("hale"));

        Assert.Equal("bell (ring, an action, once): 4,2; every enemy but a boss within 3 wakes and marches to it next enemy phase, striking no one", Bell.Line(start));
        Assert.Equal($"bell on 4,2: rung; answering this enemy phase, striking no one: {IdAt(start, 5, 2)} {IdAt(start, 7, 2)}", Bell.Line(rung));
    }

    [Fact]
    public void ABellRoundTripsThroughTheMapFormat()
    {
        var map = MapFixture.Parse(Square.Replace("{0}", "P captain 4,2"));

        Assert.Equal(new AlarmBell(new Coord(4, 2), 3), map.Bell);
        Assert.Equal(map, MapFixture.Parse(MapFormat.Write(map, MapFixture.Content)));
    }

    [Theory]
    [InlineData("bell: 4,2", "bell needs a tile and a radius")]
    [InlineData("bell: 9,2 3", "outside the 9x5 grid")]
    [InlineData("bell: 4,2 0", "bell radius must be 1 to")]
    public void AMalformedBellIsRefused(string line, string message)
    {
        var ex = Assert.Throws<MapException>(() => MapFixture.Parse(Square.Replace("bell: 4,2 3", line).Replace("{0}", "P captain 4,2")));

        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public void ABellOnGroundNoOneCanEnterIsRefused()
    {
        var text = Square.Replace("{0}", "P captain 0,0").Replace("bell: 4,2 3", "bell: 4,1 3").Replace(".........\n.........\n.........", ".........\n....#....\n.........");

        var ex = Assert.Throws<MapException>(() => MapFixture.Parse(text));

        Assert.Contains("bell at 4,1 stands on", ex.Message);
    }
}
