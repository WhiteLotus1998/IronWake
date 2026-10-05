using static Ironwake.Core.Tests.Battle.BattleFixture;

namespace Ironwake.Core.Tests.Battle;

/// <summary>
/// Issue 1106: <c>forecast</c> and <c>attack</c> name the sleeping groups the fight's noise would wake,
/// in <c>threat</c>'s words, and the tiles that are heard: the attacker's, the target's or both, at the
/// radius the wind bends. Read from the wake check with the resolver's own tiles and radius; a group the
/// stop alone wakes is <c>threat</c>'s line and is not repeated; the protocol's forecast carries <c>wakes</c>.
/// </summary>
public class FightWakesTests
{
    private static string Field(string header) =>
        $"""
        name: Field
        size: 16x7
        win: rout
        turn_limit: 10
        recall: 3
        enemy_level: 1
        {header}

        ................
        ................
        ................
        ................
        ................
        ................
        ................

        units:
        P captain 4,0
        E brigand 2,3 group:band behavior:hold
        E soldier 9,3 group:watch behavior:guard
        E soldier 15,6 group:yard behavior:guard

        """.Replace("\n\n\n", "\n\n");

    private static BattleState Start(string header = "") => BattleFixture.Start(map: Field(header));

    private static BattleUnit Captain(BattleState state) => state.Find("hale")!;

    /// <summary>The state with the brigand standing on <paramref name="at"/>.</summary>
    private static BattleState TargetOn(BattleState state, Coord at) => state.WithUnit(state.UnitsOf(Side.Enemy).First(u => u.Group == "band") with { At = at });

    private static BattleUnit Target(BattleState state) => state.UnitsOf(Side.Enemy).First(u => u.Group == "band");

    private static string Text(BattleState state, Coord tile)
    {
        var captain = Captain(state);
        var target = Target(state);
        var forecast = Queries.Forecast(state, Starter, captain, target, tile)!;
        return Ironwake.Cli.PlaySession.ForecastText(state, Starter, captain, target, forecast, tile, fromTile: true);
    }

    [Fact]
    public void AFightHeardOnlyFromTheAttackersTileNamesTheGroupAndThatTile()
    {
        var state = Start();
        Assert.Equal(6, Starter.NoiseRadius);

        var wakes = Queries.FightWakes(state, Starter, Captain(state), Target(state), new Coord(3, 3));

        Assert.Equal(new[] { ("watch", (string?)null, "3,3") }, wakes.Select(w => (w.Group, w.CalledBy, string.Join(" ", w.HeardFrom))));
        Assert.Contains("\n  Fighting here wakes: the watch group (noise, heard from 3,3)", Text(state, new Coord(3, 3)));
    }

    [Fact]
    public void AFightHeardFromBothTilesNamesBoth()
    {
        var state = TargetOn(Start(), new Coord(5, 3));

        Assert.Contains("\n  Fighting here wakes: the watch group (noise, heard from 4,3 and 5,3)", Text(state, new Coord(4, 3)));
    }

    [Fact]
    public void AFightOutOfEarshotPrintsNoWakeLine()
    {
        var state = TargetOn(Start(), new Coord(1, 0));

        Assert.Empty(Queries.FightWakes(state, Starter, Captain(state), Target(state), new Coord(2, 0)));
        Assert.DoesNotContain("ighting here wakes", Text(state, new Coord(2, 0)));
    }

    [Fact]
    public void AGroupTheStopAloneWakesIsThreatsLineNotTheForecasts()
    {
        var state = TargetOn(Start(), new Coord(4, 3));

        Assert.Single(Queries.StopWakes(state, Starter, Captain(state), new Coord(5, 3))!);
        Assert.Empty(Queries.FightWakes(state, Starter, Captain(state), Target(state), new Coord(5, 3)));
    }

    [Fact]
    public void AGroupTheFightWakesCallsItsLinkedGroup()
    {
        var state = Start("wake_links: watch>yard");

        Assert.Contains("\n  Fighting here wakes: the watch group (noise, heard from 3,3), the yard group (called by the watch group)", Text(state, new Coord(3, 3)));
    }

    [Fact]
    public void UnderTheWindATiedOffsetReadsAcrossAndAnUpwindOneIsQuiet()
    {
        // Blowing west, the watch at 9,3 is upwind of the fight: an upwind fight is heard within 4,
        // a tie (as far across as along) at the rule's 6.
        var state = Start("wind: west");

        var tied = TargetOn(state, new Coord(5, 0));
        Assert.Contains("\n  Fighting here wakes: the watch group (noise, heard from 6,0)", Text(tied, new Coord(6, 0)));

        var upwind = TargetOn(state, new Coord(4, 2));
        Assert.Empty(Queries.FightWakes(upwind, Starter, Captain(upwind), Target(upwind), new Coord(5, 2)));
        Assert.Single(Queries.FightWakes(TargetOn(Start(), new Coord(4, 2)), Starter, Captain(state), Target(state), new Coord(5, 2)));
    }

    [Fact]
    public void AnEnemysFightNamesNoWakeSinceOnlyThePlayerSideWakesAGroup()
    {
        var state = Start();
        var captain = Captain(state) with { At = new Coord(3, 3) };
        state = state.WithUnit(captain);

        Assert.Empty(Queries.FightWakes(state, Starter, Target(state), captain, Target(state).At));
        Assert.Single(Queries.FightWakes(state, Starter, captain, Target(state), captain.At));
    }

    [Fact]
    public void TheResolverWakesWhatTheForecastNamed()
    {
        var state = Start();
        var named = Queries.FightWakes(state, Starter, Captain(state), Target(state), new Coord(3, 3)).Select(w => w.Group);

        var moved = Resolver.Apply(state, Starter, new Move("hale", new Coord(3, 3))).Next;
        var fought = Resolver.Apply(moved, Starter, new Attack("hale", Target(moved).Id));

        Assert.Equal(named, fought.Events.OfType<GroupWoke>().Select(w => w.Group));
    }

    [Fact]
    public void TheProtocolsForecastAnswerCarriesTheWakes()
    {
        var state = Start();
        var target = Target(state);

        var loud = new Ironwake.Cli.ProtocolSession(Starter, state, TextWriter.Null).Answer("{\"query\":\"forecast\",\"unit\":\"hale\",\"target\":\"" + target.Id + "\",\"from\":{\"x\":3,\"y\":3}}");
        var quiet = new Ironwake.Cli.ProtocolSession(Starter, TargetOn(state, new Coord(1, 0)), TextWriter.Null).Answer("{\"query\":\"forecast\",\"unit\":\"hale\",\"target\":\"" + target.Id + "\",\"from\":{\"x\":2,\"y\":0}}");

        Assert.Contains("\"wakes\":[{\"group\":\"watch\",\"cause\":\"noise\",\"heardFrom\":[{\"x\":3,\"y\":3}]}]", loud);
        Assert.Contains("\"wakes\":[]", quiet);
    }
}
