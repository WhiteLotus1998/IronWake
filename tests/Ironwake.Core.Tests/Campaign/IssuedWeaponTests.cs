using Ironwake.Content;
using Ironwake.Core.Tests.Content;
using Ironwake.Core.Tests.Maps;

namespace Ironwake.Core.Tests.Campaign;

/// <summary>
/// Issue 804 slice 2 (round 263, DESIGN 13.23): the campaign issues Kinsbane on the pick.
/// <c>campaign.json</c>'s <c>issues</c> names a weapon for a cast member, and
/// <see cref="CampaignRecord.Kitted"/> puts it in front of their pack in place of the first weapon of its
/// type, so Keziah joins with the scythe and not her iron axe. A passed Keziah comes back on the field
/// fed to the fourth tooth's count. The cast file is unchanged, so a battle outside the campaign carries none.
/// </summary>
public class IssuedWeaponTests
{
    private static readonly GameContent Content = MapFixture.Content;

    private static string Dir => Fixture.RealContentDirectory();

    private static ItemStack Fresh(string id) => new(id, Content.Weapon(id).Durability);

    [Fact]
    public void TheShippedCampaignIssuesKinsbaneToKeziah()
    {
        Assert.Equal(new CampaignIssue("keziah", "kinsbane"), Assert.Single(Content.Campaign.Issues));
    }

    [Fact]
    public void KittedPutsTheIssuedWeaponInFrontInPlaceOfHerIronAxe()
    {
        var keziah = CampaignRecord.Kitted(Content.Unit("keziah"), Content);

        Assert.Equal(new[] { Fresh(Kinsbane.ItemId), new ItemStack("iron_gauntlets", Content.Weapon("iron_gauntlets").Durability) }, keziah.Inventory.Items);
        Assert.Contains(Content.Unit("keziah").Inventory.Items, s => s.ItemId == "iron_axe");
    }

    [Fact]
    public void KittedNeverIssuesTheWeaponTwice()
    {
        var once = CampaignRecord.Kitted(Content.Unit("keziah"), Content);

        Assert.Equal(once, CampaignRecord.Kitted(once, Content));
    }

    [Fact]
    public void AnyoneTheCampaignIssuesNothingIsKittedAsBefore()
    {
        Assert.Equal(Content.Unit("rook"), CampaignRecord.Kitted(Content.Unit("rook"), Content));
    }

    [Fact]
    public void PickedAtTheRaidKeziahJoinsCarryingTheScythe()
    {
        var record = CampaignRecord.StartAt(Content, 804, "ironwake_raid").PickClaimant("keziah", Content).Record;

        var keziah = record.Present(Content).Single(u => u.Id == "keziah");
        Assert.Equal(Fresh(Kinsbane.ItemId), keziah.Inventory.Items[0]);
        Assert.DoesNotContain(keziah.Inventory.Items, s => s.ItemId == "iron_axe");
    }

    [Fact]
    public void APassedKeziahReturnsOnTheFieldOneToothShortOfWaking()
    {
        var field = MapFiles.Load(MapFiles.CampaignPath(Dir, Content, "the_field"), Content);
        var opening = CampaignRecord.StartAt(Content, 804, "the_field", pick: "rook").Begin(field, Content);

        var scythe = opening.Find("keziah")!.Unit.Inventory.Items[0];
        Assert.Equal(Kinsbane.ItemId, scythe.ItemId);
        Assert.Equal(Kinsbane.FedFor(4), scythe.Fed);
        Assert.Equal(9, scythe.Fed);
        Assert.Equal(4, Kinsbane.Teeth(scythe.Fed));
        Assert.False(Kinsbane.Woken(scythe.Fed));
    }

    [Fact]
    public void APassedRookReturnsWithHerOwnPack()
    {
        var field = MapFiles.Load(MapFiles.CampaignPath(Dir, Content, "the_field"), Content);
        var opening = CampaignRecord.StartAt(Content, 804, "the_field", pick: "keziah").Begin(field, Content);

        Assert.Equal(Content.Unit("rook").Inventory.Items, opening.Find("rook")!.Unit.Inventory.Items);
    }

    private static ContentFiles With(string issues) =>
        ContentSerializer.Write(Content) with { Campaign = new ContentFile(ContentFiles.CampaignName, $$"""{ "startingPurse": 0, "certificationPrice": 0, "maps": [ { "map": "one", "reward": 0, "stock": [] } ], "issues": {{issues}} }""") };

    [Fact]
    public void IssuesRoundTripThroughTheSerializer()
    {
        var content = ContentLoader.Parse(With("""{ "keziah": "kinsbane" }"""));

        Assert.Equal(content.Campaign.Issues, ContentLoader.Parse(ContentSerializer.Write(content)).Campaign.Issues);
        Assert.Equal("kinsbane", content.Campaign.IssuedTo("keziah"));
    }

    [Theory]
    [InlineData("""{ "nobody": "kinsbane" }""", "issues.nobody", "is not a cast member")]
    [InlineData("""{ "keziah": "no_such_item" }""", "issues.keziah", "must be a weapon id in weapons.json")]
    [InlineData("""{ "keziah": 3 }""", "issues.keziah", "must be a weapon id in weapons.json")]
    [InlineData("""{ "keziah": "maud_psalter" }""", "issues.keziah", "is bound to maud")]
    public void ABadIssueIsRefusedNamingTheField(string issues, string field, string message)
    {
        var e = Assert.Throws<ContentException>(() => ContentLoader.Parse(With(issues)));

        Assert.Equal((ContentFiles.CampaignName, (string?)null, field), (e.File, e.Entry, e.Field));
        Assert.Contains(message, e.Message);
    }
}
