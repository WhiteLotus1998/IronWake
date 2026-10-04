using Ironwake.Cli;
using Ironwake.Content;

namespace Ironwake.Core.Tests.Content;

/// <summary>
/// The field's <c>seen_far: rook 2</c> (issue 973): on Rook's pick the campaign seats her in the
/// slot Keziah's absence leaves bare, with no other bench, and refuses to bench her; on Keziah's
/// pick, where Rook is not with the company, the deployment is the one the map gave before the header.
/// </summary>
public class FieldSeenFarTests
{
    private static readonly GameContent Content = ContentLoader.Load(Fixture.RealContentDirectory());

    private static MapDefinition Map(CampaignRecord record) =>
        CampaignSession.MapFor(Fixture.RealContentDirectory(), Content, record, record.NextMap(Content).MapId);

    private static CampaignRecord Field(string pick) => CampaignRecord.StartAt(Content, 973, "the_field", pick: pick);

    [Fact]
    public void TheFieldSeesRookFromTwoTilesFarther()
    {
        Assert.Equal(new SeenFar("rook", 2), Map(Field("rook")).SeenFar);
    }

    [Fact]
    public void OnRooksPickRookIsSeatedWithoutAnyOtherBench()
    {
        var record = Field("rook");
        var map = Map(record);

        Assert.Contains("rook", record.Deployment(map, Content));
        Assert.DoesNotContain("rook", record.Deployment(map with { SeenFar = null }, Content));
        Assert.Equal(new Coord(1, 13), record.Begin(map, Content).Find("rook")!.At);
    }

    [Fact]
    public void OnRooksPickBenchingRookIsRefusedNamingWhy()
    {
        var record = Field("rook");

        var bench = record.Bench("rook", Map(record), Content);

        Assert.False(bench.Accepted);
        Assert.Equal(record, bench.Record);
        Assert.Contains("The Field Before the Keep sees Rook from far off; Rook flies here", bench.Text);
        Assert.True(record.Bench("rook", Map(record) with { SeenFar = null }, Content).Accepted);
    }

    [Fact]
    public void OnKeziahsPickTheDeploymentIsUnchanged()
    {
        var record = Field("keziah");
        var map = Map(record);

        Assert.Equal(record.Deployment(map with { SeenFar = null }, Content), record.Deployment(map, Content));
        Assert.DoesNotContain(record.Begin(map, Content).UnitsOf(Side.Player), u => u.Id == "rook");
    }
}
