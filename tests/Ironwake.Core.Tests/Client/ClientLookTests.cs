using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// The presenter's two additions for showcase slice 1 (issue 511): the move preview's one line
/// for a tile with no strike to price, and the enemy strike set the threat hatch draws, which
/// never counts an enemy the dark hides.
/// </summary>
public class ClientLookTests
{
    private static ClientSession Open(string map, ulong seed)
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var file = MapFiles.Load(Path.Combine(Fixture.RealContentDirectory(), "maps", map + ".map"), content);
        return new ClientSession(content, BattleState.From(file, content, content.Cast, seed));
    }

    [Fact]
    public void TheMovePreviewIsOneLineOfTileMoveAvoidAndVerdict()
    {
        var client = Open("the_tollgate", 113);
        client.Select(new Coord(6, 11));

        var preview = client.Preview(new Coord(4, 9))!;

        Assert.True(preview.Safe);
        Assert.Equal("Plain 4,9  move 4 of 4  evade 0  safe here", preview.Text);
    }

    /// <summary>The Tollgate at seed 113 on turn 3 with Teodor selected on 8,7: the woods group awake and in reach (the review frame's board).</summary>
    private static ClientSession TurnThree()
    {
        var client = Open("the_tollgate", 113);
        var script = File.ReadAllLines(Path.Combine(Directory.GetParent(Fixture.RealContentDirectory())!.FullName, "docs", "screenshots", "the_tollgate-113-turn3.script"));
        Ironwake.Client.Script.Apply(client, string.Join("\n", script));
        Assert.True(client.Select(new Coord(8, 7)));
        return client;
    }

    [Fact]
    public void TheMovePreviewCountsTheStrikesThreatPricesOnTheTile()
    {
        var client = TurnThree();
        var teodor = client.State.Find("teodor")!;
        var tile = client.Reach!.Destinations.First(at => client.Hover(at).Count == 0
            && Queries.Threats(client.State, client.Content, teodor, at)!.Count > 0);
        var lines = Queries.Threats(client.State, client.Content, teodor, tile)!;

        var preview = client.Preview(tile)!;

        Assert.False(preview.Safe);
        Assert.Equal(lines.Count, preview.Strikers);
        Assert.Equal(Queries.IfAllLand(lines), preview.IfAllLand);
        Assert.Contains($"for {preview.IfAllLand}", preview.Text);
    }

    [Fact]
    public void TheMovePreviewGivesWayToTheForecastWhenThereIsAStrikeToPrice()
    {
        var client = TurnThree();
        var strike = new Coord(7, 5);

        Assert.NotEmpty(client.Hover(strike));
        Assert.Null(client.Preview(strike));
    }

    [Fact]
    public void TheMovePreviewIsNullWithNoSelectionAndOffTheReach()
    {
        var client = Open("the_tollgate", 113);
        Assert.Null(client.Preview(new Coord(4, 9)));

        client.Select(new Coord(6, 11));
        Assert.Null(client.Preview(new Coord(0, 0)));
    }

    [Fact]
    public void TheThreatHatchIsTheStrikeSetOfEverySeenEnemy()
    {
        var client = Open("the_tollgate", 113);
        var expected = client.State.UnitsOf(Side.Enemy)
            .SelectMany(enemy => Threat.StruckByUnit(client.State, client.Content, enemy))
            .ToHashSet();

        Assert.NotEmpty(expected);
        Assert.True(expected.SetEquals(client.EnemyThreat));
    }

    [Fact]
    public void TheThreatHatchNeverCountsAnEnemyTheDarkHides()
    {
        var client = Open("brackwater_cut", 7);
        var hidden = client.State.UnitsOf(Side.Enemy).Where(enemy => !Dusk.Seen(client.State, enemy)).ToList();
        var seen = client.State.UnitsOf(Side.Enemy).Where(enemy => Dusk.Seen(client.State, enemy))
            .SelectMany(enemy => Threat.StruckByUnit(client.State, client.Content, enemy))
            .ToHashSet();

        Assert.NotEmpty(hidden);
        Assert.True(seen.SetEquals(client.EnemyThreat));
        var lit = hidden.SelectMany(enemy => Threat.StruckByUnit(client.State, client.Content, enemy)).Where(at => !seen.Contains(at));
        Assert.NotEmpty(lit);
        Assert.DoesNotContain(lit.First(), client.EnemyThreat);
    }
}
