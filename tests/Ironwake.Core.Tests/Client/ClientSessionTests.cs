using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The thin renderer's presenter (issue 347): selection and reach, hover forecasts asked of
/// the core from any reachable tile, clicks as commands, and an enemy phase that plays one
/// event at a time.
/// </summary>
public class ClientSessionTests
{
    private static ClientSession Brackwater()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var map = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", "brackwater_cut.map"), content);
        return new ClientSession(content, BattleState.From(map, content, content.Cast, 53));
    }

    [Fact]
    public void SelectingAPlayerUnitShowsTheCoresReach()
    {
        var client = Brackwater();
        var rook = client.State.Find("rook")!;

        Assert.True(client.Select(rook.At));
        Assert.Equal(Queries.Reachable(client.State, client.Content, rook), client.Reach);
    }

    [Fact]
    public void SelectingAnEnemyOrAnEmptyTileSelectsNothing()
    {
        var client = Brackwater();
        var enemy = client.State.UnitsOf(Side.Enemy).First();

        Assert.False(client.Select(enemy.At));
        Assert.Null(client.Reach);
    }

    [Fact]
    public void HoverForecastsEachTargetFromTheHoveredTileWithTheConsolesText()
    {
        var client = Brackwater();
        var rook = client.State.Find("rook")!;
        var archer = client.State.Find("archer-2")!;
        var tile = new Coord(10, 1);
        client.Select(rook.At);

        var hover = client.Hover(tile);

        var line = Assert.Single(hover, h => h.TargetId == "archer-2");
        var forecast = Queries.Forecast(client.State, client.Content, rook, archer, tile)!;
        Assert.Equal(PlaySession.ForecastText(client.State, client.Content, rook, archer, forecast, tile, fromTile: true), line.Text);
    }

    [Fact]
    public void HoverFromATileOutOfReachShowsNothing()
    {
        var client = Brackwater();
        client.Select(client.State.Find("rook")!.At);

        Assert.Empty(client.Hover(new Coord(19, 11)));
    }

    [Fact]
    public void ClicksMoveThenAttackAndLogTheConsolesLines()
    {
        var client = Brackwater();
        client.Click(client.State.Find("rook")!.At);

        Assert.IsType<Move>(client.Click(new Coord(10, 1)));
        Assert.IsType<Attack>(client.Click(client.State.Find("archer-2")!.At));
        Assert.Equal("Rook moves 8,2 -> 10,1 via 8,1 9,1", client.Log[0]);
        Assert.StartsWith("Rook attacks Archer 2\n", client.Log[1]);
        Assert.Null(client.Selected);
    }

    [Fact]
    public void ClickingTheSelectedUnitWaits()
    {
        var client = Brackwater();
        var at = client.State.Find("captain")!.At;
        client.Click(at);

        Assert.IsType<Wait>(client.Click(at));
        Assert.True(client.State.Find("captain")!.Acted);
    }

    [Fact]
    public void ARefusedCommandGoesToTheStatusNotTheLog()
    {
        var client = Brackwater();

        Assert.False(client.Submit(new Move("rook", new Coord(19, 11))));
        Assert.NotNull(client.Status);
        Assert.Empty(client.Log);
    }

    [Fact]
    public void TheEnemyPhasePlaysOneEventAtATime()
    {
        var client = Brackwater();
        Assert.True(client.Submit(new EndPhase()));
        var before = client.Log.Count;

        Assert.True(client.EnemyPhasePlaying);
        Assert.Equal(Side.Enemy, client.State.Phase);
        Assert.True(client.Step());
        Assert.Equal(before + 1, client.Log.Count);
        Assert.True(client.Step());
        Assert.Equal(before + 2, client.Log.Count);
    }

    [Fact]
    public void NoPlayerCommandIsTakenWhileTheEnemyPhasePlays()
    {
        var client = Brackwater();
        client.Submit(new EndPhase());

        Assert.False(client.Submit(new Wait("captain")));
        Assert.Equal("The enemy phase is playing; step or continue", client.Status);
        Assert.Null(client.Click(client.State.Find("captain")!.At));

        client.Continue();
        Assert.False(client.EnemyPhasePlaying);
        Assert.False(client.Step());
        Assert.Equal(Side.Player, client.State.Phase);
        Assert.True(client.Submit(new Wait("captain")));
    }
}
