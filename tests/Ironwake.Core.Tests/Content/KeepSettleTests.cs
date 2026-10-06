using Ironwake.Content;
using Ironwake.Content.Protocol;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// A save whose keep work a later menu moved (issue 1154, DECISIONS/0266): on load the work off
/// the menu is dropped and its price refunded into the purse, one line says so, and the camp
/// draws the keep instead of throwing.
/// </summary>
[Collection("console")]
public class KeepSettleTests
{
    private static GameContent Content => ContentLoader.Load(Fixture.RealContentDirectory());

    private static string TempDir() => Path.Combine(Path.GetTempPath(), "ironwake-settle-" + Guid.NewGuid().ToString("N"));

    private static int AfterTheRaid(GameContent content) =>
        content.Campaign.Maps.Select(m => m.MapId).ToList().IndexOf(content.Campaign.Keep.RaidId) + 1;

    private static CampaignRecord WithWork(GameContent content, params KeepWork[] work) =>
        CampaignRecord.Start(content, 3) with { MapIndex = AfterTheRaid(content), Purse = 100, Keep = ValueList<KeepWork>.From(work) };

    [Fact]
    public void AKeepWorkOffTheMenuIsDroppedOnLoadAndItsPriceRefunded()
    {
        var content = Content;
        var record = WithWork(content, new KeepWork("wall", new Coord(10, 3)), new KeepWork("ditch", new Coord(9, 4)));

        var loaded = ProtocolJson.ReadCampaign(ProtocolJson.Campaign(record), content);

        Assert.Equal(new[] { new KeepWork("ditch", new Coord(9, 4)) }, loaded.Keep);
        Assert.Equal(500, loaded.Purse);
        Assert.Equal(
            "The keep's menu moved since this save: dropped wall 10,3 (Rebuild a wall goes only on 10,2 10,9); 400 refunded, the purse holds 500",
            loaded.KeepSettled);
        var bare = MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), content, content.Campaign.Keep.MapId), content);
        Assert.Equal("water", loaded.KeepMap(bare, content).TerrainIdAt(new Coord(9, 4)));
    }

    [Fact]
    public void AKeepWorkOnTheMenuLoadsUnchangedWithNoLine()
    {
        var content = Content;
        var record = WithWork(content, new KeepWork("wall", new Coord(10, 2)));

        var loaded = ProtocolJson.ReadCampaign(ProtocolJson.Campaign(record), content);

        Assert.Same(record, record.SettleKeep(content));
        Assert.Equal(record.Keep, loaded.Keep);
        Assert.Equal(100, loaded.Purse);
        Assert.Null(loaded.KeepSettled);
    }

    [Fact]
    public void TheSettleLineIsNeverWrittenBackIntoASave()
    {
        var content = Content;
        var loaded = ProtocolJson.ReadCampaign(ProtocolJson.Campaign(WithWork(content, new KeepWork("wall", new Coord(10, 3)))), content);

        Assert.DoesNotContain("menu moved", ProtocolJson.Campaign(loaded));
        Assert.Null(ProtocolJson.ReadCampaign(ProtocolJson.Campaign(loaded), content).KeepSettled);
    }

    [Fact]
    public void LoadingASaveWithAMovedWallPrintsTheLineAndDrawsTheCamp()
    {
        var content = Content;
        var dir = TempDir();
        var path = Path.Combine(Path.GetTempPath(), "ironwake-settle-" + Guid.NewGuid().ToString("N") + ".script");
        try
        {
            Assert.Null(new SaveStore(dir).Save("old-wall", WithWork(content, new KeepWork("wall", new Coord(10, 3)))));
            File.WriteAllText(path, "quit\n");
            var args = new[] { "campaign", "--script", path, "--content", Fixture.RealContentDirectory(), "--saves", dir, "--load", "old-wall" };
            var code = 0;

            var output = ConsoleCapture.Run(() => code = Ironwake.Cli.Program.Main(args));

            Assert.DoesNotContain("Unhandled", output);
            Assert.Contains("The keep's menu moved since this save: dropped wall 10,3 (Rebuild a wall goes only on 10,2 10,9); 400 refunded, the purse holds 500\n", output);
            Assert.NotEqual(2, code);
        }
        finally
        {
            File.Delete(path);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
