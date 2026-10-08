using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 1357 (rounds 457 and 458): on the map the picked claimant joins, the first seat a bench
/// frees goes to her, in the benched unit's own slot; a later bench, and every later map, seats in
/// roster order. Every bench names who takes the seat, or says nobody does when the company is short.
/// </summary>
public class PickSeatTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static MapDefinition Map(string id) => MapFiles.Load(MapFiles.CampaignPath(Fixture.RealContentDirectory(), Content, id), Content);

    private static CampaignRecord PickedAtTheRaid() => CampaignRecord.StartAt(Content, 2055, "ironwake_raid").PickClaimant("keziah", Content).Record;

    [Fact]
    public void OnHerJoinMapThePickIsNotSeatedUntilABench()
    {
        Assert.DoesNotContain("keziah", PickedAtTheRaid().Deployment(Map("ironwake_raid"), Content));
    }

    [Fact]
    public void OnHerJoinMapTheFirstBenchSeatsThePickInTheBenchedUnitsSlot()
    {
        var map = Map("ironwake_raid");
        var record = PickedAtTheRaid();
        var before = record.Deployment(map, Content).ToList();
        var result = record.Bench("dunstan", map, Content);

        Assert.True(result.Accepted);
        Assert.Equal("dunstan is benched from Raid on Ironwake; keziah takes the seat", result.Text);
        Assert.Equal(before.Select(id => id == "dunstan" ? "keziah" : id), result.Record.Deployment(map, Content));
    }

    [Fact]
    public void OnHerJoinMapASecondBenchSeatsInRosterOrder()
    {
        var map = Map("ironwake_raid");
        var once = PickedAtTheRaid().Bench("dunstan", map, Content).Record;
        var twice = once.Bench("teodor", map, Content);

        Assert.Equal("teodor is benched from Raid on Ironwake; maud takes the seat", twice.Text);
        Assert.Contains("keziah", twice.Record.Deployment(map, Content));
    }

    [Fact]
    public void WithoutAPickABenchSeatsInRosterOrder()
    {
        var map = Map("ironwake_raid");
        var result = CampaignRecord.StartAt(Content, 2055, "ironwake_raid").Bench("dunstan", map, Content);

        Assert.Equal("dunstan is benched from Raid on Ironwake; maud takes the seat", result.Text);
    }

    [Fact]
    public void OnALaterMapABenchSeatsInRosterOrderNotThePick()
    {
        var map = Map("brackwater_cut");
        var record = CampaignRecord.StartAt(Content, 2055, "brackwater_cut", pick: "keziah");
        var before = record.Deployment(map, Content);
        var next = record.Present(Content).Select(u => u.Id).First(id => id != "teodor" && !before.Contains(id));
        var result = record.Bench("teodor", map, Content);

        Assert.Contains("keziah", record.Roster.Select(u => u.Id));
        Assert.NotEqual("keziah", next);
        Assert.Equal($"teodor is benched from Brackwater Cut; {next} takes the seat", result.Text);
        Assert.DoesNotContain("keziah", result.Record.Deployment(map, Content));
    }

    [Fact]
    public void AShortCompanySeatsNobodyAndSaysSo()
    {
        var map = Map("ironwake_raid");
        var record = PickedAtTheRaid();
        var seated = record.Deployment(map, Content);
        var short_ = record with { Roster = ValueList<Unit>.From(record.Roster.Where(u => seated.Contains(u.Id))) };
        var result = short_.Bench("dunstan", map, Content);

        Assert.True(result.Accepted);
        Assert.EndsWith("; keziah takes the seat", result.Text);
        var shorter = result.Record.Bench("teodor", map, Content);
        Assert.Equal("teodor is benched from Raid on Ironwake; nobody takes the seat", shorter.Text);
    }
}
