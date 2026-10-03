using Ironwake.Client;
using Ironwake.Content;
using Ironwake.Content.Protocol;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 871 (Lotus's ruling): the drain runs with no enemy near, so a map whose walk drains her
/// carries <c>keziah_warning: on</c>, and a campaign's bare <c>march</c> asks Lotus's question once
/// when the hungering weapon's bearer deploys there. <c>march sure</c> answers it; benching her
/// removes it. The flag is refused where there is no choice to warn about.
/// </summary>
public class KeziahWarningTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static string Dir => Fixture.RealContentDirectory();

    private static MapDefinition Map(string id) => MapFiles.Load(MapFiles.CampaignPath(Dir, Content, id), Content);

    private static MapDefinition Flagged() => Map("sallow_grange") with { KeziahWarning = true };

    private static MapDefinition Unflagged() => Map("sallow_grange") with { KeziahWarning = false };

    private static string Sample(string name) => Path.Combine(Directory.GetParent(Dir)!.FullName, "docs", "samples", name);

    /// <summary>A Keziah run at Sallow with the back of the order benched until she deploys.</summary>
    private static CampaignRecord WithKeziah()
    {
        var record = CampaignRecord.StartAt(Content, 871, "sallow_grange", pick: "keziah");
        while (!record.Deployment(Flagged(), Content).Contains("keziah"))
        {
            var before = record;
            foreach (var id in record.Deployment(Flagged(), Content).Reverse())
            {
                if (record.Bench(id, Flagged(), Content) is { Accepted: true } benched)
                {
                    record = benched.Record;
                    break;
                }
            }

            Assert.NotSame(before, record);
        }

        return record;
    }

    [Fact]
    public void TheBearerIsWhoeverKinsbaneIsBoundTo()
    {
        Assert.Equal("keziah", Kinsbane.Bearer(Content));
    }

    [Fact]
    public void ABareMarchAsksLotussQuestionOnAFlaggedMapWithHerDeployed()
    {
        var record = WithKeziah();
        Assert.Contains("keziah", record.Deployment(Flagged(), Content));

        Assert.Equal("This map is not ideal for Keziah. Are you sure you want to continue with her?", record.MarchWarning(Flagged(), Content));
    }

    [Fact]
    public void BenchingHerRemovesTheQuestion()
    {
        var benched = WithKeziah().Bench("keziah", Flagged(), Content);
        Assert.True(benched.Accepted);

        Assert.Null(benched.Record.MarchWarning(Flagged(), Content));
    }

    [Fact]
    public void AnUnflaggedMapNeverAsks()
    {
        Assert.Null(WithKeziah().MarchWarning(Unflagged(), Content));
    }

    [Fact]
    public void TheQuestionIsAskedOncePerMapSoARetryNeverAsksAgain()
    {
        var confirmed = WithKeziah().ConfirmWarning();

        Assert.Equal(confirmed.MapIndex, confirmed.WarningConfirmed);
        Assert.Null(confirmed.MarchWarning(Flagged(), Content));
        Assert.NotNull((confirmed with { WarningConfirmed = confirmed.MapIndex - 1 }).MarchWarning(Flagged(), Content));
    }

    [Fact]
    public void TheRecordCarriesTheAnsweredMapThroughTheProtocol()
    {
        var confirmed = WithKeziah().ConfirmWarning();
        var json = ProtocolJson.Campaign(confirmed);

        Assert.Contains("\"warningConfirmed\":" + confirmed.MapIndex, json, StringComparison.Ordinal);
        Assert.Equal(confirmed.WarningConfirmed, ProtocolJson.ReadCampaign(json, Content).WarningConfirmed);
        Assert.DoesNotContain("warningConfirmed", ProtocolJson.Campaign(WithKeziah()), StringComparison.Ordinal);
    }

    [Fact]
    public void TheHeaderRoundTripsThroughTheCanonicalWriter()
    {
        var text = MapFormat.Write(Flagged(), Content);
        Assert.Contains("keziah_warning: on\n", text, StringComparison.Ordinal);

        Assert.True(MapFormat.Parse("flagged.map", text, Content).KeziahWarning);
        Assert.DoesNotContain("keziah_warning", MapFormat.Write(Unflagged(), Content), StringComparison.Ordinal);
    }

    [Fact]
    public void TheFlagIsRefusedOnADeployAllMap()
    {
        var keep = MapFiles.Load(Sample("ironwake_keep_finale.map"), Content);
        Assert.True(keep.DeploysAll);
        var text = MapFormat.Write(keep, Content).Replace("deploy: all\n", "deploy: all\nkeziah_warning: on\n", StringComparison.Ordinal);

        var error = Assert.Throws<MapException>(() => MapFormat.Parse("keep.map", text, Content));
        Assert.Contains("keziah_warning: on is refused on a deploy: all map", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFlagIsRefusedOnAMapThatPlacesHerByName()
    {
        var sample = MapFiles.Load(Sample("the_gleaning_kinsbane.map"), Content);
        var text = MapFormat.Write(sample with { KeziahWarning = true }, Content);

        var error = Assert.Throws<MapException>(() => MapFormat.Parse("gleaning.map", text, Content));
        Assert.Contains("keziah_warning: on is refused on a map that places 'keziah' by name", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFlagIsRefusedOnHerOwnSideMap()
    {
        Assert.Equal("keziah_warning: on is refused on 'keziah''s own side map", Kinsbane.WarningRefusal(Flagged(), Content, questMember: "keziah"));
        Assert.Null(Kinsbane.WarningRefusal(Flagged(), Content, questMember: "maud"));
        Assert.Null(Kinsbane.WarningRefusal(Unflagged(), Content, questMember: "keziah"));
    }

    [Fact]
    public void TheClientRefusesABareMarchUntilMarchSure()
    {
        var record = CampaignRecord.StartAt(Content, 871, "sallow_grange", pick: "keziah");
        var client = new CampaignClient(Content, Dir, record);
        Assert.True(client.Bench("dunstan"));
        Assert.True(client.Bench("maud"));

        Assert.False(client.March());
        Assert.Contains("This map is not ideal for Keziah.", client.Status, StringComparison.Ordinal);
        Assert.Null(client.Battle);
        Assert.True(client.March(sure: true));
        Assert.NotNull(client.Battle);
        Assert.Equal(record.MapIndex, client.Record.WarningConfirmed);
    }
}
