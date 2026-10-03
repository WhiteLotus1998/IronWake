using Ironwake.Cli;
using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 844 (round 271): the camp prints the trade when the pick and a side character compete for a
/// map's open places. On the field the board names five members and Keziah; with Rook as the pick,
/// Keziah's slot is the one bare slot, and Rook and Ansgar contest it. Rules go on screen.
/// </summary>
public class ContestedPlaceTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static string Dir => Fixture.RealContentDirectory();

    private static MapDefinition Map(string id) => MapFiles.Load(MapFiles.CampaignPath(Dir, Content, id), Content);

    private static CampaignRecord AtTheField(string pick) => CampaignRecord.StartAt(Content, 844, "the_field", pick: pick);

    private static IReadOnlyList<string> Lines(CampaignRecord record, string map = "the_field") =>
        CampaignSession.ContestLines(record, Content, Map(map));

    [Fact]
    public void TheFieldNamesItsOneOpenPlaceWhenAnsgarIsOnOfferAndRookIsThePick()
    {
        var record = AtTheField("rook");

        var contest = record.ContestedPlaces(Map("the_field"), Content);

        Assert.NotNull(contest);
        Assert.Equal(1, contest.Value.Open);
        Assert.Equal(new[] { "ansgar", "rook" }, contest.Value.Contenders);
        Assert.Equal(new[] { "The Field Before the Keep has one open place: Ansgar or Rook" }, Lines(record));
    }

    [Fact]
    public void TheContestedPlaceLinePrintsInTheRosterPanel()
    {
        var panel = CampaignSession.RosterPanelLines(AtTheField("rook"), Content, Map("the_field"));

        Assert.Contains("The Field Before the Keep has one open place: Ansgar or Rook", panel);
    }

    [Fact]
    public void TheContestedPlaceLineStaysOnceAnsgarIsMet()
    {
        var met = AtTheField("rook").Meet("ansgar", Content);
        Assert.True(met.Accepted, met.Text);

        Assert.Equal(new[] { "The Field Before the Keep has one open place: Ansgar or Rook" }, Lines(met.Record));
    }

    [Fact]
    public void NoContestedPlaceWhenThePickHoldsANamedSlot()
    {
        Assert.Null(AtTheField("keziah").ContestedPlaces(Map("the_field"), Content));
        Assert.Empty(Lines(AtTheField("keziah")));
    }

    [Fact]
    public void NoContestedPlaceWithoutAPick()
    {
        Assert.Empty(Lines(AtTheField("rook") with { Pick = null }));
    }

    [Fact]
    public void NoContestedPlaceWhenTheMeetingIsRefused()
    {
        var record = AtTheField("rook");
        var full = record with { Fallen = ValueList<string>.From(Enumerable.Range(0, record.FreeBeds(Content)!.Value).Select(i => "lost" + i)) };
        Assert.False(full.Meet("ansgar", Content).Accepted);

        Assert.Empty(Lines(full));
    }

    [Fact]
    public void NoContestedPlaceOnAMapWithoutAMeeting()
    {
        Assert.Empty(Lines(CampaignRecord.StartAt(Content, 844, "sallow_grange", pick: "rook"), "sallow_grange"));
    }
}
