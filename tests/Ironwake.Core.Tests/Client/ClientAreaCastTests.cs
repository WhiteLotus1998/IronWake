using Ironwake.Cli;
using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;

namespace Ironwake.Core.Tests.Client;

/// <summary>
/// Issue 1392: an area cast reached with the mouse. Spark Storm is a row of the action list,
/// <c>Item: Spark Storm (area)</c>, that arms a pick of every tile the core takes the cast at; the
/// hover reads <see cref="AreaCast.Preview"/> on any tile, a refusal included, and marks the area;
/// a click on a marked tile casts. The parity gate drives the journaled Tollgate storm play
/// (DECISIONS/0348) through the row and its click.
/// </summary>
[Collection("console")]
public class ClientAreaCastTests
{
    private const ulong Seed = 1329;

    private static string Repo => Directory.GetParent(Fixture.RealContentDirectory())!.FullName;

    private static string MapPath => Path.Combine(Fixture.RealContentDirectory(), "maps", "the_tollgate.map");

    private static string ScriptPath => Path.Combine(Repo, "docs", "transcripts", "2026-10-08-the_tollgate-1329-storm.script");

    private const string Label = "Item: Spark Storm (area)";

    /// <summary>The Tollgate on seed 1329 with the journaled play's first turn taken and Pell moved to 5,7.</summary>
    private static ClientSession BeforeTheFirstCast()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var client = new ClientSession(content, BattleState.From(MapFiles.Load(MapPath, content), content, content.Cast, Seed));
        Script.Apply(client, "move pell 4,8\nmove wren 4,10\nmove teodor 8,10\nmove captain 7,10\nend\nmove pell 5,7");
        client.Select(new Coord(5, 7));
        return client;
    }

    private static string ConsoleLog(string script)
    {
        var log = Path.Combine(Path.GetTempPath(), $"ironwake-parity-{Guid.NewGuid():N}.log");
        try
        {
            ConsoleCapture.Run(() => PlaySession.Run(new[]
            {
                MapPath, "--seed", Seed.ToString(), "--script", script,
                "--content", Fixture.RealContentDirectory(), "--log", log,
            }));
            return File.ReadAllText(log);
        }
        finally
        {
            File.Delete(log);
        }
    }

    [Fact]
    public void ClientEventLogMatchesTheConsoleWithAnAreaCastTakenByClicks()
    {
        var console = ConsoleLog(ScriptPath);
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var client = new ClientSession(content, BattleState.From(MapFiles.Load(MapPath, content), content, content.Cast, Seed));
        Script.ApplyByClicks(client, File.ReadAllText(ScriptPath));

        Assert.Contains(" casts Spark Storm at ", console);
        Assert.Null(Parity.FirstDifference(console, client.LogText));
    }

    [Fact]
    public void AnAreaTomeHasAnAreaRowThatArmsAPickOfTheTilesTheCoreTakes()
    {
        var client = BeforeTheFirstCast();

        var row = Assert.Single(client.Actions(), r => r.Label == Label);
        Assert.True(row.Legal);
        var pick = Assert.IsType<ItemPick>(row.Pick);
        Assert.True(pick.Marks(new Coord(5, 5)));
        Assert.False(pick.Marks(new Coord(5, 0)));
        Assert.All(pick.Targets.Keys, at => Assert.True(Resolver.Apply(client.State, client.Content, pick.At(at)!).Accepted));
    }

    [Fact]
    public void TheAreaHoverReadsTheConsolesPreviewAndItsRefusal()
    {
        var client = BeforeTheFirstCast();
        client.TakeAction(client.Actions().ToList().FindIndex(r => r.Label == Label));
        var caster = client.State.Find("pell")!;
        var spell = client.Content.Weapon(caster.Unit.Inventory.Items[1].ItemId);

        var preview = Assert.Single(client.PickPreview(new Coord(5, 5)));
        Assert.Equal(AreaCast.Preview(client.State, client.Content, caster, spell, new Coord(5, 5)), preview);
        Assert.StartsWith("Spark Storm at 5,5 strikes archer-2 ", preview);

        Assert.Contains("tiles from pell", Assert.Single(client.PickPreview(new Coord(5, 0))));
        Assert.Empty(client.PickPreview(new Coord(-1, 0)));
    }

    [Fact]
    public void TheAreaHoverMarksEveryTileWithinTheRadiusOfAMarkedTile()
    {
        var client = BeforeTheFirstCast();
        client.TakeAction(client.Actions().ToList().FindIndex(r => r.Label == Label));
        var radius = client.Content.Weapon(client.State.Find("pell")!.Unit.Inventory.Items[1].ItemId).Area;

        var area = client.PickArea(new Coord(5, 5));
        Assert.Contains(new Coord(5, 5), area);
        Assert.All(area, at => Assert.True(at.DistanceTo(new Coord(5, 5)) <= radius));
        Assert.Contains(client.State.Find("archer-2")!.At, area);
        Assert.Empty(client.PickArea(new Coord(5, 0)));
    }

    [Fact]
    public void AClickOnAMarkedTileCasts()
    {
        var client = BeforeTheFirstCast();
        client.TakeAction(client.Actions().ToList().FindIndex(r => r.Label == Label));
        var word = ((ItemPick)client.Pick!).Targets[new Coord(5, 5)];

        Assert.Equal(new UseItem("pell", 1, word), client.Click(new Coord(5, 5)));
        Assert.Null(client.Pick);
        Assert.Contains("Pell casts Spark Storm at 5,5", client.LogText);
    }

    [Fact]
    public void TheAreaRowIsGreyedWithNoEnemyInReach()
    {
        var content = ContentLoader.Load(Fixture.RealContentDirectory());
        var client = new ClientSession(content, BattleState.From(MapFiles.Load(MapPath, content), content, content.Cast, Seed));
        client.Select(client.State.Find("pell")!.At);

        var row = Assert.Single(client.Actions(), r => r.Label == Label);
        Assert.Equal("no target in reach", row.Refusal);
    }
}
